namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A pattern matching the components of a URL.
    /// </summary>
    /// <remarks>
    /// Matching uses .NET ECMAScript regular expressions with a one-second timeout,
    /// so custom expressions do not support all JavaScript regular expression syntax.
    /// String-valued and negated Unicode set operations are not supported.
    /// </remarks>
    [DomName("URLPattern")]
    [DomExposed("Window")]
    [DomExposed("Worker")]
    public sealed class UrlPattern
    {
        private readonly UrlPatternMatcher _matcher;
        private readonly Object _undefined;

        /// <summary>Creates a pattern matching every URL component.</summary>
        public UrlPattern() : this(new UrlPatternInit(), null)
        {
        }

        /// <summary>Creates a pattern from its component patterns.</summary>
        /// <param name="input">The component patterns and optional base URL.</param>
        /// <param name="options">The matching options.</param>
        public UrlPattern(UrlPatternInit input, UrlPatternOptions options = null)
        {
            _matcher = new UrlPatternMatcher(input ?? new UrlPatternInit(), options);
        }

        /// <summary>Creates a pattern from a URL pattern string.</summary>
        /// <param name="input">The URL pattern string.</param>
        /// <param name="options">The matching options.</param>
        public UrlPattern(String input, UrlPatternOptions options = null)
            : this(input, null, options)
        {
        }

        /// <summary>Creates a pattern from a URL pattern string and base URL.</summary>
        /// <param name="input">The URL pattern string.</param>
        /// <param name="baseUrl">The base URL for a relative pattern.</param>
        /// <param name="options">The matching options.</param>
        public UrlPattern(String input, String baseUrl, UrlPatternOptions options = null)
        {
            _matcher = new UrlPatternMatcher(UrlPatternMatcher.ParsePattern(input, baseUrl), options);
        }

        /// <summary>Creates a pattern matching every URL component for a window.</summary>
        /// <param name="window">The window constructing the pattern.</param>
        [DomConstructor]
        public UrlPattern(IWindow window) : this()
        {
            _undefined = GetUndefined(window);
        }

        /// <summary>Creates a pattern from its component patterns for a window.</summary>
        /// <param name="window">The window constructing the pattern.</param>
        /// <param name="input">The component patterns and optional base URL.</param>
        /// <param name="options">The matching options.</param>
        [DomConstructor]
        public UrlPattern(IWindow window, UrlPatternInit input, UrlPatternOptions options = null) : this(input, options)
        {
            _undefined = GetUndefined(window);
        }

        /// <summary>Creates a URL pattern for a window.</summary>
        /// <param name="window">The window constructing the pattern.</param>
        /// <param name="input">The URL pattern string.</param>
        /// <param name="options">The matching options.</param>
        [DomConstructor]
        public UrlPattern(IWindow window, String input, UrlPatternOptions options = null) : this(input, options)
        {
            _undefined = GetUndefined(window);
        }

        /// <summary>Creates a URL pattern relative to a base URL for a window.</summary>
        /// <param name="window">The window constructing the pattern.</param>
        /// <param name="input">The URL pattern string.</param>
        /// <param name="baseUrl">The base URL for a relative pattern.</param>
        /// <param name="options">The matching options.</param>
        [DomConstructor]
        public UrlPattern(IWindow window, String input, String baseUrl, UrlPatternOptions options = null) : this(input, baseUrl, options)
        {
            _undefined = GetUndefined(window);
        }

        /// <summary>Rejects a separate base URL with a component dictionary.</summary>
        /// <param name="window">The window constructing the pattern.</param>
        /// <param name="input">The component patterns.</param>
        /// <param name="baseUrl">The invalid separate base URL.</param>
        [DomConstructor]
        public UrlPattern(IWindow window, UrlPatternInit input, String baseUrl)
        {
            throw new ArgumentException("A base URL must be a member of the component dictionary.", nameof(baseUrl));
        }

        /// <summary>Gets the protocol pattern.</summary>
        [DomName("protocol")]
        public String Protocol => _matcher.Patterns[0];

        /// <summary>Gets the username pattern.</summary>
        [DomName("username")]
        public String Username => _matcher.Patterns[1];

        /// <summary>Gets the password pattern.</summary>
        [DomName("password")]
        public String Password => _matcher.Patterns[2];

        /// <summary>Gets the hostname pattern.</summary>
        [DomName("hostname")]
        public String Hostname => _matcher.Patterns[3];

        /// <summary>Gets the port pattern.</summary>
        [DomName("port")]
        public String Port => _matcher.Patterns[4];

        /// <summary>Gets the pathname pattern.</summary>
        [DomName("pathname")]
        public String Pathname => _matcher.Patterns[5];

        /// <summary>Gets the search pattern.</summary>
        [DomName("search")]
        public String Search => _matcher.Patterns[6];

        /// <summary>Gets the fragment pattern.</summary>
        [DomName("hash")]
        public String Hash => _matcher.Patterns[7];

        /// <summary>Gets whether a component contains a regular expression group.</summary>
        [DomName("hasRegExpGroups")]
        public Boolean HasRegExpGroups => _matcher.HasRegExpGroups;

        /// <summary>Tests the empty component dictionary against this pattern.</summary>
        /// <returns>Whether all components match.</returns>
        [DomName("test")]
        public Boolean Test() => Exec() != null;

        /// <summary>Tests URL components against this pattern.</summary>
        /// <param name="input">The URL components.</param>
        /// <returns>Whether all components match.</returns>
        [DomName("test")]
        public Boolean Test(UrlPatternInit input) => Exec(input) != null;

        /// <summary>Rejects a separate base URL with URL components.</summary>
        /// <param name="input">The URL components.</param>
        /// <param name="baseUrl">The invalid separate base URL.</param>
        /// <returns>Never returns.</returns>
        [DomName("test")]
        public Boolean Test(UrlPatternInit input, String baseUrl) => throw new ArgumentException("A base URL must be a member of the component dictionary.", nameof(baseUrl));

        /// <summary>Tests a URL against this pattern.</summary>
        /// <param name="input">The URL to match.</param>
        /// <param name="baseUrl">The base URL for a relative input.</param>
        /// <returns>Whether all components match.</returns>
        [DomName("test")]
        public Boolean Test(String input, String baseUrl = null) => Exec(input, baseUrl) != null;

        /// <summary>Matches the empty component dictionary.</summary>
        /// <returns>The component matches, or null.</returns>
        [DomName("exec")]
        public Dictionary<String, Object> Exec() => Exec(new UrlPatternInit());

        /// <summary>Matches URL components against this pattern.</summary>
        /// <param name="input">The URL components.</param>
        /// <returns>The component matches, or null.</returns>
        [DomName("exec")]
        public Dictionary<String, Object> Exec(UrlPatternInit input) =>
            _matcher.Match(input ?? new UrlPatternInit(), _undefined);

        /// <summary>Rejects a separate base URL with URL components.</summary>
        /// <param name="input">The URL components.</param>
        /// <param name="baseUrl">The invalid separate base URL.</param>
        /// <returns>Never returns.</returns>
        [DomName("exec")]
        public Dictionary<String, Object> Exec(UrlPatternInit input, String baseUrl) =>
            throw new ArgumentException("A base URL must be a member of the component dictionary.", nameof(baseUrl));

        /// <summary>Matches a URL against this pattern.</summary>
        /// <param name="input">The URL to match.</param>
        /// <param name="baseUrl">The base URL for a relative input.</param>
        /// <returns>The component matches, or null.</returns>
        [DomName("exec")]
        public Dictionary<String, Object> Exec(String input, String baseUrl = null) =>
            _matcher.Match(input, baseUrl, _undefined);

        private static Object GetUndefined(IWindow window) =>
            window?.Document?.Context?.GetService<IScriptValueCloner>()?.Undefined;
    }

    /// <summary>Components and an optional base URL for a URL pattern or input.</summary>
    [DomName("URLPatternInit")]
    public sealed class UrlPatternInit
    {
        /// <summary>Gets or sets the protocol.</summary>
        [DomName("protocol")]
        public String Protocol { get; set; }

        /// <summary>Gets or sets the username.</summary>
        [DomName("username")]
        public String Username { get; set; }

        /// <summary>Gets or sets the password.</summary>
        [DomName("password")]
        public String Password { get; set; }

        /// <summary>Gets or sets the hostname.</summary>
        [DomName("hostname")]
        public String Hostname { get; set; }

        /// <summary>Gets or sets the port.</summary>
        [DomName("port")]
        public String Port { get; set; }

        /// <summary>Gets or sets the pathname.</summary>
        [DomName("pathname")]
        public String Pathname { get; set; }

        /// <summary>Gets or sets the search.</summary>
        [DomName("search")]
        public String Search { get; set; }

        /// <summary>Gets or sets the fragment.</summary>
        [DomName("hash")]
        public String Hash { get; set; }

        /// <summary>Gets or sets the base URL.</summary>
        [DomName("baseURL")]
        public String BaseUrl { get; set; }
    }

    /// <summary>Options used when matching a URL pattern.</summary>
    [DomName("URLPatternOptions")]
    public sealed class UrlPatternOptions
    {
        /// <summary>Gets or sets whether matching ignores case.</summary>
        [DomName("ignoreCase")]
        public Boolean IgnoreCase { get; set; }
    }
}
