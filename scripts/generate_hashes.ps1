# Generate SHA-256 Hashes and Security Table for NetMonitor Releases
param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (-not $Version) {
    $versionFile = Join-Path $root "version.txt"
    if (Test-Path $versionFile) {
        $Version = (Get-Content $versionFile -Raw).Trim()
    } else {
        $Version = "1.0.0"
    }
}

$releaseDir = Join-Path $root "release\v$Version"
if (-not (Test-Path $releaseDir)) {
    Write-Error "Pasta de release nao encontrada: $releaseDir"
    exit 1
}

Write-Host "Calculando hashes SHA-256 para NetMonitor v$Version..." -ForegroundColor Cyan

$files = Get-ChildItem -Path $releaseDir -File | Where-Object { $_.Extension -in ".exe", ".zip" }
$table = @()

foreach ($f in $files) {
    $hash = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash.ToLower()
    $sizeKb = [math]::Round($f.Length / 1KB, 1)
    $table += [PSCustomObject]@{
        Arquivo = $f.Name
        Tamanho = "$sizeKb KB"
        SHA256 = $hash
        VirusTotal = "https://www.virustotal.com/gui/file/$hash"
    }
}

$table | Format-Table -AutoSize

# Output Markdown block
$md = @"
### 🛡️ Verificação de Integridade Criptográfica (SHA-256) & Segurança

| Arquivo | Tamanho | Hash SHA-256 | Relatório VirusTotal |
| :--- | :--- | :--- | :--- |
"@

foreach ($row in $table) {
    $md += "`n| **``$($row.Arquivo)``** | $($row.Tamanho) | ``$($row.SHA256)`` | [Verificar no VirusTotal]($($row.VirusTotal)) |"
}

$outMd = Join-Path $root "docs\SECURITY_AND_HASHES_v$Version.md"
Set-Content -Path $outMd -Value $md -Encoding UTF8
Write-Host "`nTabela de seguranca salva em: $outMd" -ForegroundColor Green
Write-Output $md
