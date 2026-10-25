# Changelog — NetMonitor

Todas as alterações notáveis deste projeto seguem o padrão de **Versionamento Semântico (SemVer)**: `MAJOR.MINOR.PATCH`.

---

## [1.0.0] - 2026-10-25 (Lançamento Oficial)

### Destaques da Versão Final (Arquitetura WPF Nativa)
- **Reescrita Completa da Interface em WPF Nativo (Sem HTML/WebView2):**
  - Toda a Central de Configurações (`SettingsWindow.cs`) e o Instalador Oficial (`InstallerApp.cs`) foram recriados em XAML/WPF nativo puro com aceleração por hardware do Windows.
  - Eliminação total de qualquer dependência de Chromium, WebView2 ou arquivos HTML embutidos em tempo de execução, garantindo inicialização instantânea (<100ms).
  - Arquitetura de dois processos isolados via IPC (Named Pipes): widget contínuo em background (~0.1% CPU, ~55 MB RAM) e configurações sob demanda (~135 MB RAM, liberada ao fechar). Consumo total no pico: ~190 MB RAM.
- **Flyout de Aplicativos em Tempo Real com Animação Bézier:**
  - Painel suspenso suave renderizado via GDI+ com curva `cubic-bezier(0, 0, 0, 1)` de 280ms.
  - Exibição em tempo real dos 5 processos com maior consumo de largura de banda, com divisão clara entre Download (RX) e Upload (TX).
  - Suporte a fixação por clique ou expansão transitória ao passar o mouse (*hover*).
- **Sistema de Cores Fluent 2 e Paletas Assinadas:**
  - Inclusão de 6 paletas de cores calibradas para alto contraste e elegância: *Ace Obsidian*, *Dark Studio*, *Cyber Stream*, *Nordic Sage*, *Sunset Clay* e *Pure Slate*.
  - Modo *Pure Mono* para setups minimalistas e monocromáticos.
  - Controles finos de opacidade, intensidade e suporte dinâmico a tema Claro e Escuro sincronizado com o Windows.
- **Tipografia e Indicadores de Tráfego:**
  - Suporte a números tabulares (`tnum`) para que os dígitos de velocidade não causem tremor (*jitter*) na tela conforme a velocidade oscila.
  - 6 famílias de indicadores vetoriais para Upload e Download: Triângulos clássicos, Linhas Finas, Outline, Chevrons modernos, Rótulos em texto (TX/RX) e Personalizado.
  - Suporte completo a **Mbps (megabits por segundo)** e **MB/s (megabytes por segundo)** com escala automática inteligente.
- **Instalador e Versão Portátil:**
  - Novo assistente de instalação nativo (`NetMonitorSetup.exe`) com design escuro/claro, criação de atalhos e desinstalador limpo (`uninstall.exe`).
  - Pacote portátil autossuficiente (`NetMonitor Portable\`) que roda sem instalação e sem privilégios de administrador.
- **Arquitetura Modular em C# (.NET Framework 4.8):**
  - Código-fonte desacoplado e modularizado em arquivos especializados: `NetMonitor.cs`, `Settings.cs`, `WidgetRenderer.cs`, `FlyoutRenderer.cs`, `Icons.cs`, `Data.cs`, `WpfKit.cs`, `SettingsWindow.cs`, `SettingsProcess.cs`, `Ipc.cs`, `InstallerApp.cs`, `Uninstaller.cs` e `Version.g.cs`.
  - Fonte única da verdade para versionamento através de `version.txt` e `Version.g.cs`.
