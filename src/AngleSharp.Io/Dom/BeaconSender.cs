namespace AngleSharp.Io.Dom
{
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Sends beacons and keeps track of the pending bytes per browsing
    /// context, which stands in for the spec's fetch group.
    /// </summary>
    sealed class BeaconSender
    {
        private static readonly ConditionalWeakTable<IBrowsingContext, BeaconSender> Senders = new ConditionalWeakTable<IBrowsingContext, BeaconSender>();

        private static readonly String[] SafelistedContentTypes = new[]
        {
            "application/x-www-form-urlencoded",
            "multipart/form-data",
            "text/plain",
        };

        private readonly IBrowsingContext _context;
        private readonly Queue<Beacon> _queue;

        private Int64 _pending;
        private Int32 _running;

        private BeaconSender(IBrowsingContext context)
        {
            _context = context;
            _queue = new Queue<Beacon>();
        }

        public static Boolean Send(IBrowsingContext context, String url, Object data)
        {
            var document = context?.Active;

            if (document is null)
            {
                return false;
            }

            var documentUrl = new Url(document.Url);
            var target = new Url(document.BaseUrl ?? documentUrl, url ?? String.Empty);

            // the spec throws a TypeError for these, the payload related
            // rejections below only return false
            if (target.IsInvalid || (target.Scheme != ProtocolNames.Http && target.Scheme != ProtocolNames.Https))
            {
                throw new InvalidOperationException("The beacon URL must be a valid http or https URL.");
            }

            var loader = context.GetService<IDocumentLoader>();

            if (loader is null || !TryExtract(data, out var body, out var contentType))
            {
                return false;
            }

            var options = context.GetService<BeaconOptions>() ?? new BeaconOptions();
            var sender = Senders.GetValue(context, c => new BeaconSender(c));
            var origin = String.IsNullOrEmpty(documentUrl.Origin) ? "null" : documentUrl.Origin;
            var beacon = new Beacon(loader, target, body, contentType, origin, document.DocumentUri);
            return sender.TryEnqueue(beacon, options);
        }

        private Boolean TryEnqueue(Beacon beacon, BeaconOptions options)
        {
            var length = beacon.Body.LongLength;
            var start = false;

            lock (_queue)
            {
                if (length > 0 && _pending + length > options.Quota)
                {
                    return false;
                }

                _pending += length;

                if (options.MaxConcurrentRequests <= 0 || _running < options.MaxConcurrentRequests)
                {
                    _running++;
                    start = true;
                }
                else
                {
                    _queue.Enqueue(beacon);
                }
            }

            if (start)
            {
                _ = RunAsync(beacon);
            }

            return true;
        }

        private async Task RunAsync(Beacon beacon)
        {
            // nobody awaits a beacon, so failures are swallowed here and only
            // release the quota, like a network error would in browsers
            try
            {
                if (!beacon.RequiresPreflight || await PreflightAsync(beacon).ConfigureAwait(false))
                {
                    var download = beacon.Loader.FetchAsync(beacon.CreateRequest());
                    var response = await download.Task.ConfigureAwait(false);
                    response?.Dispose();
                }
            }
            catch
            {
            }

            var next = default(Beacon);

            lock (_queue)
            {
                _pending -= beacon.Body.LongLength;

                if (_queue.Count > 0)
                {
                    next = _queue.Dequeue();
                }
                else
                {
                    _running--;
                }
            }

            if (next is not null)
            {
                _ = RunAsync(next);
            }
        }

        private async Task<Boolean> PreflightAsync(Beacon beacon)
        {
            // the preflight goes to the requester directly as it must be
            // sent without credentials, i.e., without the loader's cookies
            var requester = _context.GetServices<IRequester>().FirstOrDefault(m => m.SupportsProtocol(beacon.Target.Scheme));

            if (requester is null)
            {
                return false;
            }

            var request = new Request
            {
                Method = HttpMethod.Options,
                Address = beacon.Target,
                Content = Stream.Null,
            };

            request.Headers[HeaderNames.Origin] = beacon.Origin;
            request.Headers["Access-Control-Request-Method"] = "POST";
            request.Headers["Access-Control-Request-Headers"] = "content-type";

            using (var response = await requester.RequestAsync(request, CancellationToken.None).ConfigureAwait(false))
            {
                return response is not null && IsPreflightSuccessful(response, beacon.Origin);
            }
        }

        private static Boolean IsPreflightSuccessful(IResponse response, String origin)
        {
            var status = (Int32)response.StatusCode;

            if (status < 200 || status > 299)
            {
                return false;
            }

            // beacons are sent with credentials, which rules out the
            // wildcards for the allowed origin and headers
            var allowedOrigin = GetHeader(response, HeaderNames.AccessControlAllowOrigin);
            var allowCredentials = GetHeader(response, "Access-Control-Allow-Credentials");
            var allowedHeaders = GetHeader(response, "Access-Control-Allow-Headers") ?? String.Empty;

            return allowedOrigin == origin &&
                allowCredentials == "true" &&
                allowedHeaders.Split(',').Any(name => name.Trim().Equals("content-type", StringComparison.OrdinalIgnoreCase));
        }

        private static String GetHeader(IResponse response, String name)
        {
            foreach (var pair in response.Headers)
            {
                if (pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value?.Trim();
                }
            }

            return null;
        }

        private static Boolean IsSafelistedContentType(String contentType)
        {
            if (contentType is null)
            {
                return true;
            }

            if (Encoding.UTF8.GetByteCount(contentType) > 128)
            {
                return false;
            }

            var separator = contentType.IndexOf(';');
            var essence = (separator < 0 ? contentType : contentType.Substring(0, separator)).Trim();
            return SafelistedContentTypes.Any(type => type.Equals(essence, StringComparison.OrdinalIgnoreCase));
        }

        private static Boolean TryExtract(Object data, out Byte[] body, out String contentType)
        {
            body = Array.Empty<Byte>();
            contentType = null;

            switch (data)
            {
                case null:
                    return true;
                case String text:
                    body = Encoding.UTF8.GetBytes(text);
                    contentType = "text/plain;charset=UTF-8";
                    return true;
                case Stream _:
                    // like a ReadableStream, a stream cannot be used for a
                    // keepalive request as its length is unknown upfront
                    return false;
                case IBlob blob when blob.Body is null:
                    contentType = String.IsNullOrEmpty(blob.Type) ? null : blob.Type;
                    return true;
            }

            try
            {
                var serialized = RequestBody.Serialize(data);

                using (var buffer = new MemoryStream())
                {
                    serialized.Content.CopyTo(buffer);
                    body = buffer.ToArray();
                }

                contentType = String.IsNullOrEmpty(serialized.ContentType) ? null : serialized.ContentType;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private sealed class Beacon
        {
            public Beacon(IDocumentLoader loader, Url target, Byte[] body, String contentType, String origin, String referer)
            {
                Loader = loader;
                Target = target;
                Body = body;
                ContentType = contentType;
                Origin = origin;
                Referer = referer;

                // a non-safelisted content type switches the request to the
                // cors mode, which needs a preflight for other origins
                RequiresPreflight = !IsSafelistedContentType(contentType) && target.Origin != origin;
            }

            public IDocumentLoader Loader { get; }

            public Url Target { get; }

            public Byte[] Body { get; }

            public String ContentType { get; }

            public String Origin { get; }

            public String Referer { get; }

            public Boolean RequiresPreflight { get; }

            public DocumentRequest CreateRequest()
            {
                var request = new DocumentRequest(Target)
                {
                    Method = HttpMethod.Post,
                    Body = Body.Length > 0 ? new MemoryStream(Body, false) : Stream.Null,
                    Referer = Referer,
                };

                request.Headers[HeaderNames.Origin] = Origin;

                if (ContentType is not null)
                {
                    request.Headers[HeaderNames.ContentType] = ContentType;
                }

                return request;
            }
        }
    }
}
