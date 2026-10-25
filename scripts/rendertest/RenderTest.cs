// Harness de validação visual: renderiza widget, flyout e as páginas da janela WPF em PNG (sem captura de tela).
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using D = System.Drawing;
using NetMonitor;
using NetMonitor.Ui;

class FakeHost : ISettingsHost
{
    public AppSettings S = new AppSettings();
    public AppSettings GetSettings() { return S.Clone(); }
    public AppSettings Last; public bool Persisted; public int Applies;
    public void Apply(AppSettings s, bool persist) { Last = s; Applies++; if (persist) Persisted = true; }
    public bool SystemDark() { return true; }
    public void LiveBits(out double up, out double down) { up = 11.7e6; down = 149.6e6; }
    public List<ProcessTrafficItem> TopApps()
    {
        return new List<ProcessTrafficItem> {
            new ProcessTrafficItem { Name = "chrome", Sub = "Navegador", TxBytes = 1200000, RxBytes = 13600000 },
            new ProcessTrafficItem { Name = "Spotify", Sub = "Música", TxBytes = 24000, RxBytes = 296000 },
            new ProcessTrafficItem { Name = "Discord", Sub = "Voz e Chat", TxBytes = 42000, RxBytes = 52000 },
            new ProcessTrafficItem { Name = "steam", Sub = "Jogos", TxBytes = 8000, RxBytes = 34000 },
            new ProcessTrafficItem { Name = "Code", Sub = "Editor", TxBytes = 2000, RxBytes = 10000 }
        };
    }
    public void SamplingDemand(bool on) { }
    public void SettingsClosed() { }
    public void PerfInfo(out double cpu, out double ram) { cpu = 0.0; ram = 11.8; }
    public void TopRefreshNow() { }
}

