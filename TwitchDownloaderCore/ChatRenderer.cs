using NeoSmart.Unicode;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public class ChatRenderer
    {
        static readonly string[] defaultColors = new string[] { "#FF0000", "#0000FF", "#00FF00", "#B22222", "#FF7F50", "#9ACD32", "#FF4500", "#2E8B57", "#DAA520", "#D2691E", "#5F9EA0", "#1E90FF", "#FF69B4", "#8A2BE2", "#00FF7F" };
        static readonly string emojiRegex = @"(?:[#*0-9]\uFE0F?\u20E3|©\uFE0F?|[®\u203C\u2049\u2122\u2139\u2194-\u2199\u21A9\u21AA\u231A\u231B\u2328\u23CF\u23ED-\u23EF\u23F1\u23F2\u23F8-\u23FA\u24C2\u25AA\u25AB\u25B6\u25C0\u25FB\u25FC\u25FE\u2600-\u2604\u260E\u2611\u2614\u2615\u2618\u2620\u2622\u2623\u2626\u262A\u262E\u262F\u2638-\u263A\u2640\u2642\u2648-\u2653\u265F\u2660\u2663\u2665\u2666\u2668\u267B\u267E\u267F\u2692\u2694-\u2697\u2699\u269B\u269C\u26A0\u26A7\u26AA\u26B0\u26B1\u26BD\u26BE\u26C4\u26C8\u26CF\u26D1\u26D3\u26E9\u26F0-\u26F5\u26F7\u26F8\u26FA\u2702\u2708\u2709\u270F\u2712\u2714\u2716\u271D\u2721\u2733\u2734\u2744\u2747\u2757\u2763\u27A1\u2934\u2935\u2B05-\u2B07\u2B1B\u2B1C\u2B55\u3030\u303D\u3297\u3299]\uFE0F?|[\u261D\u270C\u270D](?:\uFE0F|\uD83C[\uDFFB-\uDFFF])?|[\u270A\u270B](?:\uD83C[\uDFFB-\uDFFF])?|[\u23E9-\u23EC\u23F0\u23F3\u25FD\u2693\u26A1\u26AB\u26C5\u26CE\u26D4\u26EA\u26FD\u2705\u2728\u274C\u274E\u2753-\u2755\u2795-\u2797\u27B0\u27BF\u2B50]|\u26F9(?:\uFE0F|\uD83C[\uDFFB-\uDFFF])?(?:\u200D[\u2640\u2642]\uFE0F?)?|\u2764\uFE0F?(?:\u200D(?:\uD83D\uDD25|\uD83E\uDE79))?|\uD83C(?:[\uDC04\uDD70\uDD71\uDD7E\uDD7F\uDE02\uDE37\uDF21\uDF24-\uDF2C\uDF36\uDF7D\uDF96\uDF97\uDF99-\uDF9B\uDF9E\uDF9F\uDFCD\uDFCE\uDFD4-\uDFDF\uDFF5\uDFF7]\uFE0F?|[\uDF85\uDFC2\uDFC7](?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC3\uDFC4\uDFCA](?:\uD83C[\uDFFB-\uDFFF])?(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDFCB\uDFCC](?:\uFE0F|\uD83C[\uDFFB-\uDFFF])?(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDCCF\uDD8E\uDD91-\uDD9A\uDE01\uDE1A\uDE2F\uDE32-\uDE36\uDE38-\uDE3A\uDE50\uDE51\uDF00-\uDF20\uDF2D-\uDF35\uDF37-\uDF7C\uDF7E-\uDF84\uDF86-\uDF93\uDFA0-\uDFC1\uDFC5\uDFC6\uDFC8\uDFC9\uDFCF-\uDFD3\uDFE0-\uDFF0\uDFF8-\uDFFF]|\uDDE6\uD83C[\uDDE8-\uDDEC\uDDEE\uDDF1\uDDF2\uDDF4\uDDF6-\uDDFA\uDDFC\uDDFD\uDDFF]|\uDDE7\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEF\uDDF1-\uDDF4\uDDF6-\uDDF9\uDDFB\uDDFC\uDDFE\uDDFF]|\uDDE8\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDEE\uDDF0-\uDDF5\uDDF7\uDDFA-\uDDFF]|\uDDE9\uD83C[\uDDEA\uDDEC\uDDEF\uDDF0\uDDF2\uDDF4\uDDFF]|\uDDEA\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDED\uDDF7-\uDDFA]|\uDDEB\uD83C[\uDDEE-\uDDF0\uDDF2\uDDF4\uDDF7]|\uDDEC\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEE\uDDF1-\uDDF3\uDDF5-\uDDFA\uDDFC\uDDFE]|\uDDED\uD83C[\uDDF0\uDDF2\uDDF3\uDDF7\uDDF9\uDDFA]|\uDDEE\uD83C[\uDDE8-\uDDEA\uDDF1-\uDDF4\uDDF6-\uDDF9]|\uDDEF\uD83C[\uDDEA\uDDF2\uDDF4\uDDF5]|\uDDF0\uD83C[\uDDEA\uDDEC-\uDDEE\uDDF2\uDDF3\uDDF5\uDDF7\uDDFC\uDDFE\uDDFF]|\uDDF1\uD83C[\uDDE6-\uDDE8\uDDEE\uDDF0\uDDF7-\uDDFB\uDDFE]|\uDDF2\uD83C[\uDDE6\uDDE8-\uDDED\uDDF0-\uDDFF]|\uDDF3\uD83C[\uDDE6\uDDE8\uDDEA-\uDDEC\uDDEE\uDDF1\uDDF4\uDDF5\uDDF7\uDDFA\uDDFF]|\uDDF4\uD83C\uDDF2|\uDDF5\uD83C[\uDDE6\uDDEA-\uDDED\uDDF0-\uDDF3\uDDF7-\uDDF9\uDDFC\uDDFE]|\uDDF6\uD83C\uDDE6|\uDDF7\uD83C[\uDDEA\uDDF4\uDDF8\uDDFA\uDDFC]|\uDDF8\uD83C[\uDDE6-\uDDEA\uDDEC-\uDDF4\uDDF7-\uDDF9\uDDFB\uDDFD-\uDDFF]|\uDDF9\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDED\uDDEF-\uDDF4\uDDF7\uDDF9\uDDFB\uDDFC\uDDFF]|\uDDFA\uD83C[\uDDE6\uDDEC\uDDF2\uDDF3\uDDF8\uDDFE\uDDFF]|\uDDFB\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDEE\uDDF3\uDDFA]|\uDDFC\uD83C[\uDDEB\uDDF8]|\uDDFD\uD83C\uDDF0|\uDDFE\uD83C[\uDDEA\uDDF9]|\uDDFF\uD83C[\uDDE6\uDDF2\uDDFC]|\uDFF3\uFE0F?(?:\u200D(?:\u26A7\uFE0F?|\uD83C\uDF08))?|\uDFF4(?:\u200D\u2620\uFE0F?|\uDB40\uDC67\uDB40\uDC62\uDB40(?:\uDC65\uDB40\uDC6E\uDB40\uDC67|\uDC73\uDB40\uDC63\uDB40\uDC74|\uDC77\uDB40\uDC6C\uDB40\uDC73)\uDB40\uDC7F)?)|\uD83D(?:[\uDC3F\uDCFD\uDD49\uDD4A\uDD6F\uDD70\uDD73\uDD76-\uDD79\uDD87\uDD8A-\uDD8D\uDDA5\uDDA8\uDDB1\uDDB2\uDDBC\uDDC2-\uDDC4\uDDD1-\uDDD3\uDDDC-\uDDDE\uDDE1\uDDE3\uDDE8\uDDEF\uDDF3\uDDFA\uDECB\uDECD-\uDECF\uDEE0-\uDEE5\uDEE9\uDEF0\uDEF3]\uFE0F?|[\uDC42\uDC43\uDC46-\uDC50\uDC66\uDC67\uDC6B-\uDC6D\uDC72\uDC74-\uDC76\uDC78\uDC7C\uDC83\uDC85\uDC8F\uDC91\uDCAA\uDD7A\uDD95\uDD96\uDE4C\uDE4F\uDEC0\uDECC](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC6E\uDC70\uDC71\uDC73\uDC77\uDC81\uDC82\uDC86\uDC87\uDE45-\uDE47\uDE4B\uDE4D\uDE4E\uDEA3\uDEB4-\uDEB6](?:\uD83C[\uDFFB-\uDFFF])?(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDD74\uDD90](?:\uFE0F|\uD83C[\uDFFB-\uDFFF])?|[\uDC00-\uDC07\uDC09-\uDC14\uDC16-\uDC3A\uDC3C-\uDC3E\uDC40\uDC44\uDC45\uDC51-\uDC65\uDC6A\uDC79-\uDC7B\uDC7D-\uDC80\uDC84\uDC88-\uDC8E\uDC90\uDC92-\uDCA9\uDCAB-\uDCFC\uDCFF-\uDD3D\uDD4B-\uDD4E\uDD50-\uDD67\uDDA4\uDDFB-\uDE2D\uDE2F-\uDE34\uDE37-\uDE44\uDE48-\uDE4A\uDE80-\uDEA2\uDEA4-\uDEB3\uDEB7-\uDEBF\uDEC1-\uDEC5\uDED0-\uDED2\uDED5-\uDED7\uDEDD-\uDEDF\uDEEB\uDEEC\uDEF4-\uDEFC\uDFE0-\uDFEB\uDFF0]|\uDC08(?:\u200D\u2B1B)?|\uDC15(?:\u200D\uD83E\uDDBA)?|\uDC3B(?:\u200D\u2744\uFE0F?)?|\uDC41\uFE0F?(?:\u200D\uD83D\uDDE8\uFE0F?)?|\uDC68(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:[\uDC68\uDC69]\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFC-\uDFFF])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFD-\uDFFF])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFD\uDFFF])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFE])))?))?|\uDC69(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?[\uDC68\uDC69]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|\uDC69\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?))|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]|\uDC8B\u200D\uD83D[\uDC68\uDC69])\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFC-\uDFFF])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]|\uDC8B\u200D\uD83D[\uDC68\uDC69])\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB\uDFFD-\uDFFF])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]|\uDC8B\u200D\uD83D[\uDC68\uDC69])\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]|\uDC8B\u200D\uD83D[\uDC68\uDC69])\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFD\uDFFF])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]|\uDC8B\u200D\uD83D[\uDC68\uDC69])\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFE])))?))?|\uDC6F(?:\u200D[\u2640\u2642]\uFE0F?)?|\uDD75(?:\uFE0F|\uD83C[\uDFFB-\uDFFF])?(?:\u200D[\u2640\u2642]\uFE0F?)?|\uDE2E(?:\u200D\uD83D\uDCA8)?|\uDE35(?:\u200D\uD83D\uDCAB)?|\uDE36(?:\u200D\uD83C\uDF2B\uFE0F?)?)|\uD83E(?:[\uDD0C\uDD0F\uDD18-\uDD1F\uDD30-\uDD34\uDD36\uDD77\uDDB5\uDDB6\uDDBB\uDDD2\uDDD3\uDDD5\uDEC3-\uDEC5\uDEF0\uDEF2-\uDEF6](?:\uD83C[\uDFFB-\uDFFF])?|[\uDD26\uDD35\uDD37-\uDD39\uDD3D\uDD3E\uDDB8\uDDB9\uDDCD-\uDDCF\uDDD4\uDDD6-\uDDDD](?:\uD83C[\uDFFB-\uDFFF])?(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDDDE\uDDDF](?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDD0D\uDD0E\uDD10-\uDD17\uDD20-\uDD25\uDD27-\uDD2F\uDD3A\uDD3F-\uDD45\uDD47-\uDD76\uDD78-\uDDB4\uDDB7\uDDBA\uDDBC-\uDDCC\uDDD0\uDDE0-\uDDFF\uDE70-\uDE74\uDE78-\uDE7C\uDE80-\uDE86\uDE90-\uDEAC\uDEB0-\uDEBA\uDEC0-\uDEC2\uDED0-\uDED9\uDEE0-\uDEE7]|\uDD3C(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF])?|\uDDD1(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83E\uDDD1))|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFC-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB\uDFFD-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB-\uDFFD\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB-\uDFFE]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:[\uDDAF-\uDDB3\uDDBC\uDDBD]|\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])))?))?|\uDEF1(?:\uD83C(?:\uDFFB(?:\u200D\uD83E\uDEF2\uD83C[\uDFFC-\uDFFF])?|\uDFFC(?:\u200D\uD83E\uDEF2\uD83C[\uDFFB\uDFFD-\uDFFF])?|\uDFFD(?:\u200D\uD83E\uDEF2\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF])?|\uDFFE(?:\u200D\uD83E\uDEF2\uD83C[\uDFFB-\uDFFD\uDFFF])?|\uDFFF(?:\u200D\uD83E\uDEF2\uD83C[\uDFFB-\uDFFE])?))?))";

        public ChatRoot chatRoot { get; set; } = new ChatRoot();
        private ChatRenderOptions renderOptions = new ChatRenderOptions();
        private List<ChatBadge> badgeList = new List<ChatBadge>();
        private List<TwitchEmote> emoteList = new List<TwitchEmote>();
        private List<TwitchEmote> emoteThirdList = new List<TwitchEmote>();
        private List<CheerEmote> cheermotesList = new List<CheerEmote>();
        private Dictionary<string, SKBitmap> emojiCache = new Dictionary<string, SKBitmap>();
        private ConcurrentDictionary<int, SKPaint> fallbackCache = new ConcurrentDictionary<int, SKPaint>();
        private SKFontManager fontManager = SKFontManager.CreateDefault();
        private SKPaint messageFont = new SKPaint();
        private SKPaint nameFont = new SKPaint();
        private SKPaint outlinePaint = new SKPaint();

        public ChatRenderer(ChatRenderOptions chatRenderOptions)
        {
            renderOptions = chatRenderOptions;
            renderOptions.TempFolder = renderOptions.TempFolder == null || renderOptions.TempFolder == "" ? Path.Combine(Path.GetTempPath(), "TwitchDownloader") : Path.Combine(renderOptions.TempFolder, "TwitchDownloader");
        }

        public async Task RenderVideoAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Fetching Images" });
            Task<List<ChatBadge>> badgeTask = Task.Run(() => TwitchHelper.GetChatBadges(chatRoot.streamer.id, renderOptions.TempFolder));
            Task<List<TwitchEmote>> emoteTask = Task.Run(() => TwitchHelper.GetEmotes(chatRoot.comments, renderOptions.TempFolder, chatRoot.emotes));
            Task<List<TwitchEmote>> emoteThirdTask = Task.Run(() => TwitchHelper.GetThirdPartyEmotes(chatRoot.streamer.id, renderOptions.TempFolder, chatRoot.emotes, renderOptions.BttvEmotes, renderOptions.FfzEmotes, renderOptions.StvEmotes));
            Task<List<CheerEmote>> cheerTask = Task.Run(() => TwitchHelper.GetBits(renderOptions.TempFolder, chatRoot.streamer.id.ToString()));
            Task<Dictionary<string, SKBitmap>> emojiTask = Task.Run(() => TwitchHelper.GetTwitterEmojis(renderOptions.TempFolder));

            await Task.WhenAll(badgeTask, emoteTask, emoteThirdTask, cheerTask, emojiTask);

            badgeList = badgeTask.Result;
            emoteList = emoteTask.Result;
            emoteThirdList = emoteThirdTask.Result;
            cheermotesList = cheerTask.Result;
            emojiCache = emojiTask.Result;

            await Task.Run(() => ScaleImages());

            outlinePaint = new SKPaint() { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(renderOptions.OutlineSize * renderOptions.EmoteScale), StrokeJoin = SKStrokeJoin.Round, Color = SKColors.Black, IsAntialias = true, IsAutohinted = true, LcdRenderText = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            nameFont = new SKPaint() { Typeface = SKTypeface.FromFamilyName(renderOptions.Font, renderOptions.UsernameFontStyle), LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, IsAutohinted = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            messageFont = new SKPaint() { Typeface = SKTypeface.FromFamilyName(renderOptions.Font, renderOptions.MessageFontStyle), LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, IsAutohinted = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High, Color = renderOptions.MessageColor };

            (int, int) tickValues = GetTotalTicks();
            int totalTicks = tickValues.Item2;
            int startTick = tickValues.Item1;

            if (File.Exists(renderOptions.OutputFile))
                File.Delete(renderOptions.OutputFile);

            if (renderOptions.GenerateMask && File.Exists(renderOptions.OutputFileMask))
                File.Delete(renderOptions.OutputFileMask);

            progress.Report(new ProgressReport() { reportType = ReportType.MessageInfo, data = "Rendering Video 0%" });
            (Process, string) processInfo = GetFfmpegProcess(0, false);

            if (renderOptions.GenerateMask)
            {
                (Process, string) maskInfo = GetFfmpegProcess(0, true);
                await Task.Run(() => RenderVideoSection(processInfo.Item1, maskInfo.Item1, startTick, startTick + totalTicks, progress), cancellationToken);
            }
            else
            {
                await Task.Run(() => RenderVideoSection(processInfo.Item1, null, startTick, startTick + totalTicks, progress), cancellationToken);
            }
        }

        private void RenderVideoSection(Process ffmpegProcess, Process maskProcess, int startTick, int endTick, IProgress<ProgressReport> progress = null)
        {
            UpdateFrame lastestUpdate = null;
            BinaryWriter ffmpegStream = new BinaryWriter(ffmpegProcess.StandardInput.BaseStream);
            BinaryWriter maskStream = null;
            if (maskProcess != null)
                maskStream = new BinaryWriter(maskProcess.StandardInput.BaseStream);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int currentTick = startTick; currentTick < endTick; currentTick++)
            {
                if (currentTick % renderOptions.UpdateFrame == 0)
                {
                    if (lastestUpdate != null)
                        lastestUpdate.Image.Dispose();

                    lastestUpdate = GenerateUpdateFrame(currentTick);
                }

                using SKBitmap frame = GetFrameFromTick(currentTick, lastestUpdate);
                ffmpegStream.Write(frame.Bytes);

                if (maskProcess != null)
                {
                    SetFrameMask(frame);
                    maskStream.Write(frame.Bytes);
                }

                double percentDouble = (double)(currentTick - startTick) / (double)(endTick - startTick) * 100.0;
                int percentInt = (int)Math.Floor(percentDouble);
                if (progress != null)
                {
                    progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = percentInt });
                    int timeLeftInt = (int)Math.Floor(100.0 / percentDouble * stopwatch.Elapsed.TotalSeconds) - (int)stopwatch.Elapsed.TotalSeconds;
                    TimeSpan timeLeft = new TimeSpan(0, 0, timeLeftInt);
                    progress.Report(new ProgressReport() { reportType = ReportType.MessageInfo, data = $"Rendering Video {percentInt}% ({timeLeft.ToString(@"h\hm\ms\s")} left)" });
                }
            }
            progress.Report(new ProgressReport() { reportType = ReportType.MessageInfo, data = "Rendering Video 100%" });
            stopwatch.Stop();
            progress.Report(new ProgressReport() { reportType = ReportType.Log, data = $"FINISHED. RENDER TIME: {(int)stopwatch.Elapsed.TotalSeconds}s SPEED: {((endTick-startTick) / renderOptions.Framerate / stopwatch.Elapsed.TotalSeconds).ToString("0.##")}x" });

            ffmpegStream.Dispose();
            if (maskProcess != null)
                maskStream.Dispose();

            ffmpegProcess.WaitForExit();
            if (maskProcess != null)
                maskProcess.WaitForExit();
        }

        private void SetFrameMask(SKBitmap frame)
        {
            IntPtr pixelsAddr = frame.GetPixels();
            int height = frame.Height;
            int width = frame.Width;
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

        private (Process, string) GetFfmpegProcess(int partNumer, bool isMask)
        {
            string savePath;
            if (partNumer == 0)
            {
                if (isMask)
                    savePath = renderOptions.OutputFileMask;
                else
                    savePath = renderOptions.OutputFile;
            }
            else
            {
                savePath = Path.Combine(renderOptions.TempFolder, Path.GetRandomFileName() + (isMask ? "_mask" : "") + Path.GetExtension(renderOptions.OutputFile));
            }

            string inputArgs = renderOptions.InputArgs.Replace("{fps}", renderOptions.Framerate.ToString())
                .Replace("{height}", renderOptions.ChatHeight.ToString()).Replace("{width}", renderOptions.ChatWidth.ToString())
                .Replace("{save_path}", savePath).Replace("{max_int}", int.MaxValue.ToString())
                .Replace("{pix_fmt}", new SKBitmap().ColorType == SKColorType.Rgba8888 ? "rgba" : "bgra");
            string outputArgs = renderOptions.OutputArgs.Replace("{fps}", renderOptions.Framerate.ToString())
                .Replace("{height}", renderOptions.ChatHeight.ToString()).Replace("{width}", renderOptions.ChatWidth.ToString())
                .Replace("{save_path}", savePath).Replace("{max_int}", int.MaxValue.ToString());

            var process = new Process
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
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            return (process, savePath);
        }

        private SKBitmap GetFrameFromTick(int currentTick, UpdateFrame currentFrame = null)
        {
            if (currentFrame == null)
                currentFrame = GenerateUpdateFrame(currentTick);
            SKBitmap frame = DrawAnimatedEmotes(currentFrame.Image, currentFrame.Comments, currentTick);
            return frame;
        }

        private SKBitmap DrawAnimatedEmotes(SKBitmap updateFrame, List<CommentSection> comments, int currentTick)
        {
            SKBitmap newFrame = updateFrame.Copy();
            int frameHeight = renderOptions.ChatHeight;
            int currentTickMs = (int)(currentTick * 1000 * (1.0 / renderOptions.Framerate));
            using (SKCanvas frameCanvas = new SKCanvas(newFrame))
            {
                foreach (var comment in comments)
                {
                    frameHeight -= comment.Image.Height + renderOptions.VerticalPadding;
                    foreach (var emote in comment.Emotes)
                    {
                        if (emote.Item2.FrameCount > 1)
                        {
                            int frameIndex = emote.Item2.EmoteFrameDurations.Count - 1;
                            int imageFrame = currentTickMs % emote.Item2.EmoteFrameDurations.Sum(x => x * 10);
                            for (int i = 0; i < emote.Item2.EmoteFrameDurations.Count; i++)
                            {
                                if (imageFrame - emote.Item2.EmoteFrameDurations[i] * 10 <= 0)
                                {
                                    frameIndex = i;
                                    break;
                                }
                                imageFrame -= emote.Item2.EmoteFrameDurations[i] * 10;
                            }

                            frameCanvas.DrawBitmap(emote.Item2.EmoteFrames[frameIndex], emote.Item1.X, emote.Item1.Y + frameHeight);
                        }
                    }
                }
            }
            return newFrame;
        }

        private UpdateFrame GenerateUpdateFrame(int currentTick)
        {
            List<CommentSection> commentList = new List<CommentSection>();
            SKBitmap newFrame = new SKBitmap(renderOptions.ChatWidth, renderOptions.ChatHeight);
            using (SKCanvas frameCanvas = new SKCanvas(newFrame))
            {
                double currentTimeSeconds = currentTick / renderOptions.Framerate;
                int commentIndex = chatRoot.comments.FindLastIndex(x => x.content_offset_seconds < currentTimeSeconds);

                int frameHeight = renderOptions.ChatHeight;
                frameCanvas.Clear(renderOptions.BackgroundColor);
                while (commentIndex >= 0 && frameHeight > -renderOptions.VerticalPadding)
                {
                    // Skip comments from ignored users
                    if (renderOptions.IgnoreUsersList.Contains(chatRoot.comments[commentIndex].commenter.name))
                    {
                        commentIndex--;
                        continue;
                    }

                    CommentSection comment = GenerateCommentSection(commentIndex);
                    if (comment != null)
                    {
                        commentList.Add(comment);

                        frameHeight -= comment.Image.Height + renderOptions.VerticalPadding;
                        frameCanvas.DrawBitmap(comment.Image, 0, frameHeight);

                        foreach (var emote in comment.Emotes)
                        {
                            //Only draw static emotes
                            if (emote.Item2.FrameCount == 1)
                            {
                                frameCanvas.DrawBitmap(emote.Item2.EmoteFrames[0], emote.Item1.X, emote.Item1.Y + frameHeight);
                            }
                        }
                    }
                    commentIndex--;
                }
            }

            return new UpdateFrame() { Image = newFrame, Comments = commentList };
        }

        private CommentSection GenerateCommentSection(int commentIndex)
        {
            CommentSection newSection = new CommentSection();
            List<(Point, TwitchEmote)> emoteSectionList = new List<(Point, TwitchEmote)>();
            Comment comment = chatRoot.comments[commentIndex];
            List<SKBitmap> sectionImages = new List<SKBitmap>();
            Point drawPos = new Point();
            Point defaultPos = new Point();
            bool ascentMessage = false;
            defaultPos.X = renderOptions.SidePadding;

            if (comment.source != "chat")
                return null;
            if (comment.message.user_notice_params != null && comment.message.user_notice_params.msg_id != null)
            {
                if (comment.message.user_notice_params.msg_id != "highlighted-message" && comment.message.user_notice_params.msg_id != "sub" && comment.message.user_notice_params.msg_id != "resub" && comment.message.user_notice_params.msg_id != "subgift" && comment.message.user_notice_params.msg_id != "")
                    return null;
                if (!renderOptions.SubMessages && (comment.message.user_notice_params.msg_id == "sub" || comment.message.user_notice_params.msg_id == "resub" || comment.message.user_notice_params.msg_id == "subgift"))
                    return null;
                if (comment.message.user_notice_params.msg_id == "highlighted-message" && comment.message.fragments == null && comment.message.body != null)
                {
                    comment.message.fragments = new List<Fragment>();
                    comment.message.fragments.Add(new Fragment());
                    comment.message.fragments[0].text = comment.message.body;
                }
            }
            if (comment.message.fragments == null || comment.commenter == null)
                return null;

            AddImageSection(sectionImages, ref drawPos, ref defaultPos);
            //Measure some sample text to determine position to draw text in, cannot assume height is font size
            SKRect textBounds = new SKRect();
            messageFont.MeasureText("abc123", ref textBounds);
            defaultPos.Y = (int)(((renderOptions.SectionHeight - textBounds.Height) / 2.0) + textBounds.Height);
            drawPos.Y = defaultPos.Y;

            if (comment.message.user_notice_params != null && comment.message.user_notice_params.msg_id != null && (comment.message.user_notice_params.msg_id == "sub" || comment.message.user_notice_params.msg_id == "resub" || comment.message.user_notice_params.msg_id == "subgift"))
            {
                ascentMessage = true;
                drawPos.X += (int)(24 * renderOptions.EmoteScale);
                DrawMessage(comment, sectionImages, emoteSectionList, ref drawPos, ref defaultPos);
            }
            else
            {
                if (renderOptions.Timestamp)
                    DrawTimestamp(comment, sectionImages, ref drawPos, ref defaultPos);
                if (renderOptions.ChatBadges)
                    DrawBadges(comment, sectionImages, ref drawPos, ref defaultPos);
                DrawUsername(comment, sectionImages, ref drawPos, ref defaultPos);
                DrawMessage(comment, sectionImages, emoteSectionList, ref drawPos, ref defaultPos);
            }

            SKBitmap finalBitmap = CombineImages(sectionImages, ascentMessage);
            newSection.Image = finalBitmap;
            newSection.Emotes = emoteSectionList;

            return newSection;
        }

        private SKBitmap CombineImages(List<SKBitmap> sectionImages, bool ascent)
        {
            SKBitmap finalBitmap = new SKBitmap(renderOptions.ChatWidth, sectionImages.Sum(x => x.Height));
            using (SKCanvas finalCanvas = new SKCanvas(finalBitmap))
            {
                for (int i = 0; i < sectionImages.Count; i++)
                {
                    finalCanvas.DrawBitmap(sectionImages[i], 0, i * renderOptions.SectionHeight);
                    sectionImages[i].Dispose();
                }

                if (ascent)
                    finalCanvas.DrawRect(renderOptions.SidePadding, 0, (float)(12 * renderOptions.EmoteScale), finalBitmap.Height, new SKPaint() { Color = SKColor.Parse("#7b2cf2") });
            }
            sectionImages.Clear();
            return finalBitmap;
        }

        private static string GetKeyName(IEnumerable<Codepoint> codepoints)
        {
            List<string> codepointList = new List<string>();
            foreach (Codepoint codepoint in codepoints)
            {
                if (codepoint.Value != 65039)
                {
                    codepointList.Add(codepoint.Value.ToString("X"));
                }
            }

            string emojiKey = String.Join(" ", codepointList);
            return emojiKey;
        }

        private void DrawMessage(Comment comment, List<SKBitmap> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, ref Point defaultPos)
        {
            int bitsCount = comment.message.bits_spent;
            foreach (var fragment in comment.message.fragments)
            {
                if (fragment.emoticon == null)
                {
                    //Either text or third party emote
                    string[] fragmentParts = SwapRTL(fragment.text.Split(' '));
                    for (int i = 0; i < fragmentParts.Length; i++)
                    {
                        string fragmentString = fragmentParts[i].Trim();

                        if (fragmentString == "" || fragmentString == "󠀀")
                            continue;

                        if (emoteThirdList.Any(x => x.Name == fragmentString))
                        {
                            TwitchEmote twitchEmote = emoteThirdList.First(x => x.Name == fragmentString);
                            Point emotePoint = new Point();
                            if (!twitchEmote.IsZeroWidth)
                            {
                                if (drawPos.X + twitchEmote.Width > renderOptions.ChatWidth - renderOptions.SidePadding)
                                    AddImageSection(sectionImages, ref drawPos, ref defaultPos);

                                emotePoint.X = drawPos.X;
                            }
                            else
                            {
                                emotePoint.X = drawPos.X - renderOptions.EmoteSpacing - twitchEmote.Width;
                            }
                            emotePoint.Y = (int)(sectionImages.Sum(x => x.Height) - renderOptions.SectionHeight - renderOptions.VerticalPadding + (renderOptions.SectionHeight - twitchEmote.Height / 2.0));
                            emotePositionList.Add((emotePoint, twitchEmote));
                            drawPos.X += twitchEmote.Width + renderOptions.EmoteSpacing;
                        }
                        else if (Regex.Match(fragmentString, emojiRegex).Success)
                        {
                            while (!String.IsNullOrWhiteSpace(fragmentString))
                            {
                                List<SingleEmoji> emojiMatches = Emoji.All.Where(x => fragmentString.StartsWith(x.ToString()) && fragmentString.Contains(x.Sequence.AsString.Trim('\uFE0F'))).ToList();

                                //Make sure the found emojis actually exist in our cache
                                for (int j = 0; j < emojiMatches.Count; j++)
                                {
                                    if (!emojiCache.ContainsKey(GetKeyName(emojiMatches[j].Sequence.Codepoints)))
                                    {
                                        emojiMatches.RemoveAt(j);
                                        j--;
                                    }
                                }

                                if (emojiMatches.Count > 0)
                                {
                                    SingleEmoji selectedEmoji = emojiMatches.OrderByDescending(x => x.Sequence.Codepoints.Count()).First();
                                    SKBitmap emojiImage = emojiCache[GetKeyName(selectedEmoji.Sequence.Codepoints)];

                                    if (drawPos.X + emojiImage.Width > renderOptions.ChatWidth - renderOptions.SidePadding)
                                        AddImageSection(sectionImages, ref drawPos, ref defaultPos);
                                    Point emotePoint = new Point();
                                    emotePoint.X = drawPos.X;
                                    emotePoint.Y = (int)(sectionImages.Sum(x => x.Height) - renderOptions.SectionHeight - renderOptions.VerticalPadding + (renderOptions.SectionHeight - emojiImage.Height / 2.0));
                                    using (SKCanvas canvas = new SKCanvas(sectionImages.Last()))
                                        canvas.DrawBitmap(emojiImage, emotePoint.X, emotePoint.Y);

                                    drawPos.X += emojiImage.Width + renderOptions.EmoteSpacing;

                                    fragmentString = fragmentString.Substring(selectedEmoji.Sequence.AsString.Trim('\uFE0F').Length);
                                }
                                else
                                {
                                    DrawText(fragmentString[0].ToString(), messageFont, false, sectionImages, ref drawPos, ref defaultPos);
                                    fragmentString = fragmentString.Substring(1);
                                }
                            }
                        }
                        else if (new StringInfo(fragmentString).LengthInTextElements < fragmentString.Length || !messageFont.ContainsGlyphs(fragmentString))
                        {
                            List<char> charList = new List<char>(fragmentString.ToArray());
                            //Very rough estimation of width of text, because we don't know the font yet. This is to show ASCII spam properly
                            int textWidth = (int)Math.Floor(charList.Count * messageFont.MeasureText("0"));
                            if (drawPos.X + textWidth > renderOptions.ChatWidth - renderOptions.SidePadding)
                                AddImageSection(sectionImages, ref drawPos, ref defaultPos);

                            //There are either surrogate pairs or characters not in the messageFont, draw one at a time
                            string messageBuffer = "";
                            for (int j = 0; j < charList.Count; j++)
                            {
                                if (char.IsHighSurrogate(charList[j]) && j + 1 < charList.Count && char.IsLowSurrogate(charList[j + 1]))
                                {
                                    if (messageBuffer != "")
                                        DrawText(messageBuffer, messageFont, true, sectionImages, ref drawPos, ref defaultPos);
                                    SKPaint fallbackFont = GetFallbackFont(char.ConvertToUtf32(charList[j], charList[j + 1]), renderOptions);
                                    fallbackFont.Color = renderOptions.MessageColor;
                                    DrawText(charList[j].ToString() + charList[j + 1].ToString(), messageFont, false, sectionImages, ref drawPos, ref defaultPos);
                                    messageBuffer = "";
                                    j++;
                                }
                                else if (new StringInfo(charList[j].ToString()).LengthInTextElements == 0 || !messageFont.ContainsGlyphs(charList[j].ToString()))
                                {
                                    if (messageBuffer != "")
                                        DrawText(messageBuffer, messageFont, true, sectionImages, ref drawPos, ref defaultPos);
                                    SKPaint fallbackFont = GetFallbackFont(charList[j], renderOptions);
                                    fallbackFont.Color = renderOptions.MessageColor;
                                    DrawText(messageBuffer, fallbackFont, true, sectionImages, ref drawPos, ref defaultPos);
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
                                if (bitsCount > 0 && fragmentString.Any(char.IsDigit) && fragmentString.Any(char.IsLetter))
                                {
                                    int bitsIndex = fragmentString.IndexOfAny("0123456789".ToCharArray());
                                    string outputPrefix = fragmentString.Substring(0, bitsIndex).ToLower();
                                    if (cheermotesList.Any(x => x.prefix.ToLower() == outputPrefix))
                                    {
                                        CheerEmote currentCheerEmote = cheermotesList.First(x => x.prefix.ToLower() == outputPrefix);
                                        int bitsAmount = Int32.Parse(fragmentString.Substring(bitsIndex));
                                        bitsCount -= bitsAmount;
                                        KeyValuePair<int, TwitchEmote> tierList = currentCheerEmote.getTier(bitsAmount);
                                        TwitchEmote twitchEmote = tierList.Value;
                                        if (drawPos.X + twitchEmote.Width > renderOptions.ChatWidth - renderOptions.SidePadding)
                                            AddImageSection(sectionImages, ref drawPos, ref defaultPos);
                                        Point emotePoint = new Point();
                                        emotePoint.X = drawPos.X;
                                        emotePoint.Y = (int)(sectionImages.Sum(x => x.Height) - renderOptions.SectionHeight - renderOptions.VerticalPadding + (renderOptions.SectionHeight - twitchEmote.Height / 2.0));
                                        emotePositionList.Add((emotePoint, twitchEmote));
                                        drawPos.X += twitchEmote.Width + renderOptions.EmoteSpacing;
                                        bitsPrinted = true;
                                    }
                                }
                            }
                            catch
                            { }
                            if (!bitsPrinted)
                                DrawText(fragmentString, messageFont, true, sectionImages, ref drawPos, ref defaultPos);
                        }
                    }
                }
                else
                {
                    //First party emote
                    string emoteId = fragment.emoticon.emoticon_id;
                    if (emoteList.Any(x => x.Id == emoteId))
                    {
                        TwitchEmote twitchEmote = emoteList.First(x => x.Id == emoteId);
                        if (drawPos.X + twitchEmote.Width > renderOptions.ChatWidth - renderOptions.SidePadding)
                            AddImageSection(sectionImages, ref drawPos, ref defaultPos);
                        Point emotePoint = new Point();
                        emotePoint.X = drawPos.X;
                        emotePoint.Y = (int)(sectionImages.Sum(x => x.Height) - renderOptions.SectionHeight - renderOptions.VerticalPadding + (renderOptions.SectionHeight - twitchEmote.Height / 2.0));
                        emotePositionList.Add((emotePoint, twitchEmote));
                        drawPos.X += twitchEmote.Width + renderOptions.EmoteSpacing;
                    }
                    else
                    {
                        //Probably an old emote that was removed
                        DrawText(fragment.text, messageFont, true, sectionImages, ref drawPos, ref defaultPos);
                    }
                }
            }
        }

        private void DrawText(string drawText, SKPaint textFont, bool padding, List<SKBitmap> sectionImages, ref Point drawPos, ref Point defaultPos)
        {
            float textWidth;
            bool isRtl = isRTL(drawText);
            try
            {
                if (isRtl)
                {
                    SKShaper messageShape = new SKShaper(textFont.Typeface);
                    SKShaper.Result measure = messageShape.Shape(drawText, textFont);
                    textWidth = measure.Points[measure.Points.Length - 1].X;
                }
                else
                {
                    textWidth = textFont.MeasureText(drawText);
                }
            }
            catch
            {
                return;
            }

            if (drawPos.X + textWidth > renderOptions.ChatWidth + renderOptions.SidePadding)
                AddImageSection(sectionImages, ref drawPos, ref defaultPos);

            using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImages.Last()))
            {
                if (renderOptions.Outline)
                {
                    //TODO: Fix outline for RTL
                    SKPath outlinePath = textFont.GetTextPath(drawText, drawPos.X, drawPos.Y);
                    SKPaint outlinePaint = new SKPaint() { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(renderOptions.OutlineSize * renderOptions.EmoteScale), StrokeJoin = SKStrokeJoin.Round, Color = SKColors.Black, IsAntialias = true, LcdRenderText = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
                    sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
                }

                try
                {
                    sectionImageCanvas.DrawShapedText(drawText, drawPos.X, drawPos.Y, textFont);
                }
                catch
                {
                    sectionImageCanvas.DrawText(drawText, drawPos.X, drawPos.Y, textFont);
                }
            }

            if (!isRtl)
            {
                drawPos.X += (int)Math.Floor(textWidth + (padding ? renderOptions.WordSpacing : 0));
            }
            else
            {
                drawPos.X += (int)Math.Floor(textWidth + (padding ? renderOptions.WordSpacing : 0));
            }
        }

        private void DrawUsername(Comment comment, List<SKBitmap> sectionImages, ref Point drawPos, ref Point defaultPos)
        {
            using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImages.Last()))
            {
                SKColor userColor = SKColor.Parse(comment.message.user_color != null ? comment.message.user_color : defaultColors[Math.Abs(comment.commenter.display_name.GetHashCode()) % defaultColors.Length]);
                userColor = GenerateUserColor(userColor, renderOptions.BackgroundColor, renderOptions);
                SKPaint userPaint = nameFont.Clone();
                userPaint.Color = userColor;

                if (comment.commenter.display_name.Any(isNotAscii))
                {
                    userPaint = GetFallbackFont((int)comment.commenter.display_name.Where(x => isNotAscii(x)).First(), renderOptions);
                    userPaint.Color = userColor;
                }

                int textWidth = (int)userPaint.MeasureText(comment.commenter.display_name + ":");
                if (renderOptions.Outline)
                {
                    SKPath outlinePath = userPaint.GetTextPath(comment.commenter.display_name + ":", drawPos.X, drawPos.Y);
                    sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
                }
                userPaint.Color = userColor;
                sectionImageCanvas.DrawText(comment.commenter.display_name + ":", drawPos.X, drawPos.Y, userPaint);
                drawPos.X += textWidth + renderOptions.WordSpacing;
            }
        }

        private SKColor GenerateUserColor(SKColor userColor, SKColor background_color, ChatRenderOptions renderOptions)
        {
            float backgroundHue, backgroundSaturation, backgroundBrightness;
            background_color.ToHsl(out backgroundHue, out backgroundSaturation, out backgroundBrightness);
            float userHue, userSaturation, userBrightness;
            userColor.ToHsl(out userHue, out userSaturation, out userBrightness);

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

        //For debugging, works on Windows only
        void OpenImage(SKBitmap newBitmap)
        {
            string tempFile = Path.GetFileNameWithoutExtension(Path.GetTempFileName()) + ".png";
            using (FileStream fs = new FileStream(tempFile, FileMode.Create))
                newBitmap.Encode(SKEncodedImageFormat.Png, 100).SaveTo(fs);

            Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
        }

        private void DrawBadges(Comment comment, List<SKBitmap> sectionImages, ref Point drawPos, ref Point defaultPos)
        {
            using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImages.Last()))
            {
                List<(SKBitmap, ChatBadgeType)> badgeImages = ParseCommentBadges(comment);
                foreach (var badge in badgeImages)
                {
                    //Don't render filtered out badges
                    if (((ChatBadgeType)renderOptions.ChatBadgeMask).HasFlag(badge.Item2))
                        continue;

                    float badgeY = (float)((renderOptions.SectionHeight - badge.Item1.Height) / 2.0);
                    sectionImageCanvas.DrawBitmap(badge.Item1, drawPos.X, badgeY);
                    drawPos.X += badge.Item1.Width + renderOptions.WordSpacing / 2;
                }
            }
        }

        private List<(SKBitmap, ChatBadgeType)> ParseCommentBadges(Comment comment)
        {
            List<(SKBitmap,ChatBadgeType)> returnList = new List<(SKBitmap, ChatBadgeType)>();

            if (comment.message.user_badges != null)
            {
                foreach (var badge in comment.message.user_badges)
                {
                    bool foundBadge = false;
                    string id = badge._id.ToString();
                    string version = badge.version.ToString();

                    foreach (var cachedBadge in badgeList)
                    {
                        if (cachedBadge.Name == id)
                        {
                            foreach (var cachedVersion in cachedBadge.Versions)
                            {
                                if (cachedVersion.Key == version)
                                {
                                    returnList.Add((cachedVersion.Value, cachedBadge.Type));
                                    foundBadge = true;
                                    break;
                                }
                            }

                            if (foundBadge)
                                break;
                        }
                    }
                }
            }

            return returnList;
        }

        private void DrawTimestamp(Comment comment, List<SKBitmap> sectionImages, ref Point drawPos, ref Point defaultPos)
        {
            using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImages.Last()))
            {
                TimeSpan timestamp = new TimeSpan(0, 0, (int)comment.content_offset_seconds);
                string timeString = timestamp.ToString(@"h\:mm\:ss");
                int textWidth = (int)messageFont.MeasureText(timeString);
                if (renderOptions.Outline)
                {
                    SKPath outlinePath = messageFont.GetTextPath(timeString, drawPos.X, drawPos.Y);
                    sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
                }
                sectionImageCanvas.DrawText(timeString, drawPos.X, drawPos.Y, messageFont);
                drawPos.X += textWidth + renderOptions.WordSpacing;
                defaultPos.X = drawPos.X;
            }
        }

        private void AddImageSection(List<SKBitmap> sectionImages, ref Point drawPos, ref Point defaultPos)
        {
            drawPos.X = defaultPos.X;
            drawPos.Y = defaultPos.Y;
            sectionImages.Add(new SKBitmap(renderOptions.ChatWidth, renderOptions.SectionHeight));
        }

        //Precompute scaled images so we don't have to scale every frame
        private void ScaleImages()
        {
            foreach (var emote in emoteList.Union(emoteThirdList))
            {
                double newScale = (2.0 / emote.ImageScale) * renderOptions.ReferenceScale * renderOptions.EmoteScale;
                if (newScale != 1.0)
                    emote.Resize(newScale);
            }

            foreach (var badge in badgeList)
            {
                //Assume badges are always 2x scale, not 1x or 4x
                if (renderOptions.ReferenceScale != 1.0)
                    badge.Resize(renderOptions.ReferenceScale);
            }

            foreach (var cheer in cheermotesList)
            {
                //Assume cheermotes are always 2x scale, not 1x or 4x
                if (renderOptions.ReferenceScale != 1.0)
                    cheer.Resize(renderOptions.ReferenceScale);
            }

            //Assume emojis are 4x (they're 72x72)
            double emojiScale = 0.5 * renderOptions.ReferenceScale;
            List<string> emojiKeys = new List<string>(emojiCache.Keys);
            foreach (var emojiKey in emojiKeys)
            {
                SKImageInfo imageInfo = new SKImageInfo((int)(emojiCache[emojiKey].Width * emojiScale), (int)(emojiCache[emojiKey].Height * emojiScale));
                SKBitmap newBitmap = new SKBitmap(imageInfo);
                emojiCache[emojiKey].ScalePixels(newBitmap, SKFilterQuality.High);
                emojiCache[emojiKey].Dispose();
                emojiCache[emojiKey] = newBitmap;
            }
        }

        private (int, int) GetTotalTicks()
        {
            if (renderOptions.StartOverride != -1 && renderOptions.EndOverride != -1)
            {
                int startSeconds = renderOptions.StartOverride;
                int videoStartTick = startSeconds * renderOptions.Framerate;
                int totalTicks = renderOptions.EndOverride * renderOptions.Framerate - videoStartTick;
                return (videoStartTick, totalTicks);
            }
            else if (chatRoot.video != null)
            {
                int startSeconds = (int)Math.Floor(chatRoot.video.start);
                int videoStartTick = startSeconds * renderOptions.Framerate;
                int totalTicks = (int)Math.Ceiling(chatRoot.video.end * renderOptions.Framerate) - videoStartTick;
                return (videoStartTick, totalTicks);
            }
            else
            {
                int videoStartTick = (int)Math.Floor(chatRoot.comments.First().content_offset_seconds * renderOptions.Framerate);
                int totalTicks = (int)Math.Ceiling(chatRoot.comments.Last().content_offset_seconds * renderOptions.Framerate) - videoStartTick;
                return (videoStartTick, totalTicks);
            }
        }
        public SKPaint GetFallbackFont(int input, ChatRenderOptions renderOptions)
        {
            if (fallbackCache.ContainsKey(input))
                return fallbackCache[input];

            SKPaint newPaint = new SKPaint() { Typeface = fontManager.MatchCharacter(input), LcdRenderText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, SubpixelText = true, IsAutohinted = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            fallbackCache.TryAdd(input, newPaint);
            return newPaint;
        }

        private static bool isNotAscii(char input)
        {
            return input > 127;
        }
        static string[] SwapRTL(string[] words)
        {
            List<string> finalWords = new List<string>();
            Stack<string> rtlStack = new Stack<string>();
            foreach (var word in words)
            {
                if (isRTL(word))
                {
                    rtlStack.Push(word);
                }
                else
                {
                    while (rtlStack.Count > 0)
                        finalWords.Add(rtlStack.Pop());
                    finalWords.Add(word);
                }
            }
            while (rtlStack.Count > 0)
                finalWords.Add(rtlStack.Pop());
            return finalWords.ToArray();
        }
        static bool isRTL(string message)
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
        public async Task<ChatRoot> ParseJson()
        {
            using (FileStream fs = new FileStream(renderOptions.InputFile, FileMode.Open, FileAccess.Read))
            {
                using (var jsonDocument = JsonDocument.Parse(fs))
                {
                    if (jsonDocument.RootElement.TryGetProperty("streamer", out JsonElement streamerJson))
                    {
                        chatRoot.streamer = streamerJson.Deserialize<Streamer>();
                    }
                    if (jsonDocument.RootElement.TryGetProperty("video", out JsonElement videoJson))
                    {
                        if (videoJson.TryGetProperty("start", out JsonElement videoStartJson) && videoJson.TryGetProperty("end", out JsonElement videoEndJson))
                        {
                            chatRoot.video = videoJson.Deserialize<VideoTime>();
                        }
                    }
                    if (jsonDocument.RootElement.TryGetProperty("emotes", out JsonElement emotesJson))
                    {
                        chatRoot.emotes = emotesJson.Deserialize<Emotes>();
                    }
                    if (jsonDocument.RootElement.TryGetProperty("comments", out JsonElement commentsJson))
                    {
                        chatRoot.comments = commentsJson.Deserialize<List<Comment>>();
                    }
                }
            }

            if (chatRoot.streamer == null)
            {
                chatRoot.streamer = new Streamer();
                chatRoot.streamer.id = int.Parse(chatRoot.comments.First().channel_id);
                chatRoot.streamer.name = await TwitchHelper.GetStreamerName(chatRoot.streamer.id);
            }

            return chatRoot;
        }
    }
}
