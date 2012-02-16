using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.IO;
using System.Security.Permissions;
using System.Diagnostics.Eventing.Reader;
using Microsoft.Win32;
using System.Collections;

/* Instructions for creating a basic service with an installer in Visual Studio:
 * http://msdn.microsoft.com/en-us/library/zt39148a.aspx
 * */

namespace StateService
{
    public partial class StateService : ServiceBase
    {
        //private TimedCheck m_tc;
        private string server_url;
        private short m_state;
        private string m_hostname;

        

        public StateService()
        {
            InitializeComponent();
            this.CanHandleSessionChangeEvent = true; // allow this service to hook into logon/off events

            // get the server URL to push the update to from the registry
            RegistryKey r = Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("rdkl.us");
            if (r == null)
            {
                this.eventLog1.WriteEntry("Unable to read server URL from registry.");
                this.Stop();
                return;
            }
            try
            {
                this.server_url = (string)r.GetValue("SERVER_URL");
            }
            catch (Exception ex)
            {
                this.eventLog1.WriteEntry("SERVER_URL value doesn't exist in rdkl.us registry entry.");
                this.Stop();
                return;
            }
            r.Close(); // just to be safe.

            this.m_hostname = System.Environment.MachineName;
        }

        protected override void OnStart(string[] args)
        {
            this.markAvailable();
        }

        protected override void OnStop()
        {
            //NOTE: to keep this accurate, this service MUST be made dependent on the Dhcp service or else the
            //computer will shut down networking before this service has a chance to report its state.
            this.markDown();
        }

        // Hook into session changes (logon/off)
        // Found at: http://stackoverflow.com/questions/248186/service-needs-to-detect-if-workstation-is-locked-and-screen-saver-is-active/734037#734037
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            switch (changeDescription.Reason)
            {
                case SessionChangeReason.SessionLogon:
                    this.markUnavailable();
                    break;
                case SessionChangeReason.SessionLogoff:
                    this.markAvailable();
                    break;
                
            }
            base.OnSessionChange(changeDescription); // forward the request to the parent for proper processing.
        }

        private void markAvailable()
        {
            this.m_state = 1;
            this.sendState();
        }

        private void markUnavailable()
        {
            this.m_state = 0;
            this.sendState();
        }

        private void markDown()
        {
            this.eventLog1.WriteEntry("This machine is now considered \"not responding\".", EventLogEntryType.Warning);
            this.m_state = -1;
            this.sendState();
        }

        private void sendState()
        {
            // only send the update if the computer isn't just logging in and rebooting for updates
            // currently only the rmadmin user qualifies as "not a user". all others qualify.
            if (Win32Helpers.isActualUser())
            {
                string url = (string)(this.server_url + "update/?id=" + this.m_hostname + "&state=" + this.m_state);
                // send the current state to the server via GET request; put the server response in the log.
                System.Net.WebRequest wget_url = System.Net.WebRequest.Create(url);
                try
                {
                    Stream response = wget_url.GetResponse().GetResponseStream();
                    StreamReader sr = new StreamReader(response);
                    string line = sr.ReadLine();
                    if (line.CompareTo("ok") != 0)
                    {
                        // didn't update ok
                        this.eventLog1.WriteEntry("Server returned not ok: " + url + "\nResponse: " + line, EventLogEntryType.Error);
                    }
                    sr.Close(); response.Close();
                }
                catch (Exception ex)
                {
                    // didn't update ok
                    this.eventLog1.WriteEntry("Server returned not ok: " + url + "\nException: " + ex.Message, EventLogEntryType.Error);
                }
            }
        }
    }
}
