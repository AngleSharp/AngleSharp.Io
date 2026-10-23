namespace AngleSharp.Io.Http
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A read-only stream replaying already consumed bytes before continuing
    /// with the remainder of the original stream.
    /// </summary>
    internal sealed class PrefixedStream : Stream
    {
        private readonly Byte[] _prefix;
        private readonly Stream _remainder;
        private Int32 _position;

        public PrefixedStream(Byte[] prefix, Stream remainder)
        {
            _prefix = prefix;
            _remainder = remainder;
        }

        public override Boolean CanRead => true;

        public override Boolean CanSeek => false;

        public override Boolean CanWrite => false;

        public override Int64 Length => throw new NotSupportedException();

        public override Int64 Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
        {
            if (_position < _prefix.Length)
            {
                return ReadPrefix(buffer, offset, count);
            }

            return _remainder.Read(buffer, offset, count);
        }

        public override Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
        {
            if (_position < _prefix.Length)
            {
                return Task.FromResult(ReadPrefix(buffer, offset, count));
            }

            return _remainder.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override void Flush()
        {
        }

        public override Int64 Seek(Int64 offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(Int64 value) => throw new NotSupportedException();

        public override void Write(Byte[] buffer, Int32 offset, Int32 count) => throw new NotSupportedException();

        protected override void Dispose(Boolean disposing)
        {
            if (disposing)
            {
                _remainder.Dispose();
            }

            base.Dispose(disposing);
        }

        private Int32 ReadPrefix(Byte[] buffer, Int32 offset, Int32 count)
        {
            var length = Math.Min(count, _prefix.Length - _position);
            Buffer.BlockCopy(_prefix, _position, buffer, offset, length);
            _position += length;
            return length;
        }
    }
}
