namespace AngleSharp.Io.Reporting
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the body of a network-error report, following the shape
    /// defined by Network Error Logging.
    /// </summary>
    public sealed class NetworkErrorReportBody : ReportBody
    {
        /// <summary>
        /// Gets or sets the sampling rate applied to the report.
        /// </summary>
        public Double SamplingFraction { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets the time in milliseconds between the start of the
        /// request and its completion or failure.
        /// </summary>
        public Int64 ElapsedTime { get; set; }

        /// <summary>
        /// Gets or sets the phase in which the error occurred, i.e., dns,
        /// connection, or application.
        /// </summary>
        public String Phase { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the type of the error, e.g., dns.name_not_resolved,
        /// tcp.refused, or http.error.
        /// </summary>
        public String Type { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the IP address of the server, if known.
        /// </summary>
        public String ServerIp { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the network protocol, if known.
        /// </summary>
        public String Protocol { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the referrer of the request.
        /// </summary>
        public String Referrer { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the method of the request.
        /// </summary>
        public String Method { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the status code of the response, or 0 if there is
        /// none.
        /// </summary>
        public Int32 StatusCode { get; set; }

        /// <inheritdoc />
        public override IReadOnlyDictionary<String, Object> ToDictionary() => new Dictionary<String, Object>(StringComparer.Ordinal)
        {
            ["sampling_fraction"] = SamplingFraction,
            ["elapsed_time"] = ElapsedTime,
            ["phase"] = Phase,
            ["type"] = Type,
            ["server_ip"] = ServerIp,
            ["protocol"] = Protocol,
            ["referrer"] = Referrer,
            ["method"] = Method,
            ["status_code"] = StatusCode,
        };
    }
}
