// ==============================================================================
// NetMonitor Uninstaller (Bilingual: Portuguese & English)
// Autor: Noé VFX (noevfx)
// ==============================================================================

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("NetMonitor Uninstall")]
[assembly: AssemblyDescription("Desinstalador do NetMonitor")]
[assembly: AssemblyCompany("Noé VFX")]
[assembly: AssemblyProduct("NetMonitor")]
[assembly: AssemblyCopyright("Copyright © 2026 Noé VFX")]

namespace NetMonitor
{
    public class Uninstaller
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool isQuiet = false;
            string lang = "pt";

            // Detect language from registry or OS
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\NetMonitor"))
                {
                    if (key != null)
                    {
                        object l = key.GetValue("Language");
                        if (l != null) lang = l.ToString();
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(lang))
            {
                lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase) ? "pt" : "en";
            }

            foreach (string arg in args)
            {
                if (arg.Equals("/quiet", StringComparison.OrdinalIgnoreCase) || arg.Equals("/q", StringComparison.OrdinalIgnoreCase)) isQuiet = true;
                if (arg.StartsWith("/lang:", StringComparison.OrdinalIgnoreCase)) lang = arg.Substring(6);
            }

            bool isPt = lang.Equals("pt", StringComparison.OrdinalIgnoreCase);

            string title = isPt ? "Desinstalação do NetMonitor" : "NetMonitor Uninstaller";
            string prompt = isPt
                ? "Deseja realmente remover o NetMonitor do seu computador?"
                : "Are you sure you want to completely remove NetMonitor from your computer?";
            string doneMsg = isPt
                ? "O NetMonitor foi desinstalado com sucesso do seu computador."
                : "NetMonitor has been successfully uninstalled from your computer.";

            if (!isQuiet)
            {
                DialogResult dr = MessageBox.Show(prompt, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr != DialogResult.Yes) return;
            }

            // 1. Fechar processos ativos do NetMonitor (fecha pela janela; só encerra à força se necessário)
            StopNetMonitor();

            // 2. Remover do Run (Boot)
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null) key.DeleteValue("NetMonitor", false);
                }
            }
            catch { }

            // 3. Remover do Painel de Controle / Configurações do Windows
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\NetMonitor", false);
            }
            catch { }

            // 4. Remover Configurações do App
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\NetMonitor", false);
            }
            catch { }

            // 5. Remover Atalhos
            try
            {
                string startLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "NetMonitor.lnk");
                if (File.Exists(startLnk)) File.Delete(startLnk);

                string deskLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NetMonitor.lnk");
                if (File.Exists(deskLnk)) File.Delete(deskLnk);
            }
            catch { }

            // 6. Remover a pasta de instalação. Sem cmd.exe: apaga agora o que não está em uso e
            // agenda o restante (inclusive este executável) para o próximo reinício.
            string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            bool scheduled = RemoveInstallDir(appDir);

            if (!isQuiet)
            {
                string msg = doneMsg;
                if (scheduled)
                {
                    msg += isPt
                        ? "\n\nA pasta de instalação será removida no próximo reinício."
                        : "\n\nThe installation folder will be removed on the next restart.";
                }
                MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Fecha pela janela e só mata o processo se ele não responder.
        static void StopNetMonitor()
        {
            int self = Process.GetCurrentProcess().Id;
            try
            {
                foreach (Process p in Process.GetProcessesByName("NetMonitor"))
                {
                    try
                    {
                        if (p.Id == self) continue;
                        if (p.CloseMainWindow()) p.WaitForExit(1500);
                        if (!p.HasExited) { p.Kill(); p.WaitForExit(1500); }
                    }
                    catch { }
                }
            }
            catch { }
        }

        const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool MoveFileEx(string existingFileName, string newFileName, uint flags);

        // Devolve true quando sobrou algo para o próximo reinício.
        static bool RemoveInstallDir(string dir)
        {
            try
            {
                foreach (string f in Directory.GetFiles(dir)) { try { File.Delete(f); } catch { } }
                foreach (string d in Directory.GetDirectories(dir)) { try { Directory.Delete(d, true); } catch { } }
                try { Directory.Delete(dir, false); return false; }
                catch { }
            }
            catch { }

            try
            {
                foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    MoveFileEx(f, null, MOVEFILE_DELAY_UNTIL_REBOOT);

                string[] dirs = Directory.GetDirectories(dir, "*", SearchOption.AllDirectories);
                Array.Sort(dirs, delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
                foreach (string d in dirs) MoveFileEx(d, null, MOVEFILE_DELAY_UNTIL_REBOOT);

                MoveFileEx(dir, null, MOVEFILE_DELAY_UNTIL_REBOOT);
                return true;
            }
            catch { return false; }
        }
    }
}
