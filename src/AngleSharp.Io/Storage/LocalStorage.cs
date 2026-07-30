namespace AngleSharp.Io.Storage
{
    using AngleSharp.Io.Cookie;
    using AngleSharp.Io.Dom;
    using System;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Represents a persistent local storage.
    /// </summary>
    public class LocalStorage : SessionStorage, ILocalStorage
    {
        private readonly ICookieFileHandler _handler;

        /// <summary>
        /// Creates a new local storage instance with sync against the given file path.
        /// </summary>
        /// <param name="syncFilePath">The file path used to sync storage state.</param>
        public LocalStorage(String syncFilePath)
            : this(new LocalFileHandler(syncFilePath))
        {
        }

        /// <summary>
        /// Creates a new local storage instance with the provided file handler.
        /// </summary>
        /// <param name="handler">The handler used to read and write storage data.</param>
        public LocalStorage(ICookieFileHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            ReadFromHandler();
        }

        /// <inheritdoc />
        public override String this[String key]
        {
            get => base[key];
            set
            {
                base[key] = value;
                WriteToHandler();
            }
        }

        /// <inheritdoc />
        public override void Remove(String key)
        {
            base.Remove(key);
            WriteToHandler();
        }

        /// <inheritdoc />
        public override void Clear()
        {
            base.Clear();
            WriteToHandler();
        }

        private void ReadFromHandler()
        {
            var content = _handler.ReadFile();

            if (String.IsNullOrEmpty(content))
            {
                return;
            }

            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var index = line.IndexOf('\t');

                if (index <= 0)
                {
                    continue;
                }

                var encodedKey = line.Substring(0, index);
                var encodedValue = line.Substring(index + 1);
                var key = Decode(encodedKey);
                var value = Decode(encodedValue);
                base[key] = value;
            }
        }

        private void WriteToHandler()
        {
            var sb = new StringBuilder();

            foreach (var key in Enumerable.Range(0, Length).Select(Key))
            {
                var value = base[key] ?? String.Empty;
                sb.Append(Encode(key));
                sb.Append('\t');
                sb.Append(Encode(value));
                sb.Append('\n');
            }

            _handler.WriteFile(sb.ToString());
        }

        private static String Encode(String value) => Uri.EscapeDataString(value ?? String.Empty);

        private static String Decode(String value) => Uri.UnescapeDataString(value ?? String.Empty);
    }
}
