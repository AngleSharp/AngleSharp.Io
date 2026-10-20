namespace AngleSharp.Io.Reporting
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Keeps the received reports in memory, e.g., for tests.
    /// </summary>
    public sealed class MemoryReportSink : IReportSink
    {
        private readonly List<Report> _reports = new List<Report>();

        /// <summary>
        /// Gets a snapshot of the received reports.
        /// </summary>
        public IReadOnlyList<Report> Reports
        {
            get
            {
                lock (_reports)
                {
                    return _reports.ToArray();
                }
            }
        }

        /// <summary>
        /// Removes all received reports.
        /// </summary>
        public void Clear()
        {
            lock (_reports)
            {
                _reports.Clear();
            }
        }

        /// <inheritdoc />
        public Task SendAsync(IReadOnlyList<Report> reports, CancellationToken cancel)
        {
            lock (_reports)
            {
                _reports.AddRange(reports);
            }

            return Task.CompletedTask;
        }
    }
}
