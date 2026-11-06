namespace AngleSharp.Io.Dom
{
    using AngleSharp.Dom;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The keys of IndexedDB and how they are found and ordered: arrays come after bytes, bytes
    /// after strings, strings after dates and dates after numbers.
    /// </summary>
    static class IdbKeys
    {
        internal const String DataError = "DataError";

        public static Object ToKey(Object value) => TryToKey(value, out var key) ? key : throw new DomException(DataError);

        public static Boolean TryToKey(Object value, out Object key)
        {
            switch (value)
            {
                case Double number when !Double.IsNaN(number):
                    key = number;
                    return true;
                case Int32 integer:
                    key = (Double)integer;
                    return true;
                case String:
                case DateTime:
                case Byte[]:
                    key = value;
                    return true;
                case Object[] items:
                    var result = new Object[items.Length];

                    for (var i = 0; i < items.Length; i++)
                    {
                        if (!TryToKey(items[i], out result[i]))
                        {
                            key = null;
                            return false;
                        }
                    }

                    key = result;
                    return true;
                default:
                    key = null;
                    return false;
            }
        }

        public static Int32 Compare(Object a, Object b)
        {
            var order = GetRank(a).CompareTo(GetRank(b));

            if (order != 0)
            {
                return order;
            }

            switch (a)
            {
                case Double number:
                    return number.CompareTo((Double)b);
                case DateTime date:
                    return date.ToUniversalTime().CompareTo(((DateTime)b).ToUniversalTime());
                case String text:
                    return String.CompareOrdinal(text, (String)b);
                case Byte[] bytes:
                    return CompareSequence(bytes, (Byte[])b, (x, y) => x.CompareTo(y));
                default:
                    return CompareSequence((Object[])a, (Object[])b, Compare);
            }
        }

        /// <summary>
        /// Gets what a key path (a name, names with dots between them, or several of these) gives
        /// for a value. Returns false if the value has no such thing.
        /// </summary>
        public static Boolean TryEvaluate(Object value, Object keyPath, out Object result)
        {
            if (keyPath is String[] paths)
            {
                var items = new Object[paths.Length];

                for (var i = 0; i < paths.Length; i++)
                {
                    if (!TryEvaluate(value, paths[i], out items[i]))
                    {
                        result = null;
                        return false;
                    }
                }

                result = items;
                return true;
            }

            result = value;
            var path = (String)keyPath;

            if (path.Length == 0)
            {
                return true;
            }

            foreach (var name in path.Split('.'))
            {
                switch (result)
                {
                    case IDictionary<String, Object> record when record.TryGetValue(name, out var next):
                        result = next;
                        break;
                    case String text when name == "length":
                        result = (Double)text.Length;
                        break;
                    case Object[] array when name == "length":
                        result = (Double)array.Length;
                        break;
                    case Byte[] bytes when name == "length" || name == "byteLength":
                        result = (Double)bytes.Length;
                        break;
                    default:
                        result = null;
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Puts a generated key where the key path says, making what is missing on the way.
        /// </summary>
        public static void Inject(Object value, String keyPath, Object key)
        {
            var names = keyPath.Split('.');
            var current = value as IDictionary<String, Object>;

            for (var i = 0; i < names.Length - 1 && current != null; i++)
            {
                if (!current.TryGetValue(names[i], out var next))
                {
                    next = new Dictionary<String, Object>();
                    current[names[i]] = next;
                }

                current = next as IDictionary<String, Object>;
            }

            if (current == null)
            {
                throw new DomException(IdbKeys.DataError);
            }

            current[names[names.Length - 1]] = key;
        }

        /// <summary>
        /// Gets the key path of what script gave, i.e. nothing, one or several.
        /// </summary>
        public static Object ToKeyPath(Object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case String text when IsValidPath(text):
                    return text;
                case String[] names when Array.TrueForAll(names, IsValidPath):
                    return names;
                case Object[] items when Array.TrueForAll(items, m => m is String text && IsValidPath(text)):
                    return Array.ConvertAll(items, m => (String)m);
                default:
                    throw new DomException(DomError.Syntax);
            }
        }

        private static Boolean IsValidPath(String path)
        {
            if (path.Length == 0)
            {
                return true;
            }

            foreach (var name in path.Split('.'))
            {
                if (name.Length == 0 || Char.IsDigit(name[0]))
                {
                    return false;
                }

                foreach (var c in name)
                {
                    if (!Char.IsLetterOrDigit(c) && c != '_' && c != '$')
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static Int32 GetRank(Object key)
        {
            switch (key)
            {
                case Double:
                    return 1;
                case DateTime:
                    return 2;
                case String:
                    return 3;
                case Byte[]:
                    return 4;
                default:
                    return 5;
            }
        }

        private static Int32 CompareSequence<T>(T[] a, T[] b, Func<T, T, Int32> compare)
        {
            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                var order = compare(a[i], b[i]);

                if (order != 0)
                {
                    return order;
                }
            }

            return a.Length.CompareTo(b.Length);
        }
    }
}
