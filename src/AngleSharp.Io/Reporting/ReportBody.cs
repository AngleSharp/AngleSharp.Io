namespace AngleSharp.Io.Reporting
{
    using AngleSharp.Attributes;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the type specific details of a report.
    /// </summary>
    [DomName("ReportBody")]
    public abstract class ReportBody
    {
        /// <summary>
        /// Gets the members of the body as they appear in its JSON form.
        /// Values may be strings, numbers, booleans, null, lists, or nested
        /// dictionaries.
        /// </summary>
        /// <returns>The members of the body.</returns>
        public abstract IReadOnlyDictionary<String, Object> ToDictionary();

        /// <summary>
        /// Serializes the body to JSON.
        /// </summary>
        /// <returns>The JSON representation.</returns>
        [DomName("toJSON")]
        public String ToJson() => ReportJson.Serialize(ToDictionary());
    }

    /// <summary>
    /// Represents a report body with arbitrary members, e.g., for custom
    /// report types.
    /// </summary>
    public sealed class GenericReportBody : ReportBody
    {
        private readonly Dictionary<String, Object> _members;

        /// <summary>
        /// Creates a new report body with the given members.
        /// </summary>
        /// <param name="members">The members of the body.</param>
        public GenericReportBody(IEnumerable<KeyValuePair<String, Object>> members)
        {
            _members = new Dictionary<String, Object>(StringComparer.Ordinal);

            foreach (var member in members ?? Array.Empty<KeyValuePair<String, Object>>())
            {
                _members[member.Key] = member.Value;
            }
        }

        /// <inheritdoc />
        public override IReadOnlyDictionary<String, Object> ToDictionary() => _members;
    }
}
