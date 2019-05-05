namespace AngleSharp.Io.Network
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// A set of useful extension methods for an IResponse.
    /// </summary>
    public static class ResponseExtensions
    {
        /// <summary>
        /// Saves the given response to the given file path.
        /// Disposes the response after saving has finished.
        /// </summary>
        /// <param name="response">The response to use.</param>
        /// <param name="filePath">The path where the response should be saved.</param>
        /// <returns>The task storing the file.</returns>
        public static async Task SaveToAsync(this IResponse response, String filePath)
        {
            using (var target = File.OpenWrite(filePath))
            {
                await response.CopyToAsync(target).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Copies the given response to the provided stream.
        /// Disposes the response after saving has finished.
        /// </summary>
        /// <param name="response">The response to use.</param>
        /// <param name="stream">The stream where the response should be copied to.</param>
        /// <returns>The task copying to the stream.</returns>
        public static async Task CopyToAsync(this IResponse response, Stream stream)
        {
            using (response)
            {
                await response.Content.CopyToAsync(stream).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Determines if the given response is provided as an attachment.
        /// </summary>
        /// <param name="response">The response to extend.</param>
        /// <returns>True if the content-disposition is attachment, otherwise false.</returns>
        public static Boolean IsAttachment(this IResponse response) =>
            response.Headers.TryGetValue(HeaderNames.ContentDisposition, out var disposition) &&
            disposition.StartsWith("attachment", StringComparison.InvariantCultureIgnoreCase);

        /// <summary>
        /// Gets the filename of the content-disposition header or
        /// alternatively via a path analysis together with the MIME type.
        /// </summary>
        /// <param name="response">The response to extend.</param>
        /// <returns>The determined file name.</returns>
        public static String GetAttachedFileName(this IResponse response)
        {
            var dispositionFileName = default(String);

            if (response.Headers.TryGetValue(HeaderNames.ContentDisposition, out var disposition))
            {
                dispositionFileName = GetFileNameFromDisposition(disposition);
            }

            var filename = dispositionFileName ?? response.Address.Path.Split('/').LastOrDefault() ?? "_";
            var standardExtension = Path.GetExtension(filename);

            if (String.IsNullOrEmpty(standardExtension))
            {
                var type = response.GetContentType(MimeTypeNames.Binary).Content;
                var extension = MimeTypeNames.GetExtension(type);
                return filename + extension;
            }


            return filename;
        }

        private static String GetFileNameFromDisposition(String value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                var head = "filename=\"";
                var start = value.IndexOf(head) + head.Length;

                if (start >= head.Length)
                {
                    var end = value.IndexOf("\"", start);

                    if (end == -1)
                    {
                        end = value.Length;
                    }

                    return value.Substring(start, end - start);
                }
            }

            return null;
        }
    }
}
