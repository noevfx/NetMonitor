# NetMonitor v1.0.5 — Hashes e VirusTotal

> **Data:** 06/11/2026
> **Origem:** `.\build.ps1` em `D:\Noe VFX\.Labs\Windows\NetMonitor`
> **Mudanças em relação à 1.0.4:** a versão interna dos executáveis passa a ser 1.0.5 (a 1.0.4 se identificava como 1.0.3) e fabricante, publicador e copyright passam a ser **Noé VFX**. Nenhuma mudança de comportamento.
> **Envio:** feito pela API do VirusTotal com `scripts\virustotal.ps1`. Os arquivos ficam na [coleção pública "Noé VFX Labs"](https://www.virustotal.com/gui/collection/4baaa336745245003c10c490999bab6c0e45ccbd5579ea716ce6b25407cacc65) da conta `noevfx`.
> **Observação:** executáveis sem assinatura digital podem receber alertas de motores de *machine learning*; o resultado pode variar sem o arquivo mudar.

## Artefatos

| Arquivo | Tamanho | SHA-256 | VirusTotal |
| :--- | :--- | :--- | :--- |
| `NetMonitorSetup.exe` | 372,0 KB | `ab2d195084ec27734c3c0171c63c27aee60996dd3b9f12b779e14820c387bd19` | **4/70** — [relatório](https://www.virustotal.com/gui/file/ab2d195084ec27734c3c0171c63c27aee60996dd3b9f12b779e14820c387bd19) |
| `NetMonitor.exe` | 237,0 KB | `c0c7f7a6eb0a49424b306874f508ae0952f905d78de58b8138e685b22dd06d29` | **2/71** — [relatório](https://www.virustotal.com/gui/file/c0c7f7a6eb0a49424b306874f508ae0952f905d78de58b8138e685b22dd06d29) |
| `uninstall.exe` | 30,5 KB | `0e30a685e4634b8e110ded82b848a5c1a3c5393ed322d5d288e936433c23bc75` | não enviado (embutido no pacote) |
| `NetMonitor-Portable-v1.0.5.zip` | 125,4 KB | `d3e6b5651201bfa7aaaab0710160c0401ebe6e871102f7fd385d93350713531a` | não enviado (arquivo de distribuição) |
| `NetMonitor-v1.0.5.zip` | 316,3 KB | `3dd1b43704f6a12aa94f2e86a69468290596d7273afe288e6ef1eca73f71ea01` | não enviado (arquivo de distribuição) |

## Detecções do VirusTotal (06/11/2026, reanalisado às 21:05 UTC com o mesmo resultado)

### `NetMonitorSetup.exe` — 4/70

| Motor | Resultado | Tipo |
| :--- | :--- | :--- |
| CrowdStrike | `win/malicious_confidence_60% (D)` | ML |
| Cylance | `Unsafe` | ML |
| APEX | `Malicious` | heurística/ML |
| Trapmine | `malicious.moderate.ml.score` | ML |

### `NetMonitor.exe` — 2/71

| Motor | Resultado | Tipo |
| :--- | :--- | :--- |
| APEX | `Malicious` | heurística/ML |
| Trapmine | `suspicious.low.ml.score` | ML |

## Interpretação

- Nenhum motor aponta família de malware conhecida (trojan, backdoor, ransomware); todos os alertas são de *machine learning*.
- O CrowdStrike aparece só nesta versão (com confiança de 60%), no instalador. O app em si (`NetMonitor.exe`) tem as mesmas duas sinalizações da 1.0.4.
- Como o hash mudou, os vereditos da Microsoft para a 1.0.3 e para a 1.0.4 não valem automaticamente: a 1.0.5 precisa de um envio novo ao WDSI.

## Disputa na Microsoft (WDSI)

- **Arquivo Submetido:** `NetMonitorSetup.exe` (SHA-256 `ab2d195084ec27734c3c0171c63c27aee60996dd3b9f12b779e14820c387bd19`)
- **Submission ID:** `276f452b-5e63-4ad1-a770-050cc5b1a40c`
- **Submetido por:** `labs@noevfx.com` (*Noé VFX Labs*)
- **Data/Hora:** `06/11/2026 18:16:10` (como mostrado no portal)
- **Opinião do usuário:** *Incorrect detection* (Defender Antivirus, Windows 11; detecção informada `Trojan:Win32/Wacatac.C!ml`, a última do Defender local, na 1.0.3; definição informada `1.463.160.0`)
- **Situação (06/11/2026):** *In progress*, determinação final *Pending*. Parecer do analista: **nossos scanners não mostram detecção positiva e não há telemetria** para o arquivo; Cloud e Client: *No malware detected* (definição `1.463.166.0`). A Microsoft pede, para seguir, Defender com a última definição e dados de diagnóstico da máquina que acusou. O analista informou que a submissão será encerrada sem ação pendente, então não se espera outro veredito. **Conferência local (06/11/2026, 22:xx):** Defender com a definição `1.463.166.0` verificou `release/v1.0.5/NetMonitorSetup.exe` (SHA-256 `ab2d1950…bd19`) com `Start-MpScan` e **não registrou nenhuma detecção**; as únicas detecções antigas do NetMonitor no histórico são da 1.0.3 (02/11/2026). Nenhuma resposta à Microsoft é necessária.
- **Histórico:** 1.0.3 — submission `ff1b8e70-808d-45bf-8ccf-2ede834c4fad`, **No malware detected**. 1.0.4 — submission `f85de95a-b47a-4d86-8169-87e5ededa378` (06/11/2026 16:16:31), aguardando; essa versão foi substituída pela 1.0.5.

## Reproduzir

```powershell
.\build.ps1
.\scripts\virustotal.ps1 -Arquivo .\release\v1.0.5\NetMonitorSetup.exe,.\bin\NetMonitor.exe
```
