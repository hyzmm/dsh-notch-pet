using System;
using System.Diagnostics;

namespace DshNotchPet
{
    internal static class DshLauncher
    {
        public static void Open(string origin)
        {
            // Prefer bringing an existing DSH desktop window forward. The
            // Windows build does not expose a bundle identifier, so match the
            // process family instead.
            try
            {
                int currentId = Process.GetCurrentProcess().Id;
                Process[] processes = Process.GetProcesses();
                for (int i = 0; i < processes.Length; i++)
                {
                    Process process = processes[i];
                    try
                    {
                        if (process.Id == currentId) continue;
                        string name = (process.ProcessName ?? string.Empty).ToLowerInvariant();
                        if (!name.Contains("deepseek") && name != "dsh") continue;
                        IntPtr handle = process.MainWindowHandle;
                        if (handle == IntPtr.Zero) continue;
                        if (NativeMethods.IsIconic(handle))
                            NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
                        if (NativeMethods.SetForegroundWindow(handle))
                        {
                            AppLog.Write("focused DSH pid=" + process.Id);
                            return;
                        }
                    }
                    catch { }
                    finally
                    {
                        try { process.Dispose(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("DSH focus lookup failed: " + ex.Message);
            }

            if (string.IsNullOrEmpty(origin)) return;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = origin;
                info.UseShellExecute = true;
                Process.Start(info);
                AppLog.Write("opened DSH origin " + origin);
            }
            catch (Exception ex)
            {
                AppLog.Write("open DSH failed: " + ex.Message);
            }
        }
    }
}
