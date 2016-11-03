using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;
using System.Net.Cache;
using System.Threading;

namespace TCNotifier
{
    public class Notifier
    {
        private CancellationToken CancellationToken { get; set; }
        private const string FailUrl = "http://notifier.no-ip.org/fail?conf={0}&steps={1}";
        private const string PassUrl = "http://notifier.no-ip.org/pass?conf={0}";
        private const string ResetUrl = "http://notifier.no-ip.org/reset";

        private Dictionary<string, Build> m_BuildsDict;
        private Dictionary<string, int> m_PersonsDict;

        public Notifier(CommandLineArguments arguments, CancellationToken cancellationToken)
        {
            if (arguments == null)
                throw new ArgumentNullException("arguments");
            Iniitialize(arguments, cancellationToken);
            Reset();
        }

        public void Reset()
        {
            SendRequest(ResetUrl);
        }

        private void Iniitialize(CommandLineArguments arguments, CancellationToken cancellationToken)
        {
            var builds = arguments.AsString(CommandLineArguments.Builds, true);
            var persons = arguments.AsString(CommandLineArguments.Persons, true);
            CancellationToken = cancellationToken;

            var buildsContent = File.ReadAllLines(builds);
            m_BuildsDict = new Dictionary<string, Build>(StringComparer.OrdinalIgnoreCase);
            foreach (var build in buildsContent)
            {
                var items = build.Split(';');
                int buildNumber;
                if (items.Count() == 2 && Int32.TryParse(items[1], out buildNumber))
                {
                    m_BuildsDict.Add(items[0], new Build(buildNumber));
                }
            }

            var personsContent = File.ReadAllLines(persons);
            m_PersonsDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var person in personsContent)
            {
                var items = person.Split(';');
                int angle;
                if (items.Count() == 2 && Int32.TryParse(items[1], out angle))
                {
                    m_PersonsDict.Add(items[0], angle);
                }
            }
        }

        public void Notify(string build, string person, bool state)
        {
            if (state)
            {
                SetSucces(build);
            }
            else
            {
                SetFailore(build, person);
                SpinWait.SpinUntil(() => CancellationToken.IsCancellationRequested, 10 * 1000);
            }
        }

        private void SetFailore(string build, string person)
        {
            try
            {
                if (m_PersonsDict.ContainsKey(person) && m_BuildsDict.ContainsKey(build))
                {
                    var angle = m_PersonsDict[person];
                    var url = string.Format(FailUrl, m_BuildsDict[build].BuildNumber, angle);
                    SendRequest(url);
                }
            }
            catch (WebException exception)
            {
                LogService.Log(exception);
            }
        }

        private void SetSucces(string build)
        {
            try
            {
                if (m_BuildsDict.ContainsKey(build))
                {
                    var url = string.Format(PassUrl, m_BuildsDict[build].BuildNumber);
                    SendRequest(url);
                }
            }
            catch (WebException exception)
            {
                LogService.Log(exception);
            }
        }

        private static void SendRequest(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
                request.KeepAlive = true;
                request.Timeout = 1 * 1000;
                var response = request.GetResponse();
                response.Close();
            }
            catch (Exception exception)
            {
                LogService.Log(url);
                LogService.Log(exception);
            }
        }

    }

    public class Build
    {
        public int BuildNumber { get; private set; }
        public Build(int buildNumber)
        {
            BuildNumber = buildNumber;
        }
    }
}