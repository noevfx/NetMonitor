# NetMonitor — Documento de Handoff & Diretrizes Arquiteturais

> **Documento Mestre de Continuidade:** Consolida a arquitetura, regras de versionamento, fluxo de backup incremental, compilação e diretrizes operacionais para todos os agentes e desenvolvedores do ecossistema Noé VFX.

---

## 📌 1. Visão Geral do Produto

- **Nome**: NetMonitor
- **Versão Atual**: `1.0.5` (06/11/2026): corrige a versão interna dos executáveis (a 1.0.4 dizia 1.0.3) e troca fabricante/copyright para Noé VFX; build local em `release/v1.0.5/`, ainda não publicada no GitHub; enviada ao WDSI em 06/11/2026 (submission `276f452b-5e63-4ad1-a770-050cc5b1a40c`, resultado em 06/11/2026: **nenhuma detecção** e sem telemetria; analista vai encerrar sem ação pendente; verificação local com o Defender na definição 1.463.166.0 também sem detecção; nada a responder). A 1.0.4 (hash `20ee03c2...`) foi enviada ao WDSI (submission `f85de95a-b47a-4d86-8169-87e5ededa378`) e fica só como histórico.
- **Plataforma**: Windows 11 e Windows 10 (x64 / AnyCPU)
- **Tecnologia**: C# puro (.NET Framework 4.8 / `csc.exe` nativo) + WPF nativo + GDI+ acelerado.
- **Filosofia**: Zero dependências, sem WebView2/Node/Electron. Dois processos: widget (Windows Forms + GDI+, nunca carrega WPF, ~22 MB privados; working set de 6 a 35 MB) e janela de Configurações WPF sob demanda (`NetMonitor.exe --settings <hwnd do widget> <página>`, ~125–135 MB, liberados ao fechar), ligados por `WM_COPYDATA`. Design alinhado ao **Windows 11 Fluent 2**; referência visual em `docs/design/paletas_fluent2_showcase.html`.
- **Autor / Empresa**: [Noé VFX](https://noevfx.com) — `labs@noevfx.com`
- **Chave Pix (Doação na aba Sobre)**: `a772dce2-7cf6-45db-a699-2102e189b30b` (Nubank PJ)

---

## 🏛️ 2. Estrutura de Repositórios Git (Repositório Único)

Desde 06/11/2026 o NetMonitor é **todo público** (decisão do Eduardo; não existe repositório privado dele):

1. **Repositório único `noevfx/NetMonitor` (público):** tudo do NetMonitor fica nele.
   - **Local**: `D:\Noe VFX\.Labs\Windows\NetMonitor` (repositório git próprio, extraído com o histórico do antigo monorepo `windows`, guardado em `_Arquivo\Antigravity-monorepo`)
   - **Remoto Git**: `git@github.com:noevfx/NetMonitor.git` (Repositório **Público**)
   - **Conteúdo**: código-fonte C# (`src/`), scripts (`scripts/`), documentação, `README.md`, `CHANGELOG.md`, `LICENSE`, telas (`docs/screenshots/`) e as **GitHub Releases** com os binários (`NetMonitorSetup.exe`, `NetMonitor-vX.Y.Z.zip` e `NetMonitor-Portable-vX.Y.Z.zip`).
2. O repositório `noevfx/windows` é **de outros apps Windows** que o Eduardo vai desenvolver; **não tem relação com o NetMonitor** e fica vazio por enquanto.

---

## 📦 3. Diretriz Mandatória de Versionamento, Backups e Releases

> [!IMPORTANT]
> **REGRA DE OURO:** NUNCA sobrescrever uma versão estável sem antes realizar o backup incremental completo.

### Ciclo de Vida de uma Nova Versão (Exemplo: v1.0.3 ➔ v1.0.4 ou v1.1.0)
Quando for iniciada uma nova versão do NetMonitor, o fluxo obrigatório a ser seguido por qualquer agente é:

1. **Congelamento & Backup Incremental da Versão Anterior:**
   - A versão estável atual (ex: `1.0.3`) torna-se automaticamente um snapshot de backup.
   - Criar a pasta de backup local: `NetMonitor\_backups\v1.0.3\` (ou executar `.\scripts\backup_version.ps1`).
   - Copiar para lá os fontes `src/`, binários estáveis e documentação correspondente.
   - Espelhar o backup para o Google Drive em `G:\Meu Drive\NetMonitor\_backups\v1.0.3\`.
2. **Histórico de Releases Crescente:**
   - Os binários compilados são organizados em pastas crescentes:
   - Local: `NetMonitor\release\v1.0.0\`, `v1.0.1\`, `v1.0.2\`, `v1.0.3\`, etc.
   - Google Drive: `G:\Meu Drive\NetMonitor\release\v1.0.0\`, `v1.0.1\`, `v1.0.2\`, `v1.0.3\`.
   - O `scripts\build.ps1` já grava em `release\v<version.txt>\`; versões anteriores nunca são apagadas nem sobrescritas.
   - No Drive, a raiz de `G:\Meu Drive\NetMonitor` recebe também uma cópia do instalador e do pacote completo da versão mais recente.
3. **Bump de Versão (SemVer):**
   - O incremento ocorre exclusivamente em dois arquivos sincronizados:
     - `NetMonitor\version.txt` (ex: `1.0.1`)
     - `NetMonitor\src\Version.g.cs` (gerado/atualizado automaticamente)
   - O `InstallerApp.cs`, `NetMonitor.cs` e os metadados do assembly leem sempre essa fonte única da verdade.
4. **Publicação nos Repositórios:**
   - Commit de código no repositório `noevfx/NetMonitor`.
   - Criação/atualização da Release e Tag no mesmo repositório `noevfx/NetMonitor` anexando os 3 artefatos (`NetMonitorSetup.exe`, `NetMonitor-vX.Y.Z.zip` e `NetMonitor-Portable-vX.Y.Z.zip`).
   - Atualização do espelho no Google Drive (`G:\Meu Drive\NetMonitor`).

---

## 📂 4. Mapa de Pastas e Organização do Projeto

```text
D:\Noe VFX\.Labs\Windows\NetMonitor\
├── .gitignore                   # Protege pastas pesadas e temporárias
├── build.ps1                    # Atalho de compilação da raiz
├── CHANGELOG.md                 # Histórico SemVer de versões
├── HANDOFF.md                   # Este manual mestre de arquitetura
├── LICENSE                      # Licença MIT
├── README.md                    # Documentação oficial completa com screenshots
├── version.txt                  # Fonte única da verdade da versão (1.0.5)
│
├── bin\                         # Binários compilados intermediários (.exe)
│
├── docs\
│   ├── design\                  # Assets de design e identidade visual
│   │   ├── paletas_fluent2_showcase.html   # Showcase visual e especificação de UI
│   │   ├── Logomarca\           # Manual de marca, vetores Illustrator .ai e fontes
│   │   └── Instagram - Carrossel\ # Peças promocionais de marketing
│   ├── screenshots\             # Screenshots das telas do app em Dark e Light Mode
│   ├── PLANO_PERFORMANCE.md     # Auditoria de performance e o que foi implementado na 1.0.3
│   ├── SECURITY_AUDIT_v1.0.3.md # Auditoria de segurança, build e instalador
│   ├── SECURITY_AND_HASHES_v1.0.5.md # Hashes e resultado do VirusTotal (as anteriores ficam ao lado)
│   └── RELEASE_NOTES_v1.0.3.md  # Notas de lançamento voltadas ao usuário
│
├── release\                     # Pacotes oficiais, uma pasta por versão (crescente)
│   └── v1.0.5\
│       ├── NetMonitor Portable\ # LEIA-ME.txt, README.md, NetMonitor.exe
│       ├── NetMonitor-Portable-v1.0.5.zip
│       ├── NetMonitor-v1.0.5.zip
│       └── NetMonitorSetup.exe
│
├── scripts\                     # Scripts de automação e compilação
│   ├── build.ps1                # Script mestre de compilação C#
│   ├── build_dev.ps1            # Compilação rápida para desenvolvimento
│   ├── build_rendertest.ps1     # Harness: PNGs em bin\shots + testes de ligação dos controles
│   ├── e2e.ps1                  # Teste real: hit-test, menu, encaixe, dois processos, RAM
│   ├── shot.ps1                 # Captura de janela (PrintWindow)
│   ├── backup_version.ps1       # Backup da versão estável em _backups\vX.Y.Z
│   ├── install.ps1 / uninstall.ps1
│   ├── upload_to_drive.ps1      # Espelho no Google Drive
│   └── rendertest\              # Código do harness (RenderTest.cs)
│
├── src\                         # Código-Fonte C# Modularizado (.NET 4.8)
│   ├── NetMonitor.cs            # Ponto de entrada do widget, tray, loop de rede e eventos
│   ├── Settings.cs              # Estrutura de dados AppSettings e persistência (Registry)
│   ├── Version.g.cs             # Versão gerada (1.0.5)
│   ├── Data.cs                  # Leitura de I/O de rede e amostragem de tráfego TCP
│   ├── WidgetRenderer.cs        # Renderização GDI+ do widget na barra de tarefas
│   ├── FlyoutRenderer.cs        # Renderização GDI+ do flyout suspenso Top 5 Apps
│   ├── Icons.cs                 # Ícones vetoriais em código C# puro
│   ├── WpfKit.cs                # Componentes e helpers XAML/WPF personalizados
│   ├── SettingsWindow.cs        # Janela completa de Configurações em WPF nativo
│   ├── SettingsProcess.cs       # Isolamento de processo da janela de configurações
│   ├── Ipc.cs                   # Comunicação entre processos via WM_COPYDATA
│   ├── InstallerApp.cs          # Assistente de instalação nativo WPF
│   ├── Uninstaller.cs           # Desinstalador nativo limpo
│   ├── app.manifest             # Manifesto do app e do instalador (asInvoker)
│   ├── uninstall.manifest       # Manifesto do desinstalador (requireAdministrator)
│   ├── app.ico                  # Ícone executável multi-resolução
│   └── res\                     # Recursos embutidos (PNGs de ícones e QR Code Pix)
│
└── _backups\                    # Backups locais de segurança (NÃO versionados no Git)
    ├── legacy\                  # Códigos legados pré-refatoração (ex: NetMonitor_original.cs)
    └── v1.0.x\                  # Um snapshot por versão estável fechada
```

---

## 💻 5. Arquitetura Modular do Código C# (`src/`)

A refatoração modularizou o código monolítico anterior em componentes coesos:

| Arquivo | Responsabilidade |
| :--- | :--- |
| `NetMonitor.cs` | `Main` (com `--settings`), widget layered (`UpdateLayeredWindow` com superfície DC/DIB reaproveitada), encaixe automático, flyout, menu em seções, tema ao vivo, IPC, `Mem.Trim`, amostragem serializada por `QueueSample` e `CrashLog` (`%LOCALAPPDATA%\NetMonitor\crash.log`). DPI, tema e verificação de tela cheia são cacheados. |
| `Settings.cs` | `AppSettings` (registro `HKCU\Software\NetMonitor`, `ToText`/`FromText`), `Tokens`, `ColorPreset` (8 combinações), `Pal` (`Resolve` com cache e `FitContrast`), `InvertColors`, `ParseGlyph`, autostart. |
| `Version.g.cs` | Versão (`AppVersion.Text`) e AssemblyVersion; acompanha `version.txt` (1.0.5). |
| `Data.cs` | Item de processo e formatação de velocidade (`SpeedFmt`). |
| `WidgetRenderer.cs` | Desenho GDI+ do widget com cache de fontes, métricas de layout e bitmap de saída reaproveitado (`RenderTo`); setas, unidade e borda. |
| `FlyoutRenderer.cs` | Flyout Top 5 com fontes em cache e bitmap reaproveitado (`RenderTo`); `SvgPath`. |
| `Icons.cs` | Ícones SVG da marca (gerado por `scripts/gen_icons_cs.py`). |
| `WpfKit.cs` | Kit WPF: `K` (pincéis do tema, escala de fonte, cache de cores), `Ico`, `Sel`, `Btn`, `Seg`, `Switch`, `Input`, `Drop`, `FSlider`. |
| `SettingsWindow.cs` | 7 páginas com **lazy loading**, prévia ao vivo com bitmap reaproveitado, tick de 500 ms sem redesenho inútil, debounce em `Changed()`, toast. |
| `SettingsProcess.cs` | `RemoteHost` (IPC) e o laço do processo `--settings`. |
| `Ipc.cs` | `WM_COPYDATA` via `SendMessageTimeout` (300 ms) com buffer nativo reaproveitado: ApplyLive, ApplySave, Hello, Bye, Sampling, Refresh, Data, ShowPage. |
| `InstallerApp.cs` | Assistente WPF; encerra instâncias com `CloseMainWindow` antes de `Kill`, cria atalhos por `IShellLink` estático, preserva preferências em atualização. |
| `Uninstaller.cs` | Desinstalador com manifesto `requireAdministrator`; remove sem `cmd.exe` e agenda o que estiver em uso para o próximo reinício. |
| `app.manifest` / `uninstall.manifest` | Níveis de execução explícitos (`asInvoker` / `requireAdministrator`) e `supportedOS` Windows 8.1/10/11. |

---

## 🎨 6. Assets de Identidade Visual & Design

- As pastas `Logomarca/` (manual de marca, PDFs, `.ai`) e `Instagram - Carrossel/` residem em `docs/design/`.
- Elas estão adicionadas ao `.gitignore` local para não inflar o repositório Git, mas são mantidas em disco e sincronizadas no **Google Drive**.
- Os scripts de compilação **não dependem** dessas pastas externas; todos os PNGs e ícones necessários para compilação já estão salvos em `src/app.ico` e `src/res/`.

---

## 🚀 7. Como Compilar e Empacotar

```powershell
# Execução na raiz do projeto:
.\build.ps1
```
O build:
1. Tenta encerrar processos ativos (`NetMonitor`, `NetMonitorSetup`) sem abortar quando a instância está elevada.
2. Compila `NetMonitor.exe` (manifesto `app.manifest`) e `uninstall.exe` (manifesto `uninstall.manifest` e `Version.g.cs`).
3. Se `NETMONITOR_SIGN_PFX`/`NETMONITOR_SIGN_PASSWORD` estiverem definidos, assina os dois **antes** de embutir no instalador.
4. Compila `NetMonitorSetup.exe` (manifesto `app.manifest`) com os dois EXEs embutidos e assina o resultado.
5. Empacota a pasta `NetMonitor Portable\` com o executável e o `LEIA-ME.txt`.
6. Inclui o `README.md` atualizado e gera os zips `NetMonitor-Portable-vX.Y.Z.zip` e `NetMonitor-vX.Y.Z.zip`.
7. Grava tudo em `release\v<versão>\`.

Restrições do compilador: **C# 5** (sem `?.`, `$""`, `nameof`); WPF via `/lib:...\v4.0.30319\WPF`.
O `csc` do Framework 4.8 não suporta compilação determinística (`/deterministic`); hashes mudam entre builds mesmo com o mesmo código.

### Testes (sem captura de tela)
- `scripts\build_rendertest.ps1`: renderiza todas as telas em `bin\shots` (claro/escuro) e testa a ligação de cada controle com `AppSettings`.
- `scripts\e2e.ps1`: abre o app real e confere hit-test, abertura do menu, encaixe na barra, os dois processos e a RAM.
- Com a sessão bloqueada o WPF não desenha (PrintWindow sai branco) e o widget se esconde: os testes desligam `HideOnScreensaver` temporariamente; UI Automation lê os textos da janela.
- PowerShell 5.1: `Get-Content`/`Set-Content` sem `-Encoding` corrompem UTF-8. Edite fontes com `[IO.File]::ReadAllText/WriteAllText` ou Python.

### Resultados medidos na 1.0.3
- Widget ocioso: **1,04% de um núcleo** (era 1,67%) e **34,3 MB** de working set (era 40,1 MB).
- Configurações ociosas: **~0 ms/15 s** (era 203 ms/15 s) — sem redesenho quando nada muda.
- Abertura das Configurações (morno): **~1622 ms** (era ~1678 ms). O ganho é limitado pela inicialização do WPF.
- Flyout aberto: **0,78%** de um núcleo; Top 5 validado com tráfego real.
- As 7 abas abriram e responderam; sem `crash.log` nem `error.log`.
- VirusTotal (1.0.5, 06/11/2026): `NetMonitorSetup.exe` **4/70** e `NetMonitor.exe` **2/71**, apenas motores de ML (CrowdStrike, Cylance, APEX, Trapmine). Detalhes em `docs/SECURITY_AND_HASHES_v1.0.5.md`. Envio com `scripts/virustotal.ps1` (chave em `D:\Noe VFX\_Segredos\VirusTotal\.env`).

---

## ☁️ 8. Sincronização com o Google Drive

- **Pasta Local do Drive**: `G:\Meu Drive\NetMonitor`
- Estrutura no Drive:
  - `release/vX.Y.Z/`: pacotes de cada versão, em ordem crescente (cópia do instalador e do pacote completo mais recentes na raiz).
  - `docs/`: Documentação, screenshots e assets de marca (`Logomarca/`, `Instagram - Carrossel/`).
  - `_backups/`: Histórico arquivado das versões fechadas (`v1.0.0_pre-wpf/`, `v1.0.0/`, etc.).
- Para sincronizar, execute:
  ```powershell
  .\scripts\upload_to_drive.ps1
  ```

---

## 🔐 9. Segurança, assinatura e VirusTotal

### 9.1 Estado da 1.0.3 (histórico; a 1.0.5 está em `docs/SECURITY_AND_HASHES_v1.0.5.md`: instalador 4/70, app 2/71, só ML)

| Arquivo | Detecções (02/11/2026) | Observação |
| :--- | :--- | :--- |
| `NetMonitorSetup.exe` | 3/71 | APEX, Trapmine e Microsoft — todos *machine learning* |
| `NetMonitor.exe` | 3/71 | APEX, Trapmine e Microsoft — todos *machine learning* |

Não há detecção de família de malware (Kaspersky, que sinalizava `HEUR:Trojan.MSIL.Agent.gen` na 1.0.2, deixou de sinalizar). Os três motores restantes avaliam o formato do executável sem assinatura digital.

### 9.2 O que foi removido do código

- COM dinâmico (`WScript.Shell` por ProgID + `Activator` + `dynamic`) → `IShellLink`/`IPersistFile` estáticos.
- `cmd.exe /c timeout & rmdir` oculto no desinstalador → `Directory.Delete` + `MoveFileEx(DELAY_UNTIL_REBOOT)`.
- WMI no script de desenvolvimento (`Invoke-CimMethod Win32_Process`) → `Start-Process`.
- Encerramento imediato por `Kill` → `CloseMainWindow` com fallback.
- Sobrescrita de preferências pelo instalador → só define padrões em instalação nova.

### 9.3 O que continua legítimo e por quê

- `OpenProcess`/`QueryFullProcessImageName`/`GetProcessIoCounters`: medir tráfego por processo (1×/s, só com flyout ou Configurações abertos).
- `GetExtendedTcpTable`/`GetExtendedUdpTable`: listar conexões por PID (somente leitura).
- Registro em `HKCU` e `Run`: preferências e opção "Iniciar com o Windows".
- Autoextração do instalador: empacotamento em um único EXE.

### 9.4 Caminho definitivo

1. **Assinatura Authenticode** com certificado OV/EV (o build já assina via `NETMONITOR_SIGN_PFX`).
2. Reenvio aos fornecedores como falso positivo (Microsoft, Kaspersky, SecureAge, Trapmine, APEX).
3. Se a heurística de autoextrator persistir, migrar o empacotamento para MSI/Inno Setup com payload separado.
4. Repositório de hashes e relatórios: `docs/SECURITY_AND_HASHES_v1.0.5.md` (as anteriores ficam ao lado).
