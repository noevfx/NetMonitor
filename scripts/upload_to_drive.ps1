# ==============================================================================
# NetMonitor - Copia e Sincronizacao para o Google Drive
# Move arquivos legados do Drive para _backups sem apagar nada
# e sincroniza release oficial, docs, design e handoff.
# ==============================================================================

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$target = "G:\Meu Drive\NetMonitor"

if (-not (Test-Path $target)) {
    Write-Host "Pasta do Google Drive nao encontrada: $target" -ForegroundColor Red
    exit 1
}

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "Sincronizando NetMonitor com o Google Drive..." -ForegroundColor Cyan
Write-Host "Destino: $target" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# 1. Mover arquivos soltos antigos do Drive para _backups\v1.0.0_pre-wpf
$legacyDriveBackup = Join-Path $target "_backups\v1.0.0_pre-wpf"
if (-not (Test-Path $legacyDriveBackup)) {
    New-Item -ItemType Directory -Path $legacyDriveBackup -Force | Out-Null
}

$legacyFiles = @("HANDOFF_NETMONITOR.md")
foreach ($lf in $legacyFiles) {
    $p = Join-Path $target $lf
    if (Test-Path $p) {
        Move-Item -Path $p -Destination $legacyDriveBackup -Force
    }
}

# Se os zips ou executaveis antigos do Drive forem da data de 23/10, arquivar
Get-ChildItem -Path $target -File | Where-Object { $_.LastWriteTime -lt [DateTime]"2026-10-24" } | ForEach-Object {
    Write-Host "  * Arquivando versao anterior no Drive: $($_.Name)" -ForegroundColor DarkGray
    Move-Item -Path $_.FullName -Destination $legacyDriveBackup -Force
}

# 2. Copiar releases oficiais mais recentes
Write-Host "`n1. Sincronizando pasta release\..." -ForegroundColor Yellow
$driveRelease = Join-Path $target "release"
if (-not (Test-Path $driveRelease)) { New-Item -ItemType Directory -Path $driveRelease -Force | Out-Null }
Copy-Item -Path "$projectRoot\release\*" -Destination $driveRelease -Recurse -Force

# Copiar também para a raiz do Drive para acesso rapido
$version = (Get-Content "$projectRoot\version.txt" -Raw).Trim()
Copy-Item -Path "$projectRoot\release\v$version\NetMonitorSetup.exe" -Destination $target -Force
Copy-Item -Path "$projectRoot\release\v$version\NetMonitor-v*.zip" -Destination $target -Force

# 3. Sincronizar Documentacao e Handoff
Write-Host "2. Sincronizando Documentacao e Handoff..." -ForegroundColor Yellow
Copy-Item -Path "$projectRoot\README.md" -Destination $target -Force
Copy-Item -Path "$projectRoot\CHANGELOG.md" -Destination $target -Force
Copy-Item -Path "$projectRoot\HANDOFF.md" -Destination $target -Force
Copy-Item -Path "$projectRoot\version.txt" -Destination $target -Force

# 4. Sincronizar pasta docs\ (screenshots e design: Logomarca, Carrossel)
Write-Host "3. Sincronizando pasta docs (screenshots, design, Logomarca)..." -ForegroundColor Yellow
$driveDocs = Join-Path $target "docs"
if (-not (Test-Path $driveDocs)) { New-Item -ItemType Directory -Path $driveDocs -Force | Out-Null }
Copy-Item -Path "$projectRoot\docs\*" -Destination $driveDocs -Recurse -Force

# 5. Sincronizar backups locais com backups do Drive
Write-Host "4. Sincronizando snapshots de backup..." -ForegroundColor Yellow
$driveBackups = Join-Path $target "_backups"
if (Test-Path "$projectRoot\_backups") {
    Copy-Item -Path "$projectRoot\_backups\*" -Destination $driveBackups -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "`n========================================================" -ForegroundColor Green
Write-Host "Sincronizacao com o Google Drive concluida com sucesso!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Get-ChildItem -Path $target | Select-Object Name, Length, LastWriteTime
