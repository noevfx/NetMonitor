// ==============================================================================
// NetMonitor - Modelo de configurações e paleta (Ace Obsidian)
// Fonte única de verdade usada pelo widget (GDI+), pelo flyout e pela janela WPF.
// Cores sempre como "#RRGGBB" para não misturar System.Drawing com System.Windows.Media.
// ==============================================================================

using System;
using System.Globalization;
using Microsoft.Win32;

namespace NetMonitor
{
    public class Tokens
    {
        public string Canvas, Sidebar, Card, Swatch, Dock, WidgetBg, FlyoutBg, TextPri, TextSec;
    }

    public class ColorPreset
    {
        public string Key, Name, Sub, DarkUp, DarkDown;
        public bool Vivid;   // vibrante: contraste mínimo 4,5:1 para preservar a saturação (ex.: azul da marca)
        public string LightUp, LightDown;   // opcional: tons do claro definidos à mão (sem cálculo automático)
        public ColorPreset(string key, string name, string sub, string du, string dd) : this(key, name, sub, du, dd, false) { }
        public ColorPreset(string key, string name, string sub, string du, string dd, bool vivid)
        {
            Key = key; Name = name; Sub = sub; DarkUp = du; DarkDown = dd; Vivid = vivid;
        }
    }

    // Tokens da interface: Ace Obsidian (escuro e claro)
    public static class Pal
    {
        public const string Brand = "#0066FF";

        public static readonly Tokens Dark = new Tokens
        {
            Canvas = "#0A0D14", Sidebar = "#06080E", Card = "#121722", Swatch = "#192030", Dock = "#0C1019",
            WidgetBg = "#1B2233", FlyoutBg = "#111622", TextPri = "#F0F4FC", TextSec = "#9CAAC3"
        };

        public static readonly Tokens Light = new Tokens
        {
            Canvas = "#F4F7FC", Sidebar = "#E6EEFA", Card = "#FFFFFF", Swatch = "#EFF4FD", Dock = "#DCE7F7",
            WidgetBg = "#FFFFFF", FlyoutBg = "#FFFFFF", TextPri = "#0F1A2C", TextSec = "#4A5B75"
        };

        // 4 combinações, desenhadas para a barra escura. No tema claro cada cor é recalculada
        // (mesmo matiz, mais profunda) até ter contraste de leitura com a barra clara.
        //  - accent: segue a cor de destaque do Windows (muda junto com o sistema)
        //  - ambar_ceu: âmbar + azul-céu (par seguro para daltonismo, Okabe-Ito)
        //  - aurora: lavanda + menta
        public static readonly ColorPreset[] Presets = new ColorPreset[]
        {
            new ColorPreset("pure_mono", "Monocromático", "Branco no escuro, preto no claro", "#FFFFFF", "#FFFFFF"),
            new ColorPreset("accent", "Cor de Destaque", "Segue o destaque do Windows", "#0078D4", "#0078D4"),
            new ColorPreset("lima_ciano", "Lima & Ciano", "Elétrico, máximo destaque", "#E2FF00", "#32C8FA") { LightUp = "#7AC600", LightDown = "#0AA6E6" },
            new ColorPreset("ambar_ceu", "Âmbar & Céu", "Seguro para daltonismo", "#FFB347", "#5CC8FF"),
            new ColorPreset("aurora", "Aurora", "Lavanda & menta", "#C4A1FF", "#4FE3C1"),
            new ColorPreset("rosa_esmeralda", "Rosa & Esmeralda", "Complementares vivos", "#FF6FAE", "#34E08A"),
            new ColorPreset("fogo_gelo", "Fogo & Gelo", "Quente sobe, frio desce", "#FF5E57", "#8FD8FF"),
            new ColorPreset("azul_magenta", "Azul & Magenta", "Azul da marca + neon", "#0066FF", "#FF2E9A", true)
        };

        const string DarkBar = "#202020", LightBar = "#F3F3F3";

