namespace AngleSharp.Io.Tests.Dom
{
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using FluentAssertions;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class AbortSignalTests
    {
        [Test]
        public void ControllerStartsNotAborted()
        {
            var controller = new AbortController();

            controller.Signal.IsAborted.Should().BeFalse();
            controller.Signal.Reason.Should().BeNull();
            controller.Signal.ThrowIfAborted();
        }

        [Test]
        public void AbortSetsAnAbortErrorAsDefaultReason()
        {
            var controller = new AbortController();

            controller.Abort();

            controller.Signal.IsAborted.Should().BeTrue();
            controller.Signal.Reason.Should().BeOfType<DomException>().Which.Code.Should().Be((Int32)DomError.Abort);
        }

        [Test]
        public void AbortKeepsTheGivenReason()
        {
            var reason = new Object();
            var controller = new AbortController();

            controller.Abort(reason);

            controller.Signal.Reason.Should().BeSameAs(reason);
        }

        [Test]
        public void AbortFiresTheEventOnce()
        {
            var controller = new AbortController();
            var calls = 0;
            controller.Signal.AddEventListener("abort", (s, ev) => calls++);
            controller.Signal.Aborted += (s, ev) => calls++;

            controller.Abort();
            controller.Abort();

            calls.Should().Be(2);
        }

        [Test]
        public void SecondAbortKeepsTheFirstReason()
        {
            var first = new Object();
            var controller = new AbortController();

            controller.Abort(first);
            controller.Abort(new Object());

            controller.Signal.Reason.Should().BeSameAs(first);
        }

        [Test]
        public void ThrowIfAbortedThrowsTheDefaultReason()
        {
            var controller = new AbortController();
            controller.Abort();

            Action action = controller.Signal.ThrowIfAborted;

            action.Should().Throw<DomException>().Which.Code.Should().Be((Int32)DomError.Abort);
        }

        [Test]
        public void ThrowIfAbortedKeepsAnotherReasonForScripts()
        {
            var reason = new Object();
            var controller = new AbortController();
            controller.Abort(reason);

            Action action = controller.Signal.ThrowIfAborted;

            action.Should().Throw<DomException>().Which.Data["AngleSharp.ScriptValue"].Should().BeSameAs(reason);
        }

        [Test]
        public void StaticAbortIsAbortedAlready()
        {
            AbortSignal.Abort().IsAborted.Should().BeTrue();
        }

        [Test]
        public void AnyFollowsItsSignals()
        {
            var reason = new Object();
            var first = new AbortController();
            var second = new AbortController();
            var any = AbortSignal.Any(new[] { first.Signal, second.Signal });
            var calls = 0;
            any.Aborted += (s, ev) => calls++;

            any.IsAborted.Should().BeFalse();
            second.Abort(reason);

            any.IsAborted.Should().BeTrue();
            any.Reason.Should().BeSameAs(reason);
            calls.Should().Be(1);
        }

        [Test]
        public void AnyOfAnAbortedSignalIsAbortedAlready()
        {
            var reason = new Object();

            var any = AbortSignal.Any(new[] { new AbortController().Signal, AbortSignal.Abort(reason) });

            any.IsAborted.Should().BeTrue();
            any.Reason.Should().BeSameAs(reason);
        }

        [Test]
        public async Task TimeoutAbortsWithATimeoutError()
        {
            var document = await BrowsingContext.New().OpenNewAsync("https://example.com/");

            var signal = AbortSignal.Timeout(document.DefaultView, 10);

            signal.IsAborted.Should().BeFalse();

            for (var i = 0; i < 200 && !signal.IsAborted; i++)
            {
                await Task.Delay(10);
            }

            signal.IsAborted.Should().BeTrue();
            signal.Reason.Should().BeOfType<DomException>().Which.Code.Should().Be((Int32)DomError.Timeout);
        }
    }
}
