using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using WpfAnimatedGif;
using System.Net;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Accord.Video.FFMPEG;
using System.Drawing.Imaging;
using System.Windows.Markup;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using SkiaSharp;
using TwitchDownloader.Properties;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageChatRender.xaml
    /// </summary>
    public partial class PageChatRender : System.Windows.Controls.Page
    {
        public bool lastHasEmoteAtEnd = false;
        public SKPaint imagePaint = new SKPaint() { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        public SKFontManager fontManager = SKFontManager.CreateDefault();
        public ConcurrentDictionary<char, SKPaint> fallbackCache = new ConcurrentDictionary<char, SKPaint>();

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

                saveFileDialog.Filter = "MP4 Files | *.mp4";
                saveFileDialog.RestoreDirectory = true;

                if (saveFileDialog.ShowDialog() == true)
                {
                    SKColor backgroundColor = new SKColor(colorBackground.SelectedColor.Value.R, colorBackground.SelectedColor.Value.G, colorBackground.SelectedColor.Value.B);
                    System.Drawing.Color backgroundColorDrawing = System.Drawing.Color.FromArgb((int)colorBackground.SelectedColor.Value.R, (int)colorBackground.SelectedColor.Value.G, (int)colorBackground.SelectedColor.Value.B);
                    RenderOptions info = new RenderOptions(textJson.Text, saveFileDialog.FileName, backgroundColor, Int32.Parse(textHeight.Text), Int32.Parse(textWidth.Text), (bool)checkBTTV.IsChecked, (bool)checkFFZ.IsChecked, (bool)checkOutline.IsChecked, (string)comboFont.SelectedItem, Double.Parse(textFontSize.Text), Double.Parse(textUpdateTime.Text), (bool)checkCompact.IsChecked, backgroundColorDrawing, (bool)checkTimestamp.IsChecked);
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
            }
            catch (JsonSerializationException)
            {
                chatJson = new ChatRoot();
                chatJson.comments = JsonConvert.DeserializeObject<List<Comment>>(File.ReadAllText(renderOptions.json_path));
                chatJson.streamer = new Streamer();
                chatJson.streamer.id = Int32.Parse(chatJson.comments.First().channel_id);
                chatJson.streamer.name = "";
            }
            BlockingCollection<TwitchComment> finalComments = new  BlockingCollection<TwitchComment>();
            List<ThirdPartyEmote> thirdPartyEmotes = new List<ThirdPartyEmote>();
            List<ChatBadge> chatBadges = new List<ChatBadge>();
            Dictionary<string, SKBitmap> chatEmotes = new Dictionary<string, SKBitmap>();
            Dictionary<string, SKBitmap> emojiCache = new Dictionary<string, SKBitmap>();
            Random rand = new Random();
            string[] defaultColors = { "#FF0000", "#0000FF", "#00FF00", "#B22222", "#FF7F50", "#9ACD32", "#FF4500", "#2E8B57", "#DAA520", "#D2691E", "#5F9EA0", "#1E90FF", "#FF69B4", "#8A2BE2", "#00FF7F" };
            string emojiRegex = "[#*0-9]\uFE0F\u20E3|[\u00A9\u00AE\u203C\u2049\u2122\u2139\u2194-\u2199\u21A9\u21AA\u231A\u231B\u2328\u23CF\u23E9-\u23F3\u23F8-\u23FA\u24C2\u25AA\u25AB\u25B6\u25C0\u25FB-\u25FE\u2600-\u2604\u260E\u2611\u2614\u2615\u2618]|\u261D(?:\uD83C[\uDFFB-\uDFFF])?|[\u2620\u2622\u2623\u2626\u262A\u262E\u262F\u2638-\u263A\u2640\u2642\u2648-\u2653\u265F\u2660\u2663\u2665\u2666\u2668\u267B\u267E\u267F\u2692-\u2697\u2699\u269B\u269C\u26A0\u26A1\u26AA\u26AB\u26B0\u26B1\u26BD\u26BE\u26C4\u26C5\u26C8\u26CE\u26CF\u26D1\u26D3\u26D4\u26E9\u26EA\u26F0-\u26F5\u26F7\u26F8]|\u26F9(?:\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?|\uFE0F\u200D[\u2640\u2642]\uFE0F)?|[\u26FA\u26FD\u2702\u2705\u2708\u2709]|[\u270A-\u270D](?:\uD83C[\uDFFB-\uDFFF])?|[\u270F\u2712\u2714\u2716\u271D\u2721\u2728\u2733\u2734\u2744\u2747\u274C\u274E\u2753-\u2755\u2757\u2763\u2764\u2795-\u2797\u27A1\u27B0\u27BF\u2934\u2935\u2B05-\u2B07\u2B1B\u2B1C\u2B50\u2B55\u3030\u303D\u3297\u3299]|\uD83C(?:[\uDC04\uDCCF\uDD70\uDD71\uDD7E\uDD7F\uDD8E\uDD91-\uDD9A]|\uDDE6\uD83C[\uDDE8-\uDDEC\uDDEE\uDDF1\uDDF2\uDDF4\uDDF6-\uDDFA\uDDFC\uDDFD\uDDFF]|\uDDE7\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEF\uDDF1-\uDDF4\uDDF6-\uDDF9\uDDFB\uDDFC\uDDFE\uDDFF]|\uDDE8\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDEE\uDDF0-\uDDF5\uDDF7\uDDFA-\uDDFF]|\uDDE9\uD83C[\uDDEA\uDDEC\uDDEF\uDDF0\uDDF2\uDDF4\uDDFF]|\uDDEA\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDED\uDDF7-\uDDFA]|\uDDEB\uD83C[\uDDEE-\uDDF0\uDDF2\uDDF4\uDDF7]|\uDDEC\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEE\uDDF1-\uDDF3\uDDF5-\uDDFA\uDDFC\uDDFE]|\uDDED\uD83C[\uDDF0\uDDF2\uDDF3\uDDF7\uDDF9\uDDFA]|\uDDEE\uD83C[\uDDE8-\uDDEA\uDDF1-\uDDF4\uDDF6-\uDDF9]|\uDDEF\uD83C[\uDDEA\uDDF2\uDDF4\uDDF5]|\uDDF0\uD83C[\uDDEA\uDDEC-\uDDEE\uDDF2\uDDF3\uDDF5\uDDF7\uDDFC\uDDFE\uDDFF]|\uDDF1\uD83C[\uDDE6-\uDDE8\uDDEE\uDDF0\uDDF7-\uDDFB\uDDFE]|\uDDF2\uD83C[\uDDE6\uDDE8-\uDDED\uDDF0-\uDDFF]|\uDDF3\uD83C[\uDDE6\uDDE8\uDDEA-\uDDEC\uDDEE\uDDF1\uDDF4\uDDF5\uDDF7\uDDFA\uDDFF]|\uDDF4\uD83C\uDDF2|\uDDF5\uD83C[\uDDE6\uDDEA-\uDDED\uDDF0-\uDDF3\uDDF7-\uDDF9\uDDFC\uDDFE]|\uDDF6\uD83C\uDDE6|\uDDF7\uD83C[\uDDEA\uDDF4\uDDF8\uDDFA\uDDFC]|\uDDF8\uD83C[\uDDE6-\uDDEA\uDDEC-\uDDF4\uDDF7-\uDDF9\uDDFB\uDDFD-\uDDFF]|\uDDF9\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDED\uDDEF-\uDDF4\uDDF7\uDDF9\uDDFB\uDDFC\uDDFF]|\uDDFA\uD83C[\uDDE6\uDDEC\uDDF2\uDDF3\uDDF8\uDDFE\uDDFF]|\uDDFB\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDEE\uDDF3\uDDFA]|\uDDFC\uD83C[\uDDEB\uDDF8]|\uDDFD\uD83C\uDDF0|\uDDFE\uD83C[\uDDEA\uDDF9]|\uDDFF\uD83C[\uDDE6\uDDF2\uDDFC]|[\uDE01\uDE02\uDE1A\uDE2F\uDE32-\uDE3A\uDE50\uDE51\uDF00-\uDF21\uDF24-\uDF84]|\uDF85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDF86-\uDF93\uDF96\uDF97\uDF99-\uDF9B\uDF9E-\uDFC1]|\uDFC2(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC3\uDFC4](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDFC5\uDFC6]|\uDFC7(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC8\uDFC9]|\uDFCA(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDFCB\uDFCC](?:\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?|\uFE0F\u200D[\u2640\u2642]\uFE0F)?|[\uDFCD-\uDFF0]|\uDFF3(?:\uFE0F\u200D\uD83C\uDF08)?|\uDFF4(?:\u200D\u2620\uFE0F|\uDB40\uDC67\uDB40\uDC62\uDB40(?:\uDC65\uDB40\uDC6E\uDB40\uDC67|\uDC73\uDB40\uDC63\uDB40\uDC74|\uDC77\uDB40\uDC6C\uDB40\uDC73)\uDB40\uDC7F)?|[\uDFF5\uDFF7-\uDFFF])|\uD83D(?:[\uDC00-\uDC14]|\uDC15(?:\u200D\uD83E\uDDBA)?|[\uDC16-\uDC40]|\uDC41(?:\uFE0F\u200D\uD83D\uDDE8\uFE0F)?|[\uDC42\uDC43](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC44\uDC45]|[\uDC46-\uDC50](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC51-\uDC65]|[\uDC66\uDC67](?:\uD83C[\uDFFB-\uDFFF])?|\uDC68(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\u2764\uFE0F\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|[\uDC68\uDC69]\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD]))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C\uDFFB|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFC]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFD]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC69(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\u2764\uFE0F\u200D\uD83D(?:\uDC8B\u200D\uD83D)?[\uDC68\uDC69]|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|\uDC69\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFC-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC68\uD83C[\uDFFB\uDFFD-\uDFFF]|\uDC69\uD83C\uDFFB)|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC68\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|\uDC69\uD83C[\uDFFB\uDFFC])|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC68\uD83C[\uDFFB-\uDFFD\uDFFF]|\uDC69\uD83C[\uDFFB-\uDFFD])|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC6A|[\uDC6B-\uDC6D](?:\uD83C[\uDFFB-\uDFFF])?|\uDC6E(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC6F(?:\u200D[\u2640\u2642]\uFE0F)?|\uDC70(?:\uD83C[\uDFFB-\uDFFF])?|\uDC71(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC72(?:\uD83C[\uDFFB-\uDFFF])?|\uDC73(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDC74-\uDC76](?:\uD83C[\uDFFB-\uDFFF])?|\uDC77(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC78(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC79-\uDC7B]|\uDC7C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC7D-\uDC80]|[\uDC81\uDC82](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC83(?:\uD83C[\uDFFB-\uDFFF])?|\uDC84|\uDC85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC86\uDC87](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDC88-\uDCA9]|\uDCAA(?:\uD83C[\uDFFB-\uDFFF])?|[\uDCAB-\uDCFD\uDCFF-\uDD3D\uDD49-\uDD4E\uDD50-\uDD67\uDD6F\uDD70\uDD73]|\uDD74(?:\uD83C[\uDFFB-\uDFFF])?|\uDD75(?:\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?|\uFE0F\u200D[\u2640\u2642]\uFE0F)?|[\uDD76-\uDD79]|\uDD7A(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD87\uDD8A-\uDD8D]|[\uDD90\uDD95\uDD96](?:\uD83C[\uDFFB-\uDFFF])?|[\uDDA4\uDDA5\uDDA8\uDDB1\uDDB2\uDDBC\uDDC2-\uDDC4\uDDD1-\uDDD3\uDDDC-\uDDDE\uDDE1\uDDE3\uDDE8\uDDEF\uDDF3\uDDFA-\uDE44]|[\uDE45-\uDE47](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDE48-\uDE4A]|\uDE4B(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDE4C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE4D\uDE4E](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDE4F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE80-\uDEA2]|\uDEA3(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDEA4-\uDEB3]|[\uDEB4-\uDEB6](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDEB7-\uDEBF]|\uDEC0(?:\uD83C[\uDFFB-\uDFFF])?|[\uDEC1-\uDEC5\uDECB]|\uDECC(?:\uD83C[\uDFFB-\uDFFF])?|[\uDECD-\uDED2\uDED5\uDEE0-\uDEE5\uDEE9\uDEEB\uDEEC\uDEF0\uDEF3-\uDEFA\uDFE0-\uDFEB])|\uD83E(?:[\uDD0D\uDD0E]|\uDD0F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD10-\uDD17]|[\uDD18-\uDD1C](?:\uD83C[\uDFFB-\uDFFF])?|\uDD1D|[\uDD1E\uDD1F](?:\uD83C[\uDFFB-\uDFFF])?|[\uDD20-\uDD25]|\uDD26(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDD27-\uDD2F]|[\uDD30-\uDD36](?:\uD83C[\uDFFB-\uDFFF])?|\uDD37(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDD38\uDD39](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDD3A|\uDD3C(?:\u200D[\u2640\u2642]\uFE0F)?|[\uDD3D\uDD3E](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDD3F-\uDD45\uDD47-\uDD71\uDD73-\uDD76\uDD7A-\uDDA2\uDDA5-\uDDAA\uDDAE-\uDDB4]|[\uDDB5\uDDB6](?:\uD83C[\uDFFB-\uDFFF])?|\uDDB7|[\uDDB8\uDDB9](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDDBA|\uDDBB(?:\uD83C[\uDFFB-\uDFFF])?|[\uDDBC-\uDDCA]|[\uDDCD-\uDDCF](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDDD0|\uDDD1(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1|\uD83C(?:\uDFFB(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C\uDFFB)?|\uDFFC(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB\uDFFC])?|\uDFFD(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFD])?|\uDFFE(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFE])?|\uDFFF(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])?))?|[\uDDD2-\uDDD5](?:\uD83C[\uDFFB-\uDFFF])?|\uDDD6(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDDD7-\uDDDD](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDDDE\uDDDF](?:\u200D[\u2640\u2642]\uFE0F)?|[\uDDE0-\uDDFF\uDE70-\uDE73\uDE78-\uDE7A\uDE80-\uDE82\uDE90-\uDE95])";
            string tempFolder = Path.Combine(Path.GetTempPath(), "TwitchDownloader");
            string downloadFolder = Path.Combine(tempFolder, "Chat Render");

            if (!Directory.Exists(downloadFolder))
                Directory.CreateDirectory(downloadFolder);

            (sender as BackgroundWorker).ReportProgress(0, new Progress("Fetching Chat Badges"));
            GetChatBadges(chatBadges, chatJson.streamer, renderOptions);
            (sender as BackgroundWorker).ReportProgress(0, new Progress("Fetching Emotes"));
            GetEmotes(chatEmotes, chatJson.comments, renderOptions);
            (sender as BackgroundWorker).ReportProgress(0, new Progress("Fetching Third Party Emotes"));
            GetThirdPartyEmotes(thirdPartyEmotes, chatJson.streamer, renderOptions);
            (sender as BackgroundWorker).ReportProgress(0, new Progress("Fetching Twitter Emojis"));
            GetTwitterEmojis(emojiCache, chatJson.comments, renderOptions, emojiRegex);

            Size canvasSize = new Size(renderOptions.chat_width, renderOptions.has_emote_height);
            SKPaint nameFont = new SKPaint() { Typeface = SKTypeface.FromFamilyName(renderOptions.font, SKFontStyle.Bold), LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.font_size, IsAntialias = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            SKPaint messageFont = new SKPaint() { Typeface = SKTypeface.FromFamilyName(renderOptions.font, SKFontStyle.Normal), LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.font_size, IsAntialias = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High, Color = SKColors.White };

            (sender as BackgroundWorker).ReportProgress(0, new Progress("Rendering Comments"));
            foreach (Comment comment in chatJson.comments)
            {
                if (comment.source != "chat")
                    continue;
                if (comment.message.user_notice_params != null && (comment.message.user_notice_params.msg_id != null && comment.message.user_notice_params.msg_id != ""))
                    continue;

                string userName = comment.commenter.display_name.ToString();
                int default_x = 2;
                Point drawPos = new Point(default_x, 0);
                System.Drawing.Color userColorSystemDrawing = System.Drawing.ColorTranslator.FromHtml(comment.message.user_color != null ? comment.message.user_color : defaultColors[rand.Next(0, defaultColors.Length)]);
                SKColor userColor = new SKColor(userColorSystemDrawing.R, userColorSystemDrawing.G, userColorSystemDrawing.B);

                List<SKBitmap> imageList = new List<SKBitmap>();
                SKBitmap sectionImage = new SKBitmap((int)canvasSize.Width, (int)canvasSize.Height);
                List<GifEmote> currentGifEmotes = new List<GifEmote>();
                new SKCanvas(sectionImage).Clear(renderOptions.background_color);
                if (renderOptions.chat_timestamp)
                    sectionImage = DrawTimestamp(sectionImage, imageList, messageFont, renderOptions, comment, canvasSize, ref drawPos, ref default_x);
                sectionImage = DrawBadges(sectionImage, imageList, renderOptions, chatBadges, comment, canvasSize, ref drawPos);
                sectionImage = DrawUsername(sectionImage, imageList, renderOptions, nameFont, userName, userColor, canvasSize, ref drawPos);
                sectionImage = DrawMessage(sectionImage, imageList, renderOptions, currentGifEmotes, messageFont, emojiCache, chatEmotes, thirdPartyEmotes, comment, canvasSize, ref drawPos, emojiRegex, ref default_x);

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

                string imagePath = Path.Combine(downloadFolder, Guid.NewGuid() + ".png");
                finalComments.Add(new TwitchComment(imagePath, Double.Parse(comment.content_offset_seconds.ToString()), currentGifEmotes));
                using (Stream s = File.OpenWrite(imagePath))
                {
                    SKImage.FromBitmap(finalImage).Encode(SKEncodedImageFormat.Png, 80).SaveTo(s);
                }
                finalImage.Dispose();
                finalImageCanvas.Dispose();
                int percent = (int)Math.Floor(((double)finalComments.Count / (double)chatJson.comments.Count) * 100);
                (sender as BackgroundWorker).ReportProgress(percent, new Progress("Rendering Comments"));
            }

            (sender as BackgroundWorker).ReportProgress(0, new Progress("Rendering Video 0%"));
            RenderVideo(renderOptions, new List<TwitchComment>(finalComments.ToArray()), chatJson.comments, sender);

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

        private void RenderVideo(RenderOptions renderOptions, List<TwitchComment> finalComments, List<Comment> comments, object sender)
        {
            System.Drawing.Bitmap canvas = new System.Drawing.Bitmap(renderOptions.chat_width, renderOptions.chat_height);
            System.Drawing.Graphics gcan = System.Drawing.Graphics.FromImage(canvas);
            int videoStart = (int)Math.Floor(comments.First().content_offset_seconds);
            int duration = (int)Math.Ceiling(comments.Last().content_offset_seconds) - videoStart;
            List<GifEmote> displayedGifs = new List<GifEmote>();
            System.Drawing.Color backgroundColor = renderOptions.background_color_drawing;
            Stopwatch stopwatch = new Stopwatch();
            using (var vFWriter = new VideoFileWriter())
            {
                // create new video file
                stopwatch.Start();
                vFWriter.Open(renderOptions.save_path, renderOptions.chat_width, renderOptions.chat_height, 60, VideoCodec.H264, 6000000);
                gcan.FillRectangle(new System.Drawing.SolidBrush(backgroundColor), 0, 0, renderOptions.chat_width, renderOptions.chat_height);
                int startTick = (int)Math.Floor(videoStart / (1.0 / 60.0));
                int endTick = (int)Math.Floor((videoStart + duration) / (1.0 / 60.0));
                int lastUpdateTick = startTick;
                int globalTick = startTick;
                for (int i = startTick; i < endTick; i++)
                {
                    int height = 0;
                    if (globalTick % renderOptions.update_frame == 0)
                    {
                        int y = 0;
                        List<GifEmote> newly_added = new List<GifEmote>();
                        List<GifEmote> old = new List<GifEmote>(displayedGifs);
                        for (int j = 0; j < finalComments.Count; j++)
                        {
                            int commentTick = (int)Math.Floor(finalComments[j].secondsOffset / (1.0 / 60.0));
                            if (commentTick >= lastUpdateTick && commentTick < globalTick)
                            {
                                System.Drawing.Bitmap sectionImage = (System.Drawing.Bitmap)System.Drawing.Bitmap.FromFile(finalComments[j].section);
                                foreach (var emote in finalComments[j].gifEmotes)
                                {
                                    GifEmote newGif = new GifEmote(new Point(emote.offset.X, emote.offset.Y + height), emote.name, emote.image, emote.imageScale);
                                    displayedGifs.Add(newGif);
                                    newly_added.Add(newGif);
                                }
                                height += sectionImage.Height;
                            }
                        }
                        foreach (var emote in old)
                            emote.offset = new Point(emote.offset.X, emote.offset.Y - height);
                        foreach (var emote in newly_added)
                            emote.offset = new Point(emote.offset.X, (renderOptions.chat_height - height) + emote.offset.Y);

                        if (height > 0)
                        {
                            System.Drawing.Bitmap buffer = new System.Drawing.Bitmap(renderOptions.chat_width, height);
                            System.Drawing.Graphics bg = System.Drawing.Graphics.FromImage(buffer);

                            for (int j = 0; j < finalComments.Count; j++)
                            {
                                int commentTick = (int)Math.Floor(finalComments[j].secondsOffset / (1.0 / 60.0));
                                if (commentTick >= lastUpdateTick && commentTick < globalTick)
                                {
                                    System.Drawing.Bitmap sectionImage = (System.Drawing.Bitmap)System.Drawing.Bitmap.FromFile(finalComments[j].section);
                                    bg.DrawImage(sectionImage, 0, y);
                                    y += sectionImage.Height;
                                }
                            }
                            gcan = System.Drawing.Graphics.FromImage(canvas);
                            gcan.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                            gcan.DrawImage(canvas, 0, -height);
                            gcan.DrawImage(buffer, 0, renderOptions.chat_height - height);
                        }
                        lastUpdateTick = globalTick;
                    }
                    List<GifEmote> to_remove = new List<GifEmote>();
                    foreach (var emote in displayedGifs)
                    {
                        if (emote.offset.Y < -emote.image.Width - renderOptions.chat_height)
                        {
                            to_remove.Add(emote);
                        }
                        else
                        {
                            int gifTime = (int)Math.Floor(1.5 * globalTick) % emote.total_duration;
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
                            FrameDimension dim = new FrameDimension(emote.image.FrameDimensionsList[0]);
                            emote.image.SelectActiveFrame(dim, frame);
                            gcan.DrawImage(emote.image, (float)emote.offset.X, (float)emote.offset.Y + (int)Math.Floor((renderOptions.has_emote_height - ((emote.image.Height/emote.imageScale) * renderOptions.image_scale)) /2.0), (float)((emote.image.Width / emote.imageScale)*renderOptions.image_scale), (float)((emote.image.Height / emote.imageScale)*renderOptions.image_scale));
                        }
                    }

                    foreach (var emote in to_remove)
                    {
                        displayedGifs.Remove(emote);
                    }

                    vFWriter.WriteVideoFrame(canvas);
                    foreach (var emote in displayedGifs)
                    {
                        gcan.FillRectangle(new System.Drawing.SolidBrush(backgroundColor), (float)emote.offset.X, (float)emote.offset.Y + (int)Math.Floor((renderOptions.has_emote_height - ((emote.image.Height / emote.imageScale) * renderOptions.image_scale)) / 2.0), (float)((emote.image.Width / emote.imageScale) * renderOptions.image_scale), (float)((emote.image.Height / emote.imageScale) * renderOptions.image_scale));
                    }
                    globalTick += 1;
                    double percentDouble = (double)(globalTick - startTick) / (double)(endTick - startTick) * 100.0;
                    int percentInt = (int)Math.Floor(percentDouble);
                    (sender as BackgroundWorker).ReportProgress(percentInt, new Progress(String.Format("Rendering Video {0}%", percentInt), (int)Math.Floor(stopwatch.Elapsed.TotalSeconds), percentDouble));
                }
                vFWriter.Close();
                stopwatch.Stop();
            }
        }
        
        private SKBitmap DrawMessage(SKBitmap sectionImage, List<SKBitmap> imageList, RenderOptions renderOptions, List<GifEmote> currentGifEmotes, SKPaint messageFont, Dictionary<string, SKBitmap> emojiCache, Dictionary<string, SKBitmap> chatEmotes, List<ThirdPartyEmote> thirdPartyEmotes, Comment comment, Size canvasSize, ref Point drawPos, string emojiRegex, ref int default_x)
        {
            bool hasEmote = false;
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
                            lock (currentEmote.emote_drawing)
                            {
                                if (drawPos.X + (currentEmote.emote_drawing.Width / currentEmote.imageScale) * renderOptions.image_scale > canvasSize.Width)
                                    sectionImage = AddImageSection(sectionImage, imageList, ref hasEmote, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

                                if (currentEmote.imageType == "gif")
                                {
                                    GifEmote emote = new GifEmote(new Point(drawPos.X, drawPos.Y), currentEmote.name, currentEmote.emote_drawing, currentEmote.imageScale);
                                    currentGifEmotes.Add(emote);
                                    hasEmote = true;
                                    drawPos.X += (currentEmote.emote_drawing.Width / currentEmote.imageScale) * renderOptions.image_scale + (3 * renderOptions.image_scale);
                                }
                                else
                                {
                                    hasEmote = true;
                                    using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                                    {
                                        float imageRatio = (float)renderOptions.image_scale / currentEmote.imageScale;
                                        float imageWidth = currentEmote.emote.Width * imageRatio;
                                        float imageHeight = currentEmote.emote.Height * imageRatio;
                                        float left = (float)drawPos.X;
                                        float right = imageWidth + left;
                                        float top = (float)((sectionImage.Height - imageHeight) / 2);
                                        float bottom = imageHeight + top;
                                        sectionImageCanvas.DrawBitmap(currentEmote.emote, new SKRect(left, top, right, bottom), imagePaint);
                                        drawPos.X += (int)Math.Ceiling(imageWidth + (3 * renderOptions.image_scale));
                                    }
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
                                            float emojiSize = (emojiBitmap.Width / 3) * (float)renderOptions.image_scale;
                                            if (drawPos.X + (20 * renderOptions.image_scale) + 3 > canvasSize.Width)
                                                sectionImage = AddImageSection(sectionImage, imageList, ref hasEmote, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

                                            hasEmote = true;
                                            float emojiLeft = (float)Math.Floor(drawPos.X);
                                            float emojiTop = (float)Math.Floor((renderOptions.has_emote_height - emojiSize) / 2.0);
                                            SKRect emojiRect = new SKRect(emojiLeft, emojiTop, emojiLeft+ emojiSize, emojiTop+ emojiSize);
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
                                    sectionImage = AddImageSection(sectionImage, imageList, ref hasEmote, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

                                for (int j = 0; j < charList.Count; j++)
                                {
                                    if (messageFont.Typeface.CountGlyphs(charList[j].ToString()) == 0)
                                    {
                                        if (messageBuffer != "")
                                            sectionImage = DrawText(sectionImage, messageBuffer, messageFont, imageList, ref hasEmote, renderOptions, currentGifEmotes, canvasSize, ref drawPos, true, default_x);
                                        SKPaint fallbackFont = GetFallbackFont(charList[j], renderOptions);
                                        fallbackFont.Color = SKColors.White;
                                        sectionImage = DrawText(sectionImage, charList[j].ToString(), fallbackFont, imageList, ref hasEmote, renderOptions, currentGifEmotes, canvasSize, ref drawPos, false, default_x);
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
                                sectionImage = DrawText(sectionImage, output, messageFont, imageList, ref hasEmote, renderOptions, currentGifEmotes, canvasSize, ref drawPos, true, default_x);
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
                        float imageWidth = (float)(emoteImage.Width * renderOptions.image_scale);
                        if (drawPos.X + imageWidth > canvasSize.Width)
                            sectionImage = AddImageSection(sectionImage, imageList, ref hasEmote, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);
                        hasEmote = true;
                        using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                        {
                            
                            float imageHeight = (float)(emoteImage.Height * renderOptions.image_scale);
                            float imageLeft = (float)Math.Floor(drawPos.X);
                            float imageTop = (float)((renderOptions.has_emote_height - imageHeight) / 2.0);
                            SKRect emoteRect = new SKRect(imageLeft, imageTop, imageLeft + imageWidth, imageTop + imageHeight);
                            sectionImageCanvas.DrawBitmap(emoteImage, emoteRect, imagePaint);
                        }
                        drawPos.X += emoteImage.Width;
                    }
                    else
                    {
                        //Probably an old emote that was removed
                        sectionImage = DrawText(sectionImage, fragment.text, messageFont, imageList, ref hasEmote, renderOptions, currentGifEmotes, canvasSize, ref drawPos, true, default_x);
                    }
                }
            }

            sectionImage = CropImage(sectionImage, renderOptions, hasEmote);
            imageList.Add(sectionImage);
            
            if (!renderOptions.compact_messages && !lastHasEmoteAtEnd && imageList.First().Height == renderOptions.has_no_emote_eight)
            {
                SKBitmap paddingImage = new SKBitmap((int)canvasSize.Width, (int)Math.Floor((renderOptions.has_emote_height - renderOptions.has_no_emote_eight) / 2.0));
                using (SKCanvas paddingImageCanvas = new SKCanvas(paddingImage))
                    paddingImageCanvas.Clear(renderOptions.background_color);
                imageList.Insert(0, paddingImage);
            }
            lastHasEmoteAtEnd = hasEmote;
            return sectionImage;
        }

        private SKBitmap DrawText(SKBitmap sectionImage, string message, SKPaint messageFont, List<SKBitmap> imageList, ref bool hasEmote, RenderOptions renderOptions, List<GifEmote> currentGifEmotes, Size canvasSize, ref Point drawPos, bool padding, int default_x)
        {
            float textWidth;
            try
            {
                textWidth = messageFont.MeasureText(message);
            }
            catch { return sectionImage; }
            if (drawPos.X + textWidth + 3 > canvasSize.Width)
                sectionImage = AddImageSection(sectionImage, imageList, ref hasEmote, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

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

        private SKBitmap AddImageSection(SKBitmap sectionImage, List<SKBitmap> imageList, ref bool hasEmote, RenderOptions renderOptions, List<GifEmote> currentGifEmotes, Size canvasSize, ref Point drawPos, int default_x)
        {
            sectionImage = CropImage(sectionImage, renderOptions, hasEmote);
            imageList.Add(sectionImage);
            SKBitmap newImage = new SKBitmap((int)canvasSize.Width, (int)canvasSize.Height);
            using (SKCanvas paddingImageCanvas = new SKCanvas(newImage))
                paddingImageCanvas.Clear(renderOptions.background_color);
            hasEmote = false;
            drawPos.X = default_x;
            drawPos.Y += sectionImage.Height;
            return newImage;
        }

        private SKBitmap CropImage(SKBitmap sectionImage, RenderOptions renderOptions, bool hasEmote)
        {
            if (!hasEmote)
            {
                SKBitmap newBitmap = new SKBitmap(sectionImage.Width, renderOptions.has_no_emote_eight);

                using (SKCanvas sectionImageCanvas = new SKCanvas(newBitmap))
                {
                    float sourceTop = (float)((renderOptions.has_emote_height - renderOptions.has_no_emote_eight) / 2);
                    SKRect sourceRect = new SKRect(0, sourceTop, sectionImage.Width, sourceTop + renderOptions.has_no_emote_eight);
                    SKRect destRect = new SKRect(0, 0, newBitmap.Width, newBitmap.Height);
                    sectionImageCanvas.DrawBitmap(sectionImage, sourceRect, destRect);
                }
                sectionImage.Dispose();
                return newBitmap;
            }
            return sectionImage;
        }
        
        private SKBitmap DrawTimestamp(SKBitmap sectionImage, List<SKBitmap> imageList, SKPaint messageFont, RenderOptions renderOptions, Comment comment, Size canvasSize, ref Point drawPos, ref int default_x)
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

        private SKBitmap DrawUsername(SKBitmap sectionImage, List<SKBitmap> imageList, RenderOptions renderOptions, SKPaint nameFont, string userName, SKColor userColor, Size canvasSize, ref Point drawPos)
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
        
        private SKBitmap DrawBadges(SKBitmap sectionImage, List<SKBitmap> imageList, RenderOptions renderOptions, List<ChatBadge> chatBadges, Comment comment, Size canvasSize, ref Point drawPos)
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

        private void GetThirdPartyEmotes(List<ThirdPartyEmote> thirdPartyEmotes, Streamer streamerInfo, RenderOptions renderOptions)
        {
            using (WebClient client = new WebClient())
            {
                if (renderOptions.bttv_emotes)
                {
                    //Global BTTV emotes
                    JObject BBTV = JObject.Parse(client.DownloadString("https://api.betterttv.net/2/emotes"));
                    foreach (var emote in BBTV["emotes"])
                    {
                        string id = emote["id"].ToString();
                        byte[] bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/emote/{0}/2x", id));
                        MemoryStream ms = new MemoryStream(bytes);
                        MemoryStream ms2 = new MemoryStream(bytes);
                        SKBitmap temp_emote = SKBitmap.Decode(ms2);
                        System.Drawing.Image temp_emote_drawing = System.Drawing.Image.FromStream(ms);
                        thirdPartyEmotes.Add(new ThirdPartyEmote(temp_emote, temp_emote_drawing, emote["code"].ToString(), emote["imageType"].ToString(), id, 2));
                    }

                    //Channel specific BTTV emotes
                    try
                    {
                        JObject BBTV_channel = JObject.Parse(client.DownloadString("https://api.betterttv.net/2/channels/" + streamerInfo.name));
                        foreach (var emote in BBTV_channel["emotes"])
                        {
                            string id = emote["id"].ToString();
                            byte[] bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/emote/{0}/2x", id));
                            MemoryStream ms = new MemoryStream(bytes);
                            MemoryStream ms2 = new MemoryStream(bytes);
                            SKBitmap temp_emote = SKBitmap.Decode(ms2);
                            System.Drawing.Image temp_emote_drawing = System.Drawing.Image.FromStream(ms);
                            thirdPartyEmotes.Add(new ThirdPartyEmote(temp_emote, temp_emote_drawing, emote["code"].ToString(), emote["imageType"].ToString(), id, 2));
                        }
                    }
                    catch { }
                }

                if (renderOptions.ffz_emotes)
                {
                    //Global FFZ emotes
                    JObject FFZ = JObject.Parse(client.DownloadString("https://api.betterttv.net/2/frankerfacez_emotes/global"));
                    foreach (var emote in FFZ["emotes"])
                    {
                        string id = emote["id"].ToString();
                        byte[] bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/1", id));
                        MemoryStream ms = new MemoryStream(bytes);
                        MemoryStream ms2 = new MemoryStream(bytes);
                        SKBitmap temp_emote = SKBitmap.Decode(ms2);
                        System.Drawing.Image temp_emote_drawing = System.Drawing.Image.FromStream(ms);
                        thirdPartyEmotes.Add(new ThirdPartyEmote(temp_emote, temp_emote_drawing, emote["code"].ToString(), emote["imageType"].ToString(), id, 1));
                    }

                    //Channel specific FFZ emotes
                    try
                    {
                        JObject FFZ_channel = JObject.Parse(client.DownloadString("https://api.betterttv.net/2/frankerfacez_emotes/channels/" + streamerInfo.id));
                        foreach (var emote in FFZ_channel["emotes"])
                        {
                            string id = emote["id"].ToString();
                            byte[] bytes;
                            int scale = 2;
                            try
                            {
                                bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/2", id));
                            }
                            catch
                            {
                                bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/1", id));
                                scale = 1;
                            }
                            MemoryStream ms = new MemoryStream(bytes);
                            MemoryStream ms2 = new MemoryStream(bytes);
                            SKBitmap temp_emote = SKBitmap.Decode(ms2);
                            System.Drawing.Image temp_emote_drawing = System.Drawing.Image.FromStream(ms);
                            thirdPartyEmotes.Add(new ThirdPartyEmote(temp_emote, temp_emote_drawing, emote["code"].ToString(), emote["imageType"].ToString(), id, scale));
                        }
                    }
                    catch { }
                }
            }
        }

        private void GetTwitterEmojis(Dictionary<string, SKBitmap> emojiCache, List<Comment> comments, RenderOptions renderOptions, string emojiRegex)
        {
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
                                        Console.WriteLine("U+{0:X4}", codepoint);
                                        if (codepoint != "" && !emojiCache.ContainsKey(codepoint))
                                        {
                                            try
                                            {
                                                byte[] bytes = client.DownloadData(String.Format("https://abs.twimg.com/emoji/v2/72x72/{0}.png", codepoint));
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

        private void GetEmotes(Dictionary<string, SKBitmap> chatEmotes, List<Comment> comments, RenderOptions renderOptions)
        {
            List<string> alreadyAdded = new List<string>();
            List<string> failedEmotes = new List<string>();
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
                                    byte[] bytes = client.DownloadData(String.Format("https://static-cdn.jtvnw.net/emoticons/v1/{0}/1.0", id));
                                    alreadyAdded.Add(id);
                                    MemoryStream ms = new MemoryStream(bytes);
                                    SKBitmap emoteImage = SKBitmap.Decode(ms);
                                    chatEmotes.Add(id, emoteImage);
                                }
                                catch
                                {
                                    string emoteName = fragment.text;
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
                                        alreadyAdded.Add(id);
                                        MemoryStream ms = new MemoryStream(bytes);
                                        SKBitmap emoteImage = SKBitmap.Decode(ms);
                                        chatEmotes.Add(id, emoteImage);
                                    }
                                    catch
                                    {
                                        AppendLog("Unable to fetch emote " + emoteName);
                                        failedEmotes.Add(id);
                                    }
                                }
                            }
                        }
                    }
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
                checkCompact.IsChecked = Settings.Default.CompactMessages;
                colorBackground.SelectedColor = System.Windows.Media.Color.FromRgb((byte)Settings.Default.BackgroundColorR, (byte)Settings.Default.BackgroundColorG, (byte)Settings.Default.BackgroundColorB);
                checkFFZ.IsChecked = Settings.Default.FFZEmotes;
                checkBTTV.IsChecked = Settings.Default.BTTVEmotes;
                textHeight.Text = Settings.Default.Height.ToString();
                textWidth.Text = Settings.Default.Width.ToString();
                textFontSize.Text = Settings.Default.FontSize.ToString();
                textUpdateTime.Text = Settings.Default.UpdateTime.ToString();
            }
            catch { }
        }
        private void SaveSettings()
        {
            Settings.Default.Font = comboFont.SelectedItem.ToString();
            Settings.Default.Outline = (bool)checkOutline.IsChecked;
            Settings.Default.Timestamp = (bool)checkTimestamp.IsChecked;
            Settings.Default.CompactMessages = (bool)checkCompact.IsChecked;
            Settings.Default.BackgroundColorR = colorBackground.SelectedColor.Value.R;
            Settings.Default.BackgroundColorG = colorBackground.SelectedColor.Value.G;
            Settings.Default.BackgroundColorB = colorBackground.SelectedColor.Value.B;
            Settings.Default.FFZEmotes = (bool)checkFFZ.IsChecked;
            Settings.Default.BTTVEmotes = (bool)checkBTTV.IsChecked;
            try
            {
                Settings.Default.Height = Int32.Parse(textHeight.Text);
                Settings.Default.Width = Int32.Parse(textWidth.Text);
                Settings.Default.FontSize = float.Parse(textFontSize.Text);
                Settings.Default.UpdateTime = float.Parse(textUpdateTime.Text);
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
            }
            catch (Exception ex)
            {
                AppendLog("ERROR: " + ex.Message);
                return false;
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

            LoadSettings();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }
    }
}

public class Progress
{
    public string message = "";
    public int time_passed = 0;
    public double percent_double = 0.0;
    public bool justMessage = false;
    public Progress(string Message, int Time_passed, double Percent_double)
    {
        message = Message;
        time_passed = Time_passed;
        percent_double = Percent_double;
    }

    public Progress(string Message)
    {
        message = Message;
        justMessage = true;
    }
}

public class ThirdPartyEmote
{
    public SKBitmap emote;
    public System.Drawing.Image emote_drawing;
    public string imageType;
    public string name;
    public string id;
    public int width;
    public int height;
    public int imageScale;

    public ThirdPartyEmote(SKBitmap Emote, System.Drawing.Image Emote_drawing, string Name, string ImageType, string Id, int ImageScale)
    {
        emote = Emote;
        emote_drawing = Emote_drawing;
        name = Name;
        imageType = ImageType;
        id = Id;
        width = emote.Width;
        height = emote.Height;
        imageScale = ImageScale;
    }
}

public class RenderOptions
{
    public string json_path { get; set; }
    public string save_path { get; set; }
    public SKColor background_color { get; set; }
    public int chat_height { get; set; }
    public int chat_width { get; set; }
    public bool bttv_emotes { get; set; }
    public bool ffz_emotes { get; set; }
    public bool outline { get; set; }
    public string font { get; set; }
    public double font_size { get; set; }
    public double image_scale { get; set; }
    public int update_frame { get; set; }
    public int has_emote_height { get; set; }
    public int has_no_emote_eight { get; set; }
    public bool compact_messages { get; set; }
    public System.Drawing.Color background_color_drawing { get; set; }
    public int outline_size { get; set; }
    public bool chat_timestamp { get; set; }
    public int default_x { get; set; }

    public RenderOptions(string Json_path, string Save_path, SKColor Background_color, int Chat_height, int Chat_width, bool Bttv_emotes, bool Ffz_emotes, bool Outline, string Font, double Font_size, double Update_rate, bool Compact_messages, System.Drawing.Color Background_color_drawing, bool Chat_timestamp)
    {
        json_path = Json_path;
        save_path = Save_path;
        background_color = Background_color;
        chat_height = Chat_height;
        chat_width = Chat_width;
        bttv_emotes = Bttv_emotes;
        ffz_emotes = Ffz_emotes;
        outline = Outline;
        font = Font;
        font_size = Font_size;
        image_scale = font_size / 12;

        if (Update_rate == 0)
            update_frame = 1;
        else
            update_frame = (int)Math.Floor(Update_rate / (1.0 / 60.0));

        has_emote_height = (int)Math.Floor(36 * image_scale);
        has_no_emote_eight = (int)Math.Floor(20 * image_scale);
        compact_messages = Compact_messages;
        background_color_drawing = Background_color_drawing;
        outline_size = (int)Math.Round(3 * image_scale);
        chat_timestamp = Chat_timestamp;
        default_x = 2;
    }
}

public class ChatBadge
{
    public string name;
    public Dictionary<string, SKBitmap> versions;

    public ChatBadge(string Name, Dictionary<string, SKBitmap> Versions)
    {
        name = Name;
        versions = Versions;
    }
}

public class GifEmote
{
    public Point offset;
    public string name;
    public System.Drawing.Image image;
    public FrameDimension dim;
    public int frames;
    public List<int> durations;
    public int total_duration;
    public int imageScale;

    public GifEmote(Point Offset, string Name, System.Drawing.Image Image, int ImageScale)
    {
        offset = Offset;
        name = Name;
        image = Image;
        dim = new FrameDimension(image.FrameDimensionsList[0]);
        frames = image.GetFrameCount(dim);
        var times = image.GetPropertyItem(0x5100).Value;

        durations = new List<int>();
        for (int i = 0; i < frames; i++)
        {
            var duration = BitConverter.ToInt32(times, 4 * i);
            durations.Add(duration);
            total_duration += duration;
        }

        if (total_duration == 0)
        {
            for (int i = 0; i < durations.Count; i++)
            {
                durations.RemoveAt(i);
                durations.Insert(i, 10);
            }
            total_duration = durations.Count * 10;
        }

        imageScale = ImageScale;
    }
}

public class TwitchComment
{
    public string section;
    public double secondsOffset;
    public List<GifEmote> gifEmotes;

    public TwitchComment(string Section, double SecondsOffset, List<GifEmote> GifEmotes)
    {
        section = Section;
        secondsOffset = SecondsOffset;
        gifEmotes = GifEmotes;
    }
}