namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using System;

    /// <summary>
    /// Represents a range of keys of IndexedDB.
    /// </summary>
    [DomName("IDBKeyRange")]
    [DomExposed("Window")]
    public sealed class IdbKeyRange
    {
        private IdbKeyRange(Object lower, Object upper, Boolean lowerOpen, Boolean upperOpen)
        {
            Lower = lower;
            Upper = upper;
            IsLowerOpen = lowerOpen;
            IsUpperOpen = upperOpen;
        }

        /// <summary>
        /// Gets the lower bound, if any.
        /// </summary>
        [DomName("lower")]
        public Object Lower { get; }

        /// <summary>
        /// Gets the upper bound, if any.
        /// </summary>
        [DomName("upper")]
        public Object Upper { get; }

        /// <summary>
        /// Gets if the lower bound is not part of the range.
        /// </summary>
        [DomName("lowerOpen")]
        public Boolean IsLowerOpen { get; }

        /// <summary>
        /// Gets if the upper bound is not part of the range.
        /// </summary>
        [DomName("upperOpen")]
        public Boolean IsUpperOpen { get; }

        /// <summary>
        /// Creates the range of a single key.
        /// </summary>
        /// <param name="window">The window of the script.</param>
        /// <param name="value">The key.</param>
        /// <returns>The range.</returns>
        [DomName("only")]
        public static IdbKeyRange Only(IWindow window, Object value)
        {
            var key = ToKey(window, value);
            return new IdbKeyRange(key, key, false, false);
        }

        /// <summary>
        /// Creates a range with a lower bound.
        /// </summary>
        /// <param name="window">The window of the script.</param>
        /// <param name="lower">The lower bound.</param>
        /// <param name="open">If the bound itself is not part of the range.</param>
        /// <returns>The range.</returns>
        [DomName("lowerBound")]
        public static IdbKeyRange LowerBound(IWindow window, Object lower, Boolean open = false) =>
            new IdbKeyRange(ToKey(window, lower), null, open, true);

        /// <summary>
        /// Creates a range with an upper bound.
        /// </summary>
        /// <param name="window">The window of the script.</param>
        /// <param name="upper">The upper bound.</param>
        /// <param name="open">If the bound itself is not part of the range.</param>
        /// <returns>The range.</returns>
        [DomName("upperBound")]
        public static IdbKeyRange UpperBound(IWindow window, Object upper, Boolean open = false) =>
            new IdbKeyRange(null, ToKey(window, upper), true, open);

        /// <summary>
        /// Creates a range with two bounds.
        /// </summary>
        /// <param name="window">The window of the script.</param>
        /// <param name="lower">The lower bound.</param>
        /// <param name="upper">The upper bound.</param>
        /// <param name="lowerOpen">If the lower bound itself is not part of the range.</param>
        /// <param name="upperOpen">If the upper bound itself is not part of the range.</param>
        /// <returns>The range.</returns>
        [DomName("bound")]
        public static IdbKeyRange Bound(IWindow window, Object lower, Object upper, Boolean lowerOpen = false, Boolean upperOpen = false)
        {
            var low = ToKey(window, lower);
            var high = ToKey(window, upper);
            var order = IdbKeys.Compare(low, high);

            if (order > 0 || order == 0 && (lowerOpen || upperOpen))
            {
                throw new DomException(IdbKeys.DataError);
            }

            return new IdbKeyRange(low, high, lowerOpen, upperOpen);
        }

        /// <summary>
        /// Gets if the key is in the range.
        /// </summary>
        /// <param name="window">The window of the script.</param>
        /// <param name="key">The key.</param>
        /// <returns>True if it is in the range.</returns>
        [DomName("includes")]
        public Boolean Includes(IWindow window, Object key) => Contains(ToKey(window, key));

        internal Boolean Contains(Object key)
        {
            if (Lower != null)
            {
                var order = IdbKeys.Compare(Lower, key);

                if (order > 0 || order == 0 && IsLowerOpen)
                {
                    return false;
                }
            }

            if (Upper != null)
            {
                var order = IdbKeys.Compare(Upper, key);

                if (order < 0 || order == 0 && IsUpperOpen)
                {
                    return false;
                }
            }

            return true;
        }

        internal static IdbKeyRange From(IWindow window, Object query)
        {
            if (query == null || query is IdbKeyRange)
            {
                return (IdbKeyRange)query;
            }

            return Only(window, query);
        }

        private static Object ToKey(IWindow window, Object value) => IdbKeys.ToKey(IdbFactory.Clone(window, value));
    }
}
