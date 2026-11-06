using System;
using System.Drawing;
using System.Windows.Forms;

namespace DshNotchPet
{
    internal static class PetMenu
    {
        public static ContextMenuStrip Build(PetWindow owner, bool trayMenu, Point screenPoint)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = System.Drawing.Color.FromArgb(28, 28, 30);
            menu.ForeColor = System.Drawing.Color.FromArgb(235, 235, 240);
            menu.Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.DropShadowEnabled = true;
            menu.Padding = new Padding(4, 5, 4, 5);
            menu.Renderer = new DarkMenuRenderer();

            ToolStripMenuItem open = Item(L10n.OpenDSH);
            open.Click += delegate { owner.OpenDshFromMenu(); };
            menu.Items.Add(open);

            if (trayMenu)
            {
                ToolStripMenuItem settings = Item(L10n.Settings);
                settings.Click += delegate { owner.OpenSettingsFromMenu(); };
                menu.Items.Add(settings);
                menu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem only = Item(L10n.OnlyWhileWorking);
                only.Checked = owner.Preferences.hideWhenIdle;
                only.Click += delegate
                {
                    owner.SetHideWhenIdleFromMenu(!owner.Preferences.hideWhenIdle);
                };
                menu.Items.Add(only);

                ToolStripMenuItem hidePet = Item(L10n.HidePet);
                hidePet.Checked = owner.Preferences.hidePet;
                hidePet.Click += delegate
                {
                    owner.SetHidePetFromMenu(!owner.Preferences.hidePet);
                };
                menu.Items.Add(hidePet);
            }
            else
            {
                ToolStripMenuItem pin = Item(owner.Model.Pinned ? L10n.Unpin : L10n.Pin);
                pin.Click += delegate { owner.TogglePinnedFromMenu(); };
                menu.Items.Add(pin);
                menu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem settings = Item(L10n.Settings);
                settings.Click += delegate { owner.OpenSettingsFromMenu(); };
                menu.Items.Add(settings);
            }

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem quit = Item(L10n.QuitPet);
            quit.Click += delegate { owner.QuitFromMenu(); };
            menu.Items.Add(quit);

            owner.BeginExternalMenu();
            menu.Closed += delegate
            {
                owner.EndExternalMenu();
                menu.Dispose();
            };
            try
            {
                menu.Show(screenPoint);
            }
            catch
            {
                owner.EndExternalMenu();
                menu.Dispose();
            }
            return menu;
        }

        private static ToolStripMenuItem Item(string text)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.ForeColor = System.Drawing.Color.FromArgb(235, 235, 240);
            item.Padding = new Padding(3, 4, 3, 4);
            return item;
        }

        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground
            {
                get { return System.Drawing.Color.FromArgb(28, 28, 30); }
            }
            public override Color ImageMarginGradientBegin
            {
                get { return System.Drawing.Color.FromArgb(28, 28, 30); }
            }
            public override Color ImageMarginGradientMiddle
            {
                get { return System.Drawing.Color.FromArgb(28, 28, 30); }
            }
            public override Color ImageMarginGradientEnd
            {
                get { return System.Drawing.Color.FromArgb(28, 28, 30); }
            }
            public override Color MenuItemSelected
            {
                get { return System.Drawing.Color.FromArgb(44, 44, 46); }
            }
            public override Color MenuItemBorder
            {
                get { return System.Drawing.Color.FromArgb(58, 58, 60); }
            }
            public override Color MenuItemSelectedGradientBegin
            {
                get { return System.Drawing.Color.FromArgb(44, 44, 46); }
            }
            public override Color MenuItemSelectedGradientEnd
            {
                get { return System.Drawing.Color.FromArgb(44, 44, 46); }
            }
            public override Color MenuItemPressedGradientBegin
            {
                get { return System.Drawing.Color.FromArgb(44, 44, 46); }
            }
            public override Color MenuItemPressedGradientEnd
            {
                get { return System.Drawing.Color.FromArgb(44, 44, 46); }
            }
            public override Color SeparatorDark
            {
                get { return System.Drawing.Color.FromArgb(58, 58, 60); }
            }
            public override Color SeparatorLight
            {
                get { return System.Drawing.Color.FromArgb(58, 58, 60); }
            }
            public override Color CheckBackground
            {
                get { return System.Drawing.Color.FromArgb(58, 58, 60); }
            }
            public override Color CheckSelectedBackground
            {
                get { return System.Drawing.Color.FromArgb(72, 72, 74); }
            }
            public override Color CheckPressedBackground
            {
                get { return System.Drawing.Color.FromArgb(72, 72, 74); }
            }
        }

        private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkColorTable()) { }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (SolidBrush brush = new SolidBrush(System.Drawing.Color.FromArgb(28, 28, 30)))
                    e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (Pen pen = new Pen(System.Drawing.Color.FromArgb(58, 58, 60), 1f))
                    e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.Selected)
                {
                    base.OnRenderMenuItemBackground(e);
                    return;
                }
                using (SolidBrush brush = new SolidBrush(System.Drawing.Color.FromArgb(44, 44, 46)))
                    e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled
                    ? System.Drawing.Color.FromArgb(235, 235, 240)
                    : System.Drawing.Color.FromArgb(99, 99, 102);
                base.OnRenderItemText(e);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using (Pen pen = new Pen(System.Drawing.Color.FromArgb(58, 58, 60), 1f))
                {
                    int y = e.Item.Height / 2;
                    e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
                }
            }

            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                using (Pen pen = new Pen(System.Drawing.Color.FromArgb(235, 235, 240), 1.5f))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    Rectangle r = e.ImageRectangle;
                    float x = r.Left + r.Width * 0.18f;
                    float y = r.Top + r.Height * 0.50f;
                    e.Graphics.DrawLine(pen, x, y, x + r.Width * 0.24f, y + r.Height * 0.25f);
                    e.Graphics.DrawLine(pen, x + r.Width * 0.24f, y + r.Height * 0.25f,
                        x + r.Width * 0.72f, y - r.Height * 0.25f);
                }
            }
        }
    }
}
