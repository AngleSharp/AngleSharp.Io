namespace AngleSharp.Io.Dom
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Reads JSON (RFC 8259) into plain objects: dictionaries for objects, object arrays for
    /// arrays, doubles for numbers, strings, booleans and null.
    /// </summary>
    static class JsonReader
    {
        public static Object Parse(String text)
        {
            var index = 0;
            var value = ReadValue(text, ref index);
            SkipWhitespace(text, ref index);

            if (index != text.Length)
            {
                throw Error(index);
            }

            return value;
        }

        private static Object ReadValue(String text, ref Int32 index)
        {
            SkipWhitespace(text, ref index);

            if (index >= text.Length)
            {
                throw Error(index);
            }

            switch (text[index])
            {
                case '{':
                    return ReadObject(text, ref index);
                case '[':
                    return ReadArray(text, ref index);
                case '"':
                    return ReadString(text, ref index);
                case 't':
                    return ReadLiteral(text, ref index, "true", true);
                case 'f':
                    return ReadLiteral(text, ref index, "false", false);
                case 'n':
                    return ReadLiteral(text, ref index, "null", null);
                default:
                    return ReadNumber(text, ref index);
            }
        }

        private static Dictionary<String, Object> ReadObject(String text, ref Int32 index)
        {
            var result = new Dictionary<String, Object>();
            index++;
            SkipWhitespace(text, ref index);

            if (index < text.Length && text[index] == '}')
            {
                index++;
                return result;
            }

            while (true)
            {
                SkipWhitespace(text, ref index);

                if (index >= text.Length || text[index] != '"')
                {
                    throw Error(index);
                }

                var key = ReadString(text, ref index);
                SkipWhitespace(text, ref index);
                Expect(text, ref index, ':');
                result[key] = ReadValue(text, ref index);
                SkipWhitespace(text, ref index);

                if (index < text.Length && text[index] == ',')
                {
                    index++;
                    continue;
                }

                Expect(text, ref index, '}');
                return result;
            }
        }

        private static Object[] ReadArray(String text, ref Int32 index)
        {
            var result = new List<Object>();
            index++;
            SkipWhitespace(text, ref index);

            if (index < text.Length && text[index] == ']')
            {
                index++;
                return result.ToArray();
            }

            while (true)
            {
                result.Add(ReadValue(text, ref index));
                SkipWhitespace(text, ref index);

                if (index < text.Length && text[index] == ',')
                {
                    index++;
                    continue;
                }

                Expect(text, ref index, ']');
                return result.ToArray();
            }
        }

        private static String ReadString(String text, ref Int32 index)
        {
            var result = new StringBuilder();
            index++;

            while (index < text.Length)
            {
                var c = text[index++];

                if (c == '"')
                {
                    return result.ToString();
                }
                else if (c != '\\')
                {
                    result.Append(c);
                }
                else if (index < text.Length)
                {
                    var escape = text[index++];

                    switch (escape)
                    {
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u':
                            if (index + 4 > text.Length || !Int32.TryParse(text.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                            {
                                throw Error(index);
                            }

                            result.Append((Char)code);
                            index += 4;
                            break;
                        default: result.Append(escape); break;
                    }
                }
            }

            throw Error(index);
        }

        private static Double ReadNumber(String text, ref Int32 index)
        {
            var start = index;

            while (index < text.Length && "+-0123456789.eE".IndexOf(text[index]) >= 0)
            {
                index++;
            }

            if (!Double.TryParse(text.Substring(start, index - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                throw Error(start);
            }

            return number;
        }

        private static Object ReadLiteral(String text, ref Int32 index, String literal, Object value)
        {
            if (String.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
            {
                throw Error(index);
            }

            index += literal.Length;
            return value;
        }

        private static void Expect(String text, ref Int32 index, Char c)
        {
            if (index >= text.Length || text[index] != c)
            {
                throw Error(index);
            }

            index++;
        }

        private static void SkipWhitespace(String text, ref Int32 index)
        {
            while (index < text.Length && (text[index] == ' ' || text[index] == '\t' || text[index] == '\n' || text[index] == '\r'))
            {
                index++;
            }
        }

        private static FormatException Error(Int32 index) =>
            new FormatException($"Unexpected token in JSON at position {index}.");
    }
}
