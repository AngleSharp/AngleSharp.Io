namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the Clipboard API.
    /// </summary>
    [DomName("Clipboard")]
    public sealed class Clipboard
    {
        private readonly IClipboardPlatform _platform;

        /// <summary>
        /// Creates a new clipboard wrapper using the provided platform implementation.
        /// </summary>
        /// <param name="platform">The platform clipboard implementation.</param>
        public Clipboard(IClipboardPlatform platform)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        }

        /// <summary>
        /// Reads text from the clipboard.
        /// </summary>
        /// <returns>The clipboard text.</returns>
        [DomName("readText")]
        public Task<String> ReadText()
        {
            return _platform.ReadTextAsync();
        }

        /// <summary>
        /// Writes text to the clipboard.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        [DomName("writeText")]
        public Task WriteText(String text)
        {
            return _platform.WriteTextAsync(text ?? String.Empty);
        }
    }
}