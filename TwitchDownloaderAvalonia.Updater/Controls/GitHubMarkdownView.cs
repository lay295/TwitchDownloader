using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Markdig;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using TwitchDownloaderAvalonia.Update.Markdown;

namespace TwitchDownloaderAvalonia.Updater.Controls
{
    public sealed class GitHubMarkdownView : ContentControl
    {
        public static readonly StyledProperty<string?> MarkdownProperty =
            AvaloniaProperty.Register<GitHubMarkdownView, string?>(nameof(Markdown));

        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        static GitHubMarkdownView()
        {
            MarkdownProperty.Changed.AddClassHandler<GitHubMarkdownView>((view, _) => view.Rebuild());
        }

        public string? Markdown
        {
            get => GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        private void Rebuild()
        {
            var host = new StackPanel { Spacing = 8 };
            var markdown = Markdown;
            if (string.IsNullOrWhiteSpace(markdown))
            {
                Content = host;
                return;
            }

            var document = Markdig.Markdown.Parse(markdown, Pipeline);
            foreach (var block in document)
                AppendBlock(host, block);

            Content = host;
        }

        private void AppendBlock(StackPanel host, Block block)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    host.Children.Add(CreateHeading(heading));
                    break;
                case ParagraphBlock paragraph:
                    host.Children.Add(CreateParagraph(paragraph.Inline, 14));
                    break;
                case AlertBlock alert:
                    host.Children.Add(CreateAlert(alert.Kind.ToString(), alert, stripMarker: false));
                    break;
                case QuoteBlock quote:
                    host.Children.Add(CreateQuote(quote));
                    break;
                case ListBlock list:
                    host.Children.Add(CreateList(list));
                    break;
                case FencedCodeBlock code:
                case CodeBlock:
                    host.Children.Add(CreateCode(block is FencedCodeBlock fenced ? fenced.Lines.ToString() : ((CodeBlock)block).Lines.ToString()));
                    break;
                case Table table:
                    host.Children.Add(CreateTable(table));
                    break;
                case ThematicBreakBlock:
                    host.Children.Add(new Border
                    {
                        Height = 1,
                        Margin = new Thickness(0, 8),
                        Background = ResolveBrush("ThemeBorderLowBrush"),
                    });
                    break;
                case LinkReferenceDefinitionGroup:
                    break;
                default:
                    if (block is ContainerBlock container)
                    {
                        foreach (var child in container)
                            AppendBlock(host, child);
                    }
                    break;
            }
        }

        private Control CreateHeading(HeadingBlock heading)
        {
            var size = heading.Level switch
            {
                1 => 22d,
                2 => 18d,
                3 => 16d,
                _ => 14d,
            };
            var text = CreateParagraph(heading.Inline, size);
            text.FontWeight = FontWeight.SemiBold;
            return text;
        }

