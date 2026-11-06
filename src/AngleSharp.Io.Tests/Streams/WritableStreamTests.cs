namespace AngleSharp.Io.Tests.Streams
{
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    [TestFixture]
    public class WritableStreamTests
    {
        [Test]
        public async Task WritesReachTheSinkInOrderAndCloseCompletes()
        {
            var written = new List<Object>();
            var closed = false;
            var stream = new WritableStream(new UnderlyingSink
            {
                Write = (chunk, _) =>
                {
                    written.Add(chunk);
                    return Task.CompletedTask;
                },
                Close = () =>
                {
                    closed = true;
                    return Task.CompletedTask;
                },
            });

            var writer = stream.GetWriter();
            await writer.Write("a").ConfigureAwait(false);
            await writer.Write("b").ConfigureAwait(false);
            await writer.Close().ConfigureAwait(false);
            await writer.Closed.ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "a", "b" }, written);
            Assert.IsTrue(closed);
        }

        [Test]
        public void SecondGetWriterOnALockedStreamThrows()
        {
            var stream = new WritableStream();
            stream.GetWriter();

            Assert.IsTrue(stream.Locked);
            Assert.Throws<InvalidOperationException>(() => stream.GetWriter());
        }

        [Test]
        public async Task DesiredSizeFallsWithQueuedWritesAndReadyWaits()
        {
            var release = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stream = new WritableStream(new UnderlyingSink
            {
                Write = (_, __) => release.Task,
            });

            var writer = stream.GetWriter();
            Assert.AreEqual(1, writer.DesiredSize);
            await writer.Ready.ConfigureAwait(false);

            var first = writer.Write("a");
            Assert.AreEqual(0, writer.DesiredSize);
            var ready = writer.Ready;
            Assert.IsFalse(ready.IsCompleted);

            release.TrySetResult(null);
            await first.ConfigureAwait(false);
            await ready.ConfigureAwait(false);
            Assert.AreEqual(1, writer.DesiredSize);
        }

        [Test]
        public async Task AbortReachesTheSinkAndFailsLaterWrites()
        {
            Object abortedWith = null;
            var stream = new WritableStream(new UnderlyingSink
            {
                Abort = reason =>
                {
                    abortedWith = reason;
                    return Task.CompletedTask;
                },
            });

            var writer = stream.GetWriter();
            await writer.Abort("stop").ConfigureAwait(false);

            Assert.AreEqual("stop", abortedWith);
            Assert.IsNull(writer.DesiredSize);
            Assert.ThrowsAsync<AngleSharp.Dom.DomException>(async () => await writer.Write("x").ConfigureAwait(false));
        }

        [Test]
        public void FailingWriteErrorsTheStream()
        {
            var stream = new WritableStream(new UnderlyingSink
            {
                Write = (_, __) => Task.FromException(new InvalidOperationException("boom")),
            });

            var writer = stream.GetWriter();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await writer.Write("x").ConfigureAwait(false));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await writer.Closed.ConfigureAwait(false));
            Assert.IsNull(writer.DesiredSize);
        }

        [Test]
        public void WriteAfterCloseFails()
        {
            var stream = new WritableStream();
            var writer = stream.GetWriter();
            _ = writer.Close();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await writer.Write("x").ConfigureAwait(false));
        }

        [Test]
        public async Task ControllerErrorFailsTheStream()
        {
            var stream = new WritableStream(new UnderlyingSink
            {
                Write = (chunk, controller) =>
                {
                    controller.Error(new InvalidOperationException("rejected"));
                    return Task.CompletedTask;
                },
            });

            var writer = stream.GetWriter();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await writer.Write("x").ConfigureAwait(false));
            await Task.CompletedTask.ConfigureAwait(false);
        }

        [Test]
        public async Task ReleaseLockFreesTheStreamForAnotherWriter()
        {
            var stream = new WritableStream();
            var writer = stream.GetWriter();
            writer.ReleaseLock();

            Assert.IsFalse(stream.Locked);
            Assert.DoesNotThrow(() => stream.GetWriter());
            Assert.ThrowsAsync<InvalidOperationException>(async () => await writer.Write("x").ConfigureAwait(false));
            await Task.CompletedTask.ConfigureAwait(false);
        }

        [Test]
        public async Task ByteLengthStrategyMeasuresChunksInBytes()
        {
            var stream = new WritableStream(new UnderlyingSink
            {
                Write = (_, __) => new TaskCompletionSource<Object>().Task,
            }, new ByteLengthQueuingStrategy(new QueuingStrategyInit { HighWaterMark = 16 }));

            var writer = stream.GetWriter();
            Assert.AreEqual(16, writer.DesiredSize);

            _ = writer.Write(new Byte[10]);
            await Task.Delay(50).ConfigureAwait(false);

            Assert.AreEqual(6, writer.DesiredSize);
        }
    }
}
