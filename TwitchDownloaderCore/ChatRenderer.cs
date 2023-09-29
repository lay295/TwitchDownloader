﻿using NeoSmart.Unicode;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public sealed class ChatRenderer : IDisposable
    {
        public bool Disposed { get; private set; } = false;
        public ChatRoot chatRoot { get; private set; } = new ChatRoot();

        private const string PURPLE = "#7B2CF2";
        private static readonly SKColor Purple = SKColor.Parse(PURPLE);
        private static readonly string[] DefaultUsernameColors = { "#FF0000", "#0000FF", "#00FF00", "#B22222", "#FF7F50", "#9ACD32", "#FF4500", "#2E8B57", "#DAA520", "#D2691E", "#5F9EA0", "#1E90FF", "#FF69B4", "#8A2BE2", "#00FF7F" };

        private static readonly Regex RtlRegex = new("[\u0591-\u07FF\uFB1D-\uFDFD\uFE70-\uFEFC]", RegexOptions.Compiled);
        private static readonly Regex BlockArtRegex = new("[\u2500-\u257F\u2580-\u259F\u2800-\u28FF]", RegexOptions.Compiled);
        private static readonly Regex EmojiRegex = new(@"(?:[#*0-9]\uFE0F?\u20E3|©\uFE0F?|[®\u203C\u2049\u2122\u2139\u2194-\u2199\u21A9\u21AA\u231A\u231B\u2328\u23CF\u23ED-\u23EF\u23F1\u23F2\u23F8-\u23FA\u24C2\u25AA\u25AB\u25B6\u25C0\u25FB\u25FC\u25FE\u2600-\u2604\u260E\u2611\u2614\u2615\u2618\u2620\u2622\u2623\u2626\u262A\u262E\u262F\u2638-\u263A\u2640\u2642\u2648-\u2653\u265F\u2660\u2663\u2665\u2666\u2668\u267B\u267E\u267F\u2692\u2694-\u2697\u2699\u269B\u269C\u26A0\u26A7\u26AA\u26B0\u26B1\u26BD\u26BE\u26C4\u26C8\u26CF\u26D1\u26D3\u26E9\u26F0-\u26F5\u26F7\u26F8\u26FA\u2702\u2708\u2709\u270F\u2712\u2714\u2716\u271D\u2721\u2733\u2734\u2744\u2747\u2757\u2763\u27A1\u2934\u2935\u2B05-\u2B07\u2B1B\u2B1C\u2B55\u3030\u303D\u3297\u3299]\uFE0F?|[\u261D\u270C\u270D](?:\uFE0F|\uD83C[\uDFFB-\uDFFF])?|[\u270A\u270B](?:\uD83C[\uDFFB-\uDFFF])?|[\u23E9-\u23EC\u23F0\u23F3\u25FD\u2693\u26A1\u26AB\u26C5\u26CE\u26D4\u26EA\u26FD\u2705\u2728\u274C\u274E\u2753-\u2755\u2795-\u2797\u27B0\u27BF\u2B50]|\u26F9(?:\uFE0F|\uD83C[\uDFFB-\uDFFF])?(?:\u200D[\u2640\u2642]\uFE0F?)?|\u2764\uFE0F?(?:\u200D(?:\uD83D\uDD25|\uD83E\uDE79))?|\uD83C(?:[\uDC04\uDD70\uDD71\uDD7E\uDD7F\uDE02\uDE37\uDF21\uDF24-\uDF2C\uDF36\uDF7D\uDF96\uDF97\uDF99-\uDF9B\uDF9E\uDF9F\uDFCD\uDFCE\uDFD4-\uDFDF\uDFF5\uDFF7]\uFE0F?|[\uDF85\uDFC2\uDFC7](?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC3\uDFC4\uDFCA](?:\uD83C[\uDFFB-\uDFFF])?(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDFCB\uDFCC](?:\uFE0F|\uD83C[\uDFFB-\uDFFF])?(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDCCF\uDD8E\uDD91-\uDD9A\uDE01\uDE1A\uDE2F\uDE32-\uDE36\uDE38-\uDE3A\uDE50\uDE51\uDF00-\uDF20\uDF2D-\uDF35\uDF37-\uDF7C\uDF7E-\uDF84\uDF86-\uDF93\uDFA0-\uDFC1\uDFC5\uDFC6\uDFC8\uDFC9\uDFCF-\uDFD3\uDFE0-\uDFF0\uDFF8-\uDFFF]|\uDDE6\uD83C[\uDDE8-\uDDEC\uDDEE\uDDF1\uDDF2\uDDF4\uDDF6-\uDDFA\uDDFC\uDDFD\uDDFF]|\uDDE7\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEF\uDDF1-\uDDF4\uDDF6-\uDDF9\uDDFB\uDDFC\uDDFE\uDDFF]|\uDDE8\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDEE\uDDF0-\uDDF5\uDDF7\uDDFA-\uDDFF]|\uDDE9\uD83C[\uDDEA\uDDEC\uDDEF\uDDF0\uDDF2\uDDF4\uDDFF]|\uDDEA\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDED\uDDF7-\uDDFA]|\uDDEB\uD83C[\uDDEE-\uDDF0\uDDF2\uDDF4\uDDF7]|\uDDEC\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEE\uDDF1-\uDDF3\uDDF5-\uDDFA\uDDFC\uDDFE]|\uDDED\uD83C[\uDDF0\uDDF2\uDDF3\uDDF7\uDDF9\uDDFA]|\uDDEE\uD83C[\uDDE8-\uDDEA\uDDF1-\uDDF4\uDDF6-\uDDF9]|\uDDEF\uD83C[\uDDEA\uDDF2\uDDF4\uDDF5]|\uDDF0\uD83C[\uDDEA\uDDEC-\uDDEE\uDDF2\uDDF3\uDDF5\uDDF7\uDDFC\uDDFE\uDDFF]|\uDDF1\uD83C[\uDDE6-\uDDE8\uDDEE\uDDF0\uDDF7-\uDDFB\uDDFE]|\uDDF2\uD83C[\uDDE6\uDDE8-\uDDED\uDDF0-\uDDFF]|\uDDF3\uD83C[\uDDE6\uDDE8\uDDEA-\uDDEC\uDDEE\uDDF1\uDDF4\uDDF5\uDDF7\uDDFA\uDDFF]|\uDDF4\uD83C\uDDF2|\uDDF5\uD83C[\uDDE6\uDDEA-\uDDED\uDDF0-\uDDF3\uDDF7-\uDDF9\uDDFC\uDDFE]|\uDDF6\uD83C\uDDE6|\uDDF7\uD83C[\uDDEA\uDDF4\uDDF8\uDDFA\uDDFC]|\uDDF8\uD83C[\uDDE6-\uDDEA\uDDEC-\uDDF4\uDDF7-\uDDF9\uDDFB\uDDFD-\uDDFF]|\uDDF9\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDED\uDDEF-\uDDF4\uDDF7\uDDF9\uDDFB\uDDFC\uDDFF]|\uDDFA\uD83C[\uDDE6\uDDEC\uDDF2\uDDF3\uDDF8\uDDFE\uDDFF]|\uDDFB\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDEE\uDDF3\uDDFA]|\uDDFC\uD83C[\uDDEB\uDDF8]|\uDDFD\uD83C\uDDF0|\uDDFE\uD83C[\uDDEA\uDDF9]|\uDDFF\uD83C[\uDDE6\uDDF2\uDDFC]|\uDFF3\uFE0F?(?:\u200D(?:\u26A7\uFE0F?|\uD83C\uDF08))?|\uDFF4(?:\u200D\u2620\uFE0F?|\uDB40\uDC67\uDB40\uDC62\uDB40(?:\uDC65\uDB40\uDC6E\uDB40\uDC67|\uDC73\uDB40\uDC63\uDB40\uDC74|\uDC77\uDB40\uDC6C\uDB40\uDC73)\uDB40\uDC7F)?)|\uD83D(?:[\uDC3F\uDCFD\uDD49\uDD4A\uDD6F\uDD70\uDD73\uDD76-\uDD79\uDD87\uDD8A-\uDD8D\uDDA5\uDDA8\uDDB1\uDDB2\uDDBC\uDDC2-\uDDC4\uDDD1-\uDDD3\uDDDC-\uDDDE\uDDE1\uDDE3\uDDE8\uDDEF\uDDF3\uDDFA\uDECB\uDECD-\uDECF\uDEE0-\uDEE5\uDEE9\uDEF0\uDEF3]\uFE0F?|[\uDC42\uDC43\uDC46-\uDC50\uDC66\uDC67\uDC6B-\uDC6D\uDC72\uDC74-\uDC76\uDC78\uDC7C\uDC83\uDC85\uDC8F\uDC91\uDCAA\uDD7A\uDD95\uDD96\uDE4C\uDE4F\uDEC0\uDECC](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC6E\uDC70\uDC71\uDC73\uDC77\uDC81\uDC82\uDC86\uDC87\uDE45-\uDE47\uDE4B\uDE4D\uDE4E\uDEA3\uDEB4-\uDEB6](?:\uD83C[\uDFFB-\uDFFF])?(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDD74\uDD90](?:\uFE0F|\uD83C[\uDFFB-\uDFFF])?|[\uDC00-\uDC07\uDC09-\uDC14\uDC16-\uDC3A\uDC3C-\uDC3E\uDC40\uDC44\uDC45\uDC51-\uDC65\uDC6A\uDC79-\uDC7B\uDC7D-\uDC80\uDC84\uDC88-\uDC8E\uDC90\uDC92-\uDCA9\uDCAB-\uDCFC\uDCFF-\uDD3D\uDD4B-\uDD4E\uDD50-\uDD67\uDDA4\uDDFB-\uDE2D\uDE2F-\uDE34\uDE37-\uDE44\uDE48-\uDE4A\uDE80-\uDEA2\uDEA4-\uDEB3\uDEB7-\uDEBF\uDEC1-\uDEC5\uDED0-\uDED2\uDED5-\uDED7\uDEDD-\uDEDF\uDEEB\uDEEC\uDEF4-\uDEFC\uDFE0-\uDFEB\uDFF0]|\uDC08(?:\u200D\u2B1B)?|\uDC15(?:\u200D\uD83E\uDDBA)?|\uDC3B(?:\u200D\u2744\uFE0F?)?|\uDC41\uFE0F?(?:\u200D\uD83D\uDDE8\uFE0F?)?|\uDC68(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:[\uDC68\uDC69]\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFC-\uDFFF])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFD-\uDFFF])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFD\uDFFF])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFE])))?))?|\uDC69(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?[\uDC68\uDC69]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|\uDC69\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?))|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]|\uDC8B\u200D\uD83D[\uDC68\uDC69])\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFC-\uDFFF])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]|\uDC8B\u200D\uD83D[\uDC68\uDC69])\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB\uDFFD-\uDFFF])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]|\uDC8B\u200D\uD83D[\uDC68\uDC69])\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]|\uDC8B\u200D\uD83D[\uDC68\uDC69])\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFD\uDFFF])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]|\uDC8B\u200D\uD83D[\uDC68\uDC69])\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFE])))?))?|\uDC6F(?:\u200D[\u2640\u2642]\uFE0F?)?|\uDD75(?:\uFE0F|\uD83C[\uDFFB-\uDFFF])?(?:\u200D[\u2640\u2642]\uFE0F?)?|\uDE2E(?:\u200D\uD83D\uDCA8)?|\uDE35(?:\u200D\uD83D\uDCAB)?|\uDE36(?:\u200D\uD83C\uDF2B\uFE0F?)?)|\uD83E(?:[\uDD0C\uDD0F\uDD18-\uDD1F\uDD30-\uDD34\uDD36\uDD77\uDDB5\uDDB6\uDDBB\uDDD2\uDDD3\uDDD5\uDEC3-\uDEC5\uDEF0\uDEF2-\uDEF6](?:\uD83C[\uDFFB-\uDFFF])?|[\uDD26\uDD35\uDD37-\uDD39\uDD3D\uDD3E\uDDB8\uDDB9\uDDCD-\uDDCF\uDDD4\uDDD6-\uDDDD](?:\uD83C[\uDFFB-\uDFFF])?(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDDDE\uDDDF](?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDD0D\uDD0E\uDD10-\uDD17\uDD20-\uDD25\uDD27-\uDD2F\uDD3A\uDD3F-\uDD45\uDD47-\uDD76\uDD78-\uDDB4\uDDB7\uDDBA\uDDBC-\uDDCC\uDDD0\uDDE0-\uDDFF\uDE70-\uDE74\uDE78-\uDE7C\uDE80-\uDE86\uDE90-\uDEAC\uDEB0-\uDEBA\uDEC0-\uDEC2\uDED0-\uDED9\uDEE0-\uDEE7]|\uDD3C(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF])?|\uDDD1(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83E\uDDD1))|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFC-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB\uDFFD-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB-\uDFFD\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB-\uDFFE]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])))?))?|\uDEF1(?:\uD83C(?:\uDFFB(?:\u200D\uD83E\uDEF2\uD83C[\uDFFC-\uDFFF])?|\uDFFC(?:\u200D\uD83E\uDEF2\uD83C[\uDFFB\uDFFD-\uDFFF])?|\uDFFD(?:\u200D\uD83E\uDEF2\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF])?|\uDFFE(?:\u200D\uD83E\uDEF2\uD83C[\uDFFB-\uDFFD\uDFFF])?|\uDFFF(?:\u200D\uD83E\uDEF2\uD83C[\uDFFB-\uDFFE])?))?))",
            RegexOptions.Compiled);

        // TODO: Use FrozenDictionary when .NET 8
        private static readonly IReadOnlyDictionary<int, string> AllEmojiSequences = Emoji.All.ToDictionary(e => e.SortOrder, e => e.Sequence.AsString);

        private readonly IProgress<ProgressReport> _progress;
        private readonly ChatRenderOptions renderOptions;
        private List<ChatBadge> badgeList = new List<ChatBadge>();
        private List<TwitchEmote> emoteList = new List<TwitchEmote>();
        private List<TwitchEmote> emoteThirdList = new List<TwitchEmote>();
        private List<CheerEmote> cheermotesList = new List<CheerEmote>();
        private Dictionary<string, SKBitmap> emojiCache = new Dictionary<string, SKBitmap>();
        private Dictionary<int, SKPaint> fallbackFontCache = new Dictionary<int, SKPaint>();
        private bool noFallbackFontFound = false;
        private readonly SKFontManager fontManager = SKFontManager.CreateDefault();
        private SKPaint messageFont;
        private SKPaint nameFont;
        private SKPaint outlinePaint;
        private readonly HighlightIcons highlightIcons;

        public ChatRenderer(ChatRenderOptions chatRenderOptions, IProgress<ProgressReport> progress = null)
        {
            renderOptions = chatRenderOptions;
            renderOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(renderOptions.TempFolder) ? Path.GetTempPath() : renderOptions.TempFolder,
                "TwitchDownloader");
            renderOptions.BlockArtPreWrapWidth = 29.166 * renderOptions.FontSize - renderOptions.SidePadding * 2;
            renderOptions.BlockArtPreWrap = renderOptions.ChatWidth > renderOptions.BlockArtPreWrapWidth;
            _progress = progress;
            highlightIcons = new HighlightIcons(renderOptions.TempFolder, Purple, renderOptions.Offline);
        }

        public async Task RenderVideoAsync(CancellationToken cancellationToken)
        {
            _progress.Report(new ProgressReport(ReportType.SameLineStatus, "Fetching Images [1/2]"));
            await Task.Run(() => FetchScaledImages(cancellationToken), cancellationToken);

            if (renderOptions.DisperseCommentOffsets)
            {
                DisperseCommentOffsets(chatRoot.comments);
            }
            FloorCommentOffsets(chatRoot.comments);

            outlinePaint = new SKPaint() { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(renderOptions.OutlineSize * renderOptions.ReferenceScale), StrokeJoin = SKStrokeJoin.Round, Color = SKColors.Black, IsAntialias = true, IsAutohinted = true, LcdRenderText = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            nameFont = new SKPaint() { LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, IsAutohinted = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            messageFont = new SKPaint() { LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, IsAutohinted = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High, Color = renderOptions.MessageColor };

            if (renderOptions.Font == "Inter Embedded")
            {
                nameFont.Typeface = GetInterTypeface(renderOptions.UsernameFontStyle);
                messageFont.Typeface = GetInterTypeface(renderOptions.MessageFontStyle);
            }
            else
            {
                nameFont.Typeface = SKTypeface.FromFamilyName(renderOptions.Font, renderOptions.UsernameFontStyle);
                messageFont.Typeface = SKTypeface.FromFamilyName(renderOptions.Font, renderOptions.MessageFontStyle);
            }

            // Cache the rendered timestamp widths
            renderOptions.TimestampWidths = !renderOptions.Timestamp ? Array.Empty<int>() : new[]
            {
                (int)messageFont.MeasureText("0:00"),
                (int)messageFont.MeasureText("00:00"),
                (int)messageFont.MeasureText("0:00:00"),
                (int)messageFont.MeasureText("00:00:00")
            };

            // Rough estimation of the width of a single block art character
            renderOptions.BlockArtCharWidth = GetFallbackFont('█').MeasureText("█");


            RemoveRestrictedComments(chatRoot.comments);

            (int startTick, int totalTicks) = GetVideoTicks();

            var renderFileDirectory = Directory.GetParent(Path.GetFullPath(renderOptions.OutputFile))!;
            if (!renderFileDirectory.Exists)
            {
                TwitchHelper.CreateDirectory(renderFileDirectory.FullName);
            }

            if (File.Exists(renderOptions.OutputFile))
                File.Delete(renderOptions.OutputFile);

            if (renderOptions.GenerateMask && File.Exists(renderOptions.MaskFile))
                File.Delete(renderOptions.MaskFile);

            FfmpegProcess ffmpegProcess = GetFfmpegProcess(0, false);
            FfmpegProcess maskProcess = renderOptions.GenerateMask ? GetFfmpegProcess(0, true) : null;
            _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Rendering Video: 0% [2/2]"));

            try
            {
                await Task.Run(() => RenderVideoSection(startTick, startTick + totalTicks, ffmpegProcess, maskProcess, cancellationToken), cancellationToken);
            }
            catch
            {
                ffmpegProcess.Dispose();
                maskProcess?.Dispose();
                GC.Collect();
                throw;
            }
        }

        /* Due to Twitch changing the API to return only whole number offsets, renders have become less readable.
         * To get around this we can disperse comment offsets according to their creation date milliseconds to
         * help bring back the better readability of comments coming in 1-by-1 */
        private static void DisperseCommentOffsets(List<Comment> comments)
        {
            // Enumerating over a span is faster than a list
            var commentSpan = CollectionsMarshal.AsSpan(comments);

            foreach (var c in commentSpan)
            {
                if (c.content_offset_seconds % 1 == 0 && c.created_at.Millisecond != 0)
                {
                    const int MILLIS_PER_HALF_SECOND = 500;
                    const double MILLIS_PER_SECOND = 1000.0;
                    // Finding the difference between the creation dates and offsets is inconsistent. This approximation looks better more often.
                    c.content_offset_seconds += (c.created_at.Millisecond - MILLIS_PER_HALF_SECOND) / MILLIS_PER_SECOND;
                }
            }
        }

        /* Why are we doing this? The question is when to display a 0.5 second offset comment with an update rate of 1.
         * At the update frame at 0 seconds, or 1 second? We're choosing at 0 seconds here. Flooring to either the
         * update rate, or if the update rate is greater than 1 just to the next whole number */
        private void FloorCommentOffsets(List<Comment> comments)
        {
            if (renderOptions.UpdateRate <= 0)
                return;

            foreach (var comment in comments)
            {
                if (renderOptions.UpdateRate > 1)
                {
                    comment.content_offset_seconds = Math.Floor(comment.content_offset_seconds);
                }
                else
                {
                    comment.content_offset_seconds = Math.Floor(comment.content_offset_seconds / renderOptions.UpdateRate) * renderOptions.UpdateRate;
                }
            }
        }

        private void RemoveRestrictedComments(List<Comment> comments)
        {
            if (renderOptions.IgnoreUsersArray.Length == 0 && renderOptions.BannedWordsArray.Length == 0)
            {
                return;
            }

            var bannedWordRegexes = new Regex[renderOptions.BannedWordsArray.Length];
            for (var i = 0; i < renderOptions.BannedWordsArray.Length; i++)
            {
                bannedWordRegexes[i] = new Regex(@$"(?<=^|[\s\d\p{{P}}\p{{S}}]){Regex.Escape(renderOptions.BannedWordsArray[i])}(?=$|[\s\d\p{{P}}\p{{S}}])",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            for (var i = 0; i < comments.Count; i++)
            {
                foreach (var username in renderOptions.IgnoreUsersArray)
                {
                    if (username.Equals(comments[i].commenter.name, StringComparison.OrdinalIgnoreCase))
                    {
                        comments.RemoveAt(i);
                        i--;
                        goto NextComment;
                    }
                }

                foreach (var bannedWordRegex in bannedWordRegexes)
                {
                    if (bannedWordRegex.IsMatch(comments[i].message.body))
                    {
                        comments.RemoveAt(i);
                        i--;
                        goto NextComment;
                    }
                }

                // goto is cheaper and more readable than using a boolean + branch check after each operation
                NextComment: ;
            }
        }

        private static SKTypeface GetInterTypeface(SKFontStyle fontStyle)
        {
            if (fontStyle == SKFontStyle.Bold)
            {
                using MemoryStream stream = new MemoryStream(Properties.Resources.InterBold);
                return SKTypeface.FromStream(stream);
            }
            else
            {
                using MemoryStream stream = new MemoryStream(Properties.Resources.Inter);
                return SKTypeface.FromStream(stream);
            }
        }

        private void RenderVideoSection(int startTick, int endTick, FfmpegProcess ffmpegProcess, FfmpegProcess maskProcess = null, CancellationToken cancellationToken = new())
        {
            UpdateFrame latestUpdate = null;
            BinaryWriter ffmpegStream = new BinaryWriter(ffmpegProcess.StandardInput.BaseStream);
            BinaryWriter maskStream = null;
            if (maskProcess != null)
                maskStream = new BinaryWriter(maskProcess.StandardInput.BaseStream);

            DriveInfo outputDrive = DriveHelper.GetOutputDrive(ffmpegProcess.SavePath);

            Stopwatch stopwatch = Stopwatch.StartNew();

            // Measure some sample text to determine the text height, cannot assume it is font size
            SKRect sampleTextBounds = new SKRect();
            messageFont.MeasureText("ABC123", ref sampleTextBounds);
            int sectionDefaultYPos = (int)(((renderOptions.SectionHeight - sampleTextBounds.Height) / 2.0) + sampleTextBounds.Height);

            for (int currentTick = startTick; currentTick < endTick; currentTick++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (currentTick % renderOptions.UpdateFrame == 0)
                {
                    latestUpdate = GenerateUpdateFrame(currentTick, sectionDefaultYPos, latestUpdate);
                }

                SKBitmap frame = null;
                bool isCopyFrame = false;
                try
                {
                    (frame, isCopyFrame) = GetFrameFromTick(currentTick, sectionDefaultYPos, latestUpdate);

                    if (!renderOptions.SkipDriveWaiting)
                        DriveHelper.WaitForDrive(outputDrive, _progress, cancellationToken).Wait(cancellationToken);

                    ffmpegStream.Write(frame.Bytes);

                    if (maskProcess != null)
                    {
                        if (!renderOptions.SkipDriveWaiting)
                            DriveHelper.WaitForDrive(outputDrive, _progress, cancellationToken).Wait(cancellationToken);

                        SetFrameMask(frame);
                        maskStream.Write(frame.Bytes);
                    }
                }
                finally
                {
                    if (isCopyFrame)
                    {
                        frame?.Dispose();
                    }
                }

                if (_progress != null && currentTick % 3 == 0)
                {
                    double percentDouble = (currentTick - startTick) / (double)(endTick - startTick) * 100.0;
                    int percentInt = (int)percentDouble;
                    _progress.Report(new ProgressReport(percentInt));

                    int timeLeftInt = (int)(100.0 / percentDouble * stopwatch.Elapsed.TotalSeconds) - (int)stopwatch.Elapsed.TotalSeconds;
                    TimeSpan timeLeft = new TimeSpan(0, 0, timeLeftInt);
                    TimeSpan timeElapsed = new TimeSpan(0, 0, (int)stopwatch.Elapsed.TotalSeconds);
                    _progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Rendering Video: {percentInt}% ({timeElapsed:h\\hm\\ms\\s} Elapsed | {timeLeft:h\\hm\\ms\\s} Remaining)"));
                }
            }

            stopwatch.Stop();
            _progress?.Report(new ProgressReport(100));
            _progress?.Report(new ProgressReport(ReportType.SameLineStatus, "Rendering Video: 100%"));
            _progress?.Report(new ProgressReport(ReportType.Log, $"FINISHED. RENDER TIME: {stopwatch.Elapsed.TotalSeconds:F1}s SPEED: {(endTick - startTick) / (double)renderOptions.Framerate / stopwatch.Elapsed.TotalSeconds:F2}x"));

            latestUpdate?.Image.Dispose();

            ffmpegStream.Dispose();
            maskStream?.Dispose();

            ffmpegProcess.WaitForExit(100_000);
            maskProcess?.WaitForExit(100_000);
        }

        private static void SetFrameMask(SKBitmap frame)
        {
            IntPtr pixelsAddr = frame.GetPixels();
            SKImageInfo frameInfo = frame.Info;
            int height = frameInfo.Height;
            int width = frameInfo.Width;
            unsafe
            {
                byte* ptr = (byte*)pixelsAddr.ToPointer();
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        byte alpha = *(ptr + 3); // alpha of the unmasked pixel
                        *ptr++ = alpha;
                        *ptr++ = alpha;
                        *ptr++ = alpha;
                        *ptr++ = 0xFF;
                    }
                }
            }
        }

        private FfmpegProcess GetFfmpegProcess(int partNumber, bool isMask)
        {
            string savePath;
            if (partNumber == 0)
            {
                if (isMask)
                    savePath = renderOptions.MaskFile;
                else
                    savePath = renderOptions.OutputFile;
            }
            else
            {
                savePath = Path.Combine(renderOptions.TempFolder, Path.GetRandomFileName() + (isMask ? "_mask" : "") + Path.GetExtension(renderOptions.OutputFile));
            }

            savePath = Path.GetFullPath(savePath);

            string inputArgs = new StringBuilder(renderOptions.InputArgs)
                .Replace("{fps}", renderOptions.Framerate.ToString())
                .Replace("{height}", renderOptions.ChatHeight.ToString())
                .Replace("{width}", renderOptions.ChatWidth.ToString())
                .Replace("{save_path}", savePath)
                .Replace("{max_int}", int.MaxValue.ToString())
                .Replace("{pix_fmt}", SKImageInfo.PlatformColorType == SKColorType.Bgra8888 ? "bgra" : "rgba")
                .ToString();
            string outputArgs = new StringBuilder(renderOptions.OutputArgs)
                .Replace("{fps}", renderOptions.Framerate.ToString())
                .Replace("{height}", renderOptions.ChatHeight.ToString())
                .Replace("{width}", renderOptions.ChatWidth.ToString())
                .Replace("{save_path}", savePath)
                .Replace("{max_int}", int.MaxValue.ToString())
                .ToString();

            var process = new FfmpegProcess
            {
                StartInfo =
                {
                    FileName = renderOptions.FfmpegPath,
                    Arguments = $"{inputArgs} {outputArgs}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
                SavePath = savePath
            };

            if (renderOptions.LogFfmpegOutput && _progress != null)
            {
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        _progress.Report(new ProgressReport() { ReportType = ReportType.FfmpegLog, Data = e.Data });
                    }
                };
            }

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            return process;
        }

        private (SKBitmap frame, bool isCopyFrame) GetFrameFromTick(int currentTick, int sectionDefaultYPos, UpdateFrame currentFrame = null)
        {
            currentFrame ??= GenerateUpdateFrame(currentTick, sectionDefaultYPos);
            var (frame, isCopyFrame) = DrawAnimatedEmotes(currentFrame.Image, currentFrame.Comments, currentTick);
            return (frame, isCopyFrame);
        }

        private (SKBitmap frame, bool isCopyFrame) DrawAnimatedEmotes(SKBitmap updateFrame, List<CommentSection> comments, int currentTick)
        {
            // If we are generating a mask then we need to produce a copy
            if (!renderOptions.GenerateMask)
            {
                bool hasAnimatedEmotes = false;
                foreach (var comment in comments)
                {
                    if (comment.Emotes.Count > 0)
                    {
                        hasAnimatedEmotes = true;
                        break;
                    }
                }
                if (!hasAnimatedEmotes)
                {
                    // If there are no animated emotes to draw then return the original bitmap. Copying is pretty expensive.
                    return (updateFrame, false);
                }
            }

            SKBitmap newFrame = updateFrame.Copy();
            int frameHeight = renderOptions.ChatHeight;
            long currentTickMs = (long)(currentTick / (double)renderOptions.Framerate * 1000);
            using (SKCanvas frameCanvas = new SKCanvas(newFrame))
            {
                for (int c = comments.Count - 1; c >= 0; c--)
                {
                    var comment = comments[c];
                    frameHeight -= comment.Image.Height + renderOptions.VerticalPadding;
                    foreach ((Point drawPoint, TwitchEmote emote) in comment.Emotes)
                    {
                        if (emote.FrameCount > 1)
                        {
                            int frameIndex = emote.EmoteFrameDurations.Count - 1;
                            long imageFrame = currentTickMs % (emote.TotalDuration * 10);
                            for (int i = 0; i < emote.EmoteFrameDurations.Count; i++)
                            {
                                if (imageFrame - emote.EmoteFrameDurations[i] * 10 <= 0)
                                {
                                    frameIndex = i;
                                    break;
                                }
                                imageFrame -= emote.EmoteFrameDurations[i] * 10;
                            }

                            frameCanvas.DrawBitmap(emote.EmoteFrames[frameIndex], drawPoint.X, drawPoint.Y + frameHeight);
                        }
                    }
                }
            }
            return (newFrame, true);
        }

        private UpdateFrame GenerateUpdateFrame(int currentTick, int sectionDefaultYPos, UpdateFrame lastUpdate = null)
        {
            SKBitmap newFrame = new SKBitmap(renderOptions.ChatWidth, renderOptions.ChatHeight);
            double currentTimeSeconds = currentTick / (double)renderOptions.Framerate;
            int newestCommentIndex = chatRoot.comments.FindLastIndex(x => x.content_offset_seconds <= currentTimeSeconds);

            if (newestCommentIndex == lastUpdate?.CommentIndex)
            {
                return lastUpdate;
            }
            lastUpdate?.Image.Dispose();

            List<CommentSection> commentList = lastUpdate?.Comments ?? new List<CommentSection>();

            int oldCommentIndex = -1;
            if (commentList.Count > 0)
            {
                oldCommentIndex = commentList.Last().CommentIndex;
            }
            else if (newestCommentIndex > 100)
            {
                // If we are starting partially through the comment list, we don't want to needlessly render *every* comment before our starting comment.
                // Skipping to 100 comments before our starting index should be more than enough to fill the frame with previous comments
                oldCommentIndex = newestCommentIndex - 100;
            }

            if (newestCommentIndex > oldCommentIndex)
            {
                int currentIndex = oldCommentIndex + 1;

                do
                {
                    CommentSection comment = GenerateCommentSection(currentIndex, sectionDefaultYPos);
                    if (comment != null)
                    {
                        commentList.Add(comment);
                    }
                    currentIndex++;
                }
                while (newestCommentIndex >= currentIndex);
            }

            using (SKCanvas frameCanvas = new SKCanvas(newFrame))
            {
                int commentsDrawn = 0;
                int commentListIndex = commentList.Count - 1;
                int frameHeight = renderOptions.ChatHeight;
                frameCanvas.Clear(renderOptions.BackgroundColor);

                while (commentListIndex >= 0 && frameHeight > -renderOptions.VerticalPadding)
                {
                    var comment = commentList[commentListIndex];
                    frameHeight -= comment.Image.Height + renderOptions.VerticalPadding;

                    if (renderOptions.AlternateMessageBackgrounds && comment.CommentIndex % 2 == 1)
                    {
                        frameCanvas.DrawRect(0, frameHeight - renderOptions.VerticalPadding / 2f, newFrame.Width, comment.Image.Height + renderOptions.VerticalPadding, renderOptions.AlternateBackgroundPaint);
                    }

                    frameCanvas.DrawBitmap(comment.Image, 0, frameHeight);

                    foreach (var (drawPoint, emote) in comment.Emotes)
                    {
                        //Only draw static emotes
                        if (emote.FrameCount == 1)
                        {
                            frameCanvas.DrawBitmap(emote.EmoteFrames[0], drawPoint.X, drawPoint.Y + frameHeight);
                        }
                    }
                    commentsDrawn++;
                    commentListIndex--;
                }

                int removeCount = commentList.Count - commentsDrawn;
                for (int i = 0; i < removeCount; i++)
                {
                    commentList[i].Image.Dispose();
                }
                commentList.RemoveRange(0, removeCount);
            }

            return new UpdateFrame() { Image = newFrame, Comments = commentList, CommentIndex = newestCommentIndex };
        }

        private CommentSection GenerateCommentSection(int commentIndex, int sectionDefaultYPos)
        {
            CommentSection newSection = new CommentSection();
            List<(Point, TwitchEmote)> emoteSectionList = new List<(Point, TwitchEmote)>();
            Comment comment = chatRoot.comments[commentIndex];
            List<(SKImageInfo info, SKBitmap bitmap)> sectionImages = new List<(SKImageInfo info, SKBitmap bitmap)>();
            Point drawPos = new Point();
            Point defaultPos = new Point();
            var highlightType = HighlightType.Unknown;
            defaultPos.X = renderOptions.SidePadding;

            if (comment.message.user_notice_params?.msg_id != null)
            {
                if (comment.message.user_notice_params.msg_id is not "highlighted-message" and not "sub" and not "resub" and not "subgift" and not "")
                {
                    return null;
                }
                if (comment.message.user_notice_params.msg_id == "highlighted-message" && comment.message.fragments == null && comment.message.body != null)
                {
                    comment.message.fragments = new List<Fragment> { new Fragment() };
                    comment.message.fragments[0].text = comment.message.body;
                    highlightType = HighlightType.ChannelPointHighlight;
                }
            }
            if (comment.message.fragments == null || comment.commenter == null)
            {
                return null;
            }

            AddImageSection(sectionImages, ref drawPos, defaultPos);
            defaultPos.Y = sectionDefaultYPos;
            drawPos.Y = defaultPos.Y;

            if (highlightType is HighlightType.Unknown)
            {
                highlightType = HighlightIcons.GetHighlightType(comment);
            }

            if (highlightType is not HighlightType.None)
            {
                if (highlightType is not HighlightType.ChannelPointHighlight && !renderOptions.SubMessages)
                {
                    return null;
                }

                DrawAccentedMessage(comment, sectionImages, emoteSectionList, highlightType, ref drawPos, defaultPos);
            }
            else
            {
                DrawNonAccentedMessage(comment, sectionImages, emoteSectionList, false, ref drawPos, ref defaultPos);
            }

            SKBitmap finalBitmap = CombineImages(sectionImages, highlightType);
            newSection.Image = finalBitmap;
            newSection.Emotes = emoteSectionList;
            newSection.CommentIndex = commentIndex;

            return newSection;
        }

        private SKBitmap CombineImages(List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, HighlightType highlightType)
        {
            SKBitmap finalBitmap = new SKBitmap(renderOptions.ChatWidth, sectionImages.Sum(x => x.info.Height));
            var finalBitmapInfo = finalBitmap.Info;
            using (SKCanvas finalCanvas = new SKCanvas(finalBitmap))
            {
                if (highlightType is HighlightType.PayingForward or HighlightType.ChannelPointHighlight)
                {
                    var accentColor = highlightType is HighlightType.PayingForward
                        ? new SKColor(0x26, 0x26, 0x2C, 0xFF) // #26262C (RRGGBB)
                        : new SKColor(0x80, 0x80, 0x8C, 0xFF); // #80808C (RRGGBB)

                    using var paint = new SKPaint { Color = accentColor };
                    finalCanvas.DrawRect(renderOptions.SidePadding, 0, renderOptions.AccentStrokeWidth, finalBitmapInfo.Height, paint);
                }
                else if (highlightType is not HighlightType.None)
                {
                    const int OPAQUE_THRESHOLD = 245;
                    if (!(renderOptions.BackgroundColor.Alpha < OPAQUE_THRESHOLD ||
                        (renderOptions.AlternateMessageBackgrounds && renderOptions.AlternateBackgroundColor.Alpha < OPAQUE_THRESHOLD)))
                    {
                        // Draw the highlight background only if the message background is opaque enough
                        var backgroundColor = new SKColor(0x6B, 0x6B, 0x6E, 0x1A); // #1A6B6B6E (AARRGGBB)
                        using var backgroundPaint = new SKPaint { Color = backgroundColor };
                        finalCanvas.DrawRect(renderOptions.SidePadding, 0, finalBitmapInfo.Width - renderOptions.SidePadding * 2, finalBitmapInfo.Height, backgroundPaint);
                    }

                    using var accentPaint = new SKPaint { Color = Purple };
                    finalCanvas.DrawRect(renderOptions.SidePadding, 0, renderOptions.AccentStrokeWidth, finalBitmapInfo.Height, accentPaint);
                }

                for (int i = 0; i < sectionImages.Count; i++)
                {
                    finalCanvas.DrawBitmap(sectionImages[i].bitmap, 0, i * renderOptions.SectionHeight);
                    sectionImages[i].bitmap.Dispose();
                }
            }
            sectionImages.Clear();
            finalBitmap.SetImmutable();
            return finalBitmap;
        }

        private static string GetKeyName(IEnumerable<Codepoint> codepoints)
        {
            List<string> codepointList = new List<string>();
            foreach (Codepoint codepoint in codepoints)
            {
                if (codepoint.Value != 0xFE0F)
                {
                    codepointList.Add(codepoint.Value.ToString("X"));
                }
            }

            string emojiKey = string.Join(' ', codepointList);
            return emojiKey;
        }

        private void DrawNonAccentedMessage(Comment comment, List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, List<(Point, TwitchEmote)> emotePositionList, bool highlightWords, ref Point drawPos, ref Point defaultPos)
        {
            if (renderOptions.Timestamp)
            {
                DrawTimestamp(comment, sectionImages, ref drawPos, ref defaultPos);
            }
            if (renderOptions.ChatBadges)
            {
                DrawBadges(comment, sectionImages, ref drawPos);
            }
            DrawUsername(comment, sectionImages, ref drawPos, defaultPos);
            DrawMessage(comment, sectionImages, emotePositionList, highlightWords, ref drawPos, defaultPos);

            foreach (var (_, bitmap) in sectionImages)
            {
                bitmap.SetImmutable();
            }
        }

        private void DrawAccentedMessage(Comment comment, List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, List<(Point, TwitchEmote)> emotePositionList, HighlightType highlightType, ref Point drawPos, Point defaultPos)
        {
            drawPos.X += renderOptions.AccentIndentWidth;
            defaultPos.X = drawPos.X;

            var highlightIcon = highlightIcons.GetHighlightIcon(highlightType, messageFont.Color, renderOptions.FontSize);

            Point iconPoint = new()
            {
                X = drawPos.X,
                Y = (int)((renderOptions.SectionHeight - highlightIcon?.Height) / 2.0 ?? 0)
            };

            switch (highlightType)
            {
                case HighlightType.SubscribedTier:
                case HighlightType.SubscribedPrime:
                    DrawSubscribeMessage(comment, sectionImages, emotePositionList, ref drawPos, defaultPos, highlightIcon, iconPoint);
                    break;
                case HighlightType.GiftedMany:
                case HighlightType.GiftedSingle:
                case HighlightType.GiftedAnonymous:
                    DrawGiftMessage(comment, sectionImages, emotePositionList, ref drawPos, defaultPos, highlightIcon, iconPoint);
                    break;
                case HighlightType.ChannelPointHighlight:
                    DrawNonAccentedMessage(comment, sectionImages, emotePositionList, true, ref drawPos, ref defaultPos);
                    break;
                case HighlightType.ContinuingGift:
                case HighlightType.PayingForward:
                case HighlightType.Raid:
                default:
                    DrawMessage(comment, sectionImages, emotePositionList, false, ref drawPos, defaultPos);
                    break;
            }

            foreach (var (_, bitmap) in sectionImages)
            {
                bitmap.SetImmutable();
            }
        }

        private void DrawSubscribeMessage(Comment comment, List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, SKImage highlightIcon, Point iconPoint)
        {
            using SKCanvas canvas = new(sectionImages.Last().bitmap);
            canvas.DrawImage(highlightIcon, iconPoint.X, iconPoint.Y);

            Point customMessagePos = drawPos;
            drawPos.X += highlightIcon.Width + renderOptions.WordSpacing;
            defaultPos.X = drawPos.X;

            DrawUsername(comment, sectionImages, ref drawPos, defaultPos, false, PURPLE);
            AddImageSection(sectionImages, ref drawPos, defaultPos);

            // Remove the commenter's name from the resub message
            comment.message.body = comment.message.body[(comment.commenter.display_name.Length + 1)..];
            if (comment.message.fragments[0].text.Equals(comment.commenter.display_name, StringComparison.OrdinalIgnoreCase))
            {
                // Some older chat replays separate user names into separate fragments
                comment.message.fragments.RemoveAt(0);
            }
            else
            {
                comment.message.fragments[0].text = comment.message.fragments[0].text[(comment.commenter.display_name.Length + 1)..];
            }

            var (resubMessage, customResubMessage) = HighlightIcons.SplitSubComment(comment);
            DrawMessage(resubMessage, sectionImages, emotePositionList, false, ref drawPos, defaultPos);

            // Return if there is no custom resub message to draw
            if (customResubMessage is null)
            {
                return;
            }

            AddImageSection(sectionImages, ref drawPos, defaultPos);
            drawPos = customMessagePos;
            defaultPos = customMessagePos;
            DrawNonAccentedMessage(customResubMessage, sectionImages, emotePositionList, false, ref drawPos, ref defaultPos);
        }

        private void DrawGiftMessage(Comment comment, List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, SKImage highlightIcon, Point iconPoint)
        {
            using SKCanvas canvas = new(sectionImages.Last().bitmap);

            canvas.DrawImage(highlightIcon, iconPoint.X, iconPoint.Y);
            drawPos.X += highlightIcon.Width + renderOptions.AccentIndentWidth - renderOptions.AccentStrokeWidth;
            defaultPos.X = drawPos.X;
            DrawMessage(comment, sectionImages, emotePositionList, false, ref drawPos, defaultPos);
        }

        private void DrawMessage(Comment comment, List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, List<(Point, TwitchEmote)> emotePositionList, bool highlightWords, ref Point drawPos, Point defaultPos)
        {
            int bitsCount = comment.message.bits_spent;
            foreach (var fragment in comment.message.fragments)
            {
                if (fragment.emoticon == null)
                {
                    // Either text or third party emote
                    var fragmentParts = SwapRightToLeft(fragment.text.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    foreach (var fragmentString in fragmentParts)
                    {
                        DrawFragmentPart(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, fragmentString, highlightWords);
                    }
                }
                else
                {
                    DrawFirstPartyEmote(sectionImages, emotePositionList, ref drawPos, defaultPos, fragment, highlightWords);
                }
            }
        }

        private void DrawFragmentPart(List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, int bitsCount, string fragmentPart, bool highlightWords, bool skipThird = false, bool skipEmoji = false, bool skipNonFont = false)
        {
            if (!skipThird && TryGetTwitchEmote(emoteThirdList, fragmentPart, out var emote))
            {
                DrawThirdPartyEmote(sectionImages, emotePositionList, ref drawPos, defaultPos, emote, highlightWords);
            }
            else if (!skipEmoji && EmojiRegex.IsMatch(fragmentPart))
            {
                DrawEmojiMessage(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, fragmentPart, highlightWords);
            }
            else if (!skipNonFont && (!messageFont.ContainsGlyphs(fragmentPart) || new StringInfo(fragmentPart).LengthInTextElements < fragmentPart.Length))
            {
                DrawNonFontMessage(sectionImages, ref drawPos, defaultPos, fragmentPart, highlightWords);
            }
            else
            {
                DrawRegularMessage(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, fragmentPart, highlightWords);
            }

            static bool TryGetTwitchEmote(List<TwitchEmote> twitchEmoteList, ReadOnlySpan<char> emoteName, out TwitchEmote twitchEmote)
            {
                // Enumerating over a span is faster than a list
                var emoteListSpan = CollectionsMarshal.AsSpan(twitchEmoteList);
                foreach (var emote1 in emoteListSpan)
                {
                    if (emote1.Name.AsSpan().SequenceEqual(emoteName))
                    {
                        twitchEmote = emote1;
                        return true;
                    }
                }

                twitchEmote = default;
                return false;
            }
        }

        private void DrawThirdPartyEmote(List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, TwitchEmote twitchEmote, bool highlightWords)
        {
            SKImageInfo emoteInfo = twitchEmote.Info;
            Point emotePoint = new Point();
            if (!twitchEmote.IsZeroWidth)
            {
                if (drawPos.X + emoteInfo.Width > renderOptions.ChatWidth - renderOptions.SidePadding * 2)
                {
                    AddImageSection(sectionImages, ref drawPos, defaultPos);
                }

                if (highlightWords)
                {
                    using var canvas = new SKCanvas(sectionImages.Last().bitmap);
                    using var paint = new SKPaint { Color = Purple };
                    canvas.DrawRect(drawPos.X, 0, emoteInfo.Width + renderOptions.EmoteSpacing, renderOptions.SectionHeight, paint);
                }

                emotePoint.X = drawPos.X;
                drawPos.X += emoteInfo.Width + renderOptions.EmoteSpacing;
            }
            else
            {
                emotePoint.X = drawPos.X - renderOptions.EmoteSpacing - emoteInfo.Width;
            }
            emotePoint.Y = (int)(sectionImages.Sum(x => x.info.Height) - renderOptions.SectionHeight + ((renderOptions.SectionHeight - emoteInfo.Height) / 2.0));
            emotePositionList.Add((emotePoint, twitchEmote));
        }

        private void DrawEmojiMessage(List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, int bitsCount, string fragmentString, bool highlightWords)
        {
            if (renderOptions.EmojiVendor == EmojiVendor.None)
            {
                DrawFragmentPart(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, fragmentString, highlightWords, true, true);
                return;
            }

            var enumerator = StringInfo.GetTextElementEnumerator(fragmentString);
            StringBuilder nonEmojiBuffer = new();
            while (enumerator.MoveNext())
            {
                if (enumerator.GetTextElement().Length == 1 && char.IsAscii(enumerator.GetTextElement()[0]))
                {
                    nonEmojiBuffer.Append(enumerator.GetTextElement());
                    continue;
                }

                var emojiBag = new ConcurrentBag<SingleEmoji>();
                Emoji.All.AsParallel()
                    .Where(emoji => enumerator.GetTextElement().StartsWith(AllEmojiSequences[emoji.SortOrder]))
                    .ForAll(emoji =>
                    {
                        if (emoji.Group != "Flags")
                        {
                            emojiBag.Add(emoji);
                            return;
                        }

                        if (enumerator.GetTextElement().StartsWith(AllEmojiSequences[emoji.SortOrder], StringComparison.Ordinal))
                        {
                            emojiBag.Add(emoji);
                        }
                    });

                if (emojiBag.IsEmpty)
                {
                    nonEmojiBuffer.Append(enumerator.GetTextElement());
                    continue;
                }

                // Make sure the found emojis actually exist in our cache
                var emojiMatches = emojiBag.ToList();
                int emojiMatchesCount = emojiMatches.Count;
                for (int j = 0; j < emojiMatchesCount; j++)
                {
                    if (!emojiCache.ContainsKey(GetKeyName(emojiMatches[j].Sequence.Codepoints)))
                    {
                        emojiMatches.RemoveAt(j);
                        emojiMatchesCount--;
                        j--;
                    }
                }

                if (emojiMatchesCount == 0)
                {
                    nonEmojiBuffer.Append(enumerator.GetTextElement());
                    continue;
                }

                if (nonEmojiBuffer.Length > 0)
                {
                    DrawFragmentPart(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, nonEmojiBuffer.ToString(), highlightWords, true, true);
                    nonEmojiBuffer.Clear();
                }

                SingleEmoji selectedEmoji = emojiMatches.MaxBy(x => x.SortOrder);
                SKBitmap emojiImage = emojiCache[GetKeyName(selectedEmoji.Sequence.Codepoints)];
                SKImageInfo emojiImageInfo = emojiImage.Info;

                if (drawPos.X + emojiImageInfo.Width > renderOptions.ChatWidth - renderOptions.SidePadding * 2)
                {
                    AddImageSection(sectionImages, ref drawPos, defaultPos);
                }

                Point emotePoint = new Point
                {
                    X = drawPos.X + (int)Math.Ceiling(renderOptions.EmoteSpacing / 2d), // emotePoint.X halfway through emote padding
                    Y = (int)((renderOptions.SectionHeight - emojiImageInfo.Height) / 2.0)
                };

                using (SKCanvas canvas = new SKCanvas(sectionImages.Last().bitmap))
                {
                    if (highlightWords)
                    {
                        using var paint = new SKPaint { Color = Purple };
                        canvas.DrawRect((int)(emotePoint.X - renderOptions.EmoteSpacing / 2d), 0, emojiImageInfo.Width + renderOptions.EmoteSpacing, renderOptions.SectionHeight, paint);
                    }

                    canvas.DrawBitmap(emojiImage, emotePoint.X, emotePoint.Y);
                }

                drawPos.X += emojiImageInfo.Width + renderOptions.EmoteSpacing;
            }
            if (nonEmojiBuffer.Length > 0)
            {
                DrawFragmentPart(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, nonEmojiBuffer.ToString(), highlightWords, true, true);
                nonEmojiBuffer.Clear();
            }
        }

        private void DrawNonFontMessage(List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, ref Point drawPos, Point defaultPos, string fragmentString, bool highlightWords)
        {
            ReadOnlySpan<char> fragmentSpan = fragmentString.AsSpan().Trim('\uFE0F');

            // TODO: use fragmentSpan instead of fragmentString once upgraded to .NET 7
            if (BlockArtRegex.IsMatch(fragmentString))
            {
                // Very rough estimation of width of block art
                int textWidth = (int)(fragmentSpan.Length * renderOptions.BlockArtCharWidth);
                if (renderOptions.BlockArtPreWrap && drawPos.X + textWidth > renderOptions.BlockArtPreWrapWidth)
                {
                    AddImageSection(sectionImages, ref drawPos, defaultPos);
                }
            }

            // We cannot draw nonFont chars individually or Arabic script looks improper https://github.com/lay295/TwitchDownloader/issues/484
            // The fragment has either surrogate pairs or characters not in the messageFont
            var inFontBuffer = new StringBuilder();
            var nonFontBuffer = new StringBuilder();
            for (int j = 0; j < fragmentSpan.Length; j++)
            {
                if (char.IsHighSurrogate(fragmentSpan[j]) && j + 1 < fragmentSpan.Length && char.IsLowSurrogate(fragmentSpan[j + 1]))
                {
                    if (inFontBuffer.Length > 0)
                    {
                        DrawText(inFontBuffer.ToString(), messageFont, false, sectionImages, ref drawPos, defaultPos, highlightWords);
                        inFontBuffer.Clear();
                    }
                    if (nonFontBuffer.Length > 0)
                    {
                        using SKPaint nonFontFallbackFont = GetFallbackFont(nonFontBuffer[0]).Clone();
                        nonFontFallbackFont.Color = renderOptions.MessageColor;
                        DrawText(nonFontBuffer.ToString(), nonFontFallbackFont, false, sectionImages, ref drawPos, defaultPos, highlightWords);
                        nonFontBuffer.Clear();
                    }
                    int utf32Char = char.ConvertToUtf32(fragmentSpan[j], fragmentSpan[j + 1]);
                    //Don't attempt to draw U+E0000
                    if (utf32Char != 0xE0000)
                    {
                        using SKPaint highSurrogateFallbackFont = GetFallbackFont(utf32Char).Clone();
                        highSurrogateFallbackFont.Color = renderOptions.MessageColor;
                        DrawText(fragmentSpan.Slice(j, 2).ToString(), highSurrogateFallbackFont, false, sectionImages, ref drawPos, defaultPos, highlightWords);
                    }
                    j++;
                }
                else if (!messageFont.ContainsGlyphs(fragmentSpan.Slice(j, 1)) || new StringInfo(fragmentSpan[j].ToString()).LengthInTextElements == 0)
                {
                    if (inFontBuffer.Length > 0)
                    {
                        DrawText(inFontBuffer.ToString(), messageFont, false, sectionImages, ref drawPos, defaultPos, highlightWords);
                        inFontBuffer.Clear();
                    }

                    nonFontBuffer.Append(fragmentSpan[j]);
                }
                else
                {
                    if (nonFontBuffer.Length > 0)
                    {
                        using SKPaint fallbackFont = GetFallbackFont(nonFontBuffer[0]).Clone();
                        fallbackFont.Color = renderOptions.MessageColor;
                        DrawText(nonFontBuffer.ToString(), fallbackFont, false, sectionImages, ref drawPos, defaultPos, highlightWords);
                        nonFontBuffer.Clear();
                    }

                    inFontBuffer.Append(fragmentSpan[j]);
                }
            }
            // Only one or the other should occur
            if (nonFontBuffer.Length > 0)
            {
                using SKPaint fallbackFont = GetFallbackFont(nonFontBuffer[0]).Clone();
                fallbackFont.Color = renderOptions.MessageColor;
                DrawText(nonFontBuffer.ToString(), fallbackFont, true, sectionImages, ref drawPos, defaultPos, highlightWords);
                nonFontBuffer.Clear();
            }
            if (inFontBuffer.Length > 0)
            {
                DrawText(inFontBuffer.ToString(), messageFont, true, sectionImages, ref drawPos, defaultPos, highlightWords);
                inFontBuffer.Clear();
            }
        }

        private void DrawRegularMessage(List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, int bitsCount, string fragmentString, bool highlightWords)
        {
            bool bitsPrinted = false;
            if (bitsCount > 0 && fragmentString.Any(char.IsDigit) && fragmentString.Any(char.IsLetter))
            {
                int bitsIndex = fragmentString.AsSpan().IndexOfAny("0123456789");
                if (int.TryParse(fragmentString.AsSpan(bitsIndex), out var bitsAmount) && TryGetCheerEmote(cheermotesList, fragmentString.AsSpan(0, bitsIndex), out var currentCheerEmote))
                {
                    KeyValuePair<int, TwitchEmote> tierList = currentCheerEmote.getTier(bitsAmount);
                    TwitchEmote cheerEmote = tierList.Value;
                    SKImageInfo cheerEmoteInfo = cheerEmote.Info;
                    if (drawPos.X + cheerEmoteInfo.Width > renderOptions.ChatWidth - renderOptions.SidePadding * 2)
                    {
                        AddImageSection(sectionImages, ref drawPos, defaultPos);
                    }

                    Point emotePoint = new Point
                    {
                        X = drawPos.X,
                        Y = (int)(sectionImages.Sum(x => x.info.Height) - renderOptions.SectionHeight + ((renderOptions.SectionHeight - cheerEmoteInfo.Height) / 2.0))
                    };
                    emotePositionList.Add((emotePoint, cheerEmote));
                    drawPos.X += cheerEmoteInfo.Width + renderOptions.EmoteSpacing;
                    bitsPrinted = true;
                }
            }
            if (!bitsPrinted)
            {
                DrawText(fragmentString, messageFont, true, sectionImages, ref drawPos, defaultPos, highlightWords);
            }

            static bool TryGetCheerEmote(List<CheerEmote> cheerEmoteList, ReadOnlySpan<char> prefix, out CheerEmote cheerEmote)
            {
                // Enumerating over a span is faster than a list
                var cheerEmoteListSpan = CollectionsMarshal.AsSpan(cheerEmoteList);
                foreach (var emote1 in cheerEmoteListSpan)
                {
                    if (emote1.prefix.AsSpan().Equals(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        cheerEmote = emote1;
                        return true;
                    }
                }

                cheerEmote = default;
                return false;
            }
        }

        private void DrawFirstPartyEmote(List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, Fragment fragment, bool highlightWords)
        {
            // First party emote
            if (TryGetTwitchEmote(emoteList, fragment.emoticon.emoticon_id, out var emote))
            {
                SKImageInfo emoteInfo = emote.Info;
                if (drawPos.X + emoteInfo.Width > renderOptions.ChatWidth - renderOptions.SidePadding * 2)
                {
                    AddImageSection(sectionImages, ref drawPos, defaultPos);
                }
                Point emotePoint = new Point
                {
                    X = drawPos.X,
                    Y = (int)(sectionImages.Sum(x => x.info.Height) - renderOptions.SectionHeight + ((renderOptions.SectionHeight - emoteInfo.Height) / 2.0))
                };

                if (highlightWords)
                {
                    using var canvas = new SKCanvas(sectionImages.Last().bitmap);
                    canvas.DrawRect(drawPos.X, 0, emoteInfo.Width + renderOptions.EmoteSpacing, renderOptions.SectionHeight, new SKPaint() { Color = Purple });
                }

                emotePositionList.Add((emotePoint, emote));
                drawPos.X += emoteInfo.Width + renderOptions.EmoteSpacing;
            }
            else
            {
                // Probably an old emote that was removed
                DrawText(fragment.text, messageFont, true, sectionImages, ref drawPos, defaultPos, highlightWords);
            }

            static bool TryGetTwitchEmote(List<TwitchEmote> twitchEmoteList, ReadOnlySpan<char> emoteId, out TwitchEmote twitchEmote)
            {
                // Enumerating over a span is faster than a list
                var emoteListSpan = CollectionsMarshal.AsSpan(twitchEmoteList);
                foreach (var emote1 in emoteListSpan)
                {
                    if (emote1.Id.AsSpan().SequenceEqual(emoteId))
                    {
                        twitchEmote = emote1;
                        return true;
                    }
                }

                twitchEmote = default;
                return false;
            }
        }

        private void DrawText(string drawText, SKPaint textFont, bool padding, List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, ref Point drawPos, Point defaultPos, bool highlightWords, bool noWrap = false)
        {
            bool isRtl = IsRightToLeft(drawText);
            float textWidth = MeasureText(drawText, textFont, isRtl);
            int effectiveChatWidth = renderOptions.ChatWidth - renderOptions.SidePadding - defaultPos.X;

            while (!noWrap && textWidth > effectiveChatWidth)
            {
                string newDrawText = SubstringToTextWidth(drawText, textFont, effectiveChatWidth, isRtl, "?-").ToString();
                var overrideWrap = false;

                if (newDrawText.Length == 0)
                {
                    // When chat width is small enough and font size is big enough, 1 character can be wider than effectiveChatWidth.
                    overrideWrap = true;
                    newDrawText = drawText[..1];
                }

                DrawText(newDrawText, textFont, padding, sectionImages, ref drawPos, defaultPos, highlightWords, overrideWrap);

                drawText = drawText[newDrawText.Length..];
                textWidth = MeasureText(drawText, textFont, isRtl);
            }
            if (drawPos.X + textWidth > renderOptions.ChatWidth - renderOptions.SidePadding * 2)
            {
                AddImageSection(sectionImages, ref drawPos, defaultPos);
            }

            using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImages.Last().bitmap))
            {
                if (highlightWords)
                {
                    using var paint = new SKPaint { Color = Purple};
                    sectionImageCanvas.DrawRect(drawPos.X, 0, textWidth + (padding ? renderOptions.WordSpacing : 0), renderOptions.SectionHeight, paint);
                }

                if (renderOptions.Outline)
                {
                    using var outlinePath = isRtl
                        ? textFont.GetShapedTextPath(drawText, drawPos.X, drawPos.Y)
                        : textFont.GetTextPath(drawText, drawPos.X, drawPos.Y);

                    sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
                }

                if (RtlRegex.IsMatch(drawText))
                {
                    sectionImageCanvas.DrawShapedText(drawText, drawPos.X, drawPos.Y, textFont);
                }
                else
                {
                    sectionImageCanvas.DrawText(drawText, drawPos.X, drawPos.Y, textFont);
                }
            }

            drawPos.X += (int)Math.Floor(textWidth + (padding ? renderOptions.WordSpacing : 0));
        }

        /// <summary>
        /// Produces a <see langword="string"/> less than or equal to <paramref name="maxWidth"/> when drawn with <paramref name="textFont"/> OR substringed to the last index of any character in <paramref name="delimiters"/>.
        /// </summary>
        /// <returns>A shortened in visual width or delimited <see langword="string"/>, whichever comes first.</returns>
        private static ReadOnlySpan<char> SubstringToTextWidth(ReadOnlySpan<char> text, SKPaint textFont, int maxWidth, bool isRtl, ReadOnlySpan<char> delimiters)
        {
            // If we are dealing with non-RTL and don't have any delimiters then SKPaint.BreakText is over 9x faster
            if (!isRtl && text.IndexOfAny(delimiters) == -1)
            {
                return SubstringToTextWidth(text, textFont, maxWidth);
            }

            using var shaper = isRtl
                ? new SKShaper(textFont.Typeface)
                : null;

            // Input text was already less than max width
            if (MeasureText(text, textFont, isRtl, shaper) <= maxWidth)
            {
                return text;
            }

            // Cut in half until <= width
            var length = text.Length;
            do
            {
                length /= 2;
            }
            while (MeasureText(text[..length], textFont, isRtl, shaper) > maxWidth);

            // Add chars until greater than width, then remove the last
            do
            {
                length++;
            } while (MeasureText(text[..length], textFont, isRtl, shaper) < maxWidth);
            text = text[..(length - 1)];

            // Cut at the last delimiter character if applicable
            var delimiterIndex = text.LastIndexOfAny(delimiters);
            if (delimiterIndex != -1)
            {
                return text[..(delimiterIndex + 1)];
            }

            return text;
        }

        /// <summary>
        /// Produces a <see cref="ReadOnlySpan{T}"/> less than or equal to <paramref name="maxWidth"/> when drawn with <paramref name="textFont"/>
        /// </summary>
        /// <returns>A shortened in visual width <see cref="ReadOnlySpan{T}"/>.</returns>
        /// <remarks>This is not compatible with text that needs to be shaped.</remarks>
        private static ReadOnlySpan<char> SubstringToTextWidth(ReadOnlySpan<char> text, SKPaint textFont, int maxWidth)
        {
            var length = (int)textFont.BreakText(text, maxWidth);
            return text[..length];
        }

        private static float MeasureText(ReadOnlySpan<char> text, SKPaint textFont, bool? isRtl = null, SKShaper shaper = null)
        {
            isRtl ??= IsRightToLeft(text);

            if (isRtl == false)
            {
                return textFont.MeasureText(text);
            }

            if (shaper == null)
            {
                return MeasureRtlText(text, textFont);
            }

            return MeasureRtlText(text, textFont, shaper);
        }

        private static float MeasureRtlText(ReadOnlySpan<char> rtlText, SKPaint textFont)
        {
            using var shaper = new SKShaper(textFont.Typeface);
            return MeasureRtlText(rtlText, textFont, shaper);
        }

        private static float MeasureRtlText(ReadOnlySpan<char> rtlText, SKPaint textFont, SKShaper shaper)
        {
            using var buffer = new HarfBuzzSharp.Buffer();
            buffer.Add(rtlText, textFont.TextEncoding);
            SKShaper.Result measure = shaper.Shape(buffer, textFont);
            return measure.Width;
        }

        private void DrawUsername(Comment comment, List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, ref Point drawPos, Point defaultPos, bool appendColon = true, string colorOverride = null)
        {
            SKColor userColor = SKColor.Parse(colorOverride ?? comment.message.user_color ?? DefaultUsernameColors[Math.Abs(comment.commenter.display_name.GetHashCode()) % DefaultUsernameColors.Length]);
            if (colorOverride is null)
                userColor = GenerateUserColor(userColor, renderOptions.BackgroundColor, renderOptions);

            using SKPaint userPaint = comment.commenter.display_name.Any(IsNotAscii)
                ? GetFallbackFont(comment.commenter.display_name.First(IsNotAscii)).Clone()
                : nameFont.Clone();

            userPaint.Color = userColor;
            string userName = comment.commenter.display_name + (appendColon ? ":" : "");
            DrawText(userName, userPaint, true, sectionImages, ref drawPos, defaultPos, false);
        }

        private static SKColor GenerateUserColor(SKColor userColor, SKColor backgroundColor, ChatRenderOptions renderOptions)
        {
            backgroundColor.ToHsl(out _, out _, out float backgroundBrightness);
            userColor.ToHsl(out float userHue, out float userSaturation, out float userBrightness);

            if (backgroundBrightness < 25 || renderOptions.Outline)
            {
                //Dark background or black outline
                if (userBrightness < 45)
                    userBrightness = 45;
                if (userSaturation > 80)
                    userSaturation = 80;
                SKColor newColor = SKColor.FromHsl(userHue, userSaturation, userBrightness);
                return newColor;
            }

            if (Math.Abs(backgroundBrightness - userBrightness) < 10 && backgroundBrightness > 50)
            {
                userBrightness -= 20;
                SKColor newColor = SKColor.FromHsl(userHue, userSaturation, userBrightness);
                return newColor;
            }

            return userColor;
        }

        private void DrawBadges(Comment comment, List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, ref Point drawPos)
        {
            using SKCanvas sectionImageCanvas = new SKCanvas(sectionImages.Last().bitmap);
            List<(SKBitmap, ChatBadgeType)> badgeImages = ParseCommentBadges(comment);
            foreach (var (badgeImage, badgeType) in badgeImages)
            {
                //Don't render filtered out badges
                if (((ChatBadgeType)renderOptions.ChatBadgeMask).HasFlag(badgeType))
                    continue;

                float badgeY = (float)((renderOptions.SectionHeight - badgeImage.Height) / 2.0);
                sectionImageCanvas.DrawBitmap(badgeImage, drawPos.X, badgeY);
                drawPos.X += badgeImage.Width + renderOptions.WordSpacing / 2;
            }
        }

        private List<(SKBitmap badgeImage, ChatBadgeType badgeType)> ParseCommentBadges(Comment comment)
        {
            List<(SKBitmap, ChatBadgeType)> returnList = new List<(SKBitmap, ChatBadgeType)>();

            if (comment.message.user_badges == null)
                return returnList;

            foreach (var badge in comment.message.user_badges)
            {
                string id = badge._id;
                string version = badge.version;

                foreach (var cachedBadge in badgeList)
                {
                    if (cachedBadge.Name != id)
                        continue;

                    foreach (var cachedVersion in cachedBadge.Versions)
                    {
                        if (cachedVersion.Key == version)
                        {
                            returnList.Add((cachedVersion.Value, cachedBadge.Type));
                            goto NextUserBadge;
                        }
                    }
                }

                // goto is cheaper and more readable than using a boolean + branch check after each operation
                NextUserBadge: ;
            }

            return returnList;
        }

        private void DrawTimestamp(Comment comment, List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, ref Point drawPos, ref Point defaultPos)
        {
            using var sectionImageCanvas = new SKCanvas(sectionImages.Last().bitmap);
            var timestamp = new TimeSpan(0, 0, (int)comment.content_offset_seconds);

            const int MAX_TIMESTAMP_LENGTH = 8; // 48:00:00
            var formattedTimestamp = FormatTimestamp(stackalloc char[MAX_TIMESTAMP_LENGTH], timestamp);

            if (renderOptions.Outline)
            {
                using var outlinePath = messageFont.GetTextPath(formattedTimestamp, drawPos.X, drawPos.Y);
                sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
            }

            sectionImageCanvas.DrawText(formattedTimestamp, drawPos.X, drawPos.Y, messageFont);
            var textWidth =
                timestamp.TotalHours >= 1
                    ? timestamp.TotalHours >= 10
                        ? renderOptions.TimestampWidths[3]
                        : renderOptions.TimestampWidths[2]
                    : timestamp.Minutes >= 10
                        ? renderOptions.TimestampWidths[1]
                        : renderOptions.TimestampWidths[0];
            drawPos.X += textWidth + renderOptions.WordSpacing * 2;
            defaultPos.X = drawPos.X;

            static ReadOnlySpan<char> FormatTimestamp(Span<char> stackSpace, TimeSpan timespan)
            {
                if (timespan.TotalHours >= 1)
                {
                    if (timespan.TotalHours >= 24)
                    {
                        return TimeSpanHFormat.ReusableInstance.Format(@"HH\:mm\:ss", timespan);
                    }

                    return timespan.TryFormat(stackSpace, out var charsWritten, @"h\:mm\:ss")
                        ? stackSpace[..charsWritten]
                        : timespan.ToString(@"h\:mm\:ss");
                }
                else
                {
                    return timespan.TryFormat(stackSpace, out var charsWritten, @"m\:ss")
                        ? stackSpace[..charsWritten]
                        : timespan.ToString(@"m\:ss");
                }
            }
        }

        private void AddImageSection(List<(SKImageInfo info, SKBitmap bitmap)> sectionImages, ref Point drawPos, Point defaultPos)
        {
            drawPos.X = defaultPos.X;
            drawPos.Y = defaultPos.Y;
            SKBitmap newBitmap = new SKBitmap(renderOptions.ChatWidth, renderOptions.SectionHeight);
            SKImageInfo newInfo = newBitmap.Info;
            sectionImages.Add((newInfo, newBitmap));
        }

        /// <summary>
        /// Fetches the emotes/badges/bits/emojis needed to render scaled to 2x
        /// </summary>
        /// <remarks>chatRoot.embeddedData will be empty after calling this to save on memory!</remarks>
        private async Task FetchScaledImages(CancellationToken cancellationToken)
        {
            var badgeTask = GetScaledBadges(cancellationToken);
            var emoteTask = GetScaledEmotes(cancellationToken);
            var emoteThirdTask = GetScaledThirdEmotes(cancellationToken);
            var cheerTask = GetScaledBits(cancellationToken);
            var emojiTask = GetScaledEmojis(cancellationToken);

            await Task.WhenAll(badgeTask, emoteTask, emoteThirdTask, cheerTask, emojiTask);

            // Clear chatRoot.embeddedData and manually call GC to save some memory
            chatRoot.embeddedData = null;
            GC.Collect();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            badgeList = badgeTask.Result;
            emoteList = emoteTask.Result;
            emoteThirdList = emoteThirdTask.Result;
            cheermotesList = cheerTask.Result;
            emojiCache = emojiTask.Result;
        }

        private async Task<List<ChatBadge>> GetScaledBadges(CancellationToken cancellationToken)
        {
            // Do not fetch if badges are disabled
            if (!renderOptions.ChatBadges)
            {
                return new List<ChatBadge>();
            }

            var badgeTask = await TwitchHelper.GetChatBadges(chatRoot.comments, chatRoot.streamer.id, renderOptions.TempFolder, chatRoot.embeddedData, renderOptions.Offline, cancellationToken);

            foreach (var badge in badgeTask)
            {
                // Assume badges are always 2x scale, not 1x or 4x
                if (Math.Abs(renderOptions.ReferenceScale - 1.0) > 0.01)
                {
                    badge.Resize(renderOptions.ReferenceScale * renderOptions.BadgeScale);
                }
                badge.VersionsData.Clear(); // Clear the image byte[]s as we aren't embedding to an output file
            }

            return badgeTask;
        }

        private async Task<List<TwitchEmote>> GetScaledEmotes(CancellationToken cancellationToken)
        {
            var emoteTask = await TwitchHelper.GetEmotes(chatRoot.comments, renderOptions.TempFolder, chatRoot.embeddedData, renderOptions.Offline, cancellationToken);

            foreach (var emote in emoteTask)
            {
                double newScale = (2.0 / emote.ImageScale) * renderOptions.ReferenceScale * renderOptions.EmoteScale;
                if (Math.Abs(newScale - 1.0) > 0.01)
                {
                    emote.Resize(newScale);
                }
                emote.ImageData = Array.Empty<byte>(); // Clear the image byte[] as we aren't embedding to an output file
            }

            return emoteTask;
        }

        private async Task<List<TwitchEmote>> GetScaledThirdEmotes(CancellationToken cancellationToken)
        {
            var emoteThirdTask = await TwitchHelper.GetThirdPartyEmotes(chatRoot.comments, chatRoot.streamer.id, renderOptions.TempFolder, chatRoot.embeddedData, renderOptions.BttvEmotes, renderOptions.FfzEmotes,
                renderOptions.StvEmotes, renderOptions.AllowUnlistedEmotes, renderOptions.Offline, cancellationToken);

            foreach (var emote in emoteThirdTask)
            {
                double newScale = (2.0 / emote.ImageScale) * renderOptions.ReferenceScale * renderOptions.EmoteScale;
                if (Math.Abs(newScale - 1.0) > 0.01)
                {
                    emote.Resize(newScale);
                }
                emote.ImageData = Array.Empty<byte>(); // Clear the image byte[] as we aren't embedding to an output file
            }

            return emoteThirdTask;
        }

        private async Task<List<CheerEmote>> GetScaledBits(CancellationToken cancellationToken)
        {
            var cheerTask = await TwitchHelper.GetBits(chatRoot.comments, renderOptions.TempFolder, chatRoot.streamer.id.ToString(), chatRoot.embeddedData, renderOptions.Offline, cancellationToken);

            foreach (var cheer in cheerTask)
            {
                //Assume cheermotes are always 2x scale, not 1x or 4x
                if (Math.Abs(renderOptions.ReferenceScale - 1.0) > 0.01)
                {
                    cheer.Resize(renderOptions.ReferenceScale * renderOptions.EmoteScale);
                }

                foreach (var tier in cheer.tierList)
                {
                    tier.Value.ImageData = Array.Empty<byte>(); // Clear the image byte[]s as we aren't embedding to an output file
                }
            }

            return cheerTask;
        }

        private async Task<Dictionary<string, SKBitmap>> GetScaledEmojis(CancellationToken cancellationToken)
        {
            var emojiTask = await TwitchHelper.GetEmojis(renderOptions.TempFolder, renderOptions.EmojiVendor, cancellationToken);

            //Assume emojis are 4x (they're 72x72)
            double emojiScale = 0.5 * renderOptions.ReferenceScale * renderOptions.EmojiScale;
            List<string> emojiKeys = new List<string>(emojiTask.Keys);
            foreach (var emojiKey in emojiKeys)
            {
                SKImageInfo oldEmojiInfo = emojiTask[emojiKey].Info;
                SKImageInfo imageInfo = new SKImageInfo((int)(oldEmojiInfo.Width * emojiScale), (int)(oldEmojiInfo.Height * emojiScale));
                SKBitmap newBitmap = new SKBitmap(imageInfo);
                emojiTask[emojiKey].ScalePixels(newBitmap, SKFilterQuality.High);
                emojiTask[emojiKey].Dispose();
                emojiTask[emojiKey] = newBitmap;
            }

            return emojiTask;
        }

        private (int startTick, int totalTicks) GetVideoTicks()
        {
            if (renderOptions.StartOverride != -1 && renderOptions.EndOverride != -1)
            {
                int startSeconds = renderOptions.StartOverride;
                int videoStartTick = startSeconds * renderOptions.Framerate;
                int totalTicks = renderOptions.EndOverride * renderOptions.Framerate - videoStartTick;
                return (videoStartTick, totalTicks);
            }
            else
            {
                int startSeconds = (int)Math.Floor(chatRoot.video.start);
                int videoStartTick = startSeconds * renderOptions.Framerate;
                int totalTicks = (int)Math.Ceiling(chatRoot.video.end * renderOptions.Framerate) - videoStartTick;
                return (videoStartTick, totalTicks);
            }
        }

        private SKPaint GetFallbackFont(int input)
        {
            ref var fallbackPaint = ref CollectionsMarshal.GetValueRefOrAddDefault(fallbackFontCache, input, out bool alreadyExists);
            if (alreadyExists)
            {
                return fallbackPaint;
            }

            SKPaint newPaint = new SKPaint() { Typeface = fontManager.MatchCharacter(input), LcdRenderText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, SubpixelText = true, IsAutohinted = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            if (newPaint.Typeface == null)
            {
                newPaint.Typeface = SKTypeface.Default;
                if (!noFallbackFontFound)
                {
                    noFallbackFontFound = true;
                    _progress?.Report(new ProgressReport(ReportType.Log, "No valid typefaces were found for some messages."));
                }
            }

            fallbackPaint = newPaint;
            return newPaint;
        }

        private static bool IsNotAscii(char input)
        {
            return input > 127;
        }

        private static List<string> SwapRightToLeft(string[] words)
        {
            List<string> finalWords = new List<string>(words.Length);
            Stack<string> rtlStack = new Stack<string>();
            foreach (var word in words)
            {
                if (IsRightToLeft(word))
                {
                    rtlStack.Push(word);
                }
                else
                {
                    while (rtlStack.Count > 0)
                    {
                        finalWords.Add(rtlStack.Pop());
                    }
                    finalWords.Add(word);
                }
            }
            while (rtlStack.Count > 0)
            {
                finalWords.Add(rtlStack.Pop());
            }
            return finalWords;
        }

        private static bool IsRightToLeft(ReadOnlySpan<char> message)
        {
            if (message.Length > 0)
            {
                if (message[0] >= '\u0591' && message[0] <= '\u07FF')
                    return true;
                else
                    return false;
            }
            else
            {
                return false;
            }
        }

        public async Task<ChatRoot> ParseJsonAsync(CancellationToken cancellationToken = new())
        {
            chatRoot = await ChatJson.DeserializeAsync(renderOptions.InputFile, true, true, cancellationToken);
            return chatRoot;
        }


#region ImplementIDisposable

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool isDisposing)
        {
            try
            {
                if (Disposed)
                {
                    return;
                }

                if (isDisposing)
                {
                    foreach (var badge in badgeList)
                        badge?.Dispose();
                    foreach (var emote in emoteList)
                        emote?.Dispose();
                    foreach (var emote in emoteThirdList)
                        emote?.Dispose();
                    foreach (var cheerEmote in cheermotesList)
                        cheerEmote?.Dispose();
                    foreach (var (_, bitmap) in emojiCache)
                        bitmap?.Dispose();
                    foreach (var (_, paint) in fallbackFontCache)
                        paint?.Dispose();
                    fontManager?.Dispose();
                    nameFont?.Dispose();
                    messageFont?.Dispose();
                    outlinePaint?.Dispose();
                    highlightIcons?.Dispose();

                    badgeList.Clear();
                    emoteList.Clear();
                    emoteThirdList.Clear();
                    cheermotesList.Clear();
                    emojiCache.Clear();
                    fallbackFontCache.Clear();

                    // Set the root references to null to explicitly tell the garbage collector that the resources have been disposed
                    chatRoot = null;
                    badgeList = null;
                    emoteList = null;
                    emoteThirdList = null;
                    cheermotesList = null;
                    emojiCache = null;
                    fallbackFontCache = null;
                }
            }
            finally
            {
                Disposed = true;
            }
        }

#endregion
    }
}