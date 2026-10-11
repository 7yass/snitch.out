using System.Runtime.InteropServices;

namespace Bloxstrap.Utility
{
    // snitch.out: multi-instance support. Roblox guards a singleton mutex;
    // closing the client's handle to it lets another client start.
    // NOTE: Roblox renamed ROBLOX_singletonEvent -> ROBLOX_singletonMutex,
    // so match on the shared prefix to cover old and new clients.
    public static class SingletonPatch
    {
        private const string SingletonName = "ROBLOX_singleton";

        private const int SystemHandleInformation = 16;
        private const int ObjectNameInformation = 1;
        private const int ObjectTypeInformation = 2;

        private const uint STATUS_SUCCESS = 0;
        private const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;

        private const uint PROCESS_DUP_HANDLE = 0x0040;
        private const uint DUPLICATE_CLOSE_SOURCE = 0x1;

        // x64 SYSTEM_HANDLE_TABLE_ENTRY_INFO stride (8-byte aligned)
        private const int EntryStride = 24;

        [DllImport("ntdll.dll")]
        private static extern uint NtQuerySystemInformation(int systemInformationClass, IntPtr systemInformation, uint systemInformationLength, out uint returnLength);

        [DllImport("ntdll.dll")]
        private static extern uint NtQueryObject(IntPtr handle, int objectInformationClass, IntPtr objectInformation, uint objectInformationLength, out uint returnLength);

        // note: kernel32 DuplicateHandle silently no-ops for CLOSE_SOURCE here,
        // ntdll NtDuplicateObject is used instead (verified empirically)
        [DllImport("ntdll.dll")]
        private static extern uint NtDuplicateObject(IntPtr sourceProcessHandle, IntPtr sourceHandle, IntPtr targetProcessHandle, out IntPtr targetHandle, uint desiredAccess, uint handleAttributes, uint options);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        public static async Task<bool> WaitAndKillAsync(int pid)
        {
            const string LOG_IDENT = "SingletonPatch::WaitAndKillAsync";

            for (int i = 0; i < 30; i++)
            {
                try
                {
                    if (KillSingletonMutex(pid))
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Closed singleton mutex for pid {pid}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                    return false;
                }

                await Task.Delay(1000);
            }

            App.Logger.WriteLine(LOG_IDENT, $"Timed out waiting for singleton mutex (pid {pid})");
            return false;
        }

        public static bool KillSingletonMutex(int pid, string? nameContains = null)
        {
            nameContains ??= SingletonName;

            bool killed = false;
            uint size = 0x10000;
            IntPtr buffer = Marshal.AllocHGlobal((int)size);

            try
            {
                uint status = NtQuerySystemInformation(SystemHandleInformation, buffer, size, out uint returnLength);

                while (status == STATUS_INFO_LENGTH_MISMATCH)
                {
                    size = Math.Max(returnLength, size * 2);
                    Marshal.FreeHGlobal(buffer);
                    buffer = Marshal.AllocHGlobal((int)size);
                    status = NtQuerySystemInformation(SystemHandleInformation, buffer, size, out returnLength);
                }

                if (status != STATUS_SUCCESS)
                    return false;

                int handleCount = Marshal.ReadInt32(buffer);

                IntPtr processHandle = OpenProcess(PROCESS_DUP_HANDLE, false, (uint)pid);

                if (processHandle == IntPtr.Zero)
                    return false;

                try
                {
                    for (int i = 0; i < handleCount; i++)
                    {
                        IntPtr entryPtr = buffer + 8 + i * EntryStride;

                        // USHORT UniqueProcessId @0, UCHAR ObjectTypeIndex @4, USHORT HandleValue @6
                        if ((ushort)Marshal.ReadInt16(entryPtr) != (ushort)pid)
                            continue;

                        IntPtr sourceHandle = (IntPtr)Marshal.ReadInt16(entryPtr + 6);

                        if (NtDuplicateObject(processHandle, sourceHandle, GetCurrentProcess(), out IntPtr duplicated, 0, 0, 0) != STATUS_SUCCESS)
                            continue;

                        try
                        {
                            if (!IsType(duplicated, "Mutant"))
                                continue;

                            string? name = GetObjectName(duplicated);

                            if (name is not null && name.Contains(nameContains, StringComparison.OrdinalIgnoreCase))
                            {
                                // target must be NULL for CLOSE_SOURCE to take effect
                                if (NtDuplicateObject(processHandle, sourceHandle, IntPtr.Zero, out _, 0, 0, DUPLICATE_CLOSE_SOURCE) == STATUS_SUCCESS)
                                    killed = true;
                            }
                        }
                        finally
                        {
                            CloseHandle(duplicated);
                        }
                    }
                }
                finally
                {
                    CloseHandle(processHandle);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return killed;
        }

        private static bool IsType(IntPtr handle, string typeName)
        {
            string? name = QueryObjectString(handle, ObjectTypeInformation, 0);
            return name == typeName;
        }

        private static string? GetObjectName(IntPtr handle)
        {
            // OBJECT_NAME_INFORMATION is a single UNICODE_STRING at offset 0
            return QueryObjectString(handle, ObjectNameInformation, 0);
        }

        // reads the UNICODE_STRING at the given offset: Length @+0, Buffer @+8
        private static string? QueryObjectString(IntPtr handle, int infoClass, int offset)
        {
            uint size = 0x200;
            IntPtr buffer = Marshal.AllocHGlobal((int)size);

            try
            {
                uint status = NtQueryObject(handle, infoClass, buffer, size, out uint returnLength);

                while (status == STATUS_INFO_LENGTH_MISMATCH)
                {
                    size = Math.Max(returnLength, size * 2);
                    Marshal.FreeHGlobal(buffer);
                    buffer = Marshal.AllocHGlobal((int)size);
                    status = NtQueryObject(handle, infoClass, buffer, size, out returnLength);
                }

                if (status != STATUS_SUCCESS)
                    return null;

                short length = Marshal.ReadInt16(buffer + offset);
                IntPtr stringBuffer = Marshal.ReadIntPtr(buffer + offset + 8);

                if (length == 0 || stringBuffer == IntPtr.Zero)
                    return null;

                return Marshal.PtrToStringUni(stringBuffer, length / 2);
            }
            catch
            {
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
