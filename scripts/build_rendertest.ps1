# Compila e executa o harness de validação visual (gera PNGs em bin\shots).
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
$src = Join-Path $root "src"
$out = Join-Path $root "bin\rendertest"
New-Item -ItemType Directory -Force $out | Out-Null
$res = @("icone_128.png","icone_256.png","pix_qrcode.png","wordmark_branca.png") | ForEach-Object { "/res:$src\res\$_,$_" }
& $csc /nologo /target:exe /optimize+ /lib:$wpf `
  /main:P /r:Microsoft.CSharp.dll,System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll,System.Xaml.dll `
  $res /out:"$out\RenderTest.exe" "$PSScriptRoot\rendertest\RenderTest.cs" "$src\Settings.cs" "$src\Version.g.cs" "$src\Data.cs" "$src\WidgetRenderer.cs" "$src\FlyoutRenderer.cs" "$src\Icons.cs" "$src\WpfKit.cs" "$src\SettingsWindow.cs" "$src\InstallerApp.cs"
if ($LASTEXITCODE -ne 0) { throw "csc falhou" }
& "$out\RenderTest.exe" (Join-Path $root "bin\shots")
