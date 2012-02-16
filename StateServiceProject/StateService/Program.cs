using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;

namespace StateService
{
    static class Program
    {        
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            if (!System.Diagnostics.EventLog.SourceExists("StateService"))
            {
                System.Diagnostics.EventLog.CreateEventSource("StateService", "Application");
            }

            System.ServiceProcess.ServiceBase[] ServicesToRun;
            ServicesToRun = new System.ServiceProcess.ServiceBase[] { new StateService() };
            System.ServiceProcess.ServiceBase.Run(ServicesToRun);
        }
    }
}
