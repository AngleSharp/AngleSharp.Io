namespace AngleSharp.Io;

using System;
using System.Collections.Generic;

/// <summary>
/// Options for setting up the navigator.
/// </summary>
public sealed class NavigatorOptions
{
    /// <summary>
    /// Gets or sets the user agent string to use for the navigator.
    /// </summary>
    public String UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/45.0.2454.85 Safari/537.36 OPR/32.0.1948.19 (Edition beta)";

    /// <summary>
    /// Gets or sets the preferred languages exposed via navigator.languages,
    /// most preferred first, as BCP 47 language tags (e.g., "en-US"). The
    /// first entry is also exposed via navigator.language. If not set or
    /// empty, the languages are derived from the current UI culture.
    /// </summary>
    public IEnumerable<String> Languages { get; set; }
}
