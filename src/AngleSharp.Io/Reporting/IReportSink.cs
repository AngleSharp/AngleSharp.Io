namespace AngleSharp.Io.Reporting
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a backend receiving flushed reports, e.g., for storing or
    /// forwarding them.
    /// </summary>
    public interface IReportSink
    {
        /// <summary>
        /// Receives the given reports.
        /// </summary>
        /// <param name="reports">The reports in the order they were queued.</param>
        /// <param name="cancel">The token for cancelling the task.</param>
        /// <returns>The task representing the operation.</returns>
        Task SendAsync(IReadOnlyList<Report> reports, CancellationToken cancel);
    }
}
