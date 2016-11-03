using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;

namespace TCNotifier
{
    public class ServiceManager
    {
        private static ServiceController GetService(string serviceName)
        {
            var services = ServiceController.GetServices().ToList();
            return services.FirstOrDefault(s => s.ServiceName.Equals(serviceName));
        }

        public static void Install(string serviceName, string displayName, string description, ServiceStartMode startType, string arguments)
        {
            LogService.Log("Installing service: " + serviceName);

            var processInstaller = new ServiceProcessInstaller
                                       {
                                           Account = ServiceAccount.NetworkService
                                       };


            var context = new InstallContext();
            var currentProcessPath = Process.GetCurrentProcess().MainModule.FileName;
            if (currentProcessPath.Length > 0)
            {
                var file = new FileInfo(currentProcessPath);
                var path = String.Format("/assemblypath={0}", file.FullName);
                context = new InstallContext("", new[] { path });
            }
            var serviceInstaller = new ServiceInstaller
                                       {
                                           Context = context,
                                           DisplayName = displayName,
                                           Description = description,
                                           ServiceName = serviceName,
                                           StartType = startType,
                                           Parent = processInstaller
                                       };

            serviceInstaller.Install(new Hashtable());

            RegisterServiceCommandLine(serviceName, arguments);
        }

        public static void Uninstall(string serviceName)
        {
            LogService.Log("Uninstalling service: " + serviceName);

            var service = GetService(serviceName);
            if (service == null)
            {
                LogService.Log(string.Format("Service {0} was not found", serviceName));
                return;
            }

            var serviceInstaller = new ServiceInstaller
                                       {
                                           Context = new InstallContext(),
                                           ServiceName = serviceName
                                       };

            serviceInstaller.Uninstall(null);
        }

        public static void StartService(string serviceName, string[] args)
        {
            LogService.Log("Starting service: " + serviceName);
            var service = new ServiceController(serviceName);
            var timeout = new TimeSpan(0, 0, 10);
            service.Start(args);
            service.WaitForStatus(ServiceControllerStatus.Running, timeout);
            LogService.Log(string.Format("Service {0} started", serviceName));
        }

        public static void StopService(string serviceName)
        {
            var service = GetService(serviceName);

            if (service == null)
            {
                LogService.Log(string.Format("Service {0} was not found", serviceName));
                return;
            }

            if (service.Status == ServiceControllerStatus.Stopped || service.Status == ServiceControllerStatus.StopPending)
            {
                LogService.Log("Service was already stopped");
                return;
            }

            var timeout = new TimeSpan(0, 0, 10);
            service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            LogService.Log(string.Format("Service {0} stopped", serviceName));
        }

        private static void RegisterServiceCommandLine(string serviceName, string arguments)
        {
            if (string.IsNullOrEmpty(arguments))
                return;

            using (var oKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName, true))
            {
                if (oKey != null)
                {
                    var sValue = (string)oKey.GetValue("ImagePath");
                    oKey.SetValue("ImagePath", sValue + " " + arguments);
                }
            }
        }
    }
}