// ==============================================================================
// NetMonitor Setup - Instalador nativo (WPF), identidade do Manual de Marca
// Banner azul NetMonitor com o wordmark, corpo escuro Ace Obsidian, pasta, boot, atalhos e entrada em "Aplicativos":
// pasta \NetMonitor, elevação (UAC) para Program Files, Run (boot), atalhos e entrada em "Aplicativos".
// ==============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NetMonitor.Ui;

[assembly: AssemblyTitle("NetMonitor Setup")]
[assembly: AssemblyDescription("Instalador do NetMonitor para Windows 11")]
[assembly: AssemblyCompany("Eduardo Noé")]
[assembly: AssemblyProduct("NetMonitor")]
[assembly: AssemblyCopyright("Copyright © 2026 Eduardo Noé")]

namespace NetMonitor
{
    public class InstallerApp : Window
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        bool pt;
        bool busy, done;
        string initialDest;
        Input dest;
        NetMonitor.Ui.Switch swBoot, swMenu, swDesk;
        Seg lang;
        Btn btnBrowse, btnInstall, btnCancel;
        TextBlock lblTitle, lblDesc, lblDest, lblBoot, lblMenu, lblDesk, lblStatus;
        Border track, fill;
        readonly System.Collections.Generic.List<Action> texts = new System.Collections.Generic.List<Action>();

        public InstallerApp(string initialDest, string initialLang)
        {
            this.initialDest = initialDest;
            pt = !string.IsNullOrEmpty(initialLang)
                ? initialLang.Equals("pt", StringComparison.OrdinalIgnoreCase)
                : System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase);

