namespace AngleSharp.Io.Dom
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    internal sealed class UrlPatternComponent
    {
        private readonly Regex _regex;
        private readonly List<String> _names;
        private readonly String _groupPrefix;

        public UrlPatternComponent(String pattern, Int32 component, Boolean ignoreCase, Boolean opaquePath = false)
        {
            var parser = new Parser(pattern, component, opaquePath);
            var expression = parser.Parse();
            Pattern = parser.Pattern;
            HasRegExpGroups = parser.HasRegExpGroups;
            _names = parser.Names;
            _groupPrefix = parser.GroupPrefix;

            try
            {
                var options = RegexOptions.CultureInvariant | RegexOptions.ECMAScript;

                if (ignoreCase)
                {
                    options |= RegexOptions.IgnoreCase;
                }

                _regex = new Regex(@"\A" + expression + @"\z", options, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException error)
            {
                throw new ArgumentException("Invalid URL pattern regular expression.", nameof(pattern), error);
            }
        }

        public String Pattern { get; }
        public Boolean HasRegExpGroups { get; }

        public Dictionary<String, Object> Match(String input, Object undefined = null)
        {
            var match = _regex.Match(input);

            if (!match.Success)
            {
                return null;
            }

            var groups = new Dictionary<String, Object>(StringComparer.Ordinal);

            for (var i = 0; i < _names.Count; i++)
            {
                var group = match.Groups[_groupPrefix + i.ToString(CultureInfo.InvariantCulture)];
                groups.Add(_names[i], group.Success ? group.Value : undefined);
            }

            return new Dictionary<String, Object>
            {
                ["input"] = input,
                ["groups"] = groups,
            };
        }

        private sealed class Part
        {
            public String Value { get; set; }
            public String Name { get; set; }
            public String Prefix { get; set; } = String.Empty;
            public String Suffix { get; set; } = String.Empty;
            public Char Modifier { get; set; }
            public Boolean IsFixed { get; set; }
            public Boolean IsFull { get; set; }
            public Boolean IsSegment { get; set; }
        }

        private sealed class Parser
        {
            private readonly String _input;
            private readonly Int32 _component;
            private readonly Boolean _opaquePath;
            private readonly Boolean _ipv6Hostname;
            private readonly List<Part> _parts = new List<Part>();
            private readonly HashSet<String> _usedNames = new HashSet<String>(StringComparer.Ordinal);
            private Int32 _index;
            private Int32 _nextName;

            public Parser(String input, Int32 component, Boolean opaquePath)
            {
                _input = input;
                _component = component;
                _opaquePath = opaquePath;
                _ipv6Hostname = component == 3 && (input.StartsWith("[", StringComparison.Ordinal) ||
                    input.StartsWith("{[", StringComparison.Ordinal) || input.StartsWith("\\[", StringComparison.Ordinal));
                GroupPrefix = "urlpattern";

                while (input.Contains("(?<" + GroupPrefix))
                {
                    GroupPrefix += "_";
                }
            }

            public List<String> Names { get; } = new List<String>();
            public Boolean HasRegExpGroups { get; private set; }
            public String Pattern { get; private set; }
            public String GroupPrefix { get; private set; }

            private String Prefix => _component == 5 && !_opaquePath ? "/" : String.Empty;
            private String Segment => _component == 5 && !_opaquePath ? "[^/]+?" : _component == 3 ? "[^.]+?" : ".+?";

            public String Parse()
            {
                var text = new StringBuilder();
                var prefixEligible = false;

                while (_index < _input.Length)
                {
                    var c = _input[_index];

                    if (c == '\\')
                    {
                        _index++;

                        if (_index == _input.Length)
                        {
                            throw InvalidPattern();
                        }

                        text.Append(_input[_index++]);
                        prefixEligible = false;
                    }
                    else if (c == '{')
                    {
                        AddText(text);
                        ReadBracedPart();
                        prefixEligible = false;
                    }
                    else if (c == ':' || c == '(' || c == '*')
                    {
                        var prefix = String.Empty;

                        if (prefixEligible && Prefix.Length != 0 && text.Length != 0 && text[text.Length - 1] == '/')
                        {
                            text.Length--;
                            prefix = "/";
                        }

                        AddText(text);
                        var part = ReadCapture();
                        part.Prefix = prefix;
                        part.Modifier = ReadModifier();
                        AddCapture(part);
                        prefixEligible = false;
                    }
                    else if (c == '}' || c == '?' || c == '+')
                    {
                        throw InvalidPattern();
                    }
                    else
                    {
                        text.Append(c);
                        prefixEligible = true;
                        _index++;
                    }
                }

                AddText(text);

                foreach (var part in _parts)
                {
                    if (part.IsFixed)
                    {
                        part.Value = Canonicalize(part.Value);
                    }
                }

                _parts.RemoveAll(part => part.IsFixed && part.Value.Length == 0);
                Pattern = GeneratePattern();
                return GenerateExpression();
            }

            private void ReadBracedPart()
            {
                _index++;
                var prefix = new StringBuilder();
                var suffix = new StringBuilder();
                Part capture = null;

                while (_index < _input.Length && _input[_index] != '}')
                {
                    var c = _input[_index];

                    if (c == '\\')
                    {
                        _index++;

                        if (_index == _input.Length)
                        {
                            throw InvalidPattern();
                        }

                        (capture is null ? prefix : suffix).Append(_input[_index++]);
                    }
                    else if (c == ':' || c == '(' || c == '*')
                    {
                        if (capture != null)
                        {
                            throw InvalidPattern();
                        }

                        capture = ReadCapture();
                    }
                    else if (c == '{' || c == '?' || c == '+')
                    {
                        throw InvalidPattern();
                    }
                    else
                    {
                        (capture is null ? prefix : suffix).Append(c);
                        _index++;
                    }
                }

                if (_index == _input.Length)
                {
                    throw InvalidPattern();
                }

                _index++;
                var modifier = ReadModifier();

                if (capture is null)
                {
                    AddFixed(prefix.ToString(), modifier);
                }
                else
                {
                    capture.Prefix = Canonicalize(prefix.ToString());
                    capture.Suffix = Canonicalize(suffix.ToString());
                    capture.Modifier = modifier;
                    AddCapture(capture);
                }
            }

            private Part ReadCapture()
            {
                var part = new Part();

                if (_input[_index] == ':')
                {
                    var start = ++_index;

                    if (_index == _input.Length || !IsNameCharacter(_input, _index, true))
                    {
                        throw InvalidPattern();
                    }

                    AdvanceName();

                    while (_index < _input.Length && IsNameCharacter(_input, _index, false))
                    {
                        AdvanceName();
                    }

                    part.Name = _input.Substring(start, _index - start);
                }

                if (_index < _input.Length && _input[_index] == '(')
                {
                    part.Value = ReadRegex();
                    part.IsFull = part.Value == ".*";
                    part.IsSegment = part.Value == Segment;
                    HasRegExpGroups |= !part.IsFull && !part.IsSegment;
                }
                else if (part.Name is null)
                {
                    _index++;
                    part.IsFull = true;
                    part.Value = ".*";
                }
                else
                {
                    part.IsSegment = true;
                    part.Value = Segment;
                }

                part.Name ??= (_nextName++).ToString(CultureInfo.InvariantCulture);

                if (!_usedNames.Add(part.Name))
                {
                    throw InvalidPattern();
                }

                return part;
            }

            private void AddCapture(Part part)
            {
                Names.Add(part.Name);
                _parts.Add(part);
            }

            private void AddText(StringBuilder text)
            {
                if (text.Length != 0)
                {
                    AddFixed(text.ToString(), '\0');
                    text.Clear();
                }
            }

            private void AddFixed(String value, Char modifier)
            {
                if (value.Length == 0)
                {
                    return;
                }

                if (modifier == '\0' && _parts.Count > 0 && _parts[_parts.Count - 1].IsFixed &&
                    _parts[_parts.Count - 1].Modifier == '\0')
                {
                    _parts[_parts.Count - 1].Value += value;
                }
                else
                {
                    _parts.Add(new Part { IsFixed = true, Value = value, Modifier = modifier });
                }
            }

            private String Canonicalize(String value) =>
                UrlPatternMatcher.CanonicalizeLiteral(value, _component, _opaquePath, _ipv6Hostname);

            private String GenerateExpression()
            {
                var result = new StringBuilder();
                var group = 0;

                foreach (var part in _parts)
                {
                    if (part.IsFixed)
                    {
                        var text = Regex.Escape(part.Value);
                        result.Append(part.Modifier == '\0' ? text : "(?:" + text + ")" + part.Modifier);
                        continue;
                    }

                    var prefix = Regex.Escape(part.Prefix);
                    var suffix = Regex.Escape(part.Suffix);
                    var name = GroupPrefix + (group++).ToString(CultureInfo.InvariantCulture);
                    var expression = TranslateSets(part.Value);

                    if (part.Modifier == '*' || part.Modifier == '+')
                    {
                        if (prefix.Length == 0 && suffix.Length == 0)
                        {
                            result.Append("(?<").Append(name).Append(">(?:").Append(expression).Append(')').Append(part.Modifier).Append(')');
                        }
                        else
                        {
                            result.Append("(?:").Append(prefix).Append("(?<").Append(name).Append(">(?:").Append(expression)
                                .Append(")(?:").Append(suffix).Append(prefix).Append("(?:").Append(expression).Append("))*")
                                .Append(')').Append(suffix).Append(')');

                            if (part.Modifier == '*')
                            {
                                result.Append('?');
                            }
                        }
                    }
                    else
                    {
                        if (part.Modifier == '?' && part.IsFull && prefix.Length == 0 && suffix.Length == 0)
                        {
                            expression = ".+";
                        }

                        result.Append("(?:").Append(prefix).Append("(?<").Append(name).Append('>').Append(expression)
                            .Append(')').Append(suffix).Append(')');

                        if (part.Modifier == '?')
                        {
                            result.Append('?');
                        }
                    }
                }

                return result.ToString();
            }

            private String GeneratePattern()
            {
                var result = new StringBuilder();

                for (var i = 0; i < _parts.Count; i++)
                {
                    var part = _parts[i];
                    var previous = i == 0 ? null : _parts[i - 1];
                    var next = i + 1 == _parts.Count ? null : _parts[i + 1];

                    if (part.IsFixed)
                    {
                        var text = UrlPatternMatcher.EscapePattern(part.Value);
                        result.Append(part.Modifier == '\0' ? text : "{" + text + "}" + part.Modifier);
                        continue;
                    }

                    var named = !Char.IsDigit(part.Name[0]);
                    var grouped = part.Suffix.Length != 0 || part.Prefix.Length != 0 && part.Prefix != Prefix;

                    if (!grouped && named && part.IsSegment && part.Modifier == '\0' && next != null &&
                        next.Prefix.Length == 0 && next.Suffix.Length == 0)
                    {
                        grouped = next.IsFixed ? next.Value.Length != 0 && IsNameCharacter(next.Value, 0, false) : Char.IsDigit(next.Name[0]);
                    }

                    if (!grouped && part.Prefix.Length == 0 && previous != null && previous.IsFixed &&
                        Prefix.Length != 0 && previous.Value.EndsWith(Prefix, StringComparison.Ordinal))
                    {
                        grouped = true;
                    }

                    if (grouped)
                    {
                        result.Append('{');
                    }

                    result.Append(UrlPatternMatcher.EscapePattern(part.Prefix));

                    if (named)
                    {
                        result.Append(':').Append(part.Name);
                    }

                    if (!part.IsFull && !part.IsSegment)
                    {
                        result.Append('(').Append(part.Value).Append(')');
                    }
                    else if (part.IsSegment && !named)
                    {
                        result.Append('(').Append(part.Value).Append(')');
                    }
                    else if (part.IsFull)
                    {
                        if (!named && (previous is null || previous.IsFixed || previous.Modifier != '\0' || grouped || part.Prefix.Length != 0))
                        {
                            result.Append('*');
                        }
                        else
                        {
                            result.Append("(.*)");
                        }
                    }

                    if (part.IsSegment && named && part.Suffix.Length != 0 && IsNameCharacter(part.Suffix, 0, false))
                    {
                        result.Append('\\');
                    }

                    result.Append(UrlPatternMatcher.EscapePattern(part.Suffix));

                    if (grouped)
                    {
                        result.Append('}');
                    }

                    if (part.Modifier != '\0')
                    {
                        result.Append(part.Modifier);
                    }
                }

                return result.ToString();
            }

            private String ReadRegex()
            {
                var start = ++_index;
                var depth = 1;

                while (_index < _input.Length)
                {
                    var c = _input[_index++];

                    if (c > 127)
                    {
                        throw InvalidPattern();
                    }

                    if (c == '\\')
                    {
                        if (_index == _input.Length || _input[_index] > 127)
                        {
                            throw InvalidPattern();
                        }

                        if ("^$\\.*+?()[]{}|/bBcdDfnrsStvwWxu0123456789kpP-".IndexOf(_input[_index]) < 0)
                        {
                            throw InvalidPattern();
                        }

                        _index++;
                    }
                    else if (c == '(')
                    {
                        if (_index == _input.Length || _input[_index] != '?')
                        {
                            throw InvalidPattern();
                        }

                        depth++;
                    }
                    else if (c == ')' && --depth == 0)
                    {
                        var value = _input.Substring(start, _index - start - 1);

                        if (value.Length == 0 || value[0] == '?')
                        {
                            throw InvalidPattern();
                        }

                        return value;
                    }
                }

                throw InvalidPattern();
            }

            private static String TranslateSets(String expression)
            {
                var result = new StringBuilder();

                for (var i = 0; i < expression.Length; i++)
                {
                    if (expression[i] == '\\' && i + 1 < expression.Length)
                    {
                        result.Append(expression[i++]).Append(expression[i]);
                    }
                    else if (expression[i] == '[')
                    {
                        var end = FindClassEnd(expression, i);
                        result.Append(TranslateClass(expression.Substring(i, end - i + 1)));
                        i = end;
                    }
                    else
                    {
                        result.Append(expression[i]);
                    }
                }

                return result.ToString();
            }

            private static String TranslateClass(String characterClass)
            {
                var parts = new List<String>();
                var operations = new List<String>();
                var start = 1;
                var depth = 0;
                var nested = false;

                for (var i = 1; i < characterClass.Length - 1; i++)
                {
                    var c = characterClass[i];

                    if (c == '\\')
                    {
                        i++;
                    }
                    else if (c == '[')
                    {
                        depth++;
                        nested = true;
                    }
                    else if (c == ']')
                    {
                        depth--;
                    }
                    else if (depth == 0 && i + 1 < characterClass.Length - 1 &&
                        (c == '&' && characterClass[i + 1] == '&' || c == '-' && characterClass[i + 1] == '-'))
                    {
                        parts.Add(characterClass.Substring(start, i - start));
                        operations.Add(characterClass.Substring(i, 2));
                        i++;
                        start = i + 1;
                    }
                }

                if (operations.Count == 0)
                {
                    if (nested || characterClass.Contains("\\q{"))
                    {
                        throw new NotSupportedException("This Unicode set expression is not supported.");
                    }

                    return characterClass;
                }

                parts.Add(characterClass.Substring(start, characterClass.Length - 1 - start));

                if (parts[0].StartsWith("^", StringComparison.Ordinal))
                {
                    throw new NotSupportedException("Negated Unicode set operations are not supported.");
                }

                var result = SetOperand(parts[0]);

                for (var i = 0; i < operations.Count; i++)
                {
                    var right = SetOperand(parts[i + 1]);
                    result = "(?" + (operations[i] == "&&" ? "=" : "!") + "(?:" + right + "))(?:" + result + ")";
                }

                return "(?:" + result + ")";
            }

            private static String SetOperand(String value)
            {
                if (value.Length == 0)
                {
                    throw InvalidPattern();
                }

                if (value[0] == '[' && FindClassEnd(value, 0) == value.Length - 1)
                {
                    return TranslateClass(value);
                }

                if (value.IndexOf('[') >= 0 || value.IndexOf(']') >= 0 || value.Contains("\\q{"))
                {
                    throw new NotSupportedException("This Unicode set operand is not supported.");
                }

                return "[" + value + "]";
            }

            private static Int32 FindClassEnd(String expression, Int32 start)
            {
                var depth = 0;

                for (var i = start; i < expression.Length; i++)
                {
                    if (expression[i] == '\\')
                    {
                        i++;
                    }
                    else if (expression[i] == '[')
                    {
                        depth++;
                    }
                    else if (expression[i] == ']' && --depth == 0)
                    {
                        return i;
                    }
                }

                throw InvalidPattern();
            }

            private Char ReadModifier()
            {
                if (_index < _input.Length && (_input[_index] == '?' || _input[_index] == '+' || _input[_index] == '*'))
                {
                    return _input[_index++];
                }

                return '\0';
            }

            private void AdvanceName() =>
                _index += Char.IsHighSurrogate(_input[_index]) && _index + 1 < _input.Length && Char.IsLowSurrogate(_input[_index + 1]) ? 2 : 1;

            private static Boolean IsNameCharacter(String value, Int32 index, Boolean first)
            {
                var c = value[index];
                var category = CharUnicodeInfo.GetUnicodeCategory(value, index);
                var start = Char.IsLetter(c) || c == '_' || c == '$' || c == '\u2118' || c == '\u212e' ||
                    c == '\u309b' || c == '\u309c' || category == UnicodeCategory.LetterNumber ||
                    Char.IsHighSurrogate(c) && category >= UnicodeCategory.UppercaseLetter && category <= UnicodeCategory.OtherLetter;

                return start || !first && (Char.IsDigit(c) || c == '\u200c' || c == '\u200d' ||
                    category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark ||
                    category == UnicodeCategory.ConnectorPunctuation);
            }

            private static ArgumentException InvalidPattern() => new ArgumentException("Invalid URL pattern.");
        }
    }
}
