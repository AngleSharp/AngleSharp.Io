namespace AngleSharp.Io.Tests.Streams
{
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    [TestFixture]
    public class ReadableStreamTests
    {
        [Test]
        public async Task StartCanEnqueueAndCloseAndReadsDrainTheQueue()
        {
            var stream = new ReadableStream(new UnderlyingSource
            {
                Start = controller =>
                {
                    controller.Enqueue("a");
                    controller.Enqueue("b");
                    controller.Close();
                    return Task.CompletedTask;
                },
            });

            var reader = stream.GetReader();
            var first = await reader.Read().ConfigureAwait(false);
            var second = await reader.Read().ConfigureAwait(false);
            var done = await reader.Read().ConfigureAwait(false);

            Assert.AreEqual("a", first.Value);
            Assert.IsFalse(first.Done);
            Assert.AreEqual("b", second.Value);
            Assert.IsTrue(done.Done);
            Assert.IsNull(done.Value);
            await reader.Closed.ConfigureAwait(false);
        }

        [Test]
        public async Task PullProducesOnDemand()
        {
            var counter = 0;
            var stream = new ReadableStream(new UnderlyingSource
            {
                Pull = controller =>
                {
                    counter++;

                    if (counter > 3)
                    {
                        controller.Close();
                    }
                    else
                    {
                        controller.Enqueue(counter);
                    }

                    return Task.CompletedTask;
                },
            });

            var reader = stream.GetReader();
            var values = new List<Object>();

            while (true)
            {
                var result = await reader.Read().ConfigureAwait(false);

                if (result.Done)
                {
                    break;
                }

                values.Add(result.Value);
            }

            CollectionAssert.AreEqual(new Object[] { 1, 2, 3 }, values);
        }

        [Test]
        public void SecondGetReaderOnALockedStreamThrows()
        {
            var stream = new ReadableStream();
            stream.GetReader();

            Assert.IsTrue(stream.Locked);
            Assert.Throws<InvalidOperationException>(() => stream.GetReader());
        }

        [Test]
        public void ReleaseLockFreesTheStreamForAnotherReader()
        {
            var stream = new ReadableStream();
            var reader = stream.GetReader();
            reader.ReleaseLock();

            Assert.IsFalse(stream.Locked);
            Assert.DoesNotThrow(() => stream.GetReader());
        }

        [Test]
        public async Task ReleaseLockRejectsPendingReads()
        {
            var stream = new ReadableStream();
            var reader = stream.GetReader();
            var pending = reader.Read();
            reader.ReleaseLock();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await pending.ConfigureAwait(false));
            await Task.CompletedTask.ConfigureAwait(false);
        }

        [Test]
        public async Task CancelReachesTheSourceAndFinishesPendingReads()
        {
            Object canceledWith = null;
            var stream = new ReadableStream(new UnderlyingSource
            {
                Cancel = reason =>
                {
                    canceledWith = reason;
                    return Task.CompletedTask;
                },
            });

            var reader = stream.GetReader();
            var pending = reader.Read();
            await reader.Cancel("enough").ConfigureAwait(false);
            var result = await pending.ConfigureAwait(false);

            Assert.IsTrue(result.Done);
            Assert.AreEqual("enough", canceledWith);
            await reader.Closed.ConfigureAwait(false);
        }

        [Test]
        public void CancelOnALockedStreamFails()
        {
            var stream = new ReadableStream();
            stream.GetReader();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await stream.Cancel().ConfigureAwait(false));
        }

        [Test]
        public void ErroredStreamRejectsReadsAndClosed()
        {
            var failure = new InvalidOperationException("boom");
            var stream = new ReadableStream(new UnderlyingSource
            {
                Start = controller =>
                {
                    controller.Error(failure);
                    return Task.CompletedTask;
                },
            });

            var reader = stream.GetReader();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await reader.Read().ConfigureAwait(false));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await reader.Closed.ConfigureAwait(false));
        }

        [Test]
        public void FailingPullErrorsTheStream()
        {
            var stream = new ReadableStream(new UnderlyingSource
            {
                Pull = _ => Task.FromException(new InvalidOperationException("boom")),
            });

            var reader = stream.GetReader();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await reader.Read().ConfigureAwait(false));
        }

        [Test]
        public async Task HighWaterMarkControlsTheEagernessOfPulls()
        {
            var pulls = 0;
            var stream = new ReadableStream(new UnderlyingSource
            {
                Pull = controller =>
                {
                    pulls++;
                    controller.Enqueue(pulls);
                    return Task.CompletedTask;
                },
            }, new QueuingStrategy { HighWaterMark = 3 });

            //  the stream fills its queue up to the high water mark without a reader
            await Task.Delay(100).ConfigureAwait(false);

            Assert.AreEqual(3, pulls);
        }

        [Test]
        public async Task BackpressureHoldsPullsUntilARead()
        {
            var pulls = 0;
            var stream = new ReadableStream(new UnderlyingSource
            {
                Pull = controller =>
                {
                    pulls++;
                    controller.Enqueue(pulls);
                    return Task.CompletedTask;
                },
            });

            await Task.Delay(100).ConfigureAwait(false);
            Assert.AreEqual(1, pulls);

            var reader = stream.GetReader();
            await reader.Read().ConfigureAwait(false);
            await Task.Delay(100).ConfigureAwait(false);

            Assert.AreEqual(2, pulls);
        }

        [Test]
        public async Task TeeGivesBothBranchesEveryChunk()
        {
            var stream = new ReadableStream(new UnderlyingSource
            {
                Start = controller =>
                {
                    controller.Enqueue("x");
                    controller.Enqueue("y");
                    controller.Close();
                    return Task.CompletedTask;
                },
            });

            var branches = stream.Tee();

            Assert.IsTrue(stream.Locked);
            Assert.AreEqual(2, branches.Length);

            var first = await ReadAll(branches[0]).ConfigureAwait(false);
            var second = await ReadAll(branches[1]).ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "x", "y" }, first);
            CollectionAssert.AreEqual(new[] { "x", "y" }, second);
        }

        [Test]
        public async Task CancelingBothBranchesCancelsTheSource()
        {
            Object canceledWith = null;
            var stream = new ReadableStream(new UnderlyingSource
            {
                Cancel = reason =>
                {
                    canceledWith = reason;
                    return Task.CompletedTask;
                },
            });

            var branches = stream.Tee();
            var pending = branches[0].Cancel("one");
            await branches[1].Cancel("two").ConfigureAwait(false);
            await pending.ConfigureAwait(false);

            CollectionAssert.AreEqual(new Object[] { "one", "two" }, (Object[])canceledWith);
        }

        [Test]
        public async Task PipeToWritesEveryChunkAndCloses()
        {
            var stream = new ReadableStream(new UnderlyingSource
            {
                Start = controller =>
                {
                    controller.Enqueue("a");
                    controller.Enqueue("b");
                    controller.Close();
                    return Task.CompletedTask;
                },
            });

            var written = new List<Object>();
            var closed = false;
            var destination = new WritableStream(new UnderlyingSink
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

            await stream.PipeTo(destination).ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "a", "b" }, written);
            Assert.IsTrue(closed);
            Assert.IsFalse(stream.Locked);
            Assert.IsFalse(destination.Locked);
        }

        [Test]
        public void PipeToAnErroringSinkCancelsTheSource()
        {
            Object canceledWith = null;
            var stream = new ReadableStream(new UnderlyingSource
            {
                Start = controller =>
                {
                    controller.Enqueue("a");
                    return Task.CompletedTask;
                },
                Cancel = reason =>
                {
                    canceledWith = reason;
                    return Task.CompletedTask;
                },
            });

            var destination = new WritableStream(new UnderlyingSink
            {
                Write = (_, __) => Task.FromException(new InvalidOperationException("full")),
            });

            Assert.ThrowsAsync<InvalidOperationException>(async () => await stream.PipeTo(destination).ConfigureAwait(false));
            Assert.IsInstanceOf<InvalidOperationException>(canceledWith);
        }

        [Test]
        public async Task PipeThroughGivesTheReadableSideOfTheTransform()
        {
            var stream = new ReadableStream(new UnderlyingSource
            {
                Start = controller =>
                {
                    controller.Enqueue("ab");
                    controller.Enqueue("cd");
                    controller.Close();
                    return Task.CompletedTask;
                },
            });

            var transform = new TransformStream(new Transformer
            {
                Transform = (chunk, controller) =>
                {
                    controller.Enqueue(((String)chunk).ToUpperInvariant());
                    return Task.CompletedTask;
                },
            });

            var values = await ReadAll(stream.PipeThrough(transform)).ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "AB", "CD" }, values);
        }

        [Test]
        public async Task FromStreamDeliversChunksAndDisposesTheSource()
        {
            var source = new MemoryStream(Encoding.UTF8.GetBytes("hello world"));
            var stream = ReadableStream.FromStream(source, chunkSize: 4);
            var reader = stream.GetReader();
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

            Assert.AreEqual("hello world", text.ToString());
            Assert.AreEqual(3, chunks);
            Assert.IsFalse(source.CanRead);
        }

        [Test]
        public async Task CancelingAFromStreamReaderDisposesTheSource()
        {
            var disposed = false;
            var source = new MemoryStream(Encoding.UTF8.GetBytes("hello world"));
            var stream = ReadableStream.FromStream(source, onCancel: () => disposed = true, chunkSize: 4);
            var reader = stream.GetReader();

            await reader.Read().ConfigureAwait(false);
            await reader.Cancel("stop").ConfigureAwait(false);

            Assert.IsTrue(disposed);
            Assert.IsFalse(source.CanRead);
        }

        private static async Task<List<Object>> ReadAll(ReadableStream stream)
        {
            var reader = stream.GetReader();
            var values = new List<Object>();

            while (true)
            {
                var result = await reader.Read().ConfigureAwait(false);

                if (result.Done)
                {
                    return values;
                }

                values.Add(result.Value);
            }
        }
    }
}
