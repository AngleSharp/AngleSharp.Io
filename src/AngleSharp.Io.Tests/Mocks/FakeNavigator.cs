namespace AngleSharp.Io.Tests.Mocks
{
    using AngleSharp;
    using AngleSharp.Browser.Dom;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class FakeNavigator : INavigator
    {
        private readonly HashSet<(String Scheme, String Url, String Title)> _protocolHandlers;
        private readonly HashSet<(String MimeType, String Url, String Title)> _contentHandlers;

        public FakeNavigator(IBrowsingContext context)
        {
            Context = context;
            _protocolHandlers = new HashSet<(String Scheme, String Url, String Title)>();
            _contentHandlers = new HashSet<(String MimeType, String Url, String Title)>();
        }

        public IBrowsingContext Context { get; }

        public String Name => "Fake Navigator";

        public String Version => "1.0";

        public String Platform => "test";

        public String UserAgent => "FakeNavigator/1.0";

        public Boolean IsOnline => true;

        public void WaitForStorageUpdates()
        {
        }

        public void RegisterProtocolHandler(String scheme, String url, String title)
        {
            _protocolHandlers.Add((scheme ?? String.Empty, url ?? String.Empty, title ?? String.Empty));
        }

        public void RegisterContentHandler(String mimeType, String url, String title)
        {
            _contentHandlers.Add((mimeType ?? String.Empty, url ?? String.Empty, title ?? String.Empty));
        }

        public Boolean IsProtocolHandlerRegistered(String scheme, String url)
        {
            var normalizedScheme = scheme ?? String.Empty;
            var normalizedUrl = url ?? String.Empty;

            return _protocolHandlers.Any(m =>
                String.Equals(m.Scheme, normalizedScheme, StringComparison.Ordinal) &&
                String.Equals(m.Url, normalizedUrl, StringComparison.Ordinal));
        }

        public Boolean IsContentHandlerRegistered(String mimeType, String url)
        {
            var normalizedMimeType = mimeType ?? String.Empty;
            var normalizedUrl = url ?? String.Empty;

            return _contentHandlers.Any(m =>
                String.Equals(m.MimeType, normalizedMimeType, StringComparison.Ordinal) &&
                String.Equals(m.Url, normalizedUrl, StringComparison.Ordinal));
        }

        public void UnregisterProtocolHandler(String scheme, String url)
        {
            var normalizedScheme = scheme ?? String.Empty;
            var normalizedUrl = url ?? String.Empty;
            _protocolHandlers.RemoveWhere(m =>
                String.Equals(m.Scheme, normalizedScheme, StringComparison.Ordinal) &&
                String.Equals(m.Url, normalizedUrl, StringComparison.Ordinal));
        }

        public void UnregisterContentHandler(String mimeType, String url)
        {
            var normalizedMimeType = mimeType ?? String.Empty;
            var normalizedUrl = url ?? String.Empty;
            _contentHandlers.RemoveWhere(m =>
                String.Equals(m.MimeType, normalizedMimeType, StringComparison.Ordinal) &&
                String.Equals(m.Url, normalizedUrl, StringComparison.Ordinal));
        }
    }
}