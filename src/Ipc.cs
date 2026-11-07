// ==============================================================================
// NetMonitor - Troca de mensagens entre o widget e a janela de Configurações
// A janela roda num processo próprio (NetMonitor.exe --settings): ao fechar, toda a memória
// do WPF volta ao Windows e o widget continua leve. A conversa é feita por WM_COPYDATA.
// ==============================================================================

using System;
using System.Runtime.InteropServices;

namespace NetMonitor
{
    public static class Ipc
    {
        public const int WM_COPYDATA = 0x004A;

        // Janela de Configurações -> widget
        public const int ApplyLive = 1;     // texto: AppSettings.ToText(), sem gravar
        public const int ApplySave = 2;     // idem, gravando no registro
        public const int Hello = 3;         // texto: HWND da janela de Configurações
        public const int Bye = 4;
        public const int Sampling = 5;      // "1" liga / "0" desliga a amostragem por processo
        public const int Refresh = 6;       // amostra os processos agora

        // Widget -> janela de Configurações
        public const int Data = 10;         // velocidades, consumo e top 5 (ver NetMonitorApp.BuildDataText)
        public const int ShowPage = 11;     // texto: chave da página

        [StructLayout(LayoutKind.Sequential)]
        struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, ref COPYDATASTRUCT lParam, int flags, int timeout, out IntPtr result);

        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int pid);

        // Buffer nativo reaproveitado (as mensagens saem sempre do mesmo processo/thread de UI).
        static readonly object sendLock = new object();
        static IntPtr sendBuf;
        static int sendCap;

        // Envia sem travar quem envia: desiste se o destino não responder em 300 ms
        public static bool Send(IntPtr to, int code, string text, IntPtr from)
        {
            if (to == IntPtr.Zero) return false;
            if (text == null) text = "";

            lock (sendLock)
            {
                int bytes = (text.Length + 1) * 2;
                if (bytes > sendCap)
                {
                    if (sendBuf != IntPtr.Zero) Marshal.FreeHGlobal(sendBuf);
                    sendBuf = Marshal.AllocHGlobal(bytes);
                    sendCap = bytes;
                }

                if (text.Length > 0) Marshal.Copy(text.ToCharArray(), 0, sendBuf, text.Length);
                Marshal.WriteInt16(sendBuf, text.Length * 2, 0);

                COPYDATASTRUCT cds = new COPYDATASTRUCT { dwData = (IntPtr)code, cbData = bytes, lpData = sendBuf };
                IntPtr res;
                return SendMessageTimeout(to, WM_COPYDATA, from, ref cds, 0x0002 /* SMTO_ABORTIFHUNG */, 300, out res) != IntPtr.Zero;
            }
        }

        public static bool Read(IntPtr lParam, out int code, out string text)
        {
            code = 0; text = "";
            try
            {
                COPYDATASTRUCT cds = (COPYDATASTRUCT)Marshal.PtrToStructure(lParam, typeof(COPYDATASTRUCT));
                code = (int)cds.dwData;
                text = cds.lpData == IntPtr.Zero ? "" : Marshal.PtrToStringUni(cds.lpData);
                return true;
            }
            catch { return false; }
        }
    }
}
