namespace AngleSharp.Io.Dom;

using AngleSharp.Browser.Dom;
using System;
using System.Net.NetworkInformation;

sealed class Navigator : INavigator
{
    private readonly IBrowsingContext _context;
    private readonly String _userAgent;

    public Navigator(IBrowsingContext context, NavigatorOptions options)
    {
        _context = context;
        _userAgent = options.UserAgent;
    }

    public IBrowsingContext Context => _context;

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
}
