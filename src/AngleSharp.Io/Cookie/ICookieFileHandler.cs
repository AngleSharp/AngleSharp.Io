namespace AngleSharp.Io.Cookie
{
    using System;

    /// <summary>
    /// Represents a file handler.
    /// </summary>
    public interface ICookieFileHandler
    {
        /// <summary>
        /// Reads the (text) content from the file.
        /// </summary>
        /// <returns>The content of the file.</returns>
        String ReadFile();

        /// <summary>
        /// Writes the (text) content to the file.
        /// </summary>
        /// <param name="content">The content to write.</param>
        void WriteFile(String content);
    }
}
