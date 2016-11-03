using System;
using System.Collections.Generic;
using System.Linq;

namespace TCNotifier
{
    public class CommandLineArguments
    {
        public const string Builds = "--builds";
        public const string Persons = "--persons";
        public const string Op = "--op";

        public readonly Dictionary<string, string> Args = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        public CommandLineArguments(IEnumerable<string> args)
        {
            if (args != null)
            {
                LoadArgs(args);
            }
        }


        public string AsString(string argKey, bool mustExists)
        {
            string sVal = null;
            if (argKey != null && Args.ContainsKey(argKey))
                sVal = Args[argKey];

            if (sVal == null)
            {
                if (!mustExists)
                    return null;
                throw new CommandLineException("missing command line parameter '{0}' | '{1}'", argKey, string.Empty);
            }
            return sVal;
        }

        public int? AsInt(string argKey, bool mustExists)
        {
            string sVal = AsString(argKey, mustExists);
            if (sVal == null)
                return null;

            int val;
            if (!int.TryParse(sVal, out val))
                throw new CommandLineException("invalid command line parameter integer value '{0}:{1}'", argKey, sVal);

            return val;
        }

        public bool AsIntBool(string argKey, bool mustExists)
        {
            return (AsInt(argKey, mustExists) ?? 0) == 1;
        }

        public ushort? AsUshort(string argKey, bool mustExists)
        {
            string sVal = AsString(argKey, mustExists);
            if (sVal == null)
                return null;

            ushort val;
            if (!ushort.TryParse(sVal, out val))
                throw new CommandLineException("invalid command line parameter value '{0}:{1}'", argKey, sVal);

            return val;
        }

        private void LoadArgs(IEnumerable<string> args)
        {
            foreach (var arg in args)
            {
                int idx = arg.IndexOf(':');

                string key, value;

                if (idx >= 0)
                {
                    key = arg.Substring(0, idx);
                    value = arg.Substring(idx + 1);
                }
                else
                {
                    key = arg;
                    value = "";
                }
                Args[key] = value;
            }
        }

        public List<string> ToArgsList(string except = null)
        {
            var args = new Dictionary<string, string>(Args);
            if (!string.IsNullOrEmpty(except)){
                args.Remove(except);
            }

            return args.Select(arg => string.Format("{0}:{1}", arg.Key, arg.Value)).ToList();
        }
    }

    public class CommandLineException : Exception
    {
        public CommandLineException(string invalidCommandLineParameterIntegerValue, string argKey, string sVal)
        {
        }
    }
}
