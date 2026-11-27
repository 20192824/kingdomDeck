using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KingdomDeck.Engine
{
    /// <summary>
    /// 엔진 전용 아주 작은 JSON 파서/작성기. (UnityEngine 참조 없이 cards.json과 저장 파일을 다루기 위함)
    /// 객체 → Dictionary&lt;string, object&gt;, 배열 → List&lt;object&gt;, 숫자 → double.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var p = new Parser(json);
            object v = p.ParseValue();
            p.SkipWs();
            if (!p.End) throw new FormatException("JSON 끝에 불필요한 문자가 있습니다 (위치 " + p.Pos + ")");
            return v;
        }

        sealed class Parser
        {
            readonly string s;
            int i;
            public Parser(string s) { this.s = s; }
            public bool End => i >= s.Length;
            public int Pos => i;

            public void SkipWs()
            {
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '﻿') i++;
                    else break;
                }
            }

            public object ParseValue()
            {
                SkipWs();
                if (End) throw new FormatException("JSON이 예상보다 일찍 끝났습니다");
                char c = s[i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ParseNumber();
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                    throw new FormatException("'" + word + "'가 필요합니다 (위치 " + i + ")");
                i += word.Length;
            }

            Dictionary<string, object> ParseObject()
            {
                var d = new Dictionary<string, object>();
                i++; // {
                SkipWs();
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWs();
                    string key = ParseString();
                    SkipWs();
                    if (s[i] != ':') throw new FormatException("':'가 필요합니다 (위치 " + i + ")");
                    i++;
                    d[key] = ParseValue();
                    SkipWs();
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("',' 또는 '}'가 필요합니다 (위치 " + i + ")");
                }
            }

            List<object> ParseArray()
            {
                var list = new List<object>();
                i++; // [
                SkipWs();
                if (s[i] == ']') { i++; return list; }
                while (true)
                {
                    list.Add(ParseValue());
                    SkipWs();
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return list; }
                    throw new FormatException("',' 또는 ']'가 필요합니다 (위치 " + i + ")");
                }
            }

            string ParseString()
            {
                if (s[i] != '"') throw new FormatException("문자열이 필요합니다 (위치 " + i + ")");
                i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (End) throw new FormatException("문자열이 닫히지 않았습니다");
                    char c = s[i++];
                    if (c == '"') break;
                    if (c != '\\') { sb.Append(c); continue; }
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
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw new FormatException("잘못된 이스케이프 문자 (위치 " + i + ")");
                    }
                }
                return sb.ToString();
            }

            double ParseNumber()
            {
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                if (start == i) throw new FormatException("알 수 없는 값 (위치 " + i + ")");
                return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }

        // ---------- 작성 ----------

        public static string Serialize(object value, bool pretty = false)
        {
            var sb = new StringBuilder();
            Write(sb, value, pretty, 0);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object v, bool pretty, int depth)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string str: WriteString(sb, str); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case ulong ul: sb.Append(ul.ToString(CultureInfo.InvariantCulture)); return;
                case double dbl: sb.Append(dbl.ToString("R", CultureInfo.InvariantCulture)); return;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); return;
                case IDictionary dict:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (DictionaryEntry kv in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        if (pretty) { sb.Append('\n'); sb.Append(' ', (depth + 1) * 2); }
                        WriteString(sb, kv.Key.ToString());
                        sb.Append(pretty ? ": " : ":");
                        Write(sb, kv.Value, pretty, depth + 1);
                    }
                    if (pretty && !first) { sb.Append('\n'); sb.Append(' ', depth * 2); }
                    sb.Append('}');
                    return;
                }
                case IEnumerable list:
                {
                    sb.Append('[');
                    bool first = true;
                    foreach (object item in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Write(sb, item, false, depth + 1);
                    }
                    sb.Append(']');
                    return;
                }
                default:
                    WriteString(sb, v.ToString());
                    return;
            }
        }

        static void WriteString(StringBuilder sb, string str)
        {
            sb.Append('"');
            foreach (char c in str)
            {
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

        // ---------- 읽기 도우미 ----------

        public static string Str(Dictionary<string, object> d, string key, string def = null)
            => d.TryGetValue(key, out var v) && v != null ? v.ToString() : def;

        public static int Int(Dictionary<string, object> d, string key, int def = 0)
            => d.TryGetValue(key, out var v) && v is double n ? (int)n : def;

        public static long Long(Dictionary<string, object> d, string key, long def = 0)
            => d.TryGetValue(key, out var v) && v is double n ? (long)n : def;

        public static bool Bool(Dictionary<string, object> d, string key, bool def = false)
            => d.TryGetValue(key, out var v) && v is bool b ? b : def;

        public static List<object> List(Dictionary<string, object> d, string key)
            => d.TryGetValue(key, out var v) ? v as List<object> : null;

        public static Dictionary<string, object> Obj(Dictionary<string, object> d, string key)
            => d.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;
    }
}
