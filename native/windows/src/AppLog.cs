using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace DshNotchPet
{
    internal static class AppLog
    {
        private static readonly object Gate = new object();

        public static string SupportDirectory
        {
            get
            {
                string overrideDir = Environment.GetEnvironmentVariable("DSH_NOTCH_PET_SUPPORT_DIR");
                if (!string.IsNullOrEmpty(overrideDir)) return overrideDir;
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(local))
                    local = Path.Combine(Path.GetTempPath(), "DSHNotchPet");
                return Path.Combine(local, "DSHNotchPet");
            }
        }

        public static string LogDirectory
        {
            get { return Path.Combine(SupportDirectory, "logs"); }
        }

        public static string LogPath
        {
            get { return Path.Combine(LogDirectory, "DSHNotchPet.log"); }
        }

        public static void EnsureDirectory()
        {
            try { Directory.CreateDirectory(LogDirectory); } catch { }
        }

        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(LogDirectory);
                    string line = DateTime.Now.ToString("o", CultureInfo.InvariantCulture)
                        + " " + message + Environment.NewLine;
                    File.AppendAllText(LogPath, line, Encoding.UTF8);
                }
            }
            catch { }
        }
    }
}
