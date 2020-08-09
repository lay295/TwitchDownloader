using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Newtonsoft.Json;
using TwitchDownloader.Properties;
using TwitchDownloaderWPF;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloader
{
    /// <summary>
    /// Interaction logic for WindowPreview.xaml
    /// </summary>
    public partial class WindowPreview : Window
    {
        public SKPaint imagePaint = new SKPaint() { IsAntialias = true, FilterQuality = SKFilterQuality.High };

        public WindowPreview()
        {
            InitializeComponent();
        }

        public void Update(PageChatRender pageChatRender)
        {
            try
            {
                this.Width = Int32.Parse(pageChatRender.textWidth.Text) + 10;
                this.Height = Int32.Parse(pageChatRender.textHeight.Text) + 38;
                imgPreview.Height = Int32.Parse(pageChatRender.textHeight.Text);
                imgPreview.Width = Int32.Parse(pageChatRender.textWidth.Text);
                PreviewData previewData = JsonConvert.DeserializeObject<PreviewData>(File.ReadAllText("preview_data.json"));
                BlockingCollection<TwitchCommentPreview> finalComments = new BlockingCollection<TwitchCommentPreview>();
                SKBitmap previewBitmap = new SKBitmap((int)this.Width, (int)imgPreview.Height);
                SKColor backgroundColor = new SKColor(pageChatRender.colorBackground.SelectedColor.Value.R, pageChatRender.colorBackground.SelectedColor.Value.G, pageChatRender.colorBackground.SelectedColor.Value.B);
                SKColor messageColor = new SKColor(pageChatRender.colorFont.SelectedColor.Value.R, pageChatRender.colorFont.SelectedColor.Value.G, pageChatRender.colorFont.SelectedColor.Value.B);

                ChatRenderOptions renderOptions = new ChatRenderOptions()
                {
                    InputFile = pageChatRender.textJson.Text,
                    OutputFile = "",
                    BackgroundColor = backgroundColor,
                    ChatHeight = Int32.Parse(pageChatRender.textHeight.Text),
                    ChatWidth = Int32.Parse(pageChatRender.textWidth.Text),
                    BttvEmotes = (bool)pageChatRender.checkBTTV.IsChecked,
                    FfzEmotes = (bool)pageChatRender.checkFFZ.IsChecked,
                    Outline = (bool)pageChatRender.checkOutline.IsChecked,
                    Font = (string)pageChatRender.comboFont.SelectedItem,
                    FontSize = Double.Parse(pageChatRender.textFontSize.Text),
                    UpdateRate = Double.Parse(pageChatRender.textUpdateTime.Text),
                    Timestamp = (bool)pageChatRender.checkTimestamp.IsChecked,
                    MessageColor = messageColor,
                    Framerate = Int32.Parse(pageChatRender.textFramerate.Text),
                    InputArgs = Settings.Default.FfmpegInputArgs,
                    OutputArgs = Settings.Default.FfmpegOutputArgs,
                    MessageFontStyle = SKFontStyle.Normal,
                    UsernameFontStyle = SKFontStyle.Bold
                };
                System.Drawing.Size canvasSize = new System.Drawing.Size(renderOptions.ChatWidth, renderOptions.SectionHeight);
                SKPaint nameFont = new SKPaint() { Typeface = SKTypeface.FromFamilyName(renderOptions.Font, renderOptions.UsernameFontStyle), LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
                SKPaint messageFont = new SKPaint() { Typeface = SKTypeface.FromFamilyName(renderOptions.Font, renderOptions.MessageFontStyle), LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High, Color = renderOptions.MessageColor };
                List<ThirdPartyEmote> thirdPartyEmotes = new List<ThirdPartyEmote>();
                Dictionary<string, SKBitmap> chatEmotes = new Dictionary<string, SKBitmap>();
                Dictionary<string, SKBitmap> emojiCache = new Dictionary<string, SKBitmap>();

                using (SKCanvas previewCanvas = new SKCanvas(previewBitmap))
                {
                    previewCanvas.Clear(backgroundColor);

                    foreach (PreviewEmote previewDataEmote in previewData.emotes)
                    {
                        byte[] imageBytes = Convert.FromBase64String(previewDataEmote.image);
                        SKBitmap emoteBitmap = SKBitmap.Decode(imageBytes);
                        SKCodec emoteCodec;
                        using (MemoryStream ms = new MemoryStream(imageBytes))
                            emoteCodec = SKCodec.Create(ms);

                        ThirdPartyEmote emote = new ThirdPartyEmote(new List<SKBitmap>() { emoteBitmap }, emoteCodec, previewDataEmote.name, ".png", "0", 1, imageBytes);
                        thirdPartyEmotes.Add(emote);
                    }

                    foreach (PreviewComment previewComment in previewData.comments)
                    {
                        int default_x = 2;
                        System.Drawing.Point drawPos = new System.Drawing.Point(default_x, 0);
                        string userName = previewComment.name;
                        SKColor userColor = new SKColor(Convert.ToByte(previewComment.color.Substring(0, 2), 16), Convert.ToByte(previewComment.color.Substring(2, 2), 16), Convert.ToByte(previewComment.color.Substring(4, 2), 16));
                        List<SKBitmap> imageList = new List<SKBitmap>();
                        SKBitmap sectionImage = new SKBitmap(canvasSize.Width, canvasSize.Height);
                        List<GifEmote> currentGifEmotes = new List<GifEmote>();
                        List<SKBitmap> emoteList = new List<SKBitmap>();
                        List<CheerEmote> cheerEmotes = new List<CheerEmote>();
                        List<SKRect> emotePositionList = new List<SKRect>();
                        new SKCanvas(sectionImage).Clear(renderOptions.BackgroundColor);
                        Comment comment = new Comment();
                        comment.message = new Message();
                        Fragment msg = new Fragment();
                        msg.text = previewComment.message;
                        comment.message.fragments = new List<Fragment>();
                        comment.message.fragments.Add(msg);
                        if (renderOptions.Timestamp)
                            sectionImage = ChatRenderer.DrawTimestamp(sectionImage, imageList, messageFont, renderOptions, comment, canvasSize, ref drawPos, ref default_x);
                        if (previewComment.badges != null)
                            sectionImage = DrawBadges(sectionImage, imageList, renderOptions, canvasSize, ref drawPos, previewComment);
                        sectionImage = ChatRenderer.DrawUsername(sectionImage, imageList, renderOptions, nameFont, userName, userColor, canvasSize, ref drawPos);
                        sectionImage = ChatRenderer.DrawMessage(sectionImage, imageList, renderOptions, currentGifEmotes, messageFont, emojiCache, chatEmotes, thirdPartyEmotes, cheerEmotes, comment, canvasSize, ref drawPos, ref default_x, emoteList, emotePositionList);

                        int finalHeight = 0;
                        foreach (var img in imageList)
                            finalHeight += img.Height;
                        SKBitmap finalImage = new SKBitmap(canvasSize.Width, finalHeight);
                        SKCanvas finalImageCanvas = new SKCanvas(finalImage);
                        finalHeight = 0;
                        foreach (var img in imageList)
                        {
                            finalImageCanvas.DrawBitmap(img, 0, finalHeight);
                            finalHeight += img.Height;
                            img.Dispose();
                        }

                        finalComments.Add(new TwitchCommentPreview(finalImage, Double.Parse(comment.content_offset_seconds.ToString()), currentGifEmotes, emoteList, emotePositionList));
                    }

                    int y = 0;
                    int tempHeight = 0;
                    foreach (TwitchCommentPreview twitchCommentPreview in finalComments)
                        tempHeight += twitchCommentPreview.section.Height;
                    SKBitmap tempBitmap = new SKBitmap((int)this.Width, tempHeight);

                    using (SKCanvas tempCanvas = new SKCanvas(tempBitmap))
                    {
                        foreach (TwitchCommentPreview twitchCommentPreview in finalComments)
                        {
                            tempCanvas.DrawBitmap(twitchCommentPreview.section, 0, y, imagePaint);

                            for (int i = 0; i < twitchCommentPreview.normalEmotes.Count; i++)
                            {
                                SKRect refrenceRect = twitchCommentPreview.normalEmotesPositions[i];
                                tempCanvas.DrawBitmap(twitchCommentPreview.normalEmotes[i], new SKRect(refrenceRect.Left, refrenceRect.Top + y, refrenceRect.Right, refrenceRect.Bottom + y), imagePaint);
                            }

                            y += twitchCommentPreview.section.Height;
                        }
                    }

                    previewCanvas.DrawBitmap(tempBitmap, 0, previewBitmap.Height - tempBitmap.Height);
                }

                using (var stream = new MemoryStream())
                {
                    SKImage.FromBitmap(previewBitmap).Encode(SKEncodedImageFormat.Png, 100).SaveTo(stream);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    imgPreview.Source = bitmap;
                }
            }
            catch
            {
                try
                {
                    this.Width = 500;
                    this.Height = 338;
                    imgPreview.Width = 500;
                    imgPreview.Height = 300;

                    SKBitmap errorBitmap = new SKBitmap(500, 300);
                    using (SKCanvas skCanvas = new SKCanvas(errorBitmap))
                    {
                        skCanvas.DrawText("ERROR, UNABLE TO RENDER CHAT", 40, 150,
                            new SKPaint()
                            {
                                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), TextSize = 18,
                                IsAntialias = true, FilterQuality = SKFilterQuality.High
                            });
                        SKBitmap peepo = SKBitmap.Decode(Application
                            .GetResourceStream(new Uri("pack://application:,,,/Images/peepoSad.png")).Stream);
                        skCanvas.DrawBitmap(peepo, 370, 132);
                    }

                    using (var stream = new MemoryStream())
                    {
                        SKImage.FromBitmap(errorBitmap).Encode(SKEncodedImageFormat.Png, 100).SaveTo(stream);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        imgPreview.Source = bitmap;
                    }
                }
                catch
                {

                }
            }
        }

        private SKBitmap DrawBadges(SKBitmap sectionImage, List<SKBitmap> imageList, ChatRenderOptions renderOptions, System.Drawing.Size canvasSize, ref System.Drawing.Point drawPos, PreviewComment previewComment)
        {
            foreach (string badgeString in previewComment.badges)
            {
                byte[] imageBytes = Convert.FromBase64String(badgeString);
                SKBitmap badgeImage = SKBitmap.Decode(imageBytes);
                using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                {
                    float imageRatio = (float)(renderOptions.EmoteScale);
                    float imageSize = badgeImage.Width * imageRatio;
                    float left = (float)drawPos.X;
                    float right = imageSize + left;
                    float top = (float)((sectionImage.Height - imageSize) / 2);
                    float bottom = imageSize + top;
                    SKRect drawBox = new SKRect(left, top, right, bottom);
                    sectionImageCanvas.DrawBitmap(badgeImage, drawBox, imagePaint);
                    drawPos.X += (int)Math.Floor(20 * renderOptions.EmoteScale);
                }
            }

            return sectionImage;
        }
    }

    public class PreviewEmote
    {
        public string name { get; set; }
        public string image { get; set; }
    }

    public class PreviewComment
    {
        public string name { get; set; }
        public string color { get; set; }
        public string message { get; set; }
        public List<string> badges { get; set; }
    }

    public class PreviewData
    {
        public List<PreviewEmote> emotes { get; set; }
        public List<PreviewComment> comments { get; set; }
    }

    public class TwitchCommentPreview
    {
        public SKBitmap section;
        public double secondsOffset;
        public List<GifEmote> gifEmotes;
        public List<SKBitmap> normalEmotes;
        public List<SKRect> normalEmotesPositions;

        public TwitchCommentPreview(SKBitmap Section, double SecondsOffset, List<GifEmote> GifEmotes, List<SKBitmap> NormalEmotes, List<SKRect> NormalEmotesPositions)
        {
            section = Section;
            secondsOffset = SecondsOffset;
            gifEmotes = GifEmotes;
            normalEmotes = NormalEmotes;
            normalEmotesPositions = NormalEmotesPositions;
        }
    }
}
