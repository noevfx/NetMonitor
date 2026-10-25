# ==============================================================================
# NetMonitor - Script de Instalação Oficial no Windows 11
# ==============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path $PSScriptRoot -Parent
$binExe = Join-Path $projectRoot "bin\NetMonitor.exe"
$uninstallerBin = Join-Path $projectRoot "bin\uninstall.exe"

if (-not (Test-Path $binExe) -or -not (Test-Path $uninstallerBin)) {
    Write-Host "Compilando binários mais recentes..." -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot "build.ps1")
}

$installDir = Join-Path $env:LOCALAPPDATA "Programs\NetMonitor"
$targetExe = Join-Path $installDir "NetMonitor.exe"
$targetUninstall = Join-Path $installDir "uninstall.exe"

Write-Host "Fechando instâncias ativas do NetMonitor..." -ForegroundColor Yellow
Get-Process NetMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

Write-Host "Copiando arquivos para $installDir..." -ForegroundColor Cyan
Copy-Item -Path $binExe -Destination $targetExe -Force
Copy-Item -Path $uninstallerBin -Destination $targetUninstall -Force

# Interface de configurações é nativa (WPF), embutida no NetMonitor.exe: não há mais settings.html.
Remove-Item -Path (Join-Path $installDir "settings.html") -Force -ErrorAction SilentlyContinue

# 1. Criar Atalho no Menu Iniciar
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDir "NetMonitor.lnk"
$wsh = New-Object -ComObject WScript.Shell
$sc = $wsh.CreateShortcut($shortcutPath)
$sc.TargetPath = $targetExe
$sc.WorkingDirectory = $installDir
$sc.Description = "NetMonitor - Monitor de Rede na Barra de Tarefas"
$sc.Save()
Write-Host "Atalho criado no Menu Iniciar." -ForegroundColor Green

# 2. Registrar no Adicionar/Remover Programas (Configurações do Windows 11)
$uninstallReg = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NetMonitor"
if (-not (Test-Path $uninstallReg)) {
    New-Item -Path $uninstallReg -Force | Out-Null
}
Set-ItemProperty -Path $uninstallReg -Name "DisplayName" -Value "NetMonitor (Monitor de Rede)"
Set-ItemProperty -Path $uninstallReg -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $uninstallReg -Name "Publisher" -Value "Eduardo Noé"
Set-ItemProperty -Path $uninstallReg -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $uninstallReg -Name "DisplayIcon" -Value "$targetExe,0"
Set-ItemProperty -Path $uninstallReg -Name "UninstallString" -Value "`"$targetUninstall`""
Set-ItemProperty -Path $uninstallReg -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallReg -Name "NoRepair" -Value 1 -Type DWord
Write-Host "Registrado no Adicionar/Remover Programas do Windows." -ForegroundColor Green

# 3. Registrar Inicialização no Boot
$runReg = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
Set-ItemProperty -Path $runReg -Name "NetMonitor" -Value "`"$targetExe`""
Write-Host "Configurado para iniciar com o Windows (Boot)." -ForegroundColor Green

# 3.5 Definir Padrões de Fábrica no Registro (Posição travada, ancorada, Ace Obsidian, setas à esquerda, fundo transparente)

$appReg = "HKCU:\Software\NetMonitor"

if (-not (Test-Path $appReg)) {

    New-Item -Path $appReg -Force | Out-Null

}

Set-ItemProperty -Path $appReg -Name "IsLocked" -Value 1 -Type DWord

Set-ItemProperty -Path $appReg -Name "ArrowOnRight" -Value 0 -Type DWord

Set-ItemProperty -Path $appReg -Name "SpeedUnitFormat" -Value "Bits"

Set-ItemProperty -Path $appReg -Name "UnitMode" -Value "Auto"

Set-ItemProperty -Path $appReg -Name "TextColorMode" -Value "Individual"

Set-ItemProperty -Path $appReg -Name "TextColorUp" -Value "#0066FF"

Set-ItemProperty -Path $appReg -Name "TextColorDown" -Value "#00E5FF"

Set-ItemProperty -Path $appReg -Name "ArrowColorMode" -Value "Individual"

Set-ItemProperty -Path $appReg -Name "ArrowColorUp" -Value "#0066FF"

Set-ItemProperty -Path $appReg -Name "ArrowColorDown" -Value "#00E5FF"

Set-ItemProperty -Path $appReg -Name "UnitColorMode" -Value "Inherit"

Set-ItemProperty -Path $appReg -Name "WidgetBgMode" -Value "transparent"

Set-ItemProperty -Path $appReg -Name "WidgetBorderEnabled" -Value 0 -Type DWord

Remove-ItemProperty -Path $appReg -Name "CustomX" -ErrorAction SilentlyContinue

Remove-ItemProperty -Path $appReg -Name "CustomY" -ErrorAction SilentlyContinue

Write-Host "Padrões de fábrica calibrados (Ace Obsidian, 3 colunas, transparente)." -ForegroundColor Green

# 4. Iniciar o programa
try {
    Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $targetExe } | Out-Null
} catch {
    Start-Process -FilePath $targetExe
}
Write-Host "`nNetMonitor v1.0.0 instalado e iniciado com sucesso!" -ForegroundColor Green
