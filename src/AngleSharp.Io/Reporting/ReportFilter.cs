namespace AngleSharp.Io.Reporting
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Selects reports by their type and destination endpoint. An empty set
    /// matches every report.
    /// </summary>
    public sealed class ReportFilter
    {
        /// <summary>
        /// Gets the report types to match, e.g., network-error.
        /// </summary>
        public ISet<String> Types { get; } = new HashSet<String>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the names of the destination endpoints to match.
        /// </summary>
        public ISet<String> Destinations { get; } = new HashSet<String>(StringComparer.Ordinal);

        /// <summary>
        /// Creates a filter matching the given report types.
        /// </summary>
        /// <param name="types">The report types to match.</param>
        /// <returns>The new filter.</returns>
        public static ReportFilter ForTypes(params String[] types)
        {
            var filter = new ReportFilter();
            filter.Types.UnionWith(types ?? Array.Empty<String>());
            return filter;
        }

        /// <summary>
        /// Creates a filter matching the given destination endpoints.
        /// </summary>
        /// <param name="destinations">The names of the endpoints to match.</param>
        /// <returns>The new filter.</returns>
        public static ReportFilter ForDestinations(params String[] destinations)
        {
            var filter = new ReportFilter();
            filter.Destinations.UnionWith(destinations ?? Array.Empty<String>());
            return filter;
        }

        /// <summary>
        /// Checks if the given report matches the filter.
        /// </summary>
        /// <param name="report">The report to check.</param>
        /// <returns>True if the report matches, otherwise false.</returns>
        public Boolean Matches(Report report) =>
            report is not null &&
            (Types.Count == 0 || Types.Contains(report.Type)) &&
            (Destinations.Count == 0 || Destinations.Contains(report.Destination));
    }
}
