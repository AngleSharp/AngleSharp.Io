namespace AngleSharp.Io
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Some general extension methods.
    /// </summary>
    static class GeneralExtensions
    {
        /// <summary>
        /// Returns the string representation for the specified HTTP method.
        /// </summary>
        /// <param name="method">The type of HTTP method to stringify.</param>
        /// <returns>The string representing the HTTP method.</returns>
        public static String Stringify(this HttpMethod method)
        {
            switch (method)
            {
                case HttpMethod.Get:
                    return "GET";
                case HttpMethod.Delete:
                    return "DELETE";
                case HttpMethod.Post:
                    return "POST";
                case HttpMethod.Put:
                    return "PUT";
                default:
                    return method.ToString().ToUpperInvariant();
            }
        }

        /// <summary>
        /// Forgets the given task. Exceptions are ignored and continuations
        /// are pointless.
        /// </summary>
        /// <param name="task">The task to forget after firing.</param>
        public static void Forget(this Task task)
        {
        }
    }
}
