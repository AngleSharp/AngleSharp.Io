namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Browser;
using AngleSharp.Dom;
using AngleSharp.Dom.Events;
using AngleSharp.Html;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Defines the XHR. For more information see:
/// https://xhr.spec.whatwg.org/#interface-xmlhttprequest
/// </summary>
[DomName("XMLHttpRequest")]
[DomExposed("Window")]
[DomExposed("DedicatedWorker")]
[DomExposed("SharedWorker")]
public sealed class XmlHttpRequest : XmlHttpRequestEventTarget
{
    #region Fields

    private const Int32 BufferSize = 16384;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(50);

    private readonly Dictionary<String, String> _headers;
    private readonly IWindow _window;
    private readonly XmlHttpRequestUpload _upload;

    private RequesterState _readyState;
    private Int32 _timeout;
    private Boolean _credentials;
    private HttpMethod _method;
    private Url _url;
    private Boolean _async;
    private String _mime;
    private HttpStatusCode _responseStatus;
    private String _responseUrl;
    private String _responseText;
    private IDownload _download;
    private Boolean _aborted;
    private Int64 _loaded;
    private Int64 _total;
    private Boolean _lengthComputable;
    private Boolean _uploadComplete;
    private Boolean _hasUploadBody;
    private Int64 _uploadLoaded;
    private Int64 _uploadTotal;

    #endregion

    #region ctor

