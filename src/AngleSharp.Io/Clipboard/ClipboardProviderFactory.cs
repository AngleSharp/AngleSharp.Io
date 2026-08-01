namespace AngleSharp.Io.ClipboardApi
{
    using AngleSharp.Browser.Dom;
    using AngleSharp.Io.Dom;
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents the default Clipboard provider factory.
    /// </summary>
    public sealed class ClipboardProviderFactory : IClipboardProviderFactory
    {
        private readonly IClipboardPlatform _platform;
        private readonly ConditionalWeakTable<INavigator, Clipboard> _clipboards;

        /// <summary>
        /// Creates a new Clipboard provider factory.
        /// </summary>
        /// <param name="platform">The platform clipboard implementation.</param>
        public ClipboardProviderFactory(IClipboardPlatform platform)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _clipboards = new ConditionalWeakTable<INavigator, Clipboard>();
        }

        /// <inheritdoc />
        public Clipboard GetClipboard(INavigator navigator)
        {
            if (navigator == null)
            {
                return null;
            }

            return _clipboards.GetValue(navigator, _ => new Clipboard(_platform));
        }
    }
}