namespace AngleSharp.Io.Dom
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the platform clipboard implementation used by the DOM clipboard wrapper.
    /// </summary>
    public interface IClipboardPlatform
    {
        /// <summary>
        /// Reads text from the platform clipboard.
        /// </summary>
        /// <returns>The clipboard text.</returns>
        Task<String> ReadTextAsync();

        /// <summary>
        /// Writes text to the platform clipboard.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        Task WriteTextAsync(String text);
    }
}