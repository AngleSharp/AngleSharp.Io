namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Browser;
using AngleSharp.Dom;
using AngleSharp.Dom.Events;
using AngleSharp.Text;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Receives server-sent events, i.e., a text/event-stream response, reconnecting
/// when the connection ends. For more information see:
/// https://html.spec.whatwg.org/multipage/server-sent-events.html
/// </summary>
[DomName("EventSource")]
[DomExposed("Window")]
[DomExposed("DedicatedWorker")]
[DomExposed("SharedWorker")]
public sealed class EventSource : EventTarget
{
    #region Fields

    private const String EventStreamType = "text/event-stream";
    private static readonly TimeSpan DefaultRetry = TimeSpan.FromSeconds(3);

    private readonly IWindow _window;
    private readonly Url _url;
    private readonly Boolean _withCredentials;
    // the state changes on the event loop, but close may come from elsewhere (without one)
    private readonly Object _gate = new Object();

    private EventSourceReadyState _readyState;
    private TimeSpan _retry;
    private String _lastEventId;
    private IDownload _download;

    #endregion

    #region Events

    /// <summary>
    /// The name of the open event.
    /// </summary>
    public static readonly String OpenEvent = "open";

    /// <summary>
    /// Adds or removes the handler for the open event.
    /// </summary>
    [DomName("onopen")]
    public event DomEventHandler Opened
    {
        add { AddEventListener(OpenEvent, value, false); }
        remove { RemoveEventListener(OpenEvent, value, false); }
    }

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
    /// Adds or removes the handler for the error event.
    /// </summary>
    [DomName("onerror")]
    public event DomEventHandler Error
    {
        add { AddEventListener(EventNames.Error, value, false); }
        remove { RemoveEventListener(EventNames.Error, value, false); }
    }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a new event source, which connects right away.
    /// </summary>
    /// <param name="window">The parent context.</param>
    /// <param name="url">The URL of the event stream.</param>
    /// <param name="init">The options.</param>
    [DomConstructor]
    public EventSource(IWindow window, String url, EventSourceInit init = null)
    {
        _window = window;
        _url = new Url(new Url(window.Document.Url), url ?? String.Empty);

        if (_url.IsInvalid)
        {
            throw new DomException(DomError.Syntax);
        }

        _withCredentials = init?.WithCredentials ?? false;
        _readyState = EventSourceReadyState.Connecting;
        _retry = DefaultRetry;
        _lastEventId = String.Empty;
        _window.Unloaded += OnUnload;
        // it connects once the script that created it could add its listeners
        Defer(Connect);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the URL of the event stream.
    /// </summary>
    [DomName("url")]
    public String Url => _url.Href;

    /// <summary>
    /// Gets if credentials are sent with a request to another origin.
    /// </summary>
    [DomName("withCredentials")]
    public Boolean WithCredentials => _withCredentials;

    /// <summary>
    /// Gets the state of the connection.
    /// </summary>
    [DomName("readyState")]
    public EventSourceReadyState ReadyState => _readyState;

    #endregion

    #region Methods

    /// <summary>
    /// Closes the connection, which is not established again.
    /// </summary>
    [DomName("close")]
    public void Close()
    {
        IDownload download;

        lock (_gate)
        {
            _readyState = EventSourceReadyState.Closed;
            download = _download;
            _download = null;
        }

        download?.Cancel();
    }

    #endregion

    #region Helpers

    private void Connect()
    {
        var loader = _window.Document?.Context.GetService<IDocumentLoader>();

        if (_readyState == EventSourceReadyState.Closed)
        {
            return;
        }

        if (loader == null || !FileUrlPolicy.IsAllowed(new Url(_window.Document.Url), _url))
        {
            FailConnection();
            return;
        }

        var request = new DocumentRequest(_url)
        {
            Referer = _window.Document.DocumentUri,
        };
        request.Headers[HeaderNames.Accept] = EventStreamType;
        request.Headers[HeaderNames.CacheControl] = "no-cache";

        if (_lastEventId.Length > 0)
        {
            request.Headers["Last-Event-ID"] = _lastEventId;
        }

        ReceiveAsync(loader, request).Forget();
    }

    private async Task ReceiveAsync(IDocumentLoader loader, DocumentRequest request)
    {
        var download = loader.FetchAsync(request);
        _download = download;

        try
        {
            using (var response = await download.Task.ConfigureAwait(false))
            {
                if (_download != download)
                {
                    return;
                }

                var contentType = response?.Headers.TryGetValue(HeaderNames.ContentType, out var value) == true ? value : String.Empty;

                if (response == null || response.StatusCode != HttpStatusCode.OK || !new MimeType(contentType).Content.Isi(EventStreamType))
                {
                    //  anything but a stream of events ends it for good
                    Enqueue(FailConnection);
                    return;
                }

                Enqueue(Announce);
                await ReadEventsAsync(response.Content, download).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            //  a network error is retried, like the end of the stream
        }

        if (_download == download)
        {
            Enqueue(Reconnect);
        }
    }

    private async Task ReadEventsAsync(Stream content, IDownload download)
    {
        if (content == null)
        {
            return;
        }

        //  the stream is always UTF-8, a leading BOM is skipped by the reader
        using (var reader = new StreamReader(content, Encoding.UTF8, true))
        {
            var data = new StringBuilder();
            var eventType = String.Empty;
            var lastEventId = _lastEventId;
            var line = default(String);

            while (_download == download && (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (line.Length == 0)
                {
                    lastEventId = DispatchEvent(data, eventType, lastEventId);
                    eventType = String.Empty;
                }
                else if (line[0] != ':')
                {
                    var colon = line.IndexOf(':');
                    var field = colon < 0 ? line : line.Substring(0, colon);
                    var fieldValue = colon < 0 ? String.Empty : line.Substring(colon + 1);

                    if (fieldValue.Length > 0 && fieldValue[0] == ' ')
                    {
                        fieldValue = fieldValue.Substring(1);
                    }

                    switch (field)
                    {
                        case "event":
                            eventType = fieldValue;
                            break;
                        case "data":
                            data.Append(fieldValue).Append('\n');
                            break;
                        case "id":
                            if (fieldValue.IndexOf('\0') < 0)
                            {
                                lastEventId = fieldValue;
                            }
                            break;
                        case "retry":
                            if (fieldValue.Length > 0 && Int64.TryParse(fieldValue, out var retry) && retry >= 0 && fieldValue.IndexOf('-') < 0 && fieldValue.IndexOf('+') < 0)
                            {
                                _retry = TimeSpan.FromMilliseconds(retry);
                            }
                            break;
                    }
                }
            }
        }
    }

    private String DispatchEvent(StringBuilder data, String eventType, String lastEventId)
    {
        _lastEventId = lastEventId;

        if (data.Length > 0)
        {
            //  the last line feed of the data is not part of it
            var message = data.ToString(0, data.Length - 1);
            var type = eventType.Length > 0 ? eventType : EventNames.Message;
            var origin = _url.Origin;
            data.Clear();

            Enqueue(() =>
            {
                if (_readyState != EventSourceReadyState.Closed)
                {
                    var evt = new MessageEvent();
                    evt.Init(type, false, false, message, origin, lastEventId, null);
                    Dispatch(evt);
                }
            });
        }

        return lastEventId;
    }

    private void Announce()
    {
        if (TryChangeState(EventSourceReadyState.Open, keepsDownload: true))
        {
            Dispatch(new Event(OpenEvent));
        }
    }

    private void Reconnect()
    {
        var download = _download;

        if (TryChangeState(EventSourceReadyState.Connecting, keepsDownload: true))
        {
            Dispatch(new Event(EventNames.Error));

            Task.Delay(_retry).ContinueWith(_ =>
            {
                if (_download == download)
                {
                    Enqueue(Connect);
                }
            }).Forget();
        }
    }

    private void FailConnection()
    {
        if (TryChangeState(EventSourceReadyState.Closed, keepsDownload: false))
        {
            Dispatch(new Event(EventNames.Error));
        }
    }

    private Boolean TryChangeState(EventSourceReadyState state, Boolean keepsDownload)
    {
        lock (_gate)
        {
            if (_readyState == EventSourceReadyState.Closed)
            {
                return false;
            }

            _readyState = state;

            if (!keepsDownload)
            {
                _download = null;
            }

            return true;
        }
    }

    private void OnUnload(Object sender, Event ev)
    {
        RemoveEventListeners();
        Close();
    }

    private void Defer(Action action)
    {
        var loop = _window.Document?.Context.GetService<IEventLoop>();

        if (loop != null)
        {
            loop.Enqueue(action);
        }
        else
        {
            Task.Run(action).Forget();
        }
    }

    private void Enqueue(Action action)
    {
        var loop = _window.Document?.Context.GetService<IEventLoop>();

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
