using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Gulp
{
    public static class Extenders
    {
        public static string ToBase32(this byte[] input)
        {
            if (input == null || input.Length == 0)
                throw new ArgumentNullException("input");

            int charCount = (int)Math.Ceiling(input.Length / 5d) * 8;

            var returnArray = new char[charCount];

            byte nextChar = 0;
            byte bitsRemaining = 5;
            int arrayIndex = 0;

            foreach (byte b in input)
            {
                nextChar = (byte)(nextChar | (b >> (8 - bitsRemaining)));

                returnArray[arrayIndex++] = ValueToChar(nextChar);

                if (bitsRemaining < 4)
                {
                    nextChar = (byte)((b >> (3 - bitsRemaining)) & 31);

                    returnArray[arrayIndex++] = ValueToChar(nextChar);

                    bitsRemaining += 5;
                }

                bitsRemaining -= 3;

                nextChar = (byte)((b << bitsRemaining) & 31);
            }

            if (arrayIndex != charCount)
            {
                returnArray[arrayIndex++] = ValueToChar(nextChar);

                while (arrayIndex != charCount)
                    returnArray[arrayIndex++] = '=';
            }

            return new string(returnArray);
        }

        private static char ValueToChar(byte value)
        {
            if (value < 26)
                return (char)(value + 65);

            if (value < 32)
                return (char)(value + 24);

            throw new ArgumentOutOfRangeException("value");
        }

        public static string ToUrlHash(this Uri uri)
        {
            var sha1 = new SHA1CryptoServiceProvider();

            return sha1.ComputeHash(Encoding.Default.GetBytes(uri.AbsoluteUri)).ToBase32();
        }

        public static async Task<HttpResponseMessage> GetResponse(this Uri uri)
        {
            const string USERAGENT =
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_8_2) AppleWebKit/537.17 (KHTML, like Gecko) Chrome/24.0.1309.0 Safari/537.17";

            var client = new HttpClient();

            var message = new HttpRequestMessage(HttpMethod.Get, uri);

            message.Headers.Add("User-Agent", USERAGENT);

            return await client.SendAsync(message);
        }

        public static void SetOnlyOnFaultedCompletion(
            this Task task, Action<AggregateException> onErrors)
        {
            task.ContinueWith(t => onErrors(t.Exception),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        public static bool IsLower(this string value)
        {
            for (int i = 0; i < value.Length; i++)
                if (char.IsUpper(value[i]))
                    return false;

            return true;
        }

        private static bool IsBaseUri(this Uri baseUri)
        {
            if (baseUri == null)
                return false;

            if (!baseUri.IsAbsoluteUri)
                return false;

            if (!baseUri.AbsoluteUri.IsLower())
                return false;

            return true;
        }

        public static bool IsLink(this string url,
            Uri baseUri, LinkKind kind, UriFilter filter = UriFilter.None)
        {
            Contract.Requires(baseUri.IsBaseUri());

            if (string.IsNullOrWhiteSpace(url))
                return false;

            url = url.Trim().ToLower();

            if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
                return false;

            if (url.StartsWith("#"))
                return false;

            Uri uri;

            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri))
                return false;

            if (!uri.IsAbsoluteUri)
            {
                if (!Uri.TryCreate(baseUri, uri, out uri))
                    return false;
            }

            if (uri.Scheme != "http")
                return false;

            switch (filter)
            {
                case UriFilter.LocalPath:
                    if (!baseUri.IsBaseOf(uri))
                        return false;
                    break;
                case UriFilter.Authority:
                    if (uri.Authority != baseUri.Authority)
                        return false;
                    break;
            }

            if (kind == LinkKind.HTML)
                return true;

            if (!string.IsNullOrWhiteSpace(uri.Query))
                return false;

            string localPath;

            try
            {
                localPath = Path.GetFileName(uri.LocalPath);
            }
            catch
            {
                return false;
            }

            switch (Path.GetExtension(localPath))
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                case ".tiff":
                    return true;
                default:
                    return false;
            }
        }
    }
}
