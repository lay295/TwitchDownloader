using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;
using System.Net;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using SkiaSharp;
using TwitchDownloader.Properties;
using MessageBox = System.Windows.MessageBox;
using TwitchDownloader;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageChatRender.xaml
    /// </summary>
    public partial class PageChatRender : System.Windows.Controls.Page
    {
        public SKPaint imagePaint = new SKPaint() { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        public SKPaint emotePaint = new SKPaint() { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        public SKFontManager fontManager = SKFontManager.CreateDefault();
        public ConcurrentDictionary<char, SKPaint> fallbackCache = new ConcurrentDictionary<char, SKPaint>();
        public WindowPreview windowPreview = null;
        public PageChatRender()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON Files | *.json";
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == true)
            {
                textJson.Text = openFileDialog.FileName;
            }
        }

        private void btnRender_Click(object sender, RoutedEventArgs e)
        {
            bool validInputs = ValidateInputs();
            if (validInputs)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();

                string fileFormat = comboFormat.SelectedItem.ToString();
                saveFileDialog.Filter = $"{fileFormat} Files | *.{fileFormat.ToLower()}";
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.FileName = Path.GetFileNameWithoutExtension(textJson.Text) + "." + fileFormat.ToLower();

                if (saveFileDialog.ShowDialog() == true)
                {
                    SKColor backgroundColor = new SKColor(colorBackground.SelectedColor.Value.R, colorBackground.SelectedColor.Value.G, colorBackground.SelectedColor.Value.B, colorBackground.SelectedColor.Value.A);
                    SKColor messageColor = new SKColor(colorFont.SelectedColor.Value.R, colorFont.SelectedColor.Value.G, colorFont.SelectedColor.Value.B);
                    RenderOptions info = new RenderOptions(textJson.Text, saveFileDialog.FileName, backgroundColor, Int32.Parse(textHeight.Text), Int32.Parse(textWidth.Text), (bool)checkBTTV.IsChecked, (bool)checkFFZ.IsChecked, (bool)checkOutline.IsChecked, (string)comboFont.SelectedItem, Double.Parse(textFontSize.Text), Double.Parse(textUpdateTime.Text), (bool)checkTimestamp.IsChecked, messageColor, Int32.Parse(textFramerate.Text), Settings.Default.FfmpegInputArgs, Settings.Default.FfmpegOutputArgs);
                    SaveSettings();
                    BackgroundWorker backgroundRenderManager = new BackgroundWorker();
                    backgroundRenderManager.WorkerReportsProgress = true;
                    backgroundRenderManager.DoWork += BackgroundRenderManager_DoWork;
                    backgroundRenderManager.ProgressChanged += BackgroundRenderManager_ProgressChanged;
                    backgroundRenderManager.RunWorkerCompleted += BackgroundRenderManager_RunWorkerCompleted;

                    SetImage("Images/ppOverheat.gif", true);
                    backgroundRenderManager.RunWorkerAsync(info);
                    btnRender.IsEnabled = false;
                }
            }
            else
            {
                MessageBox.Show("Please double check your inputs are valid", "Unable to parse inputs", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackgroundRenderManager_DoWork(object sender, DoWorkEventArgs e)
        {
            RenderOptions renderOptions = (RenderOptions)e.Argument;
            ChatRoot chatJson;
            try
            {
                chatJson = JsonConvert.DeserializeObject<ChatRoot>(File.ReadAllText(renderOptions.json_path));
                chatJson.streamer.name = GetStreamerName(chatJson.streamer.id);
            }
            catch (JsonSerializationException)
            {
                chatJson = new ChatRoot();
                chatJson.comments = JsonConvert.DeserializeObject<List<Comment>>(File.ReadAllText(renderOptions.json_path));
                chatJson.streamer = new Streamer();
                chatJson.streamer.id = Int32.Parse(chatJson.comments.First().channel_id);
                chatJson.streamer.name = GetStreamerName(chatJson.streamer.id);
            }
            BlockingCollection<TwitchComment> finalComments = new BlockingCollection<TwitchComment>();
            List<ThirdPartyEmote> thirdPartyEmotes = new List<ThirdPartyEmote>();
            List<ChatBadge> chatBadges = new List<ChatBadge>();
            List<CheerEmote> cheerEmotes = new List<CheerEmote>();
            Dictionary<string, SKBitmap> chatEmotes = new Dictionary<string, SKBitmap>();
            Dictionary<string, SKBitmap> emojiCache = new Dictionary<string, SKBitmap>();
            Random rand = new Random();
            string[] defaultColors = { "#FF0000", "#0000FF", "#00FF00", "#B22222", "#FF7F50", "#9ACD32", "#FF4500", "#2E8B57", "#DAA520", "#D2691E", "#5F9EA0", "#1E90FF", "#FF69B4", "#8A2BE2", "#00FF7F" };
            string emojiRegex = "[#*0-9]\uFE0F\u20E3|[\u00A9\u00AE\u203C\u2049\u2122\u2139\u2194-\u2199\u21A9\u21AA\u231A\u231B\u2328\u23CF\u23E9-\u23F3\u23F8-\u23FA\u24C2\u25AA\u25AB\u25B6\u25C0\u25FB-\u25FE\u2600-\u2604\u260E\u2611\u2614\u2615\u2618]|\u261D(?:\uD83C[\uDFFB-\uDFFF])?|[\u2620\u2622\u2623\u2626\u262A\u262E\u262F\u2638-\u263A\u2640\u2642\u2648-\u2653\u265F\u2660\u2663\u2665\u2666\u2668\u267B\u267E\u267F\u2692-\u2697\u2699\u269B\u269C\u26A0\u26A1\u26AA\u26AB\u26B0\u26B1\u26BD\u26BE\u26C4\u26C5\u26C8\u26CE\u26CF\u26D1\u26D3\u26D4\u26E9\u26EA\u26F0-\u26F5\u26F7\u26F8]|\u26F9(?:\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?|\uFE0F\u200D[\u2640\u2642]\uFE0F)?|[\u26FA\u26FD\u2702\u2705\u2708\u2709]|[\u270A-\u270D](?:\uD83C[\uDFFB-\uDFFF])?|[\u270F\u2712\u2714\u2716\u271D\u2721\u2728\u2733\u2734\u2744\u2747\u274C\u274E\u2753-\u2755\u2757\u2763\u2764\u2795-\u2797\u27A1\u27B0\u27BF\u2934\u2935\u2B05-\u2B07\u2B1B\u2B1C\u2B50\u2B55\u3030\u303D\u3297\u3299]|\uD83C(?:[\uDC04\uDCCF\uDD70\uDD71\uDD7E\uDD7F\uDD8E\uDD91-\uDD9A]|\uDDE6\uD83C[\uDDE8-\uDDEC\uDDEE\uDDF1\uDDF2\uDDF4\uDDF6-\uDDFA\uDDFC\uDDFD\uDDFF]|\uDDE7\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEF\uDDF1-\uDDF4\uDDF6-\uDDF9\uDDFB\uDDFC\uDDFE\uDDFF]|\uDDE8\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDEE\uDDF0-\uDDF5\uDDF7\uDDFA-\uDDFF]|\uDDE9\uD83C[\uDDEA\uDDEC\uDDEF\uDDF0\uDDF2\uDDF4\uDDFF]|\uDDEA\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDED\uDDF7-\uDDFA]|\uDDEB\uD83C[\uDDEE-\uDDF0\uDDF2\uDDF4\uDDF7]|\uDDEC\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEE\uDDF1-\uDDF3\uDDF5-\uDDFA\uDDFC\uDDFE]|\uDDED\uD83C[\uDDF0\uDDF2\uDDF3\uDDF7\uDDF9\uDDFA]|\uDDEE\uD83C[\uDDE8-\uDDEA\uDDF1-\uDDF4\uDDF6-\uDDF9]|\uDDEF\uD83C[\uDDEA\uDDF2\uDDF4\uDDF5]|\uDDF0\uD83C[\uDDEA\uDDEC-\uDDEE\uDDF2\uDDF3\uDDF5\uDDF7\uDDFC\uDDFE\uDDFF]|\uDDF1\uD83C[\uDDE6-\uDDE8\uDDEE\uDDF0\uDDF7-\uDDFB\uDDFE]|\uDDF2\uD83C[\uDDE6\uDDE8-\uDDED\uDDF0-\uDDFF]|\uDDF3\uD83C[\uDDE6\uDDE8\uDDEA-\uDDEC\uDDEE\uDDF1\uDDF4\uDDF5\uDDF7\uDDFA\uDDFF]|\uDDF4\uD83C\uDDF2|\uDDF5\uD83C[\uDDE6\uDDEA-\uDDED\uDDF0-\uDDF3\uDDF7-\uDDF9\uDDFC\uDDFE]|\uDDF6\uD83C\uDDE6|\uDDF7\uD83C[\uDDEA\uDDF4\uDDF8\uDDFA\uDDFC]|\uDDF8\uD83C[\uDDE6-\uDDEA\uDDEC-\uDDF4\uDDF7-\uDDF9\uDDFB\uDDFD-\uDDFF]|\uDDF9\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDED\uDDEF-\uDDF4\uDDF7\uDDF9\uDDFB\uDDFC\uDDFF]|\uDDFA\uD83C[\uDDE6\uDDEC\uDDF2\uDDF3\uDDF8\uDDFE\uDDFF]|\uDDFB\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDEE\uDDF3\uDDFA]|\uDDFC\uD83C[\uDDEB\uDDF8]|\uDDFD\uD83C\uDDF0|\uDDFE\uD83C[\uDDEA\uDDF9]|\uDDFF\uD83C[\uDDE6\uDDF2\uDDFC]|[\uDE01\uDE02\uDE1A\uDE2F\uDE32-\uDE3A\uDE50\uDE51\uDF00-\uDF21\uDF24-\uDF84]|\uDF85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDF86-\uDF93\uDF96\uDF97\uDF99-\uDF9B\uDF9E-\uDFC1]|\uDFC2(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC3\uDFC4](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDFC5\uDFC6]|\uDFC7(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC8\uDFC9]|\uDFCA(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDFCB\uDFCC](?:\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?|\uFE0F\u200D[\u2640\u2642]\uFE0F)?|[\uDFCD-\uDFF0]|\uDFF3(?:\uFE0F\u200D\uD83C\uDF08)?|\uDFF4(?:\u200D\u2620\uFE0F|\uDB40\uDC67\uDB40\uDC62\uDB40(?:\uDC65\uDB40\uDC6E\uDB40\uDC67|\uDC73\uDB40\uDC63\uDB40\uDC74|\uDC77\uDB40\uDC6C\uDB40\uDC73)\uDB40\uDC7F)?|[\uDFF5\uDFF7-\uDFFF])|\uD83D(?:[\uDC00-\uDC14]|\uDC15(?:\u200D\uD83E\uDDBA)?|[\uDC16-\uDC40]|\uDC41(?:\uFE0F\u200D\uD83D\uDDE8\uFE0F)?|[\uDC42\uDC43](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC44\uDC45]|[\uDC46-\uDC50](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC51-\uDC65]|[\uDC66\uDC67](?:\uD83C[\uDFFB-\uDFFF])?|\uDC68(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\u2764\uFE0F\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|[\uDC68\uDC69]\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD]))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C\uDFFB|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFC]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFD]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC69(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\u2764\uFE0F\u200D\uD83D(?:\uDC8B\u200D\uD83D)?[\uDC68\uDC69]|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|\uDC69\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFC-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC68\uD83C[\uDFFB\uDFFD-\uDFFF]|\uDC69\uD83C\uDFFB)|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC68\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|\uDC69\uD83C[\uDFFB\uDFFC])|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC68\uD83C[\uDFFB-\uDFFD\uDFFF]|\uDC69\uD83C[\uDFFB-\uDFFD])|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC6A|[\uDC6B-\uDC6D](?:\uD83C[\uDFFB-\uDFFF])?|\uDC6E(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC6F(?:\u200D[\u2640\u2642]\uFE0F)?|\uDC70(?:\uD83C[\uDFFB-\uDFFF])?|\uDC71(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC72(?:\uD83C[\uDFFB-\uDFFF])?|\uDC73(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDC74-\uDC76](?:\uD83C[\uDFFB-\uDFFF])?|\uDC77(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC78(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC79-\uDC7B]|\uDC7C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC7D-\uDC80]|[\uDC81\uDC82](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC83(?:\uD83C[\uDFFB-\uDFFF])?|\uDC84|\uDC85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC86\uDC87](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDC88-\uDCA9]|\uDCAA(?:\uD83C[\uDFFB-\uDFFF])?|[\uDCAB-\uDCFD\uDCFF-\uDD3D\uDD49-\uDD4E\uDD50-\uDD67\uDD6F\uDD70\uDD73]|\uDD74(?:\uD83C[\uDFFB-\uDFFF])?|\uDD75(?:\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?|\uFE0F\u200D[\u2640\u2642]\uFE0F)?|[\uDD76-\uDD79]|\uDD7A(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD87\uDD8A-\uDD8D]|[\uDD90\uDD95\uDD96](?:\uD83C[\uDFFB-\uDFFF])?|[\uDDA4\uDDA5\uDDA8\uDDB1\uDDB2\uDDBC\uDDC2-\uDDC4\uDDD1-\uDDD3\uDDDC-\uDDDE\uDDE1\uDDE3\uDDE8\uDDEF\uDDF3\uDDFA-\uDE44]|[\uDE45-\uDE47](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDE48-\uDE4A]|\uDE4B(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDE4C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE4D\uDE4E](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDE4F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE80-\uDEA2]|\uDEA3(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDEA4-\uDEB3]|[\uDEB4-\uDEB6](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDEB7-\uDEBF]|\uDEC0(?:\uD83C[\uDFFB-\uDFFF])?|[\uDEC1-\uDEC5\uDECB]|\uDECC(?:\uD83C[\uDFFB-\uDFFF])?|[\uDECD-\uDED2\uDED5\uDEE0-\uDEE5\uDEE9\uDEEB\uDEEC\uDEF0\uDEF3-\uDEFA\uDFE0-\uDFEB])|\uD83E(?:[\uDD0D\uDD0E]|\uDD0F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD10-\uDD17]|[\uDD18-\uDD1C](?:\uD83C[\uDFFB-\uDFFF])?|\uDD1D|[\uDD1E\uDD1F](?:\uD83C[\uDFFB-\uDFFF])?|[\uDD20-\uDD25]|\uDD26(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDD27-\uDD2F]|[\uDD30-\uDD36](?:\uD83C[\uDFFB-\uDFFF])?|\uDD37(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDD38\uDD39](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDD3A|\uDD3C(?:\u200D[\u2640\u2642]\uFE0F)?|[\uDD3D\uDD3E](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDD3F-\uDD45\uDD47-\uDD71\uDD73-\uDD76\uDD7A-\uDDA2\uDDA5-\uDDAA\uDDAE-\uDDB4]|[\uDDB5\uDDB6](?:\uD83C[\uDFFB-\uDFFF])?|\uDDB7|[\uDDB8\uDDB9](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDDBA|\uDDBB(?:\uD83C[\uDFFB-\uDFFF])?|[\uDDBC-\uDDCA]|[\uDDCD-\uDDCF](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDDD0|\uDDD1(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1|\uD83C(?:\uDFFB(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C\uDFFB)?|\uDFFC(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB\uDFFC])?|\uDFFD(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFD])?|\uDFFE(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFE])?|\uDFFF(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])?))?|[\uDDD2-\uDDD5](?:\uD83C[\uDFFB-\uDFFF])?|\uDDD6(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDDD7-\uDDDD](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDDDE\uDDDF](?:\u200D[\u2640\u2642]\uFE0F)?|[\uDDE0-\uDDFF\uDE70-\uDE73\uDE78-\uDE7A\uDE80-\uDE82\uDE90-\uDE95])";
            string tempFolder = Path.Combine(Path.GetTempPath(), "TwitchDownloader");
            string downloadFolder = Path.Combine(tempFolder, "Chat Render");
            string cacheFolder = Path.Combine(tempFolder, "cache");

            if (renderOptions.background_color.Alpha < 255)
            {
                imagePaint.BlendMode = SKBlendMode.Src;
                emotePaint.BlendMode = SKBlendMode.SrcOver;
            }

            if (!Directory.Exists(downloadFolder))
                Directory.CreateDirectory(downloadFolder);
            if (!Directory.Exists(cacheFolder))
                Directory.CreateDirectory(cacheFolder);

            (sender as BackgroundWorker).ReportProgress(0, new Progress("Fetching Chat Badges"));
            GetChatBadges(chatBadges, chatJson.streamer, renderOptions);
            (sender as BackgroundWorker).ReportProgress(0, new Progress("Fetching Emotes"));
            GetEmotes(chatEmotes, chatJson.comments, renderOptions, cacheFolder);
            (sender as BackgroundWorker).ReportProgress(0, new Progress("Fetching Third Party Emotes"));
            GetThirdPartyEmotes(thirdPartyEmotes, chatJson.streamer, renderOptions, cacheFolder);
            (sender as BackgroundWorker).ReportProgress(0, new Progress("Fetching Twitter Emojis"));
            GetTwitterEmojis(emojiCache, chatJson.comments, renderOptions, cacheFolder, emojiRegex);
            (sender as BackgroundWorker).ReportProgress(0, new Progress("Fetching Bit Emotes"));
            GetBits(cheerEmotes, renderOptions, cacheFolder, emojiRegex);

            Size canvasSize = new Size(renderOptions.chat_width, renderOptions.text_height);
            SKPaint nameFont = new SKPaint() { Typeface = SKTypeface.FromFamilyName(renderOptions.font, SKFontStyle.Bold), LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.font_size, IsAntialias = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            SKPaint messageFont = new SKPaint() { Typeface = SKTypeface.FromFamilyName(renderOptions.font, SKFontStyle.Normal), LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.font_size, IsAntialias = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High, Color = renderOptions.message_color };

            (sender as BackgroundWorker).ReportProgress(0, new Progress("Rendering Comments"));
            foreach (Comment comment in chatJson.comments)
            {
                if (comment.source != "chat")
                    continue;
                if (comment.message.user_notice_params != null && comment.message.user_notice_params.msg_id != null)
                {
                    if (comment.message.user_notice_params.msg_id != "highlighted-message" && comment.message.user_notice_params.msg_id != "")
                        continue;
                }

                string userName = comment.commenter.display_name.ToString();
                int default_x = 2;
                Point drawPos = new Point(default_x, 0);
                string colorHtml = (comment.message.user_color != null ? comment.message.user_color : defaultColors[Math.Abs(userName.GetHashCode()) % defaultColors.Length]);
                SKColor userColor = SKColor.Parse(colorHtml);
                userColor = GenerateUserColor(userColor, renderOptions.background_color);

                List<SKBitmap> imageList = new List<SKBitmap>();
                SKBitmap sectionImage = new SKBitmap((int)canvasSize.Width, (int)canvasSize.Height);

                List<GifEmote> currentGifEmotes = new List<GifEmote>();
                List<SKBitmap> emoteList = new List<SKBitmap>();
                List<SKRect> emotePositionList = new List<SKRect>();
                new SKCanvas(sectionImage).Clear(renderOptions.background_color);
                if (renderOptions.chat_timestamp)
                    sectionImage = DrawTimestamp(sectionImage, imageList, messageFont, renderOptions, comment, canvasSize, ref drawPos, ref default_x);
                sectionImage = DrawBadges(sectionImage, imageList, renderOptions, chatBadges, comment, canvasSize, ref drawPos);
                sectionImage = DrawUsername(sectionImage, imageList, renderOptions, nameFont, userName, userColor, canvasSize, ref drawPos);
                sectionImage = DrawMessage(sectionImage, imageList, renderOptions, currentGifEmotes, messageFont, emojiCache, chatEmotes, thirdPartyEmotes, cheerEmotes, comment, canvasSize, ref drawPos, emojiRegex, ref default_x, emoteList, emotePositionList);

                int finalHeight = 0;
                foreach (var img in imageList)
                    finalHeight += img.Height;
                SKBitmap finalImage = new SKBitmap((int)canvasSize.Width, finalHeight);
                SKCanvas finalImageCanvas = new SKCanvas(finalImage);
                finalHeight = 0;
                finalImageCanvas.Clear(renderOptions.background_color);
                foreach (var img in imageList)
                {
                    finalImageCanvas.DrawBitmap(img, 0, finalHeight);
                    finalHeight += img.Height;
                    img.Dispose();
                }

                string imagePath = Path.Combine(downloadFolder, Guid.NewGuid() + ".png");
                finalComments.Add(new TwitchComment(imagePath, Double.Parse(comment.content_offset_seconds.ToString()), currentGifEmotes, emoteList, emotePositionList));
                using (Stream s = File.OpenWrite(imagePath))
                {
                    SKImage saveImage = SKImage.FromBitmap(finalImage);
                    if (saveImage != null)
                    {
                        saveImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(s);
                        saveImage.Dispose();
                    }
                }
                finalImage.Dispose();
                finalImageCanvas.Dispose();
                int percent = (int)Math.Floor(((double)finalComments.Count / (double)chatJson.comments.Count) * 100);
                (sender as BackgroundWorker).ReportProgress(percent, new Progress("Rendering Comments"));
            }

            (sender as BackgroundWorker).ReportProgress(0, new Progress("Rendering Video 0%"));
            RenderVideo(renderOptions, new Queue<TwitchComment>(finalComments.ToArray()), chatJson.comments, sender);

            (sender as BackgroundWorker).ReportProgress(0, new Progress("Cleaning up..."));
            try
            {
                Directory.Delete(downloadFolder, true);
            }
            catch
            {
                string[] files = Directory.GetFiles(downloadFolder);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        File.Delete(files[i]);
                    }
                    catch { }
                }
            }
        }

        private string GetStreamerName(int id)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("Accept", "application/vnd.twitchtv.v5+json; charset=UTF-8");
                    client.Headers.Add("Client-Id", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                    JObject response = JObject.Parse(client.DownloadString("https://api.twitch.tv/kraken/users/" + id));
                    return response["name"].ToString();
                }
            }
            catch { return ""; }
        }

        private SKColor GenerateUserColor(SKColor userColor, SKColor background_color)
        {
            float backgroundHue, backgroundSaturation, backgroundBrightness;
            background_color.ToHsl(out backgroundHue, out backgroundSaturation, out backgroundBrightness);
            float userHue, userSaturation, userBrightness;
            userColor.ToHsl(out userHue, out userSaturation, out userBrightness);

            if (backgroundBrightness < 25)
            {
                //Dark background
                if (userBrightness < 45)
                {
                    userBrightness = 45;
                    SKColor newColor = SKColor.FromHsl(userHue, userSaturation, userBrightness);
                    return newColor;
                }
            }

            if (Math.Abs(backgroundBrightness - userBrightness) < 10 && backgroundBrightness > 50)
            {
                userBrightness -= 20;
                SKColor newColor = SKColor.FromHsl(userHue, userSaturation, userBrightness);
                return newColor;
            }

            return userColor;
        }

        private void RenderVideo(RenderOptions renderOptions, Queue<TwitchComment> finalComments, List<Comment> comments, object sender)
        {
            SKBitmap bufferBitmap = new SKBitmap(renderOptions.chat_width, renderOptions.chat_height);
            SKCanvas bufferCanvas = new SKCanvas(bufferBitmap);
            SKPaint gifBackgroundPaint = new SKPaint();
            SKPaint gifPaint = new SKPaint();
            int videoStart = (int)Math.Floor(comments.First().content_offset_seconds);
            int duration = (int)Math.Ceiling(comments.Last().content_offset_seconds) - videoStart;
            List<GifEmote> displayedGifs = new List<GifEmote>();
            Stopwatch stopwatch = new Stopwatch();

            if (renderOptions.background_color.Alpha < 255)
            {
                gifPaint.BlendMode = SKBlendMode.SrcOver;
                gifBackgroundPaint.BlendMode = SKBlendMode.Src;
            }

            if (File.Exists(renderOptions.save_path))
                File.Delete(renderOptions.save_path);

            string inputArgs = renderOptions.input_args.Replace("{fps}", renderOptions.framerate.ToString())
                .Replace("{height}", renderOptions.chat_height.ToString()).Replace("{width}", renderOptions.chat_width.ToString())
                .Replace("{save_path}", renderOptions.save_path).Replace("{max_int}", int.MaxValue.ToString());
            string outputArgs = renderOptions.output_args.Replace("{fps}", renderOptions.framerate.ToString())
                .Replace("{height}", renderOptions.chat_height.ToString()).Replace("{width}", renderOptions.chat_width.ToString())
                .Replace("{save_path}", renderOptions.save_path).Replace("{max_int}", int.MaxValue.ToString());

            var process = new Process
            {
                StartInfo =
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"{inputArgs} {outputArgs}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            //process.ErrorDataReceived += ErrorDataHandler;

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            stopwatch.Start();
            using (var ffmpegStream = new BinaryWriter(process.StandardInput.BaseStream))
            {
                bufferCanvas.Clear(renderOptions.background_color);
                int startTick = (int)Math.Floor(videoStart / (1.0 / renderOptions.framerate));
                int endTick = (int)Math.Floor((videoStart + duration) / (1.0 / renderOptions.framerate));
                int lastUpdateTick = startTick;
                int globalTick = startTick;
                for (int i = startTick; i < endTick; i++)
                {
                    int height = 0;
                    if (globalTick % renderOptions.update_frame == 0)
                    {
                        int y = 0;
                        List<GifEmote> old = new List<GifEmote>(displayedGifs);
                        List<GifEmote> newly_added = new List<GifEmote>();
                        List<TwitchComment> new_comments = new List<TwitchComment>();

                        bool isDone = false;
                        while (finalComments.Count > 0 && !isDone)
                        {
                            int commentTick = (int)Math.Floor(finalComments.Peek().secondsOffset / (1.0 / renderOptions.framerate));
                            if (commentTick >= lastUpdateTick && commentTick < globalTick)
                            {
                                TwitchComment currentComment = finalComments.Dequeue();
                                foreach (var emote in currentComment.gifEmotes)
                                {
                                    GifEmote newGif = new GifEmote(new Point(emote.offset.X, emote.offset.Y + height), emote.name, emote.codec, emote.imageScale, emote.image_frames);
                                    displayedGifs.Add(newGif);
                                    newly_added.Add(newGif);
                                }
                                height += SKBitmap.Decode(currentComment.section).Height;
                                new_comments.Add(currentComment);
                            }
                            else
                            {
                                isDone = true;
                            }
                        }
                        foreach (var emote in old)
                            emote.offset = new Point(emote.offset.X, emote.offset.Y - height);
                        foreach (var emote in newly_added)
                        {
                            emote.offset = new Point(emote.offset.X, (renderOptions.chat_height - height) + emote.offset.Y);
                        }


                        if (height > 0)
                        {
                            List<SKBitmap> emoteList = new List<SKBitmap>();
                            List<SKRect> emotePos = new List<SKRect>();
                            SKBitmap sectionBitmap = new SKBitmap(renderOptions.chat_width, height);
                            SKCanvas sectionCanvas = new SKCanvas(sectionBitmap);
                            sectionCanvas.Clear(renderOptions.background_color);

                            for (int j = 0; j < new_comments.Count; j++)
                            {
                                int commentTick = (int)Math.Floor(new_comments[j].secondsOffset / (1.0 / renderOptions.framerate));
                                if (commentTick >= lastUpdateTick && commentTick < globalTick)
                                {
                                    SKBitmap sectionImage = SKBitmap.Decode(new_comments[j].section);
                                    sectionCanvas.DrawBitmap(sectionImage, 0, y);
                                    for (int k = 0; k < new_comments[j].normalEmotes.Count; k++)
                                    {
                                        emoteList.Add(new_comments[j].normalEmotes[k]);
                                        SKRect refrenceRect = new_comments[j].normalEmotesPositions[k];
                                        float top = bufferBitmap.Height - sectionBitmap.Height + y + refrenceRect.Top;
                                        emotePos.Add(new SKRect(refrenceRect.Left, top, refrenceRect.Right, top + (refrenceRect.Bottom - refrenceRect.Top)));
                                    }
                                    y += sectionImage.Height;
                                }
                            }
                            if (renderOptions.background_color.Alpha < 255)
                            {
                                SKBitmap bufferCopy = bufferBitmap.Copy();
                                bufferCanvas.Clear();
                                bufferCanvas.DrawBitmap(bufferCopy, 0, -height);
                                bufferCopy.Dispose();
                            }
                            else
                            {
                                bufferCanvas.DrawBitmap(bufferBitmap, 0, -height);
                            }
                            bufferCanvas.DrawBitmap(sectionBitmap, 0, renderOptions.chat_height - height);

                            for (int k = 0; k < emoteList.Count; k++)
                                bufferCanvas.DrawBitmap(emoteList[k], emotePos[k], emotePaint);

                            foreach (var emote in newly_added)
                            {
                                float temp_x = (float)emote.offset.X;
                                float temp_y = (float)emote.offset.Y + (int)Math.Floor((renderOptions.text_height - ((emote.image_frames.First().Height / emote.imageScale) * renderOptions.image_scale)) / 2.0);
                                SKRect copyRect = new SKRect(temp_x, temp_y, temp_x + (float)((emote.image_frames.First().Width / emote.imageScale) * renderOptions.image_scale), temp_y + (float)((emote.image_frames.First().Height / emote.imageScale) * renderOptions.image_scale));
                                emote.backgroundImage = new SKBitmap((int)copyRect.Width, (int)copyRect.Height);
                                using (SKCanvas tempCanvas = new SKCanvas(emote.backgroundImage))
                                {
                                    tempCanvas.Clear(renderOptions.background_color);
                                    tempCanvas.DrawBitmap(bufferBitmap, copyRect,
                                        new SKRect(0, 0, copyRect.Width, copyRect.Height));
                                }
                            }
                        }
                        lastUpdateTick = globalTick;
                    }

                    List<GifEmote> to_remove = new List<GifEmote>();
                    foreach (var emote in displayedGifs)
                    {
                        if (emote.offset.Y < -emote.width - renderOptions.chat_height)
                        {
                            to_remove.Add(emote);
                        }
                        else
                        {
                            int gifTime = (int)Math.Floor((1.5 * globalTick / (renderOptions.framerate / 60.0)) % emote.total_duration);
                            int frame = emote.frames - 1;
                            int timeCount = 0;
                            for (int k = 0; k < emote.durations.Count; k++)
                            {
                                if (timeCount + emote.durations[k] > gifTime)
                                {
                                    frame = k;
                                    break;
                                }
                                timeCount += emote.durations[k];
                            }

                            SKBitmap gifBitmap = emote.image_frames[frame];
                            float x = (float)emote.offset.X;
                            float y = (float)emote.offset.Y + (int)Math.Floor((renderOptions.text_height - ((gifBitmap.Height / emote.imageScale) * renderOptions.image_scale)) / 2.0);
                            bufferCanvas.DrawBitmap(gifBitmap, new SKRect(x, y, x + (float)((gifBitmap.Width / emote.imageScale) * renderOptions.image_scale), y + (float)((gifBitmap.Height / emote.imageScale) * renderOptions.image_scale)), gifPaint);
                        }
                    }

                    foreach (var emote in to_remove)
                    {
                        displayedGifs.Remove(emote);
                    }

                    var pix = bufferBitmap.PeekPixels();
                    var data = SKData.Create(pix.GetPixels(), pix.Info.BytesSize);
                    var bytes = data.ToArray();
                    ffmpegStream.Write(bytes);

                    foreach (var emote in displayedGifs)
                    {
                        SKRect drawRect = new SKRect((float)emote.offset.X, (float)emote.offset.Y + (int)Math.Floor((renderOptions.text_height - ((emote.height / emote.imageScale) * renderOptions.image_scale)) / 2.0), (float)emote.offset.X + (float)((emote.width / emote.imageScale) * renderOptions.image_scale), (float)emote.offset.Y + (int)Math.Floor((renderOptions.text_height - ((emote.height / emote.imageScale) * renderOptions.image_scale)) / 2.0) + (float)((emote.height / emote.imageScale) * renderOptions.image_scale));
                        bufferCanvas.DrawBitmap(emote.backgroundImage, drawRect, gifBackgroundPaint);
                    }
                    globalTick += 1;
                    double percentDouble = (double)(globalTick - startTick) / (double)(endTick - startTick) * 100.0;
                    int percentInt = (int)Math.Floor(percentDouble);
                    (sender as BackgroundWorker).ReportProgress(percentInt, new Progress(String.Format("Rendering Video {0}%", percentInt), (int)Math.Floor(stopwatch.Elapsed.TotalSeconds), percentDouble));
                }
            }
            stopwatch.Stop();
            AppendLog($"FINISHED. RENDER TIME: {(int)stopwatch.Elapsed.TotalSeconds}s SPEED: {(duration / stopwatch.Elapsed.TotalSeconds).ToString("0.##")}x");
            process.WaitForExit();
        }

        private void ErrorDataHandler(object sender, DataReceivedEventArgs e)
        {
            AppendLog(e.Data);
        }

        public SKBitmap DrawMessage(SKBitmap sectionImage, List<SKBitmap> imageList, RenderOptions renderOptions, List<GifEmote> currentGifEmotes, SKPaint messageFont, Dictionary<string, SKBitmap> emojiCache, Dictionary<string, SKBitmap> chatEmotes, List<ThirdPartyEmote> thirdPartyEmotes, List<CheerEmote> cheerEmotes, Comment comment, Size canvasSize, ref Point drawPos, string emojiRegex, ref int default_x, List<SKBitmap> emoteList, List<SKRect> emotePositionList)
        {
            int bitsCount = comment.message.bits_spent;

            foreach (var fragment in comment.message.fragments)
            {
                if (fragment.emoticon == null)
                {
                    string[] fragmentParts = fragment.text.Split(' ');
                    for (int i = 0; i < fragmentParts.Length; i++)
                    {
                        string output = fragmentParts[i].Trim();
                        bool isThirdPartyEmote = false;
                        ThirdPartyEmote currentEmote = null;

                        if (output == "" || output == "󠀀")
                            continue;

                        foreach (var thirdPartyEmote in thirdPartyEmotes)
                        {
                            if (fragmentParts[i] == thirdPartyEmote.name)
                            {
                                isThirdPartyEmote = true;
                                currentEmote = thirdPartyEmote;
                            }
                        }

                        if (isThirdPartyEmote)
                        {
                            if (drawPos.X + (currentEmote.width / currentEmote.imageScale) * renderOptions.image_scale > canvasSize.Width)
                                sectionImage = AddImageSection(sectionImage, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

                            if (currentEmote.imageType == "gif")
                            {
                                GifEmote emote = new GifEmote(new Point(drawPos.X, drawPos.Y), currentEmote.name, currentEmote.codec, currentEmote.imageScale, currentEmote.emote_frames);
                                currentGifEmotes.Add(emote);
                                drawPos.X += (currentEmote.width / currentEmote.imageScale) * renderOptions.image_scale + (3 * renderOptions.image_scale);
                            }
                            else
                            {
                                using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                                {
                                    float imageRatio = (float)renderOptions.image_scale / currentEmote.imageScale;
                                    float imageWidth = currentEmote.width * imageRatio;
                                    float imageHeight = currentEmote.height * imageRatio;
                                    float left = (float)drawPos.X;
                                    float right = imageWidth + left;
                                    float top = (float)((sectionImage.Height - imageHeight) / 2);
                                    float bottom = imageHeight + top;
                                    emoteList.Add(currentEmote.emote_frames.First());
                                    emotePositionList.Add(new SKRect(left, top + (renderOptions.text_height * imageList.Count), right, bottom + (renderOptions.text_height * imageList.Count)));
                                    drawPos.X += (int)Math.Ceiling(imageWidth + (3 * renderOptions.image_scale));
                                }
                            }
                        }
                        else
                        {
                            if (Regex.Match(output, emojiRegex).Success)
                            {
                                Match m = Regex.Match(output, emojiRegex);
                                for (var k = 0; k < m.Value.Length; k += char.IsSurrogatePair(m.Value, k) ? 2 : 1)
                                {
                                    string codepoint = String.Format("{0:X4}", char.ConvertToUtf32(m.Value, k)).ToLower();
                                    codepoint = codepoint.Replace("fe0f", "");
                                    if (codepoint != "" && emojiCache.ContainsKey(codepoint))
                                    {
                                        using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                                        {
                                            SKBitmap emojiBitmap = emojiCache[codepoint];
                                            float emojiSize = (emojiBitmap.Width / 4) * (float)renderOptions.image_scale;
                                            if (drawPos.X + (20 * renderOptions.image_scale) + 3 > canvasSize.Width)
                                                sectionImage = AddImageSection(sectionImage, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

                                            float emojiLeft = (float)Math.Floor(drawPos.X);
                                            float emojiTop = (float)Math.Floor((renderOptions.text_height - emojiSize) / 2.0);
                                            SKRect emojiRect = new SKRect(emojiLeft, emojiTop, emojiLeft + emojiSize, emojiTop + emojiSize);
                                            sectionImageCanvas.DrawBitmap(emojiBitmap, emojiRect, imagePaint);
                                            drawPos.X += emojiSize + (int)Math.Floor(3 * renderOptions.image_scale);
                                        }
                                    }
                                }
                            }
                            else if (messageFont.Typeface.CountGlyphs(output) < output.Length)
                            {
                                SKPaint renderFont = messageFont;
                                List<char> charList = new List<char>(output.ToArray());
                                string messageBuffer = "";

                                //Very rough estimation of width of text, because we don't know the font yet. This is to show ASCII spam properly
                                int textWidth = (int)Math.Floor(charList.Count * (9.0 * renderOptions.image_scale));
                                if (drawPos.X + textWidth > canvasSize.Width)
                                    sectionImage = AddImageSection(sectionImage, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

                                for (int j = 0; j < charList.Count; j++)
                                {
                                    if (messageFont.Typeface.CountGlyphs(charList[j].ToString()) == 0)
                                    {
                                        if (messageBuffer != "")
                                            sectionImage = DrawText(sectionImage, messageBuffer, messageFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, true, default_x);
                                        SKPaint fallbackFont = GetFallbackFont(charList[j], renderOptions);
                                        fallbackFont.Color = renderOptions.message_color;
                                        sectionImage = DrawText(sectionImage, charList[j].ToString(), fallbackFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, false, default_x);
                                        messageBuffer = "";
                                    }
                                    else
                                    {
                                        messageBuffer += charList[j];
                                    }
                                }
                            }
                            else
                            {
                                bool bitsPrinted = false;
                                try
                                {
                                    if (bitsCount > 0 && output.Any(char.IsDigit) && cheerEmotes.Any(x => output.Contains(x.prefix)))
                                    {
                                        CheerEmote currentCheerEmote = cheerEmotes.Find(x => output.Contains(x.prefix));
                                        int bitsIndex = output.IndexOfAny("0123456789".ToCharArray());
                                        int bitsAmount = Int32.Parse(output.Substring(bitsIndex));
                                        bitsCount -= bitsAmount;
                                        KeyValuePair<int, ThirdPartyEmote> tierList = currentCheerEmote.getTier(bitsAmount);
                                        GifEmote emote = new GifEmote(new Point(drawPos.X, drawPos.Y), tierList.Value.name, tierList.Value.codec, tierList.Value.imageScale, tierList.Value.emote_frames);
                                        currentGifEmotes.Add(emote);
                                        drawPos.X += (tierList.Value.width / tierList.Value.imageScale) * renderOptions.image_scale + (3 * renderOptions.image_scale);
                                        sectionImage = DrawText(sectionImage, bitsAmount.ToString(), messageFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, true, default_x);
                                        bitsPrinted = true;
                                    }
                                }
                                catch
                                { }
                                if (!bitsPrinted)
                                    sectionImage = DrawText(sectionImage, output, messageFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, true, default_x);
                            }
                        }
                    }
                }
                else
                {
                    //Is a first party emote
                    string emoteId = fragment.emoticon.emoticon_id;
                    if (chatEmotes.ContainsKey(emoteId))
                    {
                        SKBitmap emoteImage = chatEmotes[emoteId];
                        float imageWidth = emoteImage.Width * (float)renderOptions.image_scale;
                        if (drawPos.X + imageWidth > canvasSize.Width)
                            sectionImage = AddImageSection(sectionImage, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);
                        using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                        {
                            float imageHeight = emoteImage.Height * (float)renderOptions.image_scale;
                            float left = (float)drawPos.X;
                            float right = imageWidth + left;
                            float top = (float)((sectionImage.Height - imageHeight) / 2);
                            float bottom = imageHeight + top;
                            //SKRect emoteRect = new SKRect(imageLeft, imageTop, imageLeft + imageWidth, imageTop + imageHeight);
                            //sectionImageCanvas.DrawBitmap(emoteImage, emoteRect, imagePaint);
                            emoteList.Add(emoteImage);
                            emotePositionList.Add(new SKRect(left, top + (renderOptions.text_height * imageList.Count), right, bottom + (renderOptions.text_height * imageList.Count)));
                        }
                        drawPos.X += (int)Math.Ceiling(imageWidth + (3 * renderOptions.image_scale));
                    }
                    else
                    {
                        //Probably an old emote that was removed
                        sectionImage = DrawText(sectionImage, fragment.text, messageFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, true, default_x);
                    }
                }
            }
            imageList.Add(sectionImage);

            SKBitmap paddingImage = new SKBitmap((int)canvasSize.Width, (int)Math.Floor(.4 * renderOptions.text_height));
            using (SKCanvas paddingImageCanvas = new SKCanvas(paddingImage))
                paddingImageCanvas.Clear(renderOptions.background_color);
            imageList.Add(paddingImage);
            return sectionImage;
        }

        public SKBitmap DrawText(SKBitmap sectionImage, string message, SKPaint messageFont, List<SKBitmap> imageList, RenderOptions renderOptions, List<GifEmote> currentGifEmotes, Size canvasSize, ref Point drawPos, bool padding, int default_x)
        {
            float textWidth;
            try
            {
                textWidth = messageFont.MeasureText(message);
            }
            catch { return sectionImage; }
            if (drawPos.X + textWidth + 3 > canvasSize.Width)
                sectionImage = AddImageSection(sectionImage, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

            float xPos = (float)Math.Floor(drawPos.X);
            float yPos = (float)(((canvasSize.Height - renderOptions.font_size) / 2) + renderOptions.font_size) - (float)(renderOptions.image_scale * 2);
            using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
            {
                if (renderOptions.outline)
                {
                    SKPath outlinePath = messageFont.GetTextPath(message, xPos, yPos);
                    SKPaint outlinePaint = new SKPaint() { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(4 * renderOptions.image_scale), StrokeJoin = SKStrokeJoin.Round, Color = SKColors.Black, IsAntialias = true, LcdRenderText = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
                    sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
                }

                sectionImageCanvas.DrawText(message, xPos, yPos, messageFont);
            }
            drawPos.X += textWidth + (padding ? (int)Math.Floor(4 * renderOptions.image_scale) : 0);
            return sectionImage;
        }

        private SKBitmap AddImageSection(SKBitmap sectionImage, List<SKBitmap> imageList, RenderOptions renderOptions, List<GifEmote> currentGifEmotes, Size canvasSize, ref Point drawPos, int default_x)
        {
            imageList.Add(sectionImage);
            SKBitmap newImage = new SKBitmap((int)canvasSize.Width, (int)canvasSize.Height);
            using (SKCanvas paddingImageCanvas = new SKCanvas(newImage))
                paddingImageCanvas.Clear(renderOptions.background_color);
            drawPos.X = default_x;
            drawPos.Y += sectionImage.Height;
            return newImage;
        }

        public SKBitmap DrawTimestamp(SKBitmap sectionImage, List<SKBitmap> imageList, SKPaint messageFont, RenderOptions renderOptions, Comment comment, Size canvasSize, ref Point drawPos, ref int default_x)
        {
            SKCanvas sectionImageCanvas = new SKCanvas(sectionImage);
            float xPos = (float)drawPos.X;
            float yPos = (float)(((canvasSize.Height - renderOptions.font_size) / 2) + renderOptions.font_size) - (float)(renderOptions.image_scale * 2);
            TimeSpan timestamp = new TimeSpan(0, 0, (int)comment.content_offset_seconds);
            string timeString = timestamp.ToString(@"h\:mm\:ss");
            float textWidth = messageFont.MeasureText(timeString);
            if (renderOptions.outline)
            {
                SKPath outlinePath = messageFont.GetTextPath(timeString, xPos, yPos);
                SKPaint outlinePaint = new SKPaint() { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(4 * renderOptions.image_scale), StrokeJoin = SKStrokeJoin.Round, Color = SKColors.Black, IsAntialias = true, LcdRenderText = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
                sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
            }
            sectionImageCanvas.DrawText(timeString, xPos, yPos, messageFont);
            drawPos.X += (int)Math.Floor(textWidth) + (int)Math.Floor(6 * renderOptions.image_scale);
            default_x = (int)drawPos.X;
            return sectionImage;
        }

        public SKBitmap DrawUsername(SKBitmap sectionImage, List<SKBitmap> imageList, RenderOptions renderOptions, SKPaint nameFont, string userName, SKColor userColor, Size canvasSize, ref Point drawPos)
        {
            using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
            {
                SKPaint userPaint = nameFont;
                if (userName.Any(isNotAscii))
                {
                    userPaint = GetFallbackFont(userName.First(), renderOptions);
                    userPaint.Color = userColor;
                }
                float textWidth = userPaint.MeasureText(userName + ":");
                float xPos = (float)drawPos.X;
                float yPos = (float)(((canvasSize.Height - renderOptions.font_size) / 2) + renderOptions.font_size) - (float)(renderOptions.image_scale * 2);
                if (renderOptions.outline)
                {
                    SKPath outlinePath = userPaint.GetTextPath(userName + ":", xPos, yPos);
                    SKPaint outlinePaint = new SKPaint() { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(4 * renderOptions.image_scale), StrokeJoin = SKStrokeJoin.Round, Color = SKColors.Black, IsAntialias = true, LcdRenderText = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
                    sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
                }
                userPaint.Color = userColor;
                sectionImageCanvas.DrawText(userName + ":", xPos, yPos, userPaint);
                drawPos.X += (int)Math.Floor(textWidth) + (int)Math.Floor(6 * renderOptions.image_scale);
            }
            return sectionImage;
        }

        public SKPaint GetFallbackFont(char input, RenderOptions renderOptions)
        {
            if (fallbackCache.ContainsKey(input))
                return fallbackCache[input];

            SKPaint newPaint = new SKPaint() { Typeface = fontManager.MatchCharacter(input), LcdRenderText = true, TextSize = (float)renderOptions.font_size, IsAntialias = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            fallbackCache.TryAdd(input, newPaint);
            return newPaint;
        }

        private bool isNotAscii(char input)
        {
            return input > 127;
        }

        public SKBitmap DrawBadges(SKBitmap sectionImage, List<SKBitmap> imageList, RenderOptions renderOptions, List<ChatBadge> chatBadges, Comment comment, Size canvasSize, ref Point drawPos)
        {
            if (comment.message.user_badges != null)
            {
                foreach (var badge in comment.message.user_badges)
                {
                    string id = badge._id.ToString();
                    string version = badge.version.ToString();

                    SKBitmap badgeImage = null;
                    foreach (var cachedBadge in chatBadges)
                    {
                        if (cachedBadge.name == id)
                        {
                            foreach (var cachedVersion in cachedBadge.versions)
                            {
                                if (cachedVersion.Key == version)
                                {
                                    badgeImage = cachedVersion.Value;
                                }
                            }
                        }
                    }

                    if (badgeImage != null)
                    {
                        using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                        {
                            float imageRatio = (float)(renderOptions.image_scale * 0.5);
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
                }
            }

            return sectionImage;
        }

        private void BackgroundRenderManager_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            statusProgressBar.Value = 0;
            btnRender.IsEnabled = true;
            if (e.Error == null)
            {
                statusMessage.Text = "Done";
                SetImage("Images/ppHop.gif", true);
            }
            else
            {
                statusMessage.Text = "ERROR";
                SetImage("Images/peepoSad.png", false);
                AppendLog("ERROR: " + e.Error.Message);
                if (e.Error.InnerException != null)
                    AppendLog(e.Error.InnerException.ToString());
            }
        }

        private void BackgroundRenderManager_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            try
            {
                Progress update = (Progress)e.UserState;
                statusProgressBar.Value = e.ProgressPercentage >= 100 ? 100 : e.ProgressPercentage;

                if (e.ProgressPercentage > 0 && !update.justMessage)
                {
                    int timeLeftInt = (int)Math.Floor(100.0 / update.percent_double * update.time_passed) - update.time_passed;
                    TimeSpan timeLeft = new TimeSpan(0, 0, timeLeftInt);
                    statusMessage.Text = String.Format("{0} ({1} left)", update.message, timeLeft.ToString(@"h\hm\ms\s"));
                }
                else
                    statusMessage.Text = update.message;
            }
            catch (Exception ex)
            {
                AppendLog("Error: " + ex.Message);
            }
        }

        private void GetChatBadges(List<ChatBadge> chatBadges, Streamer streamerInfo, RenderOptions renderOptions)
        {
            using (WebClient client = new WebClient())
            {
                //Global chat badges
                JObject globalBadges = JObject.Parse(client.DownloadString("https://badges.twitch.tv/v1/badges/global/display"));
                //Subscriber badges
                JObject subBadges = JObject.Parse(client.DownloadString(String.Format("https://badges.twitch.tv/v1/badges/channels/{0}/display", streamerInfo.id.ToString())));

                foreach (var badge in globalBadges["badge_sets"].Union(subBadges["badge_sets"]))
                {
                    JProperty jBadgeProperty = badge.ToObject<JProperty>();
                    string name = jBadgeProperty.Name;
                    Dictionary<string, SKBitmap> versions = new Dictionary<string, SKBitmap>();

                    foreach (var version in badge.First["versions"])
                    {
                        JProperty jVersionProperty = version.ToObject<JProperty>();
                        string versionString = jVersionProperty.Name;
                        string downloadUrl = version.First["image_url_2x"].ToString();
                        byte[] bytes = client.DownloadData(downloadUrl);
                        MemoryStream ms = new MemoryStream(bytes);
                        try
                        {
                            //For some reason, twitch has corrupted images sometimes :) for example
                            //https://static-cdn.jtvnw.net/badges/v1/a9811799-dce3-475f-8feb-3745ad12b7ea/1
                            SKBitmap badgeImage = SKBitmap.Decode(ms);
                            versions.Add(versionString, badgeImage);
                        }
                        catch (ArgumentException)
                        { }
                    }

                    chatBadges.Add(new ChatBadge(name, versions));
                }
            }
        }

        private void GetThirdPartyEmotes(List<ThirdPartyEmote> thirdPartyEmotes, Streamer streamerInfo, RenderOptions renderOptions, string cacheFolder)
        {
            string bttvFolder = Path.Combine(cacheFolder, "bttv");
            string ffzFolder = Path.Combine(cacheFolder, "ffz");
            if (!Directory.Exists(bttvFolder))
                Directory.CreateDirectory(bttvFolder);
            if (!Directory.Exists(ffzFolder))
                Directory.CreateDirectory(ffzFolder);
            using (WebClient client = new WebClient())
            {
                if (renderOptions.bttv_emotes)
                {
                    //Global BTTV emotes
                    JArray BBTV = JArray.Parse(client.DownloadString("https://api.betterttv.net/3/cached/emotes/global"));
                    foreach (var emote in BBTV)
                    {
                        string id = emote["id"].ToString();
                        byte[] bytes;
                        string fileName = Path.Combine(bttvFolder, id + "_2x.png");
                        if (File.Exists(fileName))
                            bytes = File.ReadAllBytes(fileName);
                        else
                        {
                            bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/emote/{0}/2x", id));
                            File.WriteAllBytes(fileName, bytes);
                        }

                        MemoryStream ms = new MemoryStream(bytes);
                        MemoryStream ms2 = new MemoryStream(bytes);
                        SKBitmap temp_emote = SKBitmap.Decode(ms2);
                        thirdPartyEmotes.Add(new ThirdPartyEmote(new List<SKBitmap>() { temp_emote }, SKCodec.Create(ms), emote["code"].ToString(), emote["imageType"].ToString(), id, 2));
                    }

                    //Channel specific BTTV emotes
                    try
                    {
                        JObject BBTV_channel = JObject.Parse(client.DownloadString("https://api.betterttv.net/3/cached/users/twitch/" + streamerInfo.id));
                        foreach (var emote in BBTV_channel["sharedEmotes"])
                        {
                            string id = emote["id"].ToString();
                            byte[] bytes;
                            string fileName = Path.Combine(bttvFolder, id + "_2x.png");
                            if (File.Exists(fileName))
                                bytes = File.ReadAllBytes(fileName);
                            else
                            {
                                bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/emote/{0}/2x", id));
                                File.WriteAllBytes(fileName, bytes);
                            }
                            MemoryStream ms = new MemoryStream(bytes);
                            MemoryStream ms2 = new MemoryStream(bytes);
                            SKBitmap temp_emote = SKBitmap.Decode(ms2);
                            thirdPartyEmotes.Add(new ThirdPartyEmote(new List<SKBitmap>() { temp_emote }, SKCodec.Create(ms), emote["code"].ToString(), emote["imageType"].ToString(), id, 2));
                        }
                        foreach (var emote in BBTV_channel["channelEmotes"])
                        {
                            string id = emote["id"].ToString();
                            byte[] bytes;
                            string fileName = Path.Combine(bttvFolder, id + "_2x.png");
                            if (File.Exists(fileName))
                                bytes = File.ReadAllBytes(fileName);
                            else
                            {
                                bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/emote/{0}/2x", id));
                                File.WriteAllBytes(fileName, bytes);
                            }
                            MemoryStream ms = new MemoryStream(bytes);
                            MemoryStream ms2 = new MemoryStream(bytes);
                            SKBitmap temp_emote = SKBitmap.Decode(ms2);
                            thirdPartyEmotes.Add(new ThirdPartyEmote(new List<SKBitmap>() { temp_emote }, SKCodec.Create(ms), emote["code"].ToString(), emote["imageType"].ToString(), id, 2));
                        }
                    }
                    catch { }
                }

                if (renderOptions.ffz_emotes)
                {
                    //Global FFZ emotes
                    JArray FFZ = JArray.Parse(client.DownloadString("https://api.betterttv.net/3/cached/frankerfacez/emotes/global"));
                    foreach (var emote in FFZ)
                    {
                        string id = emote["id"].ToString();
                        byte[] bytes;
                        string fileName = Path.Combine(ffzFolder, id + "_1x.png");
                        if (File.Exists(fileName))
                            bytes = File.ReadAllBytes(fileName);
                        else
                        {
                            bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/1", id));
                            File.WriteAllBytes(fileName, bytes);
                        }
                        MemoryStream ms = new MemoryStream(bytes);
                        MemoryStream ms2 = new MemoryStream(bytes);
                        SKBitmap temp_emote = SKBitmap.Decode(ms2);
                        thirdPartyEmotes.Add(new ThirdPartyEmote(new List<SKBitmap>() { temp_emote }, SKCodec.Create(ms), emote["code"].ToString(), emote["imageType"].ToString(), id, 1));
                    }

                    //Channel specific FFZ emotes
                    try
                    {
                        JArray FFZ_channel = JArray.Parse(client.DownloadString("https://api.betterttv.net/3/cached/frankerfacez/users/twitch/" + streamerInfo.id));
                        foreach (var emote in FFZ_channel)
                        {
                            string id = emote["id"].ToString();
                            byte[] bytes;
                            int scale = 2;
                            string fileName = Path.Combine(ffzFolder, id + "_2x.png");
                            try
                            {
                                if (File.Exists(fileName))
                                    bytes = File.ReadAllBytes(fileName);
                                else
                                {
                                    bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/2", id));
                                    File.WriteAllBytes(fileName, bytes);
                                }
                            }
                            catch
                            {
                                fileName = Path.Combine(ffzFolder, id + "_1x.png");
                                if (File.Exists(fileName))
                                    bytes = File.ReadAllBytes(fileName);
                                else
                                {
                                    bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/1", id));
                                    File.WriteAllBytes(fileName, bytes);
                                }
                                scale = 1;
                            }
                            MemoryStream ms = new MemoryStream(bytes);
                            MemoryStream ms2 = new MemoryStream(bytes);
                            SKBitmap temp_emote = SKBitmap.Decode(ms2);
                            thirdPartyEmotes.Add(new ThirdPartyEmote(new List<SKBitmap>() { temp_emote }, SKCodec.Create(ms), emote["code"].ToString(), emote["imageType"].ToString(), id, scale));
                        }
                    }
                    catch { }
                }
            }
        }

        private void GetTwitterEmojis(Dictionary<string, SKBitmap> emojiCache, List<Comment> comments, RenderOptions renderOptions, string cacheFolder, string emojiRegex)
        {
            string emojiFolder = Path.Combine(cacheFolder, "emojis");
            if (!Directory.Exists(emojiFolder))
                Directory.CreateDirectory(emojiFolder);
            using (WebClient client = new WebClient())
            {
                foreach (var comment in comments)
                {
                    foreach (var fragment in comment.message.fragments)
                    {
                        if (fragment.emoticon == null)
                        {
                            string[] fragmentParts = fragment.text.Split(' ');
                            for (int i = 0; i < fragmentParts.Length; i++)
                            {
                                string output = fragmentParts[i].Trim();

                                if (output == "󠀀")
                                    continue;

                                Match m = Regex.Match(output, emojiRegex);
                                if (m.Success)
                                {
                                    for (var k = 0; k < m.Value.Length; k += char.IsSurrogatePair(m.Value, k) ? 2 : 1)
                                    {
                                        string codepoint = String.Format("{0:X4}", char.ConvertToUtf32(m.Value, k)).ToLower();
                                        codepoint = codepoint.Replace("fe0f", "");
                                        if (codepoint != "" && !emojiCache.ContainsKey(codepoint))
                                        {
                                            try
                                            {
                                                byte[] bytes;
                                                string fileName = Path.Combine(emojiFolder, codepoint + ".png");
                                                if (File.Exists(fileName))
                                                    bytes = File.ReadAllBytes(fileName);
                                                else
                                                {
                                                    bytes = client.DownloadData(String.Format("https://abs.twimg.com/emoji/v2/72x72/{0}.png", codepoint));
                                                    File.WriteAllBytes(fileName, bytes);
                                                }

                                                MemoryStream ms = new MemoryStream(bytes);
                                                SKBitmap emojiImage = SKBitmap.Decode(ms);
                                                emojiCache.Add(codepoint, emojiImage);
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void GetEmotes(Dictionary<string, SKBitmap> chatEmotes, List<Comment> comments, RenderOptions renderOptions, string cacheFolder)
        {
            List<string> alreadyAdded = new List<string>();
            List<string> failedEmotes = new List<string>();
            string emoteFolder = Path.Combine(cacheFolder, "emotes");
            if (!Directory.Exists(emoteFolder))
                Directory.CreateDirectory(emoteFolder);
            using (WebClient client = new WebClient())
            {
                foreach (var comment in comments)
                {
                    foreach (var fragment in comment.message.fragments)
                    {
                        if (fragment.emoticon != null)
                        {
                            string id = fragment.emoticon.emoticon_id;
                            if (!alreadyAdded.Contains(id) && !failedEmotes.Contains(id))
                            {
                                try
                                {
                                    string filePath = Path.Combine(emoteFolder, id + "_1x.png");

                                    if (File.Exists(filePath))
                                    {
                                        SKBitmap emoteImage = SKBitmap.Decode(filePath);
                                        chatEmotes.Add(id, emoteImage);
                                        alreadyAdded.Add(id);
                                    }
                                    else
                                    {
                                        byte[] bytes = client.DownloadData(String.Format("https://static-cdn.jtvnw.net/emoticons/v1/{0}/1.0", id));
                                        alreadyAdded.Add(id);
                                        MemoryStream ms = new MemoryStream(bytes);
                                        SKBitmap emoteImage = SKBitmap.Decode(ms);
                                        chatEmotes.Add(id, emoteImage);
                                        File.WriteAllBytes(filePath, bytes);
                                    }
                                }
                                catch (WebException)
                                {
                                    string emoteName = fragment.text;
                                    //lets try waybackmachine, very slow though :(
                                    bool foundEmote = false;

                                    try
                                    {
                                        for (int i = 1; i <= 3; i++)
                                        {
                                            JObject response = JObject.Parse(client.DownloadString($"https://archive.org/wayback/available?url=https://static-cdn.jtvnw.net/emoticons/v1/{id}/{i}.0/"));
                                            if (response["archived_snapshots"]["closest"] != null && response["archived_snapshots"]["closest"]["available"].ToObject<bool>() == true)
                                            {
                                                string filePath = Path.Combine(emoteFolder, id + "_1x.png");
                                                byte[] bytes = client.DownloadData(response["archived_snapshots"]["closest"]["url"].ToString().Replace("/https://static-cdn.jtvnw.net", "if_/https://static-cdn.jtvnw.net"));
                                                File.WriteAllBytes(filePath, bytes);
                                                MemoryStream ms = new MemoryStream(bytes);
                                                SKBitmap emoteImage = SKBitmap.Decode(ms);
                                                SKBitmap emoteImageScaled = new SKBitmap(28, 28);
                                                emoteImage.ScalePixels(emoteImageScaled, SKFilterQuality.High);
                                                alreadyAdded.Add(id);
                                                chatEmotes.Add(id, emoteImageScaled);
                                                emoteImage.Dispose();
                                                foundEmote = true;
                                                break;
                                            }
                                        }
                                    }
                                    catch { }


                                    if (foundEmote)
                                        continue;
                                    else
                                    {
                                        //sometimes emote still exists but id is different, I use twitch metrics because I can't find an api to find an emote by name
                                        try
                                        {
                                            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://www.twitchmetrics.net/e/" + emoteName);
                                            request.AllowAutoRedirect = false;
                                            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                                            string redirUrl = response.Headers["Location"];
                                            response.Close();
                                            string newId = redirUrl.Split('/').Last().Split('-').First();
                                            byte[] bytes = client.DownloadData(String.Format("https://static-cdn.jtvnw.net/emoticons/v1/{0}/1.0", newId));
                                            string filePath = Path.Combine(emoteFolder, id + "_1x.png");
                                            File.WriteAllBytes(filePath, bytes);
                                            alreadyAdded.Add(id);
                                            MemoryStream ms = new MemoryStream(bytes);
                                            SKBitmap emoteImage = SKBitmap.Decode(ms);
                                            chatEmotes.Add(id, emoteImage);
                                            foundEmote = true;
                                        }
                                        catch
                                        {

                                        }
                                    }
                                    if (!foundEmote)
                                    {
                                        AppendLog($"Unable to fetch emote {emoteName}(ID:{id})");
                                        failedEmotes.Add(id);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        private void GetBits(List<CheerEmote> cheerEmotes, RenderOptions renderOptions, string cacheFolder, string emojiRegex)
        {
            string bitsFolder = Path.Combine(cacheFolder, "bits");
            if (!Directory.Exists(bitsFolder))
                Directory.CreateDirectory(bitsFolder);

            using (WebClient client = new WebClient())
            {
                client.Headers.Add("Accept", "application/vnd.twitchtv.v5+json");
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                JObject globalCheer = JObject.Parse(client.DownloadString("https://api.twitch.tv/kraken/bits/actions"));

                foreach (JToken emoteToken in globalCheer["actions"])
                {
                    string prefix = emoteToken["prefix"].ToString();
                    List<KeyValuePair<int, ThirdPartyEmote>> tierList = new List<KeyValuePair<int, ThirdPartyEmote>>();
                    CheerEmote newEmote = new CheerEmote() {prefix = prefix, tierList = tierList };
                    byte[] finalBytes = null;
                    foreach (JToken tierToken in emoteToken["tiers"])
                    {
                        try
                        {
                            int minBits = tierToken["min_bits"].ToObject<int>();
                            string fileName = Path.Combine(bitsFolder, prefix + minBits + "_2x.gif");

                            if (File.Exists(fileName))
                            {
                                finalBytes = File.ReadAllBytes(fileName);
                            }
                            else
                            {
                                byte[] bytes = client.DownloadData(tierToken["images"]["dark"]["animated"]["2"].ToString());
                                File.WriteAllBytes(fileName, bytes);
                                finalBytes = bytes;
                            }

                            if (finalBytes != null)
                            {
                                MemoryStream ms = new MemoryStream(finalBytes);
                                MemoryStream ms2 = new MemoryStream(finalBytes);
                                SKBitmap finalBitmap = SKBitmap.Decode(ms);
                                ThirdPartyEmote emote = new ThirdPartyEmote(new List<SKBitmap>() { finalBitmap }, SKCodec.Create(ms2), prefix, "gif", "", 2);
                                tierList.Add(new KeyValuePair<int, ThirdPartyEmote>(minBits, emote));
                            }
                        }
                        catch
                        { }
                    }
                    cheerEmotes.Add(newEmote);
                }
            }
        }

        private void LoadSettings()
        {
            try
            {
                comboFont.SelectedItem = Settings.Default.Font;
                checkOutline.IsChecked = Settings.Default.Outline;
                checkTimestamp.IsChecked = Settings.Default.Timestamp;
                colorBackground.SelectedColor = System.Windows.Media.Color.FromArgb((byte)Settings.Default.BackgroundColorA, (byte)Settings.Default.BackgroundColorR, (byte)Settings.Default.BackgroundColorG, (byte)Settings.Default.BackgroundColorB);
                checkFFZ.IsChecked = Settings.Default.FFZEmotes;
                checkBTTV.IsChecked = Settings.Default.BTTVEmotes;
                textHeight.Text = Settings.Default.Height.ToString();
                textWidth.Text = Settings.Default.Width.ToString();
                textFontSize.Text = Settings.Default.FontSize.ToString("0.##");
                textUpdateTime.Text = Settings.Default.UpdateTime.ToString("0.##");
                colorFont.SelectedColor = System.Windows.Media.Color.FromRgb((byte)Settings.Default.FontColorR, (byte)Settings.Default.FontColorG, (byte)Settings.Default.FontColorB);
                textFramerate.Text = Settings.Default.Framerate.ToString();

                foreach (VideoContainer container in comboFormat.Items)
                {
                    if (container.Name == Settings.Default.VideoContainer)
                    {
                        comboFormat.SelectedItem = container;
                        foreach (Codec codec in container.SupportedCodecs)
                        {
                            comboCodec.Items.Add(codec);
                            if (codec.Name == Settings.Default.VideoCodec)
                                comboCodec.SelectedItem = codec;
                        }
                        break;
                    }
                }

                comboFormat.SelectionChanged += ComboFormatOnSelectionChanged;
                comboCodec.SelectionChanged += ComboCodecOnSelectionChanged;
            }
            catch { }
        }

        private void ComboCodecOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboCodec.SelectedItem != null)
            {
                Settings.Default.FfmpegInputArgs = ((Codec)comboCodec.SelectedItem).InputArgs;
                Settings.Default.FfmpegOutputArgs = ((Codec)comboCodec.SelectedItem).OutputArgs;
                Settings.Default.Save();
            }
        }

        private void ComboFormatOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            VideoContainer currentContainer = (VideoContainer)comboFormat.SelectedItem;
            comboCodec.Items.Clear();
            foreach (Codec codec in currentContainer.SupportedCodecs)
            {
                comboCodec.Items.Add(codec);
                if (Settings.Default.VideoCodec == codec.Name)
                    comboCodec.SelectedItem = Settings.Default.VideoCodec;
            }

            if (comboCodec.SelectedItem == null)
                comboCodec.SelectedIndex = 0;
        }

        private void SaveSettings()
        {
            Settings.Default.Font = comboFont.SelectedItem.ToString();
            Settings.Default.Outline = (bool)checkOutline.IsChecked;
            Settings.Default.Timestamp = (bool)checkTimestamp.IsChecked;
            Settings.Default.BackgroundColorR = colorBackground.SelectedColor.Value.R;
            Settings.Default.BackgroundColorG = colorBackground.SelectedColor.Value.G;
            Settings.Default.BackgroundColorB = colorBackground.SelectedColor.Value.B;
            Settings.Default.BackgroundColorA = colorBackground.SelectedColor.Value.A;
            Settings.Default.FFZEmotes = (bool)checkFFZ.IsChecked;
            Settings.Default.BTTVEmotes = (bool)checkBTTV.IsChecked;
            Settings.Default.FontColorR = colorFont.SelectedColor.Value.R;
            Settings.Default.FontColorG = colorFont.SelectedColor.Value.G;
            Settings.Default.FontColorB = colorFont.SelectedColor.Value.B;
            if (comboFormat.SelectedItem != null)
                Settings.Default.VideoContainer = ((VideoContainer)comboFormat.SelectedItem).Name;
            if (comboCodec.SelectedItem != null)
                Settings.Default.VideoCodec = ((Codec)comboCodec.SelectedItem).Name;
            try
            {
                Settings.Default.Height = Int32.Parse(textHeight.Text);
                Settings.Default.Width = Int32.Parse(textWidth.Text);
                Settings.Default.FontSize = float.Parse(textFontSize.Text);
                Settings.Default.UpdateTime = float.Parse(textUpdateTime.Text);
                Settings.Default.Framerate = Int32.Parse(textFramerate.Text);
            }
            catch { }
            Settings.Default.Save();
        }

        private bool ValidateInputs()
        {
            if (!File.Exists(textJson.Text))
            {
                AppendLog("ERROR: JSON File Not Found");
                return false;
            }

            try
            {
                Double.Parse(textFontSize.Text);
                Int32.Parse(textHeight.Text);
                Int32.Parse(textWidth.Text);
                Double.Parse(textUpdateTime.Text);
                Int32.Parse(textFramerate.Text);
            }
            catch (Exception ex)
            {
                AppendLog("ERROR: " + ex.Message);
                return false;
            }

            if (colorBackground.SelectedColor.Value.A < 255)
            {
                if ((((VideoContainer)comboFormat.SelectedItem).Name == "MOV" && ( ((Codec)comboCodec.SelectedItem).Name == "RLE") || ((Codec)comboCodec.SelectedItem).Name == "ProRes") || ((VideoContainer)comboFormat.SelectedItem).Name == "WEBM")
                {
                    return true;
                }
                else
                {
                    AppendLog("ERROR: You've selected an alpha channel (transparency) for a container/codec that does not support it.");
                    AppendLog("Remove transparency or encode with MOV and RLE/PRORES (file size will be large)");
                    return false;
                }
            }

            return true;
        }

        private void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke((Action)(() =>
                textLog.AppendText(message + Environment.NewLine)
            ));
        }

        public void SetImage(string imageUri, bool isGif)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(imageUri, UriKind.Relative);
            image.EndInit();
            if (isGif)
                ImageBehavior.SetAnimatedSource(statusImage, image);
            else
            {
                ImageBehavior.SetAnimatedSource(statusImage, null);
                statusImage.Source = image;
            }
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            List<string> fonts = new List<string>();
            foreach (var fontFamily in fontManager.FontFamilies)
                fonts.Add(fontFamily);
            fonts.Sort();
            foreach (var font in fonts)
            {
                comboFont.Items.Add(font);
            }
            if (comboFont.Items.Contains("Arial"))
                comboFont.SelectedItem = "Arial";

            Codec h264Codec = new Codec() { Name = "H264", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"" };
            Codec h265Codec = new Codec() { Name = "H265", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libx265 -preset veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"" };
            Codec vp8Codec = new Codec() { Name = "VP8", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx -crf 18 -b:v 2M -pix_fmt yuva420p -auto-alt-ref 0 \"{save_path}\"" };
            Codec vp9Codec = new Codec() { Name = "VP9", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v libvpx-vp9 -crf 18 -b:v 2M -pix_fmt yuva420p \"{save_path}\"" };
            Codec rleCodec = new Codec() { Name = "RLE", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v qtrle -pix_fmt argb \"{save_path}\"" };
            Codec proresCodec = new Codec() { Name = "ProRes", InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", OutputArgs = "-c:v prores_ks -pix_fmt argb \"{save_path}\"" };
            VideoContainer mp4Container = new VideoContainer() { Name = "MP4", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec } };
            VideoContainer movContainer = new VideoContainer() { Name = "MOV", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec, rleCodec, proresCodec } };
            VideoContainer webmContainer = new VideoContainer() { Name = "WEBM", SupportedCodecs = new List<Codec>() { vp8Codec, vp9Codec } };
            VideoContainer mkvContainer = new VideoContainer() { Name = "MKV", SupportedCodecs = new List<Codec>() { h264Codec, h265Codec, vp8Codec, vp9Codec } };
            comboFormat.Items.Add(mp4Container);
            comboFormat.Items.Add(movContainer);
            comboFormat.Items.Add(webmContainer);
            comboFormat.Items.Add(mkvContainer);

            LoadSettings();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            if (windowPreview != null && windowPreview.IsLoaded)
            {
                windowPreview.Close();
            }
        }

        private void btnFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            FfmpegOptions ffmpegOptions = new FfmpegOptions(this);
            ffmpegOptions.ShowDialog();
        }

        private void btnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (windowPreview == null || (windowPreview != null && !windowPreview.IsLoaded))
            {
                windowPreview = new WindowPreview();
                windowPreview.Update(this);
                windowPreview.Show();
            }
        }

        public void Update()
        {
            if (windowPreview != null && windowPreview.IsInitialized)
                windowPreview.Update(this);
        }

        private void UpdatePreview(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Update();
        }

        private void UpdateColor(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            Update();
        }

        private void UpdateFont(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Update();
        }

        private void UpdateCheckbox(object sender, RoutedEventArgs e)
        {
            Update();
        }
    }

    public class VideoContainer
    {
        public string Name;
        public List<Codec> SupportedCodecs;

        public override string ToString()
        {
            return Name;
        }
    }

    public class Codec
    {
        public string Name;
        public string InputArgs;
        public string OutputArgs;

        public override string ToString()
        {
            return Name;
        }
    }

    public class CheerEmote
    {
        public string prefix { get; set; }
        public List<KeyValuePair<int, ThirdPartyEmote>> tierList = new List<KeyValuePair<int, ThirdPartyEmote>>();

        public KeyValuePair<int, ThirdPartyEmote> getTier(int value)
        {
            KeyValuePair<int, ThirdPartyEmote> returnPair = tierList.First();
            foreach (KeyValuePair<int, ThirdPartyEmote> tierPair in tierList)
            {
                if (tierPair.Key > value)
                    break;
                returnPair = tierPair;
            }

            return returnPair;
        }
    }
}