using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DshNotchPet
{
    internal static class Program
    {
        private static Mutex instanceMutex;
        private static bool ownsMutex;

        [STAThread]
        public static void Main(string[] args)
        {
            AppLog.EnsureDirectory();
            AppLog.Write("launch " + Environment.OSVersion + " args=" + string.Join(" ", args));
            TryEnableDpiAwareness();

            string pipe = ArgumentValue(args, "--pipe") ?? ArgumentValue(args, "--socket");
            string stateFile = ArgumentValue(args, "--state-file") ?? string.Empty;
            if (string.IsNullOrEmpty(pipe))
            {
                AppLog.Write("missing --pipe argument");
                return;
            }

            bool created;
            instanceMutex = new Mutex(true, @"Local\DSHNotchPet", out created);
            ownsMutex = created;
            if (!created)
            {
                // The macOS helper terminates duplicate app instances. A host
                // restart can race the old panel's 20s socket watchdog, so do
                // the same here instead of silently losing the new overlay.
                AppLog.Write("duplicate instance, replacing it");
                TerminateDuplicates();
                try
                {
                    ownsMutex = instanceMutex.WaitOne(3000);
                }
                catch (AbandonedMutexException)
                {
                    // A killed helper leaves the mutex abandoned; ownership of
                    // the abandoned mutex is granted to this waiter.
                    ownsMutex = true;
                }
                catch { }
                if (!ownsMutex)
                {
                    AppLog.Write("duplicate instance could not be replaced");
                    return;
                }
            }

            try
            {
                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
                {
                    AppLog.Write("dispatcher exception: " + e.Exception);
                    e.Handled = true;
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    AppLog.Write("unhandled exception: " + e.ExceptionObject);
                };

                PetWindow window = new PetWindow(pipe, stateFile);
                app.MainWindow = window;
                window.Start();
                app.Run();
            }
            catch (Exception ex)
            {
                AppLog.Write("fatal: " + ex);
            }
            finally
            {
                try
                {
                    if (instanceMutex != null && ownsMutex) instanceMutex.ReleaseMutex();
                }
                catch { }
                AppLog.Write("exit");
            }
        }

        private static void TerminateDuplicates()
        {
            try
            {
                Process current = Process.GetCurrentProcess();
                Process[] processes = Process.GetProcessesByName(current.ProcessName);
                for (int i = 0; i < processes.Length; i++)
                {
                    Process process = processes[i];
                    try
                    {
                        if (process.Id == current.Id) continue;
                        AppLog.Write("terminating duplicate pid=" + process.Id);
                        process.Kill();
                        process.WaitForExit(1500);
                    }
                    catch { }
                    finally
                    {
                        try { process.Dispose(); } catch { }
                    }
                }
                try { current.Dispose(); } catch { }
            }
            catch (Exception ex)
            {
                AppLog.Write("duplicate cleanup failed: " + ex.Message);
            }
        }

        private static string ArgumentValue(string[] args, string name)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        private static void TryEnableDpiAwareness()
        {
            try
            {
                if (!NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4)))
                {
                    try { NativeMethods.SetProcessDpiAwareness(2); } catch { }
                }
            }
            catch { }
        }
    }
}
