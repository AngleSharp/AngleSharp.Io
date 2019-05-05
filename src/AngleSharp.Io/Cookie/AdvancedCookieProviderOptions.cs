namespace AngleSharp.Io.Cookie
{
    using System;

    /// <summary>
    /// Options for the AdvancedCookieProvider.
    /// </summary>
    public struct AdvancedCookieProviderOptions
    {
        /// <summary>
        /// Gets or sets if parsing is forced. If true, errors
        /// will be suppressed.
        /// </summary>
        public Boolean IsForceParse { get; set; }

        /// <summary>
        /// Gets or sets if http only declarations should be
        /// allowed.
        /// </summary>
        public Boolean IsHttpOnlyExtension { get; set; }
    }
}
