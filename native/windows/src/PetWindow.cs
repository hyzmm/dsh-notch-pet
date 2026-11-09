using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DshNotchPet
{
    internal sealed class PetWindow : Window
    {
        private const double TopEdge = 6;
        private const double CollapsedWidth = PetViewModel.CollapsedWidth;
        private const double CollapsedHeight = PetViewModel.CollapsedHeight;
        private const double ExpandedWidth = PetViewModel.ExpandedWidth;
        private const double ExpandedHeight = PetViewModel.ExpandedHeight;

        private readonly PetViewModel model;
        private readonly PetTheme theme;
        private readonly PetPreferences preferences;
        private readonly PetSurface surface;
        private readonly StateClient client;
        private readonly TrayController tray;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly DispatcherTimer motionTimer;
        private readonly DispatcherTimer collapseTimer;
        private readonly DispatcherTimer peekTimer;

        private SettingsWindow settings;
        private bool panelVisible;
        private bool launchPeekScheduled;
        private bool hovering;
        private bool externalMenuOpen;
        private bool quitting;
        private bool started;
        private double screenWidthDip = 1280;
        private double screenHeightDip = 720;
        private double dpiScale = 1;
        private double lastInvalidate;
        private bool resizing;
        private DateTime resizeStarted;
        private double resizeFromWidth;
        private double resizeFromHeight;
        private double resizeToWidth;
        private double resizeToHeight;
        private DateTime opacityStarted;
        private double opacityFrom;
        private double opacityTo;
        private DateTime suppressSettingsReopenUntil = DateTime.MinValue;
        private DateTime suppressPanelActionsUntil = DateTime.MinValue;

        internal PetViewModel Model { get { return model; } }
        internal PetPreferences Preferences { get { return preferences; } }

        public PetWindow(string pipePath, string stateFilePath)
        {
            model = new PetViewModel();
            theme = PetTheme.Current;
            preferences = PetPreferences.Current;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            Focusable = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            Title = L10n.AppName;

            UpdateMetricsFromSystem();
            Opacity = 0;
            Width = CollapsedWidth;
            Height = CollapsedHeight;
            Left = Math.Max(0, (screenWidthDip - CollapsedWidth) / 2);
            Top = TopEdge;

            surface = new PetSurface(model, theme, preferences);
            Content = surface;

            tray = new TrayController();
            tray.MenuRequested += ShowTrayMenu;

            client = new StateClient(pipePath, stateFilePath);
            client.PayloadReceived += delegate(PetPayload payload)
            {
                Post(delegate { model.ApplyPayload(payload); });
            };
            client.ConnectionChanged += delegate(bool connected)
            {
                Post(delegate { model.SetConnected(connected); });
            };
            client.GaveUp += delegate { Post(Quit); };

            model.Changed += OnModelChanged;
            theme.Changed += delegate { surface.InvalidateVisual(); };
            preferences.Changed += OnPreferencesChanged;

            surface.HoverChanged += SetHovering;
            surface.TogglePinRequested += delegate
            {
                if (settings == null && DateTime.UtcNow >= suppressPanelActionsUntil) TogglePinned();
            };
            surface.ToggleSettingsRequested += ToggleSettings;
            surface.OpenDshRequested += delegate
            {
                if (settings == null && DateTime.UtcNow >= suppressPanelActionsUntil)
                    DshLauncher.Open(model.Origin, model.Desktop);
            };
            surface.ContextMenuRequested += ShowPanelMenu;

            motionTimer = new DispatcherTimer();
            motionTimer.Interval = TimeSpan.FromMilliseconds(15);
            motionTimer.Tick += delegate { OnMotionTick(); };
            motionTimer.Start();

            collapseTimer = new DispatcherTimer();
            collapseTimer.Tick += delegate { OnCollapseTick(); };

            peekTimer = new DispatcherTimer();
            peekTimer.Tick += delegate
            {
                peekTimer.Stop();
                if (!panelVisible) return;
                Expand(true);
                ScheduleCollapse(2.8);
            };

            SourceInitialized += delegate { ApplyWindowStyles(); };
            Loaded += delegate
            {
                UpdateMetricsFromWindow();
                ApplyFrame(CollapsedWidth, CollapsedHeight);
                surface.Time = clock.Elapsed.TotalSeconds;
                surface.ReduceMotion = !SystemParameters.ClientAreaAnimation;
                surface.InvalidateVisual();
            };
            Closing += delegate
            {
                if (!quitting)
                {
                    quitting = true;
                    ShutdownResources();
                }
            };
            Closed += delegate
            {
                ShutdownResources();
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            };
            DpiChanged += delegate
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    UpdateMetricsFromWindow();
                    ApplyFrame(model.Expanded ? ExpandedWidth : CollapsedWidth,
                        model.Expanded ? ExpandedHeight : CollapsedHeight);
                }));
            };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            AppLog.Write("window created pipe=" + pipePath);
        }

        public void Start()
        {
            if (started) return;
            started = true;
            UpdateMetricsFromSystem();
            ApplyFrame(CollapsedWidth, CollapsedHeight);
            Show();
            panelVisible = true;
            UpdateMetricsFromWindow();
            ApplyFrame(CollapsedWidth, CollapsedHeight);
            RaiseToTop();
            Opacity = 1;
            surface.ReduceMotion = !SystemParameters.ClientAreaAnimation;
            tray.SetVisible(false);
            client.Start();
            ScheduleLaunchPeek();
            EvaluateVisibility();
            AppLog.Write("panel shown");
        }

        private void ApplyWindowStyles()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            int ex = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE,
                ex | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
            RaiseToTop();
        }

        private void RaiseToTop()
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                if (handle == IntPtr.Zero) return;
                NativeMethods.SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0,
                    0x0001 | 0x0002 | 0x0010);
            }
            catch { }
        }

        private void OnModelChanged(PetChange change)
        {
            if ((change & PetChange.Expanded) != 0) surface.HideToolTip();
            surface.InvalidateVisual();
            if ((change & PetChange.Expanded) != 0)
                StartResize(model.Expanded);
            if ((change & PetChange.Pinned) != 0)
            {
                if (model.Pinned)
                {
                    Expand(true);
                    CancelCollapse();
                }
                else if (!hovering && settings == null && !externalMenuOpen)
                {
                    ScheduleCollapse(0.45);
                }
                EvaluateVisibility();
            }
            if ((change & PetChange.Settings) != 0)
            {
                if (model.SettingsOpen)
                {
                    Expand(true);
                    if (!panelVisible) SetPanelVisible(true);
                }
                else if (!model.Pinned && !hovering)
                {
                    ScheduleCollapse(0.25);
                }
                EvaluateVisibility();
            }
            if ((change & (PetChange.Content | PetChange.Connection)) != 0)
                EvaluateVisibility();
        }

        private void OnPreferencesChanged()
        {
            Title = L10n.AppName;
            tray.RefreshText();
            surface.HideToolTip();
            surface.InvalidateVisual();
            UpdateTrayVisibility();
            EvaluateVisibility();
            if (settings != null) settings.Surface.InvalidateVisual();
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                UpdateMetricsFromWindow();
                ApplyFrame(model.Expanded ? ExpandedWidth : CollapsedWidth,
                    model.Expanded ? ExpandedHeight : CollapsedHeight);
                if (settings != null)
                {
                    settings.PlaceBelow((screenWidthDip - ExpandedWidth) / 2, ExpandedWidth,
                        TopEdge + ExpandedHeight, screenWidthDip, screenHeightDip);
                }
            }));
        }

        private void UpdateMetricsFromSystem()
        {
            double dpi = 96;
            try
            {
                uint systemDpi = NativeMethods.GetDpiForSystem();
                if (systemDpi > 0) dpi = systemDpi;
            }
            catch { }
            try
            {
                double wpfScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                if (wpfScale > 1) dpi = wpfScale * 96.0;
            }
            catch { }
            dpiScale = dpi / 96.0;
            int width = NativeMethods.GetSystemMetrics(0);
            int height = NativeMethods.GetSystemMetrics(1);
            if (width <= 0) width = 1920;
            if (height <= 0) height = 1080;
            screenWidthDip = width / dpiScale;
            screenHeightDip = height / dpiScale;
        }

        private void UpdateMetricsFromWindow()
        {
            try
            {
                // WPF's DPI is the authoritative scale for Window.Left/Top.
                // GetDpiForWindow stays at 96 for a system-aware WPF process on
                // a 200% display, while VisualTreeHelper reports the real 2x.
                double wpfScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                if (wpfScale > 0) dpiScale = wpfScale;
            }
            catch { }
            if (dpiScale <= 0) dpiScale = 1;
            int width = NativeMethods.GetSystemMetrics(0);
            int height = NativeMethods.GetSystemMetrics(1);
            if (width <= 0) width = 1920;
            if (height <= 0) height = 1080;
            screenWidthDip = width / dpiScale;
            screenHeightDip = height / dpiScale;
            AppLog.Write("metrics dpi=" + dpiScale + " screenDip=" + screenWidthDip + "x" + screenHeightDip);
        }

        private void ApplyFrame(double width, double height)
        {
            double maxWidth = Math.Max(240, screenWidthDip - 16);
            width = Math.Min(width, maxWidth);
            Width = width;
            Height = height;
            Left = Math.Max(0, (screenWidthDip - width) / 2);
            Top = TopEdge;
        }

        private void StartResize(bool expanded)
        {
            double targetWidth = expanded ? ExpandedWidth : CollapsedWidth;
            double targetHeight = expanded ? ExpandedHeight : CollapsedHeight;
            if (!panelVisible || !SystemParameters.ClientAreaAnimation)
            {
                resizing = false;
                surface.ContentOpacity = 1;
                ApplyFrame(targetWidth, targetHeight);
                surface.InvalidateVisual();
                return;
            }
            opacityFrom = 0.12;
            opacityTo = 1;
            opacityStarted = DateTime.UtcNow;
            surface.ContentOpacity = opacityFrom;
            resizeFromWidth = ActualWidth > 0 ? ActualWidth : Width;
            resizeFromHeight = ActualHeight > 0 ? ActualHeight : Height;
            resizeToWidth = targetWidth;
            resizeToHeight = targetHeight;
            resizeStarted = DateTime.UtcNow;
            resizing = true;
            AppLog.Write("panel resize expanded=" + expanded);
        }

        private void StepResize()
        {
            if (!resizing) return;
            double elapsed = (DateTime.UtcNow - resizeStarted).TotalSeconds;
            double progress = Math.Min(1, elapsed / 0.20);
            double eased = 1 - Math.Pow(1 - progress, 3);
            double width = resizeFromWidth + (resizeToWidth - resizeFromWidth) * eased;
            double height = resizeFromHeight + (resizeToHeight - resizeFromHeight) * eased;
            ApplyFrame(width, height);
            if (progress >= 1)
            {
                resizing = false;
                ApplyFrame(resizeToWidth, resizeToHeight);
            }
        }

        private void OnMotionTick()
        {
            if (quitting) return;
            StepResize();
            surface.Time = clock.Elapsed.TotalSeconds;
            surface.ReduceMotion = !SystemParameters.ClientAreaAnimation;
            if (resizing)
            {
                double opacityProgress = Math.Min(1,
                    (DateTime.UtcNow - opacityStarted).TotalSeconds / 0.18);
                double easedOpacity = 1 - Math.Pow(1 - opacityProgress, 3);
                surface.ContentOpacity = opacityFrom + (opacityTo - opacityFrom) * easedOpacity;
            }
            else if (surface.ContentOpacity < 1)
            {
                surface.ContentOpacity = 1;
            }

            if (model.Celebrating
                && (DateTime.UtcNow - model.CelebrationStarted).TotalSeconds >= 1.7)
            {
                model.Celebrating = false;
            }

            bool busy = model.Phase.IsBusy() || resizing;
            double now = clock.Elapsed.TotalSeconds;
            double interval = busy ? 0.015 : 0.040;
            if (!panelVisible) return;
            if (now - lastInvalidate >= interval || model.Celebrating)
            {
                lastInvalidate = now;
                surface.InvalidateVisual();
            }
        }

        private void Expand(bool expanded)
        {
            if (model.Expanded == expanded) return;
            if (expanded) CancelCollapse();
            model.SetExpanded(expanded);
        }

        private void SetHovering(bool value)
        {
            hovering = value;
            if (value)
            {
                CancelCollapse();
                if (panelVisible) Expand(true);
            }
            else if (!model.Pinned && settings == null && !externalMenuOpen)
            {
                ScheduleCollapse(0.32);
            }
        }

        private void ScheduleCollapse(double seconds)
        {
            collapseTimer.Stop();
            collapseTimer.Interval = TimeSpan.FromSeconds(seconds);
            collapseTimer.Start();
        }

        private void CancelCollapse()
        {
            collapseTimer.Stop();
        }

        private void OnCollapseTick()
        {
            collapseTimer.Stop();
            if (!panelVisible) return;
            if (model.Pinned || model.SettingsOpen || externalMenuOpen)
                return;
            if (IsPointerInside())
            {
                ScheduleCollapse(0.4);
                return;
            }
            hovering = false;
            Expand(false);
        }

        private bool IsPointerInside()
        {
            try
            {
                if (surface.IsMouseOver) return true;
                NativeMethods.POINT point;
                if (!NativeMethods.GetCursorPos(out point)) return false;
                NativeMethods.RECT rect;
                IntPtr handle = new WindowInteropHelper(this).Handle;
                if (handle == IntPtr.Zero || !NativeMethods.GetWindowRect(handle, out rect)) return false;
                return point.X >= rect.Left && point.X <= rect.Right
                    && point.Y >= rect.Top && point.Y <= rect.Bottom;
            }
            catch
            {
                return false;
            }
        }

        private void ScheduleLaunchPeek()
        {
            if (launchPeekScheduled) return;
            launchPeekScheduled = true;
            peekTimer.Interval = TimeSpan.FromMilliseconds(150);
            peekTimer.Start();
        }

        private void EvaluateVisibility()
        {
            if (!started || quitting) return;
            bool visible = !preferences.hideWhenIdle || model.HasActiveTask || model.Pinned
                || model.SettingsOpen;
            SetPanelVisible(visible);
            UpdateTrayVisibility();
        }

        private void SetPanelVisible(bool visible)
        {
            if (panelVisible == visible) return;
            if (visible)
            {
                CancelCollapse();
                Expand(false);
                ApplyFrame(CollapsedWidth, CollapsedHeight);
                Show();
                panelVisible = true;
                RaiseToTop();
                tray.SetVisible(false);
                surface.InvalidateVisual();
                AppLog.Write("panel shown");
            }
            else
            {
                panelVisible = false;
                surface.HideToolTip();
                CancelCollapse();
                hovering = false;
                Expand(false);
                ApplyFrame(CollapsedWidth, CollapsedHeight);
                Hide();
                UpdateTrayVisibility();
                AppLog.Write("panel hidden: no task in progress");
            }
        }

        private void UpdateTrayVisibility()
        {
            tray.SetVisible(!panelVisible);
        }

        private void TogglePinned()
        {
            AppLog.Write("toggle pin");
            model.SetPinned(!model.Pinned);
        }

        private void ToggleSettings()
        {
            AppLog.Write("toggle settings open=" + (settings == null));
            if (settings != null)
            {
                CloseSettings();
                return;
            }
            OpenSettings(false);
        }

        private void OpenSettings(bool fromMenu)
        {
            if (settings != null) return;
            if (!fromMenu && DateTime.UtcNow < suppressSettingsReopenUntil) return;
            if (!panelVisible) SetPanelVisible(true);
            UpdateMetricsFromWindow();
            model.SetSettingsOpen(true);
            Expand(true);

            SettingsWindow next = new SettingsWindow(theme, preferences);
            settings = next;
            next.Closed += delegate
            {
                bool wasCurrent = settings == next;
                if (wasCurrent) settings = null;
                suppressSettingsReopenUntil = DateTime.UtcNow.AddMilliseconds(300);
                suppressPanelActionsUntil = DateTime.UtcNow.AddMilliseconds(220);
                if (wasCurrent && model.SettingsOpen) model.SetSettingsOpen(false);
                if (wasCurrent) EvaluateVisibility();
            };
            double panelLeft = Math.Max(0, (screenWidthDip - ExpandedWidth) / 2);
            next.PlaceBelow(panelLeft, ExpandedWidth, TopEdge + ExpandedHeight,
                screenWidthDip, screenHeightDip);
            next.Show();
            AppLog.Write("settings opened");
        }

        private void CloseSettings()
        {
            SettingsWindow current = settings;
            if (current == null) return;
            settings = null;
            if (model.SettingsOpen) model.SetSettingsOpen(false);
            current.Close();
            suppressSettingsReopenUntil = DateTime.UtcNow.AddMilliseconds(300);
            suppressPanelActionsUntil = DateTime.UtcNow.AddMilliseconds(220);
            if (!model.Pinned && !hovering) ScheduleCollapse(0.25);
            EvaluateVisibility();
        }

        private void ShowPanelMenu(Point screenPoint)
        {
            if (externalMenuOpen || settings != null
                || DateTime.UtcNow < suppressPanelActionsUntil) return;
            PetMenu.Build(this, false, new System.Drawing.Point((int)screenPoint.X, (int)screenPoint.Y));
        }

        private void ShowTrayMenu()
        {
            if (externalMenuOpen) return;
            System.Drawing.Point point = System.Windows.Forms.Cursor.Position;
            PetMenu.Build(this, true, point);
        }

        internal void BeginExternalMenu()
        {
            externalMenuOpen = true;
            CancelCollapse();
        }

        internal void EndExternalMenu()
        {
            externalMenuOpen = false;
            if (!model.Pinned && settings == null && !hovering) ScheduleCollapse(0.30);
        }

        internal void OpenDshFromMenu()
        {
            DshLauncher.Open(model.Origin, model.Desktop);
        }

        internal void TogglePinnedFromMenu()
        {
            TogglePinned();
        }

        internal void OpenSettingsFromMenu()
        {
            Post(delegate { OpenSettings(true); }, DispatcherPriority.Background);
        }

        private void Post(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            try
            {
                if (Dispatcher.HasShutdownStarted) return;
                Dispatcher.BeginInvoke(action, priority);
            }
            catch { }
        }

        internal void SetHideWhenIdleFromMenu(bool value)
        {
            preferences.SetHideWhenIdle(value);
        }

        internal void SetHidePetFromMenu(bool value)
        {
            preferences.SetHidePet(value);
        }

        internal void QuitFromMenu()
        {
            Quit();
        }

        private void Quit()
        {
            if (quitting) return;
            quitting = true;
            CancelCollapse();
            peekTimer.Stop();
            client.Stop();
            if (settings != null)
            {
                SettingsWindow current = settings;
                settings = null;
                current.Close();
            }
            tray.Dispose();
            Close();
            Application.Current.Shutdown();
        }

        private void ShutdownResources()
        {
            try
            {
                motionTimer.Stop();
                collapseTimer.Stop();
                peekTimer.Stop();
                client.Stop();
                tray.Dispose();
                if (settings != null)
                {
                    SettingsWindow current = settings;
                    settings = null;
                    current.Close();
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("shutdown error: " + ex.Message);
            }
        }
    }
}
