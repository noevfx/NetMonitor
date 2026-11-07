// ==============================================================================
// NetMonitor - Janela de Configurações nativa (WPF), Fluent 2 / Ace Obsidian
// Implementa o design validado em paletas_fluent2_showcase.html: sidebar, 7 páginas e painel lateral
// com prévia ao vivo do widget e do flyout (renderizados pelo mesmo código GDI+ da barra de tarefas).
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using D = System.Drawing;

namespace NetMonitor.Ui
{
    public interface ISettingsHost
    {
        AppSettings GetSettings();
        void Apply(AppSettings s, bool persist);
        bool SystemDark();
        void LiveBits(out double up, out double down);
        List<ProcessTrafficItem> TopApps();
        void SamplingDemand(bool on);
        void SettingsClosed();
        void PerfInfo(out double cpuPct, out double ramMb);
        void TopRefreshNow();
    }

    class Win32Owner : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; set; }
    }

    class Rad : Border
    {
        readonly Border dot = K.Dot(8, "brand");
        public Rad()
        {
            Width = 16; Height = 16; CornerRadius = new CornerRadius(8); BorderThickness = new Thickness(1.5);
            Child = dot; dot.HorizontalAlignment = HorizontalAlignment.Center; dot.VerticalAlignment = VerticalAlignment.Center;
            Set(false);
        }
        public void Set(bool on)
        {
            BorderBrush = on ? K.B("selbd") : K.B("sub");
            dot.Visibility = on ? Visibility.Visible : Visibility.Hidden;
        }
    }

    public class SettingsWindow : Window
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        readonly ISettingsHost host;
        AppSettings S, orig;
        readonly List<Action> upd = new List<Action>();
        readonly Dictionary<string, FrameworkElement> pages = new Dictionary<string, FrameworkElement>();
        readonly Dictionary<string, Border> navBox = new Dictionary<string, Border>();
        readonly Dictionary<string, Ico> navIco = new Dictionary<string, Ico>();
        readonly Dictionary<string, TextBlock> navTxt = new Dictionary<string, TextBlock>();
        TextBlock pageTitle, pageSub;
        ScrollViewer scroller;
        string curPage = "cores";
        Grid root;
        ColumnDefinition railCol;
        Border toast;
        TextBlock toastTxt;
        DispatcherTimer toastTimer, liveTimer;
        double dpi = 1.0;

        // prévia
        Image imgWidget, imgFlyout;
        Border flyoutWrap, dockBorder;
        FlyoutHit lastHit;
        int hoverRow = -1;
        bool widgetHover, flyoutHover;
        Btn pinBtn;
        Border perfCpuBox;
        TextBlock lblCpu, lblRam, lblRamWin;
        StackPanel appsList;
        TextBlock appsCount;
        StackPanel pagesHost;

        // Lazy loading, tick enxuto e memória reaproveitada
        bool railVisible = true;
        bool sysDarkCache = true;
        DateTime sysDarkAt = DateTime.MinValue;
        string appsKey = "";
        DateTime ramWinAt = DateTime.MinValue;
        DispatcherTimer changeDebounce;
        D.Bitmap widgetPreviewBuf, flyoutPreviewBuf;
        readonly Process selfProc = Process.GetCurrentProcess();

        static readonly string[] Pages = { "cores", "layout", "fonte", "setas", "apps", "geral", "sobre" };

        public SettingsWindow(ISettingsHost host, string startPage)
        {
            this.host = host;
            S = host.GetSettings();
            orig = S.Clone();
            if (Array.IndexOf(Pages, startPage) >= 0) curPage = startPage;

            Title = "NetMonitor";
            Width = 1560; Height = 980;
            MinWidth = 900; MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            FontFamily = K.Sans;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);   // glifos suaves (sem hinting em grade)
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            try { Icon = Res("icone_128.png"); } catch { }

            K.SetTheme(S.IsDark(SysDark()));
            Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), ScrollStyle());

            Build();
            ApplyAll();

            SourceInitialized += delegate { ApplyChrome(); };
            Loaded += delegate
            {
                try { dpi = VisualTreeHelper.GetDpi(this).DpiScaleX; } catch { dpi = 1.0; }
                ClampToScreen();
                host.SamplingDemand(true);
                RefreshPreview();
                SizeChanged_();
            };
            SizeChanged += delegate { SizeChanged_(); };
            DpiChanged += delegate(object s, DpiChangedEventArgs e) { dpi = e.NewDpi.DpiScaleX; previewDirty = true; RefreshPreview(); };
            Closing += delegate
            {
                if (changeDebounce != null) changeDebounce.Stop();
                host.Apply(orig, false);   // o que não foi salvo é descartado
                host.SamplingDemand(false);
            };
            Closed += delegate
            {
                if (widgetPreviewBuf != null) { widgetPreviewBuf.Dispose(); widgetPreviewBuf = null; }
                if (flyoutPreviewBuf != null) { flyoutPreviewBuf.Dispose(); flyoutPreviewBuf = null; }
                host.SettingsClosed();
            };

            liveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            liveTimer.Tick += delegate { LiveTick(); };
            liveTimer.Start();
        }

        // ------------------------------------------------------------ utilidades
        static readonly Dictionary<string, BitmapImage> resCache = new Dictionary<string, BitmapImage>();

        public static BitmapImage Res(string name)
        {
            BitmapImage hit;
            if (resCache.TryGetValue(name, out hit)) return hit;

            BitmapImage bi = new BitmapImage();
            using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = st;
                bi.EndInit();
            }
            bi.Freeze();
            resCache[name] = bi;
            return bi;
        }

        static Style ScrollStyle()
        {
            string xaml = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'>" +
                "<Grid Width='12' Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True'>" +
                "<Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton>" +
                "<Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton>" +
                "<Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Margin='3,0' CornerRadius='3' Background='#55808A9A'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>" +
                "</Track></Grid></ControlTemplate>";
            Style st = new Style(typeof(System.Windows.Controls.Primitives.ScrollBar));
            st.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse(xaml)));
            return st;
        }

        void ApplyChrome()
        {
            try
            {
                IntPtr h = new WindowInteropHelper(this).Handle;
                int dark = K.Dark ? 1 : 0;
                DwmSetWindowAttribute(h, 20, ref dark, 4);
                Color c = K.B("sidebar").Color;
                int cref = c.R | (c.G << 8) | (c.B << 16);
                DwmSetWindowAttribute(h, 35, ref cref, 4);   // cor da barra de título
                Color tx = K.B("text").Color;
                int tref = tx.R | (tx.G << 8) | (tx.B << 16);
                DwmSetWindowAttribute(h, 36, ref tref, 4);   // texto da barra de título
            }
            catch { }
        }

        void ClampToScreen()
        {
            try
            {
                Rect wa = SystemParameters.WorkArea;
                if (Width > wa.Width - 20) Width = wa.Width - 20;
                if (Height > wa.Height - 20) Height = wa.Height - 20;
                Left = wa.Left + (wa.Width - Width) / 2;
                Top = wa.Top + (wa.Height - Height) / 2;
            }
            catch { }
        }

        void SizeChanged_()
        {
            if (railCol == null) return;
            bool on = ActualWidth >= 1280;
            railCol.Width = on ? new GridLength(424) : new GridLength(0);
            railVisible = on;
        }

        // Tema do sistema com validade curta: evita abrir o registro várias vezes por segundo.
        bool SysDark()
        {
            if ((DateTime.UtcNow - sysDarkAt).TotalSeconds > 3)
            {
                sysDarkCache = host.SystemDark();
                sysDarkAt = DateTime.UtcNow;
            }
            return sysDarkCache;
        }

        public void ShowPage(string key)
        {
            if (Array.IndexOf(Pages, key) < 0) return;
            curPage = key;
            ApplyPage();
        }

        // ------------------------------------------------------------ alterações
        void Changed()
        {
            K.SetTheme(S.IsDark(SysDark()));
            ApplyAll();

            // Arrastar sliders dispara isto dezenas de vezes por segundo: adia a prévia e o IPC.
            if (changeDebounce == null)
            {
                changeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
                changeDebounce.Tick += delegate
                {
                    changeDebounce.Stop();
                    ApplyChrome();
                    host.Apply(S.Clone(), false);
                    previewDirty = true;
                    RefreshPreview();
                };
            }
            changeDebounce.Stop();
            changeDebounce.Start();
        }

        void ApplyAll()
        {
            for (int i = 0; i < upd.Count; i++) upd[i]();
            ApplyPage();
        }

        static string FontLabel(string fam)
        {
            if (fam == "Segoe UI Variable Display") return "Segoe UI Variable";
            if (fam == "Segoe UI") return "Segoe UI Clássico";
            if (fam == "Bahnschrift") return "Bahnschrift (DIN)";
            return fam;
        }

        // ------------------------------------------------------------ casca
        void Build()
        {
            root = new Grid { Background = K.B("canvas") };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(288) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            railCol = new ColumnDefinition { Width = new GridLength(424) };
            root.ColumnDefinitions.Add(railCol);

            FrameworkElement side = BuildSidebar();
            FrameworkElement content = BuildContent();
            FrameworkElement rail = BuildRail();
            Grid.SetColumn(side, 0); Grid.SetColumn(content, 1); Grid.SetColumn(rail, 2);
            root.Children.Add(side); root.Children.Add(content); root.Children.Add(rail);

            // toast
            toastTxt = K.T("", 12, FontWeights.SemiBold, "text");
            toast = new Border
            {
                Background = K.B("card"), BorderBrush = K.B("line2"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 16, 10), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 24), Opacity = 0, IsHitTestVisible = false,
                Child = K.H(8, new Ico("acao-confirmar", 16, "green"), toastTxt)
            };
            toast.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.35 };
            Grid.SetColumnSpan(toast, 3);
            root.Children.Add(toast);
            Content = root;
        }

        void Toast(string msg)
        {
            toastTxt.Text = msg;
            toast.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
            if (toastTimer != null) toastTimer.Stop();
            toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3200) };
            toastTimer.Tick += delegate
            {
                toastTimer.Stop();
                toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)));
            };
            toastTimer.Start();
        }

        // ------------------------------------------------------------ sidebar
        FrameworkElement BuildSidebar()
        {
            Image logo = new Image { Source = Res("icone_128.png"), Width = 44, Height = 44 };
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            StackPanel head = K.H(10, logo, K.V(1, K.Bold("NetMonitor", 15), K.T("Taskbar Traffic Widget", 10.5, "sub")));
            head.Margin = new Thickness(4, 6, 0, 26);

            TextBlock nav = K.T("NAVEGAÇÃO", 10, FontWeights.Bold, "sub");
            nav.Margin = new Thickness(14, 0, 0, 10);

            StackPanel items = new StackPanel();
            AddNav(items, "cores", "nav-cores-tema", "Cores & Tema", null);
            AddNav(items, "layout", "nav-layout-barra", "Layout & System Tray", null);
            AddNav(items, "fonte", "nav-tipografia", "Tipografia", delegate { string l = FontLabel(S.FontFamily); return l.StartsWith("Segoe UI") ? "Segoe UI" : l.Split(' ')[0]; });
            AddNav(items, "setas", "nav-icones-seta", "Ícones de Seta", delegate { return ArrowBadge(); });
            AddNav(items, "apps", "nav-top5", "Top 5 Apps (TX/RX)", delegate { return "Ao Vivo"; });
            AddNav(items, "geral", "nav-geral", "Geral", null);
            AddNav(items, "sobre", "nav-sobre", "Sobre", null);

            // rodapé: marca Noé VFX e links
            Canvas mark = new Canvas { Width = 659.58, Height = 323.92 };
            Action<Geometry> addG = delegate(Geometry g) { mark.Children.Add(new System.Windows.Shapes.Path { Data = g, Fill = K.B("brand") }); };
            Geometry dia = new RectangleGeometry(new Rect(264.64, 79.16, 158.27, 158.27), 4, 4);
            dia.Transform = new TransformGroup { Children = { new RotateTransform(-45), new TranslateTransform(-11.24, 289.45) } };
            addG(dia);
            addG(new RectangleGeometry(new Rect(474.9, 52.77, 184.68, 52.77), 4, 4));
            addG(new RectangleGeometry(new Rect(474.9, 131.92, 105.53, 52.77), 4, 4));
            addG(new RectangleGeometry(new Rect(474.9, 211.07, 184.68, 52.77), 4, 4));
            string[] markPaths = new string[] {
                "M205.41,263.83H4c-2.21,0-4-1.79-4-4V58.42l205.41,205.41Z",
                "M211.06,56.76v201.41L5.65,52.77h201.41c2.21,0,4,1.8,4,4Z",
                "M554.93,6.83l19.85,19.85c3.12,3.12,8.19,3.12,11.31,0l19.85-19.85c2.52-2.52.74-6.83-2.83-6.83h-45.35c-3.56,0-5.35,4.31-2.83,6.83Z",
                "M655.59,290.48h-71.15c-2.21,0-4,1.79-4,4v13.9c1.2,0,2.37.44,3.28,1.23l5.05,4.4c.86.75,1.43,1.75,1.63,2.85h65.19c2.21,0,4-1.79,4-4v-18.38c0-2.21-1.79-4-4-4ZM602.05,311.57h-4.56l-9.25-15.25.2-.56h6.05l5.38,9.25,5.17-9.25h5.69l.23.52-8.91,15.29ZM629.06,299.68h-10.66v2.33h8.8v4.05h-8.8v5.47h-6.05v-15.77h16.71v3.92ZM651.58,311.53h-6.55l-4.27-4.39-4.43,4.39h-6.12l-.2-.61,7.17-7.22c-2.2-2.25-4.76-4.97-6.94-7.29l.24-.65h6.28l4.2,4.3,4.12-4.3h5.96l.43.63-6.95,7.11,7.26,7.4-.2.63Z",
                "M586.8,316.27l-5.05-4.4c-.75-.66-1.87-.66-2.63,0l-5.05,4.4c-.44.38-.69.93-.69,1.51v4.14c0,1.1.9,2,2,2h10.1c1.1,0,2-.9,2-2v-4.14c0-.58-.25-1.13-.69-1.51Z" };
            for (int i = 0; i < markPaths.Length; i++) addG(Geometry.Parse(markPaths[i]));
            Viewbox vb = new Viewbox { Width = 136, Child = mark, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 0, 0, 18) };

            StackPanel links = K.V(8,
                Link("link-site", "noevfx.com", "https://noevfx.com"),
                Link("link-scripts", "noevfx.com/scripts", "https://noevfx.com/scripts"),
                Link("link-repositorio", "github.com/noevfx", "https://github.com/noevfx"));
            links.Margin = new Thickness(8, 0, 0, 0);

            Border foot = new Border { BorderBrush = K.B("line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 16, 0, 0), Child = K.V(0, vb, links) };

            DockPanel dp = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(foot, Dock.Bottom);
            StackPanel top = K.V(0, head, nav, items);
            dp.Children.Add(foot);
            dp.Children.Add(top);

            return new Border { Background = K.B("sidebar"), Padding = new Thickness(20, 20, 20, 20), Child = dp };
        }

        // Sites da Noé VFX no azul da marca; o GitHub em cinza
        FrameworkElement Link(string icon, string text, string url)
        {
            string tone = url.Contains("github") ? "sub" : "brand";
            Ico ic = new Ico(icon, 16, tone);
            TextBlock tb = K.T(text, 12, tone);
            StackPanel sp = K.H(8, ic, tb);
            sp.Background = Brushes.Transparent; sp.Cursor = Cursors.Hand;
            sp.MouseLeftButtonUp += delegate { try { Process.Start(url); } catch { } };
            sp.MouseEnter += delegate { tb.TextDecorations = TextDecorations.Underline; tb.Foreground = K.B("text"); ic.Tint(K.B("text")); };
            sp.MouseLeave += delegate { tb.TextDecorations = null; tb.Foreground = K.B(tone); ic.Tint(K.B(tone)); };
            return sp;
        }

        string ArrowBadge()
        {
            if (S.ArrowPreset == "text") return "TX RX";
            if (S.ArrowPreset == "custom") return (S.CustomUp ?? "") + " " + (S.CustomDown ?? "");
            if (S.ArrowPreset == "thin") return "↑ ↓";
            if (S.ArrowPreset == "outline") return "△ ▽";
            if (S.ArrowPreset == "chevrons") return "˄ ˅";
            return "▲ ▼";
        }

        void AddNav(StackPanel host_, string key, string icon, string label, Func<string> badge)
        {
            Ico ic = new Ico(icon, 18, "text");
            TextBlock tb = K.T(label, 12, FontWeights.SemiBold, "text");
            StackPanel left = K.H(10, ic, tb);
            UIElement inner = left;
            TextBlock bd = null;
            if (badge != null)
            {
                bd = K.T("", 10, FontWeights.SemiBold, "acc");
                Border chip = new Border { Background = K.B("selbg"), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), Child = bd };
                inner = K.LR(left, chip);
            }
            Border b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 11, 14, 11), Margin = new Thickness(0, 0, 0, 6), Cursor = Cursors.Hand, Background = Brushes.Transparent, Child = inner };
            b.MouseEnter += delegate { if (curPage != key) b.Background = K.B("hover"); };
            b.MouseLeave += delegate { if (curPage != key) b.Background = Brushes.Transparent; };
            b.MouseLeftButtonUp += delegate { curPage = key; ApplyPage(); };
            host_.Children.Add(b);
            navBox[key] = b; navIco[key] = ic; navTxt[key] = tb;
            if (badge != null) upd.Add(delegate { bd.Text = badge(); });
        }

        // ------------------------------------------------------------ conteúdo
        static readonly string[][] Titles = new string[][]
        {
            new string[] { "cores", "Cores & Paleta Visual", "Cores do widget e tema da janela." },
            new string[] { "layout", "Layout & System Tray", "Formato, unidade, fundo e borda do widget." },
            new string[] { "fonte", "Tipografia", "Fonte, tamanho e peso dos números." },
            new string[] { "setas", "Ícones de Seta & Glifos", "Estilo e posição das setas." },
            new string[] { "apps", "Top 5 Processos de Rede", "Quem está usando a rede agora." },
            new string[] { "geral", "Geral & Comportamento", "Inicialização e comportamento em segundo plano." },
            new string[] { "sobre", "Sobre o NetMonitor", "Versão, créditos e contato." }
        };

        string shownPage = null;

        void ApplyPage()
        {
            EnsurePage(curPage);
            for (int i = 0; i < Titles.Length; i++)
            {
                string k = Titles[i][0];
                bool on = k == curPage;
                if (on) { pageTitle.Text = Titles[i][1]; pageSub.Text = Titles[i][2]; }
                FrameworkElement pg;
                if (pages.TryGetValue(k, out pg)) pg.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                Border nb = navBox[k];
                nb.Background = on ? K.B("brand") : Brushes.Transparent;
                navTxt[k].Foreground = on ? K.B("white") : K.B("text");
                navTxt[k].FontWeight = on ? FontWeights.Bold : FontWeights.SemiBold;
                navTxt[k].Opacity = on ? 1 : 0.75;
                navIco[k].Tint(on ? K.B("white") : K.B("text"));
                navIco[k].Opacity = on ? 1 : 0.75;
            }
            if (curPage == "apps") RebuildApps();
            if (shownPage != curPage)
            {
                shownPage = curPage;
                FrameworkElement cur;
                if (pages.TryGetValue(curPage, out cur))
                    cur.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(180))) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                if (scroller != null) scroller.ScrollToTop();
            }
        }

        // Constrói uma aba na primeira visita e aplica o estado inicial só dela.
        void EnsurePage(string k)
        {
            if (pages.ContainsKey(k)) return;
            int before = upd.Count;

            FrameworkElement pg = null;
            if (k == "cores") pg = PageCores();
            else if (k == "layout") pg = PageLayout();
            else if (k == "fonte") pg = PageFonte();
            else if (k == "setas") pg = PageSetas();
            else if (k == "apps") pg = PageApps();
            else if (k == "geral") pg = PageGeral();
            else if (k == "sobre") pg = PageSobre();
            if (pg == null) return;

            pg.Visibility = Visibility.Collapsed;
            pages[k] = pg;
            pagesHost.Children.Add(pg);

            for (int i = before; i < upd.Count; i++) { try { upd[i](); } catch { } }
        }

        FrameworkElement BuildContent()
        {
            pageTitle = K.T("", 18, FontWeights.Bold, "text");
            pageSub = K.Wrap(K.T("", 12, "sub"));
            StackPanel header = K.V(2, pageTitle, pageSub);
            header.Margin = new Thickness(0, 0, 0, 20);

            // As abas são criadas sob demanda (EnsurePage); aqui só existe o contêiner.
            pagesHost = new StackPanel();

            scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = new StackPanel { Children = { header, pagesHost }, Margin = new Thickness(36, 30, 28, 24) } };

            Btn reset = new Btn("Restaurar", false, 12, new Thickness(14, 6, 14, 6), "acao-restaurar");
            reset.Click = delegate { ResetDefaults(); };
            Btn cancel = new Btn("Cancelar", false, 12, new Thickness(18, 6, 18, 6), null);
            cancel.Click = delegate { Close(); };
            Btn save = new Btn("Salvar Alterações", true, 12, new Thickness(20, 6, 20, 6), null);
            save.Click = delegate
            {
                host.Apply(S.Clone(), true);
                orig = S.Clone();
                Toast("Configurações salvas.");
            };
            Border footer = new Border
            {
                BorderBrush = K.B("line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(36, 14, 36, 16), Background = K.B("canvas"),
                Child = K.LR(reset, K.H(10, cancel, save))
            };

            DockPanel dp = new DockPanel();
            DockPanel.SetDock(footer, Dock.Bottom);
            dp.Children.Add(footer);
            dp.Children.Add(scroller);
            return dp;
        }

        void ResetDefaults()
        {
            AppSettings d = new AppSettings();
            d.Language = S.Language; d.Adapter = S.Adapter;
            S = d;
            Changed();
            Toast("Padrões restaurados. Salve para manter.");
        }


        // ------------------------------------------------------------ blocos reutilizáveis
        Border CardBox(string title, string desc, UIElement body) { return CardBox(title, desc, null, body); }

        // right: controle opcional à direita do título (ex.: um switch)
        Border CardBox(string title, string desc, UIElement right, UIElement body)
        {
            TextBlock h = K.T(title.ToUpper(), 12, FontWeights.Bold, "text");
            TextBlock p = K.Wrap(K.T(desc, 11, "sub"));
            UIElement head = K.V(2, h, p);
            if (right != null) head = K.LR(head, right);
            StackPanel sp = K.V(16, head, body);
            return new Border { Background = K.B("card"), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = sp, Margin = new Thickness(0, 0, 0, 20) };
        }

        Border IconBox(string icon, double box, double ico, string tint)
        {
            return new Border { Width = box, Height = box, CornerRadius = new CornerRadius(5), Background = K.B("line"), Child = new Ico(icon, ico, tint), };
        }

        Sel Opt(string icon, string title, string desc, out Rad rad, Action click)
        {
            rad = new Rad();
            Border ib = IconBox(icon, 38, 20, "acc");
            TextBlock t = K.Wrap(K.T(title, 12, FontWeights.Bold, "text"));
            TextBlock d = K.Wrap(K.T(desc, 10, "sub"));
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel tx = K.V(2, t, d); tx.Margin = new Thickness(12, 0, 8, 0); tx.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ib, 0); Grid.SetColumn(tx, 1); Grid.SetColumn(rad, 2);
            rad.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(ib); g.Children.Add(tx); g.Children.Add(rad);
            Sel s = new Sel(12) { Padding = new Thickness(14), Child = g, Click = click };
            return s;
        }

        // ------------------------------------------------------------ páginas
        FrameworkElement PageCores()
        {
            // presets
            List<UIElement> cards = new List<UIElement>();
            List<Sel> presetSel = new List<Sel>();
            List<Ico> presetChecks = new List<Ico>();
            for (int i = 0; i < Pal.Presets.Length; i++)
            {
                ColorPreset p = Pal.Presets[i];
                string key = p.Key;
                Border bar = new Border { Width = 56, Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left };
                Ico chk = new Ico("acao-confirmar", 14, "acc");
                Grid top = new Grid();
                top.Children.Add(bar);
                chk.HorizontalAlignment = HorizontalAlignment.Right;
                top.Children.Add(chk);
                TextBlock nm = K.Wrap(K.T(p.Name, 11, FontWeights.Bold, "text"));
                TextBlock sb = K.Wrap(K.T(p.Sub, 9, "sub"));
                sb.MinHeight = K.S(9) * 2.7;   // reserva 2 linhas: a linha TX/RX fica na mesma altura em todos os cartões
                TextBlock tx = K.T("▲ TX", 10, FontWeights.Bold, "text");
                TextBlock rx = K.T("▼ RX", 10, FontWeights.Bold, "text");
                StackPanel bot = K.H(6, tx, K.T("·", 10, "dim"), rx);
                bot.Margin = new Thickness(0, 10, 0, 0);
                Sel c = new Sel(12) { Padding = new Thickness(12), Child = K.V(4, top, nm, sb, bot) };
                c.Click = delegate { S.ColorPreset = key; S.UpCustom = null; S.DownCustom = null; Changed(); };
                int idx = i;
                upd.Add(delegate
                {
                    bool on = S.ColorPreset == key && string.IsNullOrEmpty(S.UpCustom) && string.IsNullOrEmpty(S.DownCustom);
                    c.Selected = on;
                    chk.Visibility = on ? Visibility.Visible : Visibility.Hidden;
                    bool dk = S.IsDark(SysDark());
                    string u = Pal.Resolve(key, true, dk), d = Pal.Resolve(key, false, dk);
                    LinearGradientBrush lg = new LinearGradientBrush(K.C(u), K.C(d), 0);
                    lg.GradientStops.Clear();
                    lg.GradientStops.Add(new GradientStop(K.C(u), 0)); lg.GradientStops.Add(new GradientStop(K.C(u), 0.5));
                    lg.GradientStops.Add(new GradientStop(K.C(d), 0.5)); lg.GradientStops.Add(new GradientStop(K.C(d), 1));
                    bar.Background = lg;
                    tx.Foreground = new SolidColorBrush(K.C(u)); rx.Foreground = new SolidColorBrush(K.C(d));
                });
                cards.Add(c);
            }
            Border presets = CardBox("Combinações de Cores", "Desenhadas no escuro e recalculadas para o claro, sempre legíveis.", K.Cols(4, 10, cards.ToArray()));

            // métricas
            Border sw1, sw2; TextBlock hx1, hx2;
            UIElement m1 = Swatch("Taxa de Upload (TX)", "seta-tx-fina", true, out sw1, out hx1);
            UIElement m2 = Swatch("Taxa de Download (RX)", "seta-rx-fina", false, out sw2, out hx2);
            upd.Add(delegate
            {
                bool dk = S.IsDark(SysDark());
                sw1.Background = new SolidColorBrush(K.C(S.UpHex(dk))); sw2.Background = new SolidColorBrush(K.C(S.DownHex(dk)));
                // seta com contraste: preta sobre cor clara, branca sobre cor escura
                ((Ico)sw1.Child).Tint(new SolidColorBrush(Pal.Luminance(S.UpHex(dk)) > 0.4 ? Colors.Black : Colors.White));
                ((Ico)sw2.Child).Tint(new SolidColorBrush(Pal.Luminance(S.DownHex(dk)) > 0.4 ? Colors.Black : Colors.White));
                hx1.Text = S.UpHex(dk); hx2.Text = S.DownHex(dk);
            });
            Switch swInv = new Switch(); swInv.Changed = delegate(bool v) { S.InvertColors = v; Changed(); };
            upd.Add(delegate { swInv.Value = S.InvertColors; });
            UIElement invert = K.H(10, K.T("Inverter cores", 12, FontWeights.SemiBold, "text"), swInv);
            Border metrics = CardBox("Cores Personalizadas", "Escolha um tom próprio para upload e download.", invert, K.Cols(2, 12, m1, m2));

            // tema
            Seg seg = new Seg(12, 12, true, new string[] { "Seguir Windows (Padrão)", "Escuro", "Claro" }, new string[] { "tema-sistema", "tema-escuro", "tema-claro" });
            seg.Changed = delegate(int i) { S.ThemeMode = i == 0 ? "system" : (i == 1 ? "dark" : "light"); Changed(); };
            upd.Add(delegate { seg.Select(S.ThemeMode == "dark" ? 1 : (S.ThemeMode == "light" ? 2 : 0), false); });
            Border theme = CardBox("Tema", "Segue o Windows ou fica fixo.", seg);

            return K.V(0, presets, metrics, theme);
        }

        Ico circleIco;

        UIElement Swatch(string title, string icon, bool up, out Border circle, out TextBlock hex)
        {
            circle = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(8), Child = new Ico(icon, 18, "white") };   // mesmo canto do logo e dos botões
            ((Ico)circle.Child).HorizontalAlignment = HorizontalAlignment.Center; ((Ico)circle.Child).VerticalAlignment = VerticalAlignment.Center;
            hex = K.T("", 10, "sub"); hex.FontFamily = K.Mono;
            Btn change = new Btn("Alterar", false, 11, new Thickness(12, 5, 12, 5), null);
            change.Click = delegate { PickColor(up); };
            circle.Cursor = Cursors.Hand;
            circle.MouseLeftButtonUp += delegate { PickColor(up); };
            circleIco = (Ico)circle.Child; circleIco.IsHitTestVisible = false;
            Border box = new Border { Background = K.B("swatch"), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Child = K.LR(K.H(12, circle, K.V(2, K.T(title, 12, FontWeights.Bold, "text"), hex)), change) };
            return box;
        }

        void PickColor(bool up)
        {
            try
            {
                bool dk = S.IsDark(SysDark());
                System.Windows.Forms.ColorDialog dlg = new System.Windows.Forms.ColorDialog { FullOpen = true };
                D.Color cur = D.ColorTranslator.FromHtml(up ? S.UpHex(dk) : S.DownHex(dk));
                dlg.Color = cur;
                Win32Owner ow = new Win32Owner { Handle = new WindowInteropHelper(this).Handle };
                if (dlg.ShowDialog(ow) == System.Windows.Forms.DialogResult.OK)
                {
                    string h = string.Format("#{0:X2}{1:X2}{2:X2}", dlg.Color.R, dlg.Color.G, dlg.Color.B);
                    // com as cores invertidas, a cor mostrada em "Upload" vem do campo de download
                    if (up != S.InvertColors) S.UpCustom = h; else S.DownCustom = h;
                    Changed();
                }
            }
            catch { }
        }

        // ---- Layout & System Tray
        FrameworkElement PageLayout()
        {
            // 1) Diagramação
            Rad r1, r2;
            Sel two = Opt("layout-duas-linhas", "Duas Linhas (Vertical)", "Upload em cima, download embaixo", out r1, delegate { S.Layout = "two-lines"; Changed(); });
            Sel one = Opt("layout-linha-unica", "Linha Única (Horizontal)", "Upload e download lado a lado", out r2, delegate { S.Layout = "one-line"; Changed(); });
            upd.Add(delegate { bool t = S.Layout != "one-line"; two.Selected = t; one.Selected = !t; r1.Set(t); r2.Set(!t); });
            Border diag = CardBox("Formato do Widget", "Duas linhas ou uma linha só.", K.Cols(2, 12, two, one));

            // 2) Unidade e escala
            Rad rb, rbits;
            Sel bytes = UnitOpt("MB/s", "Megabytes por segundo", null, "Como arquivos e downloads", out rb, delegate { S.UnitBase = "bytes"; Changed(); });
            Sel bits = UnitOpt("Mbps", "Megabits por segundo", "(Padrão)", "Como provedores e testes de velocidade", out rbits, delegate { S.UnitBase = "bits"; Changed(); });
            upd.Add(delegate { bool b = S.UnitBase != "bytes"; bits.Selected = b; bytes.Selected = !b; rbits.Set(b); rb.Set(!b); });

            string[][] modes = new string[][]
            {
                new string[] { "auto", "Automático (Padrão)", "Kbps → Mbps → Gbps" },
                new string[] { "mb", "", "Sempre em Megas" },
                new string[] { "kb", "", "Sempre em Quilos" },
                new string[] { "individual", "Individual TX/RX", "TX e RX separados" }
            };
            List<UIElement> modeCards = new List<UIElement>();
            List<Sel> modeSel = new List<Sel>();
            List<TextBlock> modeTitle = new List<TextBlock>();
            for (int i = 0; i < modes.Length; i++)
            {
                string key = modes[i][0];
                TextBlock tt = K.Wrap(K.T(modes[i][1], 11, FontWeights.Bold, "text"));
                Sel c = new Sel(12) { Padding = new Thickness(10), Child = K.V(2, tt, K.Wrap(K.T(modes[i][2], 10, "sub"))) };
                c.Click = delegate { S.ScaleMode = key; Changed(); };
                modeCards.Add(c); modeSel.Add(c); modeTitle.Add(tt);
            }
            upd.Add(delegate
            {
                bool bt = S.UnitBase != "bytes";
                modeTitle[1].Text = bt ? "Fixo em Mbps" : "Fixo em MB/s";
                modeTitle[2].Text = bt ? "Fixo em Kbps" : "Fixo em KB/s";
                for (int i = 0; i < modes.Length; i++) { modeSel[i].Selected = S.ScaleMode == modes[i][0]; modeTitle[i].Foreground = modeSel[i].Selected ? K.B("acc") : K.B("text"); }
            });
            Grid modeGrid = K.Cols(4, 8, modeCards.ToArray());

            string[] scaleOpts = new string[] { "Automático (Auto KB/MB/GB)", "Fixo em KB/s (Kbps)", "Fixo em MB/s (Mbps)", "Fixo em GB/s (Gbps)" };
            string[] scaleKeys = new string[] { "auto", "kb", "mb", "gb" };
            Drop dTx = new Drop(scaleOpts, 220), dRx = new Drop(scaleOpts, 220);
            dTx.Changed = delegate(int i) { S.ScaleTx = scaleKeys[i]; Changed(); };
            dRx.Changed = delegate(int i) { S.ScaleRx = scaleKeys[i]; Changed(); };
            upd.Add(delegate { dTx.Select(Array.IndexOf(scaleKeys, S.ScaleTx), false); dRx.Select(Array.IndexOf(scaleKeys, S.ScaleRx), false); });
            Border indiv = new Border
            {
                Background = K.B("line"), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0, 8, 0, 0),
                Child = K.V(8, K.T("Ajuste Individual por Fluxo de Rede:", 11, FontWeights.Bold, "text"),
                    K.Cols(2, 12, K.V(4, K.T("Upload (TX):", 11, "sub"), dTx), K.V(4, K.T("Download (RX):", 11, "sub"), dRx)))
            };
            upd.Add(delegate { indiv.Visibility = S.ScaleMode == "individual" ? Visibility.Visible : Visibility.Collapsed; });

            UIElement scaleHead = K.T("Escala", 12, FontWeights.Bold, "text");
            StackPanel scaleBox = K.V(8, scaleHead, modeGrid, indiv);
            scaleBox.Margin = new Thickness(0, 4, 0, 0);
            Border sepLine = new Border { Height = 1, Background = K.B("line"), Margin = new Thickness(0, 4, 0, 0) };

            Switch swUnit = new Switch(); swUnit.Changed = delegate(bool v) { S.ShowUnit = v; Changed(); };
            upd.Add(delegate { swUnit.Value = S.ShowUnit; });
            Border unitRow = new Border
            {
                BorderBrush = K.B("line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 14, 0, 0), Margin = new Thickness(0, 4, 0, 0),
                Child = K.LR(K.V(2, K.T("Mostrar a unidade", 12, FontWeights.Bold, "text"), K.T("Desligado, o widget mostra só os números e encolhe.", 10, "sub")), swUnit)
            };
            Border unit = CardBox("Unidade & Escala", "Bits ou bytes, e como a escala muda.",
                K.V(12, K.Cols(2, 12, bytes, bits), sepLine, scaleBox, unitRow));

            // 3) Fundo e borda
            
            Sel bgT = BgOpt("fundo-transparente", "Transparente", "Texto direto na barra", delegate { S.BgMode = "transparent"; Changed(); });
            Sel bgS = BgOpt("fundo-pill-solido", "Pill Sólido", "Cartão com fundo", delegate { S.BgMode = "solid"; Changed(); });
            Sel bgA = BgOpt("fundo-pill-translucido", "Pill Translúcido", "Cartão translúcido", delegate { S.BgMode = "acrylic"; Changed(); });
            upd.Add(delegate
            {
                bgT.Selected = S.BgMode == "transparent"; bgS.Selected = S.BgMode == "solid"; bgA.Selected = S.BgMode == "acrylic";
                ShowCheck(bgT, bgT.Selected); ShowCheck(bgS, bgS.Selected); ShowCheck(bgA, bgA.Selected);
            });

            // borda
            TextBlock brdStatus = K.T("", 11, FontWeights.SemiBold, "acc");
            Seg brdSeg = new Seg(10, 11, false, "Desabilitada (Padrão)", "Habilitada");
            brdSeg.Changed = delegate(int i) { S.BorderEnabled = i == 1; Changed(); };
            UIElement brdHead = K.LR(K.H(8, new Ico("borda-contorno", 18, "acc"), K.T("Linha de Contorno / Borda", 12, FontWeights.Bold, "text"), K.T("·", 12, "dim"), new Ico("borda-cor", 16, "acc"), brdStatus), brdSeg);

            string[][] bc = new string[][]
            {
                new string[] { "neutral", "Neutro Sutil", "Translúcido Win11" },
                new string[] { "accent", "Cor de Acento", "Segue a paleta" },
                new string[] { "upload", "Upload (TX)", "Tom de envio" },
                new string[] { "download", "Download (RX)", "Tom de download" }
            };
            List<UIElement> bcCards = new List<UIElement>();
            List<Sel> bcSel = new List<Sel>();
            List<Border> bcDot = new List<Border>();
            for (int i = 0; i < bc.Length; i++)
            {
                string key = bc[i][0];
                Border dot = K.Circle(16, Brushes.Gray);
                Sel c = new Sel(12) { Padding = new Thickness(10), IdleFill = "card", Child = K.H(8, dot, K.V(0, K.T(bc[i][1], 11, FontWeights.Bold, "text"), K.T(bc[i][2], 9, "sub"))) };
                c.Click = delegate { S.BorderColorMode = key; Changed(); };
                bcCards.Add(c); bcSel.Add(c); bcDot.Add(dot);
            }
            Border customSw = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(4), Cursor = Cursors.Hand, BorderBrush = K.B("line2"), BorderThickness = new Thickness(1) };
            Input hexIn = new Input(84, S.BorderCustomHex, true, 12);
            hexIn.Box.FontWeight = FontWeights.Bold;
            Action commitHex = delegate
            {
                string v = hexIn.Box.Text.Trim();
                if (!v.StartsWith("#")) v = "#" + v;
                if (Regex.IsMatch(v, "^#[0-9A-Fa-f]{6}$")) { S.BorderCustomHex = v.ToUpper(); S.BorderColorMode = "custom"; Changed(); }
            };
            hexIn.Box.LostKeyboardFocus += delegate { commitHex(); };
            hexIn.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) commitHex(); };
            customSw.MouseLeftButtonUp += delegate
            {
                try
                {
                    System.Windows.Forms.ColorDialog dlg = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = D.ColorTranslator.FromHtml(S.BorderCustomHex) };
                    if (dlg.ShowDialog(new Win32Owner { Handle = new WindowInteropHelper(this).Handle }) == System.Windows.Forms.DialogResult.OK)
                    {
                        S.BorderCustomHex = string.Format("#{0:X2}{1:X2}{2:X2}", dlg.Color.R, dlg.Color.G, dlg.Color.B);
                        S.BorderColorMode = "custom"; Changed();
                    }
                }
                catch { }
            };
            double[] ops = new double[] { 0.15, 0.35, 0.60, 1.0 };
            string[] opLabels = new string[] { "15%", "35%", "60%", "100%" };
            List<Border> opBtns = new List<Border>();
            List<TextBlock> opTxt = new List<TextBlock>();
            StackPanel opRow = new StackPanel { Orientation = Orientation.Horizontal };
            opRow.Children.Add(K.T("Opacidade:", 11, "sub"));
            for (int i = 0; i < ops.Length; i++)
            {
                double v = ops[i];
                TextBlock tt = K.T(opLabels[i], 10, FontWeights.SemiBold, "text");
                Border ob = new Border { CornerRadius = new CornerRadius(5), Padding = new Thickness(9, 3, 9, 3), Margin = new Thickness(6, 0, 0, 0), Cursor = Cursors.Hand, Child = tt, Background = K.B("btnbg") };
                ob.MouseLeftButtonUp += delegate { S.BorderOpacity = v; Changed(); };
                opRow.Children.Add(ob); opBtns.Add(ob); opTxt.Add(tt);
            }
            UIElement customRow = K.LR(K.H(8, K.T("Cor Personalizada:", 11, "sub"), customSw, hexIn), opRow);
            upd.Add(delegate
            {
                bool dk = S.IsDark(SysDark());
                brdSeg.Select(S.BorderEnabled ? 1 : 0, false);
                for (int i = 0; i < bc.Length; i++) bcSel[i].Selected = S.BorderColorMode == bc[i][0];
                bcDot[0].Background = new SolidColorBrush(dk ? Colors.White : Colors.Black) { Opacity = 0.35 };
                bcDot[1].Background = new SolidColorBrush(K.C(Pal.Brand));
                bcDot[2].Background = new SolidColorBrush(K.C(S.UpHex(dk)));
                bcDot[3].Background = new SolidColorBrush(K.C(S.DownHex(dk)));
                customSw.Background = new SolidColorBrush(K.C(S.BorderCustomHex));
                if (!hexIn.Box.IsKeyboardFocused) hexIn.Box.Text = S.BorderCustomHex;
                for (int i = 0; i < ops.Length; i++)
                {
                    bool on = Math.Abs(S.BorderOpacity - ops[i]) < 0.05;
                    opBtns[i].Background = on ? K.B("segon") : K.B("btnbg");
                    opTxt[i].Foreground = on ? K.B("white") : K.B("text");
                }
                string nm = S.BorderColorMode == "neutral" ? "Neutro Sutil" : (S.BorderColorMode == "accent" ? "Cor de Acento" : (S.BorderColorMode == "upload" ? "Upload TX" : (S.BorderColorMode == "download" ? "Download RX" : "Personalizada " + S.BorderCustomHex)));
                brdStatus.Text = nm + " (" + Math.Round(S.BorderOpacity * 100) + "%)";
            });
            Border borderSec = new Border { BorderBrush = K.B("line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 16, 0, 0), Margin = new Thickness(0, 4, 0, 0), Child = K.V(12, brdHead, K.Cols(4, 8, bcCards.ToArray()), customRow) };
            Border bg = CardBox("Fundo & Borda", "Sem fundo ou com cartão; borda opcional.",
                K.V(12, K.Cols(3, 12, bgT, bgS, bgA), borderSec));

            // 4) Ordem
            Sel oUp = new Sel(12) { Padding = new Thickness(12), Child = K.T("▲ Upload Primeiro / ▼ Download", 12, FontWeights.SemiBold, "text"), Click = delegate { S.MetricsOrder = "up-first"; Changed(); } };
            Sel oDn = new Sel(12) { Padding = new Thickness(12), Child = K.T("▼ Download Primeiro / ▲ Upload", 12, FontWeights.SemiBold, "text"), Click = delegate { S.MetricsOrder = "down-first"; Changed(); } };
            upd.Add(delegate { oUp.Selected = S.MetricsOrder != "down-first"; oDn.Selected = S.MetricsOrder == "down-first"; });
            Border order = CardBox("Ordem das Taxas", "Qual aparece primeiro.", K.Cols(2, 12, oUp, oDn));

            return K.V(0, diag, unit, bg, order);
        }

        Sel UnitOpt(string abbr, string title, string tag, string desc, out Rad rad, Action click)
        {
            rad = new Rad();
            Border ab = new Border { Background = K.B("line"), CornerRadius = new CornerRadius(5), Padding = new Thickness(8, 6, 8, 6), MinWidth = 44, Child = K.T(abbr, 10, FontWeights.Bold, "acc") };
            ((TextBlock)ab.Child).FontFamily = K.Mono; ((TextBlock)ab.Child).HorizontalAlignment = HorizontalAlignment.Center;
            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock ttl = K.Wrap(K.T(title, 12, FontWeights.Bold, "text"));
            head.Children.Add(ttl);
            if (tag != null)
            {
                TextBlock tg = K.T(tag, 10, FontWeights.SemiBold, "acc");
                tg.Margin = new Thickness(8, 1, 0, 0); tg.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(tg, 1); head.Children.Add(tg);
            }
            StackPanel tx = K.V(2, head, K.Wrap(K.T(desc, 10, "sub")));
            tx.Margin = new Thickness(12, 0, 8, 0);
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(ab, 0); Grid.SetColumn(tx, 1); Grid.SetColumn(rad, 2);
            ab.VerticalAlignment = VerticalAlignment.Center; tx.VerticalAlignment = VerticalAlignment.Center; rad.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(ab); g.Children.Add(tx); g.Children.Add(rad);
            return new Sel(12) { Padding = new Thickness(14), Child = g, Click = click };
        }

        Sel BgOpt(string icon, string title, string desc, Action click)
        {
            Ico ic = new Ico(icon, 18, "acc");
            Ico chk = new Ico("acao-confirmar", 14, "acc"); chk.Name = "chk";
            Grid head = new Grid();
            StackPanel l = K.H(8, ic, K.T(title, 12, FontWeights.Bold, "text"));
            head.Children.Add(l);
            chk.HorizontalAlignment = HorizontalAlignment.Right; chk.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(chk);
            return new Sel(12) { Padding = new Thickness(14), Child = K.V(6, head, K.Wrap(K.T(desc, 10, "sub"))), Click = click };
        }

        static void ShowCheck(Sel s, bool on)
        {
            StackPanel sp = (StackPanel)s.Child;
            Grid head = (Grid)sp.Children[0];
            head.Children[1].Visibility = on ? Visibility.Visible : Visibility.Hidden;
        }

        // ---- Tipografia
        FrameworkElement PageFonte()
        {
            string[][] fonts = new string[][]
            {
                new string[] { "Segoe UI Variable Display", "Segoe UI Variable", "Oficial do Windows 11 Fluent 2" },
                new string[] { "Cascadia Code", "Cascadia Code", "Monospaçada Moderna Win11" },
                new string[] { "Consolas", "Consolas", "Monospaçada Técnica Clássica" },
                new string[] { "Segoe UI", "Segoe UI Clássico", "Tipografia padrão do Windows 10" },
                new string[] { "Bahnschrift", "Bahnschrift (DIN)", "Geométrica Proporcional DIN" },
                new string[] { "JetBrains Mono", "JetBrains Mono", "Tipografia para Desenvolvedores" }
            };
            List<UIElement> cards = new List<UIElement>();
            List<Sel> sels = new List<Sel>();
            List<TextBlock> checks = new List<TextBlock>();
            for (int i = 0; i < fonts.Length; i++)
            {
                string fam = fonts[i][0];
                TextBlock chk = K.T(i == 0 ? "Padrão ✓" : "✓", 10, FontWeights.SemiBold, "acc");
                TextBlock sample = K.T("1.4 MB/s · 18.7 MB/s", 11, FontWeights.SemiBold, "text");
                sample.FontFamily = new FontFamily(fam);
                Border sbox = new Border { Background = K.B("line"), CornerRadius = new CornerRadius(5), Padding = new Thickness(10, 7, 10, 7), Child = sample, Margin = new Thickness(0, 8, 0, 0) };
                Sel c = new Sel(12) { Padding = new Thickness(14), Child = K.V(0, K.LR(K.T(fonts[i][1], 12, FontWeights.Bold, "text"), chk), K.T(fonts[i][2], 10, "sub"), sbox) };
                c.Click = delegate { S.FontFamily = fam; Changed(); };
                cards.Add(c); sels.Add(c); checks.Add(chk);
            }
            Input custom = new Input(260, "", false, 12);
            custom.Box.ToolTip = "Digite o nome de qualquer fonte instalada (ex: Aptos, Arial, Lucida Console)";
            TextBlock ph = K.T("Nome de qualquer fonte instalada (ex: Aptos, Arial)", 11, "dim");
            ph.IsHitTestVisible = false; ph.Margin = new Thickness(9, 0, 0, 0); ph.VerticalAlignment = VerticalAlignment.Center;
            Grid cg = new Grid(); cg.Children.Add(custom); cg.Children.Add(ph);
            custom.Box.TextChanged += delegate { ph.Visibility = custom.Box.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible; };
            Btn apply = new Btn("Aplicar Fonte", false, 11, new Thickness(12, 6, 12, 6), null);
            apply.Click = delegate
            {
                string v = custom.Box.Text.Trim();
                if (v.Length == 0) return;
                S.FontFamily = v; Changed();
                if (!WidgetRenderer.FontInstalled(v)) Toast("Fonte \"" + v + "\" não encontrada: o widget usa Segoe UI no lugar.");
            };
            custom.Width = 300;
            UIElement customRow = K.H(8, K.T("Fonte Personalizada:", 11, FontWeights.SemiBold, "sub"), cg, apply);
            Border sepb = new Border { BorderBrush = K.B("line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 14, 0, 0), Child = customRow };
            upd.Add(delegate
            {
                bool any = false;
                for (int i = 0; i < fonts.Length; i++)
                {
                    bool on = string.Equals(S.FontFamily, fonts[i][0], StringComparison.OrdinalIgnoreCase);
                    sels[i].Selected = on; checks[i].Visibility = on ? Visibility.Visible : Visibility.Hidden;
                    any |= on;
                }
                if (!any && !custom.Box.IsKeyboardFocused) custom.Box.Text = S.FontFamily;
            });
            Border family = CardBox("Fonte", "Padrão: Segoe UI Variable, a fonte do Windows 11.", K.V(12, K.Cols(2, 12, cards.ToArray()), sepb));

            // tamanho e peso
            TextBlock sizeLbl = K.T("", 11, FontWeights.Bold, "acc");
            FSlider slider = new FSlider(8, 14, 0.5);
            slider.Changed = delegate(double v) { S.FontSize = v; Changed(); };
            double[] sizes = new double[] { 9.0, 10.0, 10.5, 11.0, 12.0 };
            string[] sizeLabels = new string[] { "9.0 pt", "10.0 pt", "10.5 pt (Padrão)", "11.0 pt", "12.0 pt" };
            List<UIElement> sizeBtns = new List<UIElement>();
            List<Sel> sizeSel = new List<Sel>();
            for (int i = 0; i < sizes.Length; i++)
            {
                double v = sizes[i];
                Sel b = new Sel(8) { Padding = new Thickness(6, 5, 6, 5), Child = K.T(sizeLabels[i], 10, FontWeights.SemiBold, "text"), Click = delegate { S.FontSize = v; Changed(); } };
                ((TextBlock)b.Child).HorizontalAlignment = HorizontalAlignment.Center; ((TextBlock)b.Child).TextAlignment = TextAlignment.Center; ((TextBlock)b.Child).TextWrapping = TextWrapping.Wrap;
                sizeBtns.Add(b); sizeSel.Add(b);
            }
            string[][] weights = new string[][] { new string[] { "normal", "Regular", "Regular (400)" }, new string[] { "600", "Semi-Bold", "Semi-Bold (600)" }, new string[] { "bold", "Bold (Padrão)", "Bold (Negrito 700)" } };
            TextBlock wLbl = K.T("", 11, FontWeights.Bold, "acc");
            List<UIElement> wBtns = new List<UIElement>();
            List<Sel> wSel = new List<Sel>();
            for (int i = 0; i < weights.Length; i++)
            {
                string key = weights[i][0];
                Sel b = new Sel(12) { Padding = new Thickness(10), Child = K.T(weights[i][1], 12, i == 0 ? FontWeights.Normal : (i == 1 ? FontWeights.SemiBold : FontWeights.Bold), "text"), Click = delegate { S.FontWeight = key; Changed(); } };
                ((TextBlock)b.Child).HorizontalAlignment = HorizontalAlignment.Center;
                wBtns.Add(b); wSel.Add(b);
            }
            UIElement tnum = K.LR(K.T("Números Tabulares (tnum):", 11, "sub"), K.T("Ativado (Sem oscilação)", 11, FontWeights.SemiBold, "acc"));
            upd.Add(delegate
            {
                slider.Value = S.FontSize;
                sizeLbl.Text = S.FontSize.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " pt" + (Math.Abs(S.FontSize - 10.5) < 0.01 ? " (Padrão)" : "");
                for (int i = 0; i < sizes.Length; i++) sizeSel[i].Selected = Math.Abs(S.FontSize - sizes[i]) < 0.01;
                for (int i = 0; i < weights.Length; i++) { wSel[i].Selected = S.FontWeight == weights[i][0]; if (wSel[i].Selected) wLbl.Text = weights[i][2]; }
            });
            Border calib = CardBox("Tamanho & Peso", "Proporção dos números na barra.",
                K.Cols(2, 20,
                    K.V(8, K.LR(K.T("Tamanho da Fonte:", 12, FontWeights.SemiBold, "text"), sizeLbl), slider, K.Cols(5, 6, sizeBtns.ToArray())),
                    K.V(8, K.LR(K.T("Peso Tipográfico:", 12, FontWeights.SemiBold, "text"), wLbl), K.Cols(3, 8, wBtns.ToArray()), tnum)));
            return K.V(0, family, calib);
        }

        // ---- Ícones de seta
        FrameworkElement PageSetas()
        {
            string[][] presets = new string[][]
            {
                new string[] { "triangles", "Triângulos Sólidos", "Padrão Clássico Windows", "seta-tx-solida", "seta-rx-solida", "▲", "▼" },
                new string[] { "thin", "Setas Finas Fluent", "Minimalistas em linha", "seta-tx-fina", "seta-rx-fina", "↑", "↓" },
                new string[] { "outline", "Triângulos Vazados", "Geométricos elegantes", "seta-tx-vazada", "seta-rx-vazada", "△", "▽" },
                new string[] { "chevrons", "Chevrons Discretos", "Ultra compactos", "seta-tx-chevron", "seta-rx-chevron", "˄", "˅" },
                new string[] { "text", "Acrônimos de Rede", "TX: / RX: técnico", null, null, "TX:", "RX:" },
                new string[] { "custom", "Personalizado", "Digite qualquer caractere", "seta-personalizada", null, null, null }
            };
            List<UIElement> cards = new List<UIElement>();
            List<Sel> sels = new List<Sel>();
            List<Ico> checks = new List<Ico>();
            Input inUp = null, inDn = null;
            for (int i = 0; i < presets.Length; i++)
            {
                string[] p = presets[i];
                UIElement glyph;
                if (p[3] == null) glyph = K.H(4, K.T("TX", 11, FontWeights.Bold, "text"), K.T("RX", 11, FontWeights.Bold, "text"));
                else if (p[4] == null) glyph = new Ico(p[3], 20, "text");
                else glyph = K.H(2, new Ico(p[3], 18, "text"), new Ico(p[4], 18, "text"));
                Border gb = new Border { Background = K.B("line"), CornerRadius = new CornerRadius(5), Padding = new Thickness(8), MinWidth = 56, Child = glyph };
                ((FrameworkElement)glyph).HorizontalAlignment = HorizontalAlignment.Center;
                Ico chk = new Ico("acao-confirmar", 14, "acc");
                Grid g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                StackPanel tx = K.V(0, K.T(p[1], 12, FontWeights.Bold, "text"), K.T(p[2], 10, "sub")); tx.Margin = new Thickness(12, 0, 8, 0); tx.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(gb, 0); Grid.SetColumn(tx, 1); Grid.SetColumn(chk, 2);
                chk.VerticalAlignment = VerticalAlignment.Center;
                g.Children.Add(gb); g.Children.Add(tx); g.Children.Add(chk);
                string key = p[0]; string u = p[5], d = p[6];
                Sel c = new Sel(12) { Padding = new Thickness(12), Child = g };
                c.Click = delegate
                {
                    S.ArrowPreset = key;
                    if (key != "custom" && u != null) { S.CustomUp = u; S.CustomDown = d; }
                    Changed();
                };
                cards.Add(c); sels.Add(c); checks.Add(chk);
            }
            Border cardPresets = CardBox("Estilo das Setas", "Formato dos indicadores de upload e download.", K.Cols(2, 12, cards.ToArray()));

            // glifos
            inUp = new Input(120, "", false, 13); inDn = new Input(120, "", false, 13);
            inUp.Box.MaxLength = 6; inDn.Box.MaxLength = 6;
            Action fromInput = delegate
            {
                S.CustomUp = inUp.Box.Text; S.CustomDown = inDn.Box.Text; S.ArrowPreset = "custom"; Changed();
            };
            inUp.Box.TextChanged += delegate { if (inUp.Box.IsKeyboardFocused) fromInput(); };
            inDn.Box.TextChanged += delegate { if (inDn.Box.IsKeyboardFocused) fromInput(); };
            TextBlock lblUp = K.T("Símbolo de Upload (TX)", 12, FontWeights.Bold, "text");
            TextBlock lblDn = K.T("Símbolo de Download (RX)", 12, FontWeights.Bold, "text");
            inUp.Width = double.NaN; inDn.Width = double.NaN;
            UIElement upField = K.V(6, K.V(0, lblUp, K.T("Ex: ▲, ↑, TX:, U+25B2", 10, "sub")), inUp);
            UIElement dnField = K.V(6, K.V(0, lblDn, K.T("Ex: ▼, ↓, RX:, U+25BC", 10, "sub")), inDn);
            TextBlock scaleLbl = K.T("", 12, FontWeights.Bold, "text");
            scaleLbl.MinWidth = 52; scaleLbl.TextAlignment = TextAlignment.Center;
            Btn minus = new Btn("−", false, 13, new Thickness(14, 4, 14, 4), null);
            Btn plus = new Btn("+", false, 13, new Thickness(14, 4, 14, 4), null);
            minus.Click = delegate { S.ArrowScale = Math.Max(0.6, Math.Round(S.ArrowScale - 0.1, 1)); Changed(); };
            plus.Click = delegate { S.ArrowScale = Math.Min(1.6, Math.Round(S.ArrowScale + 0.1, 1)); Changed(); };
            upd.Add(delegate { scaleLbl.Text = Math.Round(S.ArrowScale * 100) + "%"; });
            Border sizeRow = new Border
            {
                BorderBrush = K.B("line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 14, 0, 0),
                Child = K.LR(K.V(2, K.T("Tamanho das setas", 12, FontWeights.Bold, "text"), K.T("Vale para upload e download.", 10, "sub")), K.H(6, minus, scaleLbl, plus))
            };

            Seg pos = new Seg(10, 11, false, "À Esquerda (▲ 1.4M)", "À Direita (1.4M ▲)", "Ocultar Setas");
            pos.Changed = delegate(int i) { S.ArrowPosition = i == 0 ? "left" : (i == 1 ? "right" : "none"); Changed(); };
            Border posRow = new Border
            {
                BorderBrush = K.B("line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 14, 0, 0),
                Child = K.LR(K.V(2, K.T("Posição das setas", 12, FontWeights.Bold, "text"), K.Wrap(K.T("Antes, depois ou sem seta.", 10, "sub"))), pos)
            };
            Border cardGlyph = CardBox("Caracteres Próprios", "Qualquer caractere ou texto curto; a barra atualiza na hora.",
                K.V(14, K.Cols(2, 16, upField, dnField), K.Wrap(K.T("Aceita código Unicode: U+25B2, \\u25B2 ou &#x25B2;.", 10, "sub")), sizeRow, posRow));

            upd.Add(delegate
            {
                int sel = -1;
                for (int i = 0; i < presets.Length; i++) { bool on = S.ArrowPreset == presets[i][0]; sels[i].Selected = on; checks[i].Visibility = on ? Visibility.Visible : Visibility.Hidden; if (on) sel = i; }
                bool dk = S.IsDark(SysDark());
                lblUp.Foreground = new SolidColorBrush(K.C(S.UpHex(dk))); lblDn.Foreground = new SolidColorBrush(K.C(S.DownHex(dk)));
                inUp.Box.Foreground = lblUp.Foreground; inDn.Box.Foreground = lblDn.Foreground;
                if (!inUp.Box.IsKeyboardFocused) inUp.Box.Text = S.CustomUp ?? "";
                if (!inDn.Box.IsKeyboardFocused) inDn.Box.Text = S.CustomDown ?? "";
                pos.Select(S.ArrowPosition == "left" ? 0 : (S.ArrowPosition == "right" ? 1 : 2), false);
            });
            return K.V(0, cardPresets, cardGlyph);
        }

        // ---- Top 5
        FrameworkElement PageApps()
        {
            appsList = new StackPanel();
            appsCount = K.T("5 processos ativos", 10, FontWeights.SemiBold, "acc");
            Border cnt = new Border { Background = K.B("selbg"), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3), Child = appsCount };
            Btn refresh = new Btn("Atualizar", false, 11, new Thickness(10, 4, 10, 4), "acao-atualizar");
            refresh.Click = delegate { host.TopRefreshNow(); RebuildApps(); };
            UIElement right = K.H(8, cnt, refresh);
            TextBlock h = K.T("CONSUMO POR APP", 12, FontWeights.Bold, "text");
            h.TextWrapping = TextWrapping.Wrap;
            UIElement head = K.LR(K.V(2, h, K.Wrap(K.T("Upload e download de cada app, medidos a cada segundo.", 11, "sub"))), right);
            ((FrameworkElement)((Grid)head).Children[0]).Margin = new Thickness(0, 0, 16, 0);
            Border card = new Border { Background = K.B("card"), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = K.V(12, head, appsList) };
            return card;
        }

        void RebuildApps()
        {
            if (appsList == null) return;
            List<ProcessTrafficItem> items = host.TopApps();
            appsKey = TopKey(items);
            appsList.Children.Clear();
            bool dk = S.IsDark(SysDark());
            D.Color cu = D.ColorTranslator.FromHtml(S.UpHex(dk)), cd = D.ColorTranslator.FromHtml(S.DownHex(dk));
            Color up = Color.FromRgb(cu.R, cu.G, cu.B), dn = Color.FromRgb(cd.R, cd.G, cd.B);
            int n = items == null ? 0 : Math.Min(5, items.Count);
            appsCount.Text = n + (n == 1 ? " processo ativo" : " processos ativos");
            if (n == 0)
            {
                appsList.Children.Add(new Border { Padding = new Thickness(0, 24, 0, 24), Child = new TextBlock { Text = "Aguardando atividade de rede...", Foreground = K.B("sub"), FontSize = K.S(12), HorizontalAlignment = HorizontalAlignment.Center } });
                return;
            }
            ulong max = 1;
            for (int i = 0; i < n; i++) max = Math.Max(max, items[i].Total);
            for (int i = 0; i < n; i++)
            {
                ProcessTrafficItem it = items[i];
                int ti = FlyoutRenderer.TagIndex(it.Name);
                string[] tb = new string[] { "#3B82F6", "#22C55E", "#6366F1", "#06B6D4", "#8B5CF6" };
                string[] tf = new string[] { "#60A5FA", "#4ADE80", "#818CF8", "#22D3EE", "#A78BFA" };
                // numeração 1-5 no lugar do ícone (como no Manual de Marca)
                TextBlock ini = new TextBlock { Text = (i + 1).ToString(), FontSize = K.S(12), FontFamily = K.Mono, FontWeight = FontWeights.Bold, Foreground = K.B("sub"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Border tag = new Border { Width = 26, Height = 34, Child = ini };
                double fTx = max == 0 ? 0 : (double)it.TxBytes / (it.Total == 0 ? 1 : it.Total);
                Grid bar = new Grid { Width = 150, Height = 6 };
                bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.01, fTx), GridUnitType.Star) });
                bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.01, 1 - fTx), GridUnitType.Star) });
                Border bt = new Border { Background = new SolidColorBrush(up) }; Border br = new Border { Background = new SolidColorBrush(dn) }; Grid.SetColumn(br, 1);
                bar.Children.Add(bt); bar.Children.Add(br);
                Border barBox = new Border { CornerRadius = new CornerRadius(3), ClipToBounds = true, Child = bar, Background = K.B("line"), VerticalAlignment = VerticalAlignment.Center };
                TextBlock vt = new TextBlock { Text = "▲ " + SpeedFmt.ForBytes(S, it.TxBytes, true), FontFamily = K.Sans, FontSize = K.S(12), FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(up), TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                TextBlock vr = new TextBlock { Text = "▼ " + SpeedFmt.ForBytes(S, it.RxBytes, false), FontFamily = K.Sans, FontSize = K.S(12), FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(dn), TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                // colunas de largura fixa: números e barras ficam alinhados de uma linha para outra
                Grid rg = new Grid();
                double[] cw = new double[] { 26, 0, 150, 138, 138 };
                for (int c = 0; c < 5; c++)
                    rg.ColumnDefinitions.Add(new ColumnDefinition { Width = c == 1 ? new GridLength(1, GridUnitType.Star) : new GridLength(cw[c]) });
                UIElement nameCol = NameBlock(it);
                ((FrameworkElement)nameCol).HorizontalAlignment = HorizontalAlignment.Left;
                ((FrameworkElement)nameCol).VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(tag, 0); Grid.SetColumn(nameCol, 1); Grid.SetColumn(barBox, 2); Grid.SetColumn(vt, 3); Grid.SetColumn(vr, 4);
                barBox.Margin = new Thickness(0, 0, 14, 0);
                rg.Children.Add(tag); rg.Children.Add(nameCol); rg.Children.Add(barBox); rg.Children.Add(vt); rg.Children.Add(vr);
                Border row = new Border { CornerRadius = new CornerRadius(5), Padding = new Thickness(10, 8, 10, 8), Background = Brushes.Transparent, Child = rg };
                row.MouseEnter += delegate { row.Background = K.B("hover"); };
                row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
                appsList.Children.Add(row);
            }
        }

        static UIElement NameBlock(ProcessTrafficItem it)
        {
            TextBlock a = K.T(it.Name + ".exe", 12, FontWeights.Bold, "text");
            TextBlock b = K.T((string.IsNullOrEmpty(it.Sub) ? "Aplicativo" : it.Sub) + " — Conexões ativas", 10, "sub");
            a.TextTrimming = TextTrimming.CharacterEllipsis; b.TextTrimming = TextTrimming.CharacterEllipsis;
            StackPanel sp = K.V(0, a, b);
            sp.MaxWidth = 190;
            return sp;
        }

        // ---- Geral
        FrameworkElement PageGeral()
        {
            Switch swStart = new Switch(); swStart.Changed = delegate(bool v) { S.StartWithWindows = v; Changed(); };
            Switch swFly = new Switch(); swFly.Changed = delegate(bool v) { S.FlyoutEnabled = v; Changed(); };
            upd.Add(delegate { swFly.Value = S.FlyoutEnabled; });
            Switch swLoop = new Switch(); swLoop.Changed = delegate(bool v) { S.LoopbackFilter = v; Changed(); };
            Switch swLock = new Switch(); swLock.Changed = delegate(bool v) { S.LockPosition = v; Changed(); };
            Switch swSs = new Switch(); swSs.Changed = delegate(bool v) { S.HideOnScreensaver = v; Changed(); };
            upd.Add(delegate { swStart.Value = S.StartWithWindows; swLoop.Value = S.LoopbackFilter; swLock.Value = S.LockPosition; swSs.Value = S.HideOnScreensaver; });

            Btn factory = new Btn("Restaurar Padrões de Fábrica", false, 12, new Thickness(14, 8, 14, 8), "acao-restaurar");
            factory.Click = delegate { ResetDefaults(); };
            factory.HorizontalAlignment = HorizontalAlignment.Left;
            Border sep = new Border { BorderBrush = K.B("line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 14, 0, 0), Child = factory };
            Border card = CardBox("Sistema", "Inicialização e recursos em segundo plano.",
                K.V(10,
                    SwitchRow("sistema-iniciar", "Iniciar com o Windows", "Abre junto com o Windows, direto na barra.", swStart),
                    SwitchRow("nav-top5", "Flyout de Apps", "Lista dos apps que mais usam a rede ao passar o mouse no widget.", swFly),
                    SwitchRow("sistema-filtro", "Filtro de Sockets em Loopback", "Ignora tráfego interno (127.0.0.1, WSL, Docker).", swLoop),
                    SwitchRow("nav-layout-barra", "Travar Posição do Widget", "Evita arrastar o widget sem querer.", swLock),
                    SwitchRow("nav-sobre", "Ocultar com Tela Bloqueada", "Esconde com a tela bloqueada ou protetor ativo.", swSs),
                    sep));
            return card;
        }

        UIElement SwitchRow(string icon, string title, string desc, Switch sw)
        {
            Border b = new Border { Background = K.B("swatch"), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 14, 16, 14), Child = K.LR(K.V(2, K.H(8, new Ico(icon, 18, "acc"), K.T(title, 12, FontWeights.Bold, "text")), K.T(desc, 10, "sub")), sw) };
            return b;
        }

        // ---- Sobre
        FrameworkElement PageSobre()
        {
            const string pixPayload = "00020126780014br.gov.bcb.pix0136a772dce2-7cf6-45db-a699-2102e189b30b0216Apoio NetMonitor5204000053039865802BR5907NOE VFX6006MACEIO62140510NETMONITOR6304FD1D";
            const string pixKey = "a772dce2-7cf6-45db-a699-2102e189b30b";

            Image logo = new Image { Source = Res("icone_256.png"), Width = 56, Height = 56 };
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            Border ver = new Border { Background = K.B("selbg"), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3), Child = K.T("v" + AppVersion.Text + " Release", 10, FontWeights.Bold, "acc"), VerticalAlignment = VerticalAlignment.Center };
            TextBlock made = K.T("Criado artesanalmente por Noé VFX", 11, "sub");
            TextBlock about = K.Wrap(K.T("Desenvolvido artesanalmente para quem ama precisão, visual perfeitamente integrado ao design nativo do Windows 11 e zero desperdício de recursos. Sem telemetria, sem anúncios e sem dependências pesadas.", 11, "sub"));
            about.LineHeight = 18;
            StackPanel links = K.H(10, SiteLink("link-site", "noevfx.com", "https://noevfx.com"), SiteLink("link-scripts", "noevfx.com/scripts", "https://noevfx.com/scripts"), SiteLink("link-repositorio", "github.com/noevfx", "https://github.com/noevfx"));
            Border c1 = new Border { Background = K.B("card"), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 20), Child = K.V(14, K.H(14, logo, K.V(2, K.H(10, K.Bold("NetMonitor for Windows 11", 16), ver), made)), about, links) };

            // café + PIX
            Image qr = new Image { Source = Res("pix_qrcode.png"), Width = 116, Height = 116 };
            RenderOptions.SetBitmapScalingMode(qr, BitmapScalingMode.NearestNeighbor);
            Border qrBox = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = qr, VerticalAlignment = VerticalAlignment.Top };
            Input pix = new Input(10, pixPayload, true, 10); pix.Width = double.NaN; pix.Box.IsReadOnly = true; pix.HorizontalAlignment = HorizontalAlignment.Stretch;
            Btn copyPix = new Btn("Copiar", false, 11, new Thickness(12, 6, 12, 6), "acao-copiar");
            copyPix.Click = delegate { CopyText(pixPayload); copyPix.Text = "✓ Copiado!"; Later(3000, delegate { copyPix.Text = "Copiar"; }); Toast("Código Pix Copia e Cola copiado!"); };
            Grid pixRow = new Grid();
            pixRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pixRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(pix, 0); Grid.SetColumn(copyPix, 1); copyPix.Margin = new Thickness(8, 0, 0, 0);
            pixRow.Children.Add(pix); pixRow.Children.Add(copyPix);
            Btn copyKey = new Btn("Chave: " + pixKey, false, 11, new Thickness(12, 6, 12, 6), "sobre-chave");
            copyKey.Click = delegate { CopyText(pixKey); Toast("Chave Aleatória Pix copiada!"); };
            Btn dlQr = new Btn("Baixar QR Code", false, 11, new Thickness(12, 6, 12, 6), "acao-baixar");
            dlQr.Click = delegate { SaveQr(); };
            StackPanel pixRight = K.V(10,
                K.LR(K.H(6, new Ico("sobre-qr", 16, "acc"), K.T("PIX Copia e Cola", 12, FontWeights.Bold, "text")), K.T("Chave Aleatória blindada", 10, "sub")),
                pixRow, new WrapPanel { Children = { copyKey, dlQr } });
            ((WrapPanel)pixRight.Children[2]).Children[1].SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
            Grid pixGrid = new Grid();
            pixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pixRight.Margin = new Thickness(16, 0, 0, 0);
            Grid.SetColumn(qrBox, 0); Grid.SetColumn(pixRight, 1);
            pixGrid.Children.Add(qrBox); pixGrid.Children.Add(pixRight);
            Border pixWell = new Border { Background = K.B("swatch"), CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Child = pixGrid };

            Border nub = new Border { Background = K.B("line"), CornerRadius = new CornerRadius(5), Padding = new Thickness(10, 4, 10, 4), VerticalAlignment = VerticalAlignment.Top, Child = K.T("Noé VFX  |  Nubank PJ", 10, FontWeights.SemiBold, "sub") };
            Grid coffeeHead = new Grid();
            coffeeHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            coffeeHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            coffeeHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border cafeIco = IconBox("sobre-cafe", 40, 22, "acc"); cafeIco.VerticalAlignment = VerticalAlignment.Top;
            StackPanel cafeTx = K.V(2, K.Bold("Pague um Café ao Desenvolvedor", 13), K.Wrap(K.T("Gostou do NetMonitor rodando lisinho aí na sua barra de tarefas? Apoie esse simplório desenvolvedor hobbysta independente.", 11, "sub")));
            cafeTx.Margin = new Thickness(12, 0, 12, 0);
            Grid.SetColumn(cafeTx, 1); Grid.SetColumn(nub, 2);
            coffeeHead.Children.Add(cafeIco); coffeeHead.Children.Add(cafeTx); coffeeHead.Children.Add(nub);
            TextBlock coffeeTx = K.Wrap(K.T("O NetMonitor foi criado de coração para a comunidade, sem anúncios e com foco em performance pura. Se o app facilitou sua rotina e deixou seu setup mais elegante, considere pagar um cafezinho ou um pão de queijo! Toda contribuição ajuda diretamente a manter o código afiado e novas atualizações saindo do forno.", 11, "sub"));
            coffeeTx.LineHeight = 18;
            Border c2 = new Border { Background = K.B("card"), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 20), Child = K.V(14, coffeeHead, coffeeTx, pixWell) };

            // e-mail
            Btn copyMail = new Btn("Copiar", false, 11, new Thickness(12, 6, 12, 6), "acao-copiar");
            copyMail.Click = delegate { CopyText("labs@noevfx.com"); copyMail.Text = "✓ Copiado!"; Later(3000, delegate { copyMail.Text = "Copiar"; }); Toast("E-mail labs@noevfx.com copiado!"); };
            Btn openMail = new Btn("Abrir no E-mail", true, 11, new Thickness(14, 6, 14, 6), "link-externo");
            openMail.Click = delegate { try { Process.Start("mailto:labs@noevfx.com?subject=Feedback%20NetMonitor%20v" + AppVersion.Text); } catch { } };
            Border mailRow = new Border { Background = K.B("swatch"), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Child = K.LR(K.H(8, new Ico("sobre-email", 18, "acc"), K.T("labs@noevfx.com", 13, FontWeights.Bold, "text")), K.H(8, copyMail, openMail)) };
            TextBlock mailTx = K.Wrap(K.T("Sua opinião molda o futuro do NetMonitor. Encontrou algum bug, tem sugestões para a próxima versão, dúvidas de configuração ou críticas construtivas? Mande um e-mail diretamente para:", 11, "sub"));
            mailTx.LineHeight = 18;
            TextBlock copy = K.T("© 2026 Noé VFX. Todos os direitos reservados.", 10, "dim");
            Border c3 = new Border { Background = K.B("card"), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = K.V(14, K.H(12, IconBox("sobre-email", 40, 22, "acc"), K.V(2, K.Bold("Dúvidas, Críticas, Sugestões & Bugs", 13), K.T("Canal direto com a equipe Noé VFX", 11, "sub"))), mailTx, mailRow, copy) };
            return K.V(0, c1, c2, c3);
        }

        UIElement SiteLink(string icon, string text, string url)
        {
            TextBlock tb = K.T(text, 11, FontWeights.SemiBold, "text");
            Border b = new Border { Background = K.B("btnbg"), BorderBrush = K.B("btnbd"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 7, 12, 7), Cursor = Cursors.Hand, Child = K.H(6, new Ico(icon, 16, "acc"), tb) };
            b.MouseEnter += delegate { b.BorderBrush = K.B("brand"); };
            b.MouseLeave += delegate { b.BorderBrush = K.B("btnbd"); };
            b.MouseLeftButtonUp += delegate { try { Process.Start(url); } catch { } };
            return b;
        }

        static void CopyText(string t)
        {
            try { Clipboard.SetText(t); } catch { }
        }

        void SaveQr()
        {
            try
            {
                System.Windows.Forms.SaveFileDialog dlg = new System.Windows.Forms.SaveFileDialog { FileName = "pix_noevfx.png", Filter = "PNG|*.png" };
                if (dlg.ShowDialog(new Win32Owner { Handle = new WindowInteropHelper(this).Handle }) == System.Windows.Forms.DialogResult.OK)
                {
                    using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("pix_qrcode.png"))
                    using (FileStream fs = File.Create(dlg.FileName)) st.CopyTo(fs);
                    Toast("QR Code salvo.");
                }
            }
            catch { }
        }

        static void Later(int ms, Action a)
        {
            DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            t.Tick += delegate { t.Stop(); a(); };
            t.Start();
        }

        // ------------------------------------------------------------ painel lateral (prévia ao vivo)
        FrameworkElement BuildRail()
        {
            Ico live = new Ico("status-ao-vivo", 12, "green");
            TextBlock title = K.T("Flyout na Barra de Tarefas", 14, FontWeights.Bold, "text");
            pinBtn = new Btn("Ao Passar Mouse", false, 10, new Thickness(8, 3, 8, 3), "acao-fixar");
            pinBtn.Click = delegate { S.FlyoutPinned = !S.FlyoutPinned; Changed(); };
            UIElement hdr = K.V(4, K.H(6, title, live), K.LR(K.T("Prévia do widget e menu flutuante", 11, "sub"), pinBtn));
            ((FrameworkElement)hdr).Margin = new Thickness(0, 0, 0, 14);
            upd.Add(delegate { pinBtn.Text = S.FlyoutPinned ? "Fixado Aberto ✓" : "Ao Passar Mouse"; });

            // flyout (imagem do renderer), recolhível com animação
            imgFlyout = new Image { Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            imgFlyout.MouseMove += delegate(object s, MouseEventArgs e) { FlyoutMouse(e.GetPosition(imgFlyout), false); };
            imgFlyout.MouseLeave += delegate { flyoutHover = false; hoverRow = -1; UpdateFlyoutOpen(); RenderFlyout(); };
            imgFlyout.MouseEnter += delegate { flyoutHover = true; UpdateFlyoutOpen(); };
            imgFlyout.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e) { FlyoutMouse(e.GetPosition(imgFlyout), true); };
            flyoutWrap = new Border { Height = 360, Opacity = 0.72, ClipToBounds = true, Child = imgFlyout, Margin = new Thickness(0, 0, 0, 14), HorizontalAlignment = HorizontalAlignment.Left };

            // doca com widget
            imgWidget = new Image { Stretch = Stretch.None, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            imgWidget.MouseEnter += delegate { widgetHover = true; UpdateFlyoutOpen(); };
            imgWidget.MouseLeave += delegate { widgetHover = false; UpdateFlyoutOpen(); };
            imgWidget.MouseLeftButtonUp += delegate { S.FlyoutPinned = !S.FlyoutPinned; Changed(); };
            TextBlock start = new TextBlock { Text = "❖", FontSize = 15, Foreground = K.B("acc"), VerticalAlignment = VerticalAlignment.Center };
            Border app1 = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(5), Background = K.B("line2"), Margin = new Thickness(8, 0, 0, 0) };
            Border app2 = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(5), Background = K.B("line2"), Margin = new Thickness(6, 0, 0, 0) };
            TextBlock tray = new TextBlock { Text = "    14:32", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 11, Foreground = K.B("text"), VerticalAlignment = VerticalAlignment.Center };
            TextBlock clock = K.T("14:32", 11, FontWeights.SemiBold, "text");
            tray.Text = "  ";
            Border trayBox = new Border { Background = K.B("line"), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4, 8, 4), Child = K.H(8, tray, clock), VerticalAlignment = VerticalAlignment.Center };
            Grid dock = new Grid();
            dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dock.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel sl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { start, app1, app2 } };
            Grid.SetColumn(sl, 0); Grid.SetColumn(imgWidget, 2); Grid.SetColumn(trayBox, 3);
            imgWidget.Margin = new Thickness(0, 0, 10, 0);
            dock.Children.Add(imgWidget); dock.Children.Add(trayBox);
            dockBorder = new Border { Background = K.B("dock"), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 8, 8, 8), Child = dock, MinHeight = 60 };




            // consumo do app
            lblCpu = K.T("0,0%", 11, FontWeights.Bold, "green");
            lblRam = K.T("0 MB", 11, FontWeights.Bold, "green");
            lblRamWin = K.T("0 MB", 11, FontWeights.Bold, "sub");
            perfCpuBox = new Border
            {
                Background = K.B("swatch"), BorderBrush = K.B("line2"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 12, 0, 0),
                Child = K.V(8,
                    K.LR(K.H(6, new Ico("status-ao-vivo", 12, "green"), K.T("Consumo do NetMonitor", 11, FontWeights.SemiBold, "text")), K.T("Tempo real", 10, "dim")),
                    K.H(8, new Ico("sistema-cpu", 14, "sub"), K.T("Widget:", 11, "sub"), lblCpu, K.T("CPU", 10, "dim"), K.T("·", 11, "dim"), lblRam, K.T("RAM", 10, "dim")),
                    K.H(8, new Ico("sistema-ram", 14, "sub"), K.T("Esta janela:", 11, "sub"), lblRamWin, K.T("RAM, liberada ao fechar", 10, "dim")))
            };

            StackPanel col = new StackPanel { Margin = new Thickness(28, 30, 28, 24) };
            col.Children.Add(hdr);
            col.Children.Add(flyoutWrap);
            col.Children.Add(dockBorder);
            col.Children.Add(perfCpuBox);
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = col };
            return new Border { Background = K.B("sidebar"), Child = sv };
        }

        // ---- renderização da prévia
        static BitmapSource ToSource(D.Bitmap bmp, double dpi)
        {
            D.Imaging.BitmapData bd = bmp.LockBits(new D.Rectangle(0, 0, bmp.Width, bmp.Height), D.Imaging.ImageLockMode.ReadOnly, D.Imaging.PixelFormat.Format32bppPArgb);
            try
            {
                BitmapSource src = BitmapSource.Create(bmp.Width, bmp.Height, dpi, dpi, PixelFormats.Pbgra32, null, bd.Scan0, bd.Stride * bmp.Height, bd.Stride);
                src.Freeze();
                return src;
            }
            finally { bmp.UnlockBits(bd); }
        }

        bool previewDirty = true;
        string previewKey = "";

        // Impressão digital do Top 5: a lista troca de referência a cada envio, então não serve comparar referências.
        static string TopKey(List<ProcessTrafficItem> l)
        {
            if (l == null) return "";
            StringBuilder sb = new StringBuilder();
            int n = Math.Min(5, l.Count);
            for (int i = 0; i < n; i++)
                sb.Append(l[i].Name).Append('|').Append(l[i].TxBytes).Append('|').Append(l[i].RxBytes).Append(';');
            return sb.ToString();
        }

        // Redesenha a prévia só quando os valores exibidos, a lista de apps ou as configurações mudaram
        void RefreshPreview()
        {
            if (imgWidget == null) return;
            try
            {
                double dd = Math.Max(1.0, dpi) * 96.0;
                bool dk = S.IsDark(SysDark());
                double ub, db; host.LiveBits(out ub, out db);
                string un, uu, dn, du;
                SpeedFmt.Format(S, ub, true, out un, out uu);
                SpeedFmt.Format(S, db, false, out dn, out du);
                string key = un + uu + dn + du + dd + TopKey(host.TopApps());
                if (!previewDirty && key == previewKey) return;
                previewDirty = false; previewKey = key;

                D.Bitmap wb = WidgetRenderer.RenderTo(widgetPreviewBuf, S, dk, un, uu, dn, du, (float)dd);
                if (!ReferenceEquals(wb, widgetPreviewBuf))
                {
                    if (widgetPreviewBuf != null) widgetPreviewBuf.Dispose();
                    widgetPreviewBuf = wb;
                }
                imgWidget.Source = ToSource(widgetPreviewBuf, dd);

                RenderFlyout();
                UpdateFlyoutOpen();
            }
            catch (Exception ex) { K.Log(ex); }
        }

        void RenderFlyout()
        {
            try
            {
                double dd = Math.Max(1.0, dpi) * 96.0;
                bool dk = S.IsDark(SysDark());
                double ub, db; host.LiveBits(out ub, out db);
                FlyoutHit hit;
                D.Bitmap fb = FlyoutRenderer.RenderTo(flyoutPreviewBuf, S, dk, host.TopApps(), ub, db, (float)dd, hoverRow, 0, out hit);
                if (!ReferenceEquals(fb, flyoutPreviewBuf))
                {
                    if (flyoutPreviewBuf != null) flyoutPreviewBuf.Dispose();
                    flyoutPreviewBuf = fb;
                }
                lastHit = hit;
                imgFlyout.Source = ToSource(flyoutPreviewBuf, dd);
                double wDip = flyoutPreviewBuf.Width * 96.0 / dd, hDip = flyoutPreviewBuf.Height * 96.0 / dd;
                flyoutScale = Math.Min(1.0, 368.0 / wDip);
                imgFlyout.Stretch = Stretch.Fill;
                imgFlyout.Width = wDip * flyoutScale; imgFlyout.Height = hDip * flyoutScale;
                flyoutHeight = hDip * flyoutScale;
                flyoutWrap.Height = flyoutHeight;
            }
            catch (Exception ex) { K.Log(ex); }
        }

        double flyoutHeight = 330;
        double flyoutScale = 1.0;
        bool flyoutOpen = false;

        void UpdateFlyoutOpen()
        {
            bool want = S.FlyoutPinned || widgetHover || flyoutHover;
            if (want == flyoutOpen) return;
            flyoutOpen = want;
            IEasingFunction ease = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6 };
            flyoutWrap.BeginAnimation(OpacityProperty, new DoubleAnimation(want ? 1 : 0.72, new Duration(TimeSpan.FromMilliseconds(240))) { EasingFunction = ease });
        }

        void FlyoutMouse(Point p, bool click)
        {
            if (lastHit == null) return;
            double dd = Math.Max(1.0, dpi);
            System.Drawing.Point q = new System.Drawing.Point((int)(p.X / flyoutScale * dd), (int)(p.Y / flyoutScale * dd));
            if (click)
            {
                if (lastHit.Separated.Contains(q)) { S.FlyoutMode = "separated"; Changed(); }
                else if (lastHit.Combined.Contains(q)) { S.FlyoutMode = "combined"; Changed(); }
                else if (lastHit.SettingsLink.Contains(q)) { curPage = "geral"; ApplyPage(); }
                return;
            }
            int h = -1;
            for (int i = 0; i < lastHit.Rows.Count; i++) if (lastHit.Rows[i].Contains(q)) { h = i; break; }
            if (h != hoverRow) { hoverRow = h; RenderFlyout(); }
        }

        void LiveTick()
        {
            try
            {
                if (S.IsDark(SysDark()) != K.Dark) { Changed(); return; }   // o Windows trocou o tema: repinta tudo agora

                if (railVisible) RefreshPreview();   // barra lateral oculta: não gasta desenho

                double cpu, ram; host.PerfInfo(out cpu, out ram);
                lblCpu.Text = cpu.ToString("0.0") + "%";
                lblRam.Text = ram.ToString("0.0") + " MB";

                if ((DateTime.UtcNow - ramWinAt).TotalSeconds > 2)
                {
                    ramWinAt = DateTime.UtcNow;
                    selfProc.Refresh();
                    lblRamWin.Text = (selfProc.WorkingSet64 / 1048576.0).ToString("0") + " MB";
                }

                if (curPage == "apps")
                {
                    string k = TopKey(host.TopApps());
                    if (k != appsKey) { appsKey = k; RebuildApps(); }
                }
            }
            catch (Exception ex) { K.Log(ex); }
        }
    }
}
