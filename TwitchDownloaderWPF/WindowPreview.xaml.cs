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
                RenderOptions renderOptions = new RenderOptions(pageChatRender.textJson.Text, "", backgroundColor, Int32.Parse(pageChatRender.textHeight.Text), Int32.Parse(pageChatRender.textWidth.Text), (bool)pageChatRender.checkBTTV.IsChecked, (bool)pageChatRender.checkFFZ.IsChecked, (bool)pageChatRender.checkOutline.IsChecked, (string)pageChatRender.comboFont.SelectedItem, Double.Parse(pageChatRender.textFontSize.Text), Double.Parse(pageChatRender.textUpdateTime.Text), (bool)pageChatRender.checkTimestamp.IsChecked, messageColor, Int32.Parse(pageChatRender.textFramerate.Text), Settings.Default.FfmpegInputArgs, Settings.Default.FfmpegOutputArgs);
                Size canvasSize = new Size(renderOptions.chat_width, renderOptions.text_height);
                SKPaint nameFont = new SKPaint() { Typeface = SKTypeface.FromFamilyName(renderOptions.font, SKFontStyle.Bold), LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.font_size, IsAntialias = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
                SKPaint messageFont = new SKPaint() { Typeface = SKTypeface.FromFamilyName(renderOptions.font, SKFontStyle.Normal), LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.font_size, IsAntialias = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High, Color = renderOptions.message_color };
                List<ThirdPartyEmote> thirdPartyEmotes = new List<ThirdPartyEmote>();
                Dictionary<string, SKBitmap> chatEmotes = new Dictionary<string, SKBitmap>();
                Dictionary<string, SKBitmap> emojiCache = new Dictionary<string, SKBitmap>();
                string emojiRegex = "[#*0-9]\uFE0F\u20E3|[\u00A9\u00AE\u203C\u2049\u2122\u2139\u2194-\u2199\u21A9\u21AA\u231A\u231B\u2328\u23CF\u23E9-\u23F3\u23F8-\u23FA\u24C2\u25AA\u25AB\u25B6\u25C0\u25FB-\u25FE\u2600-\u2604\u260E\u2611\u2614\u2615\u2618]|\u261D(?:\uD83C[\uDFFB-\uDFFF])?|[\u2620\u2622\u2623\u2626\u262A\u262E\u262F\u2638-\u263A\u2640\u2642\u2648-\u2653\u265F\u2660\u2663\u2665\u2666\u2668\u267B\u267E\u267F\u2692-\u2697\u2699\u269B\u269C\u26A0\u26A1\u26AA\u26AB\u26B0\u26B1\u26BD\u26BE\u26C4\u26C5\u26C8\u26CE\u26CF\u26D1\u26D3\u26D4\u26E9\u26EA\u26F0-\u26F5\u26F7\u26F8]|\u26F9(?:\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?|\uFE0F\u200D[\u2640\u2642]\uFE0F)?|[\u26FA\u26FD\u2702\u2705\u2708\u2709]|[\u270A-\u270D](?:\uD83C[\uDFFB-\uDFFF])?|[\u270F\u2712\u2714\u2716\u271D\u2721\u2728\u2733\u2734\u2744\u2747\u274C\u274E\u2753-\u2755\u2757\u2763\u2764\u2795-\u2797\u27A1\u27B0\u27BF\u2934\u2935\u2B05-\u2B07\u2B1B\u2B1C\u2B50\u2B55\u3030\u303D\u3297\u3299]|\uD83C(?:[\uDC04\uDCCF\uDD70\uDD71\uDD7E\uDD7F\uDD8E\uDD91-\uDD9A]|\uDDE6\uD83C[\uDDE8-\uDDEC\uDDEE\uDDF1\uDDF2\uDDF4\uDDF6-\uDDFA\uDDFC\uDDFD\uDDFF]|\uDDE7\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEF\uDDF1-\uDDF4\uDDF6-\uDDF9\uDDFB\uDDFC\uDDFE\uDDFF]|\uDDE8\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDEE\uDDF0-\uDDF5\uDDF7\uDDFA-\uDDFF]|\uDDE9\uD83C[\uDDEA\uDDEC\uDDEF\uDDF0\uDDF2\uDDF4\uDDFF]|\uDDEA\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDED\uDDF7-\uDDFA]|\uDDEB\uD83C[\uDDEE-\uDDF0\uDDF2\uDDF4\uDDF7]|\uDDEC\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEE\uDDF1-\uDDF3\uDDF5-\uDDFA\uDDFC\uDDFE]|\uDDED\uD83C[\uDDF0\uDDF2\uDDF3\uDDF7\uDDF9\uDDFA]|\uDDEE\uD83C[\uDDE8-\uDDEA\uDDF1-\uDDF4\uDDF6-\uDDF9]|\uDDEF\uD83C[\uDDEA\uDDF2\uDDF4\uDDF5]|\uDDF0\uD83C[\uDDEA\uDDEC-\uDDEE\uDDF2\uDDF3\uDDF5\uDDF7\uDDFC\uDDFE\uDDFF]|\uDDF1\uD83C[\uDDE6-\uDDE8\uDDEE\uDDF0\uDDF7-\uDDFB\uDDFE]|\uDDF2\uD83C[\uDDE6\uDDE8-\uDDED\uDDF0-\uDDFF]|\uDDF3\uD83C[\uDDE6\uDDE8\uDDEA-\uDDEC\uDDEE\uDDF1\uDDF4\uDDF5\uDDF7\uDDFA\uDDFF]|\uDDF4\uD83C\uDDF2|\uDDF5\uD83C[\uDDE6\uDDEA-\uDDED\uDDF0-\uDDF3\uDDF7-\uDDF9\uDDFC\uDDFE]|\uDDF6\uD83C\uDDE6|\uDDF7\uD83C[\uDDEA\uDDF4\uDDF8\uDDFA\uDDFC]|\uDDF8\uD83C[\uDDE6-\uDDEA\uDDEC-\uDDF4\uDDF7-\uDDF9\uDDFB\uDDFD-\uDDFF]|\uDDF9\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDED\uDDEF-\uDDF4\uDDF7\uDDF9\uDDFB\uDDFC\uDDFF]|\uDDFA\uD83C[\uDDE6\uDDEC\uDDF2\uDDF3\uDDF8\uDDFE\uDDFF]|\uDDFB\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDEE\uDDF3\uDDFA]|\uDDFC\uD83C[\uDDEB\uDDF8]|\uDDFD\uD83C\uDDF0|\uDDFE\uD83C[\uDDEA\uDDF9]|\uDDFF\uD83C[\uDDE6\uDDF2\uDDFC]|[\uDE01\uDE02\uDE1A\uDE2F\uDE32-\uDE3A\uDE50\uDE51\uDF00-\uDF21\uDF24-\uDF84]|\uDF85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDF86-\uDF93\uDF96\uDF97\uDF99-\uDF9B\uDF9E-\uDFC1]|\uDFC2(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC3\uDFC4](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDFC5\uDFC6]|\uDFC7(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC8\uDFC9]|\uDFCA(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDFCB\uDFCC](?:\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?|\uFE0F\u200D[\u2640\u2642]\uFE0F)?|[\uDFCD-\uDFF0]|\uDFF3(?:\uFE0F\u200D\uD83C\uDF08)?|\uDFF4(?:\u200D\u2620\uFE0F|\uDB40\uDC67\uDB40\uDC62\uDB40(?:\uDC65\uDB40\uDC6E\uDB40\uDC67|\uDC73\uDB40\uDC63\uDB40\uDC74|\uDC77\uDB40\uDC6C\uDB40\uDC73)\uDB40\uDC7F)?|[\uDFF5\uDFF7-\uDFFF])|\uD83D(?:[\uDC00-\uDC14]|\uDC15(?:\u200D\uD83E\uDDBA)?|[\uDC16-\uDC40]|\uDC41(?:\uFE0F\u200D\uD83D\uDDE8\uFE0F)?|[\uDC42\uDC43](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC44\uDC45]|[\uDC46-\uDC50](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC51-\uDC65]|[\uDC66\uDC67](?:\uD83C[\uDFFB-\uDFFF])?|\uDC68(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\u2764\uFE0F\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|[\uDC68\uDC69]\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD]))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C\uDFFB|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFC]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFD]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC69(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\u2764\uFE0F\u200D\uD83D(?:\uDC8B\u200D\uD83D)?[\uDC68\uDC69]|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|\uDC69\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFC-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC68\uD83C[\uDFFB\uDFFD-\uDFFF]|\uDC69\uD83C\uDFFB)|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC68\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|\uDC69\uD83C[\uDFFB\uDFFC])|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC68\uD83C[\uDFFB-\uDFFD\uDFFF]|\uDC69\uD83C[\uDFFB-\uDFFD])|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC6A|[\uDC6B-\uDC6D](?:\uD83C[\uDFFB-\uDFFF])?|\uDC6E(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC6F(?:\u200D[\u2640\u2642]\uFE0F)?|\uDC70(?:\uD83C[\uDFFB-\uDFFF])?|\uDC71(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC72(?:\uD83C[\uDFFB-\uDFFF])?|\uDC73(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDC74-\uDC76](?:\uD83C[\uDFFB-\uDFFF])?|\uDC77(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC78(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC79-\uDC7B]|\uDC7C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC7D-\uDC80]|[\uDC81\uDC82](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC83(?:\uD83C[\uDFFB-\uDFFF])?|\uDC84|\uDC85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC86\uDC87](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDC88-\uDCA9]|\uDCAA(?:\uD83C[\uDFFB-\uDFFF])?|[\uDCAB-\uDCFD\uDCFF-\uDD3D\uDD49-\uDD4E\uDD50-\uDD67\uDD6F\uDD70\uDD73]|\uDD74(?:\uD83C[\uDFFB-\uDFFF])?|\uDD75(?:\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?|\uFE0F\u200D[\u2640\u2642]\uFE0F)?|[\uDD76-\uDD79]|\uDD7A(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD87\uDD8A-\uDD8D]|[\uDD90\uDD95\uDD96](?:\uD83C[\uDFFB-\uDFFF])?|[\uDDA4\uDDA5\uDDA8\uDDB1\uDDB2\uDDBC\uDDC2-\uDDC4\uDDD1-\uDDD3\uDDDC-\uDDDE\uDDE1\uDDE3\uDDE8\uDDEF\uDDF3\uDDFA-\uDE44]|[\uDE45-\uDE47](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDE48-\uDE4A]|\uDE4B(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDE4C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE4D\uDE4E](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDE4F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE80-\uDEA2]|\uDEA3(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDEA4-\uDEB3]|[\uDEB4-\uDEB6](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDEB7-\uDEBF]|\uDEC0(?:\uD83C[\uDFFB-\uDFFF])?|[\uDEC1-\uDEC5\uDECB]|\uDECC(?:\uD83C[\uDFFB-\uDFFF])?|[\uDECD-\uDED2\uDED5\uDEE0-\uDEE5\uDEE9\uDEEB\uDEEC\uDEF0\uDEF3-\uDEFA\uDFE0-\uDFEB])|\uD83E(?:[\uDD0D\uDD0E]|\uDD0F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD10-\uDD17]|[\uDD18-\uDD1C](?:\uD83C[\uDFFB-\uDFFF])?|\uDD1D|[\uDD1E\uDD1F](?:\uD83C[\uDFFB-\uDFFF])?|[\uDD20-\uDD25]|\uDD26(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDD27-\uDD2F]|[\uDD30-\uDD36](?:\uD83C[\uDFFB-\uDFFF])?|\uDD37(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDD38\uDD39](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDD3A|\uDD3C(?:\u200D[\u2640\u2642]\uFE0F)?|[\uDD3D\uDD3E](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDD3F-\uDD45\uDD47-\uDD71\uDD73-\uDD76\uDD7A-\uDDA2\uDDA5-\uDDAA\uDDAE-\uDDB4]|[\uDDB5\uDDB6](?:\uD83C[\uDFFB-\uDFFF])?|\uDDB7|[\uDDB8\uDDB9](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDDBA|\uDDBB(?:\uD83C[\uDFFB-\uDFFF])?|[\uDDBC-\uDDCA]|[\uDDCD-\uDDCF](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDDD0|\uDDD1(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1|\uD83C(?:\uDFFB(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C\uDFFB)?|\uDFFC(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB\uDFFC])?|\uDFFD(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFD])?|\uDFFE(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFE])?|\uDFFF(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])?))?|[\uDDD2-\uDDD5](?:\uD83C[\uDFFB-\uDFFF])?|\uDDD6(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDDD7-\uDDDD](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDDDE\uDDDF](?:\u200D[\u2640\u2642]\uFE0F)?|[\uDDE0-\uDDFF\uDE70-\uDE73\uDE78-\uDE7A\uDE80-\uDE82\uDE90-\uDE95])";

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

                        ThirdPartyEmote emote = new ThirdPartyEmote(new List<SKBitmap>() { emoteBitmap }, emoteCodec, previewDataEmote.name, ".png", "0", 1);
                        thirdPartyEmotes.Add(emote);
                    }

                    foreach (PreviewComment previewComment in previewData.comments)
                    {
                        int default_x = 2;
                        Point drawPos = new Point(default_x, 0);
                        string userName = previewComment.name;
                        SKColor userColor = new SKColor(Convert.ToByte(previewComment.color.Substring(0, 2), 16), Convert.ToByte(previewComment.color.Substring(2, 2), 16), Convert.ToByte(previewComment.color.Substring(4, 2), 16));
                        List<SKBitmap> imageList = new List<SKBitmap>();
                        SKBitmap sectionImage = new SKBitmap((int)canvasSize.Width, (int)canvasSize.Height);
                        List<GifEmote> currentGifEmotes = new List<GifEmote>();
                        List<SKBitmap> emoteList = new List<SKBitmap>();
                        List<SKRect> emotePositionList = new List<SKRect>();
                        new SKCanvas(sectionImage).Clear(renderOptions.background_color);
                        Comment comment = new Comment();
                        comment.message = new Message();
                        Fragment msg = new Fragment();
                        msg.text = previewComment.message;
                        comment.message.fragments = new List<Fragment>();
                        comment.message.fragments.Add(msg);
                        if (renderOptions.chat_timestamp)
                            sectionImage = pageChatRender.DrawTimestamp(sectionImage, imageList, messageFont, renderOptions, comment, canvasSize, ref drawPos, ref default_x);
                        if (previewComment.badges != null)
                            sectionImage = DrawBadges(sectionImage, imageList, renderOptions, canvasSize, ref drawPos, previewComment);
                        sectionImage = pageChatRender.DrawUsername(sectionImage, imageList, renderOptions, nameFont, userName, userColor, canvasSize, ref drawPos);
                        sectionImage = pageChatRender.DrawMessage(sectionImage, imageList, renderOptions, currentGifEmotes, messageFont, emojiCache, chatEmotes, thirdPartyEmotes, comment, canvasSize, ref drawPos, emojiRegex, ref default_x, emoteList, emotePositionList);


                        int finalHeight = 0;
                        foreach (var img in imageList)
                            finalHeight += img.Height;
                        SKBitmap finalImage = new SKBitmap((int)canvasSize.Width, finalHeight);
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

        private SKBitmap DrawBadges(SKBitmap sectionImage, List<SKBitmap> imageList, RenderOptions renderOptions, Size canvasSize, ref Point drawPos, PreviewComment previewComment)
        {
            foreach (string badgeString in previewComment.badges)
            {
                byte[] imageBytes = Convert.FromBase64String(badgeString);
                SKBitmap badgeImage = SKBitmap.Decode(imageBytes);
                using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                {
                    float imageRatio = (float)(renderOptions.image_scale);
                    float imageSize = badgeImage.Width * imageRatio;
                    float left = (float)drawPos.X;
                    float right = imageSize + left;
                    float top = (float)((sectionImage.Height - imageSize) / 2);
                    float bottom = imageSize + top;
                    SKRect drawBox = new SKRect(left, top, right, bottom);
                    sectionImageCanvas.DrawBitmap(badgeImage, drawBox, imagePaint);
                    drawPos.X += (int)Math.Floor(20 * renderOptions.image_scale);
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
}
