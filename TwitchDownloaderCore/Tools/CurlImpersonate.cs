using CurlThin.Enums;
using CurlThin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.Tools
{
    public static class CurlImpersonate
    {
        static CURLcode global = CurlNative.Init();
        public static string GetCurlReponse(string url)
        {
            
            var easy = CurlNative.Easy.Init();
            try
            {
                CurlNative.Easy.SetOpt(easy, CURLoption.URL, url);
                CurlNative.Easy.SetOpt(easy, CURLoption.CAINFO, "curl-ca-bundle.crt");
                CurlNative.Easy.SetOpt(easy, CURLoption.TIMEOUT_MS, 3000);

                var stream = new MemoryStream();
                CurlNative.Easy.SetOpt(easy, CURLoption.WRITEFUNCTION, (data, size, nmemb, user) =>
                {
                    var length = (int)size * (int)nmemb;
                    var buffer = new byte[length];
                    Marshal.Copy(data, buffer, 0, length);
                    stream.Write(buffer, 0, length);
                    return (UIntPtr)length;
                });

                var result = CurlNative.Easy.Perform(easy);
                string response = Encoding.UTF8.GetString(stream.ToArray());
                return response;
            }
            finally
            {
                easy.Dispose();
            }
        }
    }
}
