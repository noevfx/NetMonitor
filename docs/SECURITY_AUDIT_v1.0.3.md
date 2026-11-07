# Auditoria de segurança, compilação e instalador — NetMonitor v1.0.3

> **Data:** 02/11/2026
> **Escopo:** `src\`, `scripts\build.ps1`, `scripts\install.ps1`, executáveis gerados, instalador e desinstalador.
> **Objetivo:** identificar causas técnicas das classificações de segurança e corrigir comportamentos desnecessariamente suspeitos, sem alterar a funcionalidade nem contornar detecções.

---

## 1. Artefatos analisados

| Artefato | SHA-256 antes desta auditoria |
|---|---|
| `NetMonitorSetup.exe` (reportado no VirusTotal) | `609098fce9e2efd9d946c61640c8d455d635f005bfed989f249ff149858743d6` |
| `NetMonitorSetup.exe` (build local anterior) | `15cc51f3ed903bfabf416ac2c751caf24cc235fa99eed74573c354ce91a2b457` |

Os hashes diferem porque o binário local já havia sido recompilado após as correções de performance. **As classificações do VirusTotal se referem ao binário antigo**; qualquer binário novo tem hash novo e histórico próprio.

Detecções registradas na 1.0.2 (3/71):

- Kaspersky: `HEUR:Trojan.MSIL.Agent.gen`
- SecureAge: `Malicious`
- Trapmine: `Suspicious.low.ml.score`
- Microsoft: `Undetected` no VirusTotal

---

## 2. Metodologia

1. Varredura do código por APIs sensíveis: WMI, injeção de processo, alocação remota, hooks, depuração, resolução dinâmica, Base64/compressão, execução de comandos.
2. Inspeção do pipeline de build (`csc` 4.8.9221.0, C# 5, .NET Framework 4.8).
3. Extração e leitura dos manifestos embutidos (`RT_MANIFEST`) dos três executáveis.
4. Leitura das informações de versão (`FileVersionInfo`).
5. Verificação dos recursos embutidos no instalador (`NetMonitor.exe`, `uninstall.exe`).
6. Teste isolado do interop COM de atalhos (`IShellLink`/`IPersistFile`).
7. Execução do app com o novo manifesto e checagem do `crash.log`.
8. Comparação de hashes entre binários e recursos embutidos.

---

## 3. Achados técnicos

| # | Achado | Evidência | Risco/heurística | Ação |
|---|---|---|---|---|
| 1 | Binários sem assinatura Authenticode | `Get-AuthenticodeSignature` retorna `NotSigned` | Baixa reputação; qualquer heurística pesa mais | **Não corrigível sem certificado**; pipeline preparado para assinar |
| 2 | Instalador autoextrator (.NET com dois EXEs embutidos) | `/res:NetMonitor.exe` e `/res:uninstall.exe` no `build.ps1` | Principal gatilho de `HEUR:Trojan.MSIL.Agent.gen` | Mantido por design; documentado; assinatura é o caminho correto |
| 3 | COM dinâmico via `WScript.Shell` | `Type.GetTypeFromProgID` + `Activator.CreateInstance` + `dynamic` | "Resolução dinâmica de APIs" / late binding | **Corrigido** com `IShellLink`/`IPersistFile` estáticos |
| 4 | `cmd.exe /c timeout & rmdir` oculto | `Uninstaller.cs` (bloco 6) | T1059 (Command and Scripting Interpreter) | **Corrigido** com `Directory.Delete` + `MoveFileEx(DELAY_UNTIL_REBOOT)` |
| 5 | Encerramento agressivo de processos | `Process.Kill()` direto em todos os `NetMonitor` | Ruído de "process termination" | **Melhorado**: `CloseMainWindow` + espera; `Kill` só como fallback |
| 6 | Desinstalador sem privilégio | Sem manifesto; pasta em Program Files | Desinstalação parcial e silenciosa | **Corrigido**: manifesto `requireAdministrator` |
| 7 | Instalador sobrescrevia preferências | `IsLocked=1`, `Language`, remoção de `CustomX/Y/CustomPos` | Modificação de registro desnecessária | **Corrigido**: só define padrões se a chave não existir |
| 8 | `uninstall.exe`/`NetMonitorSetup.exe` sem versão | `FileVersionInfo` vazio antes | Transparência/reputação | **Corrigido**: `Version.g.cs` + atributos de assembly |
| 9 | Sem manifesto explícito | csc gerava manifesto padrão | Comportamento implícito | **Corrigido**: `app.manifest` (`asInvoker`) e `uninstall.manifest` (`requireAdministrator`) |
| 10 | WMI | `Invoke-CimMethod Win32_Process` em `scripts/install.ps1` | T1047 | **Corrigido**: `Start-Process` (script de desenvolvimento, não distribuído) |

### 3.1 Detalhe dos itens corrigidos

**COM estático (`InstallerApp.cs`).** O atalho passou a ser criado por interfaces COM declaradas em tempo de compilação:

```csharp
[ComImport, Guid("00021401-0000-0000-C000-000000000046")] class ShellLink { }
IShellLinkW sl = (IShellLinkW)new ShellLink();
sl.SetPath(target); sl.SetWorkingDirectory(workDir); sl.SetDescription(description);
((IPersistFile)link).Save(path, true);
```

Isso elimina `Type.GetTypeFromProgID`, `Activator.CreateInstance`, `dynamic` e a referência a `Microsoft.CSharp.dll`.
O interop foi validado em um executável de teste: `TargetPath`, `WorkingDirectory` e `Description` do `.lnk` conferiram.

**Remoção da pasta (`Uninstaller.cs`).** Sem `cmd.exe`:

1. apaga os arquivos que não estão em uso;
2. tenta remover a pasta;
3. o que restar (incluindo o próprio `uninstall.exe`, que está em execução) é agendado com `MoveFileEx(..., MOVEFILE_DELAY_UNTIL_REBOOT)`;
4. a mensagem final avisa que a pasta sai no próximo reinício, quando for o caso.

**Privilégio do desinstalador.** `uninstall.manifest` agora declara `requireAdministrator`. Sem isso, a remoção de `C:\Program Files\NetMonitor` falhava em silêncio quando o usuário clicava em "Desinstalar" nas Configurações do Windows.

**Preservação de preferências.** Em atualização, o instalador não sobrescreve `Language`, não força `IsLocked` e não apaga a posição customizada do widget. Em instalação nova, os padrões continuam iguais aos anteriores.

**Versão e manifesto.** `uninstall.exe` e `NetMonitorSetup.exe` passaram a exibir `1.0.2.0` em Propriedades. Todos os executáveis têm manifesto explícito com `supportedOS` (Windows 8.1/10/11).

---

## 4. Indicadores do sandbox sem correlato no código

Estes itens apareceram no relatório comportamental, mas **não existem no código-fonte entregue**:

| Indicador | Verificação |
|---|---|
| Process Injection (T1055) | Nenhum uso de `CreateRemoteThread`, `VirtualAllocEx`, `WriteProcessMemory`, `SetWindowsHookEx` |
| Criação de processo suspenso | Nenhum uso de `CreateProcess` com `CREATE_SUSPENDED`; só `Process.Start` comum |
| Alterações de proteção de memória / memória executável | Nenhum `VirtualProtect`, `VirtualAlloc`, `Marshal.AllocHGlobal` executável |
| Vectored exception handlers | Nenhum `AddVectoredExceptionHandler` |
| Detecção de ambiente de depuração | Nenhum `Debugger.IsAttached`, `IsDebuggerPresent`, `CheckRemoteDebuggerPresent` |
| Resolução dinâmica de APIs | Removida nesta auditoria (era o `WScript.Shell` por ProgID) |
| Windows Management Instrumentation (T1047) | Removido nesta auditoria (era um script de desenvolvimento) |

Os três primeiros são compatíveis com o comportamento normal do CLR (.NET JIT, GC e tratamento de exceções), que os sandboxes observam em qualquer aplicativo .NET. Eles não podem ser eliminados sem sair do .NET Framework.

### 4.1 Comportamentos legítimos que continuam existindo

| Comportamento | Motivo | Mitigação |
|---|---|---|
| Escrita em `HKCU\Software\NetMonitor` | Preferências do app | Apenas HKCU, sem elevação |
| Escrita em `Run` | "Iniciar com o Windows" (opção do usuário) | Só grava se a caixa estiver marcada |
| Entrada em `Uninstall` | Necessária para aparecer em "Aplicativos" | Padrão do Windows |
| `OpenProcess` + `QueryFullProcessImageName` + `GetProcessIoCounters` | Medir tráfego por processo (função principal) | Somente 1×/s e apenas com flyout/configurações abertos |
| `GetExtendedTcpTable` / `GetExtendedUdpTable` | Listar conexões por PID | Somente leitura; sem captura de pacotes |
| `SendMessageTimeout(WM_COPYDATA)` | Comunicação widget ↔ configurações | Isolado por janela e com timeout |
| Autoextração do instalador | Empacotamento em um único EXE | Documentado; assinatura recomendada |

---

## 5. O que permanece em aberto

1. **Assinatura de código (Authenticode).** É a correção definitiva para reputação e SmartScreen. O `build.ps1` já assina automaticamente quando `NETMONITOR_SIGN_PFX` e `NETMONITOR_SIGN_PASSWORD` estiverem definidos e `signtool.exe` estiver no PATH. Ordem correta: assinar `NetMonitor.exe` e `uninstall.exe` **antes** de embutir no instalador, e o instalador depois.
2. **Empacotamento.** Se a heurística de autoextrator continuar, a alternativa legítima é migrar para MSI (WiX) ou Inno Setup com payload em arquivos separados. Isso muda a distribuição, não o aplicativo; fica como decisão de produto.
3. **Reenvio aos fornecedores.** Com o binário novo e assinado, reenviar como falso positivo para Microsoft, Kaspersky, SecureAge e Trapmine. Sem assinatura, o reenvio tem eficácia limitada.
4. **Compilação determinística.** O `csc` do .NET Framework 4.8 (C# 5) não suporta `/deterministic`. Para hashes reproduzíveis, seria necessário adotar o compilador Roslyn (VS Build Tools ou pacote `Microsoft.Net.Compilers`) — não instalado nesta máquina.
5. **Mensagem da Microsoft.** O texto do formulário foi truncado em "Cloud e Client exibiram `No…`". Para cruzar a inconsistência do analista com a interface, é necessário o restante da mensagem.

---

## 6. Hashes da versão 1.0.3

| Arquivo | SHA-256 |
|---|---|
| `NetMonitor.exe` | `0b1c72976d60f00adca721a9f2ae2a60f8284439f759425e6fc03cdcb19cb5c1` |
| `uninstall.exe` | `62ce0fb627225dd4a92789810c4eafc4e14fd7173e4cabc58d9396e9c3ccf3ca` |
| `NetMonitorSetup.exe` | `f13fffcc23a1cf288e7bc71b361e43877c9a888c6fcccc77dac5c2c74b3073a7` |
| `NetMonitor-Portable-v1.0.3.zip` | `e3d17da45578070a352eb87386af46f567fce2864d4a810e79ed23ade46141d9` |
| `NetMonitor-v1.0.3.zip` | `a363b9ab035a353612941dfed631ced52f10a67a74f709c555d436a372a28058` |

O `README.md` é atualizado automaticamente pelo `build.ps1` com os hashes dos EXEs.

## 7. Como reproduzir e validar

```powershell
.\build.ps1
(Get-FileHash .\bin\NetMonitorSetup.exe -Algorithm SHA256).Hash.ToLower()
[Diagnostics.FileVersionInfo]::GetVersionInfo(".\bin\uninstall.exe")
Get-AuthenticodeSignature .\bin\NetMonitorSetup.exe
```

Validações feitas nesta auditoria:

- manifestos extraídos dos três EXEs (`asInvoker` no app/setup, `requireAdministrator` no desinstalador);
- versão `1.0.3.0` presente nos três;
- recursos embutidos idênticos aos binários de `bin\`;
- atalho criado pelo interop estático com destino, pasta de trabalho e descrição corretos;
- app inicia com o novo manifesto, responde e não gera `crash.log`.

Nenhuma alteração foi feita para contornar assinaturas de antivírus. As mudanças removem comportamentos que não eram necessários (COM dinâmico, shell oculto, clobber de configurações) e tornam o pacote mais explícito e auditável.

---

## 8. Resultado no VirusTotal (1.0.3, 02/11/2026)

| Arquivo | Hash | Detecções | Motores |
|---|---|---|---|
| `NetMonitorSetup.exe` | `f13fffcc…073a7` | **3/71** | APEX (`Malicious`), Trapmine (`malicious.moderate.ml.score`), Microsoft (`Program:Win32/Wacapew.C!ml`) |
| `NetMonitor.exe` | `0b1c7297…cb5c1` | **3/71** | APEX (`Malicious`), Trapmine (`suspicious.low.ml.score`), Microsoft (`Program:Win32/Wacapew.C!ml`) |

Leitura do resultado:

- **Não há detecção de família de malware.** Os três motores usam *machine learning* e/ou heurística genérica; nenhum aponta trojan, backdoor, ransomware ou nome de família conhecida.
- **Kaspersky deixou de sinalizar** `HEUR:Trojan.MSIL.Agent.gen` — a heurística que motivou esta auditoria.
- **SecureAge deixou de sinalizar** `Malicious`.
- **Microsoft passou a sinalizar por ML** (`!ml`), o que é compatível com executável novo, sem assinatura e sem histórico de reputação. Essa classificação costuma ser revertida após análise manual e assinatura.
- O total de motores varia (70/71) conforme disponibilidade do serviço; a contagem pode mudar sem o arquivo mudar.

Relatórios:

- Setup: https://www.virustotal.com/gui/file/f13fffcc23a1cf288e7bc71b361e43877c9a888c6fcccc77dac5c2c74b3073a7
- App: https://www.virustotal.com/gui/file/0b1c72976d60f00adca721a9f2ae2a60f8284439f759425e6fc03cdcb19cb5c1

**Conclusão:** as mudanças eliminaram a detecção nomeada da Kaspersky e reduziram o comportamento suspeito no código, mas não zeraram os alertas. Sem assinatura de código, motores de ML continuam sinalizando o instalador autoextrator e o executável. O próximo passo efetivo é assinar e reenviar como falso positivo.
