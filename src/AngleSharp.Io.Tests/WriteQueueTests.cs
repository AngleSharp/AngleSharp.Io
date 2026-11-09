namespace AngleSharp.Io.Tests
{
    using AngleSharp.Io;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    [TestFixture]
    public class WriteQueueTests
    {
        [Test]
        public async Task WritesOfOneKeyRunOneAfterAnother()
        {
            var queue = new WriteQueue();
            var gate = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
            var order = new List<String>();

            var first = queue.RunAsync("https://a.example", async () =>
            {
                order.Add("first started");
                await gate.Task.ConfigureAwait(false);
                order.Add("first done");
            });
            var second = queue.RunAsync("https://a.example", () =>
            {
                order.Add("second started");
                return Task.CompletedTask;
            });

            // the second write may only start once the first is done, which it is not yet
            Assert.IsFalse(second.IsCompleted);
            CollectionAssert.AreEqual(new[] { "first started" }, order);

            gate.SetResult(true);
            await Task.WhenAll(first, second).ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "first started", "first done", "second started" }, order);
        }

        [Test]
        public async Task WritesOfOtherKeysDoNotWait()
        {
            var queue = new WriteQueue();
            var gate = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

            var blocked = queue.RunAsync("https://a.example", () => gate.Task);
            await queue.RunAsync("https://b.example", () => Task.CompletedTask).ConfigureAwait(false);

            Assert.IsFalse(blocked.IsCompleted);
            gate.SetResult(true);
            await blocked.ConfigureAwait(false);
        }

        [Test]
        public async Task AFailedWriteReportsItsErrorAndDoesNotStopTheNext()
        {
            var queue = new WriteQueue();
            var gate = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ran = false;

            var failing = queue.RunAsync("https://a.example", async () =>
            {
                await gate.Task.ConfigureAwait(false);
                throw new InvalidOperationException("disk full");
            });
            var next = queue.RunAsync("https://a.example", () =>
            {
                ran = true;
                return Task.CompletedTask;
            });

            gate.SetResult(true);

            var error = Assert.ThrowsAsync<InvalidOperationException>(() => failing);
            Assert.AreEqual("disk full", error.Message);
            await next.ConfigureAwait(false);
            Assert.IsTrue(ran);
        }
    }
}
