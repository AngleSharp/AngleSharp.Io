namespace AngleSharp.Io.Reporting
{
    using AngleSharp.Attributes;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a report, e.g., about a network error or a deprecation.
    /// </summary>
    [DomName("Report")]
    public sealed class Report
    {
        /// <summary>
        /// The endpoint used when no destination is given.
        /// </summary>
        public static readonly String DefaultDestination = "default";

        /// <summary>
        /// Creates a new report.
        /// </summary>
        /// <param name="type">The type of the report, e.g., network-error.</param>
        /// <param name="url">The URL the report is about.</param>
        /// <param name="body">The type specific details, if any.</param>
        /// <param name="destination">The name of the endpoint receiving the report.</param>
        public Report(String type, String url, ReportBody body, String destination = null)
        {
            if (String.IsNullOrEmpty(type))
            {
                throw new ArgumentException("The type of a report must not be empty.", nameof(type));
            }

            Type = type;
            Url = url ?? String.Empty;
            Body = body;
            Destination = String.IsNullOrEmpty(destination) ? DefaultDestination : destination;
        }

        /// <summary>
        /// Gets the type of the report.
        /// </summary>
        [DomName("type")]
        public String Type { get; }

        /// <summary>
        /// Gets the URL the report is about.
        /// </summary>
        [DomName("url")]
        public String Url { get; }

        /// <summary>
        /// Gets the type specific details, if any.
        /// </summary>
        [DomName("body")]
        public ReportBody Body { get; }

        /// <summary>
        /// Gets the name of the endpoint receiving the report.
        /// </summary>
        public String Destination { get; }

        /// <summary>
        /// Gets the URL of the destination endpoint, if it is known when the
        /// report is queued.
        /// </summary>
        public String EndpointUrl { get; internal set; }

        /// <summary>
        /// Gets the user agent string of the report's origin, if known.
        /// </summary>
        public String UserAgent { get; internal set; } = String.Empty;

        /// <summary>
        /// Gets the time the report was queued.
        /// </summary>
        public DateTimeOffset Timestamp { get; internal set; }

        /// <summary>
        /// Serializes the report to JSON with the members type, url, and body.
        /// </summary>
        /// <returns>The JSON representation.</returns>
        [DomName("toJSON")]
        public String ToJson() => ReportJson.Serialize(ToDictionary());

        /// <summary>
        /// Serializes the report to JSON in the format used for delivering it
        /// to an endpoint, i.e., including the age and the user agent.
        /// </summary>
        /// <param name="now">The time of the delivery.</param>
        /// <returns>The JSON representation.</returns>
        public String ToDeliveryJson(DateTimeOffset now) => ReportJson.Serialize(ToDeliveryDictionary(now));

        internal IReadOnlyDictionary<String, Object> ToDictionary() => new Dictionary<String, Object>(StringComparer.Ordinal)
        {
            ["type"] = Type,
            ["url"] = Url,
            ["body"] = Body?.ToDictionary(),
        };

        internal IReadOnlyDictionary<String, Object> ToDeliveryDictionary(DateTimeOffset now) => new Dictionary<String, Object>(StringComparer.Ordinal)
        {
            ["age"] = Math.Max(0L, (Int64)(now - Timestamp).TotalMilliseconds),
            ["type"] = Type,
            ["url"] = Url,
            ["user_agent"] = UserAgent ?? String.Empty,
            ["body"] = Body?.ToDictionary(),
        };
    }
}
