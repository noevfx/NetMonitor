## NetMonitor 1.0.3

### Desempenho
- Widget ocioso com **~38% menos CPU** e **~14% menos memória**: a verificação de tela cheia, o tema e o reforço de TOPMOST deixaram de rodar sem necessidade a cada 300 ms.
- Flyout e prévia das Configurações reaproveitam bitmap e fontes; fim da alocação de ~633 KB por segundo no LOH.
- Configurações abrem com as abas criadas sob demanda e não redesenham a prévia quando nada mudou.
- Arrastar os sliders não engasga mais: a prévia e o envio ao widget são agrupados a cada 70 ms.
- Coleta por processo com buffers nativos reaproveitados e menos alocações por amostra.

### Corrigido
- Widget travado após a otimização do cache de formatação.
- Exceção a cada segundo na normalização da amostragem (dicionário alterado durante a enumeração).
- Log de emergência em `%LOCALAPPDATA%\NetMonitor\crash.log` para falhas de UI, threads de fundo e tarefas.
- Instalador não sobrescreve mais idioma, trava de posição e posição do widget em atualizações.
- Desinstalador agora pede elevação e remove a pasta sem `cmd.exe`; o que estiver em uso sai no próximo reinício.

### Segurança e transparência
- Atalhos criados por `IShellLink`/`IPersistFile` estáticos (sem `WScript.Shell`, `ProgID` ou `dynamic`).
- Removido `cmd.exe` oculto do desinstalador e WMI do script de desenvolvimento.
- Encerramento gracioso das instâncias antes de qualquer `Kill`.
- Manifestos explícitos e versão/empresa/descrição nos três executáveis.
- O build assina automaticamente quando há certificado (`NETMONITOR_SIGN_PFX`).

### Atualização
As preferências existentes (`HKCU\Software\NetMonitor`) são preservadas. Basta executar o instalador por cima da 1.0.2 ou usar a versão portátil.

### Primeiro uso
O Windows pode exibir o aviso do **SmartScreen** por ser um app independente ainda sem assinatura digital. Clique em **Mais informações** e depois em **Executar assim mesmo**.

### Verificação (SHA-256 e VirusTotal)

| Arquivo | SHA-256 | VirusTotal |
| :--- | :--- | :--- |
| `NetMonitorSetup.exe` | `f13fffcc23a1cf288e7bc71b361e43877c9a888c6fcccc77dac5c2c74b3073a7` | [3/71](https://www.virustotal.com/gui/file/f13fffcc23a1cf288e7bc71b361e43877c9a888c6fcccc77dac5c2c74b3073a7) |
| `NetMonitor.exe` (portátil) | `0b1c72976d60f00adca721a9f2ae2a60f8284439f759425e6fc03cdcb19cb5c1` | [3/71](https://www.virustotal.com/gui/file/0b1c72976d60f00adca721a9f2ae2a60f8284439f759425e6fc03cdcb19cb5c1) |
| `NetMonitor-v1.0.3.zip` | `a363b9ab035a353612941dfed631ced52f10a67a74f709c555d436a372a28058` | não enviado (arquivo de distribuição) |
| `NetMonitor-Portable-v1.0.3.zip` | `e3d17da45578070a352eb87386af46f567fce2864d4a810e79ed23ade46141d9` | não enviado (arquivo de distribuição) |

As três sinalizações da 1.0.3 vêm de motores de *machine learning* (APEX, Trapmine e Microsoft `Program:Win32/Wacapew.C!ml`), comuns em executáveis novos sem assinatura. **Kaspersky e SecureAge deixaram de sinalizar** em relação à 1.0.2. Nenhum motor aponta família de malware conhecida. O NetMonitor é código aberto (MIT), não coleta dados e só lê os contadores de rede do Windows.
