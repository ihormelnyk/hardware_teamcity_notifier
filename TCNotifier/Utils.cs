using System.Collections.Generic;
using System.Text;

namespace TCNotifier
{
    public static class Utils
    {
        public static string MakeCommandLine(IEnumerable<string> args)
        {
            var sb = new StringBuilder();
            foreach (var arg in args)
            {
                if (sb.Length > 0)
                    sb.Append(' ');
                string s = arg;
                if (s.IndexOf(' ') >= 0)
                {
                    s = "\"" + s.Replace("\"", "\"\"") + "\"";
                }
                sb.Append(s);
            }
            return sb.ToString();
        }
    }
}