// ==============================================================================
// NetMonitor - Kit WPF (Fluent 2 / Ace Obsidian): pincéis temáveis, ícones da marca e controles.
// Todos os pincéis são instâncias compartilhadas: trocar a Color em SetTheme() repinta a janela inteira.
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NetMonitor.Ui
{
    public static class K
    {
        public static readonly FontFamily Sans = new FontFamily("Segoe UI Variable Text, Segoe UI");
        public static readonly FontFamily Display = new FontFamily("Segoe UI Variable Display, Segoe UI");
        public static readonly FontFamily Mono = new FontFamily("Consolas, Cascadia Code");
        public static bool Dark = true;

        // Corpo de fonte da interface: tudo cresce 15% (legibilidade em monitor 4K a 100%)
        public const double FontScale = 1.15;
        public static double S(double size) { double v = size <= 11.5 ? size + 1 : size; return Math.Round(v * FontScale * 2) / 2; }   // textos pequenos ganham +1 pt

        // Registro de erros (somente diagnóstico): %LOCALAPPDATA%\NetMonitor\error.log
        public static void Log(Exception ex)
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetMonitor");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "error.log"), DateTime.Now.ToString("s") + "  " + ex + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }

        static readonly Dictionary<string, SolidColorBrush> br = new Dictionary<string, SolidColorBrush>();

        public static SolidColorBrush B(string key)
        {
            SolidColorBrush b;
            if (!br.TryGetValue(key, out b)) { b = new SolidColorBrush(Colors.Transparent); br[key] = b; }
            return b;
        }

        public static Color C(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        public static SolidColorBrush Fixed(string hex)
        {
            return new SolidColorBrush(C(hex));
        }

        static void S(string key, string dark, string light) { B(key).Color = C(Dark ? dark : light); }

        public static event Action ThemeChanged;

        public static void SetTheme(bool dark)
        {
            Dark = dark;
            Tokens t = Pal.For(dark);
            B("canvas").Color = C(t.Canvas);
            B("sidebar").Color = C(t.Sidebar);
            B("card").Color = C(t.Card);
            B("swatch").Color = C(t.Swatch);
            B("dock").Color = C(t.Dock);
            B("flyout").Color = C(t.FlyoutBg);
            B("text").Color = C(t.TextPri);
            B("sub").Color = C(t.TextSec);
            B("brand").Color = C(Pal.Brand);
            B("white").Color = Colors.White;
            S("dim", "#A0F0F4FC", "#A00F1A2C");
            S("line", "#0AFFFFFF", "#0D000000");
            S("line2", "#12FFFFFF", "#16000000");
            S("hover", "#0FFFFFFF", "#0A000000");
            S("btnbg", "#0FFFFFFF", "#FFFFFFFF");
            S("btnbd", "#17FFFFFF", "#290066FF");
            S("btntx", "#FFFFFFFF", "#1E293B");
            S("inbg", "#73000000", "#FFFFFFFF");
            S("inbd", "#1FFFFFFF", "#380066FF");
            S("selbd", "#3B82F6", "#3B82F6");
            S("selbg", "#1A3B82F6", "#1A3B82F6");
            S("selfill", "#1C2A44", "#DDE9FC");
            S("selhover", "#663B82F6", "#663B82F6");
            S("acc", "#60A5FA", "#2563EB");
            S("seg", "#66000000", "#14000000");
            S("segon", "#2563EB", "#2563EB");
            S("green", "#12A150", "#12A150");
            S("shadow", "#66000000", "#22000000");
            if (ThemeChanged != null) ThemeChanged();
        }

        // ------------------------------------------------------------ texto
        public static TextBlock T(string s, double size, FontWeight w, string brush)
        {
            TextBlock t = new TextBlock { Text = s, FontSize = S(size), FontWeight = w, FontFamily = Sans, Foreground = B(brush), TextTrimming = TextTrimming.None };
            return t;
        }

        public static TextBlock T(string s, double size)
        {
            return T(s, size, FontWeights.Normal, "text");
        }

        public static TextBlock T(string s, double size, string brush)
        {
            return T(s, size, FontWeights.Normal, brush);
        }

        public static TextBlock Bold(string s, double size)
        {
            return T(s, size, FontWeights.Bold, "text");
        }

        public static TextBlock Wrap(TextBlock t)
        {
            t.TextWrapping = TextWrapping.Wrap;
            return t;
        }

        // ------------------------------------------------------------ layout
        public static StackPanel V(double gap, params UIElement[] kids)
        {
            StackPanel p = new StackPanel { Orientation = Orientation.Vertical };
            for (int i = 0; i < kids.Length; i++)
            {
                FrameworkElement fe = kids[i] as FrameworkElement;
                if (fe != null && i > 0) fe.Margin = new Thickness(fe.Margin.Left, fe.Margin.Top + gap, fe.Margin.Right, fe.Margin.Bottom);
                p.Children.Add(kids[i]);
            }
            return p;
        }

        public static StackPanel H(double gap, params UIElement[] kids)
        {
            StackPanel p = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < kids.Length; i++)
            {
                FrameworkElement fe = kids[i] as FrameworkElement;
                if (fe != null)
                {
                    if (i > 0) fe.Margin = new Thickness(fe.Margin.Left + gap, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom);
                    if (fe.VerticalAlignment == VerticalAlignment.Stretch) fe.VerticalAlignment = VerticalAlignment.Center;
                }
                p.Children.Add(kids[i]);
            }
            return p;
        }

        // n colunas de mesma largura, com espaçamento; as linhas crescem conforme o conteúdo
        public static Grid Cols(int n, double gap, params UIElement[] kids)
        {
            Grid g = new Grid();
            for (int c = 0; c < n; c++)
            {
                if (c > 0) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            int rows = (kids.Length + n - 1) / n;
            for (int r = 0; r < rows; r++)
            {
                if (r > 0) g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(gap) });
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            for (int i = 0; i < kids.Length; i++)
            {
                int r = i / n, c = i % n;
                Grid.SetColumn(kids[i], c * 2);
                Grid.SetRow(kids[i], r * 2);
                g.Children.Add(kids[i]);
            }
            return g;
        }

        // texto à esquerda, elemento à direita
        public static Grid LR(UIElement left, UIElement right)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(left, 0); Grid.SetColumn(right, 1);
            FrameworkElement fl = left as FrameworkElement, fr = right as FrameworkElement;
            if (fl != null) { fl.VerticalAlignment = VerticalAlignment.Center; fl.HorizontalAlignment = HorizontalAlignment.Left; }
            if (fr != null) { fr.VerticalAlignment = VerticalAlignment.Center; fr.HorizontalAlignment = HorizontalAlignment.Right; }
            g.Children.Add(left); g.Children.Add(right);
            return g;
        }

        public static Border Box(UIElement child, string bg, double radius, Thickness pad)
        {
            return new Border { Child = child, Background = bg == null ? null : B(bg), CornerRadius = new CornerRadius(radius), Padding = pad };
        }

        public static Border Dot(double size, string brush)
        {
            return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = B(brush) };
        }

        public static Border Circle(double size, Brush fill)
        {
            return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = fill };
        }
    }

    // ------------------------------------------------------------------ Ícone da marca (traço 2, grade 24)
    public class Ico : Viewbox
    {
        static readonly Dictionary<string, Geometry[]> cache = new Dictionary<string, Geometry[]>();
        readonly List<Path> paths = new List<Path>();
        readonly List<string> modes = new List<string>();

        public Ico(string name, double size, string brush)
        {
            Width = Height = Math.Round(size * 1.1);
            Stretch = Stretch.Uniform;
            IsHitTestVisible = false;
            string[] data;
            Canvas cv = new Canvas { Width = 24, Height = 24 };
            if (IconData.All.TryGetValue(name, out data))
            {
                Geometry[] geos;
                if (!cache.TryGetValue(name, out geos))
                {
                    geos = new Geometry[data.Length / 2];
                    for (int i = 0; i < geos.Length; i++) { geos[i] = Geometry.Parse(data[i * 2]); geos[i].Freeze(); }
                    cache[name] = geos;
                }
                for (int i = 0; i < geos.Length; i++)
                {
                    Path p = new Path { Data = geos[i], StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
                    paths.Add(p); modes.Add(data[i * 2 + 1]);
                    cv.Children.Add(p);
                }
            }
            Child = cv;
            Tint(K.B(brush));
        }

        public void Tint(Brush b)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                paths[i].Stroke = modes[i].IndexOf('S') >= 0 ? b : null;
                paths[i].Fill = modes[i].IndexOf('F') >= 0 ? b : null;
            }
        }

        public void Tint(string key) { Tint(K.B(key)); }
    }

    // ------------------------------------------------------------------ Cartão selecionável
    public class Sel : Border
    {
        public Action Click;
        public string IdleFill = "swatch", OnFill = "selfill";
        bool sel, hov;
        readonly SolidColorBrush bg = new SolidColorBrush(), bd = new SolidColorBrush();
        static readonly Duration Fade = new Duration(TimeSpan.FromMilliseconds(140));

        public Sel(double radius)
        {
            CornerRadius = new CornerRadius(Math.Round(radius * 0.66));
            BorderThickness = new Thickness(1);
            Cursor = Cursors.Hand;
            Background = bg; BorderBrush = bd;
            MouseEnter += delegate { hov = true; Refresh(true); };
            MouseLeave += delegate { hov = false; Refresh(true); };
            MouseLeftButtonUp += delegate { if (Click != null) Click(); };
            Action theme = delegate { Refresh(false); };
            Loaded += delegate { K.ThemeChanged += theme; Refresh(false); };
            Unloaded += delegate { K.ThemeChanged -= theme; };
            Refresh(false);
        }

        public bool Selected
        {
            get { return sel; }
            set { if (sel != value) { sel = value; Refresh(IsLoaded); } }
        }

        // Cores com transição suave (140 ms); na troca de tema, aplica direto
        void Refresh(bool animate)
        {
            Color fill = K.B(sel ? OnFill : IdleFill).Color;
            Color line = sel ? K.B("selbd").Color : (hov ? K.B("selhover").Color : K.B("line").Color);
            if (!animate)
            {
                bg.BeginAnimation(SolidColorBrush.ColorProperty, null); bg.Color = fill;
                bd.BeginAnimation(SolidColorBrush.ColorProperty, null); bd.Color = line;
                return;
            }
            bg.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(bg.Color, fill, Fade));
            bd.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(bd.Color, line, Fade));
        }
    }

    // ------------------------------------------------------------------ Botão (primário / secundário)
    public class Btn : Border
    {
        public Action Click;
        readonly bool primary;
        readonly TextBlock label;
        bool hov;

        public Btn(string text, bool primary, double fs, Thickness pad, string icon)
        {
            this.primary = primary;
            CornerRadius = new CornerRadius(6);
            BorderThickness = new Thickness(1);
            Padding = pad;
            Cursor = Cursors.Hand;
            label = K.T(text, fs, FontWeights.SemiBold, primary ? "white" : "btntx");
            if (icon != null)
            {
                Ico ic = new Ico(icon, fs + 3, primary ? "white" : "btntx");
                StackPanel sp = K.H(6, ic, label);
                Child = sp;
                this.icon = ic;
            }
            else Child = label;
            MouseEnter += delegate { hov = true; Refresh(); };
            MouseLeave += delegate { hov = false; Refresh(); };
            MouseLeftButtonUp += delegate { if (Click != null) Click(); };
            Refresh();
        }

        Ico icon;

        public string Text { get { return label.Text; } set { label.Text = value; } }

        void Refresh()
        {
            if (primary)
            {
                Background = hov ? K.Fixed("#1A75FF") : K.B("brand");
                BorderBrush = Brushes.Transparent;
            }
            else
            {
                Background = K.B("btnbg");
                BorderBrush = hov ? K.B("brand") : K.B("btnbd");
                label.Foreground = hov ? K.B("brand") : K.B("btntx");
                if (icon != null) icon.Tint(hov ? K.B("brand") : K.B("btntx"));
            }
        }
    }

    // ------------------------------------------------------------------ Segmentado (botões lado a lado)
    public class Seg : Border
    {
        readonly List<Border> items = new List<Border>();
        readonly List<TextBlock> labels = new List<TextBlock>();
        readonly List<Ico> icons = new List<Ico>();
        int cur = -1;
        public Action<int> Changed;

        public Seg(double radius, double fs, bool fill, params string[] texts) : this(radius, fs, fill, texts, null) { }

        public Seg(double radius, double fs, bool fill, string[] texts, string[] iconNames)
        {
            CornerRadius = new CornerRadius(Math.Round(radius * 0.6));
            Background = K.B("seg");
            Padding = new Thickness(3);
            Grid g = new Grid();
            for (int i = 0; i < texts.Length; i++)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = fill ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
                TextBlock tb = K.T(texts[i], fs, FontWeights.SemiBold, "text");
                tb.HorizontalAlignment = HorizontalAlignment.Center;
                UIElement content = tb;
                Ico ic = null;
                if (iconNames != null && iconNames[i] != null)
                {
                    ic = new Ico(iconNames[i], fs + 4, "text");
                    StackPanel sp = K.H(6, ic, tb);
                    sp.HorizontalAlignment = HorizontalAlignment.Center;
                    content = sp;
                }
                Border b = new Border { CornerRadius = new CornerRadius(Math.Max(3, Math.Round(radius * 0.6) - 2)), Padding = new Thickness(16, 8, 16, 8), Child = content, Cursor = Cursors.Hand, Background = Brushes.Transparent };
                int idx = i;
                b.MouseLeftButtonUp += delegate { Select(idx, true); };
                Grid.SetColumn(b, i);
                g.Children.Add(b);
                items.Add(b); labels.Add(tb); icons.Add(ic);
            }
            Child = g;
        }

        public int Index { get { return cur; } }

        public void Select(int i, bool raise)
        {
            cur = i;
            for (int k = 0; k < items.Count; k++)
            {
                bool on = k == i;
                items[k].Background = on ? K.B("segon") : Brushes.Transparent;
                labels[k].Foreground = on ? K.B("white") : K.B("sub");
                labels[k].FontWeight = on ? FontWeights.Bold : FontWeights.SemiBold;
                if (icons[k] != null) icons[k].Tint(on ? K.B("white") : K.B("sub"));
            }
            if (raise && Changed != null) Changed(i);
        }
    }

    // ------------------------------------------------------------------ Interruptor
    public class Switch : Border
    {
        public Action<bool> Changed;
        readonly Border knob;
        bool on;

        public Switch()
        {
            Width = 40; Height = 22; CornerRadius = new CornerRadius(11); Cursor = Cursors.Hand;
            BorderThickness = new Thickness(1);
            knob = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), VerticalAlignment = VerticalAlignment.Center, Background = K.B("white") };
            Child = knob;
            MouseLeftButtonUp += delegate { Value = !on; if (Changed != null) Changed(on); };
            Refresh();
        }

        public bool Value
        {
            get { return on; }
            set { on = value; Refresh(); }
        }

        void Refresh()
        {
            Background = on ? K.B("brand") : K.B("btnbd");
            BorderBrush = on ? K.B("brand") : K.B("line2");
            knob.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            knob.Margin = on ? new Thickness(0, 0, 4, 0) : new Thickness(4, 0, 0, 0);
            knob.Background = on ? K.B("white") : K.B("sub");
        }
    }

    // ------------------------------------------------------------------ Campo de texto
    public class Input : Border
    {
        public readonly TextBox Box;

        public Input(double width, string text, bool mono, double fs)
        {
            Width = width;
            CornerRadius = new CornerRadius(5);
            BorderThickness = new Thickness(1);
            Background = K.B("inbg");
            BorderBrush = K.B("inbd");
            Box = new TextBox
            {
                Text = text, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                Foreground = K.B("text"), CaretBrush = K.B("text"), FontSize = K.S(fs), FontFamily = mono ? K.Mono : K.Sans,
                Padding = new Thickness(8, 4, 8, 4), VerticalContentAlignment = VerticalAlignment.Center, SelectionBrush = K.B("brand")
            };
            Box.GotKeyboardFocus += delegate { BorderBrush = K.B("brand"); };
            Box.LostKeyboardFocus += delegate { BorderBrush = K.B("inbd"); };
            Child = Box;
        }
    }

    // ------------------------------------------------------------------ Seletor (dropdown)
    public class Drop : Border
    {
        public Action<int> Changed;
        readonly TextBlock label;
        readonly Popup popup;
        readonly StackPanel list = new StackPanel();
        string[] opts;
        int cur;

        public Drop(string[] options, double width)
        {
            opts = options;
            Width = width;
            CornerRadius = new CornerRadius(5);
            BorderThickness = new Thickness(1);
            Background = K.B("inbg");
            BorderBrush = K.B("inbd");
            Cursor = Cursors.Hand;
            Padding = new Thickness(10, 6, 8, 6);
            label = K.T("", 12);
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Ico ch = new Ico("acao-dropdown", 14, "sub");
            Grid.SetColumn(label, 0); Grid.SetColumn(ch, 1);
            g.Children.Add(label); g.Children.Add(ch);
            Child = g;

            popup = new Popup { PlacementTarget = this, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, VerticalOffset = 2 };
            Border pb = new Border { Background = K.B("card"), BorderBrush = K.B("line2"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(4), Child = list, MinWidth = width, Margin = new Thickness(0, 0, 8, 8) };
            pb.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.4 };
            popup.Child = pb;
            Build();
            MouseLeftButtonUp += delegate { popup.IsOpen = !popup.IsOpen; };
        }

        void Build()
        {
            list.Children.Clear();
            for (int i = 0; i < opts.Length; i++)
            {
                int idx = i;
                Border it = new Border { Padding = new Thickness(10, 6, 10, 6), CornerRadius = new CornerRadius(4), Cursor = Cursors.Hand, Background = Brushes.Transparent, Child = K.T(opts[i], 12) };
                it.MouseEnter += delegate { it.Background = K.B("hover"); };
                it.MouseLeave += delegate { it.Background = Brushes.Transparent; };
                it.MouseLeftButtonUp += delegate { popup.IsOpen = false; Select(idx, true); };
                list.Children.Add(it);
            }
        }

        public int Index { get { return cur; } }

        public void Select(int i, bool raise)
        {
            cur = i;
            label.Text = (i >= 0 && i < opts.Length) ? opts[i] : "";
            if (raise && Changed != null) Changed(i);
        }
    }

    // ------------------------------------------------------------------ Slider Fluent
    public class FSlider : Grid
    {
        public Action<double> Changed;
        readonly Border track = new Border { Height = 4, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
        readonly Border fill = new Border { Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        readonly Border thumb = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        readonly double min, max, step;
        double val;

        public FSlider(double min, double max, double step)
        {
            this.min = min; this.max = max; this.step = step;
            Height = 24; Background = Brushes.Transparent; Cursor = Cursors.Hand;
            track.Background = K.B("btnbd");
            fill.Background = K.B("brand");
            thumb.Background = K.B("white");
            thumb.BorderBrush = K.B("brand"); thumb.BorderThickness = new Thickness(5);
            Children.Add(track); Children.Add(fill); Children.Add(thumb);
            MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { CaptureMouse(); Pick(e.GetPosition(this).X); };
            MouseMove += delegate(object s, MouseEventArgs e) { if (IsMouseCaptured) Pick(e.GetPosition(this).X); };
            MouseLeftButtonUp += delegate { ReleaseMouseCapture(); };
            SizeChanged += delegate { Layout(); };
        }

        void Pick(double x)
        {
            double w = Math.Max(1, ActualWidth - 18);
            double f = Math.Max(0, Math.Min(1, (x - 9) / w));
            double v = min + f * (max - min);
            v = Math.Round(v / step) * step;
            if (Math.Abs(v - val) > 1e-9) { val = v; Layout(); if (Changed != null) Changed(val); }
        }

        public double Value
        {
            get { return val; }
            set { val = value; Layout(); }
        }

        void Layout()
        {
            double w = Math.Max(0, ActualWidth - 18);
            double f = (val - min) / (max - min);
            thumb.Margin = new Thickness(f * w, 0, 0, 0);
            fill.Width = Math.Max(0, f * w + 9);
        }
    }
}
