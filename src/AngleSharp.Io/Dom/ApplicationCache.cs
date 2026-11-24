namespace AngleSharp.Io.Dom;

using AngleSharp.Attributes;
using AngleSharp.Browser;
using AngleSharp.Browser.Dom;
using AngleSharp.Dom;
using AngleSharp.Dom.Events;
using AngleSharp.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// The application cache (offline web applications) of a window, as it was in HTML before
/// it was removed in favor of service workers: the manifest of the document's html element
/// (its manifest attribute) lists the resources to keep, which are downloaded with the
/// document's loader (and so kept by the HTTP cache). It runs once script accesses it.
/// For more information see:
/// https://www.w3.org/TR/2011/WD-html5-20110525/offline.html
/// </summary>
public sealed class ApplicationCache : EventTarget, IApplicationCache
{
    #region Fields

    private static readonly ConditionalWeakTable<IWindow, ApplicationCache> Instances = new ConditionalWeakTable<IWindow, ApplicationCache>();
    // the manifests seen before (by their address), to tell an update from the first download
    private static readonly Dictionary<String, String> Manifests = new Dictionary<String, String>();

    private readonly IWindow _window;
    private CacheStatus _status;
    private String _pendingManifest;
    private Boolean _aborted;
    private Boolean _isRunning;

    #endregion

    #region Events

