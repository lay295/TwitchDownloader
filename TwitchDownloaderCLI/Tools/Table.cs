using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace TwitchDownloaderCLI.Tools
{
    public sealed class Table
    {
        public enum TextAlign
        {
            Left,
            Right
        }

        private const string SEPARATOR = "│";
        private const string FILLER = "─";
        private const string TOP_LEFT = "┌";
        private const string TOP_SEPARATOR = "┬";
        private const string TOP_RIGHT = "┐";
        private const string LEFT = "├";
        private const string MIDDLE_SEPARATOR = "┼";
        private const string RIGHT = "┤";
        private const string BOTTOM_LEFT = "└";
        private const string BOTTOM_SEPARATOR = "┴";
        private const string BOTTOM_RIGHT ="┘";

        private readonly int _rowCount;
        private readonly string _emptyValue;
        private readonly List<Column> _columns = new();

        private int _version = 0;

        public Table(int rowCount, string emptyValue)
        {
            _rowCount = rowCount;
            _emptyValue = emptyValue;
        }

        public Table AddColumn(string columnName, TextAlign textAlign, string item)
        {
            Debug.Assert(_rowCount == 1, "Too many items were added to a new column.");

            var col = new[] { item };

            _columns.Add(new Column(columnName, textAlign, col));
            _version++;
            return this;
        }

        public Table AddColumn(string columnName, TextAlign textAlign, IEnumerable<string> items)
        {
            Debug.Assert(items.Count() <= _rowCount, "Too many items were added to a new column.");

            var col = new string[_rowCount];
            var i = 0;
            foreach (var item in items)
            {
                col[i] = item;
                i++;
            }

            if (i < col.Length)
            {
                Array.Fill(col, _emptyValue, i, col.Length - i);
            }

            _columns.Add(new Column(columnName, textAlign, col));
            _version++;
            return this;
        }

        public Table AddSeparator()
        {
            _columns.Add(Column.SeparatorInstance);
            _version++;
            return this;
        }

        public override string ToString()
        {
            var rowLength = _columns.Sum(x => x.IsSeparator ? SEPARATOR.Length * 2 : Math.Max(x.Name.Length, x.Items.Max(i => i.Length)));
            rowLength += _columns.Count + SEPARATOR.Length * 2 + 1;
            rowLength += Environment.NewLine.Length;

            return string.Create(rowLength * (_rowCount + 1 + 3), this, static (span, table) =>
            {
                foreach (var row in table.GetRows())
                {
                    row.CopyTo(span);
                    span = span[row.Length..];
                    Environment.NewLine.CopyTo(span);
                    span = span[Environment.NewLine.Length..];
                }
            });
        }

        public IEnumerable<string> GetRows()
        {
            var version = _version;

            // Measure the table
            var tableWidths = new int[_columns.Count];
            var w = 0;
            foreach (var (isSeparator, name, _, items) in _columns)
            {
                tableWidths[w] = isSeparator
                    ? SEPARATOR.Length
                    : Math.Max(name.Length, items.Max(x => x.Length));

                w++;
            }

            var sb = new StringBuilder(tableWidths.Sum() + tableWidths.Length + SEPARATOR.Length * 2 + 1);

            // Generate top
            WriteTableEdge(sb, _columns, tableWidths, TOP_LEFT, FILLER, TOP_SEPARATOR , TOP_RIGHT);
            yield return sb.ToString();

            sb.Clear();
            ThrowIfChanged(version);

            // Generate the names
            sb.Append(SEPARATOR);
            for (var n = 0; n < _columns.Count; n++)
            {
                var (isSeparator, name, textAlign, _) = _columns[n];

                if (isSeparator)
                {
                    sb.Append(' ');
                    sb.Append(SEPARATOR);
                    continue;
                }

                var width = tableWidths[n] * (textAlign == TextAlign.Left ? -1 : 1);
                sb.AppendFormat($" {{0,{width}}}", name);
            }

            sb.Append(' ');
            sb.Append(SEPARATOR);
            yield return sb.ToString();

            sb.Clear();
            ThrowIfChanged(version);

            // Generate name/items separator
            WriteTableEdge(sb, _columns, tableWidths, LEFT, FILLER, MIDDLE_SEPARATOR, RIGHT);
            yield return sb.ToString();

            sb.Clear();
            ThrowIfChanged(version);

            // Generate the items
            for (var i = 0; i < _rowCount; i++)
            {
                sb.Append(SEPARATOR);
                for (var c = 0; c < _columns.Count; c++)
                {
                    var (isSeparator, _, textAlign, items) = _columns[c];

                    if (isSeparator)
                    {
                        sb.Append(' ');
                        sb.Append(SEPARATOR);
                        continue;
                    }

                    var rowItem = items[i];
                    var width = tableWidths[c] * (textAlign == TextAlign.Left ? -1 : 1);
                    sb.AppendFormat($" {{0,{width}}}", rowItem);
                }

                sb.Append(' ');
                sb.Append(SEPARATOR);
                yield return sb.ToString();

                sb.Clear();
                ThrowIfChanged(version);
            }

            // Generate bottom
            WriteTableEdge(sb, _columns, tableWidths, BOTTOM_LEFT, FILLER,  BOTTOM_SEPARATOR, BOTTOM_RIGHT);
            yield return sb.ToString();

            sb.Clear();
            yield break;

            static void WriteTableEdge(StringBuilder sb, IReadOnlyList<Column> columns, IReadOnlyList<int> tableWidths, string leftEdge, string filler, string separatorConnection, string rightEdge)
            {
                sb.Append(leftEdge);
                for (var i = 0; i < tableWidths.Count; i++)
                {
                    if (columns[i].IsSeparator)
                    {
                        sb.Append(filler);
                        sb.Append(separatorConnection);
                        continue;
                    }

                    for (var j = 0; j < tableWidths[i] + 1; j++)
                    {
                        sb.Append(filler);
                    }
                }

                sb.Append(filler);
                sb.Append(rightEdge);
            }
        }

        public IEnumerable<(string name, IEnumerable<string> items)> GetColumns()
        {
            var version = _version;
            foreach (var (isSeparator, name, _, items) in _columns)
            {
                ThrowIfChanged(version);

                if (isSeparator)
                {
                    continue;
                }

                yield return (name, items);
            }
        }

        private void ThrowIfChanged(int version)
        {
            if (_version != version)
            {
                throw new InvalidOperationException("The table was changed during enumeration.");
            }
        }

        private sealed record Column
        {
            public static readonly Column SeparatorInstance = new();

            private Column()
            {
                IsSeparator = true;
            }

            public Column(string name, TextAlign textAlign, string[] items)
            {
                IsSeparator = false;
                Name = name;
                TextAlign = textAlign;
                Items = items;
            }

            public bool IsSeparator { get; }
            public string Name { get; }
            public TextAlign TextAlign { get; }
            public string[] Items { get; }

            public void Deconstruct(out bool isSeparator, out string name, out TextAlign textAlign, out string[] items)
            {
                isSeparator = IsSeparator;
                name = Name;
                textAlign = TextAlign;
                items = Items;
            }
        }
    }
}