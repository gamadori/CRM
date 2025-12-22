using System;
using System.IO;
using System.Threading.Tasks;

namespace CRM.Shared.Extensions
{
    public static class StreamExtensions
    {
        /// <summary>
        /// Allows to convert a stream to a base64 encoded string
        /// </summary>
        /// <param name="stream">The stream to convert</param>
        /// <returns>Base64 encoded string</returns>
        public static string ToBase64String(this Stream stream)
        {
            if (stream is MemoryStream memoryStream)
            {
                return Convert.ToBase64String(memoryStream.ToArray());
            }

            var bytes = new Byte[(int)stream.Length];

            stream.ReadAsync(bytes, 0, (int)stream.Length);

            return Convert.ToBase64String(bytes);
        }

        public static async Task<MemoryStream> ToMemoryStreamAsync(this Stream stream)
        {
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }

      
        public static async Task<byte[]> CopyToArrayAsync(this Stream input)
        {
           
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    await input.CopyToAsync(memoryStream);
                    return memoryStream.ToArray();
                }
            
        }
    }
}
