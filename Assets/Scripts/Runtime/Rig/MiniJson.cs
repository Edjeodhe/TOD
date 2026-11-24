// 아주 작은 JSON 읽기 (리깅 페이지가 저장한 리그 JSON 을 그대로 읽으려고). JsonUtility 는 사전(parts 처럼 이름 → 값)을 못 읽는다.
// 결과: 객체 = Dictionary<string, object>, 배열 = List<object>, 수 = double, 문자열, bool, null.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TOD
{
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            object v = Value(text, ref i);
            return v;
        }

        static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON 이 끝났습니다");
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (start == i) throw new FormatException($"JSON {i} 번째 글자를 읽지 못했습니다");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Skip(s, ref i);
                string key = Str(s, ref i);
                Skip(s, ref i);
                i++;   // :
                d[key] = Value(s, ref i);
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++;   // }
                return d;
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++;   // ]
                return l;
            }
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            i++;
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 꺼내기 도우미
        public static Dictionary<string, object> O(object o, string key) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(key, out object v) ? v as Dictionary<string, object> : null;
        public static List<object> A(object o, string key) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(key, out object v) ? v as List<object> : null;
        public static float F(object o, string key, float def = 0) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(key, out object v) && v is double d ? (float)d : def;
        public static string S(object o, string key) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(key, out object v) ? v as string : null;
        public static bool B(object o, string key) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(key, out object v) && v is bool b && b;
        public static float[] V(object o, string key)
        {
            List<object> l = A(o, key);
            if (l == null) return null;
            var r = new float[l.Count];
            for (int k = 0; k < l.Count; k++) r[k] = l[k] is double d ? (float)d : 0;
            return r;
        }
    }
}
