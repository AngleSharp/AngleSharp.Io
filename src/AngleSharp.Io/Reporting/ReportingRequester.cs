namespace AngleSharp.Io.Reporting
{
    using AngleSharp.Dom;
    using System;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A requester reporting network errors of the wrapped requester and
    /// learning the endpoints announced via the Reporting-Endpoints header.
    /// </summary>
    public class ReportingRequester : BaseRequester
    {
        #region Fields

        private readonly IRequester _inner;
        private readonly ReportingService _service;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new reporting requester.
        /// </summary>
        /// <param name="inner">The requester used for network requests.</param>
        /// <param name="service">The service receiving the reports.</param>
        public ReportingRequester(IRequester inner, ReportingService service)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the wrapped requester.
        /// </summary>
        public IRequester Inner => _inner;

        /// <summary>
        /// Gets the service receiving the reports.
        /// </summary>
        public ReportingService Service => _service;

        #endregion

        #region Methods

        /// <summary>
        /// Checks if the given protocol is supported by the wrapped requester.
        /// </summary>
        /// <param name="protocol">The protocol to check for, e.g., http.</param>
        /// <returns>True if the protocol is supported, otherwise false.</returns>
        public override Boolean SupportsProtocol(String protocol) => _inner.SupportsProtocol(protocol);

        /// <summary>
        /// Performs the request using the wrapped requester.
        /// </summary>
        /// <param name="request">The options to consider.</param>
        /// <param name="cancel">The token for cancelling the task.</param>
        /// <returns>The task that will eventually give the response data.</returns>
        protected override async Task<IResponse> PerformRequestAsync(Request request, CancellationToken cancel)
        {
            var stopwatch = Stopwatch.StartNew();
            IResponse response;

            try
            {
                response = await _inner.RequestAsync(request, cancel).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ReportFailure(request, ex, cancel, stopwatch.ElapsedMilliseconds);
                throw;
            }

            var status = (Int32)(response?.StatusCode ?? 0);

            if (status == 0)
            {
                ReportFailure(request, NetworkFailures.Get(response), cancel, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                if (response.Headers.TryGetValue("Reporting-Endpoints", out var endpoints))
                {
                    _service.LearnEndpoints(response.Address ?? request.Address, endpoints);
                }

                if (status >= 400 && _service.Options.CaptureHttpErrors)
                {
                    Report(request, "application", "http.error", status, stopwatch.ElapsedMilliseconds);
                }
            }

            return response;
        }

        #endregion

        #region Helpers

        private void ReportFailure(Request request, Exception error, CancellationToken cancel, Int64 elapsed)
        {
            if (_service.Options.CaptureNetworkErrors)
            {
                var (phase, type) = Classify(error, cancel);
                Report(request, phase, type, 0, elapsed);
            }
        }

        private void Report(Request request, String phase, String type, Int32 status, Int64 elapsed)
        {
            // like NEL, the report omits credentials and the fragment
            var url = new Url(request.Address)
            {
                Fragment = null,
                UserName = null,
                Password = null,
            };

            var body = new NetworkErrorReportBody
            {
                ElapsedTime = elapsed,
                Phase = phase,
                Type = type,
                Referrer = GetHeader(request, HeaderNames.Referer),
                Method = request.Method.ToString().ToUpperInvariant(),
                StatusCode = status,
            };

            var report = new Report(ReportTypes.NetworkError, url.Href, body, _service.Options.NetworkErrorDestination)
            {
                UserAgent = GetHeader(request, HeaderNames.UserAgent),
            };

            _service.QueueReport(report);
        }

        private static String GetHeader(Request request, String name)
        {
            foreach (var pair in request.Headers)
            {
                if (pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value ?? String.Empty;
                }
            }

            return String.Empty;
        }

        internal static (String Phase, String Type) Classify(Exception error, CancellationToken cancel)
        {
            if (error is OperationCanceledException)
            {
                return cancel.IsCancellationRequested ? ("application", "abandoned") : ("connection", "tcp.timed_out");
            }

            for (var current = error; current is not null; current = current.InnerException)
            {
                switch (current)
                {
                    case SocketException socket:
                        return ClassifySocketError(socket.SocketErrorCode);
                    case AuthenticationException _:
                        return ("connection", "tls.failed");
                    case TimeoutException _:
                        return ("connection", "tcp.timed_out");
                }
            }

            return ("application", "unknown");
        }

        private static (String Phase, String Type) ClassifySocketError(SocketError error) => error switch
        {
            SocketError.HostNotFound => ("dns", "dns.name_not_resolved"),
            SocketError.NoData => ("dns", "dns.name_not_resolved"),
            SocketError.TryAgain => ("dns", "dns.failed"),
            SocketError.ConnectionRefused => ("connection", "tcp.refused"),
            SocketError.ConnectionReset => ("connection", "tcp.reset"),
            SocketError.ConnectionAborted => ("connection", "tcp.aborted"),
            SocketError.TimedOut => ("connection", "tcp.timed_out"),
            SocketError.HostUnreachable => ("connection", "tcp.address_unreachable"),
            SocketError.NetworkUnreachable => ("connection", "tcp.address_unreachable"),
            SocketError.AddressNotAvailable => ("connection", "tcp.address_invalid"),
            _ => ("connection", "tcp.failed"),
        };

        #endregion
    }
}