            K.SetTheme(SysDark());
            Width = 600; SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.CanMinimize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            FontFamily = K.Sans;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            try { Icon = Load("icone_128.png"); } catch { }
            Content = Build();
            ApplyText();
            SourceInitialized += delegate { Chrome(); };
            System.Windows.Threading.DispatcherTimer themeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            themeTimer.Tick += delegate { bool d = SysDark(); if (d != K.Dark) { K.SetTheme(d); Chrome(); InvalidateVisual(); } };
            themeTimer.Start();
        }

        static BitmapImage Load(string name)
        {
            BitmapImage bi = new BitmapImage();
            using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = st; bi.EndInit();
            }
            bi.Freeze();
            return bi;
        }

        // Tema do Windows (Aplicativos): o instalador acompanha, inclusive se mudar com ele aberto
        static bool SysDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    if (v != null) return Convert.ToInt32(v) == 0;
                }
            }
            catch { }
            return true;
        }

        void Chrome()
        {
            try
            {
                IntPtr h = new WindowInteropHelper(this).Handle;
                int dark = K.Dark ? 1 : 0; DwmSetWindowAttribute(h, 20, ref dark, 4);
                Color c = K.B("canvas").Color; int cref = c.R | (c.G << 8) | (c.B << 16);
                DwmSetWindowAttribute(h, 35, ref cref, 4);
                Color t = K.B("text").Color; int tref = t.R | (t.G << 8) | (t.B << 16);
                DwmSetWindowAttribute(h, 36, ref tref, 4);
            }
            catch { }
        }

        TextBlock T(double size, FontWeight w, string brush)
        {
            return K.T("", size, w, brush);
        }

        UIElement Build()
        {
            // banner azul com o wordmark
            Image word = new Image { Source = Load("wordmark_branca.png"), Width = 210, HorizontalAlignment = HorizontalAlignment.Left };
            RenderOptions.SetBitmapScalingMode(word, BitmapScalingMode.HighQuality);
            lblDesc = K.T("", 12, FontWeights.Normal, "white"); lblDesc.Opacity = 0.9;
            lang = new Seg(10, 11, false, "PT", "EN");
            lang.Select(pt ? 0 : 1, false);
            lang.Changed = delegate(int i) { pt = i == 0; ApplyText(); };
            lang.HorizontalAlignment = HorizontalAlignment.Right; lang.VerticalAlignment = VerticalAlignment.Top;
            Grid banner = new Grid { Background = K.B("brand"), Height = 116 };
            StackPanel bt = K.V(10, word, lblDesc);
            bt.Margin = new Thickness(28, 30, 28, 0);
            banner.Children.Add(bt);
            lang.Margin = new Thickness(0, 16, 16, 0);
            banner.Children.Add(lang);

            // corpo
            lblTitle = T(18, FontWeights.Bold, "text");
            lblDest = T(11, FontWeights.SemiBold, "sub");
            string def = !string.IsNullOrEmpty(initialDest) ? initialDest : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\NetMonitor");
            dest = new Input(10, def, true, 12); dest.Width = double.NaN; dest.HorizontalAlignment = HorizontalAlignment.Stretch;
            btnBrowse = new Btn("", false, 12, new Thickness(14, 7, 14, 7), null);
            btnBrowse.Click = delegate { Browse(); };
            Grid destRow = new Grid();
            destRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            destRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(dest, 0); Grid.SetColumn(btnBrowse, 1); btnBrowse.Margin = new Thickness(8, 0, 0, 0);
            destRow.Children.Add(dest); destRow.Children.Add(btnBrowse);

            swBoot = new NetMonitor.Ui.Switch { Value = true }; swMenu = new NetMonitor.Ui.Switch { Value = true }; swDesk = new NetMonitor.Ui.Switch { Value = false };
            lblBoot = T(12, FontWeights.SemiBold, "text"); lblMenu = T(12, FontWeights.SemiBold, "text"); lblDesk = T(12, FontWeights.SemiBold, "text");
            Border opts = new Border
            {
                Background = K.B("swatch"), CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 6, 16, 6),
                Child = K.V(0, Row(lblBoot, swBoot), Row(lblMenu, swMenu), Row(lblDesk, swDesk))
            };

            track = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = K.B("btnbd"), Visibility = Visibility.Hidden };
            fill = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = K.B("brand"), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            Grid tg = new Grid(); tg.Children.Add(track); tg.Children.Add(fill);
            lblStatus = T(11, FontWeights.Normal, "sub");

            btnCancel = new Btn("", false, 12, new Thickness(18, 8, 18, 8), null);
            btnCancel.Click = delegate { if (!busy) Close(); };
            btnInstall = new Btn("", true, 12, new Thickness(26, 8, 26, 8), null);
            btnInstall.Click = delegate { if (done) Close(); else Install(); };
            StackPanel buttons = K.H(10, btnCancel, btnInstall);
            buttons.HorizontalAlignment = HorizontalAlignment.Right;

            StackPanel body = K.V(14, lblTitle, K.V(6, lblDest, destRow), opts, K.V(8, tg, lblStatus), buttons);
            body.Margin = new Thickness(28, 24, 28, 24);

            return new Border { Background = K.B("canvas"), Child = K.V(0, banner, body) };
        }

        UIElement Row(TextBlock label, NetMonitor.Ui.Switch sw)
        {
            Grid g = K.LR(label, sw);
            g.Margin = new Thickness(0, 9, 0, 9);
            return g;
        }

        void ApplyText()
        {
            Title = (pt ? "Instalar o NetMonitor v" : "NetMonitor Setup v") + AppVersion.Text;
            lblDesc.Text = pt ? "Medidor de banda para a barra de tarefas do Windows 11." : "Bandwidth meter for the Windows 11 taskbar.";
            lblTitle.Text = pt ? "Instalar o NetMonitor" : "Install NetMonitor";
            lblDest.Text = pt ? "Pasta de destino" : "Destination folder";
            btnBrowse.Text = pt ? "Procurar..." : "Browse...";
            lblBoot.Text = pt ? "Iniciar com o Windows" : "Start with Windows";
            lblMenu.Text = pt ? "Atalho no Menu Iniciar" : "Start Menu shortcut";
            lblDesk.Text = pt ? "Atalho na Área de Trabalho" : "Desktop shortcut";
            btnCancel.Text = pt ? "Cancelar" : "Cancel";
            btnInstall.Text = done ? (pt ? "Concluir" : "Finish") : (pt ? "Instalar" : "Install");
            if (!busy && !done) lblStatus.Text = pt ? "Pronto para instalar." : "Ready to install.";
        }

        void Browse()
        {
            try
            {
                using (System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog())
                {
                    fbd.SelectedPath = dest.Box.Text;
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string sel = fbd.SelectedPath.Trim().TrimEnd('\\');
                        if (!sel.EndsWith("NetMonitor", StringComparison.OrdinalIgnoreCase)) sel = Path.Combine(sel, "NetMonitor");
                        dest.Box.Text = sel;
                    }
                }
            }
            catch { }
        }

        static bool IsAdministrator()
        {
            using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        void Progress(int pct, string text)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                track.Visibility = Visibility.Visible;
                fill.Width = track.ActualWidth * pct / 100.0;
                if (text != null) lblStatus.Text = text;
            }));
        }

        void Install()
        {
            string installDir = dest.Box.Text.Trim().TrimEnd('\\');
            if (string.IsNullOrEmpty(installDir))
                installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\NetMonitor");
            if (!installDir.EndsWith("NetMonitor", StringComparison.OrdinalIgnoreCase))
            {
                installDir = Path.Combine(installDir, "NetMonitor");
                dest.Box.Text = installDir;
            }

            bool needsAdmin = installDir.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase) ||
                              installDir.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), StringComparison.OrdinalIgnoreCase) ||
                              installDir.IndexOf("Program Files", StringComparison.OrdinalIgnoreCase) >= 0;
            if (needsAdmin && !IsAdministrator())
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Assembly.GetExecutingAssembly().Location,
                        Arguments = string.Format("\"{0}\" {1}", installDir, pt ? "pt" : "en"),
                        Verb = "runas", UseShellExecute = true
                    });
                    Close();
                }
                catch
                {
                    lblStatus.Text = pt ? "Essa pasta exige permissão de administrador." : "This folder requires administrator permission.";
                }
                return;
            }

            busy = true;
            btnInstall.IsEnabled = false; btnCancel.IsEnabled = false; btnBrowse.IsEnabled = false; dest.IsEnabled = false; lang.IsEnabled = false;
            bool boot = swBoot.Value, menu = swMenu.Value, desk = swDesk.Value, isPt = pt;
            Thread t = new Thread(delegate() { DoInstall(installDir, boot, menu, desk, isPt); }) { IsBackground = true };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        void Fail(string msg)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                busy = false; btnInstall.IsEnabled = true; btnCancel.IsEnabled = true; btnBrowse.IsEnabled = true; dest.IsEnabled = true; lang.IsEnabled = true;
                lblStatus.Text = (pt ? "Não foi possível instalar: " : "Could not install: ") + msg;
                lblStatus.Foreground = K.Fixed("#F87171");
            }));
        }

        void DoInstall(string installDir, bool boot, bool menu, bool desk, bool isPt)
        {
            try
            {
                Progress(10, isPt ? "Fechando instâncias anteriores..." : "Closing previous instances...");
                foreach (Process p in Process.GetProcessesByName("NetMonitor"))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }

                if (!Directory.Exists(installDir)) Directory.CreateDirectory(installDir);
                Progress(35, isPt ? "Copiando os arquivos..." : "Copying files...");
                string exePath = Path.Combine(installDir, "NetMonitor.exe");
                string uninstallerPath = Path.Combine(installDir, "uninstall.exe");
                Extract("NetMonitor.exe", exePath);
                Progress(60, null);
                Extract("uninstall.exe", uninstallerPath);
                // restos da versão anterior (interface HTML) não são mais usados
                try { string old = Path.Combine(installDir, "settings.html"); if (File.Exists(old)) File.Delete(old); } catch { }

                Progress(75, isPt ? "Registrando no Windows..." : "Registering with Windows...");
                using (RegistryKey app = Registry.CurrentUser.CreateSubKey(@"Software\NetMonitor"))
                {
                    if (app != null)
                    {
                        app.SetValue("Language", isPt ? "pt" : "en");
                        app.SetValue("IsLocked", 1, RegistryValueKind.DWord);
                        try { app.DeleteValue("CustomX", false); } catch { }
                        try { app.DeleteValue("CustomY", false); app.DeleteValue("CustomPos", false); } catch { }
                    }
                }
                if (boot)
                {
                    using (RegistryKey run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                        if (run != null) run.SetValue("NetMonitor", "\"" + exePath + "\"");
                }
                string desc = isPt ? "NetMonitor - Monitor de Rede" : "NetMonitor - Network Monitor";
                if (menu) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "NetMonitor.lnk"), exePath, installDir, desc);
                if (desk) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NetMonitor.lnk"), exePath, installDir, desc);

                using (RegistryKey u = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\NetMonitor"))
                {
                    if (u != null)
                    {
                        u.SetValue("DisplayName", "NetMonitor");
                        u.SetValue("DisplayVersion", AppVersion.Text);
                        u.SetValue("Publisher", "Eduardo Noé");
                        u.SetValue("InstallLocation", installDir);
                        u.SetValue("DisplayIcon", exePath + ",0");
                        u.SetValue("UninstallString", "\"" + uninstallerPath + "\"");
                        u.SetValue("QuietUninstallString", "\"" + uninstallerPath + "\" /quiet");
                        u.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        u.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }

                Progress(92, isPt ? "Iniciando o NetMonitor..." : "Starting NetMonitor...");
                try { Process.Start(new ProcessStartInfo { FileName = exePath, WorkingDirectory = installDir }); } catch { }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    fill.Width = track.ActualWidth;
                    busy = false; done = true;
                    lblStatus.Text = pt ? "Pronto. O NetMonitor já está na sua barra de tarefas." : "Done. NetMonitor is now on your taskbar.";
                    btnInstall.IsEnabled = true; btnCancel.Visibility = Visibility.Collapsed;
                    ApplyText();
                }));
            }
            catch (Exception ex) { Fail(ex.Message); }
        }

        static void Extract(string resource, string output)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string match = null;
            foreach (string n in asm.GetManifestResourceNames())
                if (n.EndsWith(resource, StringComparison.OrdinalIgnoreCase)) { match = n; break; }
            if (match == null) throw new FileNotFoundException("Recurso não encontrado: " + resource);
            using (Stream s = asm.GetManifestResourceStream(match))
            using (FileStream fs = new FileStream(output, FileMode.Create, FileAccess.Write))
                s.CopyTo(fs);
        }

        static void Shortcut(string path, string target, string workDir, string description)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return;
                dynamic shell = Activator.CreateInstance(t);
                dynamic sc = shell.CreateShortcut(path);
                sc.TargetPath = target; sc.WorkingDirectory = workDir; sc.Description = description;
                sc.Save();
            }
            catch { }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            string initialDest = args.Length > 0 ? args[0] : null;
            string initialLang = args.Length > 1 ? args[1] : null;
            Application app = new Application();
            app.Run(new InstallerApp(initialDest, initialLang));
        }
    }
}

