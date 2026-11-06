namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Browser;
using AngleSharp.Dom;
using AngleSharp.Text;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Reads a blob or file asynchronously, telling about it with events. For
/// more information see: https://w3c.github.io/FileAPI/#APIASynch
/// </summary>
[DomName("FileReader")]
[DomExposed("Window")]
[DomExposed("Worker")]
public sealed class FileReader : EventTarget
{
    #region Fields

    private const Int32 BufferSize = 16384;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(50);

    private readonly IWindow _window;
    private FileReaderState _readyState;
    private Object _result;
    private IDomException _error;
    private Int32 _generation;

    #endregion

    #region Event names

    internal static readonly String LoadStartEvent = "loadstart";
    internal static readonly String ProgressEvent = "progress";
    internal static readonly String LoadEvent = "load";
    internal static readonly String AbortEvent = "abort";
    internal static readonly String ErrorEvent = "error";
    internal static readonly String LoadEndEvent = "loadend";

    #endregion

    #region ctor

    /// <summary>
    /// Creates a new reader.
    /// </summary>
    [DomConstructor]
    public FileReader(IWindow window)
    {
        _window = window;
        _readyState = FileReaderState.Empty;
    }

    #endregion

    #region Events

    /// <summary>
    /// Adds or removes the handler for the loadstart event.
    /// </summary>
    [DomName("onloadstart")]
    public event DomEventHandler Started
    {
        add { AddEventListener(LoadStartEvent, value, false); }
        remove { RemoveEventListener(LoadStartEvent, value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the progress event.
    /// </summary>
    [DomName("onprogress")]
    public event DomEventHandler Progressed
    {
        add { AddEventListener(ProgressEvent, value, false); }
        remove { RemoveEventListener(ProgressEvent, value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the load event.
    /// </summary>
    [DomName("onload")]
    public event DomEventHandler Loaded
    {
        add { AddEventListener(LoadEvent, value, false); }
        remove { RemoveEventListener(LoadEvent, value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the abort event.
    /// </summary>
    [DomName("onabort")]
    public event DomEventHandler Aborted
    {
        add { AddEventListener(AbortEvent, value, false); }
        remove { RemoveEventListener(AbortEvent, value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the error event.
    /// </summary>
    [DomName("onerror")]
    public event DomEventHandler Failed
    {
        add { AddEventListener(ErrorEvent, value, false); }
        remove { RemoveEventListener(ErrorEvent, value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the loadend event.
    /// </summary>
    [DomName("onloadend")]
    public event DomEventHandler Ended
    {
        add { AddEventListener(LoadEndEvent, value, false); }
        remove { RemoveEventListener(LoadEndEvent, value, false); }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the state of the reader.
    /// </summary>
    [DomName("readyState")]
    public FileReaderState ReadyState => _readyState;

    /// <summary>
    /// Gets what was read, which is null until the read finishes: a string,
    /// or an ArrayBuffer for <see cref="ReadAsArrayBuffer"/>.
    /// </summary>
    [DomName("result")]
    public Object Result => _result;

    /// <summary>
    /// Gets why the read failed, if it did.
    /// </summary>
    [DomName("error")]
    public IDomException Error => _error;

    #endregion

    #region Methods

    /// <summary>
    /// Reads the blob as text: the byte order mark decides the encoding
    /// first, then the given one, then the blob's charset, then UTF-8.
    /// </summary>
    /// <param name="blob">The blob to read.</param>
    /// <param name="encoding">The label of the encoding to use.</param>
    [DomName("readAsText")]
    public void ReadAsText(IBlob blob, String encoding = null) =>
        Read(blob, bytes => DecodeText(bytes, encoding, blob.Type));

    /// <summary>
    /// Reads the blob into a data: URL carrying its type.
    /// </summary>
    /// <param name="blob">The blob to read.</param>
    [DomName("readAsDataURL")]
    public void ReadAsDataUrl(IBlob blob) =>
        Read(blob, bytes =>
        {
            var type = String.IsNullOrEmpty(blob.Type) ? "application/octet-stream" : blob.Type;
            return String.Concat("data:", type, ";base64,", Convert.ToBase64String(bytes));
        });

    /// <summary>
    /// Reads the blob's bytes, which scripts receive as an ArrayBuffer.
    /// </summary>
    /// <param name="blob">The blob to read.</param>
    [DomName("readAsArrayBuffer")]
    public void ReadAsArrayBuffer(IBlob blob) =>
        Read(blob, bytes => bytes);

    /// <summary>
    /// Reads the blob into a string with one character per byte.
    /// </summary>
    /// <param name="blob">The blob to read.</param>
    [DomName("readAsBinaryString")]
    public void ReadAsBinaryString(IBlob blob) =>
        Read(blob, bytes =>
        {
            var chars = new Char[bytes.Length];

            for (var i = 0; i < bytes.Length; i++)
            {
                chars[i] = (Char)bytes[i];
            }

            return new String(chars);
        });

    /// <summary>
    /// Aborts a running read: the result stays null.
    /// </summary>
    [DomName("abort")]
    public void Abort()
    {
        if (_readyState == FileReaderState.Loading)
        {
            _generation++;
            _readyState = FileReaderState.Done;
            _result = null;
            Fire(AbortEvent, 0, 0);
            Fire(LoadEndEvent, 0, 0);
        }
    }

    #endregion

    #region Helpers

    private void Read(IBlob blob, Func<Byte[], Object> convert)
    {
        if (blob == null)
        {
            throw new ArgumentNullException(nameof(blob));
        }

        if (_readyState == FileReaderState.Loading)
        {
            throw new DomException(DomError.InvalidState);
        }

        _readyState = FileReaderState.Loading;
        _result = null;
        _error = null;
        var generation = ++_generation;
        //  the task queue keeps the events after the call that started the read
        Task.Run(() => ReadBlob(blob, convert, generation));
    }

    private async Task ReadBlob(IBlob blob, Func<Byte[], Object> convert, Int32 generation)
    {
        var total = 0L;
        var loaded = 0L;

        try
        {
            total = blob.Length;
            Fire(LoadStartEvent, 0, total, generation);

            if (blob.IsClosed)
            {
                throw new DomException(DomError.InvalidState);
            }

            var body = blob.Body ?? Stream.Null;

            if (body.CanSeek)
            {
                body.Seek(0, SeekOrigin.Begin);
            }

            var buffer = new Byte[BufferSize];
            var lastProgress = DateTime.UtcNow;

            using (var content = new MemoryStream())
            {
                while (true)
                {
                    var count = await body.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                    if (count == 0 || generation != _generation)
                    {
                        break;
                    }

                    content.Write(buffer, 0, count);
                    loaded += count;
                    var now = DateTime.UtcNow;

                    if (now - lastProgress >= ProgressInterval)
                    {
                        lastProgress = now;
                        Fire(ProgressEvent, loaded, total, generation);
                    }
                }

                if (generation != _generation)
                {
                    return;
                }

                var result = convert.Invoke(content.ToArray());

                if (generation == _generation)
                {
                    _result = result;
                    _readyState = FileReaderState.Done;
                    Fire(ProgressEvent, loaded, total, generation);
                    Fire(LoadEvent, loaded, total, generation);
                    Fire(LoadEndEvent, loaded, total, generation);
                }
            }
        }
        catch (Exception error)
        {
            if (generation == _generation)
            {
                _error = error as IDomException ?? new DomException(error.Message);
                _result = null;
                _readyState = FileReaderState.Done;
                Fire(ErrorEvent, loaded, total, generation);
                Fire(LoadEndEvent, loaded, total, generation);
            }
        }
    }

    private static String DecodeText(Byte[] bytes, String label, String blobType)
    {
        var encoding = TextEncoding.Utf8;

        if (!String.IsNullOrEmpty(label) && TextEncoding.IsSupported(label))
        {
            encoding = TextEncoding.Resolve(label);
        }
        else if (!String.IsNullOrEmpty(blobType))
        {
            var charset = new MimeType(blobType).GetParameter("charset");

            if (!String.IsNullOrEmpty(charset) && TextEncoding.IsSupported(charset))
            {
                encoding = TextEncoding.Resolve(charset);
            }
        }

        //  the reader strips a byte order mark and takes its encoding over the given one
        using (var reader = new StreamReader(new MemoryStream(bytes), encoding, true))
        {
            return reader.ReadToEnd();
        }
    }

    private void Fire(String eventName, Int64 loaded, Int64 total, Int32? generation = null)
    {
        var loop = _window?.Document?.Context.GetService<IEventLoop>();
        var expected = generation ?? _generation;

        void Dispatch()
        {
            if (expected == _generation || eventName == AbortEvent || eventName == LoadEndEvent)
            {
                base.Dispatch(new ProgressEvent(eventName, lengthComputable: total > 0, loaded: loaded, total: total));
            }
        }

        if (loop != null)
        {
            loop.Enqueue(Dispatch);
        }
        else
        {
            Dispatch();
        }
    }

    #endregion
}

/// <summary>
/// Defines the states of a <see cref="FileReader"/>.
/// </summary>
[DomName("FileReader")]
public enum FileReaderState : ushort
{
    /// <summary>
    /// Nothing has been read yet.
    /// </summary>
    [DomName("EMPTY")]
    Empty = 0,
    /// <summary>
    /// A read is running.
    /// </summary>
    [DomName("LOADING")]
    Loading = 1,
    /// <summary>
    /// The read finished, successfully or not.
    /// </summary>
    [DomName("DONE")]
    Done = 2,
}
