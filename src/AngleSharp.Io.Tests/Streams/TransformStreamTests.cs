namespace AngleSharp.Io.Tests.Streams
{
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    [TestFixture]
    public class TransformStreamTests
    {
        [Test]
        public async Task DefaultTransformPassesChunksThrough()
        {
            var transform = new TransformStream();
            var writer = transform.Writable.GetWriter();
            var reader = transform.Readable.GetReader();

            var write = writer.Write("x");
            var result = await reader.Read().ConfigureAwait(false);
            await write.ConfigureAwait(false);

            Assert.AreEqual("x", result.Value);

            _ = writer.Close();
            var done = await reader.Read().ConfigureAwait(false);
            Assert.IsTrue(done.Done);
        }

        [Test]
        public async Task TransformChangesTheChunks()
        {
            var transform = new TransformStream(new Transformer
            {
                Transform = (chunk, controller) =>
                {
                    controller.Enqueue(((String)chunk).ToUpperInvariant());
                    return Task.CompletedTask;
                },
            });

            var writer = transform.Writable.GetWriter();
            var reader = transform.Readable.GetReader();

            _ = writer.Write("hi");
            var result = await reader.Read().ConfigureAwait(false);

            Assert.AreEqual("HI", result.Value);
        }

        [Test]
        public async Task FlushCanEnqueueTheRest()
        {
            var transform = new TransformStream(new Transformer
            {
                Transform = (chunk, controller) => Task.CompletedTask,
                Flush = controller =>
                {
                    controller.Enqueue("rest");
                    return Task.CompletedTask;
                },
            });

            var writer = transform.Writable.GetWriter();
            var reader = transform.Readable.GetReader();

            _ = writer.Write("swallowed");
            _ = writer.Close();

            var result = await reader.Read().ConfigureAwait(false);
            var done = await reader.Read().ConfigureAwait(false);

            Assert.AreEqual("rest", result.Value);
            Assert.IsTrue(done.Done);
        }

        [Test]
        public async Task ErrorFailsBothSides()
        {
            var transform = new TransformStream(new Transformer
            {
                Transform = (chunk, controller) =>
                {
                    controller.Error(new InvalidOperationException("broken"));
                    return Task.CompletedTask;
                },
            });

            var writer = transform.Writable.GetWriter();
            var reader = transform.Readable.GetReader();

            _ = ReadSoon(reader);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await writer.Write("x").ConfigureAwait(false));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await reader.Read().ConfigureAwait(false));
            await Task.CompletedTask.ConfigureAwait(false);
        }

        [Test]
        public async Task TerminateClosesTheReadableAndFailsTheWritable()
        {
            var transform = new TransformStream(new Transformer
            {
                Transform = (chunk, controller) =>
                {
                    controller.Terminate();
                    return Task.CompletedTask;
                },
            });

            var writer = transform.Writable.GetWriter();
            var reader = transform.Readable.GetReader();

            var read = reader.Read();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await writer.Write("x").ConfigureAwait(false));
            var result = await read.ConfigureAwait(false);

            Assert.IsTrue(result.Done);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await writer.Write("y").ConfigureAwait(false));
        }

        [Test]
        public async Task TextEncoderAndDecoderStreamsRoundTrip()
        {
            var encoder = new TextEncoderStream();
            var decoder = new TextDecoderStream();

            Assert.AreEqual("utf-8", encoder.Encoding);
            Assert.AreEqual("utf-8", decoder.Encoding);

            var writer = encoder.Writable.GetWriter();
            var reader = encoder.Readable.PipeThrough(new ReadableWritablePair
            {
                Readable = decoder.Readable,
                Writable = decoder.Writable,
            }).GetReader();

            var read = ReadAllText(reader);
            await writer.Write("héllo ").ConfigureAwait(false);
            await writer.Write("wörld").ConfigureAwait(false);
            await writer.Close().ConfigureAwait(false);

            Assert.AreEqual("héllo wörld", await read.ConfigureAwait(false));
        }

        [Test]
        public async Task TextDecoderStreamStripsTheByteOrderMark()
        {
            var decoder = new TextDecoderStream();
            var writer = decoder.Writable.GetWriter();
            var reader = decoder.Readable.GetReader();
            var read = ReadAllText(reader);

            //  the byte order mark split over two chunks still counts as one
            await writer.Write(new Byte[] { 0xef, 0xbb }).ConfigureAwait(false);
            await writer.Write(new Byte[] { 0xbf, 0x68, 0x69 }).ConfigureAwait(false);
            await writer.Close().ConfigureAwait(false);

            Assert.AreEqual("hi", await read.ConfigureAwait(false));
        }

        [Test]
        public async Task TextDecoderStreamKeepsTheByteOrderMarkWhenIgnored()
        {
            var decoder = new TextDecoderStream("utf-8", new TextDecoderOptions { IgnoreBom = true });
            var writer = decoder.Writable.GetWriter();
            var reader = decoder.Readable.GetReader();
            var read = ReadAllText(reader);

            await writer.Write(new Byte[] { 0xef, 0xbb, 0xbf, 0x68 }).ConfigureAwait(false);
            await writer.Close().ConfigureAwait(false);

            Assert.AreEqual("\ufeffh", await read.ConfigureAwait(false));
        }

        [Test]
        public async Task TextDecoderStreamReplacesInvalidBytes()
        {
            var decoder = new TextDecoderStream();
            var writer = decoder.Writable.GetWriter();
            var reader = decoder.Readable.GetReader();
            var read = ReadAllText(reader);

            await writer.Write(new Byte[] { 0x68, 0xff, 0x69 }).ConfigureAwait(false);
            await writer.Close().ConfigureAwait(false);

            Assert.AreEqual("h\ufffdi", await read.ConfigureAwait(false));
        }

        [Test]
        public async Task TextDecoderStreamJoinsCharactersSplitOverChunks()
        {
            var decoder = new TextDecoderStream();
            var writer = decoder.Writable.GetWriter();
            var reader = decoder.Readable.GetReader();
            var read = ReadAllText(reader);
            var euro = Encoding.UTF8.GetBytes("€");

            await writer.Write(new Byte[] { euro[0] }).ConfigureAwait(false);
            await writer.Write(new Byte[] { euro[1], euro[2] }).ConfigureAwait(false);
            await writer.Close().ConfigureAwait(false);

            Assert.AreEqual("€", await read.ConfigureAwait(false));
        }

        [TestCase("gzip")]
        [TestCase("deflate-raw")]
        public async Task CompressionRoundTripRestoresTheBytes(String format)
        {
            var original = Encoding.UTF8.GetBytes(new String('a', 4000) + "the end");
            var compression = new CompressionStream(format);
            var writer = compression.Writable.GetWriter();
            var compressed = ReadAllBytes(compression.Readable.GetReader());

            await writer.Write(original).ConfigureAwait(false);
            await writer.Close().ConfigureAwait(false);

            var payload = await compressed.ConfigureAwait(false);
            Assert.Less(payload.Length, original.Length);

            var decompression = new DecompressionStream(format);
            var decompressionWriter = decompression.Writable.GetWriter();
            var decompressed = ReadAllBytes(decompression.Readable.GetReader());

            await decompressionWriter.Write(payload).ConfigureAwait(false);
            await decompressionWriter.Close().ConfigureAwait(false);

            CollectionAssert.AreEqual(original, await decompressed.ConfigureAwait(false));
        }

        [Test]
        public void UnknownCompressionFormatThrows()
        {
            Assert.Throws<NotSupportedException>(() => new CompressionStream("lzma"));
            Assert.Throws<NotSupportedException>(() => new DecompressionStream("lzma"));
        }

        private static async Task<String> ReadAllText(ReadableStreamDefaultReader reader)
        {
            var text = new StringBuilder();

            while (true)
            {
                var result = await reader.Read().ConfigureAwait(false);

                if (result.Done)
                {
                    return text.ToString();
                }

                text.Append((String)result.Value);
            }
        }

        private static async Task<Byte[]> ReadAllBytes(ReadableStreamDefaultReader reader)
        {
            var bytes = new List<Byte>();

            while (true)
            {
                var result = await reader.Read().ConfigureAwait(false);

                if (result.Done)
                {
                    return bytes.ToArray();
                }

                var segment = (ArraySegment<Byte>)result.Value;

                for (var i = 0; i < segment.Count; i++)
                {
                    bytes.Add(segment.Array[segment.Offset + i]);
                }
            }
        }

        private static async Task ReadSoon(ReadableStreamDefaultReader reader)
        {
            try
            {
                await reader.Read().ConfigureAwait(false);
            }
            catch (Exception)
            {
                //  the stream failing is what the test is about
            }
        }
    }
}