        // Cor final de um preset para o tema: up = upload, down = download
        public static string Resolve(string key, bool up, bool dark)
        {
            ColorPreset p = Find(key);
            if (p.Key == "pure_mono") return dark ? "#FFFFFF" : "#000000";
            if (!dark && p.LightUp != null) return up ? p.LightUp : p.LightDown;
            string baseHex = up ? p.DarkUp : p.DarkDown;
            if (p.Key == "accent")
            {
                // upload = destaque; download = mesmo matiz, mais claro e menos saturado (tons do próprio destaque)
                string acc = WindowsAccent();
                baseHex = up ? acc : Tint(acc, 0.45);
            }
            double min = p.Vivid ? 4.5 : (dark ? 6.0 : 5.0);
            return dark ? FitContrast(baseHex, DarkBar, min, true) : FitContrast(baseHex, LightBar, min, false);
        }

        public static string WindowsAccent()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    object v = k == null ? null : k.GetValue("AccentColor");
                    if (v != null)
                    {
                        uint abgr = unchecked((uint)Convert.ToInt32(v));
                        int r = (int)(abgr & 0xFF), g = (int)((abgr >> 8) & 0xFF), b = (int)((abgr >> 16) & 0xFF);
                        return string.Format("#{0:X2}{1:X2}{2:X2}", r, g, b);
                    }
                }
            }
            catch { }
            return "#0078D4";
        }

        // ---------------------------------------------------------------- matemática de cor
        static void Rgb(string hex, out double r, out double g, out double b)
        {
            int v = int.Parse(hex.TrimStart('#'), NumberStyles.HexNumber);
            r = ((v >> 16) & 255) / 255.0; g = ((v >> 8) & 255) / 255.0; b = (v & 255) / 255.0;
        }

        static string Hex(double r, double g, double b)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", (int)Math.Round(Clamp(r) * 255), (int)Math.Round(Clamp(g) * 255), (int)Math.Round(Clamp(b) * 255));
        }

        static double Clamp(double v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }

        static double Lin(double c) { return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }

        public static double Luminance(string hex)
        {
            double r, g, b; Rgb(hex, out r, out g, out b);
            return 0.2126 * Lin(r) + 0.7152 * Lin(g) + 0.0722 * Lin(b);
        }

        public static double Contrast(string a, string b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        static void ToHsl(string hex, out double h, out double s, out double l)
        {
            double r, g, b; Rgb(hex, out r, out g, out b);
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            l = (mx + mn) / 2; h = 0; s = 0;
            double d = mx - mn;
            if (d > 1e-9)
            {
                s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn);
                if (mx == r) h = (g - b) / d + (g < b ? 6 : 0);
                else if (mx == g) h = (b - r) / d + 2;
                else h = (r - g) / d + 4;
                h /= 6;
            }
        }

        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1; if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        static string FromHsl(double h, double s, double l)
        {
            if (s < 1e-9) return Hex(l, l, l);
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            return Hex(Hue(p, q, h + 1.0 / 3), Hue(p, q, h), Hue(p, q, h - 1.0 / 3));
        }

        // Mistura com branco (fração 0..1)
        static string Tint(string hex, double f)
        {
            double r, g, b; Rgb(hex, out r, out g, out b);
            return Hex(r + (1 - r) * f, g + (1 - g) * f, b + (1 - b) * f);
        }

        // Ajusta só a luminosidade (mantém o matiz) até atingir o contraste mínimo com o fundo
        public static string FitContrast(string hex, string bg, double min, bool lighten)
        {
            double h, s, l; ToHsl(hex, out h, out s, out l);
            if (!lighten) s = Math.Min(1, s * 1.08);          // no claro, um pouco mais de saturação compensa a cor mais escura
            string c = FromHsl(h, s, l);
            for (int i = 0; i < 100 && Contrast(c, bg) < min; i++)
            {
                l = lighten ? Math.Min(1, l + 0.01) : Math.Max(0, l - 0.01);
                c = FromHsl(h, s, l);
            }
            return c;
        }

        public static ColorPreset Find(string key)
        {
            for (int i = 0; i < Presets.Length; i++)
                if (Presets[i].Key == key) return Presets[i];
            return Presets[0];
        }

        public static Tokens For(bool dark) { return dark ? Dark : Light; }
    }

    public class AppSettings
    {
        public const string RegKey = @"Software\NetMonitor";
        public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        // Geral
        public string Language = "pt";
        public string ThemeMode = "system";          // system | dark | light
        public string Adapter = "Auto";
        public bool LockPosition = false;
        public bool HideOnScreensaver = true;
        public bool StartWithWindows = false;
        public bool LoopbackFilter = true;

        // Unidade e escala
        public string UnitBase = "bits";             // bits | bytes
        public string ScaleMode = "auto";            // auto | mb | kb | individual
        public string ScaleTx = "auto";              // auto | kb | mb | gb
        public string ScaleRx = "auto";

        // Layout do widget
        public string Layout = "two-lines";          // two-lines | one-line
        public string BgMode = "transparent";        // transparent | solid | acrylic
        public bool BorderEnabled = false;
        public string BorderColorMode = "upload";    // neutral | accent | upload | download | custom
        public string BorderCustomHex = "#0F6CBD";
        public double BorderOpacity = 1.0;
        public string MetricsOrder = "up-first";     // up-first | down-first

        // Tipografia
        public string FontFamily = "Segoe UI Variable Display";
        public double FontSize = 10.5;
        public string FontWeight = "bold";           // normal | 600 | bold

        // Setas
        public string ArrowPreset = "triangles";     // triangles | thin | outline | chevrons | text | custom
        public string CustomUp = "▲";
        public string CustomDown = "▼";
        public string ArrowPosition = "left";        // left | right | none
        public double ArrowScale = 1.0;              // tamanho das setas (0,6 a 1,6)
        public bool ShowUnit = true;                 // mostra "Kbps/Mbps" ao lado do número

        // Cores
        public string ColorPreset = "pure_mono";
        public string UpCustom = null;               // sobrepõe o preset quando definido
        public string DownCustom = null;

        // Flyout
        public bool InvertColors = false;           // troca a cor de upload com a de download
        public bool FlyoutEnabled = true;           // desligado: o flyout não abre nem existe em memória
        public bool FlyoutPinned = false;
        public string FlyoutMode = "separated";      // separated | combined

        public AppSettings Clone() { return (AppSettings)this.MemberwiseClone(); }

        // Texto "Campo=valor" por linha (todos os campos públicos): usado para mandar as
        // configurações da janela (processo próprio) para o widget.
        public string ToText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (System.Reflection.FieldInfo f in typeof(AppSettings).GetFields())
            {
                if (f.IsLiteral) continue;
                object v = f.GetValue(this);
                string t = v == null ? "\u0001" : (v is double ? ((double)v).ToString("R", CultureInfo.InvariantCulture) : v.ToString());
                sb.Append(f.Name).Append('=').Append(t.Replace("\n", " ")).Append('\n');
            }
            return sb.ToString();
        }

        public static AppSettings FromText(string text)
        {
            AppSettings s = new AppSettings();
            foreach (string line in text.Split('\n'))
            {
                int i = line.IndexOf('=');
                if (i <= 0) continue;
                System.Reflection.FieldInfo f = typeof(AppSettings).GetField(line.Substring(0, i));
                if (f == null || f.IsLiteral) continue;
                string v = line.Substring(i + 1);
                try
                {
                    if (v == "\u0001") f.SetValue(s, null);
                    else if (f.FieldType == typeof(bool)) f.SetValue(s, bool.Parse(v));
                    else if (f.FieldType == typeof(double)) f.SetValue(s, double.Parse(v, CultureInfo.InvariantCulture));
                    else f.SetValue(s, v);
                }
                catch { }
            }
            return s;
        }

        public bool IsDark(bool systemDark)
        {
            if (ThemeMode == "dark") return true;
            if (ThemeMode == "light") return false;
            return systemDark;
        }

        // Cor sem a inversão (o que o preset ou o usuário definiu para cada sentido)
        public string RawHex(bool up, bool dark)
        {
            string custom = up ? UpCustom : DownCustom;
            if (!string.IsNullOrEmpty(custom)) return custom;
            return Pal.Resolve(ColorPreset, up, dark);
        }

        // InvertColors troca as cores de upload e download entre si
        public string UpHex(bool dark) { return RawHex(!InvertColors, dark); }
        public string DownHex(bool dark) { return RawHex(InvertColors, dark); }

        public string BorderHex(bool dark)
        {
            if (BorderColorMode == "neutral") return dark ? "#FFFFFF" : "#000000";
            if (BorderColorMode == "accent") return Pal.Brand;
            if (BorderColorMode == "download") return DownHex(dark);
            if (BorderColorMode == "custom") return BorderCustomHex;
            return UpHex(dark);
        }

        // Converte "U+25B2", "\\u25B2", "0x25B2" ou "&#x25B2;" no caractere; qualquer outro texto passa como está.
        public static string ParseGlyph(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string t = input.Trim();
            string hex = null;
            if (t.StartsWith("U+", StringComparison.OrdinalIgnoreCase)) hex = t.Substring(2);
            else if (t.StartsWith("\\u", StringComparison.OrdinalIgnoreCase)) hex = t.Substring(2);
            else if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = t.Substring(2);
            else if (t.StartsWith("&#x", StringComparison.OrdinalIgnoreCase) && t.EndsWith(";")) hex = t.Substring(3, t.Length - 4);
            if (hex != null)
            {
                int code;
                if (hex.Length >= 2 && hex.Length <= 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code) && code > 0 && code <= 0x10FFFF && (code < 0xD800 || code > 0xDFFF))
                    return char.ConvertFromUtf32(code);
            }
            return input;
        }

        // Texto exibido para as setas (presets de texto e personalizado)
        public string UpGlyph()
        {
            if (ArrowPreset == "text") return "TX:";
            if (ArrowPreset == "custom") return ParseGlyph(CustomUp);
            return "";
        }

        public string DownGlyph()
        {
            if (ArrowPreset == "text") return "RX:";
            if (ArrowPreset == "custom") return ParseGlyph(CustomDown);
            return "";
        }

        // ---------------------------------------------------------------- Registro
        static string Str(RegistryKey k, string name, string def)
        {
            object v = k.GetValue(name);
            return v == null ? def : v.ToString();
        }

        static bool Bool(RegistryKey k, string name, bool def)
        {
            object v = k.GetValue(name);
            if (v == null) return def;
            try { return Convert.ToInt32(v) == 1; } catch { return def; }
        }

        static double Dbl(RegistryKey k, string name, double def)
        {
            object v = k.GetValue(name);
            double d;
            if (v != null && double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return def;
        }

        public static AppSettings Load(string systemLanguage)
        {
            AppSettings s = new AppSettings();
            s.Language = systemLanguage;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    if (k != null)
                    {
                        s.Language = Str(k, "Language", s.Language);
                        s.ThemeMode = Str(k, "ThemeMode", s.ThemeMode);
                        s.Adapter = Str(k, "SelectedAdapter", s.Adapter);
                        s.LockPosition = Bool(k, "IsLocked", s.LockPosition);
                        s.HideOnScreensaver = Bool(k, "HideOnScreensaver", s.HideOnScreensaver);
                        s.LoopbackFilter = Bool(k, "LoopbackFilter", s.LoopbackFilter);
                        s.UnitBase = Str(k, "UnitBase", s.UnitBase);
                        s.ScaleMode = Str(k, "ScaleMode", s.ScaleMode);
                        s.ScaleTx = Str(k, "ScaleTx", s.ScaleTx);
                        s.ScaleRx = Str(k, "ScaleRx", s.ScaleRx);
                        s.Layout = Str(k, "Layout", s.Layout);
                        s.BgMode = Str(k, "WidgetBgMode", s.BgMode);
                        s.BorderEnabled = Bool(k, "WidgetBorderEnabled", s.BorderEnabled);
                        s.BorderColorMode = Str(k, "BorderColorMode", s.BorderColorMode);
                        s.BorderCustomHex = Str(k, "BorderCustomHex", s.BorderCustomHex);
                        s.BorderOpacity = Dbl(k, "BorderOpacity", s.BorderOpacity);
                        s.MetricsOrder = Str(k, "MetricsOrder", s.MetricsOrder);
                        s.FontFamily = Str(k, "FontFamily", s.FontFamily);
                        s.FontSize = Dbl(k, "FontSize", s.FontSize);
                        s.FontWeight = Str(k, "FontWeight", s.FontWeight);
                        s.ArrowPreset = Str(k, "ArrowPreset", s.ArrowPreset);
                        s.CustomUp = Str(k, "CustomUpChar", s.CustomUp);
                        s.CustomDown = Str(k, "CustomDownChar", s.CustomDown);
                        s.ArrowPosition = Str(k, "ArrowPosition", s.ArrowPosition);
                        s.ArrowScale = Dbl(k, "ArrowScale", s.ArrowScale);
                        s.ShowUnit = Bool(k, "ShowUnit", s.ShowUnit);
                        s.FlyoutEnabled = Bool(k, "FlyoutEnabled", s.FlyoutEnabled);
                        s.InvertColors = Bool(k, "InvertColors", s.InvertColors);
                        s.ColorPreset = Str(k, "ColorPreset", s.ColorPreset);
                        string uc = Str(k, "UpCustom", "");
                        string dc = Str(k, "DownCustom", "");
                        s.UpCustom = uc.Length == 0 ? null : uc;
                        s.DownCustom = dc.Length == 0 ? null : dc;
                        s.FlyoutPinned = Bool(k, "FlyoutPinned", s.FlyoutPinned);
                        s.FlyoutMode = Str(k, "FlyoutMode", s.FlyoutMode);
                    }
                }
            }
            catch { }
            s.StartWithWindows = IsAutoStart();
            return s;
        }

        public void Save()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    if (k == null) return;
                    k.SetValue("Language", Language);
                    k.SetValue("ThemeMode", ThemeMode);
                    k.SetValue("SelectedAdapter", Adapter);
                    k.SetValue("IsLocked", LockPosition ? 1 : 0);
                    k.SetValue("HideOnScreensaver", HideOnScreensaver ? 1 : 0);
                    k.SetValue("LoopbackFilter", LoopbackFilter ? 1 : 0);
                    k.SetValue("UnitBase", UnitBase);
                    k.SetValue("ScaleMode", ScaleMode);
                    k.SetValue("ScaleTx", ScaleTx);
                    k.SetValue("ScaleRx", ScaleRx);
                    k.SetValue("Layout", Layout);
                    k.SetValue("WidgetBgMode", BgMode);
                    k.SetValue("WidgetBorderEnabled", BorderEnabled ? 1 : 0);
                    k.SetValue("BorderColorMode", BorderColorMode);
                    k.SetValue("BorderCustomHex", BorderCustomHex);
                    k.SetValue("BorderOpacity", BorderOpacity.ToString(CultureInfo.InvariantCulture));
                    k.SetValue("MetricsOrder", MetricsOrder);
                    k.SetValue("FontFamily", FontFamily);
                    k.SetValue("FontSize", FontSize.ToString(CultureInfo.InvariantCulture));
                    k.SetValue("FontWeight", FontWeight);
                    k.SetValue("ArrowPreset", ArrowPreset);
                    k.SetValue("CustomUpChar", CustomUp ?? "");
                    k.SetValue("CustomDownChar", CustomDown ?? "");
                    k.SetValue("ArrowPosition", ArrowPosition);
                    k.SetValue("ArrowScale", ArrowScale.ToString(CultureInfo.InvariantCulture));
                    k.SetValue("ShowUnit", ShowUnit ? 1 : 0);
                    k.SetValue("FlyoutEnabled", FlyoutEnabled ? 1 : 0);
                    k.SetValue("InvertColors", InvertColors ? 1 : 0);
                    k.SetValue("ColorPreset", ColorPreset);
                    k.SetValue("UpCustom", UpCustom ?? "");
                    k.SetValue("DownCustom", DownCustom ?? "");
                    k.SetValue("FlyoutPinned", FlyoutPinned ? 1 : 0);
                    k.SetValue("FlyoutMode", FlyoutMode);
                }
            }
            catch { }
            SetAutoStart(StartWithWindows);
        }

        public static bool IsAutoStart()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                    return k != null && k.GetValue("NetMonitor") != null;
            }
            catch { return false; }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (enable) k.SetValue("NetMonitor", "\"" + System.Reflection.Assembly.GetEntryAssembly().Location + "\"");
                    else k.DeleteValue("NetMonitor", false);
                }
            }
            catch { }
        }
    }
}
