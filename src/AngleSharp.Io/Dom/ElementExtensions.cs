namespace AngleSharp.Io.Dom
{
    using AngleSharp.Dom;
    using AngleSharp.Html;
    using AngleSharp.Html.Dom;
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Extensions for DOM elements.
    /// </summary>
    public static class ElementExtensions
    {
        /// <summary>
        /// Appends a file to the input element.
        /// Requires the input element to be of type "file".
        /// </summary>
        /// <typeparam name="TElement">The type of element.</typeparam>
        /// <param name="input">The input to append to.</param>
        /// <param name="file">The file to append.</param>
        /// <returns>The input itself for chaining.</returns>
        public static TElement AppendFile<TElement>(this TElement input, InputFile file)
            where TElement : class, IHtmlInputElement
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
        /// <typeparam name="TElement">The type of element.</typeparam>
        /// <param name="input">The input to append to.</param>
        /// <param name="filePath">The path to the file, which should be appended.</param>
        /// <returns>The input itself for chaining.</returns>
        public static TElement AppendFile<TElement>(this TElement input, String filePath)
            where TElement : class, IHtmlInputElement
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

        /// <summary>
        /// Appends a file to the input element.
        /// Requires the input element to be of type "file".
        /// </summary>
        /// <typeparam name="TElement">The type of element.</typeparam>
        /// <param name="input">The input to append to.</param>
        /// <param name="fileName">The name to the file, which should be appended.</param>
        /// <param name="content">The content to the file, which should be appended.</param>
        /// <param name="mimeType">
        /// The MIME type of the file, which should be appended.
        /// If not given the default value is maps to an unknown binary (octet stream).
        /// </param>
        /// <returns>The input itself for chaining.</returns>
        public static TElement AppendFile<TElement>(this TElement input, String fileName, Stream content, String mimeType = null)
            where TElement : class, IHtmlInputElement
        {
            fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            content = content ?? throw new ArgumentNullException(nameof(content));
            var type = mimeType ?? MimeTypeNames.Binary;
            var file = new InputFile(fileName, type, content);
            return input.AppendFile(file);
        }

        /// <summary>
        /// Downloads the content from to the hyper reference given by the provided
        /// element.
        /// </summary>
        /// <typeparam name="TElement">The type of element.</typeparam>
        /// <param name="element">The element referencing the link to follow.</param>
        /// <param name="cancellationToken">The token to cancel the download.</param>
        /// <returns>The task eventually resulting in the response.</returns>
        public static Task<IResponse> DownloadAsync<TElement>(this TElement element, CancellationToken cancellationToken = default)
            where TElement : class, IUrlUtilities, IElement
        {
            var context = element?.Owner.Context ?? throw new InvalidOperationException("The element needs to be inside a browsing context.");
            var loader = context.GetService<IDocumentLoader>() ?? throw new InvalidOperationException("A document loader is required. Check your configuration.");
            var download = loader.FetchAsync(new DocumentRequest(new Url(element.Href)));
            cancellationToken.Register(download.Cancel);
            return download.Task;
        }
    }
}
