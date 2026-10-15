using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GSWebExport
{
    /// <summary>Tiny JSON writer (no external dependency). Always uses '.' decimals.</summary>
    internal sealed class JsonBuilder
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly Stack<bool> _first = new Stack<bool>();

        public override string ToString() => _sb.ToString();

        private void Sep()
        {
            if (_first.Count == 0) return;
            if (_first.Peek()) { _first.Pop(); _first.Push(false); }
            else _sb.Append(',');
        }

        public JsonBuilder BeginObject(string key = null) { Key(key); _sb.Append('{'); _first.Push(true); return this; }
        public JsonBuilder EndObject() { _sb.Append('}'); _first.Pop(); return this; }
        public JsonBuilder BeginArray(string key = null) { Key(key); _sb.Append('['); _first.Push(true); return this; }
        public JsonBuilder EndArray() { _sb.Append(']'); _first.Pop(); return this; }

        private void Key(string key)
        {
            Sep();
            if (key != null) { AppendString(key); _sb.Append(':'); }
        }

        public JsonBuilder Str(string key, string v) { Key(key); if (v == null) _sb.Append("null"); else AppendString(v); return this; }
        public JsonBuilder Num(string key, double v) { Key(key); _sb.Append(Number(v)); return this; }
        public JsonBuilder Int(string key, long v) { Key(key); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        public JsonBuilder Bool(string key, bool v) { Key(key); _sb.Append(v ? "true" : "false"); return this; }
        public JsonBuilder Null(string key) { Key(key); _sb.Append("null"); return this; }
        public JsonBuilder Raw(string key, string json) { Key(key); _sb.Append(json); return this; }

        public JsonBuilder NumArray(string key, params double[] v)
        {
            BeginArray(key);
            foreach (var d in v) { Sep(); _sb.Append(Number(d)); }
            return EndArray();
        }

        public JsonBuilder IntArray(string key, params int[] v)
        {
            BeginArray(key);
            foreach (var d in v) { Sep(); _sb.Append(d.ToString(CultureInfo.InvariantCulture)); }
            return EndArray();
        }

        private static string Number(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        private void AppendString(string s)
        {
            _sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    case '<': _sb.Append("\\u003c"); break; // keeps "</script>" out of the HTML page
                    default:
                        if (c < 0x20) _sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)c);
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }
    }
}
