# ==============================================================================
# NetMonitor - Script de Build Automatizado
# Gera os pacotes organizados na pasta release\vX.Y.Z\:
#   1. NetMonitorSetup.exe (Instalador nativo WPF)
#   2. NetMonitor Portable\ (Pasta da versão portátil com README)
#   3. NetMonitor-Portable-vX.Y.Z.zip (Arquivo portátil avulso com README)
#   4. NetMonitor-vX.Y.Z.zip (Pacote completo com instalador + portátil + README)
# ==============================================================================

# -PackageOnly: só refaz README/zips com os .exe já compilados em bin\ (mantém os hashes já enviados ao VirusTotal)
param([switch]$PackageOnly)
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path $PSScriptRoot -Parent
$versionFile = Join-Path $projectRoot "version.txt"
$version = if (Test-Path $versionFile) { (Get-Content $versionFile -Raw).Trim() } else { "1.0.0" }

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
$srcDir = Join-Path $projectRoot "src"
$resDir = Join-Path $srcDir "res"
$binDir = Join-Path $projectRoot "bin"
$releaseDir = Join-Path $projectRoot "release\v$version"   # uma pasta por versão, em ordem crescente
$readmeFile = Join-Path $projectRoot "README.md"
$iconPath = Join-Path $srcDir "app.ico"
$wpfRefs = "System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll,System.Xaml.dll"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "Iniciando Build do NetMonitor v$version..." -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# 1. Fechar processos em execução
Get-Process NetMonitor, NetMonitorSetup -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

# 2. Criar diretórios necessários
foreach ($dir in @($binDir, $releaseDir)) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

# 3. Compilar NetMonitor.exe (widget + flyout + janela de configurações WPF)
$netMonitorExe = Join-Path $binDir "NetMonitor.exe"
$uninstallerExe = Join-Path $binDir "uninstall.exe"
$installerExe = Join-Path $binDir "NetMonitorSetup.exe"
$releaseSetup = Join-Path $releaseDir "NetMonitorSetup.exe"
if (-not $PackageOnly) {
Write-Host "1. Compilando NetMonitor.exe..." -ForegroundColor Cyan
& $csc /nologo /target:winexe /optimize+ /platform:anycpu /win32icon:$iconPath /lib:$wpf `
    /r:$wpfRefs `
    "/res:$resDir\icone_128.png,icone_128.png" "/res:$resDir\icone_256.png,icone_256.png" "/res:$resDir\pix_qrcode.png,pix_qrcode.png" `
    /out:$netMonitorExe `
    "$srcDir\NetMonitor.cs" "$srcDir\Settings.cs" "$srcDir\Version.g.cs" "$srcDir\Data.cs" "$srcDir\WidgetRenderer.cs" "$srcDir\FlyoutRenderer.cs" `
    "$srcDir\Icons.cs" "$srcDir\WpfKit.cs" "$srcDir\SettingsWindow.cs" "$srcDir\SettingsProcess.cs" "$srcDir\Ipc.cs"
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar NetMonitor.exe" }

# 4. Compilar uninstall.exe
$uninstallerExe = Join-Path $binDir "uninstall.exe"
Write-Host "2. Compilando uninstall.exe..." -ForegroundColor Cyan
& $csc /nologo /target:winexe /optimize+ /platform:anycpu /win32icon:$iconPath `
    /r:System.dll,System.Windows.Forms.dll,System.Drawing.dll `
    /out:$uninstallerExe "$srcDir\Uninstaller.cs"
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar uninstall.exe" }

# 5. Compilar o instalador nativo (NetMonitorSetup.exe)
$installerExe = Join-Path $binDir "NetMonitorSetup.exe"
$releaseSetup = Join-Path $releaseDir "NetMonitorSetup.exe"
Write-Host "3. Compilando NetMonitorSetup.exe..." -ForegroundColor Cyan
& $csc /nologo /target:winexe /optimize+ /platform:anycpu /win32icon:$iconPath /lib:$wpf `
    /r:$wpfRefs,Microsoft.CSharp.dll `
    "/res:$netMonitorExe,NetMonitor.exe" "/res:$uninstallerExe,uninstall.exe" `
    "/res:$resDir\icone_128.png,icone_128.png" "/res:$resDir\wordmark_branca.png,wordmark_branca.png" `
    /out:$installerExe `
    "$srcDir\InstallerApp.cs" "$srcDir\Settings.cs" "$srcDir\Version.g.cs" "$srcDir\Icons.cs" "$srcDir\WpfKit.cs"
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar NetMonitorSetup.exe" }
}
Copy-Item -Path $installerExe -Destination $releaseSetup -Force

