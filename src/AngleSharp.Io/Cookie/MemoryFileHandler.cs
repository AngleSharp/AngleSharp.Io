namespace AngleSharp.Io.Cookie
{
    using System;

    /// <summary>
    /// A memory file handler to feed the cookie provider.
    /// Ideal for testing and sandboxed ("private") browsing.
    /// </summary>
    public class MemoryFileHandler : IFileHandler
    {
        private String _content;

        /// <summary>
        /// Creates a new memory file handler.
        /// If no initial content is provided the handler starts empty.
        /// </summary>
        /// <param name="initialContent">The optional initial content.</param>
        public MemoryFileHandler(String initialContent = "") => _content = initialContent;

        String IFileHandler.ReadFile() => _content;

        void IFileHandler.WriteFile(String content) => _content = content;
    }
}
