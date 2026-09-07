namespace TwitchDownloaderAvalonia.Models
{
    public sealed class RenderCodec
    {
        public required string Name { get; init; }
        public required string InputArgs { get; init; }
        public required string OutputArgs { get; init; }

        public override string ToString() => Name;
    }

    public sealed class RenderContainer
    {
        public required string Name { get; init; }
        public required IReadOnlyList<RenderCodec> Codecs { get; init; }

        public override string ToString() => Name;
    }

    public sealed class CustomFfmpegArgs
    {
        public string CodecName { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
        public string InputArgs { get; set; } = string.Empty;
        public string OutputArgs { get; set; } = string.Empty;
    }

    public static class RenderEncodingPresets
    {
        private const string RAW_INPUT =
            "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -";

        public static IReadOnlyList<RenderContainer> CreateContainers()
        {
            var h264 = Codec("H264", "-c:v libx264 -preset:v veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"");
            var h264Nvenc = Codec("H264 NVIDIA", "-c:v h264_nvenc -preset:v p4 -cq 20 -pix_fmt yuv420p \"{save_path}\"");
            var h264Amf = Codec("H264 AMD", "-c:v h264_amf -preset:v p4 -cq 20 -pix_fmt yuv420p \"{save_path}\"");
            var h265 = Codec("H265", "-c:v libx265 -preset:v veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"");
            var h265Nvenc = Codec("H265 NVIDIA", "-c:v hevc_nvenc -preset:v p4 -cq 21 -pix_fmt yuv420p \"{save_path}\"");
            var h265Amf = Codec("H265 AMD", "-c:v hevc_amf -preset:v p4 -cq 21 -pix_fmt yuv420p \"{save_path}\"");
            var vp8 = Codec("VP8", "-c:v libvpx -crf 18 -b:v 2M -pix_fmt yuva420p -auto-alt-ref 0 \"{save_path}\"");
            var vp9 = Codec("VP9", "-c:v libvpx-vp9 -crf 18 -b:v 2M -deadline realtime -quality realtime -speed 3 -pix_fmt yuva420p \"{save_path}\"");
            var rle = Codec("RLE", "-c:v qtrle -pix_fmt argb \"{save_path}\"");
            var prores = Codec("ProRes", "-c:v prores_ks -qscale:v 62 -pix_fmt argb \"{save_path}\"");

            return
            [
                new RenderContainer { Name = "MP4", Codecs = [h264, h265, h264Nvenc, h265Nvenc, h264Amf, h265Amf] },
                new RenderContainer { Name = "MOV", Codecs = [h264, h265, rle, prores, h264Nvenc, h265Nvenc, h264Amf, h265Amf] },
                new RenderContainer { Name = "WEBM", Codecs = [vp8, vp9] },
                new RenderContainer { Name = "MKV", Codecs = [h264, h265, vp8, vp9, h264Nvenc, h265Nvenc, h264Amf, h265Amf] },
            ];
        }

        private static RenderCodec Codec(string name, string outputArgs) => new()
        {
            Name = name,
            InputArgs = RAW_INPUT,
            OutputArgs = outputArgs,
        };
    }
}
