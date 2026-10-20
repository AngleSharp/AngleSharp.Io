namespace AngleSharp.Io.Reporting
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Options for collecting and delivering reports.
    /// </summary>
    public sealed class ReportingOptions
    {
        /// <summary>
        /// Gets or sets the filter deciding which reports are queued. By
        /// default, this is null, which means that all reports are queued.
        /// </summary>
        public ReportFilter Filter { get; set; }

        /// <summary>
        /// Gets or sets if failed requests, i.e., requests without response,
        /// are reported as network-error. By default, this is true.
        /// </summary>
        public Boolean CaptureNetworkErrors { get; set; } = true;

        /// <summary>
        /// Gets or sets if responses with a status code of 400 or above are
        /// reported as network-error of type http.error. By default, this is
        /// true.
        /// </summary>
        public Boolean CaptureHttpErrors { get; set; } = true;

        /// <summary>
        /// Gets or sets the name of the endpoint receiving network-error
        /// reports. By default, this is "default".
        /// </summary>
        public String NetworkErrorDestination { get; set; } = Report.DefaultDestination;

        /// <summary>
        /// Gets the endpoints, i.e., names mapped to URLs, used when the
        /// origin of a report did not announce the endpoint via the
        /// Reporting-Endpoints header.
        /// </summary>
        public IDictionary<String, String> Endpoints { get; } = new Dictionary<String, String>(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the maximum number of queued reports. The oldest
        /// reports are dropped when the limit is reached.
        /// </summary>
        public Int32 MaxQueueSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the interval for flushing the queued reports
        /// automatically. By default, this is null, which means that reports
        /// are only flushed explicitly.
        /// </summary>
        public TimeSpan? FlushInterval { get; set; }

        /// <summary>
        /// Gets the sinks receiving the flushed reports.
        /// </summary>
        public IList<ReportSinkRegistration> Sinks { get; } = new List<ReportSinkRegistration>();

        /// <summary>
        /// Adds a sink receiving the flushed reports matching the filter.
        /// </summary>
        /// <param name="sink">The sink to add.</param>
        /// <param name="filter">The filter for the sink, or null for all reports.</param>
        /// <returns>The options.</returns>
        public ReportingOptions AddSink(IReportSink sink, ReportFilter filter = null)
        {
            Sinks.Add(new ReportSinkRegistration(sink, filter));
            return this;
        }

        /// <summary>
        /// Adds a callback receiving the flushed reports matching the filter.
        /// </summary>
        /// <param name="sink">The callback to add.</param>
        /// <param name="filter">The filter for the callback, or null for all reports.</param>
        /// <returns>The options.</returns>
        public ReportingOptions AddSink(Func<IReadOnlyList<Report>, CancellationToken, Task> sink, ReportFilter filter = null) =>
            AddSink(new DelegateReportSink(sink ?? throw new ArgumentNullException(nameof(sink))), filter);

        internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

        private sealed class DelegateReportSink : IReportSink
        {
            private readonly Func<IReadOnlyList<Report>, CancellationToken, Task> _sink;

            public DelegateReportSink(Func<IReadOnlyList<Report>, CancellationToken, Task> sink)
            {
                _sink = sink;
            }

            public Task SendAsync(IReadOnlyList<Report> reports, CancellationToken cancel) => _sink.Invoke(reports, cancel);
        }
    }

    /// <summary>
    /// Represents a sink together with the filter for its reports.
    /// </summary>
    public sealed class ReportSinkRegistration
    {
        /// <summary>
        /// Creates a new registration.
        /// </summary>
        /// <param name="sink">The sink receiving the reports.</param>
        /// <param name="filter">The filter for the reports, or null for all reports.</param>
        public ReportSinkRegistration(IReportSink sink, ReportFilter filter = null)
        {
            Sink = sink ?? throw new ArgumentNullException(nameof(sink));
            Filter = filter;
        }

        /// <summary>
        /// Gets the sink receiving the reports.
        /// </summary>
        public IReportSink Sink { get; }

        /// <summary>
        /// Gets the filter for the reports, or null for all reports.
        /// </summary>
        public ReportFilter Filter { get; }
    }
}
