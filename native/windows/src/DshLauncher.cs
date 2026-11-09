using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DshNotchPet
{
    /// <summary>
    /// Returns the user to DSH. A session hosted by DSH Desktop belongs to the
    /// desktop application, so the launcher raises that application's window;
    /// when every window is gone it starts the executable again, and the
    /// running shell's single-instance handler reopens the window. A session
    /// served by a CLI host has no window to return to, so the reported origin
    /// opens in the browser instead.
    /// </summary>
    internal static class DshLauncher
    {
        public static void Open(string origin, DesktopApp desktop)
        {
            string exe = desktop == null ? null : desktop.exe;
            if (!string.IsNullOrEmpty(exe))
            {
                // A window means the application is already there: raise it and
                // stop. Starting a second copy on top of a window the pet could
                // not reach (an elevated shell, for instance) would be worse
                // than leaving it alone. Only a windowless application needs to
                // be started again.
                IntPtr window = FindDesktopWindow(exe);
                if (window != IntPtr.Zero)
                {
                    RaiseDesktopWindow(window);
                    return;
                }
                if (StartDesktop(exe)) return;
                AppLog.Write("desktop launch failed, falling back to the origin");
            }
            if (string.IsNullOrEmpty(origin)) return;
            StartUrl(origin);
        }

        private static void RaiseDesktopWindow(IntPtr window)
        {
            try
            {
                if (NativeMethods.IsIconic(window))
                    NativeMethods.ShowWindow(window, NativeMethods.SW_RESTORE);
                else if (!NativeMethods.IsWindowVisible(window))
                    NativeMethods.ShowWindow(window, NativeMethods.SW_SHOW);
                Raise(window);
                AppLog.Write("focused DSH window hwnd=" + window);
            }
            catch (Exception ex)
            {
                AppLog.Write("focus DSH window failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Windows only lets the foreground process raise its own windows. The
        /// pet never takes focus, so its SetForegroundWindow runs from the
        /// background and is refused. Sharing the input state of the current
        /// foreground thread for the duration of the call makes the request
        /// legal; without it Open DSH used to fall through to a browser tab.
        /// </summary>
        private static void Raise(IntPtr window)
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == window)
            {
                NativeMethods.BringWindowToTop(window);
                return;
            }
            int ignored;
            int foregroundThread = foreground == IntPtr.Zero
                ? 0
                : NativeMethods.GetWindowThreadProcessId(foreground, out ignored);
            int currentThread = NativeMethods.GetCurrentThreadId();
            bool attached = false;
            if (foregroundThread != 0 && foregroundThread != currentThread)
                attached = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
            try
            {
                NativeMethods.BringWindowToTop(window);
                NativeMethods.SetForegroundWindow(window);
                NativeMethods.SetFocus(window);
            }
            finally
            {
                if (attached)
                    NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }

        /// <summary>
        /// Picks the desktop application's window, process lookup first and the
        /// title suffix second. A hidden window stays eligible so one closed to
        /// the tray can be raised.
        /// </summary>
        private static IntPtr FindDesktopWindow(string exe)
        {
            string processName = Path.GetFileNameWithoutExtension(exe);
            if (string.IsNullOrEmpty(processName)) return IntPtr.Zero;
            IntPtr owned = FindOwnedWindow(processName);
            if (owned != IntPtr.Zero) return owned;
            // A confined helper (the DSH sandbox runs the panel at low
            // integrity) can still enumerate windows but not the processes that
            // own them, so the process lookup above comes back empty. The
            // window itself is readable: match the product-name suffix DSH puts
            // on every desktop window title.
            return FindTitledWindow(processName);
        }

        private static IntPtr FindOwnedWindow(string processName)
        {
            HashSet<int> owners = new HashSet<int>();
            try
            {
                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        owners.Add(process.Id);
                    }
                    finally
                    {
                        try { process.Dispose(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("DSH window lookup failed: " + ex.Message);
            }
            if (owners.Count == 0) return IntPtr.Zero;

            IntPtr best = IntPtr.Zero;
            int bestScore = 0;
            try
            {
                NativeMethods.EnumWindows(delegate(IntPtr window, IntPtr unused)
                {
                    int owner;
                    NativeMethods.GetWindowThreadProcessId(window, out owner);
                    if (owner == 0 || !owners.Contains(owner)) return true;
                    // Only an Electron browser window is the application
                    // surface; renderer IME and message windows are not.
                    if (!IsElectronWindow(window)) return true;
                    int titleLength = NativeMethods.GetWindowTextLength(window);
                    bool visible = NativeMethods.IsWindowVisible(window);
                    int score = 0;
                    if (titleLength > 0) score += 4;
                    if (visible) score += 2;
                    if (visible && !NativeMethods.IsIconic(window)) score += 1;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = window;
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                AppLog.Write("DSH window enumeration failed: " + ex.Message);
            }
            return best;
        }

        /// <summary>
        /// Finds the application window without touching its process: DSH titles
        /// every desktop window with the product name ("Session — DeepSeek
        /// Harness"), which a confined helper can read even when the owning
        /// process is out of reach.
        /// </summary>
        private static IntPtr FindTitledWindow(string productName)
        {
            IntPtr best = IntPtr.Zero;
            int bestScore = -1;
            try
            {
                NativeMethods.EnumWindows(delegate(IntPtr window, IntPtr unused)
                {
                    if (NativeMethods.GetWindow(window, NativeMethods.GW_OWNER) != IntPtr.Zero) return true;
                    if (!IsElectronWindow(window)) return true;
                    if (NativeMethods.GetWindowTextLength(window) == 0) return true;
                    if (!TitleNamesProduct(window, productName)) return true;
                    bool visible = NativeMethods.IsWindowVisible(window);
                    int score = (visible ? 2 : 0) + (NativeMethods.IsIconic(window) ? 0 : 1);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = window;
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                AppLog.Write("DSH title lookup failed: " + ex.Message);
            }
            return best;
        }

        private static bool TitleNamesProduct(IntPtr window, string productName)
        {
            string title = WindowTitle(window);
            if (title.Length == 0) return false;
            if (string.Equals(title, productName, StringComparison.OrdinalIgnoreCase)) return true;
            // The separator is an em dash on current builds; accept an en dash
            // or a plain hyphen too so a locale or copy change cannot hide the
            // window.
            return title.EndsWith("\u2014 " + productName, StringComparison.OrdinalIgnoreCase)
                || title.EndsWith("\u2013 " + productName, StringComparison.OrdinalIgnoreCase)
                || title.EndsWith("- " + productName, StringComparison.OrdinalIgnoreCase);
        }

        private static string WindowTitle(IntPtr window)
        {
            try
            {
                int length = NativeMethods.GetWindowTextLength(window);
                if (length <= 0) return string.Empty;
                StringBuilder title = new StringBuilder(length + 2);
                if (NativeMethods.GetWindowText(window, title, title.Capacity) <= 0) return string.Empty;
                return title.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsElectronWindow(IntPtr window)
        {
            string name = ClassName(window);
            return name.StartsWith("Chrome_WidgetWin_1", StringComparison.Ordinal);
        }

        private static string ClassName(IntPtr window)
        {
            try
            {
                StringBuilder name = new StringBuilder(128);
                if (NativeMethods.GetClassName(window, name, name.Capacity) <= 0) return string.Empty;
                return name.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// A Host started by DSH Desktop is a Node-mode Electron child, so this
        /// process inherited ELECTRON_RUN_AS_NODE=1. The desktop shell needs
        /// that variable gone: with it set, the relaunched executable runs as
        /// plain Node and quits before it can reopen its window.
        /// </summary>
        private static bool StartDesktop(string exe)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = exe;
                info.UseShellExecute = false;
                info.WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty;
                info.EnvironmentVariables.Remove("ELECTRON_RUN_AS_NODE");
                Process.Start(info);
                AppLog.Write("started DSH desktop " + exe);
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Write("start DSH desktop failed for " + exe + ": " + ex.Message);
                return false;
            }
        }

        private static bool StartUrl(string origin)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = origin;
                info.UseShellExecute = true;
                Process.Start(info);
                AppLog.Write("opened DSH origin " + origin);
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Write("open DSH origin failed for " + origin + ": " + ex.Message);
                return false;
            }
        }
    }
}
