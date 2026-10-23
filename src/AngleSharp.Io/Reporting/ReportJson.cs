namespace AngleSharp.Io.Reporting
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// A minimal JSON writer for reports. Members are written in ordinal
    /// order, which keeps the output deterministic.
    /// </summary>
    static class ReportJson
    {
        public static String Serialize(Object value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, Object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case String text:
                    WriteString(sb, text);
                    break;
                case Boolean flag:
                    sb.Append(flag ? "true" : "false");
                    break;
                case Double number when Double.IsNaN(number) || Double.IsInfinity(number):
                    sb.Append("null");
                    break;
                case Single number when Single.IsNaN(number) || Single.IsInfinity(number):
                    sb.Append("null");
                    break;
                case Double number:
                    sb.Append(number.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case Single number:
                    sb.Append(number.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case Byte _:
                case SByte _:
                case Int16 _:
                case UInt16 _:
                case Int32 _:
                case UInt32 _:
                case Int64 _:
                case UInt64 _:
                case Decimal _:
                    sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                case ReportBody body:
                    Write(sb, body.ToDictionary());
                    break;
                case Report report:
                    Write(sb, report.ToDictionary());
                    break;
                case IReadOnlyDictionary<String, Object> members:
                    WriteObject(sb, members);
                    break;
                case IDictionary<String, Object> members:
                    WriteObject(sb, members);
                    break;
                case IEnumerable items:
                    WriteArray(sb, items);
                    break;
                default:
                    WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        private static void WriteObject(StringBuilder sb, IEnumerable<KeyValuePair<String, Object>> members)
        {
            var first = true;
            sb.Append('{');

            foreach (var member in members.OrderBy(m => m.Key, StringComparer.Ordinal))
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                WriteString(sb, member.Key);
                sb.Append(':');
                Write(sb, member.Value);
            }

            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable items)
        {
            var first = true;
            sb.Append('[');

            foreach (var item in items)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                Write(sb, item);
            }

            sb.Append(']');
        }

        private static void WriteString(StringBuilder sb, String text)
        {
            sb.Append('"');

            foreach (var c in text)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((Int32)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
        }
    }
}
