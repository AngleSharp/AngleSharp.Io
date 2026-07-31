namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the BroadcastChannel interface.
    /// </summary>
    [DomName("BroadcastChannel")]
    public sealed class BroadcastChannel : EventTarget, IDisposable
    {
        private readonly String _name;
        private readonly String _origin;
        private readonly IWindow _window;

        private Boolean _isClosed;

        /// <summary>
        /// Creates a new broadcast channel.
        /// </summary>
        /// <param name="window">The parent window.</param>
        /// <param name="name">The channel name.</param>
        [DomConstructor]
        public BroadcastChannel(IWindow window, String name)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _name = String.IsNullOrEmpty(name) ? String.Empty : name;
            _origin = GetOrigin(window);

            BroadcastChannelRegistry.Register(this);
            _window.Unloaded += OnUnload;
        }

        /// <summary>
        /// Gets the channel name.
        /// </summary>
        [DomName("name")]
        public String Name => _name;

        /// <summary>
        /// Adds or removes the handler for the message event.
        /// </summary>
        [DomName("onmessage")]
        public event DomEventHandler Message
        {
            add { AddEventListener(EventNames.Message, value, false); }
            remove { RemoveEventListener(EventNames.Message, value, false); }
        }

        /// <summary>
        /// Posts a message to other channels with the same name and origin.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        [DomName("postMessage")]
        public void PostMessage(Object message)
        {
            if (_isClosed)
            {
                return;
            }

            BroadcastChannelRegistry.PostMessage(this, message);
        }

        /// <summary>
        /// Closes the channel and releases its registration.
        /// </summary>
        [DomName("close")]
        public void Close()
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;
            BroadcastChannelRegistry.Unregister(this);
            RemoveEventListeners();
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        internal String Origin => _origin;

        internal Boolean IsClosed => _isClosed;

        internal void Receive(Object message, BroadcastChannel source)
        {
            if (_isClosed)
            {
                return;
            }

            var evt = new MessageEvent(EventNames.Message, false, false, message, _origin, String.Empty, source?._window, Array.Empty<IMessagePort>());
            Dispatch(evt);
        }

        private void OnUnload(Object sender, Event ev)
        {
            Close();
        }

        private static String GetOrigin(IWindow window)
        {
            var href = window?.Location?.Href;

            if (String.IsNullOrEmpty(href))
            {
                return String.Empty;
            }

            var url = new Url(href);

            if (url.IsInvalid || url.IsRelative)
            {
                return String.Empty;
            }

            return url.Origin ?? String.Empty;
        }
    }

    internal static class BroadcastChannelRegistry
    {
        private static readonly Object SyncRoot = new Object();
        private static readonly Dictionary<String, List<WeakReference<BroadcastChannel>>> ChannelsByOriginAndName = new Dictionary<String, List<WeakReference<BroadcastChannel>>>(StringComparer.Ordinal);

        public static void Register(BroadcastChannel channel)
        {
            if (channel == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                var key = GetKey(channel.Origin, channel.Name);

                if (!ChannelsByOriginAndName.TryGetValue(key, out var channels))
                {
                    channels = new List<WeakReference<BroadcastChannel>>();
                    ChannelsByOriginAndName[key] = channels;
                }

                Cleanup(channels);
                channels.Add(new WeakReference<BroadcastChannel>(channel));
            }
        }

        public static void Unregister(BroadcastChannel channel)
        {
            if (channel == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                var key = GetKey(channel.Origin, channel.Name);

                if (!ChannelsByOriginAndName.TryGetValue(key, out var channels))
                {
                    return;
                }

                Cleanup(channels);

                for (var i = channels.Count - 1; i >= 0; i--)
                {
                    if (!channels[i].TryGetTarget(out var target) || ReferenceEquals(target, channel))
                    {
                        channels.RemoveAt(i);
                    }
                }

                if (channels.Count == 0)
                {
                    ChannelsByOriginAndName.Remove(key);
                }
            }
        }

        public static void PostMessage(BroadcastChannel source, Object message)
        {
            if (source == null)
            {
                return;
            }

            List<BroadcastChannel> targets = null;

            lock (SyncRoot)
            {
                var key = GetKey(source.Origin, source.Name);

                if (!ChannelsByOriginAndName.TryGetValue(key, out var channels))
                {
                    return;
                }

                Cleanup(channels);
                targets = new List<BroadcastChannel>();

                for (var i = 0; i < channels.Count; i++)
                {
                    if (!channels[i].TryGetTarget(out var target) || target.IsClosed || ReferenceEquals(target, source))
                    {
                        continue;
                    }

                    targets.Add(target);
                }
            }

            for (var i = 0; i < targets.Count; i++)
            {
                targets[i].Receive(message, source);
            }
        }

        private static void Cleanup(List<WeakReference<BroadcastChannel>> channels)
        {
            for (var i = channels.Count - 1; i >= 0; i--)
            {
                if (!channels[i].TryGetTarget(out var target) || target.IsClosed)
                {
                    channels.RemoveAt(i);
                }
            }
        }

        private static String GetKey(String origin, String name)
        {
            return (origin ?? String.Empty) + "|" + (name ?? String.Empty);
        }
    }
}