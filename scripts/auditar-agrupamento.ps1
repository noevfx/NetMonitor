# ==============================================================================
# Script de Auditoria Técnica de Agrupamento e Carga de Rede
# ==============================================================================
param([int]$Duracao = 25)

$Url = "https://speed.cloudflare.com/__down?bytes=50000000"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host " INICIANDO AUDITORIA TÉCNICA DE REDE E AGRUPAMENTO (DURAÇÃO: ${Duracao}s)" -ForegroundColor Yellow
Write-Host "==================================================================" -ForegroundColor Cyan

$processos = @()

# 1. Disparar 3 subprocessos curl.exe
Write-Host "`n[1/3] Disparando 3 instâncias de curl.exe (Conexões paralelas)..." -ForegroundColor Green
$curlPids = @()
1..3 | ForEach-Object {
    $p = Start-Process -FilePath "curl.exe" -ArgumentList "-s", "-o", "NUL", "--retry", "999", "-m", "$Duracao", "$Url" -PassThru -WindowStyle Hidden
    $processos += $p
    $curlPids += $p.Id
    Write-Host "  -> curl.exe [PID: $($p.Id)] iniciado." -ForegroundColor Gray
}

# 2. Disparar 3 subprocessos powershell.exe
Write-Host "`n[2/3] Disparando 3 instâncias de powershell.exe (Conexões paralelas)..." -ForegroundColor Magenta
$psPids = @()
$psCode = @"
`$client = New-Object System.Net.WebClient
`$sw = [System.Diagnostics.Stopwatch]::StartNew()
while (`$sw.ElapsedMilliseconds -lt $($Duracao * 1000)) {
    try { `$null = `$client.DownloadData('$Url') } catch { }
}
"@
$encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($psCode))
1..3 | ForEach-Object {
    $p = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile", "-WindowStyle", "Hidden", "-EncodedCommand", $encoded -PassThru
    $processos += $p
    $psPids += $p.Id
    Write-Host "  -> powershell.exe [PID: $($p.Id)] iniciado." -ForegroundColor Gray
}

Write-Host "`n==================================================================" -ForegroundColor Cyan
Write-Host ">> PASSE O MOUSE AGORA NO WIDGET DO NETMONITOR PARA VER O FLYOUT <<" -ForegroundColor Yellow
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "Monitorando os sockets TCP e bytes transferidos a cada 3 segundos:`n"

try {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $amostra = 1

    while ($sw.ElapsedMilliseconds -lt ($Duracao * 1000)) {
        Start-Sleep -Seconds 3
        if ($sw.ElapsedMilliseconds -ge ($Duracao * 1000)) { break }

        # Verificar conexões TCP ativas
        $todasConexoes = Get-NetTCPConnection -State Established -ErrorAction SilentlyContinue

        $curlConexoes = $todasConexoes | Where-Object { $curlPids -contains $_.OwningProcess }
        $psConexoes   = $todasConexoes | Where-Object { $psPids -contains $_.OwningProcess }

        Write-Host "--- [Amostra #$amostra - Tempo: $([math]::Round($sw.ElapsedMilliseconds/1000, 1))s] ---" -ForegroundColor Yellow
        Write-Host "Serviço 'curl':       3 PIDs ($($curlPids -join ', ')) | Conexões TCP Ativas: $($curlConexoes.Count)" -ForegroundColor Green
        Write-Host "Serviço 'powershell': 3 PIDs ($($psPids -join ', ')) | Conexões TCP Ativas: $($psConexoes.Count)" -ForegroundColor Magenta
        
        $amostra++
    }
}
finally {
    Write-Host "`n[3/3] Finalizando processos de teste..." -ForegroundColor Yellow
    foreach ($p in $processos) {
        try { if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } } catch { }
    }
    Write-Host "Todos os 6 processos foram encerrados com sucesso.`n" -ForegroundColor Green
}
