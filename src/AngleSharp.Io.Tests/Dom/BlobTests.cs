namespace AngleSharp.Io.Tests.Dom
{
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    [TestFixture]
    public class BlobTests
    {
        [Test]
        public void EmptyBlobHasNoContentAndNoType()
        {
            var blob = new Blob();

            Assert.AreEqual(0, blob.Length);
            Assert.AreEqual(String.Empty, blob.Type);
            Assert.IsFalse(blob.IsClosed);
        }

        [Test]
        public void PartsAreConcatenated()
        {
            var inner = new Blob(new Object[] { "b" }, null);
            var blob = new Blob(new Object[] { "a", inner, new Byte[] { 0x63 }, new ArraySegment<Byte>(new Byte[] { 0x63, 0x64, 0x65 }, 1, 1) }, null);

            Assert.AreEqual("abcd", ReadAsText(blob));
        }

        [Test]
        public void TypeIsLowercasedAndInvalidTypeIsDropped()
        {
            Assert.AreEqual("text/plain", new Blob(null, new BlobPropertyBag { Type = "Text/PLAIN" }).Type);
            Assert.AreEqual(String.Empty, new Blob(null, new BlobPropertyBag { Type = "text/pläin" }).Type);
        }

        [Test]
        public async Task TextDecodesUtf8AndStripsBom()
        {
            var bytes = new Byte[] { 0xef, 0xbb, 0xbf, 0x68, 0x69 };
            var blob = new Blob(new Object[] { bytes }, null);

            Assert.AreEqual("hi", await blob.Text().ConfigureAwait(false));
        }

        [Test]
        public async Task ArrayBufferGivesACopyOfTheBytes()
        {
            var blob = new Blob(new Object[] { "hi" }, null);
            var bytes = await blob.ArrayBuffer().ConfigureAwait(false);

            CollectionAssert.AreEqual(new Byte[] { 0x68, 0x69 }, bytes);
            bytes[0] = 0;
            Assert.AreEqual("hi", await blob.Text().ConfigureAwait(false));
        }

        [Test]
        public void SliceTakesTheSubsetWithNegativeIndices()
        {
            var blob = new Blob(new Object[] { "hello world" }, new BlobPropertyBag { Type = "text/plain" });

            Assert.AreEqual("world", ReadAsText((Blob)blob.Slice(6)));
            Assert.AreEqual("hello", ReadAsText((Blob)blob.Slice(0, 5)));
            Assert.AreEqual("world", ReadAsText((Blob)blob.Slice(-5)));
            Assert.AreEqual("hello", ReadAsText((Blob)blob.Slice(0, -6)));
            Assert.AreEqual(String.Empty, ReadAsText((Blob)blob.Slice(8, 2)));
        }

        [Test]
        public void SliceHasNoTypeUnlessGiven()
        {
            var blob = new Blob(new Object[] { "hello" }, new BlobPropertyBag { Type = "text/plain" });

            Assert.AreEqual(String.Empty, blob.Slice(0, 2).Type);
            Assert.AreEqual("text/x", blob.Slice(0, 2, "Text/X").Type);
        }

        [Test]
        public async Task StreamDeliversTheContent()
        {
            var blob = new Blob(new Object[] { "hello world" }, null);
            var reader = blob.Stream().GetReader();
            var text = new StringBuilder();

            while (true)
            {
                var result = await reader.Read().ConfigureAwait(false);

                if (result.Done)
                {
                    break;
                }

                var segment = (ArraySegment<Byte>)result.Value;
                text.Append(Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count));
            }

            Assert.AreEqual("hello world", text.ToString());
        }

        [Test]
        public void FileKeepsNameTypeAndLastModified()
        {
            var file = new WebFile(new Object[] { "data" }, "test.txt", new FilePropertyBag { Type = "text/plain", LastModified = 1000 });

            Assert.AreEqual("test.txt", file.Name);
            Assert.AreEqual("text/plain", file.Type);
            Assert.AreEqual(4, file.Length);
            Assert.AreEqual(new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc), file.LastModified);
        }

        private static String ReadAsText(Blob blob)
        {
            using (var reader = new StreamReader(blob.Body, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
