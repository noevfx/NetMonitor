# ==============================================================================
# NetMonitor - Script de Backup Incremental de Versão
# Uso: .\backup_version.ps1 [-Version "1.0.0"]
# Congela a versão estável atual em _backups\ e espelha no Google Drive.
# ==============================================================================

param(
    [string]$Version
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$versionFile = Join-Path $projectRoot "version.txt"

if (-not $Version) {
    if (Test-Path $versionFile) {
        $Version = (Get-Content $versionFile -Raw).Trim()
    } else {
        $Version = "1.0.0"
    }
}

$localBackupDir = Join-Path $projectRoot "_backups\v$Version"
$driveRoot = "G:\Meu Drive\NetMonitor"
$driveBackupDir = Join-Path $driveRoot "_backups\v$Version"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "Realizando backup incremental da versao v$Version..." -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# 1. Criar pasta de backup local
if (-not (Test-Path $localBackupDir)) {
    New-Item -ItemType Directory -Path $localBackupDir -Force | Out-Null
}

# 2. Copiar código-fonte, release e docs locais
Write-Host "1. Copiando arquivos locais para $localBackupDir..." -ForegroundColor Yellow
$itemsToBackup = @("src", "release", "version.txt", "README.md", "CHANGELOG.md", "HANDOFF.md")
foreach ($item in $itemsToBackup) {
    $srcPath = Join-Path $projectRoot $item
    if (Test-Path $srcPath) {
        $destPath = Join-Path $localBackupDir $item
        if (Test-Path $destPath) { Remove-Item -Path $destPath -Recurse -Force }
        Copy-Item -Path $srcPath -Destination $destPath -Recurse -Force
    }
}

# 3. Espelhar no Google Drive se disponível
if (Test-Path $driveRoot) {
    Write-Host "2. Espelhando backup incremental no Google Drive ($driveBackupDir)..." -ForegroundColor Yellow
    if (-not (Test-Path $driveBackupDir)) {
        New-Item -ItemType Directory -Path $driveBackupDir -Force | Out-Null
    }
    foreach ($item in $itemsToBackup) {
        $srcPath = Join-Path $projectRoot $item
        if (Test-Path $srcPath) {
            $destDrivePath = Join-Path $driveBackupDir $item
            if (Test-Path $destDrivePath) { Remove-Item -Path $destDrivePath -Recurse -Force }
            Copy-Item -Path $srcPath -Destination $destDrivePath -Recurse -Force
        }
    }
    Write-Host "  * Backup no Drive concluido com sucesso!" -ForegroundColor Green
} else {
    Write-Host "  * Google Drive ($driveRoot) nao detectado no momento." -ForegroundColor DarkYellow
}

Write-Host "========================================================" -ForegroundColor Green
Write-Host "Backup da v$Version concluido com sucesso!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
