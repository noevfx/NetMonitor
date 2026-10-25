# Compila apenas o NetMonitor.exe de desenvolvimento em bin\dev (não mexe em release\ nem na versão instalada).
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
$src = Join-Path $root "src"
$out = Join-Path $root "bin\dev"
New-Item -ItemType Directory -Force $out | Out-Null
Get-Process NetMonitor -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq "$out\NetMonitor.exe" } | Stop-Process -Force
Start-Sleep -Milliseconds 400
$res = @("icone_128.png","icone_256.png","pix_qrcode.png") | ForEach-Object { "/res:$src\res\$_,$_" }
& $csc /nologo /target:winexe /optimize+ /platform:anycpu /win32icon:"$src\app.ico" /lib:$wpf `
  /r:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll,System.Xaml.dll `
  $res /out:"$out\NetMonitor.exe" "$src\NetMonitor.cs" "$src\Settings.cs" "$src\Version.g.cs" "$src\Data.cs" "$src\WidgetRenderer.cs" "$src\FlyoutRenderer.cs" "$src\Icons.cs" "$src\WpfKit.cs" "$src\SettingsWindow.cs" "$src\SettingsProcess.cs" "$src\Ipc.cs"
if ($LASTEXITCODE -ne 0) { throw "csc falhou ($LASTEXITCODE)" }
Write-Host "OK -> $out\NetMonitor.exe"
