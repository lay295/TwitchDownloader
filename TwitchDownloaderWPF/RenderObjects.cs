using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SkiaSharp;

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
    public SKCodec codec;
    public string imageType;
    public string name;
    public string id;
    public int width;
    public int height;
    public int imageScale;

    public ThirdPartyEmote(SKBitmap Emote, SKCodec Codec, string Name, string ImageType, string Id, int ImageScale)
    {
        emote = Emote;
        codec = Codec;
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
    public SKColor message_color { get; set; }
    public int chat_height { get; set; }
    public int chat_width { get; set; }
    public bool bttv_emotes { get; set; }
    public bool ffz_emotes { get; set; }
    public bool outline { get; set; }
    public string font { get; set; }
    public double font_size { get; set; }
    public double image_scale { get; set; }
    public int update_frame { get; set; }
    public int text_height { get; set; }
    public int outline_size { get; set; }
    public bool chat_timestamp { get; set; }
    public int default_x { get; set; }
    public int framerate { get; set; }
    public string input_args { get; set; }
    public string output_args { get; set; }

    public RenderOptions(string Json_path, string Save_path, SKColor Background_color, int Chat_height, int Chat_width, bool Bttv_emotes, bool Ffz_emotes, bool Outline, string Font, double Font_size, double Update_rate, bool Chat_timestamp, SKColor Message_color, int Framerate, string Input_args, string Output_args)
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
        message_color = Message_color;
        framerate = Framerate;
        input_args = Input_args;
        output_args = Output_args;

        if (Update_rate == 0)
            update_frame = 1;
        else
            update_frame = (int)Math.Floor(Update_rate / (1.0 / Framerate));

        text_height = (int)Math.Floor(22 * image_scale);
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
    public SKCodec codec;
    public int frames;
    public List<int> durations;
    public int total_duration;
    public int imageScale;
    public int width;
    public int height;

    public GifEmote(Point Offset, string Name, SKCodec Codec, int ImageScale)
    {
        offset = Offset;
        name = Name;
        frames = Codec.FrameCount;
        codec = Codec;

        durations = new List<int>();
        for (int i = 0; i < frames; i++)
        {
            var duration = Codec.FrameInfo[i].Duration / 10;
            durations.Add(duration);
            total_duration += duration;
        }

        if (total_duration == 0 || total_duration == frames)
        {
            for (int i = 0; i < durations.Count; i++)
            {
                durations.RemoveAt(i);
                durations.Insert(i, 10);
            }
            total_duration = durations.Count * 10;
        }

        SKBitmap temp = GetFrame(0);
        width = temp.Width;
        height = temp.Height;
        temp.Dispose();

        imageScale = ImageScale;
    }

    public SKBitmap GetFrame(int frameNum)
    {
        SKImageInfo imageInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height);
        SKBitmap newBitmap = new SKBitmap(imageInfo);
        IntPtr pointer = newBitmap.GetPixels();
        SKCodecOptions codecOptions = new SKCodecOptions(frameNum);
        codec.GetPixels(imageInfo, pointer, codecOptions);
        return newBitmap;
    }
}

public class TwitchComment
{
    public string section;
    public double secondsOffset;
    public List<GifEmote> gifEmotes;
    public List<SKBitmap> normalEmotes;
    public List<SKRect> normalEmotesPositions;

    public TwitchComment(string Section, double SecondsOffset, List<GifEmote> GifEmotes, List<SKBitmap> NormalEmotes, List<SKRect> NormalEmotesPositions)
    {
        section = Section;
        secondsOffset = SecondsOffset;
        gifEmotes = GifEmotes;
        normalEmotes = NormalEmotes;
        normalEmotesPositions = NormalEmotesPositions;
    }
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