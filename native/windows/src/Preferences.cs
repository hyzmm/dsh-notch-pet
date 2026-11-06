using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Media;

namespace DshNotchPet
{
    /// <summary>
    /// User-facing switches and the selected accent. They live in a small JSON
    /// file under %LOCALAPPDATA%, the Windows equivalent of the macOS
    /// UserDefaults suite.
    /// </summary>
    internal sealed class PetPreferences
    {
        public bool hideWhenIdle { get; set; }
        public bool hideStatusItem { get; set; }
        public bool hidePet { get; set; }
        public string accentHex { get; set; }
        public bool accentDefaultMigration { get; set; }

        public event Action Changed;

        private static readonly object Gate = new object();
        private static PetPreferences current;

        public static PetPreferences Current
        {
            get
            {
                if (current == null)
                {
                    lock (Gate)
                    {
                        if (current == null) current = Load();
                    }
                }
                return current;
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(AppLog.SupportDirectory, "preferences.json"); }
        }

        public PetPreferences() { }

        private static PetPreferences Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath, Encoding.UTF8);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    PetPreferences loaded = serializer.Deserialize<PetPreferences>(json);
                    if (loaded != null)
                    {
                        loaded.accentHex = ColorUtil.NormalizeHex(loaded.accentHex);
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("preferences load failed: " + ex.Message);
            }
            PetPreferences defaults = new PetPreferences();
            defaults.accentHex = null;
            return defaults;
        }

        public void SetHideWhenIdle(bool value)
        {
            if (hideWhenIdle == value) return;
            hideWhenIdle = value;
            Save();
            AppLog.Write("hideWhenIdle=" + value);
            RaiseChanged();
        }

        public void SetHideStatusItem(bool value)
        {
            if (hideStatusItem == value) return;
            hideStatusItem = value;
            Save();
            AppLog.Write("hideStatusItem=" + value);
            RaiseChanged();
        }

        public void SetHidePet(bool value)
        {
            if (hidePet == value) return;
            hidePet = value;
            Save();
            AppLog.Write("hidePet=" + value);
            RaiseChanged();
        }

        internal void SetAccentHex(string hex, bool migration)
        {
            accentHex = ColorUtil.NormalizeHex(hex);
            if (migration) accentDefaultMigration = true;
            Save();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppLog.SupportDirectory);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(this);
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(FilePath))
                {
                    try { File.Replace(temp, FilePath, null); }
                    catch
                    {
                        File.Copy(temp, FilePath, true);
                        File.Delete(temp);
                    }
                }
                else
                {
                    File.Move(temp, FilePath);
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("preferences save failed: " + ex.Message);
            }
        }

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }
    }

    internal sealed class AccentPreset
    {
        public string Id;
        public string Hex;
        public string CompanionHex;
        public string IdleHex;

        public AccentPreset(string id, string hex, string companionHex, string idleHex)
        {
            Id = id;
            Hex = hex;
            CompanionHex = companionHex;
            IdleHex = idleHex;
        }

        public string Name { get { return L10n.AccentPresetName(Id); } }
        public Color color { get { return ColorUtil.ParseHex(Hex); } }
        public Color companion
        {
            get
            {
                return CompanionHex == null ? ColorUtil.Dimmed(color, 0.32)
                    : ColorUtil.ParseHex(CompanionHex);
            }
        }

        public static readonly AccentPreset[] All = new AccentPreset[]
        {
            // The original notch pet palette: cyan for live work and resting,
            // blue for depth. Kept first so it is the default.
            new AccentPreset("glacier", "#45D1FA", "#5B7CFF", "#45D1FA"),
            new AccentPreset("aurora", "#5B7CFF", "#3D5BFF", null),
            new AccentPreset("iris", "#A78BFA", "#7C5CFF", null),
            new AccentPreset("sakura", "#FF8FB8", "#FF5C9E", null),
            new AccentPreset("tangerine", "#FFA65C", "#FF7A3D", null),
            new AccentPreset("mint", "#46E0A6", "#21B98A", null)
        };

        public static AccentPreset Fallback { get { return All[0]; } }
    }

    /// <summary>
    /// The resolved colors for one accent choice. The chosen color drives the
    /// working and resting states; waiting, success and error keep their
    /// semantic colors so the status stays readable whichever accent is selected.
    /// </summary>
    internal sealed class PetPalette
    {
        public Color Primary;
        public Color Companion;
        public Color Idle;
        public Color Waiting;
        public Color Done;
        public Color Error;

        public PetPalette(Color primary)
            : this(primary, ColorUtil.Dimmed(primary, 0.32), ColorUtil.IdleTint(primary))
        {
        }

        public PetPalette(Color primary, Color companion, Color idle)
        {
            Primary = primary;
            Companion = companion;
            Idle = idle;
            Waiting = Color.FromRgb(255, 181, 77);
            Done = Color.FromRgb(89, 240, 173);
            Error = Color.FromRgb(255, 99, 115);
        }

        public Color ColorFor(PetPhase phase)
        {
            switch (phase)
            {
                case PetPhase.Running:
                case PetPhase.Thinking:
                case PetPhase.Tool:
                    return Primary;
                case PetPhase.Waiting:
                    return Waiting;
                case PetPhase.Done:
                    return Done;
                case PetPhase.Error:
                    return Error;
                default:
                    return Idle;
            }
        }

        /// <summary>
        /// The second stop in the pet's body gradient. Working and resting
        /// states blend the accent into its companion, while waiting, success
        /// and errors stay inside their own hue.
        /// </summary>
        public Color SecondToneFor(PetPhase phase)
        {
            switch (phase)
            {
                case PetPhase.Running:
                case PetPhase.Thinking:
                case PetPhase.Tool:
                case PetPhase.Idle:
                    return Companion;
                default:
                    return ColorUtil.Dimmed(ColorFor(phase), 0.32);
            }
        }
    }

    internal sealed class PetTheme
    {
        private static readonly string LegacyDefaultHex = "#5B7CFF";
        private static readonly string LegacyGlacierHex = "#3FD8EC";
        private static readonly object Gate = new object();
        private static PetTheme current;

        private readonly PetPreferences preferences;
        private string hex;
        private Color chosenColor;
        private PetPalette palette;

        public event Action Changed;

        public static PetTheme Current
        {
            get
            {
                if (current == null)
                {
                    lock (Gate)
                    {
                        if (current == null) current = new PetTheme();
                    }
                }
                return current;
            }
        }

        private PetTheme()
        {
            preferences = PetPreferences.Current;
            string stored = ColorUtil.NormalizeHex(preferences.accentHex);
            if (!preferences.accentDefaultMigration)
            {
                if (stored == LegacyDefaultHex || stored == LegacyGlacierHex)
                {
                    stored = AccentPreset.Fallback.Hex;
                    preferences.SetAccentHex(stored, true);
                }
                else
                {
                    preferences.SetAccentHex(stored ?? AccentPreset.Fallback.Hex, true);
                }
            }
            if (stored == null) stored = AccentPreset.Fallback.Hex;

            AccentPreset preset = PresetMatching(stored);
            if (preset != null)
            {
                ApplyPreset(preset, false);
            }
            else
            {
                Color custom = ColorUtil.ParseHex(stored);
                hex = stored;
                chosenColor = custom;
                palette = new PetPalette(custom);
            }
        }

        public string Hex { get { return hex; } }
        public Color ChosenColor { get { return chosenColor; } }
        public PetPalette Palette { get { return palette; } }

        public AccentPreset SelectedPreset { get { return PresetMatching(hex); } }
        public bool IsDefault { get { return string.Equals(hex, AccentPreset.Fallback.Hex, StringComparison.OrdinalIgnoreCase); } }

        public void Select(AccentPreset preset)
        {
            if (preset == null) return;
            ApplyPreset(preset, true);
        }

        public void SetCustom(Color color)
        {
            string next = ColorUtil.ToHex(color);
            if (next == null) return;
            AccentPreset preset = PresetMatching(next);
            if (preset != null)
            {
                Select(preset);
                return;
            }
            hex = next;
            chosenColor = color;
            palette = new PetPalette(color);
            preferences.SetAccentHex(next, false);
            RaiseChanged();
        }

        public void Reset()
        {
            Select(AccentPreset.Fallback);
        }

        private void ApplyPreset(AccentPreset preset, bool persist)
        {
            string next = ColorUtil.NormalizeHex(preset.Hex) ?? AccentPreset.Fallback.Hex;
            Color primary = ColorUtil.ParseHex(next);
            Color companion = preset.CompanionHex == null ? ColorUtil.Dimmed(primary, 0.32)
                : ColorUtil.ParseHex(preset.CompanionHex);
            Color idle = preset.IdleHex == null ? ColorUtil.IdleTint(primary)
                : ColorUtil.ParseHex(preset.IdleHex);
            bool changed = !string.Equals(hex, next, StringComparison.OrdinalIgnoreCase);
            hex = next;
            chosenColor = primary;
            palette = new PetPalette(primary, companion, idle);
            if (persist)
            {
                preferences.SetAccentHex(next, false);
            }
            if (persist || changed) RaiseChanged();
        }

        private static AccentPreset PresetMatching(string value)
        {
            if (value == null) return null;
            for (int i = 0; i < AccentPreset.All.Length; i++)
            {
                if (string.Equals(AccentPreset.All[i].Hex, value, StringComparison.OrdinalIgnoreCase))
                    return AccentPreset.All[i];
            }
            return null;
        }

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }
    }

    internal struct HsbComponents
    {
        public double Hue;
        public double Saturation;
        public double Brightness;
    }

    internal static class ColorUtil
    {
        public static string NormalizeHex(string value)
        {
            if (value == null) return null;
            string text = value.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal)) text = text.Substring(1);
            if (text.Length != 6) return null;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return null;
            }
            return "#" + text.ToUpperInvariant();
        }

        public static Color ParseHex(string value)
        {
            string normalized = NormalizeHex(value);
            if (normalized == null) return Color.FromRgb(69, 209, 250);
            int number = int.Parse(normalized.Substring(1), System.Globalization.NumberStyles.HexNumber);
            return Color.FromRgb((byte)((number >> 16) & 0xFF), (byte)((number >> 8) & 0xFF), (byte)(number & 0xFF));
        }

        public static string ToHex(Color color)
        {
            return String.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        public static Color WithAlpha(Color color, double alpha)
        {
            byte a = (byte)Math.Round(Math.Max(0, Math.Min(1, alpha)) * 255);
            return Color.FromArgb(a, color.R, color.G, color.B);
        }

        public static HsbComponents ToHsb(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            double hue = 0;
            if (delta > 0.00001)
            {
                if (max == r) hue = ((g - b) / delta) % 6;
                else if (max == g) hue = (b - r) / delta + 2;
                else hue = (r - g) / delta + 4;
                hue /= 6;
                if (hue < 0) hue += 1;
            }
            double saturation = max <= 0 ? 0 : delta / max;
            return new HsbComponents { Hue = hue, Saturation = saturation, Brightness = max };
        }

        public static Color FromHsb(double hue, double saturation, double brightness)
        {
            hue = ((hue % 1) + 1) % 1;
            saturation = Math.Max(0, Math.Min(1, saturation));
            brightness = Math.Max(0, Math.Min(1, brightness));
            double c = brightness * saturation;
            double h = hue * 6;
            double x = c * (1 - Math.Abs(h % 2 - 1));
            double r = 0, g = 0, b = 0;
            if (h < 1) { r = c; g = x; }
            else if (h < 2) { r = x; g = c; }
            else if (h < 3) { g = c; b = x; }
            else if (h < 4) { g = x; b = c; }
            else if (h < 5) { r = x; b = c; }
            else { r = c; b = x; }
            double m = brightness - c;
            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }

        public static Color IdleTint(Color color)
        {
            HsbComponents c = ToHsb(color);
            double saturation = c.Saturation < 0.05 ? 0 : Math.Max(0.16, c.Saturation * 0.55);
            double brightness = Math.Max(0.34, Math.Min(0.82, c.Brightness * 0.80));
            return FromHsb(c.Hue, saturation, brightness);
        }

        public static Color Dimmed(Color color, double amount)
        {
            HsbComponents c = ToHsb(color);
            return FromHsb(c.Hue, Math.Min(1, c.Saturation * 1.04),
                Math.Max(0.06, c.Brightness * (1 - amount)));
        }
    }
}
