namespace AngleSharp.Io.Reporting
{
    using System;

    /// <summary>
    /// The arguments for the event raised when a report is queued.
    /// </summary>
    public sealed class ReportEventArgs : EventArgs
    {
        /// <summary>
        /// Creates new arguments for the given report.
        /// </summary>
        /// <param name="report">The queued report.</param>
        public ReportEventArgs(Report report)
        {
            Report = report;
        }

        /// <summary>
        /// Gets the queued report.
        /// </summary>
        public Report Report { get; }
    }
}
