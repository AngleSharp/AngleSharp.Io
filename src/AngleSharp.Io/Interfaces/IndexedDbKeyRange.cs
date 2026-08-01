namespace AngleSharp.Io.Dom
{
    using System;

    /// <summary>
    /// Represents a key range used for IndexedDB lookups.
    /// </summary>
    public sealed class IndexedDbKeyRange
    {
        private IndexedDbKeyRange(String lower, String upper, Boolean lowerOpen, Boolean upperOpen)
        {
            Lower = lower;
            Upper = upper;
            LowerOpen = lowerOpen;
            UpperOpen = upperOpen;
        }

        /// <summary>
        /// Gets the lower bound key.
        /// </summary>
        public String Lower { get; }

        /// <summary>
        /// Gets the upper bound key.
        /// </summary>
        public String Upper { get; }

        /// <summary>
        /// Gets if the lower bound is open.
        /// </summary>
        public Boolean LowerOpen { get; }

        /// <summary>
        /// Gets if the upper bound is open.
        /// </summary>
        public Boolean UpperOpen { get; }

        /// <summary>
        /// Creates a range that includes exactly one key.
        /// </summary>
        public static IndexedDbKeyRange Only(String value)
        {
            return new IndexedDbKeyRange(value, value, false, false);
        }

        /// <summary>
        /// Creates a lower-bounded range.
        /// </summary>
        public static IndexedDbKeyRange LowerBound(String lower, Boolean open = false)
        {
            return new IndexedDbKeyRange(lower, null, open, false);
        }

        /// <summary>
        /// Creates an upper-bounded range.
        /// </summary>
        public static IndexedDbKeyRange UpperBound(String upper, Boolean open = false)
        {
            return new IndexedDbKeyRange(null, upper, false, open);
        }

        /// <summary>
        /// Creates a bounded range.
        /// </summary>
        public static IndexedDbKeyRange Bound(String lower, String upper, Boolean lowerOpen = false, Boolean upperOpen = false)
        {
            return new IndexedDbKeyRange(lower, upper, lowerOpen, upperOpen);
        }

        internal Boolean Includes(String key)
        {
            if (key == null)
            {
                return false;
            }

            if (Lower != null)
            {
                var lowerCompare = StringComparer.Ordinal.Compare(key, Lower);

                if (lowerCompare < 0 || (lowerCompare == 0 && LowerOpen))
                {
                    return false;
                }
            }

            if (Upper != null)
            {
                var upperCompare = StringComparer.Ordinal.Compare(key, Upper);

                if (upperCompare > 0 || (upperCompare == 0 && UpperOpen))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
