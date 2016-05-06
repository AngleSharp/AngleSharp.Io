namespace AngleSharp.Io.Tests.Network
{
    using AngleSharp.Network.Default;
    using FluentAssertions;
    using NUnit.Framework;
    using System;
    using System.IO;
    using System.Net;

    [TestFixture]
    public class ResponseTests
    {
        [Test]
        public void Initialize()
        {
            // ARRANGE, ACT
            var response = new Response();

            // ASSERT
            response.Content.Should().BeNull();
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            response.Headers.Should().NotBeNull();
            response.Headers.Should().BeEmpty();
            response.Address.Should().BeNull();
        }

        class DisposableStream : Stream
        {
            public override Boolean CanRead
            {
                get { return false; }
            }

            public override Boolean CanSeek
            {
                get { return false; }
            }

            public override Boolean CanWrite
            {
                get { return false; }
            }

            public override Int64 Length
            {
                get { return 0; }
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