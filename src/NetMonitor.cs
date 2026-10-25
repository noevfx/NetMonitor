// ==============================================================================
// NetMonitor - Monitor de Rede Ultraleve para Barra de Tarefas (Windows 11)
// Autor: Eduardo Noé (eduardonoe)
//
// Arquitetura:
// - Widget e flyout: janelas "layered" (UpdateLayeredWindow) desenhadas em GDI+ por WidgetRenderer/FlyoutRenderer.
//   Fundo com alpha 1 mantém a área inteira clicável (menu de contexto e arraste em qualquer ponto do widget).
// - Configurações: janela WPF nativa (SettingsWindow) numa thread própria, criada só quando aberta.
// - Modelo único de configurações: AppSettings (registro HKCU\Software\NetMonitor).
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

[assembly: AssemblyTitle("NetMonitor")]
[assembly: AssemblyDescription("Monitor de Rede Ultraleve para Barra de Tarefas do Windows 11")]
[assembly: AssemblyCompany("Eduardo Noé")]
[assembly: AssemblyProduct("NetMonitor")]
[assembly: AssemblyCopyright("Copyright © 2026 Eduardo Noé")]
// AssemblyVersion/AssemblyFileVersion vêm de src/Version.g.cs (gerado a partir de version.txt)

namespace NetMonitor
{
    public class NetMonitorApp : Form
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int awareness);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetProcessIoCounters(IntPtr ProcessHandle, out IO_COUNTERS IoCounters);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder text, ref int size);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetExtendedUdpTable(IntPtr pUdpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, uint reserved);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint state;
            public uint localAddr;
            public byte localPort1, localPort2, localPort3, localPort4;
            public uint remoteAddr;
            public byte remotePort1, remotePort2, remotePort3, remotePort4;
            public int owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCP6ROW_OWNER_PID
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucLocalAddr;
            public uint dwLocalScopeId;
            public byte localPort1, localPort2, localPort3, localPort4;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucRemoteAddr;
            public uint dwRemoteScopeId;
            public byte remotePort1, remotePort2, remotePort3, remotePort4;
            public uint state;
            public int owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_UDPROW_OWNER_PID
        {
            public uint localAddr;
            public byte localPort1, localPort2, localPort3, localPort4;
            public int owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_UDP6ROW_OWNER_PID
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucLocalAddr;
            public uint dwLocalScopeId;
            public byte localPort1, localPort2, localPort3, localPort4;
            public int owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int WM_DISPLAYCHANGE = 0x007E;
        private const int GWLP_HWNDPARENT = -8;

        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_POPUP = unchecked((int)0x80000000);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_HIDEWINDOW = 0x0080;

        public const uint SPI_GETSCREENSAVERRUNNING = 0x0072;
        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private const int AF_INET = 2;
        private const int AF_INET6 = 23;
        private const int TCP_TABLE_OWNER_PID_ALL = 5;
        private const int UDP_TABLE_OWNER_PID = 1;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private const string REG_KEY_APP = @"Software\NetMonitor";
        private const string REG_KEY_RUN = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "NetMonitor";

        private const int WS_EX_LAYERED = 0x00080000;
        private const int WM_DPICHANGED = 0x02E0;
        private const string OPEN_SETTINGS_EVENT = "NetMonitor_OpenSettings_EduardoNoe";

        // ---- Estado
        private AppSettings S = new AppSettings();
        private readonly object sLock = new object();
        private volatile bool settingsOpen = false;
        private volatile bool settingsSampling = false;
        private IntPtr settingsHwnd = IntPtr.Zero;   // janela de Configurações (outro processo)
        private Process settingsProc;
        private bool lastSystemDark = true;

        // compatibilidade com o código de placa de rede
        private string selectedNicId { get { return S.Adapter; } set { S.Adapter = value; } }
        private string currentLanguage { get { return S.Language; } }
        private string activeNicDisplayName = "Auto";

        // Medição
        private Timer updateTimer;
        private NetworkInterface cachedNic = null;
        private long lastBytesIn = -1;
        private long lastBytesOut = -1;
        private DateTime lastMeasurementTime = DateTime.MinValue;
        private double liveUpBits = 0, liveDownBits = 0;
        private double physRxBytes = 0, physTxBytes = 0;
        private string lastRenderKey = "";
        private int curW = 106, curH = 38;

        // Componentes UI
        private ContextMenuStrip contextMenu;
        private NotifyIcon trayIcon;

        // Flyout (Top 5 apps)
        private FlyoutForm flyout;
        private Timer hoverCheckTimer;
        private Timer processSampleTimer;
        private volatile bool isHoverSampling = false;
        private static volatile bool filterLoopback = true;
        private struct IoSnap { public ulong R, W; }
        private Dictionary<int, IoSnap> lastProcessIo = new Dictionary<int, IoSnap>();
        private Dictionary<int, string> cachedProcessNames = new Dictionary<int, string>();
        private Dictionary<string, string> subCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private DateTime lastSampleAt = DateTime.MinValue;
        private readonly object topLock = new object();
        private List<ProcessTrafficItem> latestTop = new List<ProcessTrafficItem>();

        // Estado de tela
        private bool isSessionLocked = false;
        private bool isScreensaverRunning = false;

        // Consumo do próprio app (painel de configurações)
        private TimeSpan lastCpu = TimeSpan.Zero;
        private DateTime lastCpuAt = DateTime.MinValue;

        public NetMonitorApp()
        {
            S = AppSettings.Load(GetSystemDefaultLanguage());
            filterLoopback = S.LoopbackFilter;
            lastSystemDark = IsWindowsDarkTheme();
            InitializeComponent();
            IntPtr h = this.Handle;            // garante o handle antes do primeiro desenho
            MeasureWidget();
            PositionWidget();
            BuildContextMenu();
            SetupTimer();
            BindToTaskbarOwner();

            this.MouseEnter += OnWidgetMouseEnter;
            this.MouseLeave += OnWidgetMouseLeave;

            try
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                SystemEvents.SessionSwitch += OnSessionSwitch;
            }
            catch { }

            StartOpenSettingsListener();
            // Depois da inicialização, devolve ao Windows a memória temporária (working set) do .NET/GDI+.
            Timer trim = new Timer { Interval = 6000 };
            trim.Tick += delegate { trim.Stop(); trim.Dispose(); Mem.Trim(); };
            trim.Start();
            if (S.FlyoutPinned)
            {
                Timer t = new Timer { Interval = 800 };
                t.Tick += delegate { t.Stop(); t.Dispose(); if (S.FlyoutPinned) ShowFlyout(); };
                t.Start();
            }
        }

        // ------------------------------------------------------------------ tema
        private bool EffectiveDark { get { return S.IsDark(lastSystemDark); } }

        private float GetDpi()
        {
            try { using (Graphics g = this.CreateGraphics()) return g.DpiX; }
            catch { return 96f; }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            try
            {
                if (e.Reason == SessionSwitchReason.SessionLock)
                {
                    isSessionLocked = true;
                    if (S.HideOnScreensaver) { this.Visible = false; HideFlyout(true); }
                }
                else if (e.Reason == SessionSwitchReason.SessionUnlock)
                {
                    isSessionLocked = false;
                    if (S.HideOnScreensaver && !isScreensaverRunning)
                    {
                        this.Visible = true;
                        PositionWidget();
                    }
                }
            }
            catch { }
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            try
            {
                if (this.IsHandleCreated) this.BeginInvoke((MethodInvoker)delegate { ApplySystemTheme(); });
            }
            catch { }
        }

        private Timer themeRepaint;
        private int themeRepaintStep;

        private void ScheduleThemeRepaint()
        {
            if (themeRepaint == null)
            {
                themeRepaint = new Timer { Interval = 600 };
                themeRepaint.Tick += delegate
                {
                    themeRepaintStep++;
                    BindToTaskbarOwner();
                    ApplySystemTheme();
                    if (themeRepaintStep >= 2) themeRepaint.Stop(); else themeRepaint.Interval = 1200;
                };
            }
            themeRepaintStep = 0;
            themeRepaint.Interval = 600;
            themeRepaint.Stop(); themeRepaint.Start();
        }

        public void ApplySystemTheme()
        {
            try
            {
                lastSystemDark = IsWindowsDarkTheme();
                RenderWidget();
                if (flyout != null && flyout.Visible) UpdateFlyout();
            }
            catch { }
        }

        // ------------------------------------------------------------------ janela
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style = WS_POPUP;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (this.Visible) RenderWidget();
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(curW, curH);
            this.Text = "NetMonitor";

            this.trayIcon = new NotifyIcon();
            this.trayIcon.Text = "NetMonitor";
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                this.trayIcon.Icon = this.Icon;
            }
            catch { this.trayIcon.Icon = SystemIcons.Application; }
            this.trayIcon.Visible = true;
            this.trayIcon.DoubleClick += (s, e) => OpenSettingsDialog("cores");
            this.DoubleClick += (s, e) => OpenSettingsDialog("cores");

            this.MouseDown += NetMonitorApp_MouseDown;
            this.MouseUp += NetMonitorApp_MouseUp;
        }

        // ------------------------------------------------------------------ desenho
        private Bitmap MakeBitmap()
        {
            string un, uu, dn, du;
            SpeedFmt.Format(S, liveUpBits, true, out un, out uu);
            SpeedFmt.Format(S, liveDownBits, false, out dn, out du);
            return WidgetRenderer.Render(S, EffectiveDark, un, uu, dn, du, GetDpi());
        }

        private void MeasureWidget()
        {
            using (Bitmap b = MakeBitmap()) { curW = b.Width; curH = b.Height; }
        }

        // Redesenha o widget. Se o tamanho mudou, mantém a borda direita e o centro vertical no lugar.
        public void RenderWidget()
        {
            if (!this.IsHandleCreated) return;
            try
            {
                using (Bitmap bmp = MakeBitmap())
                {
                    int x = this.Left, y = this.Top;
                    if (bmp.Width != curW || bmp.Height != curH)
                    {
                        int oldW = curW, oldH = curH;
                        curW = bmp.Width; curH = bmp.Height;
                        if (!customPos) { Point a = AutoAnchor(); x = a.X; y = a.Y; }
                        else { x += oldW - curW; y += (oldH - curH) / 2; }
                    }
                    LayeredWindow.Present(this.Handle, bmp, x, y, 255);
                    this.Location = new Point(x, y);
                }
                lastRenderKey = RenderKey();
            }
            catch { }
        }

        private string RenderKey()
        {
            string un, uu, dn, du;
            SpeedFmt.Format(S, liveUpBits, true, out un, out uu);
            SpeedFmt.Format(S, liveDownBits, false, out dn, out du);
            return un + uu + dn + du;
        }

        // ------------------------------------------------------------------ utilidades
        public static string ColorToHex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        public static Color HexToColor(string hex, Color defaultColor)
        {
            if (string.IsNullOrEmpty(hex)) return defaultColor;
            try
            {
                return ColorTranslator.FromHtml(hex);
            }
            catch
            {
                return defaultColor;
            }
        }

        public static bool IsWindowsDarkTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object sysLight = key.GetValue("SystemUsesLightTheme");
                        if (sysLight != null) return Convert.ToInt32(sysLight) == 0;
                        object appLight = key.GetValue("AppsUseLightTheme");
                        if (appLight != null) return Convert.ToInt32(appLight) == 0;
                    }
                }
            }
            catch { }
            return true;
        }

        public static string GetSystemDefaultLanguage()
        {
            try
            {
                string twoLetter = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                return (twoLetter == "pt") ? "pt" : "en";
            }
            catch
            {
                return "pt";
            }
        }

        private static bool IsScreensaverName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.EndsWith(".scr", StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("screensaver", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   string.Equals(name, "Bubbles", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Mystify", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Ribbons", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "PhotoScreensaver", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "ssText3d", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "scrnsave", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAnyScreensaverActive()
        {
            try
            {
                // 1. Verificação oficial SPI (quando o Windows ativa automaticamente por timeout de ociosidade)
                bool ss = false;
                if (SystemParametersInfo(SPI_GETSCREENSAVERRUNNING, 0, ref ss, 0) && ss)
                    return true;

                // 2. Janela clássica de screensaver de tela cheia
                if (FindWindow("WindowsScreenSaverClass", null) != IntPtr.Zero)
                    return true;

                // 3. Verificação da Janela em Primeiro Plano (cobre imediatamente o botão "Visualizar" de desk.cpl)
                IntPtr fg = GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    uint fgPid = 0;
                    GetWindowThreadProcessId(fg, out fgPid);
                    if (fgPid > 0)
                    {
                        try
                        {
                            using (Process p = Process.GetProcessById((int)fgPid))
                            {
                                if (IsScreensaverName(p.ProcessName))
                                {
                                    RECT rc;
                                    if (GetWindowRect(fg, out rc))
                                    {
                                        int w = rc.Right - rc.Left;
                                        int h = rc.Bottom - rc.Top;
                                        // Mini-preview tem ~150x110px. Tela cheia tem >= 640x480px.
                                        if (w >= 400 && h >= 300)
                                            return true;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                // 4. Varredura via Toolhelp32 de processos .scr ativos em tela cheia (sem pai e com tamanho real)
                IntPtr hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
                if (hSnap != IntPtr.Zero && hSnap != (IntPtr)(-1))
                {
                    try
                    {
                        PROCESSENTRY32 pe = new PROCESSENTRY32();
                        pe.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                        if (Process32First(hSnap, ref pe))
                        {
                            do
                            {
                                if (IsScreensaverName(pe.szExeFile))
                                {
                                    try
                                    {
                                        using (Process p = Process.GetProcessById((int)pe.th32ProcessID))
                                        {
                                            IntPtr hMain = p.MainWindowHandle;
                                            if (hMain != IntPtr.Zero)
                                            {
                                                RECT rc;
                                                if (GetWindowRect(hMain, out rc))
                                                {
                                                    int w = rc.Right - rc.Left;
                                                    int h = rc.Bottom - rc.Top;
                                                    // Em mini-preview, GetParent != IntPtr.Zero e tamanho < 200px.
                                                    // Em tela cheia ("Visualizar" ou timer), GetParent == IntPtr.Zero e cobre a tela.
                                                    if (w >= 400 && h >= 300 && GetParent(hMain) == IntPtr.Zero)
                                                    {
                                                        return true;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            } while (Process32Next(hSnap, ref pe));
                        }
                    }
                    finally
                    {
                        CloseHandle(hSnap);
                    }
                }
            }
            catch { }

            return false;
        }

        private NetworkInterface GetTargetNic()
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

            if (!string.Equals(selectedNicId, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var nic in nics)
                {
                    if (nic.Id == selectedNicId || nic.Name == selectedNicId)
                    {
                        activeNicDisplayName = nic.Name;
                        return nic;
                    }
                }
            }

            NetworkInterface bestNic = null;
            long maxBytes = -1;

            foreach (var nic in nics)
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                var ipProps = nic.GetIPProperties();
                bool hasGateway = ipProps.GatewayAddresses != null && ipProps.GatewayAddresses.Count > 0;

                try
                {
                    var stats = nic.GetIPv4Statistics();
                    long totalTraffic = stats.BytesReceived + stats.BytesSent;

                    if (hasGateway)
                    {
                        activeNicDisplayName = nic.Name;
                        return nic;
                    }

                    if (totalTraffic > maxBytes)
                    {
                        maxBytes = totalTraffic;
                        bestNic = nic;
                    }
                }
                catch { }
            }

            if (bestNic != null)
                activeNicDisplayName = bestNic.Name;
            else
                activeNicDisplayName = currentLanguage == "pt" ? "Nenhum" : "None";

            return bestNic;
        }

        private static HashSet<int> GetActiveNetworkPids()
        {
            HashSet<int> pids = new HashSet<int>();

            // 1. TCP IPv4 - Filtro rigoroso: apenas conexões ESTABLISHED (state == 5) para IPs externos reais
            int size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            if (size > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedTcpTable(buf, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf);
                        IntPtr rowPtr = (IntPtr)((long)buf + 4);
                        int rowSize = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));
                        for (int i = 0; i < count; i++)
                        {
                            MIB_TCPROW_OWNER_PID row = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_TCPROW_OWNER_PID));
                            // state == 5 (MIB_TCP_STATE_ESTAB)
                            if (row.owningPid > 4 && row.state == 5)
                            {
                                uint rem = row.remoteAddr;
                                uint loc = row.localAddr;
                                byte remFirst = (byte)(rem & 0xFF);
                                byte locFirst = (byte)(loc & 0xFF);

                                // Ignora 0.0.0.0 e loopback 127.x.x.x
                                if (rem != 0 && (!filterLoopback || (remFirst != 127 && locFirst != 127)))
                                {
                                    pids.Add(row.owningPid);
                                }
                            }
                            rowPtr = (IntPtr)((long)rowPtr + rowSize);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }

            // 2. TCP IPv6 - Filtro rigoroso: apenas conexões ESTABLISHED (state == 5) para IPs externos reais
            size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET6, TCP_TABLE_OWNER_PID_ALL, 0);
            if (size > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedTcpTable(buf, ref size, false, AF_INET6, TCP_TABLE_OWNER_PID_ALL, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf);
                        IntPtr rowPtr = (IntPtr)((long)buf + 4);
                        int rowSize = Marshal.SizeOf(typeof(MIB_TCP6ROW_OWNER_PID));
                        for (int i = 0; i < count; i++)
                        {
                            MIB_TCP6ROW_OWNER_PID row = (MIB_TCP6ROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_TCP6ROW_OWNER_PID));
                            if (row.owningPid > 4 && row.state == 5 && row.ucRemoteAddr != null && row.ucRemoteAddr.Length == 16)
                            {
                                bool isZero = true;
                                for (int b = 0; b < 16; b++) { if (row.ucRemoteAddr[b] != 0) { isZero = false; break; } }

                                if (!isZero)
                                {
                                    // Verifica se é loopback ::1
                                    bool isLoopback6 = true;
                                    for (int b = 0; b < 15; b++) { if (row.ucRemoteAddr[b] != 0) { isLoopback6 = false; break; } }
                                    if (!filterLoopback || !(isLoopback6 && row.ucRemoteAddr[15] == 1))
                                    {
                                        // Verifica link-local fe80::
                                        if (!(row.ucRemoteAddr[0] == 0xFE && (row.ucRemoteAddr[1] & 0xC0) == 0x80))
                                        {
                                            pids.Add(row.owningPid);
                                        }
                                    }
                                }
                            }
                            rowPtr = (IntPtr)((long)rowPtr + rowSize);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }

            // 3. UDP IPv4 - Filtra loopback
            size = 0;
            GetExtendedUdpTable(IntPtr.Zero, ref size, false, AF_INET, UDP_TABLE_OWNER_PID, 0);
            if (size > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedUdpTable(buf, ref size, false, AF_INET, UDP_TABLE_OWNER_PID, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf);
                        IntPtr rowPtr = (IntPtr)((long)buf + 4);
                        int rowSize = Marshal.SizeOf(typeof(MIB_UDPROW_OWNER_PID));
                        for (int i = 0; i < count; i++)
                        {
                            MIB_UDPROW_OWNER_PID row = (MIB_UDPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_UDPROW_OWNER_PID));
                            if (row.owningPid > 4)
                            {
                                byte locFirst = (byte)(row.localAddr & 0xFF);
                                if (row.localAddr != 0 && (!filterLoopback || locFirst != 127))
                                {
                                    pids.Add(row.owningPid);
                                }
                            }
                            rowPtr = (IntPtr)((long)rowPtr + rowSize);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }

            // 4. UDP IPv6 - Filtra loopback
            size = 0;
            GetExtendedUdpTable(IntPtr.Zero, ref size, false, AF_INET6, UDP_TABLE_OWNER_PID, 0);
            if (size > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (GetExtendedUdpTable(buf, ref size, false, AF_INET6, UDP_TABLE_OWNER_PID, 0) == 0)
                    {
                        int count = Marshal.ReadInt32(buf);
                        IntPtr rowPtr = (IntPtr)((long)buf + 4);
                        int rowSize = Marshal.SizeOf(typeof(MIB_UDP6ROW_OWNER_PID));
                        for (int i = 0; i < count; i++)
                        {
                            MIB_UDP6ROW_OWNER_PID row = (MIB_UDP6ROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_UDP6ROW_OWNER_PID));
                            if (row.owningPid > 4 && row.ucLocalAddr != null && row.ucLocalAddr.Length == 16)
                            {
                                bool isLoopback6 = true;
                                for (int b = 0; b < 15; b++) { if (row.ucLocalAddr[b] != 0) { isLoopback6 = false; break; } }
                                if (!filterLoopback || !(isLoopback6 && row.ucLocalAddr[15] == 1))
                                {
                                    pids.Add(row.owningPid);
                                }
                            }
                            rowPtr = (IntPtr)((long)rowPtr + rowSize);
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }

            return pids;
        }

        private string GetProcessName(int pid)
        {
            if (cachedProcessNames.ContainsKey(pid))
                return cachedProcessNames[pid];

            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc != IntPtr.Zero)
            {
                try
                {
                    StringBuilder sb = new StringBuilder(512);
                    int size = sb.Capacity;
                    if (QueryFullProcessImageName(hProc, 0, sb, ref size) != 0)
                    {
                        string fn = System.IO.Path.GetFileName(sb.ToString());
                        if (!string.IsNullOrEmpty(fn))
                        {
                            cachedProcessNames[pid] = fn;
                            return fn;
                        }
                    }
                }
                finally
                {
                    CloseHandle(hProc);
                }
            }

            try
            {
                string n = Process.GetProcessById(pid).ProcessName + ".exe";
                cachedProcessNames[pid] = n;
                return n;
            }
            catch
            {
                string fallback = "PID " + pid;
                cachedProcessNames[pid] = fallback;
                return fallback;
            }
        }


        // ------------------------------------------------------------------ timer principal
        private void SetupTimer()
        {
            int tickCount = 0;
            updateTimer = new Timer();
            updateTimer.Interval = 300;
            bool ssCached = false;
            int ssTick = 0;
            updateTimer.Tick += (s, e) =>
            {
                if (S.HideOnScreensaver)
                {
                    // a varredura de protetor de tela é cara: roda a cada ~0,9 s, não a cada tick de 300 ms
                    if (ssTick++ % 3 == 0) ssCached = IsAnyScreensaverActive();
                    bool ss = ssCached;
                    if (ss || isSessionLocked)
                    {
                        if (!isScreensaverRunning || this.Visible)
                        {
                            isScreensaverRunning = true;
                            this.Visible = false;
                            SetWindowPos(this.Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_HIDEWINDOW);
                            HideFlyout(true);
                        }
                        return;
                    }
                    else if (isScreensaverRunning || !this.Visible)
                    {
                        isScreensaverRunning = false;
                        this.Visible = true;
                        PositionWidget();
                    }
                }

                tickCount++;
                if (tickCount >= 3)
                {
                    tickCount = 0;
                    UpdateTraffic();
                    if (settingsOpen) PushData();
                    if (IsWindowsDarkTheme() != lastSystemDark) { ApplySystemTheme(); ScheduleThemeRepaint(); }
                    if (!customPos && this.Visible && !isScreensaverRunning)
                    {
                        Point a = AutoAnchor();
                        if (a != this.Location) { this.Location = a; if (flyout != null && flyout.Visible) PositionFlyout(); }
                    }
                }

                if (this.Visible && !isScreensaverRunning && !isSessionLocked)
                {
                    SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            };
            updateTimer.Start();
            UpdateTraffic();
        }

        private void BindToTaskbarOwner()
        {
            try
            {
                IntPtr trayWnd = FindWindow("Shell_TrayWnd", null);
                if (trayWnd != IntPtr.Zero) SetWindowLongPtr(this.Handle, GWLP_HWNDPARENT, trayWnd);
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SETTINGCHANGE = 0x001A;
            const int WM_THEMECHANGED = 0x031A;
            if (m.Msg == WM_SETTINGCHANGE || m.Msg == WM_THEMECHANGED || m.Msg == 0x031E /* DWMCOMPOSITIONCHANGED */ || m.Msg == 0x0320 /* DWMCOLORIZATIONCOLORCHANGED */)
            {
                ApplySystemTheme();
                ScheduleThemeRepaint();
            }

            if (m.Msg == WM_DISPLAYCHANGE)
            {
                PositionWidget(false);
                if (flyout != null && flyout.Visible) PositionFlyout();
            }

            if (m.Msg == Ipc.WM_COPYDATA)
            {
                int code; string text;
                if (Ipc.Read(m.LParam, out code, out text)) { OnIpc(code, text, m.WParam); m.Result = new IntPtr(1); return; }
            }

            if (m.Msg == WM_DPICHANGED)
            {
                MeasureWidget();
                RenderWidget();
                if (flyout != null && flyout.Visible) UpdateFlyout();
            }

            if (m.Msg == WM_WINDOWPOSCHANGING)
            {
                if (this.Visible && !isScreensaverRunning && !isSessionLocked)
                {
                    WINDOWPOS pos = (WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(WINDOWPOS));
                    pos.hwndInsertAfter = HWND_TOPMOST;
                    Marshal.StructureToPtr(pos, m.LParam, false);
                }
            }
            base.WndProc(ref m);
        }

        // ------------------------------------------------------------------ configurações
        // Aplica um novo conjunto de configurações (thread da interface). persist=true grava no registro.
        private void ApplyInternal(AppSettings ns, bool persist)
        {
            string oldAdapter;
            lock (sLock) { oldAdapter = S.Adapter; S = ns; }
            filterLoopback = S.LoopbackFilter;
            lastSystemDark = IsWindowsDarkTheme();
            if (persist) S.Save();
            if (!string.Equals(oldAdapter, S.Adapter, StringComparison.OrdinalIgnoreCase))
            {
                cachedNic = null; lastBytesIn = -1; lastBytesOut = -1; // trocou de placa: recomeça a medição
            }
            RenderWidget();
            if (!S.FlyoutEnabled)
            {
                // desligado: descarta a janela e a imagem; recriadas se o usuário religar
                if (hoverCheckTimer != null) hoverCheckTimer.Stop();
                if (flyout != null) { flyout.Dispose(); flyout = null; }
            }
            else if (S.FlyoutPinned && !isScreensaverRunning && !isSessionLocked) ShowFlyout();
            else if (flyout != null && flyout.Visible)
            {
                UpdateFlyout();
                if (!S.FlyoutPinned && !this.Bounds.Contains(Cursor.Position) && !flyout.CardBounds.Contains(Cursor.Position)) HideFlyout(false);
            }
        }

        // Altera um campo, aplica e grava (menu de contexto)
        private void Edit(Action<AppSettings> change)
        {
            AppSettings n;
            lock (sLock) { n = S.Clone(); }
            change(n);
            ApplyInternal(n, true);
        }

        public void SavePosition()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REG_KEY_APP))
                {
                    if (key != null)
                    {
                        key.SetValue("CustomX", this.Location.X);
                        key.SetValue("CustomY", this.Location.Y);
                        key.SetValue("CustomPos", 1);
                    }
                }
            }
            catch { }
        }

        private bool customPos = false;
        private Point savedPos = new Point(0, 0);

        // Posição automática: encostado à esquerda da área de notificação e centralizado na vertical da barra de tarefas.
        private Point AutoAnchor()
        {
            Screen primary = Screen.PrimaryScreen;
            float sc = GetDpi() / 96f;
            int posX = primary.Bounds.Width - 360 - curW;
            int posY = primary.WorkingArea.Bottom + (primary.Bounds.Height - primary.WorkingArea.Bottom - curH) / 2;
            IntPtr trayWnd = FindWindow("Shell_TrayWnd", null);
            if (trayWnd != IntPtr.Zero)
            {
                RECT tr;
                if (GetWindowRect(trayWnd, out tr) && tr.Bottom > tr.Top) posY = tr.Top + ((tr.Bottom - tr.Top) - curH) / 2;
                IntPtr notifyWnd = FindWindowEx(trayWnd, IntPtr.Zero, "TrayNotifyWnd", null);
                RECT nr;
                if (notifyWnd != IntPtr.Zero && GetWindowRect(notifyWnd, out nr) && nr.Right > nr.Left)
                    posX = nr.Left - curW - (int)Math.Round(8 * sc);
            }
            return new Point(posX, posY);
        }

        private void LoadSavedPos()
        {
            customPos = false;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REG_KEY_APP))
                {
                    if (key != null && key.GetValue("CustomPos") != null && Convert.ToInt32(key.GetValue("CustomPos")) == 1
                        && key.GetValue("CustomX") != null && key.GetValue("CustomY") != null)
                    {
                        savedPos = new Point(Convert.ToInt32(key.GetValue("CustomX")), Convert.ToInt32(key.GetValue("CustomY")));
                        customPos = true;
                    }
                }
            }
            catch { }
        }

        public void ClearSavedPos()
        {
            customPos = false;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REG_KEY_APP))
                {
                    if (key != null)
                    {
                        key.DeleteValue("CustomX", false);
                        key.DeleteValue("CustomY", false);
                        key.DeleteValue("CustomPos", false);
                    }
                }
            }
            catch { }
        }

        public void PositionWidget(bool forceReset = false)
        {
            if (forceReset) ClearSavedPos(); else LoadSavedPos();
            Point p = customPos ? savedPos : AutoAnchor();

            // Proteção estrita multi-monitor
            Screen target = Screen.FromPoint(p);
            if (target == null) target = Screen.PrimaryScreen;
            int posX = Math.Max(target.Bounds.Left, Math.Min(p.X, target.Bounds.Right - curW));
            int posY = Math.Max(target.Bounds.Top, Math.Min(p.Y, target.Bounds.Bottom - curH));

            this.Location = new Point(posX, posY);
            SetWindowPos(this.Handle, HWND_TOPMOST, posX, posY, curW, curH, SWP_NOACTIVATE | SWP_SHOWWINDOW);
            RenderWidget();
        }

        // ------------------------------------------------------------------ medição
        private void UpdateTraffic()
        {
            if (cachedNic == null || cachedNic.OperationalStatus != OperationalStatus.Up)
            {
                cachedNic = GetTargetNic();
            }

            if (cachedNic == null)
            {
                if (liveUpBits != 0 || liveDownBits != 0) { liveUpBits = 0; liveDownBits = 0; }
                RenderIfChanged();
                return;
            }

            try
            {
                var stats = cachedNic.GetIPv4Statistics();
                long currentIn = stats.BytesReceived;
                long currentOut = stats.BytesSent;
                DateTime now = DateTime.UtcNow;

                if (lastBytesIn >= 0 && lastMeasurementTime != DateTime.MinValue)
                {
                    double elapsed = (now - lastMeasurementTime).TotalSeconds;
                    if (elapsed > 0.1)
                    {
                        long diffIn = currentIn - lastBytesIn;
                        long diffOut = currentOut - lastBytesOut;
                        if (diffIn < 0) diffIn = 0;
                        if (diffOut < 0) diffOut = 0;

                        liveDownBits = (diffIn * 8.0) / elapsed;
                        liveUpBits = (diffOut * 8.0) / elapsed;
                        physRxBytes = diffIn / elapsed;
                        physTxBytes = diffOut / elapsed;
                        RenderIfChanged();
                    }
                }

                lastBytesIn = currentIn;
                lastBytesOut = currentOut;
                lastMeasurementTime = now;
            }
            catch
            {
                cachedNic = null;
            }
        }

        // Zero CPU quando nada muda: só redesenha se o texto exibido mudou
        private void RenderIfChanged()
        {
            string k = RenderKey();
            if (k != lastRenderKey) RenderWidget();
        }

        // ------------------------------------------------------------------ mouse
        private void NetMonitorApp_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            HideFlyoutIfNotPinned();
            Point start = Cursor.Position;
            Point startLoc = this.Location;
            if (!S.LockPosition)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                Screen scr = Screen.FromControl(this);
                if (scr != null)
                {
                    Rectangle b = scr.Bounds;
                    int clX = Math.Max(b.Left, Math.Min(this.Left, b.Right - curW));
                    int clY = Math.Max(b.Top, Math.Min(this.Top, b.Bottom - curH));
                    if (clX != this.Left || clY != this.Top) this.Location = new Point(clX, clY);
                }
                if (this.Location != startLoc) { customPos = true; SavePosition(); }
            }
            Point end = Cursor.Position;
            // clique sem arrastar: alterna o flyout entre "ao passar o mouse" e "fixado aberto"
            if (Math.Abs(end.X - start.X) < 4 && Math.Abs(end.Y - start.Y) < 4)
                Edit(delegate(AppSettings n) { n.FlyoutPinned = !n.FlyoutPinned; });
        }

        private void NetMonitorApp_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                HideFlyoutIfNotPinned();
                BuildContextMenu();
                Screen scr = Screen.FromControl(this);
                if (scr == null) scr = Screen.PrimaryScreen;
                Rectangle b = scr.Bounds;

                Size pref = contextMenu.PreferredSize;
                int mnuW = pref.Width > 0 ? pref.Width : 240;
                int mnuH = pref.Height > 0 ? pref.Height : 360;

                int x = this.Left;
                if (x + mnuW > b.Right - 4) x = b.Right - mnuW - 4;
                if (x < b.Left + 4) x = b.Left + 4;
                int y = this.Top - mnuH - 2;
                if (y < b.Top + 4) y = this.Bottom + 2;
                contextMenu.Show(x, y);
            }
        }

        // ------------------------------------------------------------------ flyout
        private void OnWidgetMouseEnter(object sender, EventArgs e)
        {
            if (settingsOpen || isScreensaverRunning || isSessionLocked) return;
            if (contextMenu != null && contextMenu.Visible) return;
            ShowFlyout();
        }

        private void OnWidgetMouseLeave(object sender, EventArgs e)
        {
            if (hoverCheckTimer != null && !hoverCheckTimer.Enabled) hoverCheckTimer.Start();
        }

        private void ShowFlyout()
        {
            if (isScreensaverRunning || isSessionLocked || !S.FlyoutEnabled) return;
            if (flyout == null || flyout.IsDisposed) flyout = new FlyoutForm(this);
            bool wasVisible = flyout.Visible;
            UpdateFlyout();
            if (!wasVisible || flyout.IsHiding) flyout.ShowAnimated();

            isHoverSampling = true;
            lastProcessIo.Clear();
            ThreadPool.QueueUserWorkItem(DoProcessSampleWork);
            EnsureSampling();

            if (hoverCheckTimer == null)
            {
                hoverCheckTimer = new Timer();
                hoverCheckTimer.Interval = 200;
                hoverCheckTimer.Tick += HoverCheckTimer_Tick;
            }
            hoverCheckTimer.Start();
        }

        private void UpdateFlyout()
        {
            if (flyout == null || flyout.IsDisposed) return;
            AppSettings cur;
            lock (sLock) { cur = S; }
            flyout.SetData(cur, EffectiveDark, TopApps(), liveUpBits, liveDownBits, GetDpi());
            PositionFlyout();
        }

        private void EnsureSampling()
        {
            if (processSampleTimer == null)
            {
                processSampleTimer = new Timer();
                processSampleTimer.Interval = 1000;
                processSampleTimer.Tick += (s, e) =>
                {
                    bool need = (flyout != null && flyout.Visible) || settingsSampling;
                    if (need) ThreadPool.QueueUserWorkItem(DoProcessSampleWork);
                    else { processSampleTimer.Stop(); isHoverSampling = false; lastProcessIo.Clear(); cachedProcessNames.Clear(); }
                };
            }
            if (!processSampleTimer.Enabled) processSampleTimer.Start();
            isHoverSampling = true;
        }

        private void HoverCheckTimer_Tick(object sender, EventArgs e)
        {
            if (!S.FlyoutEnabled) { HideFlyout(true); return; }
            if (S.FlyoutPinned) { if (flyout == null || !flyout.Visible) ShowFlyout(); return; }
            Point cursor = Cursor.Position;
            bool overWidget = this.Visible && this.Bounds.Contains(cursor);
            bool overFlyout = flyout != null && flyout.Visible && flyout.CardBounds.Contains(cursor);
            if (!overWidget && !overFlyout) HideFlyout(false);
        }

        private void HideFlyoutIfNotPinned() { if (!S.FlyoutPinned) HideFlyout(false); }

        private void HideFlyout(bool force)
        {
            if (S.FlyoutPinned && S.FlyoutEnabled && !force) return;
            if (hoverCheckTimer != null) hoverCheckTimer.Stop();
            if (flyout != null && flyout.Visible) flyout.HideAnimated();
        }

        private void PositionFlyout()
        {
            if (flyout == null || flyout.IsDisposed) return;
            Screen scr = Screen.FromControl(this);
            if (scr == null) scr = Screen.PrimaryScreen;
            flyout.PlaceAbove(this.Bounds, scr.WorkingArea);
        }

        public void SetFlyoutMode(string mode) { Edit(delegate(AppSettings n) { n.FlyoutMode = mode; }); }

        // ------------------------------------------------------------------ amostragem por processo
        private static string CategoryFor(string name, string path)
        {
            string n = name.ToLowerInvariant();
            switch (n)
            {
                case "chrome": case "msedge": case "firefox": case "brave": case "opera": case "vivaldi": return "Navegador";
                case "spotify": return "Música";
                case "discord": case "slack": case "telegram": case "whatsapp": case "zoom": case "teams": case "ms-teams": return "Voz e Chat";
                case "steam": case "steamwebhelper": case "epicgameslauncher": case "battle.net": return "Jogos";
                case "code": case "devenv": case "idea64": case "pycharm64": case "cursor": return "Editor";
                case "onedrive": case "dropbox": case "googledrivefs": return "Nuvem";
                case "svchost": return "Serviço do Windows";
                case "qbittorrent": case "utorrent": return "Torrent";
            }
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    string d = FileVersionInfo.GetVersionInfo(path).FileDescription;
                    if (!string.IsNullOrEmpty(d) && d.Length <= 22) return d;
                }
            }
            catch { }
            return "Aplicativo";
        }

        private string GetProcessPath(int pid)
        {
            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc == IntPtr.Zero) return null;
            try
            {
                StringBuilder sb = new StringBuilder(520);
                int size = sb.Capacity;
                if (QueryFullProcessImageName(hProc, 0, sb, ref size) != 0) return sb.ToString();
            }
            finally { CloseHandle(hProc); }
            return null;
        }

        private void DoProcessSampleWork(object state)
        {
            if (!isHoverSampling) return;
            try
            {
                HashSet<int> activePids = GetActiveNetworkPids();
                DateTime now = DateTime.UtcNow;
                double elapsed = lastSampleAt == DateTime.MinValue ? 1.0 : Math.Max(0.2, (now - lastSampleAt).TotalSeconds);
                lastSampleAt = now;
                double physRx = physRxBytes, physTx = physTxBytes;

                Dictionary<string, ulong> appTx = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, ulong> appRx = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> appPid = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (int pid in activePids)
                {
                    IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                    if (hProc == IntPtr.Zero) continue;
                    try
                    {
                        IO_COUNTERS c;
                        if (!GetProcessIoCounters(hProc, out c)) continue;
                        IoSnap cur = new IoSnap { R = c.ReadTransferCount, W = c.WriteTransferCount };
                        IoSnap last;
                        if (lastProcessIo.TryGetValue(pid, out last))
                        {
                            ulong dr = cur.R >= last.R ? (ulong)((cur.R - last.R) / elapsed) : 0;
                            ulong dw = cur.W >= last.W ? (ulong)((cur.W - last.W) / elapsed) : 0;
                            // Isolamento de I/O de disco: um processo não transfere pela rede mais que a placa mediu.
                            ulong capR = (ulong)(physRx * 1.2) + 4096, capW = (ulong)(physTx * 1.2) + 4096;
                            if (dr > capR) dr = physRx > 100 ? (ulong)physRx : 0;
                            if (dw > capW) dw = physTx > 100 ? (ulong)physTx : 0;
                            if (dr > 0 || dw > 0)
                            {
                                string baseName = GetProcessName(pid);
                                if (baseName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) baseName = baseName.Substring(0, baseName.Length - 4);
                                if (!appTx.ContainsKey(baseName)) { appTx[baseName] = 0; appRx[baseName] = 0; appPid[baseName] = pid; }
                                appTx[baseName] += dw;
                                appRx[baseName] += dr;
                            }
                        }
                        lastProcessIo[pid] = cur;
                    }
                    finally { CloseHandle(hProc); }
                }

                if (lastProcessIo.Count > 100)
                {
                    List<int> rm = new List<int>();
                    foreach (int k in lastProcessIo.Keys) if (!activePids.Contains(k)) rm.Add(k);
                    for (int i = 0; i < rm.Count; i++) lastProcessIo.Remove(rm[i]);
                }

                // Normalização coletiva por direção
                Normalize(appTx, physTx);
                Normalize(appRx, physRx);

                List<ProcessTrafficItem> items = new List<ProcessTrafficItem>();
                foreach (var kv in appTx)
                {
                    ulong tx = kv.Value, rx = appRx[kv.Key];
                    if (tx == 0 && rx == 0) continue;
                    string sub;
                    if (!subCache.TryGetValue(kv.Key, out sub)) { sub = CategoryFor(kv.Key, GetProcessPath(appPid[kv.Key])); subCache[kv.Key] = sub; }
                    items.Add(new ProcessTrafficItem { Pid = appPid[kv.Key], Name = kv.Key, Sub = sub, TxBytes = tx, RxBytes = rx });
                }
                items.Sort((a, b) => b.Total.CompareTo(a.Total));
                if (items.Count > 5) items.RemoveRange(5, items.Count - 5);

                lock (topLock) { latestTop = items; }
                if (this.IsHandleCreated && flyout != null && flyout.Visible)
                    this.BeginInvoke((MethodInvoker)delegate { UpdateFlyout(); });
            }
            catch { }
        }

        private static void Normalize(Dictionary<string, ulong> d, double phys)
        {
            ulong total = 0;
            foreach (var v in d.Values) total += v;
            ulong max = (ulong)(phys * 1.25) + 8192;
            if (total > max && total > 0 && phys > 0)
            {
                double scale = phys / (double)total;
                List<string> keys = new List<string>(d.Keys);
                foreach (string k in keys) d[k] = (ulong)(d[k] * scale);
            }
        }

        // ------------------------------------------------------------------ menu de contexto
        private string L(string pt, string en) { return currentLanguage == "pt" ? pt : en; }

        private void BuildContextMenu()
        {
            bool isDark = EffectiveDark;
            if (contextMenu != null) { try { contextMenu.Dispose(); } catch { } }
            contextMenu = new ContextMenuStrip();
            contextMenu.Opening += (s, e) => { HideFlyoutIfNotPinned(); };
            contextMenu.Renderer = new Windows11MenuRenderer(isDark);
            contextMenu.ShowCheckMargin = true;
            contextMenu.ShowImageMargin = false;
            Font menuFont = new Font(WidgetRenderer.FontInstalled("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI", 10f);
            contextMenu.Font = menuFont;
            contextMenu.Padding = new Padding(4, 6, 4, 6);
            contextMenu.Opened += (s, e) => Windows11MenuRenderer.StyleDropDown(contextMenu, isDark);
            AppSettings c;
            lock (sLock) { c = S; }

            if (this.trayIcon != null) this.trayIcon.ContextMenuStrip = this.contextMenu;
            bool bytes = c.UnitBase == "bytes";

            // Seções com título, como no design: as escolhas frequentes ficam a um clique
            contextMenu.Items.Add(Header(L("UNIDADE", "UNIT"), menuFont));
            contextMenu.Items.Add(Check(L("Mbps · megabits (padrão)", "Mbps · megabits (default)"), !bytes, delegate(AppSettings n) { n.UnitBase = "bits"; }));
            contextMenu.Items.Add(Check(L("MB/s · megabytes", "MB/s · megabytes"), bytes, delegate(AppSettings n) { n.UnitBase = "bytes"; }));
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(Header(L("ESCALA", "SCALE"), menuFont));
            contextMenu.Items.Add(Check(L("Automática", "Automatic"), c.ScaleMode == "auto", delegate(AppSettings n) { n.ScaleMode = "auto"; }));
            contextMenu.Items.Add(Check(L("Fixa em ", "Fixed in ") + (bytes ? "MB/s" : "Mbps"), c.ScaleMode == "mb", delegate(AppSettings n) { n.ScaleMode = "mb"; }));
            contextMenu.Items.Add(Check(L("Fixa em ", "Fixed in ") + (bytes ? "KB/s" : "Kbps"), c.ScaleMode == "kb", delegate(AppSettings n) { n.ScaleMode = "kb"; }));
            contextMenu.Items.Add(Check(L("Individual (TX e RX)", "Individual (TX and RX)"), c.ScaleMode == "individual", delegate(AppSettings n) { n.ScaleMode = "individual"; }));
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(Header("FLYOUT", menuFont));
            contextMenu.Items.Add(Check(L("Ativado", "Enabled"), c.FlyoutEnabled, delegate(AppSettings n) { n.FlyoutEnabled = !n.FlyoutEnabled; }));
            if (c.FlyoutEnabled)
            {
                contextMenu.Items.Add(Check(L("Abrir ao passar o mouse", "Open on hover"), !c.FlyoutPinned, delegate(AppSettings n) { n.FlyoutPinned = false; }));
                // clicar de novo em "Fixado aberto" desfixa
                contextMenu.Items.Add(Check(L("Fixado aberto", "Pinned open"), c.FlyoutPinned, delegate(AppSettings n) { n.FlyoutPinned = !n.FlyoutPinned; }));
            }

            // o que se usa raramente fica em "Mais opções"
            ToolStripMenuItem more = new ToolStripMenuItem(L("Mais opções", "More options"));
            ConfigureSubmenu(more);
            ToolStripMenuItem mnuAdapters = new ToolStripMenuItem(L("Placa de rede", "Network adapter"));
            ConfigureSubmenu(mnuAdapters);
            PopulateAdaptersMenu(mnuAdapters);
            more.DropDownItems.Add(mnuAdapters);
            ToolStripMenuItem mnuArrows = new ToolStripMenuItem(L("Posição das setas", "Arrow position"));
            ConfigureSubmenu(mnuArrows);
            mnuArrows.DropDownItems.Add(Check(L("À esquerda (▲ 823 Kbps)", "Left (▲ 823 Kbps)"), c.ArrowPosition == "left", delegate(AppSettings n) { n.ArrowPosition = "left"; }));
            mnuArrows.DropDownItems.Add(Check(L("À direita (823 Kbps ▲)", "Right (823 Kbps ▲)"), c.ArrowPosition == "right", delegate(AppSettings n) { n.ArrowPosition = "right"; }));
            mnuArrows.DropDownItems.Add(Check(L("Sem setas", "No arrows"), c.ArrowPosition == "none", delegate(AppSettings n) { n.ArrowPosition = "none"; }));
            more.DropDownItems.Add(mnuArrows);
            ToolStripMenuItem mnuLang = new ToolStripMenuItem(L("Idioma do menu", "Menu language"));
            ConfigureSubmenu(mnuLang);
            mnuLang.DropDownItems.Add(Check("Português (Brasil)", c.Language == "pt", delegate(AppSettings n) { n.Language = "pt"; }));
            mnuLang.DropDownItems.Add(Check("English (United States)", c.Language != "pt", delegate(AppSettings n) { n.Language = "en"; }));
            more.DropDownItems.Add(mnuLang);
            more.DropDownItems.Add(new ToolStripSeparator());
            more.DropDownItems.Add(Check(L("Flyout: upload e download separados", "Flyout: separate upload and download"), c.FlyoutMode != "combined", delegate(AppSettings n) { n.FlyoutMode = c.FlyoutMode == "combined" ? "separated" : "combined"; }));
            more.DropDownItems.Add(Check(L("Travar posição", "Lock position"), c.LockPosition, delegate(AppSettings n) { n.LockPosition = !n.LockPosition; }));
            more.DropDownItems.Add(Check(L("Iniciar com o Windows", "Start with Windows"), c.StartWithWindows, delegate(AppSettings n) { n.StartWithWindows = !n.StartWithWindows; }));
            ToolStripMenuItem mnuReset = new ToolStripMenuItem(L("Redefinir posição", "Reset position"));
            mnuReset.Click += (s, e) => PositionWidget(true);
            more.DropDownItems.Add(mnuReset);
            contextMenu.Items.Add(more);

            contextMenu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem mnuSettings = new ToolStripMenuItem(L("Configurações...", "Settings..."));
            mnuSettings.Font = new Font(menuFont, FontStyle.Bold);
            mnuSettings.Click += (s, e) => OpenSettingsDialog("cores");
            contextMenu.Items.Add(mnuSettings);
            ToolStripMenuItem mnuAbout = new ToolStripMenuItem(L("Sobre o NetMonitor...", "About NetMonitor..."));
            mnuAbout.Click += (s, e) => OpenSettingsDialog("sobre");
            contextMenu.Items.Add(mnuAbout);
            ToolStripMenuItem mnuExit = new ToolStripMenuItem(L("Sair", "Exit"));
            mnuExit.Click += (s, e) =>
            {
                try { HideFlyout(true); this.Close(); }
                catch { Environment.Exit(0); }
            };
            contextMenu.Items.Add(mnuExit);
            StyleItems(contextMenu.Items, menuFont, isDark);
        }

        // Altura confortável, mesma fonte e mesmo visual (cantos/borda) em todos os submenus
        private void StyleItems(ToolStripItemCollection items, Font f, bool dark)
        {
            foreach (ToolStripItem it in items)
            {
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi == null) continue;
                mi.Padding = new Padding(4, 5, 4, 5);
                if (mi.HasDropDownItems)
                {
                    ToolStripDropDown dd = mi.DropDown;
                    dd.Font = f;
                    dd.Renderer = contextMenu.Renderer;
                    dd.Padding = new Padding(4, 6, 4, 6);
                    dd.Opened += (s, e) => Windows11MenuRenderer.StyleDropDown(dd, dark);
                    StyleItems(mi.DropDownItems, f, dark);
                }
            }
        }

        // Título de seção (não clicável), em versalete pequeno e cor secundária
        private static ToolStripMenuItem Header(string text, Font baseFont)
        {
            ToolStripMenuItem h = new ToolStripMenuItem(text);
            h.Enabled = false;
            h.Font = new Font(baseFont.FontFamily, 7.5f, FontStyle.Bold);
            return h;
        }

        private ToolStripMenuItem Check(string text, bool on, Action<AppSettings> change)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            it.Checked = on;
            it.Click += (s, e) => Edit(change);
            return it;
        }

        private void ConfigureSubmenu(ToolStripMenuItem parent)
        {
            parent.DropDown.DropShadowEnabled = true;
            parent.DropDownOpening += (s, e) =>
            {
                Screen scr = Screen.FromControl(this);
                if (contextMenu.Right + parent.DropDown.Width > scr.WorkingArea.Right) parent.DropDownDirection = ToolStripDropDownDirection.Left;
                else parent.DropDownDirection = ToolStripDropDownDirection.Right;
            };
        }

        private void PopulateAdaptersMenu(ToolStripMenuItem parent)
        {
            parent.DropDownItems.Clear();
            ToolStripMenuItem itmAuto = new ToolStripMenuItem(L("Automático (Maior Tráfego)", "Auto (Highest Traffic)"));
            itmAuto.Checked = string.Equals(selectedNicId, "Auto", StringComparison.OrdinalIgnoreCase);
            itmAuto.Click += (s, e) => SelectAdapter("Auto");
            parent.DropDownItems.Add(itmAuto);
            parent.DropDownItems.Add(new ToolStripSeparator());

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback || nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                string id = nic.Id;
                string name = string.Format("{0} ({1})", nic.Name, nic.Description);
                if (name.Length > 45) name = name.Substring(0, 42) + "...";
                ToolStripMenuItem itm = new ToolStripMenuItem(name);
                itm.Checked = (selectedNicId == id || selectedNicId == nic.Name);
                itm.Click += (s, e) => SelectAdapter(id);
                parent.DropDownItems.Add(itm);
            }
        }

        private void SelectAdapter(string id)
        {
            cachedNic = null; lastBytesIn = -1; lastBytesOut = -1;
            Edit(delegate(AppSettings n) { n.Adapter = id; });
            UpdateTraffic();
        }

        // ------------------------------------------------------------------ janela de configurações
        // A janela roda em outro processo (NetMonitor.exe --settings): o WPF nunca entra neste processo
        public void OpenSettingsDialog(string page)
        {
            settingsOpen = true;
            HideFlyout(true);
            Ipc.AllowSetForegroundWindow(-1);
            if (settingsHwnd != IntPtr.Zero && Ipc.IsWindow(settingsHwnd))
            {
                Ipc.Send(settingsHwnd, Ipc.ShowPage, page, this.Handle);
                return;
            }
            try { if (settingsProc != null && !settingsProc.HasExited) return; } catch { }
            try
            {
                settingsProc = Process.Start(new ProcessStartInfo(Application.ExecutablePath,
                    "--settings " + this.Handle.ToInt64().ToString(CultureInfo.InvariantCulture) + " " + page) { UseShellExecute = false });
            }
            catch { settingsOpen = false; }
        }

        private void OnIpc(int code, string text, IntPtr sender)
        {
            if (code == Ipc.ApplyLive || code == Ipc.ApplySave)
            {
                ApplyInternal(AppSettings.FromText(text), code == Ipc.ApplySave);
            }
            else if (code == Ipc.Hello)
            {
                long h;
                settingsHwnd = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out h) ? new IntPtr(h) : sender;
                settingsOpen = true;
                PushData();
            }
            else if (code == Ipc.Bye) SettingsGone();
            else if (code == Ipc.Sampling)
            {
                settingsSampling = text == "1";
                if (settingsSampling) { EnsureSampling(); ThreadPool.QueueUserWorkItem(DoProcessSampleWork); }
            }
            else if (code == Ipc.Refresh) ThreadPool.QueueUserWorkItem(DoProcessSampleWork);
        }

        private void SettingsGone()
        {
            settingsHwnd = IntPtr.Zero;
            settingsOpen = false;
            settingsSampling = false;
            if (S.FlyoutPinned) ShowFlyout();
        }

        // Envia à janela de Configurações as velocidades, o consumo deste processo e o top 5
        private void PushData()
        {
            if (settingsHwnd == IntPtr.Zero)
            {
                // a janela não chegou a abrir (ou caiu antes de se apresentar)
                try { if (settingsOpen && (settingsProc == null || settingsProc.HasExited)) settingsOpen = false; } catch { settingsOpen = false; }
                return;
            }
            if (!Ipc.IsWindow(settingsHwnd)) { SettingsGone(); return; }
            Ipc.Send(settingsHwnd, Ipc.Data, BuildDataText(), this.Handle);
        }

        private string BuildDataText()
        {
            double cpu, ram;
            PerfInfo(out cpu, out ram);
            CultureInfo inv = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.Append("up=").Append(liveUpBits.ToString("R", inv)).Append('\n');
            sb.Append("dn=").Append(liveDownBits.ToString("R", inv)).Append('\n');
            sb.Append("cpu=").Append(cpu.ToString("R", inv)).Append('\n');
            sb.Append("ram=").Append(ram.ToString("R", inv)).Append('\n');
            foreach (ProcessTrafficItem it in TopApps())
                sb.Append("app=").Append(it.Name.Replace('\t', ' ')).Append('\t').Append((it.Sub ?? "").Replace('\t', ' '))
                  .Append('\t').Append(it.TxBytes).Append('\t').Append(it.RxBytes).Append('\n');
            return sb.ToString();
        }

        private void StartOpenSettingsListener()
        {
            try
            {
                EventWaitHandle ev = new EventWaitHandle(false, EventResetMode.AutoReset, OPEN_SETTINGS_EVENT);
                Thread t = new Thread(delegate()
                {
                    while (true)
                    {
                        try { ev.WaitOne(); this.BeginInvoke((MethodInvoker)delegate { OpenSettingsDialog("cores"); }); }
                        catch { break; }
                    }
                }) { IsBackground = true, Name = "NetMonitorOpenSettings" };
                t.Start();
            }
            catch { }
        }

        // ------------------------------------------------------------------ dados para a janela de Configurações
        public List<ProcessTrafficItem> TopApps() { lock (topLock) { return latestTop; } }

        public void PerfInfo(out double cpuPct, out double ramMb)
        {
            lock (sLock)
            {
                Process p = Process.GetCurrentProcess();
                TimeSpan t = p.TotalProcessorTime;
                DateTime now = DateTime.UtcNow;
                double el = (now - lastCpuAt).TotalSeconds;
                cpuPct = (lastCpuAt == DateTime.MinValue || el <= 0) ? 0 : (t - lastCpu).TotalSeconds / el / Environment.ProcessorCount * 100.0;
                lastCpu = t; lastCpuAt = now;
                ramMb = p.WorkingSet64 / 1048576.0;
            }
        }

        // ------------------------------------------------------------------ encerramento e entrada
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                if (updateTimer != null) { updateTimer.Stop(); updateTimer.Dispose(); }
                if (hoverCheckTimer != null) { hoverCheckTimer.Stop(); hoverCheckTimer.Dispose(); }
                if (processSampleTimer != null) { processSampleTimer.Stop(); processSampleTimer.Dispose(); }
                if (flyout != null) { flyout.Dispose(); flyout = null; }
                if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
            }
            catch { }
            base.OnFormClosing(e);
        }

        private static Mutex singleInstanceMutex = null;

        // Método separado: só o processo da janela carrega (e compila) o código WPF
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void RunSettings(string[] args)
        {
            try { Ui.SettingsProcess.Run(args); }
            catch (Exception ex) { Ui.K.Log(ex); }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--settings")
            {
                try { SetProcessDpiAwareness(2); } catch { }
                RunSettings(args);
                return;
            }

            bool createdNew = false;
            try { singleInstanceMutex = new Mutex(true, "NetMonitor_SingleInstance_Mutex_EduardoNoe", out createdNew); }
            catch { createdNew = false; }

            if (!createdNew)
            {
                // Já existe uma instância: pede para ela abrir as configurações.
                try { EventWaitHandle.OpenExisting(OPEN_SETTINGS_EVENT).Set(); } catch { }
                return;
            }

            try { SetProcessDpiAwareness(2); }
            catch { try { SetProcessDPIAware(); } catch { } }

            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => { };
                Application.ThreadException += (s, e) => { };
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new NetMonitorApp()); }
            finally
            {
                if (singleInstanceMutex != null)
                {
                    try { singleInstanceMutex.ReleaseMutex(); } catch { }
                    try { singleInstanceMutex.Close(); } catch { }
                }
            }
        }
    }

    // ==============================================================================
    // JANELA EM CAMADAS: apresenta um bitmap ARGB pré-multiplicado com alpha por pixel
    // ==============================================================================
    public static class LayeredWindow
    {
        [StructLayout(LayoutKind.Sequential)] struct PT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] struct SZ { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        struct BMIH
        {
            public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
        }

        [DllImport("user32.dll", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref PT pptDst, ref SZ psize, IntPtr hdcSrc, ref PT pptSrc, int crKey, ref BLEND pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BMIH pbmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")] static extern void CopyMemory(IntPtr dest, IntPtr src, UIntPtr count);

        public static void Present(IntPtr hwnd, Bitmap bmp, int x, int y, byte alpha)
        {
            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr mem = CreateCompatibleDC(screen);
            IntPtr hbmp = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                BMIH h = new BMIH();
                h.biSize = (uint)Marshal.SizeOf(typeof(BMIH));
                h.biWidth = bmp.Width; h.biHeight = -bmp.Height; h.biPlanes = 1; h.biBitCount = 32;
                IntPtr bits;
                hbmp = CreateDIBSection(screen, ref h, 0, out bits, IntPtr.Zero, 0);
                if (hbmp == IntPtr.Zero) return;
                BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try { CopyMemory(bits, bd.Scan0, (UIntPtr)(uint)(bd.Stride * bmp.Height)); }
                finally { bmp.UnlockBits(bd); }
                old = SelectObject(mem, hbmp);

                PT dst = new PT { x = x, y = y };
                PT src = new PT { x = 0, y = 0 };
                SZ sz = new SZ { cx = bmp.Width, cy = bmp.Height };
                BLEND bf = new BLEND { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
                UpdateLayeredWindow(hwnd, screen, ref dst, ref sz, mem, ref src, 0, ref bf, 2);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screen);
                if (old != IntPtr.Zero) SelectObject(mem, old);
                if (hbmp != IntPtr.Zero) DeleteObject(hbmp);
                DeleteDC(mem);
            }
        }
    }

    // ==============================================================================
    // FLYOUT "TRÁFEGO DE REDE POR APP" (janela em camadas, animada, sem roubar o foco)
    // ==============================================================================
    public class FlyoutForm : Form
    {
        private readonly NetMonitorApp app;
        private Bitmap bmp;
        private FlyoutHit hit = new FlyoutHit();
        private int hoverRow = -1;
        private float dpi = 96f;
        private int margin = 18;
        private AppSettings s = new AppSettings();
        private bool dark = true;
        private List<ProcessTrafficItem> items = new List<ProcessTrafficItem>();
        private double up, dn;
        private Point basePos = new Point(0, 0);
        private byte alpha = 0;
        private int offsetY = 0;
        private Timer anim;
        private DateTime animStart;
        private bool animShow;

        public FlyoutForm(NetMonitorApp app)
        {
            this.app = app;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            anim = new Timer { Interval = 15 };
            anim.Tick += AnimTick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style = unchecked((int)0x80000000);
                cp.ExStyle |= 0x00080000 | 0x00000080 | 0x08000000 | 0x00000008; // LAYERED | TOOLWINDOW | NOACTIVATE | TOPMOST
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        // retângulo do cartão (sem a margem da sombra), em coordenadas de tela
        public Rectangle CardBounds
        {
            get { return new Rectangle(basePos.X + hit.Body.X, basePos.Y + hit.Body.Y, hit.Body.Width, hit.Body.Height); }
        }

        public void SetData(AppSettings settings, bool isDark, List<ProcessTrafficItem> list, double upBits, double dnBits, float dpiValue)
        {
            s = settings; dark = isDark; items = list ?? new List<ProcessTrafficItem>(); up = upBits; dn = dnBits; dpi = dpiValue;
            margin = (int)Math.Round(18 * dpi / 96f);
            Redraw();
        }

        private void Redraw()
        {
            IntPtr h = this.Handle;
            Bitmap nb = FlyoutRenderer.Render(s, dark, items, up, dn, dpi, hoverRow, margin, out hit);
            Bitmap old = bmp;
            bmp = nb;
            if (old != null) old.Dispose();
            Present();
        }

        private void Present()
        {
            if (bmp == null || !this.IsHandleCreated) return;
            LayeredWindow.Present(this.Handle, bmp, basePos.X, basePos.Y + offsetY, alpha);
        }

        // Centraliza sobre o widget, acima dele (ou abaixo se não couber)
        public void PlaceAbove(Rectangle widget, Rectangle work)
        {
            if (bmp == null) return;
            int cardW = hit.Body.Width, cardH = hit.Body.Height;
            int gap = (int)Math.Round(8 * dpi / 96f);
            int cx = widget.Left + widget.Width / 2 - cardW / 2;
            cx = Math.Max(work.Left + 6, Math.Min(work.Right - cardW - 6, cx));
            int cy = widget.Top - cardH - gap;
            if (cy < work.Top + 6) cy = widget.Bottom + gap;
            basePos = new Point(cx - margin, cy - margin);
            Present();
        }

        public bool IsHiding { get { return anim.Enabled && !animShow; } }

        public void ShowAnimated()
        {
            animShow = true; animStart = DateTime.UtcNow; alpha = 0; offsetY = (int)(8 * dpi / 96f);
            if (!this.Visible) { this.Show(); }
            SetWindowPos(this.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
            Present();
            anim.Start();
        }

        public void HideAnimated()
        {
            animShow = false; animStart = DateTime.UtcNow;
            anim.Start();
        }

        private void AnimTick(object sender, EventArgs e)
        {
            double ms = (DateTime.UtcNow - animStart).TotalMilliseconds;
            double dur = animShow ? 280 : 160;
            double t = Math.Min(1, ms / dur);
            double ease = 1 - Math.Pow(1 - t, 4);                 // ~ cubic-bezier(0,0,0,1)
            double v = animShow ? ease : 1 - ease;
            alpha = (byte)(255 * v);
            offsetY = (int)((8 * dpi / 96f) * (1 - v));
            Present();
            if (t >= 1)
            {
                anim.Stop();
                if (!animShow) { this.Hide(); hoverRow = -1; }
            }
        }

        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = -1;
            for (int i = 0; i < hit.Rows.Count; i++) if (hit.Rows[i].Contains(e.Location)) { h = i; break; }
            bool hand = hit.Separated.Contains(e.Location) || hit.Combined.Contains(e.Location) || hit.SettingsLink.Contains(e.Location);
            this.Cursor = hand ? Cursors.Hand : Cursors.Default;
            if (h != hoverRow) { hoverRow = h; Redraw(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverRow != -1) { hoverRow = -1; Redraw(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (hit.Separated.Contains(e.Location)) app.SetFlyoutMode("separated");
            else if (hit.Combined.Contains(e.Location)) app.SetFlyoutMode("combined");
            else if (hit.SettingsLink.Contains(e.Location)) app.OpenSettingsDialog("geral");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (anim != null) anim.Dispose(); if (bmp != null) bmp.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // ==============================================================================
    // MEMÓRIA: devolve o working set ao Windows (classe separada para não carregar o WPF no início)
    // ==============================================================================
    public static class Mem
    {
        [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        public static void Trim()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }
        }
    }

    // ==============================================================================
    // MENU DE CONTEXTO: mesma paleta Ace Obsidian e ícones da marca usados na janela e no flyout
    // ==============================================================================
    public class Windows11MenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly bool isDark;
        private readonly Tokens t;

        public Windows11MenuRenderer(bool isDark) : base(new Windows11ColorTable(isDark))
        {
            this.isDark = isDark;
            this.t = Pal.For(isDark);
            this.RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(WidgetRenderer.Hex(t.FlyoutBg, 255)))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // a borda e os cantos arredondados vêm do DWM (Windows 11)
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Rectangle r = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
            Color hover = isDark ? Color.FromArgb(22, 255, 255, 255) : Color.FromArgb(14, 0, 0, 0);
            using (SolidBrush b = new SolidBrush(hover))
            using (GraphicsPath path = CreateRoundedRectanglePath(r, 4))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(b, path);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? WidgetRenderer.Hex(t.TextPri, 255) : WidgetRenderer.Hex(t.TextSec, 255);
            e.TextFormat |= TextFormatFlags.VerticalCenter;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // marca de seleção = ícone "acao-confirmar" da marca, sem caixa
            Rectangle r = e.ImageRectangle;
            float size = Math.Min(r.Width, r.Height) + 2;
            float x = r.Left + (r.Width - size) / 2f, y = r.Top + (r.Height - size) / 2f;
            SvgPath.DrawIcon(e.Graphics, "acao-confirmar", x, y, size, WidgetRenderer.Hex(isDark ? "#60A5FA" : "#0066FF", 255));
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = WidgetRenderer.Hex(t.TextSec, 255);
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int midY = e.Item.Height / 2;
            using (Pen p = new Pen(isDark ? Color.FromArgb(26, 255, 255, 255) : Color.FromArgb(22, 0, 0, 0)))
                e.Graphics.DrawLine(p, 10, midY, e.Item.Width - 10, midY);
        }

        public static GraphicsPath CreateRoundedRectanglePath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Cantos arredondados e borda sutil do Windows 11 no menu e nos submenus
        public static void StyleDropDown(ToolStripDropDown dd, bool dark)
        {
            try
            {
                int round = 2;
                NetMonitorApp.DwmSetWindowAttribute(dd.Handle, 33, ref round, 4);
                Color bc = dark ? Color.FromArgb(46, 52, 66) : Color.FromArgb(214, 222, 236);
                int cref = bc.R | (bc.G << 8) | (bc.B << 16);
                NetMonitorApp.DwmSetWindowAttribute(dd.Handle, 34, ref cref, 4);
            }
            catch { }
        }
    }

    public class Windows11ColorTable : ProfessionalColorTable
    {
        private readonly Color bg, sel;

        public Windows11ColorTable(bool isDark)
        {
            this.UseSystemColors = false;
            bg = WidgetRenderer.Hex(Pal.For(isDark).FlyoutBg, 255);
            sel = isDark ? Color.FromArgb(40, 46, 58) : Color.FromArgb(236, 240, 247);
        }

        public override Color ToolStripDropDownBackground { get { return bg; } }
        public override Color MenuBorder { get { return bg; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return sel; } }
        public override Color MenuItemSelectedGradientBegin { get { return sel; } }
        public override Color MenuItemSelectedGradientEnd { get { return sel; } }
        public override Color MenuItemPressedGradientBegin { get { return sel; } }
        public override Color MenuItemPressedGradientEnd { get { return sel; } }
        public override Color ImageMarginGradientBegin { get { return bg; } }
        public override Color ImageMarginGradientMiddle { get { return bg; } }
        public override Color ImageMarginGradientEnd { get { return bg; } }
        public override Color CheckBackground { get { return Color.Transparent; } }
        public override Color CheckSelectedBackground { get { return Color.Transparent; } }
        public override Color CheckPressedBackground { get { return Color.Transparent; } }
        public override Color SeparatorDark { get { return bg; } }
        public override Color SeparatorLight { get { return bg; } }
    }

}
