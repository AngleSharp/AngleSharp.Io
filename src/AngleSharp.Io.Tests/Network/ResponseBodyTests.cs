namespace AngleSharp.Io.Tests.Network
{
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class ResponseBodyTests
    {
        [Test]
        public async Task BodyDeliversTheContentInChunksAsTheyArrive()
        {
            var content = new ChunkedStream("first ", "second ", "third");
            var response = new Response(CreateResponse(content));
            var reader = response.Body.GetReader();
            var text = new StringBuilder();
            var chunks = 0;

            while (true)
            {
                var result = await reader.Read().ConfigureAwait(false);

                if (result.Done)
                {
                    break;
                }

                var segment = (ArraySegment<Byte>)result.Value;
                text.Append(Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count));
                chunks++;
            }

            Assert.AreEqual("first second third", text.ToString());
            Assert.AreEqual(3, chunks);
        }

        [Test]
        public async Task BodyIsTheSameStreamEveryTime()
        {
            var response = new Response(CreateResponse("data"));

            Assert.AreSame(response.Body, response.Body);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        [Test]
        public async Task ReadingTheBodySetsBodyUsed()
        {
            var response = new Response(CreateResponse("data"));

            Assert.IsFalse(response.IsBodyUsed);
            var reader = response.Body.GetReader();
            Assert.IsFalse(response.IsBodyUsed);

            await reader.Read().ConfigureAwait(false);

            Assert.IsTrue(response.IsBodyUsed);
        }

        [Test]
        public async Task TextAfterReadingTheBodyThrows()
        {
            var response = new Response(CreateResponse("data"));
            var reader = response.Body.GetReader();
            await reader.Read().ConfigureAwait(false);

            Assert.ThrowsAsync<InvalidOperationException>(async () => await response.Text().ConfigureAwait(false));
        }

        [Test]
        public void TextOnALockedBodyThrows()
        {
            var response = new Response(CreateResponse("data"));
            response.Body.GetReader();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await response.Text().ConfigureAwait(false));
        }

        [Test]
        public async Task CancelingTheBodyAbortsTheDownloadStream()
        {
            var content = new ChunkedStream("first ", "second ", "third");
            var response = new Response(CreateResponse(content));
            var reader = response.Body.GetReader();

            await reader.Read().ConfigureAwait(false);
            await reader.Cancel("enough").ConfigureAwait(false);

            Assert.IsTrue(content.Disposed);
            Assert.IsTrue(response.IsBodyUsed);
        }

        [Test]
        public async Task CloneOfAStreamedResponseLetsBothReadTheBody()
        {
            var content = new ChunkedStream("first ", "second ", "third");
            var response = new Response(CreateResponse(content));
            var clone = response.Clone();

            var first = await response.Text().ConfigureAwait(false);
            var second = await ReadBody(clone.Body).ConfigureAwait(false);

            Assert.AreEqual("first second third", first);
            Assert.AreEqual("first second third", second);
        }

        [Test]
        public async Task ArrayBufferGivesTheBytes()
        {
            var response = new Response(CreateResponse("data"));
            var bytes = await response.ArrayBuffer().ConfigureAwait(false);

            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("data"), bytes);
            Assert.IsTrue(response.IsBodyUsed);
        }

        [Test]
        public async Task BlobCarriesTheContentType()
        {
            var raw = CreateResponse("data");
            raw.Headers["Content-Type"] = "text/plain";
            var response = new Response(raw);
            var blob = await response.Blob().ConfigureAwait(false);

            Assert.AreEqual("text/plain", blob.Type);
            Assert.AreEqual("data", await blob.Text().ConfigureAwait(false));
        }

        [Test]
        public async Task ScriptCreatedResponseTakesAStreamAsBody()
        {
            var stream = new ReadableStream(new UnderlyingSource
            {
                Start = controller =>
                {
                    controller.Enqueue(new ArraySegment<Byte>(Encoding.UTF8.GetBytes("streamed ")));
                    controller.Enqueue(new ArraySegment<Byte>(Encoding.UTF8.GetBytes("body")));
                    controller.Close();
                    return Task.CompletedTask;
                },
            });

            var response = new Response(stream);

            Assert.AreEqual("streamed body", await response.Text().ConfigureAwait(false));
        }

        [Test]
        public async Task RequestBodyIsAStreamOverTheSerializedBody()
        {
            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Address("https://example.test/").Content("<!doctype html>")).ConfigureAwait(false);
            var request = new FetchRequest(document.DefaultView, "https://example.test/api", new FetchOptions
            {
                Method = "POST",
                Body = "payload",
            });

            Assert.IsFalse(request.IsBodyUsed);
            var body = await ReadBody(request.Body).ConfigureAwait(false);

            Assert.AreEqual("payload", body);
            Assert.IsTrue(request.IsBodyUsed);
        }

        [Test]
        public async Task RequestWithoutBodyHasANullBodyStream()
        {
            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Address("https://example.test/").Content("<!doctype html>")).ConfigureAwait(false);
            var request = new FetchRequest(document.DefaultView, "https://example.test/api");

            Assert.IsNull(request.Body);
        }

        private static async Task<String> ReadBody(ReadableStream stream)
        {
            var reader = stream.GetReader();
            var text = new StringBuilder();

            while (true)
            {
                var result = await reader.Read().ConfigureAwait(false);

                if (result.Done)
                {
                    return text.ToString();
                }

                var segment = (ArraySegment<Byte>)result.Value;
                text.Append(Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count));
            }
        }

        private static DefaultResponse CreateResponse(String body) =>
            CreateResponse(new MemoryStream(Encoding.UTF8.GetBytes(body)));

        private static DefaultResponse CreateResponse(Stream content) => new DefaultResponse
        {
            Address = Url.Create("https://api.example.test/data"),
            StatusCode = HttpStatusCode.OK,
            Headers = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase),
            Content = content,
        };

        /// <summary>
        /// Delivers one chunk per read, as a connection would.
        /// </summary>
        private sealed class ChunkedStream : Stream
        {
            private readonly Queue<Byte[]> _chunks;

            public ChunkedStream(params String[] chunks)
            {
                _chunks = new Queue<Byte[]>();

                foreach (var chunk in chunks)
                {
                    _chunks.Enqueue(Encoding.UTF8.GetBytes(chunk));
                }
            }

            public Boolean Disposed { get; private set; }

            public override Boolean CanRead => true;

            public override Boolean CanSeek => false;

            public override Boolean CanWrite => false;

            public override Int64 Length => throw new NotSupportedException();

            public override Int64 Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
            {
                if (_chunks.Count == 0)
                {
                    return 0;
                }

                var chunk = _chunks.Dequeue();
                Array.Copy(chunk, 0, buffer, offset, chunk.Length);
                return chunk.Length;
            }

            public override Int64 Seek(Int64 offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(Int64 value) => throw new NotSupportedException();

            public override void Write(Byte[] buffer, Int32 offset, Int32 count) => throw new NotSupportedException();

            protected override void Dispose(Boolean disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
