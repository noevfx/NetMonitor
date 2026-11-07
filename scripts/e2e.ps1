# Teste de ponta a ponta do NetMonitor de desenvolvimento (sem captura de tela):
#  - confere o hit-test do widget (WindowFromPoint em área "vazia" deve devolver o widget)
#  - abre as configurações por duplo clique e captura a janela com PrintWindow
param([string]$Exe = "D:\Noe VFX\.Labs\Windows\NetMonitor\bin\dev\NetMonitor.exe", [string]$Out = "D:\Noe VFX\.Labs\Windows\NetMonitor\bin\shots", [string]$Clicks = "", [string]$Shot2 = "real_settings_after.png")
Add-Type @"
using System; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Drawing; using System.Drawing.Imaging;
public static class W {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr c);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string t);
  [DllImport("user32.dll")] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string t);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  public static List<IntPtr> ForPid(uint pid) {
    List<IntPtr> l = new List<IntPtr>();
    EnumWindows((h, p) => { uint x; GetWindowThreadProcessId(h, out x); if (x == pid) l.Add(h); return true; }, IntPtr.Zero);
    return l;
  }
  public static string Info(IntPtr h) {
    StringBuilder c = new StringBuilder(256), t = new StringBuilder(256); GetClassName(h, c, 256); GetWindowText(h, t, 256);
    RECT r; GetWindowRect(h, out r);
    return h + " | " + c + " | '" + t + "' | vis=" + IsWindowVisible(h) + " | " + r.L + "," + r.T + " " + (r.R-r.L) + "x" + (r.B-r.T);
  }
  public static void Shot(IntPtr h, string path) {
    RECT r; GetWindowRect(h, out r); int w = r.R - r.L, hh = r.B - r.T;
    using (Bitmap b = new Bitmap(w, hh)) { using (Graphics g = Graphics.FromImage(b)) { IntPtr dc = g.GetHdc(); PrintWindow(h, dc, 2); g.ReleaseHdc(dc); } b.Save(path, ImageFormat.Png); }
  }
}
"@ -ReferencedAssemblies System.Drawing
[W]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
Get-Process NetMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item "$env:LOCALAPPDATA\NetMonitor\error.log" -ErrorAction SilentlyContinue
$p = Start-Process $Exe -PassThru
Start-Sleep -Seconds 3
"Processo: $($p.Id)  RAM(MB)=" + [math]::Round((Get-Process -Id $p.Id).WorkingSet64/1MB,1)
$wins = [W]::ForPid([uint32]$p.Id)
"--- janelas"; $wins | ForEach-Object { [W]::Info($_) }
$widget = $wins | Where-Object { $i = [W]::Info($_); $i -match 'vis=True' -and $i -match 'NetMonitor' -and $i -notmatch 'NotifyIcon' } | Select-Object -First 1
if (-not $widget) { "widget nao encontrado"; return }
$r = New-Object W+RECT; [W]::GetWindowRect($widget, [ref]$r) | Out-Null
"widget: $($r.L),$($r.T) $($r.R-$r.L)x$($r.B-$r.T)"
$trayW = [W]::FindWindow("Shell_TrayWnd", $null); $notW = [W]::FindWindowEx($trayW, [IntPtr]::Zero, "TrayNotifyWnd", $null)
$tr = New-Object W+RECT; $nr = New-Object W+RECT; [W]::GetWindowRect($trayW, [ref]$tr) | Out-Null; [W]::GetWindowRect($notW, [ref]$nr) | Out-Null
"ancoragem: folga ate a bandeja = $($nr.L - $r.R)px ; centro vertical widget=$(($r.T+$r.B)/2) barra=$(($tr.T+$tr.B)/2)"
foreach ($pt in @(@(2,2), @(([int](($r.R-$r.L)/2)), 2), @(($r.R-$r.L-3), ($r.B-$r.T-3)))) {
  $q = New-Object W+POINT; $q.X = $r.L + $pt[0]; $q.Y = $r.T + $pt[1]
  $h = [W]::WindowFromPoint($q)
  "hit-test em +$($pt[0]),+$($pt[1]) -> $h  (widget=$widget)  " + $(if ($h -eq $widget) {"OK"} else {"FALHA"})
}
# clique direito em QUALQUER ponto do widget (canto) -> o menu de contexto deve abrir
$before = ([W]::ForPid([uint32]$p.Id) | Where-Object { [W]::Info($_) -match 'vis=True' }).Count
$lpc = [IntPtr](3 + (3 -shl 16))
[W]::PostMessage($widget, 0x0204, [IntPtr]2, $lpc) | Out-Null
[W]::PostMessage($widget, 0x0205, [IntPtr]0, $lpc) | Out-Null
Start-Sleep -Seconds 2
$menuWin = [W]::ForPid([uint32]$p.Id) | Where-Object { $i = [W]::Info($_); $i -match 'vis=True' -and $i -notmatch "'NetMonitor'" -and $i -match 'WindowsForms10' -and $i -match '\d{3}x\d{3}' } | Select-Object -First 1
"menu de contexto (clique no canto do widget): " + $(if ($menuWin) { "ABRIU -> " + [W]::Info($menuWin) } else { "NAO ABRIU" })
if ($menuWin) { [W]::Shot($menuWin, "$Out
eal_menu.png") }
[W]::PostMessage($widget, 0x0100, [IntPtr]0x1B, [IntPtr]0) | Out-Null

# segunda execução do .exe -> a instância existente abre as configurações (evento nomeado)
Start-Process $Exe | Out-Null
Start-Sleep -Seconds 8
$wins = @(); Get-Process NetMonitor | ForEach-Object { $wins += [W]::ForPid([uint32]$_.Id) }
"processos NetMonitor: " + (Get-Process NetMonitor | ForEach-Object { "$($_.Id)=" + [math]::Round($_.WorkingSet64/1MB,1) + "MB" })
"--- janelas apos abrir configuracoes"; $wins | ForEach-Object { [W]::Info($_) }
$cfg = $wins | Where-Object { $i = [W]::Info($_); $i -match 'vis=True' -and $i -match "'NetMonitor'" -and $i -match 'HwndWrapper' } | Select-Object -First 1
if ($cfg) { [W]::Shot($cfg, "$Out\real_settings.png"); "captura: $Out\real_settings.png"; "RAM widget(MB) com janela aberta=" + [math]::Round((Get-Process -Id $p.Id).WorkingSet64/1MB,1) } else { "janela WPF nao encontrada" }

# cliques na janela WPF (coordenadas da captura real_settings.png), depois nova captura
if ($cfg -and $Clicks) {
  $wr = New-Object W+RECT; [W]::GetWindowRect($cfg, [ref]$wr) | Out-Null
  $o = New-Object W+POINT; $o.X = 0; $o.Y = 0; [W]::ClientToScreen($cfg, [ref]$o) | Out-Null
  $dx = $o.X - $wr.L; $dy = $o.Y - $wr.T
  "origem do cliente na captura: +$dx,+$dy"
  foreach ($c in $Clicks.Split(';')) {
    $xy = $c.Split(','); $cx = [int]$xy[0] - $dx; $cy = [int]$xy[1] - $dy
    $l = [IntPtr]($cx + ($cy -shl 16))
    [W]::PostMessage($cfg, 0x0200, [IntPtr]0, $l) | Out-Null   # mouse move
    Start-Sleep -Milliseconds 150
    [W]::PostMessage($cfg, 0x0201, [IntPtr]1, $l) | Out-Null   # down
    [W]::PostMessage($cfg, 0x0202, [IntPtr]0, $l) | Out-Null   # up
    Start-Sleep -Milliseconds 900
  }
  [W]::Shot($cfg, "$Out\$Shot2"); "captura 2: $Out\$Shot2"
}
Start-Sleep -Seconds 1
"RAM final(MB)=" + [math]::Round((Get-Process -Id $p.Id).WorkingSet64/1MB,1)
$reg = Get-ItemProperty 'HKCU:\Software\NetMonitor'
"registro: BgMode=$($reg.WidgetBgMode) Layout=$($reg.Layout) Unit=$($reg.UnitBase) Preset=$($reg.ColorPreset) Border=$($reg.WidgetBorderEnabled) Pinned=$($reg.FlyoutPinned)"
if (Test-Path "$env:LOCALAPPDATA\NetMonitor\error.log") { "--- error.log"; Get-Content "$env:LOCALAPPDATA\NetMonitor\error.log" }
