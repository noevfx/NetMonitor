# NetMonitor v1.0.3 — Hashes e VirusTotal

> **Data:** 02/11/2026
> **Origem:** `.\build.ps1` em `D:\Noe VFX\.Labs\Windows\NetMonitor`
> **Observação:** executáveis sem assinatura digital podem receber alertas de motores de *machine learning*; o resultado do VirusTotal pode variar sem o arquivo mudar.

## Artefatos

| Arquivo | Tamanho | SHA-256 | VirusTotal |
| :--- | :--- | :--- | :--- |
| `NetMonitorSetup.exe` | 372,0 KB | `f13fffcc23a1cf288e7bc71b361e43877c9a888c6fcccc77dac5c2c74b3073a7` | **3/71** — [relatório](https://www.virustotal.com/gui/file/f13fffcc23a1cf288e7bc71b361e43877c9a888c6fcccc77dac5c2c74b3073a7) |
| `NetMonitor.exe` | 237,0 KB | `0b1c72976d60f00adca721a9f2ae2a60f8284439f759425e6fc03cdcb19cb5c1` | **3/71** — [relatório](https://www.virustotal.com/gui/file/0b1c72976d60f00adca721a9f2ae2a60f8284439f759425e6fc03cdcb19cb5c1) |
| `uninstall.exe` | 30,5 KB | `62ce0fb627225dd4a92789810c4eafc4e14fd7173e4cabc58d9396e9c3ccf3ca` | não enviado (embutido no pacote) |
| `NetMonitor-Portable-v1.0.3.zip` | 125,3 KB | `e3d17da45578070a352eb87386af46f567fce2864d4a810e79ed23ade46141d9` | não enviado (arquivo de distribuição) |
| `NetMonitor-v1.0.3.zip` | 316,1 KB | `a363b9ab035a353612941dfed631ced52f10a67a74f709c555d436a372a28058` | não enviado (arquivo de distribuição) |

## Detecções do VirusTotal

### `NetMonitorSetup.exe` — 3/71

| Motor | Resultado | Tipo |
| :--- | :--- | :--- |
| APEX | `Malicious` | heurística/ML |
| Trapmine | `malicious.moderate.ml.score` | ML |
| Microsoft | `Program:Win32/Wacapew.C!ml` | ML (`!ml`) |

### `NetMonitor.exe` — 3/71

| Motor | Resultado | Tipo |
| :--- | :--- | :--- |
| APEX | `Malicious` | heurística/ML |
| Trapmine | `suspicious.low.ml.score` | ML |
| Microsoft | `Program:Win32/Wacapew.C!ml` | ML (`!ml`) |

## Interpretação

- Nenhum motor aponta família de malware conhecida (trojan, backdoor, ransomware).
- Kaspersky (`HEUR:Trojan.MSIL.Agent.gen`) e SecureAge (`Malicious`), que sinalizavam a 1.0.2, **não sinalizam mais** a 1.0.3.
- Microsoft passou a sinalizar por *machine learning* (`!ml`), típico de executável novo, sem assinatura e sem histórico.
- O app interno (`NetMonitor.exe`) tem as mesmas três sinalizações de ML do instalador; não há detecção específica do comportamento de rede.

Histórico das detecções da 1.0.2:

| Motor | 1.0.2 | 1.0.3 |
| :--- | :--- | :--- |
| Kaspersky | `HEUR:Trojan.MSIL.Agent.gen` | limpo |
| SecureAge | `Malicious` | limpo |
| Trapmine | `Suspicious.low.ml.score` | `ml.score` (setup e app) |
| Microsoft | não detectado | `Program:Win32/Wacapew.C!ml` |
| APEX | não detectado | `Malicious` |

## Reproduzir

```powershell
.\build.ps1
Get-ChildItem .\release\v1.0.3 -Recurse -File |
  Where-Object { $_.Extension -in ".exe", ".zip" } |
  ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name }
```

## Auditoria e Disputa de Falso Positivo na Microsoft (WDSI)

- **Arquivo Submetido:** `NetMonitorSetup.exe`
- **Hash SHA-256:** `f13fffcc23a1cf288e7bc71b361e43877c9a888c6fcccc77dac5c2c74b3073a7`
- **Submission ID:** `ff1b8e70-808d-45bf-8ccf-2ede834c4fad`
- **Submetido por:** `labs@noevfx.com` (*Noé VFX Labs*)
- **Data/Hora:** `02/11/2026 20:42:56`
- **Veredito Oficial da Microsoft:**
  > *"The submitted files do not meet our criteria for malware or potentially unwanted applications. No detection will be added for these files."*
- **Determinação:** **No malware detected** (Cloud: *Limpo*, Client: *Limpo*, Definição: *1.463.102.0*)

---

## Próximos passos para reduzir os alertas

1. Assinar `NetMonitor.exe`, `uninstall.exe` e `NetMonitorSetup.exe` com certificado Authenticode (o build já suporta via `NETMONITOR_SIGN_PFX`).
2. Reenviar os hashes assinados como falso positivo para Microsoft, APEX, Trapmine, Kaspersky e SecureAge.
3. Se a heurística de autoextrator persistir, avaliar empacotamento MSI/Inno Setup com payload separado.
