using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Threading;

namespace TCNotifier
{
    public class TcnService : ServiceBase
    {
        private CommandLineArguments Args { get; set; }
        private Thread Thread { get; set; }
        private CancellationTokenSource CancellationTokenSource { get; set; }

        public TcnService()
        {
            CanStop = true;
        }

        protected override void OnStart(string[] args)
        {
            LogService.Log("Arguments: " + string.Join(" ", Environment.GetCommandLineArgs()));
            Args = new CommandLineArguments(Environment.GetCommandLineArgs());
            try
            {
                var builds = GetBuilds();
                CancellationTokenSource = new CancellationTokenSource();
                Thread = new Thread(() => DoRun(builds, CancellationTokenSource.Token));
                Thread.Start();
            }
            catch (Exception ex)
            {
                LogService.Log(ex);
                throw;
            }
        }

        public void Start(string[] args)
        {
            OnStart(args);
        }

        private List<string> GetBuilds()
        {
            var buildsFilePath = Args.AsString(CommandLineArguments.Builds, true);
            var lines = File.ReadLines(buildsFilePath);
            return lines.Select(line => line.Split(';')).Select(items => items[0]).ToList();
        }


        protected override void OnStop()
        {
            try
            {
                CancellationTokenSource.Cancel();
                SpinWait.SpinUntil(() => false, 200);
                if (Thread != null)
                {
                    Thread.Abort();
                    Thread.Join();
                }
            }
            catch (Exception){}
        }

        private void DoRun(List<string> builds, CancellationToken cancellationToken)
        {
            var notifier = new Notifier(Args, cancellationToken);
            while (true)
            {
                foreach (var build in builds)
                {
                    string person;
                    try
                    {
                        var checkBuild = BuildChecker.CheckBuild(build, out person);
                        notifier.Notify(build, person, checkBuild);
                    }
                    catch(WebException ex)
                    {
                        notifier.Reset();
                        SpinWait.SpinUntil(() => cancellationToken.IsCancellationRequested, 5 * 1000);
                    }
                    if (cancellationToken.IsCancellationRequested){
                        break;
                    }
                }
                SpinWait.SpinUntil(() => cancellationToken.IsCancellationRequested, 1000);

                if (cancellationToken.IsCancellationRequested){
                    break;
                }
            }
        }
    }
}