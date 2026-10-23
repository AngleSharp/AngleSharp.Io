namespace AngleSharp.Io.Reporting
{
    using AngleSharp.Dom;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Delivers reports to their endpoints, i.e., posts them as
    /// application/reports+json to the URL of their destination. Reports
    /// without a known endpoint URL are skipped.
    /// </summary>
    public sealed class EndpointReportSink : IReportSink
    {
        private readonly IRequester _requester;

        /// <summary>
        /// Creates a new endpoint sink using the given requester. The
        /// requester should not be wrapped for reporting, as the delivery of
        /// reports must not cause new reports.
        /// </summary>
        /// <param name="requester">The requester for posting the reports.</param>
        public EndpointReportSink(IRequester requester)
        {
            _requester = requester ?? throw new ArgumentNullException(nameof(requester));
        }

        internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

        /// <summary>
        /// Posts the reports grouped by their endpoint URL. A failed delivery
        /// throws after all endpoints have been tried.
        /// </summary>
        /// <param name="reports">The reports to deliver.</param>
        /// <param name="cancel">The token for cancelling the task.</param>
        /// <returns>The task representing the delivery.</returns>
        public async Task SendAsync(IReadOnlyList<Report> reports, CancellationToken cancel)
        {
            var failed = new List<String>();
            var now = Clock.Invoke();

            foreach (var group in reports.Where(m => !String.IsNullOrEmpty(m.EndpointUrl)).GroupBy(m => m.EndpointUrl, StringComparer.Ordinal))
            {
                var payload = ReportJson.Serialize(group.Select(m => m.ToDeliveryDictionary(now)).ToArray());
                var request = new Request
                {
                    Method = HttpMethod.Post,
                    Address = new Url(group.Key),
                    Content = new MemoryStream(Encoding.UTF8.GetBytes(payload), false),
                };

                request.Headers[HeaderNames.ContentType] = "application/reports+json";

                using (var response = await _requester.RequestAsync(request, cancel).ConfigureAwait(false))
                {
                    var status = (Int32)(response?.StatusCode ?? 0);

                    if (status < 200 || status > 299)
                    {
                        failed.Add(group.Key);
                    }
                }
            }

            if (failed.Count > 0)
            {
                throw new InvalidOperationException("Delivering reports failed for: " + String.Join(", ", failed));
            }
        }
    }
}