    /// <summary>
    /// Creates a new XHR.
    /// </summary>
    [DomConstructor]
    public XmlHttpRequest(IWindow window)
    {
        _window = window;
        _upload = new XmlHttpRequestUpload();
        _async = true;
        _method = HttpMethod.Get;
        _headers = new Dictionary<String, String>();
        _url = null;
        _mime = null;
        _responseUrl = String.Empty;
        _responseText = String.Empty;
        _readyState = RequesterState.Unsent;
        _responseStatus = (HttpStatusCode)0;
        _credentials = false;
        _timeout = 45000;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets if response headers are accessible.
    /// </summary>
    public Boolean HasResponseHeaders => _readyState == RequesterState.Loading || _readyState == RequesterState.Done;

    /// <summary>
    /// Adds or removes the handler for the readystatechange event.
    /// </summary>
    [DomName("onreadystatechange")]
    public event DomEventHandler ReadyStateChanged
    {
        add { AddEventListener(ReadyStateChangeEvent, value, false); }
        remove { RemoveEventListener(ReadyStateChangeEvent, value, false); }
    }

    /// <summary>
    /// Gets the current ready state.
    /// </summary>
    [DomName("readyState")]
    public RequesterState ReadyState
    {
        get => _readyState;
        private set
        {
            _readyState = value;
            Fire(ReadyStateChangeEvent);
        }
    }

    /// <summary>
    /// Gets or sets the timeout of the request in milliseconds.
    /// </summary>
    [DomName("timeout")]
    public Int32 Timeout
    {
        get => _timeout;
        set => _timeout = value;
    }

    /// <summary>
    /// Gets the associated upload process.
    /// </summary>
    [DomName("upload")]
    public XmlHttpRequestUpload Upload => _upload;

    /// <summary>
    /// Gets or sets if credentials should be used for the request.
    /// </summary>
    [DomName("withCredentials")]
    public Boolean WithCredentials
    {
        get => _credentials;
        set => _credentials = value;
    }

    /// <summary>
    /// Gets the determined response type.
    /// </summary>
    [DomName("responseType")]
    public XmlHttpRequestResponseType ResponseType => XmlHttpRequestResponseType.None;

    /// <summary>
    /// Gets the url of the response.
    /// </summary>
    [DomName("responseURL")]
    public String ResponseUrl => _responseUrl;

    /// <summary>
    /// Gets the status code of the response.
    /// </summary>
    [DomName("status")]
    public Int32 StatusCode => (Int32)_responseStatus;

    /// <summary>
    /// Gets the status text of the response.
    /// </summary>
    [DomName("statusText")]
    public String StatusText => StatusCode != 0 ? _responseStatus.ToString() : String.Empty;

    /// <summary>
    /// Gets the resulting response object.
    /// </summary>
    [DomName("response")]
    public Object Response => null;

    /// <summary>
    /// Gets the body text of the response, which grows while the response is loading.
    /// </summary>
    [DomName("responseText")]
    public String ResponseText => _responseText;

    /// <summary>
    /// Gets the XML document of the response, if any.
    /// </summary>
    [DomName("responseXML")]
    public IDocument ResponseXml => null;

    #endregion

    #region Methods

    /// <summary>
    /// Aborts the request.
    /// </summary>
    [DomName("abort")]
    public void Abort()
    {
        if (_readyState == RequesterState.HeadersReceived || _readyState == RequesterState.Loading)
        {
            _aborted = true;
            _download?.Cancel();
            FinishUpload(AbortEvent);
            ReadyState = RequesterState.Done;
            FireProgress(AbortEvent);
            FireProgress(LoadEndEvent);
        }
    }

    /// <summary>
    /// Opens a new request with the provided method and URL.
    /// </summary>
    /// <param name="method">The method to use.</param>
    /// <param name="url">The URL to send to request to.</param>
    /// <param name="async">Should the request be send async?</param>
    /// <param name="username">Should a username be used?</param>
    /// <param name="password">Should a password be used?</param>
    [DomName("open")]
    public void Open(String method, String url, Boolean async = true, String username = null, String password = null)
    {
        if (_readyState == RequesterState.Unsent)
        {
            ReadyState = RequesterState.Opened;

            if (!Enum.TryParse(method, true, out _method))
            {
                _method = HttpMethod.Get;
            }

            _url = new Url(new Url(_window.Document.Url), url);
            _async = async;
            _url.UserName = username;
            _url.Password = password;
        }
    }

    /// <summary>
    /// Sends the created request with the potentially provided object.
    /// </summary>
    /// <param name="body">The body to send (e.g., for forms POST).</param>
    [DomName("send")]
    public void Send(Object body = null)
    {
        if (_readyState == RequesterState.Opened)
        {
            var requestBody = RequestBody.Serialize(body);

            if (!String.IsNullOrEmpty(requestBody.ContentType) && !ContainsHeader(_headers, HeaderNames.ContentType))
            {
                _headers[HeaderNames.ContentType] = requestBody.ContentType;
            }

            var loader = GetLoader();

            if (!FileUrlPolicy.IsAllowed(new Url(_window.Document.Url), _url))
            {
                _headers.Clear();
                ReadyState = RequesterState.Done;
                FireProgress(ErrorEvent);
                FireProgress(LoadEndEvent);
            }
            else if (loader != null)
            {
                var content = requestBody.Content;
                _hasUploadBody = content != Stream.Null && (!content.CanSeek || content.Length > 0);
                _uploadComplete = !_hasUploadBody;

                if (_hasUploadBody)
                {
                    content = CreateUploadStream(content);
                }

                var request = new DocumentRequest(_url)
                {
                    Body = content,
                    Method = _method,
                    MimeType = default,
                    Referer = _window.Document.DocumentUri,
                };

                foreach (var header in _headers)
                {
                    request.Headers[header.Key] = header.Value;
                }

                _headers.Clear();

                FireProgress(LoadStartEvent);

                if (_hasUploadBody)
                {
                    FireUpload(LoadStartEvent, 0, _uploadTotal);
                }

                var connection = Receive(loader, request);

                if (!_async)
                {
                    connection.Wait();
                }
            }
        }
    }

    /// <summary>
    /// Sets the request header.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <param name="value">The value of the field.</param>
    [DomName("setRequestHeader")]
    public void SetRequestHeader(String name, String value)
    {
        if (_readyState == RequesterState.Opened)
        {
            _headers[name] = value;
        }
    }

    /// <summary>
    /// Gets the response header.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns>The value of the field.</returns>
    [DomName("getResponseHeader")]
    public String GetResponseHeader(String name)
    {
        if (HasResponseHeaders && _headers.TryGetValue(name, out string value))
        {
            return value;
        }

        return String.Empty;
    }

    /// <summary>
    /// Gets all response headers.
    /// </summary>
    /// <returns>The name and values.</returns>
    [DomName("getAllResponseHeaders")]
    public String GetAllResponseHeaders()
    {
        if (HasResponseHeaders)
        {
            var headers = _headers;
            var lines = new String[headers.Count];
            var index = 0;

            foreach (var header in headers)
            {
                lines[index] = String.Concat(header.Key, ": ", header.Value);
                index++;
            }

            return String.Join(Environment.NewLine, lines);
        }

        return String.Empty;
    }

    /// <summary>
    /// Overrides the returned mime-type to force a specific mode.
    /// </summary>
    /// <param name="mime">The mime-type to use.</param>
    [DomName("overrideMimeType")]
    public void OverrideMimeType(String mime)
    {
        if (_readyState == RequesterState.Opened)
        {
            _mime = mime;
        }
    }

    #endregion

    #region Helpers

    private async Task Receive(IDocumentLoader loader, DocumentRequest request)
    {
        try
        {
            _download = loader.FetchAsync(request);

            using (var response = await _download.Task.ConfigureAwait(false))
            {
                if (_aborted)
                {
                    return;
                }

                if (response != null && FileUrlPolicy.IsAllowed(new Url(_window.Document.Url), response.Address))
                {
                    foreach (var header in response.Headers)
                    {
                        _headers[header.Key] = header.Value;
                    }

                    _responseUrl = response.Address.Href;
                    _responseStatus = response.StatusCode;
                    ReadyState = RequesterState.HeadersReceived;
                    await ReadBody(response.Content).ConfigureAwait(false);

                    if (!_aborted)
                    {
                        FinishUpload(LoadEvent);
                        ReadyState = RequesterState.Done;
                        FireProgress(LoadEvent);
                        FireProgress(LoadEndEvent);
                    }
                }
                else
                {
                    Fail(ErrorEvent);
                }
            }
        }
        catch (TaskCanceledException)
        {
            Fail(TimeoutEvent);
        }
        catch (Exception)
        {
            Fail(ErrorEvent);
        }
    }

    private async Task ReadBody(Stream content)
    {
        SetResponseTotal();
        ReadyState = RequesterState.Loading;

        var encoding = GetResponseEncoding();
        var decoder = encoding.GetDecoder();
        var text = new StringBuilder();
        var buffer = new Byte[BufferSize];
        var chars = new Char[encoding.GetMaxCharCount(BufferSize)];
        var sawFirst = false;
        var lastProgress = DateTime.UtcNow;

        if (content != null)
        {
            while (true)
            {
                var count = await content.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                if (count == 0 || _aborted)
                {
                    break;
                }

                var offset = 0;

                //  the leading byte order mark is part of the transport, not the text
                if (!sawFirst && encoding.CodePage == Encoding.UTF8.CodePage &&
                    count >= 3 && buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf)
                {
                    offset = 3;
                }

                sawFirst = true;
                _loaded += count;
                var written = decoder.GetChars(buffer, offset, count - offset, chars, 0);
                text.Append(chars, 0, written);
                var now = DateTime.UtcNow;

                if (now - lastProgress >= ProgressInterval)
                {
                    lastProgress = now;
                    _responseText = text.ToString();
                    FireProgress(ProgressEvent);
                }
            }
        }

        _responseText = text.ToString();

        if (!_aborted)
        {
            FireProgress(ProgressEvent);
        }
    }

    private void Fail(String eventName)
    {
        if (!_aborted)
        {
            FinishUpload(eventName);
            ReadyState = RequesterState.Done;
            FireProgress(eventName);
            FireProgress(LoadEndEvent);
        }
    }

    private void SetResponseTotal()
    {
        //  a content encoding makes the transported length differ from the received bytes
        if (_headers.TryGetValue(HeaderNames.ContentLength, out var value) &&
            Int64.TryParse(value, out var length) &&
            !_headers.ContainsKey(HeaderNames.ContentEncoding))
        {
            _total = length;
            _lengthComputable = true;
        }
    }

    private Encoding GetResponseEncoding()
    {
        var contentType = _mime ?? (_headers.TryGetValue(HeaderNames.ContentType, out var value) ? value : null);

        if (contentType != null)
        {
            var charset = new MimeType(contentType).GetParameter("charset");

            if (!String.IsNullOrEmpty(charset) && EncodingSupport.IsSupported(charset))
            {
                return EncodingSupport.Resolve(charset);
            }
        }

        return EncodingSupport.Utf8;
    }

    private Stream CreateUploadStream(Stream content)
    {
        try
        {
            _uploadTotal = content.CanSeek ? content.Length : 0;
        }
        catch (NotSupportedException)
        {
            _uploadTotal = 0;
        }

        var lastProgress = DateTime.UtcNow;
        return new ProgressReportingStream(content, (loaded, completed) =>
        {
            _uploadLoaded = loaded;

            if (completed)
            {
                FinishUpload(LoadEvent);
            }
            else if (!_uploadComplete)
            {
                var now = DateTime.UtcNow;

                if (now - lastProgress >= ProgressInterval)
                {
                    lastProgress = now;
                    FireUpload(ProgressEvent, loaded, _uploadTotal);
                }
            }
        });
    }

    private void FinishUpload(String eventName)
    {
        if (_hasUploadBody && !_uploadComplete)
        {
            _uploadComplete = true;
            var total = Math.Max(_uploadLoaded, _uploadTotal);
            FireUpload(eventName, _uploadLoaded, total);
            FireUpload(LoadEndEvent, _uploadLoaded, total);
        }
    }

    private void FireUpload(String eventName, Int64 loaded, Int64 total) =>
        Enqueue(() => _upload.Dispatch(new ProgressEvent(eventName, lengthComputable: total > 0, loaded: loaded, total: total)));

    private IEventLoop GetEventLoop() =>
        _window?.Document?.Context.GetService<IEventLoop>();

    private IDocumentLoader GetLoader() =>
        _window?.Document?.Context.GetService<IDocumentLoader>();

    private static Boolean ContainsHeader(Dictionary<String, String> headers, String name)
    {
        foreach (var pair in headers)
        {
            if (String.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void Fire(String eventName) =>
        Enqueue(() => Dispatch(new Event(eventName)));

    private void FireProgress(String eventName)
    {
        var loaded = _loaded;
        var total = _total;
        var lengthComputable = _lengthComputable;
        Enqueue(() => Dispatch(new ProgressEvent(eventName, lengthComputable: lengthComputable, loaded: loaded, total: total)));
    }

    private void Enqueue(Action action)
    {
        var loop = GetEventLoop();

        //  without a loop the events still have to fire, just right away
        if (loop != null)
        {
            loop.Enqueue(action);
        }
        else
        {
            action.Invoke();
        }
    }

    #endregion
}
