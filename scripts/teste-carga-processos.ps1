# ==============================================================================
# NetMonitor - Teste de Carga de Rede e Agrupamento de Subprocessos
# Dispara 2 grupos de download com múltiplos PIDs cada para validar o Flyout:
# - Grupo A: 3 processos curl.exe (devem ser agrupados sob "curl")
# - Grupo B: 3 processos powershell.exe (devem ser agrupados sob "powershell")
# Tráfego 100% em memória (descarta no NUL, sem gravar no SSD).
# ==============================================================================

$DuracaoSegundos = 30
$Url = "https://speed.cloudflare.com/__down?bytes=50000000" # 50 MB em loop

Write-Host ""
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host " INICIANDO TESTE DE MULTIPROCESSOS E AGRUPAMENTO DO NETMONITOR" -ForegroundColor Yellow
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "Duração: $DuracaoSegundos segundos"
Write-Host "Alvo: Disparar múltiplos PIDs de 2 aplicativos diferentes para verificar:"
Write-Host "  1. Se a velocidade de rede mede em tempo real."
Write-Host "  2. Se o NetMonitor REÚNE todos os subprocessos sob o mesmo nome no Flyout."
Write-Host ""

$processos = @()

# Grupo 1: 3 subprocessos curl.exe
Write-Host "[1/2] Disparando 3 subprocessos curl.exe..." -ForegroundColor Green
1..3 | ForEach-Object {
    $p = Start-Process -FilePath "curl.exe" -ArgumentList "-s", "-o", "NUL", "--retry", "999", "-m", "$DuracaoSegundos", "$Url" -PassThru -WindowStyle Hidden
    $processos += $p
    Write-Host "  -> curl.exe iniciado (PID: $($p.Id))" -ForegroundColor Gray
}

# Grupo 2: 3 subprocessos powershell.exe
Write-Host "[2/2] Disparando 3 subprocessos powershell.exe..." -ForegroundColor Magenta
$psCode = @"
`$client = New-Object System.Net.WebClient
`$sw = [System.Diagnostics.Stopwatch]::StartNew()
while (`$sw.ElapsedMilliseconds -lt $($DuracaoSegundos * 1000)) {
    try { `$null = `$client.DownloadData('$Url') } catch { }
}
"@
1..3 | ForEach-Object {
    $bytes = [System.Text.Encoding]::Unicode.GetBytes($psCode)
    $encoded = [Convert]::ToBase64String($bytes)
    $p = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile", "-WindowStyle", "Hidden", "-EncodedCommand", $encoded -PassThru
    $processos += $p
    Write-Host "  -> powershell.exe iniciado (PID: $($p.Id))" -ForegroundColor Gray
}

Write-Host ""
Write-Host ">>> TESTE EM ANDAMENTO! <<<" -ForegroundColor Yellow
Write-Host "PASSE O MOUSE AGORA SOBRE O WIDGET DO NETMONITOR PARA ABRIR O FLYOUT!" -ForegroundColor Cyan
Write-Host "Você deve ver apenas 2 linhas agrupadas:" -ForegroundColor White
Write-Host "  - 'curl' (somando os 3 PIDs)" -ForegroundColor Green
Write-Host "  - 'powershell' (somando os 3 PIDs)" -ForegroundColor Magenta
Write-Host ""

try {
    for ($i = $DuracaoSegundos; $i -gt 0; $i--) {
        Write-Progress -Activity "Gerando tráfego de teste..." -Status "Restam $i segundos (Passe o mouse no NetMonitor)" -PercentComplete ((($DuracaoSegundos - $i) / $DuracaoSegundos) * 100)
        Start-Sleep -Seconds 1
    }
}
finally {
    Write-Host "`nFinalizando todos os subprocessos de teste..." -ForegroundColor Yellow
    foreach ($p in $processos) {
        try {
            if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
        } catch { }
    }
    Write-Host "Todos os processos de teste foram encerrados com sucesso!" -ForegroundColor Green
    Write-Host ""
}