    /// <summary>
    /// Adds or removes the handler for the checking event.
    /// </summary>
    public event DomEventHandler Checking
    {
        add { AddEventListener("checking", value, false); }
        remove { RemoveEventListener("checking", value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the error event.
    /// </summary>
    public event DomEventHandler Error
    {
        add { AddEventListener(EventNames.Error, value, false); }
        remove { RemoveEventListener(EventNames.Error, value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the noupdate event.
    /// </summary>
    public event DomEventHandler NoUpdate
    {
        add { AddEventListener("noupdate", value, false); }
        remove { RemoveEventListener("noupdate", value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the downloading event.
    /// </summary>
    public event DomEventHandler Downloading
    {
        add { AddEventListener("downloading", value, false); }
        remove { RemoveEventListener("downloading", value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the progress event.
    /// </summary>
    public event DomEventHandler Progress
    {
        add { AddEventListener("progress", value, false); }
        remove { RemoveEventListener("progress", value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the updateready event.
    /// </summary>
    public event DomEventHandler UpdateReady
    {
        add { AddEventListener("updateready", value, false); }
        remove { RemoveEventListener("updateready", value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the cached event.
    /// </summary>
    public event DomEventHandler Cached
    {
        add { AddEventListener("cached", value, false); }
        remove { RemoveEventListener("cached", value, false); }
    }

    /// <summary>
    /// Adds or removes the handler for the obsolete event.
    /// </summary>
    public event DomEventHandler Obsolete
    {
        add { AddEventListener("obsolete", value, false); }
        remove { RemoveEventListener("obsolete", value, false); }
    }

    #endregion

    #region ctor

    private ApplicationCache(IWindow window)
    {
        _window = window;
        _status = CacheStatus.Uncached;

        // it starts once the script that got it could add its listeners
        if (ManifestUrl is not null)
        {
            var loop = window.Document?.Context.GetService<IEventLoop>();

            if (loop is not null)
            {
                loop.Enqueue(() => Run(isUpdate: false));
            }
            else
            {
                Task.Run(() => Run(isUpdate: false)).Forget();
            }
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the state of the cache.
    /// </summary>
    public CacheStatus Status => _status;

    private Url ManifestUrl
    {
        get
        {
            var document = _window.Document;
            var manifest = document?.DocumentElement?.GetAttribute("manifest");

            if (String.IsNullOrEmpty(manifest))
            {
                return null;
            }

            var url = new Url(new Url(document.Url), manifest);
            return url.IsInvalid || url.Origin != new Url(document.Url).Origin ? null : url;
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the application cache of the window.
    /// </summary>
    public static ApplicationCache Get(IWindow window) => Instances.GetValue(window, w => new ApplicationCache(w));

    /// <summary>
    /// Checks the manifest for a new version of the cache.
    /// </summary>
    public void Update()
    {
        if (_status == CacheStatus.Uncached || _status == CacheStatus.Obsolete)
        {
            throw new DomException(DomError.InvalidState);
        }

        Enqueue(() => Run(isUpdate: true));
    }

    /// <summary>
    /// Stops the download of the cache.
    /// </summary>
    public void Abort()
    {
        if (_isRunning)
        {
            _aborted = true;
        }
    }

    /// <summary>
    /// Uses the new version of the cache that is ready.
    /// </summary>
    public void Swap()
    {
        if (_status != CacheStatus.UpdateReady)
        {
            throw new DomException(DomError.InvalidState);
        }

        lock (Manifests)
        {
            Manifests[ManifestUrl.Href] = _pendingManifest;
        }

        _status = CacheStatus.Idle;
    }

    #endregion

    #region Helpers

    private async void Run(Boolean isUpdate)
    {
        var manifestUrl = ManifestUrl;
        var document = _window.Document;
        var loader = document?.Context.GetService<IDocumentLoader>();

        if (_isRunning || manifestUrl is null || loader is null)
        {
            return;
        }

        _isRunning = true;
        _aborted = false;
        var previous = default(String);

        lock (Manifests)
        {
            Manifests.TryGetValue(manifestUrl.Href, out previous);
        }

        var wasCached = previous is not null;
        _status = CacheStatus.Checking;
        Fire("checking");

        try
        {
            var (status, manifest) = await FetchTextAsync(loader, manifestUrl, document).ConfigureAwait(false);

            if (status == HttpStatusCode.NotFound || status == HttpStatusCode.Gone)
            {
                lock (Manifests)
                {
                    Manifests.Remove(manifestUrl.Href);
                }

                Enqueue(() =>
                {
                    _status = wasCached ? CacheStatus.Obsolete : CacheStatus.Uncached;
                    Fire(wasCached ? "obsolete" : EventNames.Error);
                });
                return;
            }

            if (status != HttpStatusCode.OK || manifest is null || !manifest.TrimStart('﻿').StartsWith("CACHE MANIFEST", StringComparison.Ordinal))
            {
                Fail(wasCached);
                return;
            }

            if (wasCached && previous == manifest)
            {
                Enqueue(() =>
                {
                    _status = CacheStatus.Idle;
                    Fire("noupdate");
                });
                return;
            }

            var entries = ParseExplicitEntries(manifest, manifestUrl);
            Enqueue(() =>
            {
                _status = CacheStatus.Downloading;
                Fire("downloading");
            });

            for (var i = 0; i < entries.Count; i++)
            {
                if (_aborted)
                {
                    Fail(wasCached);
                    return;
                }

                var (entryStatus, _) = await FetchTextAsync(loader, entries[i], document).ConfigureAwait(false);

                if (entryStatus != HttpStatusCode.OK)
                {
                    Fail(wasCached);
                    return;
                }

                var loaded = i + 1;
                Enqueue(() => Dispatch(new ProgressEvent("progress", lengthComputable: true, loaded: loaded, total: entries.Count)));
            }

            Enqueue(() =>
            {
                // a new version of a cache is used once swapped (or with the next document)
                if (wasCached)
                {
                    _pendingManifest = manifest;
                    _status = CacheStatus.UpdateReady;
                    Fire("updateready");
                }
                else
                {
                    lock (Manifests)
                    {
                        Manifests[manifestUrl.Href] = manifest;
                    }

                    _status = CacheStatus.Idle;
                    Fire("cached");
                }
            });
        }
        catch (Exception)
        {
            Fail(wasCached);
        }
        finally
        {
            _isRunning = false;
        }
    }

    private void Fail(Boolean wasCached) => Enqueue(() =>
    {
        _status = wasCached ? CacheStatus.Idle : CacheStatus.Uncached;
        Fire(EventNames.Error);
    });

    private static async Task<(HttpStatusCode, String)> FetchTextAsync(IDocumentLoader loader, Url url, IDocument document)
    {
        var request = new DocumentRequest(url)
        {
            Referer = document.DocumentUri,
        };

        using (var response = await loader.FetchAsync(request).Task.ConfigureAwait(false))
        {
            if (response?.Content is null)
            {
                return (response?.StatusCode ?? 0, null);
            }

            using (var reader = new StreamReader(response.Content, Encoding.UTF8))
            {
                return (response.StatusCode, await reader.ReadToEndAsync().ConfigureAwait(false));
            }
        }
    }

    /// <summary>
    /// Gets the explicit entries of the manifest (its CACHE section), which are of the same origin.
    /// </summary>
    internal static List<Url> ParseExplicitEntries(String manifest, Url manifestUrl)
    {
        var entries = new List<Url>();
        var lines = manifest.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        var section = "CACHE:";

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (line is "CACHE:" or "NETWORK:" or "FALLBACK:" or "SETTINGS:")
            {
                section = line;
            }
            else if (line.EndsWith(":", StringComparison.Ordinal) && line.IndexOf(' ') < 0)
            {
                // an unknown section, whose lines are ignored
                section = line;
            }
            else if (section == "CACHE:")
            {
                var token = line.Split(' ', '\t')[0];
                var url = new Url(manifestUrl, token);

                if (!url.IsInvalid && url.Origin == manifestUrl.Origin)
                {
                    url.Fragment = null;
                    entries.Add(url);
                }
            }
        }

        return entries;
    }

    private void Fire(String type) => Dispatch(new Event(type));

    private void Enqueue(Action action)
    {
        var loop = _window.Document?.Context.GetService<IEventLoop>();

        if (loop is not null)
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
