namespace AngleSharp.Io.Cookie
{
    using System;
    using System.IO;

    /// <summary>
    /// Represents a file handler against the local
    /// file system.
    /// </summary>
    public class LocalFileHandler : ICookieFileHandler
    {
        private readonly String _filePath;

        /// <summary>
        /// Creates a new local file handler for the given path.
        /// </summary>
        /// <param name="filePath">The path to resolve to.</param>
        public LocalFileHandler(String filePath) => _filePath = filePath;

        String ICookieFileHandler.ReadFile() => File.Exists(_filePath) ?
            File.ReadAllText(_filePath) : String.Empty;

        void ICookieFileHandler.WriteFile(String content)
        {
            //TODO Replace with queued async method
            File.WriteAllText(_filePath, content);
        }
    }
}
