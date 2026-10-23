namespace AngleSharp.Io.Dom;

using AngleSharp.Browser.Dom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;

sealed class Navigator : INavigator
{
    private readonly IBrowsingContext _context;
    private readonly String _userAgent;
    private readonly String[] _languages;

    public Navigator(IBrowsingContext context, NavigatorOptions options)
    {
        _context = context;
        _userAgent = options.UserAgent;
        _languages = GetLanguages(options.Languages);
    }

    public IBrowsingContext Context => _context;

    public String[] Languages => _languages;

    public String Name => "Netscape";

    public String Platform => String.Empty;

    public String UserAgent => _userAgent;

    public String Version => "1.0.0";

    public Boolean IsContentHandlerRegistered(String mimeType, String url) => false;

    public Boolean IsProtocolHandlerRegistered(String scheme, String url) => false;

    public void RegisterContentHandler(String mimeType, String url, String title)
    {
    }

    public void RegisterProtocolHandler(String scheme, String url, String title)
    {
    }

    public void UnregisterContentHandler(String mimeType, String url)
    {
    }

    public void UnregisterProtocolHandler(String scheme, String url)
    {
    }

    public void WaitForStorageUpdates()
    {
    }

    public Boolean IsOnline => NetworkInterface.GetIsNetworkAvailable();

    internal static String[] GetLanguages(IEnumerable<String> languages)
    {
        var result = (languages ?? Enumerable.Empty<String>())
            .Where(language => !String.IsNullOrWhiteSpace(language))
            .Select(language => language.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return result.Length > 0 ? result : GetCultureLanguages(CultureInfo.CurrentUICulture);
    }

    private static String[] GetCultureLanguages(CultureInfo culture)
    {
        var languages = new List<String>();

        // walk up the parent chain (de-AT -> de) like browsers list a regional
        // tag followed by its base language; the invariant culture has no name
        while (culture != null && !String.IsNullOrEmpty(culture.Name))
        {
            if (!languages.Contains(culture.Name))
            {
                languages.Add(culture.Name);
            }

            culture = culture.Parent;
        }

        if (languages.Count == 0)
        {
            languages.Add("en-US");
        }

        return languages.ToArray();
    }
}
