namespace AngleSharp.Io.Tests.Network
{
    using System;
    using System.IO;
    using AngleSharp.Io.Network;
    using FluentAssertions;
    using NUnit.Framework;

    [TestFixture]
    public class ResponseTests
    {
        [Test]
        public void DisposesContent()
        {
            // ARRANGE
            var stream = new DisposableStream();
            var response = new Response {Content = stream};

            // ACT
            response.Dispose();

            // ASSERT
            stream.Disposed.Should().BeTrue();
        }

        [Test]
        public void DisposesNothingWhenContentIsNull()
        {
            // ARRANGE
            var response = new Response {Content = null};

            // ACT
            Action action = () => response.Dispose();

            // ASSERT
            action.ShouldNotThrow();
        }
        class DisposableStream : Stream
        {
            public override Boolean CanRead
            {
                get;
            }

            public override Boolean CanSeek
            {
                get;
            }

            public override Boolean CanWrite
            {
                get;
            }

            public override Int64 Length
            {
                get;
            }

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
}