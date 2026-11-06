namespace AngleSharp.Io.Tests.Dom
{
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    [TestFixture]
    public class FileReaderTests
    {
        [Test]
        public async Task ReadAsTextGivesTheContent()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new Blob(new Object[] { "hello" }, null);

            reader.ReadAsText(blob);
            Assert.AreEqual(FileReaderState.Loading, reader.ReadyState);
            Assert.IsNull(reader.Result);

            await WaitForEndAsync(reader).ConfigureAwait(false);

            Assert.AreEqual(FileReaderState.Done, reader.ReadyState);
            Assert.AreEqual("hello", reader.Result);
            Assert.IsNull(reader.Error);
        }

        [Test]
        public async Task ReadAsTextUsesTheGivenEncoding()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new Blob(new Object[] { Encoding.GetEncoding("iso-8859-1").GetBytes("héllo") }, null);

            reader.ReadAsText(blob, "iso-8859-1");
            await WaitForEndAsync(reader).ConfigureAwait(false);

            Assert.AreEqual("héllo", reader.Result);
        }

        [Test]
        public async Task ReadAsTextPrefersTheByteOrderMark()
        {
            var text = Encoding.Unicode.GetPreamble();
            var bytes = Encoding.Unicode.GetBytes("hi");
            var all = new Byte[text.Length + bytes.Length];
            text.CopyTo(all, 0);
            bytes.CopyTo(all, text.Length);

            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new Blob(new Object[] { all }, null);

            reader.ReadAsText(blob, "utf-8");
            await WaitForEndAsync(reader).ConfigureAwait(false);

            Assert.AreEqual("hi", reader.Result);
        }

        [Test]
        public async Task ReadAsTextUsesTheBlobCharsetBeforeUtf8()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new Blob(new Object[] { Encoding.GetEncoding("iso-8859-1").GetBytes("héllo") }, new BlobPropertyBag { Type = "text/plain;charset=iso-8859-1" });

            reader.ReadAsText(blob);
            await WaitForEndAsync(reader).ConfigureAwait(false);

            Assert.AreEqual("héllo", reader.Result);
        }

        [Test]
        public async Task InvalidBytesBecomeReplacementCharacters()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new Blob(new Object[] { new Byte[] { 0x68, 0xff, 0x69 } }, null);

            reader.ReadAsText(blob);
            await WaitForEndAsync(reader).ConfigureAwait(false);

            Assert.AreEqual("h\ufffdi", reader.Result);
        }

        [Test]
        public async Task ReadAsDataUrlCarriesTheTypeAndBase64Content()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new Blob(new Object[] { "hello" }, new BlobPropertyBag { Type = "text/plain" });

            reader.ReadAsDataUrl(blob);
            await WaitForEndAsync(reader).ConfigureAwait(false);

            Assert.AreEqual("data:text/plain;base64,aGVsbG8=", reader.Result);
        }

        [Test]
        public async Task ReadAsDataUrlWithoutTypeUsesOctetStream()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new Blob(new Object[] { "x" }, null);

            reader.ReadAsDataUrl(blob);
            await WaitForEndAsync(reader).ConfigureAwait(false);

            Assert.AreEqual("data:application/octet-stream;base64,eA==", reader.Result);
        }

        [Test]
        public async Task ReadAsArrayBufferGivesTheBytes()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new Blob(new Object[] { "hi" }, null);

            reader.ReadAsArrayBuffer(blob);
            await WaitForEndAsync(reader).ConfigureAwait(false);

            CollectionAssert.AreEqual(new Byte[] { 0x68, 0x69 }, (Byte[])reader.Result);
        }

        [Test]
        public async Task ReadAsBinaryStringMapsEveryByteToAChar()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new Blob(new Object[] { new Byte[] { 0x68, 0xff } }, null);

            reader.ReadAsBinaryString(blob);
            await WaitForEndAsync(reader).ConfigureAwait(false);

            Assert.AreEqual("h\u00ff", reader.Result);
        }

        [Test]
        public async Task EventsFireInOrderWithProgressEvents()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new Blob(new Object[] { "hello" }, null);
            var names = new List<String>();
            var loads = new List<Int64>();
            var ended = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);

            foreach (var name in new[] { "loadstart", "progress", "load", "abort", "error", "loadend" })
            {
                reader.AddEventListener(name, (_, ev) =>
                {
                    lock (names)
                    {
                        if (ev.Type != "progress" || !names.Contains("progress"))
                        {
                            names.Add(ev.Type);
                        }

                        loads.Add(((ProgressEvent)ev).Loaded);
                    }

                    if (ev.Type == "loadend")
                    {
                        ended.TrySetResult(null);
                    }
                });
            }

            reader.ReadAsText(blob);
            await Task.WhenAny(ended.Task, Task.Delay(5000)).ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "loadstart", "progress", "load", "loadend" }, names);
            Assert.AreEqual(0, loads[0]);
            Assert.AreEqual(5, loads[loads.Count - 1]);
        }

        [Test]
        public async Task EventsDoNotFireSynchronously()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new GatedBlob("hello");
            var fired = false;
            reader.Started += (_, __) => fired = true;

            reader.ReadAsText(blob);

            Assert.IsFalse(fired);
            blob.Release();
            await WaitForEndAsync(reader).ConfigureAwait(false);
            Assert.IsTrue(fired);
        }

        [Test]
        public async Task SecondReadWhileLoadingThrowsInvalidState()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new GatedBlob("hello");

            reader.ReadAsText(blob);

            Assert.Throws<DomException>(() => reader.ReadAsText(blob));
            blob.Release();
            await WaitForEndAsync(reader).ConfigureAwait(false);
        }

        [Test]
        public async Task AbortLeavesANullResultAndFiresAbortThenLoadEnd()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new GatedBlob("hello");
            var names = new List<String>();
            reader.Aborted += (_, ev) => names.Add(ev.Type);
            reader.Ended += (_, ev) => names.Add(ev.Type);

            reader.ReadAsText(blob);
            reader.Abort();
            blob.Release();

            Assert.AreEqual(FileReaderState.Done, reader.ReadyState);
            Assert.IsNull(reader.Result);
            await Task.Delay(100).ConfigureAwait(false);
            CollectionAssert.AreEqual(new[] { "abort", "loadend" }, names);
        }

        [Test]
        public async Task ClosedBlobFiresErrorWithTheErrorSet()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new Blob(new Object[] { "hello" }, null);
            blob.Close();
            var names = new List<String>();
            reader.Failed += (_, ev) => names.Add(ev.Type);

            reader.ReadAsText(blob);
            await WaitForEndAsync(reader).ConfigureAwait(false);

            Assert.IsNull(reader.Result);
            Assert.IsNotNull(reader.Error);
            CollectionAssert.AreEqual(new[] { "error" }, names);
        }

        [Test]
        public async Task EmptyBlobFinishesWithAnEmptyText()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);

            reader.ReadAsText(new Blob());
            await WaitForEndAsync(reader).ConfigureAwait(false);

            Assert.AreEqual(String.Empty, reader.Result);
            Assert.IsNull(reader.Error);
        }

        [Test]
        public async Task SlicedBlobReadsOnlyTheSubset()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);
            var blob = new Blob(new Object[] { "hello world" }, null);

            reader.ReadAsText(blob.Slice(6));
            await WaitForEndAsync(reader).ConfigureAwait(false);

            Assert.AreEqual("world", reader.Result);
        }

        [Test]
        public async Task ReaderCanBeReusedAfterARead()
        {
            var reader = await CreateReaderAsync().ConfigureAwait(false);

            reader.ReadAsText(new Blob(new Object[] { "first" }, null));
            await WaitForEndAsync(reader).ConfigureAwait(false);

            reader.ReadAsText(new Blob(new Object[] { "second" }, null));
            Assert.AreEqual(FileReaderState.Loading, reader.ReadyState);
            await WaitForEndAsync(reader).ConfigureAwait(false);

            Assert.AreEqual("second", reader.Result);
        }

        private static async Task<FileReader> CreateReaderAsync()
        {
            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Address("https://example.test/").Content("<!doctype html>")).ConfigureAwait(false);
            return new FileReader(document.DefaultView);
        }

        private static async Task WaitForEndAsync(FileReader reader)
        {
            for (var i = 0; i < 500; i++)
            {
                if (reader.ReadyState == FileReaderState.Done)
                {
                    //  the loadend event trails the state, so give the loop a moment
                    await Task.Delay(20).ConfigureAwait(false);
                    return;
                }

                await Task.Delay(5).ConfigureAwait(false);
            }

            Assert.Fail("The read did not finish.");
        }

        /// <summary>
        /// A blob whose read blocks until released, making the asynchrony of
        /// the reader deterministic to test.
        /// </summary>
        private sealed class GatedBlob : IBlob
        {
            private readonly System.Threading.SemaphoreSlim _gate = new System.Threading.SemaphoreSlim(0);
            private readonly Byte[] _content;

            public GatedBlob(String content)
            {
                _content = Encoding.UTF8.GetBytes(content);
            }

            public void Release() => _gate.Release();

            public Int32 Length
            {
                get
                {
                    _gate.Wait();
                    _gate.Release();
                    return _content.Length;
                }
            }

            public String Type => String.Empty;

            public Boolean IsClosed => false;

            public System.IO.Stream Body => new System.IO.MemoryStream(_content, false);

            public IBlob Slice(Int32 start = 0, Int32 end = Int32.MaxValue, String contentType = null) => this;

            public void Close()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
