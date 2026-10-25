// ==============================================================================
// NetMonitor - Processo da janela de Configurações (NetMonitor.exe --settings <hwnd do widget> <página>)
// Recebe do widget as velocidades, o consumo e o top 5 (prévia ao vivo) e devolve cada alteração.
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace NetMonitor.Ui
{
    public class RemoteHost : ISettingsHost
    {
        readonly IntPtr widget;
        public IntPtr Self;
        double up, down, cpu, ram;
        List<ProcessTrafficItem> top = new List<ProcessTrafficItem>();

        public RemoteHost(IntPtr widget) { this.widget = widget; }

        public AppSettings GetSettings()
        {
            string lang = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName.ToLowerInvariant() == "pt" ? "pt" : "en";
            return AppSettings.Load(lang);
        }

        public void Apply(AppSettings s, bool persist) { Ipc.Send(widget, persist ? Ipc.ApplySave : Ipc.ApplyLive, s.ToText(), Self); }

        public bool SystemDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : (k.GetValue("SystemUsesLightTheme") ?? k.GetValue("AppsUseLightTheme"));
                    if (v != null) return Convert.ToInt32(v) == 0;
                }
            }
            catch { }
            return true;
        }

        public void LiveBits(out double u, out double d) { u = up; d = down; }
        public List<ProcessTrafficItem> TopApps() { return top; }
        public void SamplingDemand(bool on) { Ipc.Send(widget, Ipc.Sampling, on ? "1" : "0", Self); }
        public void SettingsClosed() { Ipc.Send(widget, Ipc.Bye, "", Self); }
        public void PerfInfo(out double c, out double r) { c = cpu; r = ram; }
        public void TopRefreshNow() { Ipc.Send(widget, Ipc.Refresh, "", Self); }

        // Linhas "chave=valor" e "app=nome<TAB>categoria<TAB>tx<TAB>rx" (formato de NetMonitorApp.BuildDataText)
        public void OnData(string text)
        {
            List<ProcessTrafficItem> list = new List<ProcessTrafficItem>();
            foreach (string line in text.Split('\n'))
            {
                int i = line.IndexOf('=');
                if (i <= 0) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1);
                double d;
                if (k == "app")
                {
                    string[] f = v.Split('\t');
                    ulong tx, rx;
                    if (f.Length == 4 && ulong.TryParse(f[2], out tx) && ulong.TryParse(f[3], out rx))
                        list.Add(new ProcessTrafficItem { Name = f[0], Sub = f[1], TxBytes = tx, RxBytes = rx });
                }
                else if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                {
                    if (k == "up") up = d; else if (k == "dn") down = d; else if (k == "cpu") cpu = d; else if (k == "ram") ram = d;
                }
            }
            top = list;   // lista nova a cada envio: a prévia percebe a mudança por referência
        }
    }

    public static class SettingsProcess
    {
        public static void Run(string[] args)
        {
            long hw = 0;
            if (args.Length > 1) long.TryParse(args[1], out hw);
            string page = args.Length > 2 ? args[2] : "cores";
            IntPtr widget = new IntPtr(hw);

            Application app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (o, e) => { K.Log(e.Exception); e.Handled = true; };
            RemoteHost host = new RemoteHost(widget);
            SettingsWindow win = new SettingsWindow(host, page);
            win.SourceInitialized += delegate
            {
                IntPtr h = new WindowInteropHelper(win).Handle;
                host.Self = h;
                HwndSource.FromHwnd(h).AddHook(delegate(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
                {
                    if (msg != Ipc.WM_COPYDATA) return IntPtr.Zero;
                    int code; string text;
                    if (!Ipc.Read(lParam, out code, out text)) return IntPtr.Zero;
                    if (code == Ipc.Data) host.OnData(text);
                    else if (code == Ipc.ShowPage)
                    {
                        win.ShowPage(text);
                        if (win.WindowState == WindowState.Minimized) win.WindowState = WindowState.Normal;
                        win.Activate();
                    }
                    handled = true;
                    return new IntPtr(1);
                });
                Ipc.Send(widget, Ipc.Hello, h.ToInt64().ToString(CultureInfo.InvariantCulture), h);
            };
            // se o widget for encerrado, a janela fecha junto
            DispatcherTimer guard = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            guard.Tick += delegate { if (!Ipc.IsWindow(widget)) win.Close(); };
            guard.Start();
            app.Run(win);
        }
    }
}
