using System;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Linq;

namespace TCNotifier
{
    internal class Program
    {
        public const string ServiceName = "TCNotifier";

        private static void Main(string[] args)
        {
            var arguments = new CommandLineArguments(args);
            var op = arguments.AsString(CommandLineArguments.Op, false);

            if (!string.IsNullOrEmpty(op))
            {
                switch (op)
                {
                    case "Install":
                        var serviceArguments = Utils.MakeCommandLine(arguments.ToArgsList().Where(p => !p.StartsWith(CommandLineArguments.Op)));
                        ServiceManager.Install(ServiceName, ServiceName, string.Empty, ServiceStartMode.Manual, serviceArguments);
                        break;
                    case "Uninstall":
                        ServiceManager.Uninstall(ServiceName);
                        break;
                    case "Start":
                        ServiceManager.StartService(ServiceName, arguments.ToArgsList(CommandLineArguments.Op).ToArray());
                        break;
                    case "Stop":
                        ServiceManager.StopService(ServiceName);
                        break;
                    case "Run":
                        var workerService = new TcnService();
                        ServiceBase.Run(workerService);
                        break;
                    default:
                        LogService.Log("Command not regognized!");
                        Console.WriteLine("Not supported command!");
                        break;
                }
            }
            else
            {
#if DEBUG
                var tcnService = new TcnService();
                tcnService.Start(args);
#else
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
                var tcnService = new TcnService();
                ServiceBase.Run(tcnService);
#endif
            }
        }
    }

    public class LogService
    {
        private static readonly string ms_LogPath = Path.Combine(Environment.CurrentDirectory, "log.txt");
        public static void Log(string str)
        {
            EnsureFileExist(ms_LogPath);
            File.AppendAllText(ms_LogPath, str + Environment.NewLine);
        }

        public static void Log(Exception exception)
        {
            EnsureFileExist(ms_LogPath);
            File.AppendAllText(ms_LogPath, ExceptionToString(exception) + Environment.NewLine);
        }

        public static void Log(HttpWebResponse response)
        {
            EnsureFileExist(ms_LogPath);
            var responseStream = response.GetResponseStream();
            if (responseStream != null)
            {
                responseStream.Dispose();
            }
        }

        private static void EnsureFileExist(string path)
        {
            if (!File.Exists(path))
            {
                File.Create(path).Dispose();
            }
        }

        private static string ExceptionToStringInternal(Exception e, bool withStackTrace = true)
        {
            string result =
                e.GetType().Name + ": " + e.Message +
                (withStackTrace ? "\r\n-------\r\n" + e.StackTrace : "");

            if (e.InnerException != null)
                result += "\r\n------- Inner Exception:\r\n" + ExceptionToStringInternal(e.InnerException, withStackTrace);
            return result;
        }

        public static string ExceptionToString(Exception e, bool withStackTrace = true)
        {
            return
                "\r\n" +
                ExceptionToStringInternal(e, withStackTrace);
        }
    }
}