static class P
{
    // procura, na árvore visual, um elemento do tipo T que contenha um TextBlock com o texto exato
    static T FindWithText<T>(DependencyObject root, string text) where T : class
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            DependencyObject c = VisualTreeHelper.GetChild(root, i);
            T t = c as T;
            if (t != null && HasText(c, text)) return t;
            T r = FindWithText<T>(c, text);
            if (r != null) return r;
        }
        return null;
    }

    static bool HasText(DependencyObject root, string text)
    {
        System.Windows.Controls.TextBlock tb = root as System.Windows.Controls.TextBlock;
        if (tb != null && tb.Text == text) return true;
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++) if (HasText(VisualTreeHelper.GetChild(root, i), text)) return true;
        return false;
    }

    static string outDir;

    static void SaveBmp(D.Bitmap bmp, string name, D.Color bg)
    {
        using (D.Bitmap flat = new D.Bitmap(bmp.Width, bmp.Height))
        using (D.Graphics g = D.Graphics.FromImage(flat))
        {
            bmp.SetResolution(96f, 96f); g.Clear(bg); g.DrawImage(bmp, 0, 0);
            flat.Save(Path.Combine(outDir, name), D.Imaging.ImageFormat.Png);
        }
    }

    static void SaveVisual(FrameworkElement root, int w, int h, string name)
    {
        root.Measure(new Size(w, h));
        root.Arrange(new Rect(0, 0, w, h));
        root.UpdateLayout();
        RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(root);
        PngBitmapEncoder enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using (FileStream fs = File.Create(Path.Combine(outDir, name))) enc.Save(fs);
    }

    // Galeria da documentação: todas as telas em Monocromático e Lima & Ciano, claro e escuro,
    // mais a barra de tarefas composta (widget + flyout aberto) nos dois temas.
    static void Gallery()
    {
        string[][] combos = new string[][] { new string[] { "pure_mono", "mono" }, new string[] { "lima_ciano", "lima" } };
        foreach (string[] c in combos)
        foreach (bool dark in new bool[] { true, false })
        {
            string theme = dark ? "dark" : "light", tag = c[1] + "_" + theme;
            foreach (string page in new string[] { "cores", "layout", "fonte", "setas", "apps", "geral", "sobre" })
            {
                FakeHost h = new FakeHost();
                h.S.ThemeMode = theme; h.S.ColorPreset = c[0];
                SaveVisual((FrameworkElement)new SettingsWindow(h, page).Content, 1560, 980, tag + "_win_" + page + ".png");
            }
            FakeHost hf = new FakeHost(); hf.S.ColorPreset = c[0];
            FlyoutHit hit;
            using (D.Bitmap fl = FlyoutRenderer.Render(hf.S, dark, hf.TopApps(), 11.7e6, 156.9e6, 144f, 1, 0, out hit))
            using (D.Bitmap wd = WidgetRenderer.Render(hf.S, dark, "11,7", "Mbps", "156,9", "Mbps", 144f))
            {
                SaveBmp(fl, tag + "_flyout.png", dark ? D.Color.FromArgb(10, 13, 20) : D.Color.FromArgb(235, 240, 250));
                Taskbar(wd, fl, dark, tag + "_taskbar.png", false);
                Taskbar(wd, fl, dark, tag + "_taskbar_transparente.png", true);
            }
        }
        foreach (string lg in new string[] { "pt", "en" })
            SaveVisual((FrameworkElement)new NetMonitor.InstallerApp(null, lg).Content, 560, 520, "installer_" + lg + ".png");
        Console.WriteLine("ok: galeria gerada");
    }

    // Barra do Windows 11 a 150% (48 px -> 72 px): área de trabalho, flyout acima do widget, bandeja e relógio
    // semFundo: sem a área de trabalho (PNG com transparência, só barra + flyout)
    static void Taskbar(D.Bitmap widget, D.Bitmap flyout, bool dark, string name, bool semFundo)
    {
        const int W = 1400, H = 980, TB = 72;
        using (D.Bitmap img = new D.Bitmap(W, H))
        using (D.Graphics g = D.Graphics.FromImage(img))
        {
            g.SmoothingMode = D.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = D.Text.TextRenderingHint.AntiAliasGridFit;
            D.Color d1 = dark ? D.Color.FromArgb(18, 28, 48) : D.Color.FromArgb(196, 214, 238);
            D.Color d2 = dark ? D.Color.FromArgb(40, 22, 58) : D.Color.FromArgb(232, 222, 240);
            if (!semFundo)
                using (D.Drawing2D.LinearGradientBrush bg = new D.Drawing2D.LinearGradientBrush(new D.Rectangle(0, 0, W, H - TB), d1, d2, 35f))
                    g.FillRectangle(bg, 0, 0, W, H - TB);
            D.Color bar = dark ? D.Color.FromArgb(28, 28, 28) : D.Color.FromArgb(238, 238, 238);
            D.Color fg = dark ? D.Color.White : D.Color.FromArgb(26, 26, 26);
            using (D.SolidBrush b = new D.SolidBrush(bar)) g.FillRectangle(b, 0, H - TB, W, TB);
            using (D.Pen p = new D.Pen(dark ? D.Color.FromArgb(60, 60, 60) : D.Color.FromArgb(214, 214, 214))) g.DrawLine(p, 0, H - TB, W, H - TB);

            // relógio
            int x = W - 18;
            using (D.Font f = new D.Font("Segoe UI Variable Text", 13.5f, D.GraphicsUnit.Point))
            using (D.SolidBrush b = new D.SolidBrush(fg))
            using (D.StringFormat sf = new D.StringFormat { Alignment = D.StringAlignment.Far })
            {
                g.DrawString("17:55", f, b, x, H - TB + 10, sf);
                g.DrawString("25/10/2026", f, b, x, H - TB + 36, sf);
                x -= (int)g.MeasureString("25/10/2026", f).Width + 14;
            }
            // ícones da bandeja (Segoe Fluent Icons)
            D.Font probe = new D.Font("Segoe Fluent Icons", 12f, D.GraphicsUnit.Point);
            string icoFont = probe.Name == "Segoe Fluent Icons" ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
            probe.Dispose();
            using (D.Font fi = new D.Font(icoFont, 12f, D.GraphicsUnit.Point))
            using (D.SolidBrush b = new D.SolidBrush(fg))
            using (D.StringFormat sf = new D.StringFormat { Alignment = D.StringAlignment.Center, LineAlignment = D.StringAlignment.Center })
            {
                foreach (string glyph in new string[] { "", "", "", "", "" })
                {
                    g.DrawString(glyph, fi, b, x - 18, H - TB / 2, sf);
                    x -= 44;
                }
            }
            // widget à esquerda da bandeja, centralizado na barra
            widget.SetResolution(96f, 96f); flyout.SetResolution(96f, 96f);
            int wx = x - widget.Width + 12, wy = H - TB + (TB - widget.Height) / 2;
            g.DrawImage(widget, wx, wy);
            // flyout acima, alinhado à direita do widget como no app
            int fx = Math.Min(W - flyout.Width - 18, wx + widget.Width - flyout.Width + 60), fy = H - TB - flyout.Height - 12;
            g.DrawImage(flyout, fx, fy);
            img.Save(Path.Combine(outDir, name), D.Imaging.ImageFormat.Png);
        }
    }

    [STAThread]
    static void Main(string[] args)
    {
        outDir = args.Length > 0 ? args[0] : ".";
        Directory.CreateDirectory(outDir);
        if (args.Length > 1 && args[1] == "gallery") { Gallery(); return; }

        // widget: variações
        string[][] v = new string[][] {
            new string[] { "default", "", "" },
        };
        foreach (bool dark in new bool[] { true, false })
        foreach (float dpi in new float[] { 96f, 144f })
        {
            AppSettings s = new AppSettings();
            using (D.Bitmap b = WidgetRenderer.Render(s, dark, "11.7", "Mbps", "156.9", "Mbps", dpi))
                SaveBmp(b, "widget_two_" + (dark ? "dark" : "light") + "_" + (int)dpi + ".png", dark ? D.Color.FromArgb(32, 32, 32) : D.Color.FromArgb(235, 240, 250));
        }
        { AppSettings sr = new AppSettings(); using (D.Bitmap raw = WidgetRenderer.Render(sr, true, "11.7", "Kbps", "156.9", "Mbps", 144f)) raw.Save(Path.Combine(outDir, "widget_raw.png"), D.Imaging.ImageFormat.Png); }
        string[] presets = new string[] { "triangles", "thin", "outline", "chevrons", "text", "custom" };
        foreach (string p in presets)
        {
            AppSettings s = new AppSettings { ArrowPreset = p, BgMode = "solid", BorderEnabled = true, ArrowPosition = "left" };
            using (D.Bitmap b = WidgetRenderer.Render(s, true, "11.7", "Mbps", "156.9", "Mbps", 144f))
                SaveBmp(b, "widget_arrow_" + p + ".png", D.Color.FromArgb(32, 32, 32));
        }
        {
            AppSettings s = new AppSettings { Layout = "one-line", BgMode = "acrylic", BorderEnabled = true, MetricsOrder = "down-first", ArrowPosition = "right" };
            using (D.Bitmap b = WidgetRenderer.Render(s, true, "11.7", "Mbps", "156.9", "Mbps", 144f))
                SaveBmp(b, "widget_oneline.png", D.Color.FromArgb(32, 32, 32));
        }

        FakeHost h0 = new FakeHost();
        FlyoutHit hit;
        foreach (bool dark in new bool[] { true, false })
        {
            using (D.Bitmap b = FlyoutRenderer.Render(h0.S, dark, h0.TopApps(), 11.7e6, 156.9e6, 144f, 1, 0, out hit))
                SaveBmp(b, "flyout_" + (dark ? "dark" : "light") + ".png", dark ? D.Color.FromArgb(10, 13, 20) : D.Color.FromArgb(235, 240, 250));
        }

        // janela WPF: cada página, escuro e claro
        foreach (string theme in new string[] { "dark", "light" })
        {
            foreach (string page in new string[] { "cores", "layout", "fonte", "setas", "apps", "geral", "sobre" })
            {
                FakeHost h = new FakeHost();
                h.S.ThemeMode = theme;
                SettingsWindow w = new SettingsWindow(h, page);
                FrameworkElement root = (FrameworkElement)w.Content;
                SaveVisual(root, 1560, 980, "win_" + theme + "_" + page + ".png");
            }
        }
        foreach (string lg in new string[] { "pt", "en" })
        {
            NetMonitor.InstallerApp ia = new NetMonitor.InstallerApp(null, lg);
            SaveVisual((FrameworkElement)ia.Content, 560, 520, "installer_" + lg + ".png");
        }

        // ---- ligação dos controles: dispara os cliques e confere o que a janela mandou ao host
        FakeHost ht = new FakeHost();
        SettingsWindow tw = new SettingsWindow(ht, "layout");
        FrameworkElement troot = (FrameworkElement)tw.Content;
        troot.Measure(new Size(1560, 980)); troot.Arrange(new Rect(0, 0, 1560, 980)); troot.UpdateLayout();
        Func<string, Sel> findSel = delegate(string text) { return FindWithText<Sel>(troot, text); };
        Func<string, Btn> findBtn = delegate(string text) { return FindWithText<Btn>(troot, text); };
        int fails = 0;
        Action<string, bool> check = delegate(string what, bool ok) { Console.WriteLine((ok ? "  OK   " : "  FALHA ") + what); if (!ok) fails++; };

        findSel("Pill Sólido").Click(); check("Pill Sólido -> BgMode=solid", ht.Last != null && ht.Last.BgMode == "solid");
        findSel("Linha Única (Horizontal)").Click(); check("Linha Única -> Layout=one-line", ht.Last.Layout == "one-line");
        findSel("Megabytes por segundo").Click(); check("MB/s -> UnitBase=bytes", ht.Last.UnitBase == "bytes");
        findSel("Individual TX/RX").Click(); check("Individual -> ScaleMode=individual", ht.Last.ScaleMode == "individual");
        findSel("▼ Download Primeiro / ▲ Upload").Click(); check("Ordem -> down-first", ht.Last.MetricsOrder == "down-first");
        findBtn("Habilitada") ;
        int before = ht.Applies;
        findBtn("Cancelar").Click();
        check("Cancelar reverte (Apply com original, sem gravar)", ht.Applies > before && !ht.Persisted && ht.Last.BgMode == "transparent");
        FakeHost hs = new FakeHost();
        SettingsWindow sw2 = new SettingsWindow(hs, "layout");
        FrameworkElement r2 = (FrameworkElement)sw2.Content;
        r2.Measure(new Size(1336, 900)); r2.Arrange(new Rect(0, 0, 1336, 900)); r2.UpdateLayout();
        FindWithText<Sel>(r2, "Pill Translúcido").Click();
        FindWithText<Btn>(r2, "Salvar Alterações").Click();
        check("Salvar grava (persist=true) com BgMode=acrylic", hs.Persisted && hs.Last.BgMode == "acrylic");
        FindWithText<Btn>(r2, "Restaurar").Click();
        check("Restaurar volta ao padrão de fábrica", hs.Last.BgMode == "transparent" && hs.Last.UnitBase == "bits" && hs.Last.ArrowPosition == "left");
        Console.WriteLine(fails == 0 ? "ok: todos os testes passaram" : "FALHAS: " + fails);
    }
}
