# NetMonitor — Documento de Handoff & Diretrizes Arquiteturais

> **Documento Mestre de Continuidade:** Consolida a arquitetura, regras de versionamento, fluxo de backup incremental, compilação e diretrizes operacionais para todos os agentes e desenvolvedores do ecossistema Noé VFX.

---

## 📌 1. Visão Geral do Produto

- **Nome**: NetMonitor
- **Versão Atual**: `1.0.0` (Lançamento Oficial)
- **Plataforma**: Windows 11 e Windows 10 (x64 / AnyCPU)
- **Tecnologia**: C# puro (.NET Framework 4.8 / `csc.exe` nativo) + WPF nativo + GDI+ acelerado.
- **Filosofia**: Zero dependências, sem WebView2/Node/Electron. Dois processos: widget (Windows Forms + GDI+, nunca carrega WPF, ~12–18 MB RAM) e janela de Configurações WPF sob demanda (`NetMonitor.exe --settings <hwnd do widget> <página>`, ~135 MB, liberados ao fechar), ligados por `WM_COPYDATA`. Design alinhado ao **Windows 11 Fluent 2**; referência visual em `docs/design/paletas_fluent2_showcase.html`.
- **Autor / Empresa**: Eduardo Noé ([Noé VFX](https://noevfx.com)) — `labs@noevfx.com`
- **Chave Pix (Doação na aba Sobre)**: `a772dce2-7cf6-45db-a699-2102e189b30b` (Nubank PJ)

---

## 🏛️ 2. Estrutura de Repositórios Git (Duplo Repositório)

Para proteger a propriedade intelectual e ao mesmo tempo oferecer distribuição pública limpa:

1. **Repositório Privado (Desenvolvimento & Código-Fonte):**
   - **Local**: `D:\_Antigravity` (pasta `NetMonitor\`)
   - **Remoto Git**: `https://github.com/eduardonoe/windows.git` (Repositório **Privado**)
   - **Conteúdo**: Código-fonte C# completo (`src/`), scripts de automação (`scripts/`), testes (`scripts/rendertest/`), documentação de design interna (`docs/design/`), manuais de marca e backups históricos (`_backups/`).
2. **Repositório Público (Distribuição & Releases Oficiais):**
   - **Remoto Git**: `https://github.com/eduardonoe/NetMonitor.git` (Repositório **Público**)
   - **Conteúdo**: `README.md` oficial voltado ao usuário, galeria de imagens (`docs/screenshots/`), `CHANGELOG.md`, `LICENSE`, e **GitHub Releases** com os binários executáveis (`NetMonitor-v1.0.0.zip`, `NetMonitor-Portable-v1.0.0.zip` e `NetMonitorSetup.exe`).
   - O código-fonte C# **não** é publicado neste repositório.

---

## 📦 3. Diretriz Mandatória de Versionamento, Backups e Releases

> [!IMPORTANT]
> **REGRA DE OURO:** NUNCA sobrescrever uma versão estável sem antes realizar o backup incremental completo.

### Ciclo de Vida de uma Nova Versão (Exemplo: v1.0.0 ➔ v1.0.1 ou v1.1.0)
Quando for iniciada uma nova versão do NetMonitor, o fluxo obrigatório a ser seguido por qualquer agente é:

1. **Congelamento & Backup Incremental da Versão Anterior:**
   - A versão estável atual (ex: `1.0.0`) torna-se automaticamente um snapshot de backup.
   - Criar a pasta de backup local: `NetMonitor\_backups\v1.0.0\` (ou executar `.\scripts\backup_version.ps1`).
   - Copiar para lá os fontes `src/`, binários estáveis e documentação correspondente.
   - Espelhar o backup para o Google Drive em `G:\Meu Drive\NetMonitor\_backups\v1.0.0\`.
2. **Histórico de Releases Crescente:**
   - Os binários compilados são organizados em pastas crescentes:
     - Local: `NetMonitor\release\v1.0.0\`, `NetMonitor\release\v1.0.1\`, etc.
     - Google Drive: `G:\Meu Drive\NetMonitor\release\v1.0.0\`, `G:\Meu Drive\NetMonitor\release\v1.0.1\`.
   - O `scripts\build.ps1` já grava em `release\v<version.txt>\`; versões anteriores nunca são apagadas nem sobrescritas.
   - No Drive, a raiz de `G:\Meu Drive\NetMonitor` recebe também uma cópia do instalador e do pacote completo da versão mais recente.
3. **Bump de Versão (SemVer):**
   - O incremento ocorre exclusivamente em dois arquivos sincronizados:
     - `NetMonitor\version.txt` (ex: `1.0.1`)
     - `NetMonitor\src\Version.g.cs` (gerado/atualizado automaticamente)
   - O `InstallerApp.cs`, `NetMonitor.cs` e os metadados do assembly leem sempre essa fonte única da verdade.
4. **Publicação nos Repositórios:**
   - Commit de código no repositório privado `eduardonoe/windows`.
   - Criação/atualização da Release e Tag no repositório público `eduardonoe/NetMonitor` anexando os 3 artefatos (`NetMonitorSetup.exe`, `NetMonitor-vX.Y.Z.zip` e `NetMonitor-Portable-vX.Y.Z.zip`).
   - Atualização do espelho no Google Drive (`G:\Meu Drive\NetMonitor`).

---

## 📂 4. Mapa de Pastas e Organização do Projeto

```text
D:\_Antigravity\NetMonitor\
├── .gitignore                   # Protege pastas pesadas e temporárias
├── build.ps1                    # Atalho de compilação da raiz
├── CHANGELOG.md                 # Histórico SemVer de versões
├── HANDOFF.md                   # Este manual mestre de arquitetura
├── LICENSE                      # Licença MIT
├── README.md                    # Documentação oficial completa com screenshots
├── version.txt                  # Fonte única da verdade da versão (ex: 1.0.0)
│
├── bin\                         # Binários compilados intermediários (.exe)
│
├── docs\
│   ├── design\                  # Assets de design e identidade visual
│   │   ├── paletas_fluent2_showcase.html   # Showcase visual e especificação de UI
│   │   ├── Logomarca\           # Manual de marca, vetores Illustrator .ai e fontes
│   │   └── Instagram - Carrossel\ # Peças promocionais de marketing
│   └── screenshots\             # Screenshots das telas do app em Dark e Light Mode
│
├── release\                     # Pacotes oficiais, uma pasta por versão (crescente)
│   └── v1.0.0\
│       ├── NetMonitor Portable\ # LEIA-ME.txt, README.md, NetMonitor.exe
│       ├── NetMonitor-Portable-v1.0.0.zip
│       ├── NetMonitor-v1.0.0.zip
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
│   ├── Version.g.cs             # Versão gerada (1.0.0)
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
│   ├── app.ico                  # Ícone executável multi-resolução
│   └── res\                     # Recursos embutidos (PNGs de ícones e QR Code Pix)
│
└── _backups\                    # Backups locais de segurança (NÃO versionados no Git)
    ├── legacy\                  # Códigos legados pré-refatoração (ex: NetMonitor_original.cs)
    └── v1.0.0\                  # Snapshot congelado da versão estável
```

---

## 💻 5. Arquitetura Modular do Código C# (`src/`)

A refatoração modularizou o código monolítico anterior em componentes coesos:

| Arquivo | Responsabilidade |
| :--- | :--- |
| `NetMonitor.cs` | `Main` (com `--settings`), widget layered (`UpdateLayeredWindow`), encaixe automático ao lado da bandeja, flyout, menu de contexto em seções (`Windows11MenuRenderer`, `RethemeMenu`), troca de tema ao vivo, IPC com a janela, `Mem.Trim`. |
| `Settings.cs` | `AppSettings` (registro `HKCU\Software\NetMonitor`, `ToText`/`FromText`), `Tokens`, `ColorPreset` (8 combinações; `Vivid` e `LightUp`/`LightDown` opcionais), `Pal` (`Resolve`, `FitContrast`: escuro 6,0, claro 5,0, vívidas 4,5), `InvertColors`, `ParseGlyph`, autostart. |
| `Version.g.cs` | Versão (`AppVersion.Text`) e AssemblyVersion; acompanha `version.txt`. |
| `Data.cs` | Amostragem da placa de rede e do tráfego por processo, filtro de loopback. |
| `WidgetRenderer.cs` | Desenho GDI+ do widget (cache de fontes, setas, unidade a 90%, fundos e borda). |
| `FlyoutRenderer.cs` | Flyout Top 5 (400 px, 5 linhas fixas, ranks, barras, colunas alinhadas, rodapé com link no azul da marca) e `SvgPath`. |
| `Icons.cs` | Ícones SVG da marca (gerado por `scripts/gen_icons_cs.py`). |
| `WpfKit.cs` | Kit WPF: `K` (pincéis do tema, escala de fonte), `Ico`, `Sel`, `Btn`, `Seg`, `Switch`, `Input`, `Drop`, `FSlider`. |
| `SettingsWindow.cs` | 7 páginas (Cores, Layout, Tipografia, Setas, Apps, Geral, Sobre), prévia ao vivo, toast. |
| `SettingsProcess.cs` | `RemoteHost` (IPC) e o laço do processo `--settings`, com log de exceções. |
| `Ipc.cs` | `WM_COPYDATA` via `SendMessageTimeout` (300 ms): ApplyLive, ApplySave, Hello, Bye, Sampling, Refresh, Data, ShowPage. |
| `InstallerApp.cs` | UI e lógica do assistente de instalação WPF (`NetMonitorSetup.exe`). |
| `Uninstaller.cs` | Executável leve para remoção de atalhos, pastas e entradas do Registro. |

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
1. Encerra processos ativos (`NetMonitor`, `NetMonitorSetup`).
2. Compila `NetMonitor.exe`, `uninstall.exe` e `NetMonitorSetup.exe` usando `csc.exe` do .NET Framework 4.
3. Empacota a pasta `NetMonitor Portable\` com o executável e o `LEIA-ME.txt`.
4. Inclui o `README.md` atualizado e gera os zips `NetMonitor-Portable-v1.0.0.zip` e `NetMonitor-v1.0.0.zip`.
5. Grava tudo em `release\v<versão>\`.

Restrições do compilador: **C# 5** (sem `?.`, `$""`, `nameof`); WPF via `/lib:...\v4.0.30319\WPF`.

### Testes (sem captura de tela)
- `scripts\build_rendertest.ps1`: renderiza todas as telas em `bin\shots` (claro/escuro) e testa a ligação de cada controle com `AppSettings`.
- `scripts\e2e.ps1`: abre o app real e confere hit-test, abertura do menu, encaixe na barra, os dois processos e a RAM.
- Com a sessão bloqueada o WPF não desenha (PrintWindow sai branco) e o widget se esconde: os testes desligam `HideOnScreensaver` temporariamente; UI Automation lê os textos da janela.
- PowerShell 5.1: `Get-Content`/`Set-Content` sem `-Encoding` corrompem UTF-8. Edite fontes com `[IO.File]::ReadAllText/WriteAllText` ou Python.

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
