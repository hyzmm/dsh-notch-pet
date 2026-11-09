using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DshNotchPet
{
    internal sealed class TrayController : IDisposable
    {
        private readonly NotifyIcon icon;
        private readonly IntPtr iconHandle;
        private readonly Bitmap bitmap;
        private bool visible;
        private bool disposed;

        public event Action MenuRequested;

        public TrayController()
        {
            Bitmap pawBitmap = null;
            Icon trayIcon = null;
            IntPtr handle = IntPtr.Zero;
            try
            {
                pawBitmap = BuildPawBitmap();
                handle = pawBitmap.GetHicon();
                trayIcon = Icon.FromHandle(handle);
            }
            catch (Exception ex)
            {
                AppLog.Write("tray icon build failed: " + ex.Message);
                trayIcon = SystemIcons.Application;
            }

            icon = new NotifyIcon();
            icon.Icon = trayIcon;
            RefreshText();
            icon.Visible = false;
            icon.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
                {
                    Action handler = MenuRequested;
                    if (handler != null) handler();
                }
            };
            iconHandle = handle;
            bitmap = pawBitmap;
            AppLog.Write("tray icon ready");
        }

        public void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            icon.Visible = value;
            AppLog.Write("tray icon " + (value ? "shown" : "hidden"));
        }

        public void RefreshText()
        {
            string text = L10n.TrayTooltip;
            icon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                icon.Visible = false;
                icon.Dispose();
            }
            catch { }
            if (iconHandle != IntPtr.Zero) NativeMethods.DestroyIcon(iconHandle);
            if (bitmap != null) bitmap.Dispose();
        }

        private static Bitmap BuildPawBitmap()
        {
            Bitmap bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);
                bool lightTheme = IsLightTheme();
                System.Drawing.Color color = lightTheme
                    ? System.Drawing.Color.FromArgb(255, 28, 28, 30)
                    : System.Drawing.Color.FromArgb(255, 245, 245, 247);
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, 9.5f, 14.5f, 13, 11);
                    g.FillEllipse(brush, 3.8f, 8.8f, 6.2f, 8.0f);
                    g.FillEllipse(brush, 9.2f, 5.4f, 6.2f, 8.4f);
                    g.FillEllipse(brush, 15.6f, 5.4f, 6.2f, 8.4f);
                    g.FillEllipse(brush, 21.8f, 8.8f, 6.2f, 8.0f);
                }
            }
            return bitmap;
        }

        private static bool IsLightTheme()
        {
            try
            {
                object value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 0);
                return value is int && (int)value != 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
