namespace AngleSharp.Io.Reporting
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Keeps the exception behind a response that requesters create instead
    /// of failing, which allows reporting the cause of the network error.
    /// </summary>
    static class NetworkFailures
    {
        private static readonly ConditionalWeakTable<IResponse, Exception> Failures = new ConditionalWeakTable<IResponse, Exception>();

        public static void Attach(IResponse response, Exception error)
        {
            if (response is not null && error is not null)
            {
                Failures.Remove(response);
                Failures.Add(response, error);
            }
        }

        public static Exception Get(IResponse response) =>
            response is not null && Failures.TryGetValue(response, out var error) ? error : null;
    }
}
