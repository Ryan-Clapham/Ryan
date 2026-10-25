using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ColourCascade.Core
{
    /// <summary>
    /// Minimal JSON reader/writer. Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;,
    /// numbers long or double, plus string, bool and null. Used for the save file so the format is
    /// fully under our control and testable outside Unity.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("JSON text is null");
            var p = new Parser(text);
            p.SkipWhitespace();
            object value = p.ParseValue();
            p.SkipWhitespace();
            if (!p.AtEnd) throw new FormatException("Unexpected trailing characters at " + p.Position);
            return value;
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder(1024);
            WriteValue(sb, value);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object> dict:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, kv.Key);
                        sb.Append(':');
                        WriteValue(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
                case System.Collections.IEnumerable list:
                    sb.Append('[');
                    bool firstItem = true;
                    foreach (var item in list)
                    {
                        if (!firstItem) sb.Append(',');
                        firstItem = false;
                        WriteValue(sb, item);
                    }
                    sb.Append(']');
                    break;
                default:
                    WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        sealed class Parser
        {
            readonly string s;
            int i;

            public Parser(string text)
            {
                s = text;
            }

            public int Position => i;
            public bool AtEnd => i >= s.Length;

            public void SkipWhitespace()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            char Peek()
            {
                if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
                return s[i];
            }

            public object ParseValue()
            {
                SkipWhitespace();
                char c = Peek();
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw new FormatException("Unexpected character '" + c + "' at " + i);
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new FormatException("Expected " + word + " at " + i);
                i += word.Length;
            }

            Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>();
                i++;
                SkipWhitespace();
                if (Peek() == '}')
                {
                    i++;
                    return dict;
                }
                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"') throw new FormatException("Expected property name at " + i);
                    string key = ParseString();
                    SkipWhitespace();
                    if (Peek() != ':') throw new FormatException("Expected ':' at " + i);
                    i++;
                    dict[key] = ParseValue();
                    SkipWhitespace();
                    char c = Peek();
                    i++;
                    if (c == '}') return dict;
                    if (c != ',') throw new FormatException("Expected ',' or '}' at " + (i - 1));
                }
            }

            List<object> ParseArray()
            {
                var list = new List<object>();
                i++;
                SkipWhitespace();
                if (Peek() == ']')
                {
                    i++;
                    return list;
                }
                while (true)
                {
                    list.Add(ParseValue());
                    SkipWhitespace();
                    char c = Peek();
                    i++;
                    if (c == ']') return list;
                    if (c != ',') throw new FormatException("Expected ',' or ']' at " + (i - 1));
                }
            }

            string ParseString()
            {
                i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (i >= s.Length) throw new FormatException("Unterminated string");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (i >= s.Length) throw new FormatException("Unterminated escape");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("Bad unicode escape");
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw new FormatException("Bad escape \\" + e);
                    }
                }
            }

            object ParseNumber()
            {
                int start = i;
                if (s[i] == '-') i++;
                while (i < s.Length && char.IsDigit(s[i])) i++;
                bool isFloat = false;
                if (i < s.Length && s[i] == '.')
                {
                    isFloat = true;
                    i++;
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                }
                if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
                {
                    isFloat = true;
                    i++;
                    if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                }
                string token = s.Substring(start, i - start);
                if (!isFloat)
                {
                    long l;
                    if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out l)) return l;
                }
                double d;
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
                throw new FormatException("Bad number '" + token + "'");
            }
        }
    }

    /// <summary>Tolerant accessors for parsed JSON objects: missing or mistyped values fall back to defaults.</summary>
    public static class JsonRead
    {
        public static Dictionary<string, object> Obj(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
        }

        public static List<object> Arr(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as List<object> : null;
        }

        public static long Long(Dictionary<string, object> d, string key, long fallback = 0)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return fallback;
            if (v is long l) return l;
            if (v is double db) return (long)db;
            if (v is bool b) return b ? 1 : 0;
            return fallback;
        }

        public static int Int(Dictionary<string, object> d, string key, int fallback = 0)
        {
            long l = Long(d, key, fallback);
            if (l > int.MaxValue) return int.MaxValue;
            if (l < int.MinValue) return int.MinValue;
            return (int)l;
        }

        public static float Float(Dictionary<string, object> d, string key, float fallback = 0)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return fallback;
            if (v is double db) return (float)db;
            if (v is long l) return l;
            return fallback;
        }

        public static bool Bool(Dictionary<string, object> d, string key, bool fallback = false)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return fallback;
            if (v is bool b) return b;
            if (v is long l) return l != 0;
            return fallback;
        }

        public static string Str(Dictionary<string, object> d, string key, string fallback = "")
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return fallback;
            return v as string ?? fallback;
        }
    }
}