# 5b. Hashes SHA-256 dos executáveis no README (entre os marcadores hashes:start/end).
# Só os .exe: o hash de um zip não pode ir no README que está dentro dele.
if (Test-Path $readmeFile) {
    $rows = foreach ($exe in @($installerExe, $netMonitorExe)) {
        $h = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLower()
        "| ``$(Split-Path $exe -Leaf)`` | ``$h`` | [relatório](https://www.virustotal.com/gui/file/$h) |"
    }
    $block = "<!-- hashes:start -->`n| Arquivo (v$version) | SHA-256 | VirusTotal |`n| :--- | :--- | :--- |`n" + ($rows -join "`n") + "`n<!-- hashes:end -->"
    $txt = [System.IO.File]::ReadAllText($readmeFile)
    $txt = [regex]::Replace($txt, '(?s)<!-- hashes:start -->.*?<!-- hashes:end -->', $block.Replace('$', '$$'))
    [System.IO.File]::WriteAllText($readmeFile, $txt, (New-Object System.Text.UTF8Encoding($false)))
}

# 6. Versão portátil com README
Write-Host "4. Empacotando versão portátil em release\..." -ForegroundColor Cyan
$portableDir = Join-Path $releaseDir "NetMonitor Portable"
if (Test-Path $portableDir) { Remove-Item $portableDir -Recurse -Force }
New-Item -ItemType Directory -Path $portableDir -Force | Out-Null
Copy-Item -Path $netMonitorExe -Destination (Join-Path $portableDir "NetMonitor.exe") -Force

if (Test-Path $readmeFile) {
    Copy-Item -Path $readmeFile -Destination (Join-Path $portableDir "README.md") -Force
}
# As telas (docs\screenshots) são só para a página do GitHub e o Drive; nunca entram nos pacotes.

$readmeContent = @"
==============================================================================
NetMonitor v$version - Versão Portátil
Autor: Eduardo Noé (Noé VFX) - https://noevfx.com
==============================================================================

[PT-BR]
Esta é a versão portátil oficial do NetMonitor.
- Não requer instalação nem privilégios de administrador.
- Pode ser executado diretamente de qualquer pasta ou pendrive.
- Para usar, basta dar um duplo clique em NetMonitor.exe.
- Para abrir as Configurações, clique com o botão direito no widget e escolha 'Configurações...'.
- No primeiro uso, o Windows pode exibir o aviso do SmartScreen por ser um app independente novo,
  sem assinatura digital. Clique em 'Mais informações' e depois em 'Executar assim mesmo'.
  Os hashes SHA-256 e os relatórios do VirusTotal estão no README.md.

[EN]
This is the official portable version of NetMonitor.
- No installation or administrator privileges required.
- Can be run directly from any folder or USB drive.
- To use, simply double-click NetMonitor.exe.
- To open Settings, right-click the widget and choose 'Configurações... / Settings...'.
- On first run, Windows SmartScreen may warn about a new, unsigned independent app.
  Click 'More info' and then 'Run anyway'. SHA-256 hashes and VirusTotal reports are in README.md.
==============================================================================
"@
[System.IO.File]::WriteAllText((Join-Path $portableDir "LEIA-ME.txt"), $readmeContent, (New-Object System.Text.UTF8Encoding($true)))

$portableZip = Join-Path $releaseDir "NetMonitor-Portable-v$version.zip"
if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
# ZipFile em vez de Compress-Archive: o do PowerShell 5.1 grava caminhos com "\" (pastas quebram fora do Windows)
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$pz = [System.IO.Compression.ZipFile]::Open($portableZip, [System.IO.Compression.ZipArchiveMode]::Create)
Get-ChildItem -Path $portableDir -Recurse -File | ForEach-Object {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($pz, $_.FullName, $_.FullName.Substring($portableDir.Length).TrimStart('\').Replace('\', '/')) | Out-Null
}
$pz.Dispose()

# 7. Pacote completo (instalador + portátil + README)
Write-Host "5. Criando pacote completo (NetMonitor-v$version.zip)..." -ForegroundColor Cyan
$combinedZip = Join-Path $releaseDir "NetMonitor-v$version.zip"
if (Test-Path $combinedZip) { Remove-Item $combinedZip -Force }
[System.Reflection.Assembly]::LoadWithPartialName("System.IO.Compression.FileSystem") | Out-Null
$zip = [System.IO.Compression.ZipFile]::Open($combinedZip, [System.IO.Compression.ZipArchiveMode]::Create)
[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $releaseSetup, "NetMonitorSetup.exe") | Out-Null
if (Test-Path $readmeFile) {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $readmeFile, "README.md") | Out-Null
}
Get-ChildItem -Path $portableDir -Recurse -File | ForEach-Object {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, "NetMonitor Portable/" + $_.Name) | Out-Null
}
$zip.Dispose()

Write-Host "`n========================================================" -ForegroundColor Green
Write-Host "Build v$version concluído com sucesso em release\v$version\!" -ForegroundColor Green
Write-Host "  * Pacote completo: $combinedZip" -ForegroundColor Yellow
Write-Host "  * Instalador:      $releaseSetup" -ForegroundColor Yellow
Write-Host "  * Portátil:        $portableZip" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Green
