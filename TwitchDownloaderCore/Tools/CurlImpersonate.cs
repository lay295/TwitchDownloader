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
        static CURLcode global = CurlNative.Init();

        public static string GetCurlResponse(string url)
        {
            string response = Encoding.UTF8.GetString(GetCurlResponseBytes(url));
            return response;
        }

        public static byte[] GetCurlResponseBytes(string url)
        {
            var easy = CurlNative.Easy.Init();
            try
            {
                CurlNative.Easy.SetOpt(easy, CURLoption.URL, url);
                CurlNative.Easy.SetOpt(easy, CURLoption.CAINFO, "curl-ca-bundle.crt");
                CurlNative.Easy.SetOpt(easy, CURLoption.TIMEOUT_MS, 30000);

                var stream = new MemoryStream();
                CurlNative.Easy.SetOpt(easy, CURLoption.WRITEFUNCTION, (data, size, nmemb, user) =>
                {
                    var length = (int)size * (int)nmemb;

                    var buffer = ArrayPool<byte>.Shared.Rent(length);
                    try
                    {
                        Marshal.Copy(data, buffer, 0, length);
                        stream.Write(buffer, 0, length);
                    }
                    finally
                    {
                        Array.Clear(buffer); // Clear the buffer in case we were working with sensitive information.
                        ArrayPool<byte>.Shared.Return(buffer);
                    }

                    return (UIntPtr)length;
                });

                var result = CurlNative.Easy.Perform(easy);
                return stream.ToArray();
            }
            finally
            {
                easy.Dispose();
            }
        }
    }
}
