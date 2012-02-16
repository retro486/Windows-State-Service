/*
 * This file contains a collection of Win32 specific helper functions/methods. Note that the system this library runs on
 * ABSOLUTELY must have Win32 libraries available (Windows 7 and older). Newer versions of Windows (i.e., Windows 8) are
 * planning to drop support for Win32, in which case this library will not work. This library
 * will also not work with Mono C# projects.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace StateService
{
    class Win32Helpers
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LPGROUP_USERS_INFO_0
        {
            public string groupname;
        }

        [DllImport("Netapi32.dll", SetLastError = true)]
        private static extern int NetUserGetGroups
            ([MarshalAs(UnmanagedType.LPWStr)] string servername,
             [MarshalAs(UnmanagedType.LPWStr)] string username,
             int level,
             out IntPtr bufptr,
             int prefmaxlen,
             out int entriesread,
             out int totalentries);

        [DllImport("Netapi32.dll", SetLastError = true)]
        private static extern int NetApiBufferFree(IntPtr Buffer);



        private static ArrayList GetUserNetGroups(string Username)
        {
            return GetUserNetGroups(null, Username);
        }

        public static ArrayList GetUserNetGroups(string Servername, string Username)
        {
            ArrayList groups = new ArrayList();
            int ErrorCode = 0;
            string _ErrorMessage = string.Empty;

            int EntriesRead;
            int TotalEntries;
            IntPtr bufPtr;

            try
            {
                ErrorCode = NetUserGetGroups(Servername, Username, 0, out bufPtr, 1024, out EntriesRead, out TotalEntries);

                if (ErrorCode != 0)
                {
                    throw new Exception("Username or computer not found");
                }

                if (EntriesRead > 0)
                {
                    LPGROUP_USERS_INFO_0[] RetGroups = new LPGROUP_USERS_INFO_0[EntriesRead];
                    IntPtr iter = bufPtr;
                    for (int i = 0; i < EntriesRead; i++)
                    {
                        RetGroups[i] = (LPGROUP_USERS_INFO_0)Marshal.PtrToStructure(iter, typeof(LPGROUP_USERS_INFO_0));
                        iter = (IntPtr)((int)iter + Marshal.SizeOf(typeof(LPGROUP_USERS_INFO_0)));
                        groups.Add(RetGroups[i].groupname);
                    }
                    NetApiBufferFree(bufPtr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Error returning global groups for {0} - {1}", Username, ex.Message));
            }
            return groups;
        }

        // ability to list local sessions from:
        // http://stackoverflow.com/questions/132620/how-do-you-retrieve-a-list-of-logged-in-connected-users-in-net

        [DllImport("wtsapi32.dll")]
        private static extern Int32 WTSEnumerateSessions(
            IntPtr hServer,
            [MarshalAs(UnmanagedType.U4)] Int32 Reserved,
            [MarshalAs(UnmanagedType.U4)] Int32 Version,
            ref IntPtr ppSessionInfo,
            [MarshalAs(UnmanagedType.U4)] ref Int32 pCount);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("Wtsapi32.dll")]
        private static extern bool WTSQuerySessionInformation(
            System.IntPtr hServer, int sessionId, WTS_INFO_CLASS wtsInfoClass, out System.IntPtr ppBuffer, out uint pBytesReturned);

        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            public Int32 SessionID;

            [MarshalAs(UnmanagedType.LPStr)]
            public String pWinStationName;

            public WTS_CONNECTSTATE_CLASS State;
        }

        [DllImport("wtsapi32.dll")]
        private static extern IntPtr WTSOpenServer([MarshalAs(UnmanagedType.LPStr)] String pServerName);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSCloseServer(IntPtr hServer);

        private enum WTS_INFO_CLASS
        {
            WTSInitialProgram,
            WTSApplicationName,
            WTSWorkingDirectory,
            WTSOEMId,
            WTSSessionId,
            WTSUserName,
            WTSWinStationName,
            WTSDomainName,
            WTSConnectState,
            WTSClientBuildNumber,
            WTSClientName,
            WTSClientDirectory,
            WTSClientProductId,
            WTSClientHardwareId,
            WTSClientAddress,
            WTSClientDisplay,
            WTSClientProtocolType
        }
        private enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }

        private static IntPtr OpenServer(String Name)
        {
            IntPtr server = WTSOpenServer(Name);
            return server;
        }
        private static void CloseServer(IntPtr ServerHandle)
        {
            WTSCloseServer(ServerHandle);
        }

        public static bool isActualUser()
        {
            String ServerName = ".";
            IntPtr serverHandle = IntPtr.Zero;
            List<String> resultList = new List<string>();
            serverHandle = OpenServer(ServerName);
            bool is_user = false;

            try
            {
                IntPtr SessionInfoPtr = IntPtr.Zero;
                IntPtr userPtr = IntPtr.Zero;
                IntPtr domainPtr = IntPtr.Zero;
                Int32 sessionCount = 0;
                Int32 retVal = WTSEnumerateSessions(serverHandle, 0, 1, ref SessionInfoPtr, ref sessionCount);
                Int32 dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                Int32 currentSession = (int)SessionInfoPtr;
                uint bytes = 0;

                if (retVal != 0)
                {
                    for (int i = 0; i < sessionCount; i++)
                    {
                        WTS_SESSION_INFO si = (WTS_SESSION_INFO)Marshal.PtrToStructure((System.IntPtr)currentSession, typeof(WTS_SESSION_INFO));
                        currentSession += dataSize;

                        WTSQuerySessionInformation(serverHandle, si.SessionID, WTS_INFO_CLASS.WTSUserName, out userPtr, out bytes);
                        WTSQuerySessionInformation(serverHandle, si.SessionID, WTS_INFO_CLASS.WTSDomainName, out domainPtr, out bytes);

                        // Ignore any sessions started under an automation user named "someusername"; some management tools do this as a workaround
                        is_user = true;
                    }

                    WTSFreeMemory(SessionInfoPtr);
                    WTSFreeMemory(userPtr);
                    WTSFreeMemory(domainPtr);
                }
            }
            finally
            {
                CloseServer(serverHandle);
            }

            return is_user;
        }
    }
}
