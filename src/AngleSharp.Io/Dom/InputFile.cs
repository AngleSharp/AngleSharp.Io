namespace AngleSharp.Io.Dom
{
    using System;
    using System.IO;

    /// <summary>
    /// Represents an input file
    /// </summary>
    public class InputFile : IFile
    {
        private readonly String _fileName;
        private readonly Stream _content;
        private readonly String _type;
        private readonly DateTime _modified;

        /// <summary>
        /// Creates a new input file.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <param name="type">The MIME type of the file.</param>
        /// <param name="content">The content stream.</param>
        /// <param name="modified">The last modified date.</param>
        public InputFile(String fileName, String type, Stream content, DateTime modified)
        {
            _fileName = fileName;
            _type = type;
            _content = content;
            _modified = modified;
        }

        /// <summary>
        /// Creates a new input file.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <param name="type">The MIME type of the file.</param>
        /// <param name="content">The content stream.</param>
        public InputFile(String fileName, String type, Stream content)
            : this(fileName, type, content, DateTime.Now)
        {
        }

        /// <summary>
        /// Creates a new input file.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <param name="type">The MIME type of the file.</param>
        /// <param name="content">The content stream.</param>
        public InputFile(String fileName, String type, Byte[] content)
            : this(fileName, type, new MemoryStream(content), DateTime.Now)
        {
        }

        /// <summary>
        /// Gets the content stream.
        /// </summary>
        public Stream Body => _content;

        /// <summary>
        /// Gets if the input stream is closed.
        /// </summary>
        public Boolean IsClosed => _content.CanRead == false;

        /// <summary>
        /// Gets the last modified date.
        /// </summary>
        public DateTime LastModified => _modified;

        /// <summary>
        /// Gets the length of the content stream.
        /// </summary>
        public Int32 Length => (Int32)_content.Length;

        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        public String Name => _fileName;

        /// <summary>
        /// Gets the MIME type of the file.
        /// </summary>
        public String Type => _type;

        /// <summary>
        /// Closes the content stream.
        /// </summary>
        public void Close() => _content.Close();

        /// <summary>
        /// Disposes the content stream.
        /// </summary>
        void IDisposable.Dispose() => _content.Dispose();

        /// <inheritdoc />
        public IBlob Slice(Int32 start = 0, Int32 end = Int32.MaxValue, String contentType = null)
        {
            var ms = new MemoryStream();
            _content.Position = start;
            var length = Math.Max(0, (Int32)Math.Min((Int64)end, _content.Length) - start);
            var buffer = new Byte[length];
            var read = 0;

            while (read < length)
            {
                var count = _content.Read(buffer, read, length - read);

                if (count == 0)
                {
                    break;
                }

                read += count;
            }

            ms.Write(buffer, 0, read);
            ms.Position = 0;
            _content.Position = 0;
            return new InputFile(_fileName, _type, ms);
        }
    }
}
