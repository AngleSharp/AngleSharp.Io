namespace AngleSharp.Io.Tests
{
    using System;
    using System.IO;

    sealed class DisposableStream : Stream
    {
        public override Boolean CanRead => false;

        public override Boolean CanSeek => false;

        public override Boolean CanWrite => false;

        public override Int64 Length => 0;

        public override Int64 Position
        {
            get;
            set;
        }

        public Boolean Disposed
        {
            get;
            set;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override Int64 Seek(Int64 offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(Int64 value)
        {
            throw new NotImplementedException();
        }

        public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
        {
            throw new NotImplementedException();
        }

        public override void Write(Byte[] buffer, Int32 offset, Int32 count)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(Boolean disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
