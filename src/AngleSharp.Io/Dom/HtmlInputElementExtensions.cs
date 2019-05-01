namespace AngleSharp.Io.Dom
{
    using AngleSharp.Html;
    using AngleSharp.Html.Dom;
    using System;
    using System.IO;

    /// <summary>
    /// Extensions for the HTML Input element.
    /// </summary>
    public static class HtmlInputElementExtensions
    {
        /// <summary>
        /// Appends a file to the input element.
        /// Requires the input element to be of type "file".
        /// </summary>
        /// <param name="input">The input to append to.</param>
        /// <param name="file">The file to append.</param>
        /// <returns>The input itself for chaining.</returns>
        public static IHtmlInputElement AppendFile(this IHtmlInputElement input, InputFile file)
        {
            input = input ?? throw new ArgumentNullException(nameof(input));

            if (input.Type == InputTypeNames.File)
            {
                input.Files.Add(file ?? throw new ArgumentNullException(nameof(file)));
            }

            return input;
        }

        /// <summary>
        /// Appends a file to the input element.
        /// Requires the input element to be of type "file".
        /// </summary>
        /// <param name="input">The input to append to.</param>
        /// <param name="filePath">The path to the file, which should be appended.</param>
        /// <returns>The input itself for chaining.</returns>
        public static IHtmlInputElement AppendFile(this IHtmlInputElement input, String filePath)
        {
            filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            var name = Path.GetFileName(filePath);
            var ext = Path.GetExtension(filePath);
            var type = MimeTypeNames.FromExtension(ext);
            var stream = File.OpenRead(filePath);
            var modified = File.GetLastWriteTimeUtc(filePath);
            var file = new InputFile(name, type, stream, modified);
            return input.AppendFile(file);
        }
    }
}
