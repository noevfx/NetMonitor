# Generate SHA-256 Hashes and Query VirusTotal API for NetMonitor Releases
# -Upload: envia ao VirusTotal os arquivos que ele ainda não conhece e espera a análise terminar
param(
    [string]$Version = "",
    [switch]$Upload
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

# Load API key from env or .env file
$apiKey = $env:VIRUSTOTAL_API_KEY
if (-not $apiKey) {
    $envFile = Join-Path $root ".env"
    if (Test-Path $envFile) {
        Get-Content $envFile | ForEach-Object {
            if ($_ -match "VIRUSTOTAL_API_KEY=(.+)") {
                $apiKey = $matches[1].Trim()
            }
        }
    }
}

Write-Host "Calculando hashes SHA-256 e consultando VirusTotal para NetMonitor v$Version..." -ForegroundColor Cyan

$files = @(Get-ChildItem -Path $releaseDir -File | Where-Object { $_.Extension -in ".exe", ".zip" })
$portableExe = Join-Path $releaseDir "NetMonitor Portable\NetMonitor.exe"
if (Test-Path $portableExe) { $files += Get-Item $portableExe }
$table = @()
$headers = @{ "x-apikey" = $apiKey; "User-Agent" = "NetMonitor-Release-Verifier/1.0" }

foreach ($f in $files) {
    $hash = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash.ToLower()
    $sizeKb = [math]::Round($f.Length / 1KB, 1)
    $vtLink = "https://www.virustotal.com/gui/file/$hash"
    $statusText = "Ainda nao enviado"

    if ($apiKey -and $Upload) {
        $known = $true
        try { Invoke-RestMethod -Uri "https://www.virustotal.com/api/v3/files/$hash" -Headers $headers -Method Get -TimeoutSec 15 | Out-Null } catch { $known = $false }
        if (-not $known) {
            Write-Host "  Enviando $($f.Name) ao VirusTotal..." -ForegroundColor Yellow
            # curl.exe: o Invoke-RestMethod do PowerShell 5.1 não envia multipart
            $resp = & curl.exe -s -X POST "https://www.virustotal.com/api/v3/files" -H "x-apikey: $apiKey" -F "file=@$($f.FullName)" | ConvertFrom-Json
            $aid = $resp.data.id
            # API pública: 4 requisições/min; a análise costuma levar 1 a 3 minutos
            for ($i = 0; $i -lt 20 -and $aid; $i++) {
                Start-Sleep -Seconds 20
                try { $an = Invoke-RestMethod -Uri "https://www.virustotal.com/api/v3/analyses/$aid" -Headers $headers -TimeoutSec 15 } catch { continue }
                if ($an.data.attributes.status -eq "completed") { break }
            }
        }
    }

    if ($apiKey) {
        try {
            $res = Invoke-RestMethod -Uri "https://www.virustotal.com/api/v3/files/$hash" -Headers $headers -Method Get -TimeoutSec 10 -ErrorAction SilentlyContinue
            if ($res -and $res.data -and $res.data.attributes -and $res.data.attributes.last_analysis_stats) {
                $stats = $res.data.attributes.last_analysis_stats
                $mal = $stats.malicious
                $total = $stats.malicious + $stats.suspicious + $stats.undetected + $stats.harmless
                if ($mal -eq 0) {
                    $statusText = "0/$total Deteccoes (100% Limpo)"
                } else {
                    $statusText = "$mal/$total (Heuristica/Nao assinado)"
                }
            }
        } catch {}
    }
    
    $table += [PSCustomObject]@{
        Arquivo = $f.Name
        Tamanho = "$sizeKb KB"
        SHA256 = $hash
        StatusVT = $statusText
        Link = $vtLink
    }
}

$table | Format-Table -AutoSize

# Output Markdown block
$md = @"
### 🛡️ Verificação de Integridade Criptográfica (SHA-256) & Segurança

| Arquivo | Tamanho | Hash SHA-256 | Auditoria VirusTotal |
| :--- | :--- | :--- | :--- |
"@

foreach ($row in $table) {
    $md += "`n| **``$($row.Arquivo)``** | $($row.Tamanho) | ``$($row.SHA256)`` | [$($row.StatusVT)]($($row.Link)) |"
}

$outMd = Join-Path $root "docs\SECURITY_AND_HASHES_v$Version.md"
Set-Content -Path $outMd -Value $md -Encoding UTF8
Write-Host "`nTabela de seguranca salva em: $outMd" -ForegroundColor Green
Write-Output $md
