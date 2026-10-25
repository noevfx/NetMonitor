// ==============================================================================
// NetMonitor - Dados compartilhados: item de processo e formatação de velocidade
// ==============================================================================

using System;

namespace NetMonitor
{
    public class ProcessTrafficItem
    {
        public int Pid;
        public string Name;       // sem ".exe"
        public string Sub;        // categoria curta (Navegador, Música...)
        public ulong TxBytes;     // bytes/s enviados
        public ulong RxBytes;     // bytes/s recebidos
        public ulong Total { get { return TxBytes + RxBytes; } }
    }

    public static class SpeedFmt
    {
        // Converte bits/s em número + unidade conforme base (bits/bytes) e escala (auto/fixa/individual).
        public static void Format(AppSettings s, double bitsPerSec, bool tx, out string num, out string unit)
        {
            bool bits = s.UnitBase != "bytes";
            double v = bits ? bitsPerSec : bitsPerSec / 8.0;
            double k = bits ? 1000.0 : 1024.0;
            double m = k * k, g = m * k;
            string uk = bits ? "Kbps" : "KB/s", um = bits ? "Mbps" : "MB/s", ug = bits ? "Gbps" : "GB/s", ub = bits ? "bps" : "B/s";

            string scale = s.ScaleMode;
            if (scale == "individual") scale = tx ? s.ScaleTx : s.ScaleRx;

            if (scale == "kb") { num = N(v / k, 100, 1); unit = uk; }
            else if (scale == "mb") { num = N(v / m, 100, 1); unit = um; }
            else if (scale == "gb") { num = N(v / g, 10, 2); unit = ug; }
            else if (v >= g) { num = N(v / g, 10, 2); unit = ug; }
            else if (v >= m) { num = N(v / m, 100, 1); unit = um; }
            else if (v >= k) { num = N(v / k, 100, 1); unit = uk; }
            else { num = v.ToString("F0"); unit = ub; }
        }

        // >= limite: sem casa decimal (mantém a largura do widget estável)
        static string N(double v, double limit, int dec)
        {
            if (v >= limit) return v.ToString("F0");
            return v.ToString("F" + dec);
        }

        public static string Compact(AppSettings s, double bitsPerSec, bool tx)
        {
            string n, u;
            Format(s, bitsPerSec, tx, out n, out u);
            return n + " " + u;
        }

        public static string ForBytes(AppSettings s, ulong bytesPerSec, bool tx)
        {
            return Compact(s, bytesPerSec * 8.0, tx);
        }
    }
}
