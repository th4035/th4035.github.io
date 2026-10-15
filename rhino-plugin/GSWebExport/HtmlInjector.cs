using System;
using System.Text;

namespace GSWebExport
{
    /// <summary>Puts the model into the viewer page. Mirrors exportPackage() in viewer/session.js.</summary>
    internal static class HtmlInjector
    {
        private const string Placeholder = "/*__RHV_PACKAGE__*/";

        public static string Inject(string shell, byte[] rhv, string packageName)
        {
            string b64 = Convert.ToBase64String(rhv);
            string inject =
                "</script><script id=\"rhv-payload\" type=\"text/plain\">" + b64 + "</script>" +
                "<script>var _p=document.getElementById(\"rhv-payload\").textContent;" +
                "window.__RHV_PACKAGE__=_p;" +
                "window.__RHV_PACKAGE_NAME__=" + JsonString(packageName) + ";";

            int at = shell.IndexOf(Placeholder, StringComparison.Ordinal);
            if (at < 0)
                throw new InvalidOperationException("The embedded viewer page is missing its package placeholder. Rebuild viewer-shell.html.");
            return shell.Substring(0, at) + inject + shell.Substring(at + Placeholder.Length);
        }

        private static string JsonString(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20 || c == '<') sb.AppendFormat("\\u{0:x4}", (int)c);
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
    }
}
