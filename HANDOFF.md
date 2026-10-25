# NetMonitor — Documento de Handoff & Diretrizes Arquiteturais

> **Documento Mestre de Continuidade:** Consolida a arquitetura, regras de versionamento, fluxo de backup incremental, compilação e diretrizes operacionais para todos os agentes e desenvolvedores do ecossistema Noé VFX.

---

## 📌 1. Visão Geral do Produto

- **Nome**: NetMonitor
- **Versão Atual**: `1.0.0` (Lançamento Oficial)
- **Plataforma**: Windows 11 e Windows 10 (x64 / AnyCPU)
- **Tecnologia**: C# puro (.NET Framework 4.8 / `csc.exe` nativo) + WPF nativo + GDI+ acelerado.
- **Filosofia**: Zero dependências pesadas, sem WebView2/Node/Electron, pegada de memória mínima (~12 MB RAM, 0% CPU em repouso), design alinhado ao **Windows 11 Fluent 2**.
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
   - Na raiz de `release\`, os links ou cópias da versão mais recente são mantidos para conveniência.
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
├── release\                     # Pacotes de distribuição oficiais
│   ├── NetMonitor Portable\     # Pasta da versão portátil autossuficiente
│   │   ├── LEIA-ME.txt
│   │   └── NetMonitor.exe
│   ├── NetMonitor-Portable-v1.0.0.zip
│   ├── NetMonitor-v1.0.0.zip
│   └── NetMonitorSetup.exe
│
├── scripts\                     # Scripts de automação e compilação
│   ├── build.ps1                # Script mestre de compilação C#
│   ├── build_dev.ps1            # Compilação rápida para desenvolvimento
│   ├── build_rendertest.ps1     # Executa harness de testes visuais e screenshots
│   ├── backup_version.ps1       # Automação de backup incremental de versão
│   ├── install.ps1              # Instalação rápida local
│   ├── uninstall.ps1            # Desinstalação limpa
│   ├── upload_to_drive.ps1      # Sincronização com o Google Drive
│   ├── rendertest\              # Código do harness de renderização visual
│   └── test\                    # Scripts auxiliares de teste
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
│   ├── Ipc.cs                   # Comunicação entre processos via Named Pipes
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
| `NetMonitor.cs` | Inicialização do app, hook de barra de tarefas, menu de contexto do widget, timers de amostragem de rede, flyout hover/click e ciclo de vida. |
| `Settings.cs` | Modelos de dados (`AppSettings`), valores padrão, clonagem, persistência no Registro do Windows (`HKCU\Software\NetMonitor`), 4 presets de cores assinados (`pure_mono`, `accent`, `ambar_ceu`, `aurora`), cálculo de contraste e tokens Ace Obsidian para tema claro e escuro. |
| `Version.g.cs` | Definição estática da versão (`1.0.0`), assembly version e link de release. |
| `Data.cs` | Coleta de métricas via `NetworkInterface` e inspeção de processos via `GetExtendedTcpTable` (com filtro de conexões locais e normalização). |
| `WidgetRenderer.cs` | Desenho GDI+ de alto desempenho do widget (linhas de velocidade, setas, fontes tabulares, fundos acrílico/sólido). |
| `FlyoutRenderer.cs` | Renderização do painel suspenso Top 5 processos com cálculo de hit-test, barra de progresso e animação Bézier. |
| `Icons.cs` | Desenho procedural em C# dos 6 conjuntos de setas e ícones dos menus. |
| `WpfKit.cs` | Framework WPF leve: controles de abas, switches Fluent 2, seletores de cor, caixas de diálogo e estilos de janela. |
| `SettingsWindow.cs` | UI completa da Central de Configurações (7 páginas: Cores, Layout, Tipografia, Setas, Apps, Geral, Sobre). |
| `SettingsProcess.cs` | Gerenciamento do ciclo de vida da janela de configurações em processo isolado para estabilidade máxima. |
| `Ipc.cs` | Canal IPC bidirecional rápido via `NamedPipeServerStream` / `NamedPipeClientStream`. |
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
5. Valida os arquivos finais na pasta `release\`.

---

## ☁️ 8. Sincronização com o Google Drive

- **Pasta Local do Drive**: `G:\Meu Drive\NetMonitor`
- Estrutura no Drive:
  - `release/`: Contém os instaladores e pacotes zips mais recentes.
  - `docs/`: Documentação, screenshots e assets de marca (`Logomarca/`, `Instagram - Carrossel/`).
  - `_backups/`: Histórico arquivado das versões fechadas (`v1.0.0_pre-wpf/`, `v1.0.0/`, etc.).
- Para sincronizar, execute:
  ```powershell
  .\scripts\upload_to_drive.ps1
  ```
