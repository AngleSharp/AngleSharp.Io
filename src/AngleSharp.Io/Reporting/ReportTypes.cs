namespace AngleSharp.Io.Reporting;

using System;

/// <summary>
/// The report types defined by the Reporting API and related specs.
/// </summary>
public static class ReportTypes
{
    /// <summary>
    /// A network error, see Network Error Logging.
    /// </summary>
    public static readonly String NetworkError = "network-error";

    /// <summary>
    /// The use of a deprecated API.
    /// </summary>
    public static readonly String Deprecation = "deprecation";

    /// <summary>
    /// A request of the document that was denied by the user agent.
    /// </summary>
    public static readonly String Intervention = "intervention";

    /// <summary>
    /// A crash of the document.
    /// </summary>
    public static readonly String Crash = "crash";

    /// <summary>
    /// A violation of the content security policy.
    /// </summary>
    public static readonly String CspViolation = "csp-violation";
}
