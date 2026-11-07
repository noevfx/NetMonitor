# virustotal.ps1: envia arquivos ao VirusTotal pela API e mostra o resultado (sem exibir a chave).
# Uso: .\virustotal.ps1 -Arquivo "caminho1","caminho2"
# A chave fica em D:\Noe VFX\_Segredos\VirusTotal\.env (linha VT_API_KEY=...). Nunca imprimir nem versionar.
param([Parameter(Mandatory)][string[]]$Arquivo)
$ErrorActionPreference = "Stop"
$envFile = "D:\Noe VFX\_Segredos\VirusTotal\.env"
$linha = Get-Content $envFile | Where-Object { $_ -like "VT_API_KEY=*" } | Select-Object -First 1
if (-not $linha) { throw "VT_API_KEY nao encontrada em $envFile" }
$key = $linha.Substring("VT_API_KEY=".Length).Trim()
$h = "x-apikey: $key"

foreach ($f in $Arquivo) {
    $sha = (Get-FileHash $f -Algorithm SHA256).Hash.ToLower()
    Write-Host "`n$([IO.Path]::GetFileName($f))  sha256=$sha"
    $up = curl.exe -s -H $h -F "file=@$f" "https://www.virustotal.com/api/v3/files" | ConvertFrom-Json
    if (-not $up.data.id) { throw "Envio falhou: $($up | ConvertTo-Json -Compress -Depth 4)" }
    $id = $up.data.id
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep 15
        $r = curl.exe -s -H $h "https://www.virustotal.com/api/v3/analyses/$id" | ConvertFrom-Json
        if ($r.data.attributes.status -eq "completed") { break }
        Write-Host "  aguardando analise..."
    }
    $s = $r.data.attributes.stats
    $total = $s.malicious + $s.suspicious + $s.undetected + $s.harmless
    Write-Host ("  malicioso={0} suspeito={1} sem deteccao={2} (total {3})" -f $s.malicious, $s.suspicious, $s.undetected, $total)
    $det = $r.data.attributes.results.PSObject.Properties | Where-Object { $_.Value.category -in "malicious", "suspicious" } |
        ForEach-Object { "{0}: {1}" -f $_.Name, $_.Value.result }
    if ($det) { $det | ForEach-Object { Write-Host "  - $_" } }
    Write-Host "  relatorio: https://www.virustotal.com/gui/file/$sha"
}
