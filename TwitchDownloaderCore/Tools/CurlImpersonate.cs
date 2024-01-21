using CurlThin.Enums;
using CurlThin;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TwitchDownloaderCore.Tools
{
    public static class CurlImpersonate
    {
        // Ideally, this class would be a singleton so we can call CurlNative.Cleanup() when shutting down.
        private static readonly CURLcode Global = CurlNative.Init();

        public static string GetCurlResponse(string url)
        {
            string response = Encoding.UTF8.GetString(GetCurlResponseBytes(url));
            return response;
        }

        public static byte[] GetCurlResponseBytes(string url)
        {
            using var ms = new MemoryStream();
            GetCurlResponse(url, ms);
            return ms.ToArray();
        }

        public static void GetCurlResponse(string url, Stream destination)
        {
            var easy = CurlNative.Easy.Init();

            try
            {
                CurlNative.Easy.SetOpt(easy, CURLoption.URL, url);
                CurlNative.Easy.SetOpt(easy, CURLoption.CAINFO, "curl-ca-bundle.crt");
                CurlNative.Easy.SetOpt(easy, CURLoption.TIMEOUT_MS, 30000);

                CurlNative.Easy.SetOpt(easy, CURLoption.WRITEFUNCTION, (data, size, nmemb, user) =>
                {
                    var length = (int)size * (int)nmemb;

                    unsafe
                    {
                        using var ums = new UnmanagedMemoryStream((byte*)data, length);
                        ums.CopyTo(destination);
                    }

                    return (UIntPtr)length;
                });

                var result = CurlNative.Easy.Perform(easy);
            }
            finally
            {
                // The author of CurlThin fixed a finalizer issue with a hack that resulted in SafeEasyHandles never actually cleaning themselves up, even when calling Dispose().
                // See https://github.com/stil/CurlThin/issues/15 for more details
                var handle = easy.DangerousGetHandle();
                if (handle != IntPtr.Zero)
                {
                    CurlNative.Easy.Cleanup(handle);
                    easy.SetHandleAsInvalid();
                    easy.Dispose();
                }
            }
        }
    }
}
