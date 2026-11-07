# NetMonitor v1.0.4 — Hashes e VirusTotal

> **Data:** 06/11/2026
> **Origem:** `.\build.ps1` em `D:\Noe VFX\.Labs\Windows\NetMonitor`
> **Substituída pela 1.0.5 (06/11/2026):** este build se identificava internamente como 1.0.3 e foi enviado ao WDSI e ao VirusTotal assim. Fica só como histórico; ver `SECURITY_AND_HASHES_v1.0.5.md`.
> **Mudança em relação à 1.0.3:** só o link do GitHub (`github.com/noevfx`). Nenhuma mudança de comportamento.
> **Envio:** feito pela API do VirusTotal com `scripts\virustotal.ps1`. Os arquivos ficam na [coleção pública "Noé VFX Labs"](https://www.virustotal.com/gui/collection/4baaa336745245003c10c490999bab6c0e45ccbd5579ea716ce6b25407cacc65) da conta `noevfx`.
> **Observação:** executáveis sem assinatura digital podem receber alertas de motores de *machine learning*; o resultado pode variar sem o arquivo mudar.

## Artefatos

| Arquivo | Tamanho | SHA-256 | VirusTotal |
| :--- | :--- | :--- | :--- |
| `NetMonitorSetup.exe` | 372,0 KB | `20ee03c2734e4ad891e4c4d54a463b66ee23ed10d9360e4adee25eec689be394` | **3/70** — [relatório](https://www.virustotal.com/gui/file/20ee03c2734e4ad891e4c4d54a463b66ee23ed10d9360e4adee25eec689be394) |
| `NetMonitor.exe` | 237,0 KB | `db70a8a01a1cb6b91591c0e434f997c270085c692665f41d20b195e558083711` | **2/71** — [relatório](https://www.virustotal.com/gui/file/db70a8a01a1cb6b91591c0e434f997c270085c692665f41d20b195e558083711) |
| `uninstall.exe` | 30,5 KB | `d3eeaa089aea5e54f5f220a3c3abc2a81097452b55e422bdc6d538b8e835653d` | não enviado (embutido no pacote) |
| `NetMonitor-Portable-v1.0.4.zip` | 125,4 KB | `72c7135a461e0fe1804876a22346eca1885f90ae8718e56cef0c9c2cdce37137` | não enviado (arquivo de distribuição) |
| `NetMonitor-v1.0.4.zip` | 316,1 KB | `8fdc6aad782a848c1812bbe3b94404222ecc70e5251e3955c7b4b683be80b431` | não enviado (arquivo de distribuição) |

## Detecções do VirusTotal (06/11/2026)

### `NetMonitorSetup.exe` — 3/70

| Motor | Resultado | Tipo |
| :--- | :--- | :--- |
| Cylance | `Unsafe` | ML |
| APEX | `Malicious` | heurística/ML |
| Trapmine | `malicious.moderate.ml.score` | ML |

### `NetMonitor.exe` — 2/71

| Motor | Resultado | Tipo |
| :--- | :--- | :--- |
| APEX | `Malicious` | heurística/ML |
| Trapmine | `suspicious.low.ml.score` | ML |

## Interpretação

- Nenhum motor aponta família de malware conhecida (trojan, backdoor, ransomware); todos são de *machine learning*.
- Na 1.0.3 a Microsoft sinalizava por ML (`Program:Win32/Wacapew.C!ml`) e depois atestou **No malware detected** no WDSI (ver `SECURITY_AND_HASHES_v1.0.3.md`). Na 1.0.4 a Microsoft **não** sinalizou; entrou o Cylance só no instalador.
- Como o hash mudou, o veredito do WDSI da 1.0.3 não vale automaticamente: reenviar a 1.0.4 à Microsoft.

## Disputa na Microsoft (WDSI)

- **Arquivo Submetido:** `NetMonitorSetup.exe` (SHA-256 `20ee03c2734e4ad891e4c4d54a463b66ee23ed10d9360e4adee25eec689be394`)
- **Submission ID:** `f85de95a-b47a-4d86-8169-87e5ededa378`
- **Submetido por:** `labs@noevfx.com` (*Noé VFX Labs*)
- **Data/Hora:** `06/11/2026 16:16:31`
- **Opinião do usuário:** *Incorrect detection* (Defender Antivirus, Windows 11; detecção informada `Program:Win32/Wacapew.C!ml`, definição `1.463.163.0`)
- **Situação:** **Submitted** (aguardando análise; determinação final *Pending*). Registrar aqui o veredito oficial quando sair.

## Reproduzir

```powershell
.\build.ps1
.\scripts\virustotal.ps1 -Arquivo .\release\v1.0.4\NetMonitorSetup.exe,.\bin\NetMonitor.exe
```
