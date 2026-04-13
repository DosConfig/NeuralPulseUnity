using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 최소 JSON 파서. 외부 의존성 없음.
/// Unity의 JsonUtility는 Dictionary를 지원하지 않으므로 별도 구현.
/// 출처: Unity MiniJSON (MIT License) — 단순화 버전.
/// </summary>
public static class MiniJson
{
    public static object Deserialize(string json)
    {
        if (json == null) return null;
        return Parser.Parse(json);
    }

    public static string Serialize(object obj)
    {
        return Serializer.Serialize(obj);
    }

    private sealed class Parser : IDisposable
    {
        private const string WORD_BREAK = "{}[],:\"";
        private System.IO.StringReader _json;

        private Parser(string jsonString)
        {
            _json = new System.IO.StringReader(jsonString);
        }

        public static object Parse(string jsonString)
        {
            using (var p = new Parser(jsonString))
            {
                return p.ParseValue();
            }
        }

        public void Dispose()
        {
            _json.Dispose();
            _json = null;
        }

        private char PeekChar => Convert.ToChar(_json.Peek());
        private char NextChar => Convert.ToChar(_json.Read());

        private string NextWord
        {
            get
            {
                var word = new StringBuilder();
                while (!IsWordBreak(PeekChar))
                {
                    word.Append(NextChar);
                    if (_json.Peek() == -1) break;
                }
                return word.ToString();
            }
        }

        private void EatWhitespace()
        {
            while (char.IsWhiteSpace(PeekChar))
            {
                _json.Read();
                if (_json.Peek() == -1) break;
            }
        }

        private static bool IsWordBreak(char c)
        {
            return char.IsWhiteSpace(c) || WORD_BREAK.IndexOf(c) != -1;
        }

        private object ParseValue()
        {
            EatWhitespace();
            if (_json.Peek() == -1) return null;

            switch (PeekChar)
            {
                case '{': return ParseObject();
                case '[': return ParseArray();
                case '"': return ParseString();
                case '-':
                case '0': case '1': case '2': case '3': case '4':
                case '5': case '6': case '7': case '8': case '9':
                    return ParseNumber();
                default: return ParseWord();
            }
        }

        private Dictionary<string, object> ParseObject()
        {
            var table = new Dictionary<string, object>();
            _json.Read(); // {

            while (true)
            {
                EatWhitespace();
                if (_json.Peek() == -1) return table;

                switch (PeekChar)
                {
                    case '}':
                        _json.Read();
                        return table;
                    case ',':
                        _json.Read();
                        continue;
                    default:
                        string name = ParseString();
                        if (name == null) return table;
                        EatWhitespace();
                        _json.Read(); // :
                        EatWhitespace();
                        table[name] = ParseValue();
                        break;
                }
            }
        }

        private List<object> ParseArray()
        {
            var array = new List<object>();
            _json.Read(); // [

            while (true)
            {
                EatWhitespace();
                if (_json.Peek() == -1) return array;

                switch (PeekChar)
                {
                    case ']':
                        _json.Read();
                        return array;
                    case ',':
                        _json.Read();
                        continue;
                    default:
                        array.Add(ParseValue());
                        break;
                }
            }
        }

        private string ParseString()
        {
            var s = new StringBuilder();
            _json.Read(); // "

            while (true)
            {
                if (_json.Peek() == -1) break;

                char c = NextChar;
                switch (c)
                {
                    case '"': return s.ToString();
                    case '\\':
                        if (_json.Peek() == -1) break;
                        c = NextChar;
                        switch (c)
                        {
                            case '"': case '\\': case '/': s.Append(c); break;
                            case 'b': s.Append('\b'); break;
                            case 'f': s.Append('\f'); break;
                            case 'n': s.Append('\n'); break;
                            case 'r': s.Append('\r'); break;
                            case 't': s.Append('\t'); break;
                            case 'u':
                                var hex = new char[4];
                                for (int i = 0; i < 4; i++) hex[i] = NextChar;
                                s.Append((char)Convert.ToInt32(new string(hex), 16));
                                break;
                        }
                        break;
                    default:
                        s.Append(c);
                        break;
                }
            }
            return s.ToString();
        }

        private object ParseNumber()
        {
            string number = NextWord;
            if (number.Contains(".") || number.Contains("e") || number.Contains("E"))
            {
                double.TryParse(number, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out double d);
                return d;
            }
            long.TryParse(number, out long l);
            return l;
        }

        private object ParseWord()
        {
            string word = NextWord;
            switch (word)
            {
                case "true": return true;
                case "false": return false;
                case "null": return null;
                default: return word;
            }
        }
    }

    private sealed class Serializer
    {
        private StringBuilder _builder;

        private Serializer() { _builder = new StringBuilder(); }

        public static string Serialize(object obj)
        {
            var s = new Serializer();
            s.SerializeValue(obj);
            return s._builder.ToString();
        }

        private void SerializeValue(object value)
        {
            if (value == null)                                      _builder.Append("null");
            else if (value is string s)                             SerializeString(s);
            else if (value is bool b)                               _builder.Append(b ? "true" : "false");
            else if (value is Dictionary<string, object> dict)      SerializeObject(dict);
            else if (value is List<object> list)                    SerializeArray(list);
            else if (value is int || value is long ||
                     value is float || value is double)             _builder.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
            else                                                    SerializeString(value.ToString());
        }

        private void SerializeObject(Dictionary<string, object> obj)
        {
            _builder.Append('{');
            bool first = true;
            foreach (var kv in obj)
            {
                if (!first) _builder.Append(',');
                SerializeString(kv.Key);
                _builder.Append(':');
                SerializeValue(kv.Value);
                first = false;
            }
            _builder.Append('}');
        }

        private void SerializeArray(List<object> array)
        {
            _builder.Append('[');
            bool first = true;
            foreach (var v in array)
            {
                if (!first) _builder.Append(',');
                SerializeValue(v);
                first = false;
            }
            _builder.Append(']');
        }

        private void SerializeString(string str)
        {
            _builder.Append('"');
            foreach (char c in str)
            {
                switch (c)
                {
                    case '"':  _builder.Append("\\\""); break;
                    case '\\': _builder.Append("\\\\"); break;
                    case '\b': _builder.Append("\\b"); break;
                    case '\f': _builder.Append("\\f"); break;
                    case '\n': _builder.Append("\\n"); break;
                    case '\r': _builder.Append("\\r"); break;
                    case '\t': _builder.Append("\\t"); break;
                    default:   _builder.Append(c); break;
                }
            }
            _builder.Append('"');
        }
    }
}