        private MarkdownTextBlock CreateParagraph(ContainerInline? inlines, double fontSize)
        {
            var text = new MarkdownTextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = fontSize,
            };
            if (inlines is not null)
                AppendInlines(text.Inlines!, inlines);
            return text;
        }

        private Control CreateQuote(QuoteBlock quote)
        {
            if (quote.FirstOrDefault() is ParagraphBlock paragraph)
            {
                var raw = paragraph.Inline?.FirstChild?.ToString() ?? string.Empty;
                if (raw.StartsWith("[!", StringComparison.Ordinal) && raw.Contains(']'))
                {
                    var end = raw.IndexOf(']');
                    var kind = raw[2..end].Trim();
                    return CreateAlert(kind, quote, stripMarker: true);
                }
            }

            var inner = new StackPanel { Spacing = 6 };
            foreach (var child in quote)
                AppendBlock(inner, child);

            return new Border
            {
                Classes = { "card" },
                Padding = new Thickness(12, 8),
                Child = inner,
            };
        }

        private Control CreateAlert(string kind, ContainerBlock body, bool stripMarker)
        {
            var accent = ResolveBrush("AccentBrush");
            var inner = new StackPanel { Spacing = 4 };
            inner.Children.Add(new TextBlock
            {
                Text = FormatAlertKind(kind),
                FontWeight = FontWeight.SemiBold,
                Foreground = accent,
            });

            var first = true;
            foreach (var child in body)
            {
                if (first && stripMarker && child is ParagraphBlock paragraph)
                {
                    first = false;
                    var cleaned = CloneWithoutAlertMarker(paragraph);
                    if (cleaned is not null)
                        inner.Children.Add(cleaned);
                    continue;
                }

                first = false;
                AppendBlock(inner, child);
            }

            return new Border
            {
                Background = ResolveBrush("CardBackgroundBrush"),
                BorderBrush = accent,
                BorderThickness = new Thickness(3, 0, 0, 0),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(12, 8),
                Child = inner,
            };
        }

        private static string FormatAlertKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
                return "Note";

            kind = kind.Trim().Trim('[', ']', '!');
            return char.ToUpperInvariant(kind[0]) + kind[1..].ToLowerInvariant();
        }

        private Control? CloneWithoutAlertMarker(ParagraphBlock paragraph)
        {
            if (paragraph.Inline is null)
                return null;

            var text = CreateParagraph(paragraph.Inline, 14);
            if (text.Inlines is { Count: > 0 } inlines && inlines[0] is Run run)
            {
                var value = run.Text ?? string.Empty;
                if (value.StartsWith("[!", StringComparison.Ordinal) && value.Contains(']'))
                {
                    var end = value.IndexOf(']');
                    run.Text = value[(end + 1)..].TrimStart();
                    if (string.IsNullOrWhiteSpace(run.Text))
                        inlines.RemoveAt(0);
                }
            }

            return string.IsNullOrWhiteSpace(text.Text) && text.Inlines is { Count: 0 } ? null : text;
        }

        private Control CreateList(ListBlock list)
        {
            var panel = new StackPanel { Spacing = 4 };
            var index = 1;
            foreach (var item in list)
            {
                if (item is not ListItemBlock listItem)
                    continue;

                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
                var marker = list.IsOrdered ? $"{index}." : "•";
                if (listItem.FirstOrDefault() is ParagraphBlock paragraph
                    && paragraph.Inline?.FirstOrDefault() is TaskList task)
                {
                    marker = task.Checked ? "☑" : "☐";
                }

                row.Children.Add(new TextBlock
                {
                    Text = marker,
                    Margin = new Thickness(0, 0, 8, 0),
                });
                var body = new StackPanel { Spacing = 4 };
                Grid.SetColumn(body, 1);
                foreach (var child in listItem)
                    AppendBlock(body, child);
                row.Children.Add(body);
                panel.Children.Add(row);
                index++;
            }

            return panel;
        }

        private Control CreateCode(string? code)
        {
            return new Border
            {
                Classes = { "card" },
                Padding = new Thickness(10),
                Child = new TextBlock
                {
                    Text = code?.TrimEnd() ?? string.Empty,
                    FontFamily = new FontFamily("Menlo, Consolas, monospace"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                },
            };
        }

        private Control CreateTable(Table table)
        {
            var grid = new Grid();
            var columnCount = 1;
            foreach (var block in table)
            {
                if (block is TableRow tableRow)
                    columnCount = Math.Max(columnCount, tableRow.Count);
            }
            for (var i = 0; i < columnCount; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var rowIndex = 0;
            foreach (var block in table)
            {
                if (block is not TableRow row)
                    continue;

                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var columnIndex = 0;
                foreach (var cellBlock in row)
                {
                    if (cellBlock is not TableCell cell)
                        continue;

                    var cellHost = new StackPanel { Margin = new Thickness(6) };
                    foreach (var child in cell)
                        AppendBlock(cellHost, child);

                    var border = new Border
                    {
                        BorderBrush = ResolveBrush("ThemeBorderLowBrush"),
                        BorderThickness = new Thickness(1),
                        Child = cellHost,
                    };
                    Grid.SetRow(border, rowIndex);
                    Grid.SetColumn(border, columnIndex);
                    grid.Children.Add(border);
                    columnIndex++;
                }

                rowIndex++;
            }

            return grid;
        }

        private void AppendInlines(InlineCollection target, ContainerInline inlines)
        {
            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case LiteralInline literal:
                        target.Add(new Run(literal.Content.ToString()));
                        break;
                    case EmphasisInline emphasis:
                        var span = new Span
                        {
                            FontWeight = emphasis.DelimiterCount >= 2 ? FontWeight.Bold : FontWeight.Normal,
                            FontStyle = emphasis.DelimiterCount == 1 ? FontStyle.Italic : FontStyle.Normal,
                        };
                        if (emphasis.DelimiterCount >= 2 && emphasis.DelimiterChar == '~')
                            span.TextDecorations = TextDecorations.Strikethrough;
                        AppendInlines(span.Inlines, emphasis);
                        target.Add(span);
                        break;
                    case CodeInline code:
                        target.Add(new Run(code.Content)
                        {
                            FontFamily = new FontFamily("Menlo, Consolas, monospace"),
                        });
                        break;
                    case LineBreakInline:
                        target.Add(new LineBreak());
                        break;
                    case LinkInline link when link.IsImage:
                        target.Add(new Run(string.IsNullOrWhiteSpace(link.Title) ? link.FirstChild?.ToString() ?? link.Url ?? string.Empty : link.Title));
                        break;
                    case LinkInline link:
                        var url = link.Url ?? string.Empty;
                        target.Add(CreateLink(GitHubMarkdownText.FormatLinkLabel(link.FirstChild?.ToString(), url), url));
                        break;
                    case AutolinkInline auto:
                        target.Add(CreateLink(GitHubMarkdownText.FormatLinkLabel(auto.Url, auto.Url), auto.Url));
                        break;
                    case ContainerInline nested:
                        AppendInlines(target, nested);
                        break;
                    default:
                        var leftover = inline.ToString();
                        if (!string.IsNullOrEmpty(leftover))
                            target.Add(new Run(leftover));
                        break;
                }
            }
        }

        private Run CreateLink(string label, string url)
        {
            var run = new Run(label)
            {
                Foreground = ResolveBrush("AccentBrush"),
            };
            MarkdownTextBlock.SetLinkUrl(run, url);
            return run;
        }

        private IBrush ResolveBrush(string key)
        {
            if (Application.Current?.TryGetResource(key, ActualThemeVariant, out var resource) == true
                && resource is IBrush brush)
            {
                return brush;
            }

            return Brushes.Gray;
        }

        private static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private sealed class MarkdownTextBlock : TextBlock
        {
            public static readonly AttachedProperty<string?> LinkUrlProperty =
                AvaloniaProperty.RegisterAttached<MarkdownTextBlock, Avalonia.Controls.Documents.Inline, string?>("LinkUrl");

            public static void SetLinkUrl(Avalonia.Controls.Documents.Inline inline, string? value) => inline.SetValue(LinkUrlProperty, value);

            public static string? GetLinkUrl(Avalonia.Controls.Documents.Inline inline) => inline.GetValue(LinkUrlProperty);

            private Run? _hovered;

            public MarkdownTextBlock()
            {
                PointerMoved += OnPointerMoved;
                PointerExited += OnPointerExited;
                PointerReleased += OnPointerReleased;
            }

            private void OnPointerMoved(object? sender, PointerEventArgs e)
            {
                var run = FindLinkRun(e.GetPosition(this));
                if (ReferenceEquals(run, _hovered))
                    return;

                ClearHover();
                _hovered = run;
                if (run is not null)
                {
                    run.Foreground = ResolveBrush("AccentHoverBrush");
                    Cursor = new Cursor(StandardCursorType.Hand);
                }
                else
                {
                    Cursor = new Cursor(StandardCursorType.Arrow);
                }
            }

            private void OnPointerExited(object? sender, PointerEventArgs e)
            {
                ClearHover();
                Cursor = new Cursor(StandardCursorType.Arrow);
            }

            private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
            {
                if (e.InitialPressMouseButton != MouseButton.Left)
                    return;

                var url = FindLinkRun(e.GetPosition(this)) is { } run ? GetLinkUrl(run) : null;
                if (string.IsNullOrWhiteSpace(url))
                    return;

                OpenUrl(url);
                e.Handled = true;
            }

            private void ClearHover()
            {
                if (_hovered is null)
                    return;

                _hovered.Foreground = ResolveBrush("AccentBrush");
                _hovered = null;
            }

            private Run? FindLinkRun(Point point)
            {
                var x = point.X - Padding.Left;
                var y = point.Y - Padding.Top;
                if (x < 0 || y < 0)
                    return null;

                var top = 0.0;
                foreach (var line in TextLayout.TextLines)
                {
                    if (y >= top && y <= top + line.Height)
                    {
                        if (x > line.WidthIncludingTrailingWhitespace)
                            return null;

                        return FindRunAt(Inlines, line.GetCharacterHitFromDistance(x).FirstCharacterIndex);
                    }

                    top += line.Height;
                }

                return null;
            }

            private static Run? FindRunAt(InlineCollection? inlines, int index)
            {
                if (inlines is null)
                    return null;

                var i = 0;
                return FindRunAt(inlines, index, ref i);
            }

            private static Run? FindRunAt(InlineCollection inlines, int index, ref int i)
            {
                foreach (var inline in inlines)
                {
                    switch (inline)
                    {
                        case Run run:
                            var length = run.Text?.Length ?? 0;
                            if (length > 0 && index >= i && index < i + length)
                                return string.IsNullOrEmpty(GetLinkUrl(run)) ? null : run;
                            i += length;
                            break;
                        case LineBreak:
                            i += 1;
                            break;
                        case Span span when span.Inlines is { Count: > 0 } nested:
                            var found = FindRunAt(nested, index, ref i);
                            if (found is not null)
                                return found;
                            break;
                    }
                }

                return null;
            }

            private static IBrush ResolveBrush(string key)
            {
                if (Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var resource) == true
                    && resource is IBrush brush)
                {
                    return brush;
                }

                return Brushes.Gray;
            }
        }
    }
}
