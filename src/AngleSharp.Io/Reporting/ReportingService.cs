namespace AngleSharp.Io.Reporting
{
    using AngleSharp.Dom;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Collects reports and delivers them to the configured sinks when
    /// flushed.
    /// </summary>
    public sealed class ReportingService : IDisposable
    {
        private readonly ReportingOptions _options;
        private readonly List<Report> _queue;
        private readonly Dictionary<String, Dictionary<String, String>> _endpointsByOrigin;
        private readonly Timer _timer;

        /// <summary>
        /// Creates a new reporting service.
        /// </summary>
        /// <param name="options">The options to use.</param>
        public ReportingService(ReportingOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _queue = new List<Report>();
            _endpointsByOrigin = new Dictionary<String, Dictionary<String, String>>(StringComparer.Ordinal);

            if (options.FlushInterval is TimeSpan interval && interval > TimeSpan.Zero)
            {
                _timer = new Timer(_ => FlushInBackground(), null, interval, interval);
            }
        }

        /// <summary>
        /// Raised after a report has been queued.
        /// </summary>
        public event EventHandler<ReportEventArgs> ReportQueued;

        /// <summary>
        /// Gets the used options.
        /// </summary>
        public ReportingOptions Options => _options;

        /// <summary>
        /// Queues the given report for delivery, unless it is excluded by the
        /// filter of the options.
        /// </summary>
        /// <param name="report">The report to queue.</param>
        /// <returns>True if the report was queued, otherwise false.</returns>
        public Boolean QueueReport(Report report)
        {
            if (report is null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (_options.Filter is not null && !_options.Filter.Matches(report))
            {
                return false;
            }

            report.Timestamp = _options.Clock.Invoke();
            report.EndpointUrl = ResolveEndpoint(report.Url, report.Destination);

            lock (_queue)
            {
                _queue.Add(report);

                if (_options.MaxQueueSize > 0 && _queue.Count > _options.MaxQueueSize)
                {
                    _queue.RemoveRange(0, _queue.Count - _options.MaxQueueSize);
                }
            }

            ReportQueued?.Invoke(this, new ReportEventArgs(report));
            return true;
        }

        /// <summary>
        /// Gets the queued reports matching the given filter.
        /// </summary>
        /// <param name="filter">The filter to apply, or null for all reports.</param>
        /// <returns>The reports in the order they were queued.</returns>
        public IReadOnlyList<Report> GetReports(ReportFilter filter = null)
        {
            lock (_queue)
            {
                return _queue.Where(m => filter is null || filter.Matches(m)).ToArray();
            }
        }

        /// <summary>
        /// Removes all queued reports and hands them to the sinks whose filter
        /// they match. The reports are removed even if a sink fails; the
        /// failure is rethrown after all sinks have been called.
        /// </summary>
        /// <param name="cancel">The token for cancelling the task.</param>
        /// <returns>The number of flushed reports.</returns>
        public async Task<Int32> FlushAsync(CancellationToken cancel = default)
        {
            Report[] reports;

            lock (_queue)
            {
                reports = _queue.ToArray();
                _queue.Clear();
            }

            var errors = new List<Exception>();

            foreach (var registration in _options.Sinks.ToArray())
            {
                var selected = reports.Where(m => registration.Filter is null || registration.Filter.Matches(m)).ToArray();

                if (selected.Length > 0)
                {
                    try
                    {
                        await registration.Sink.SendAsync(selected, cancel).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                }
            }

            if (errors.Count == 1)
            {
                throw errors[0];
            }
            else if (errors.Count > 1)
            {
                throw new AggregateException(errors);
            }

            return reports.Length;
        }

        /// <summary>
        /// Stops flushing automatically.
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
        }

        internal void LearnEndpoints(Url address, String header)
        {
            var origin = address?.Origin;

            if (String.IsNullOrEmpty(origin) || header is null)
            {
                return;
            }

            var endpoints = new Dictionary<String, String>(StringComparer.Ordinal);

            foreach (var member in SplitMembers(header))
            {
                var separator = member.IndexOf('=');

                if (separator > 0)
                {
                    var name = member.Substring(0, separator).Trim();
                    var value = member.Substring(separator + 1).Trim().Trim('"');
                    var url = new Url(address, value);

                    if (name.Length > 0 && !url.IsInvalid && (url.Scheme == ProtocolNames.Http || url.Scheme == ProtocolNames.Https))
                    {
                        endpoints[name] = url.Href;
                    }
                }
            }

            lock (_endpointsByOrigin)
            {
                _endpointsByOrigin[origin] = endpoints;
            }
        }

        internal String ResolveEndpoint(String reportUrl, String destination)
        {
            var origin = String.IsNullOrEmpty(reportUrl) ? null : new Url(reportUrl).Origin;

            lock (_endpointsByOrigin)
            {
                if (origin is not null && _endpointsByOrigin.TryGetValue(origin, out var endpoints) && endpoints.TryGetValue(destination, out var learned))
                {
                    return learned;
                }
            }

            return _options.Endpoints.TryGetValue(destination, out var configured) ? configured : null;
        }

        private void FlushInBackground()
        {
            // nobody observes the automatic flushes, failing sinks have to
            // wait for the next interval or an explicit flush
            FlushAsync().ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        private static IEnumerable<String> SplitMembers(String header)
        {
            var start = 0;
            var quoted = false;

            for (var i = 0; i < header.Length; i++)
            {
                if (header[i] == '"')
                {
                    quoted = !quoted;
                }
                else if (header[i] == ',' && !quoted)
                {
                    yield return header.Substring(start, i - start);
                    start = i + 1;
                }
            }

            yield return header.Substring(start);
        }
    }
}
