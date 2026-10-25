# NetMonitor

<div align="center">

<img src="src/res/icone_128.png" alt="NetMonitor Icon" width="96" height="96" />

### Monitor de Rede Inteligente e Minimalista para Windows 11
*Acompanhe seu tráfego de download e upload em tempo real, direto na barra de tarefas ou na tela, com elegância e performance nativa.*

[![Windows 11](https://img.shields.io/badge/Windows-11%20%7C%2010-0078d4?style=flat-square&logo=windows&logoColor=white)](https://github.com/eduardonoe/NetMonitor)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8%20Nativo-512bd4?style=flat-square&logo=dotnet&logoColor=white)](https://github.com/eduardonoe/NetMonitor)
[![Versão](https://img.shields.io/badge/Versão-1.0.0-success?style=flat-square)](https://github.com/eduardonoe/NetMonitor/releases/tag/v1.0.0)
[![Licença](https://img.shields.io/badge/Licença-MIT-blue?style=flat-square)](LICENSE)

[**Download do Instalador (.exe)**](https://github.com/eduardonoe/NetMonitor/releases/download/v1.0.0/NetMonitorSetup.exe) &nbsp;|&nbsp; [**Download Versão Portátil (.zip)**](https://github.com/eduardonoe/NetMonitor/releases/download/v1.0.0/NetMonitor-Portable-v1.0.0.zip) &nbsp;|&nbsp; [**Pacote Completo (.zip)**](https://github.com/eduardonoe/NetMonitor/releases/download/v1.0.0/NetMonitor-v1.0.0.zip)

</div>

---

## ⚡ Visão Geral

O **NetMonitor** é um aplicativo nativo ultraleve projetado especificamente para o Windows 11 e 10. Ele exibe continuamente as taxas de transferência de **Upload (TX)** e **Download (RX)** da sua rede ativa com visual moderno alinhado aos padrões do **Fluent 2 Design**.

Construído em **C# puro e WPF**, sem dependências externas e **sem WebView2 ou Chromium embutido**, o NetMonitor consome praticamente **0.0% de CPU** e apenas cerca de **~12 MB de memória RAM**, garantindo que seu sistema permaneça rápido e responsivo mesmo durante sessões intensas de trabalho, render ou jogos.

---

## ✨ Principais Recursos

- 🚀 **100% Nativo & Ultraleve**: Compilado para .NET Framework 4.8 (já incluído no Windows). Não exige runtime extra, Node.js ou Electron.
- 🎨 **Design Fluent 2 Moderno**: Efeitos visuais de vidro acrílico, cantos arredondados, suporte completo e adaptativo a **Dark Mode** e **Light Mode** seguindo o tema do seu Windows.
- 📊 **Flyout Inteligente (Top 5 Processos)**: Ao passar o mouse ou clicar no widget, um painel suspenso suave revela quais aplicativos estão mais consumindo largura de banda no momento, com tráfego discriminado de subida e descida.
- 🎛️ **Central de Configurações Completa (WPF)**:
  - **Paletas Assinadas**: 6 presets de cores refinados (*Ace Obsidian*, *Dark Studio*, *Cyber Stream*, *Nordic Sage*, *Sunset Clay*, *Pure Slate*) além de modo *Pure Mono*.
  - **Layout Flexível**: Escolha entre visual em 2 linhas (compacto para barra de tarefas) ou linha única horizontal. Borda, fundo e alinhamento ajustáveis.
  - **Tipografia Refinada**: Seleção da fonte do sistema (Segoe UI Variable), controle de tamanho, peso e espaçamento com números tabulares (`tnum`).
  - **Ícones e Setas**: 6 famílias de indicadores vetoriais (*Triângulos*, *Linhas Finas*, *Outline*, *Chevrons*, *Texto TX/RX*, *Customizado*).
  - **Controle de Rede**: Seleção automática ou manual do adaptador de rede (Wi-Fi, Ethernet ou VPN).
  - **Unidades Adaptativas**: Suporte a **Mbps (bits)** ou **MB/s (bytes)** com troca automática de escala (Kbps ➔ Mbps ➔ Gbps).
- 📦 **Instalação Flexível**:
  - **Instalador Oficial (`NetMonitorSetup.exe`)**: Assistente elegante em WPF com atalhos, integração e desinstalador limpo.
  - **Versão Portátil (`NetMonitor Portable`)**: Pasta autossuficiente — basta descompactar e rodar em qualquer lugar ou pendrive, sem alterar o Registro.

---

## 📸 Galeria de Telas

### Central de Configurações (Dark & Light)

| Modo Escuro (Dark) | Modo Claro (Light) |
| :---: | :---: |
| ![Cores Dark](docs/screenshots/win_dark_cores.png) | ![Cores Light](docs/screenshots/win_light_cores.png) |
| *Personalização de Cores e Paletas* | *Interface Clara Adaptativa* |
| ![Layout Dark](docs/screenshots/win_dark_layout.png) | ![Layout Light](docs/screenshots/win_light_layout.png) |
| *Ajuste de Dimensões, Borda e Fundo* | *Configuração de Layout Horizontal/Vertical* |
| ![Tipografia Dark](docs/screenshots/win_dark_fonte.png) | ![Tipografia Light](docs/screenshots/win_light_fonte.png) |
| *Calibração Fina de Fontes e Escalas* | *Ajuste de Tipografia em Modo Claro* |
| ![Setas Dark](docs/screenshots/win_dark_setas.png) | ![Setas Light](docs/screenshots/win_light_setas.png) |
| *Presets de Indicadores Vetoriais* | *Configuração de Ícones de Upload/Download* |

### Monitor de Apps em Tempo Real & Flyout

| Flyout Suspenso (Hover) | Monitor de Processos |
| :---: | :---: |
| ![Flyout Dark](docs/screenshots/flyout_dark.png) | ![Apps Dark](docs/screenshots/win_dark_apps.png) |
| *Consumo ao vivo por aplicação com curvas suaves* | *Ajustes de amostragem e telemetria de rede* |

### Instalador Nativo WPF

<div align="center">
<img src="docs/screenshots/installer_pt.png" alt="Instalador NetMonitor" width="520" />
<br>
<em>Assistente de instalação moderno, bilíngue e integrado ao sistema.</em>
</div>

---

## 🎯 Como Usar

1. **Executar o NetMonitor**:
   - Abra pelo atalho do Menu Iniciar ou execute diretamente `NetMonitor.exe`.
2. **Posicionamento**:
   - O widget flutuante pode ser arrastado livremente para qualquer canto da sua tela ou sobre a barra de tarefas.
3. **Menu de Contexto (Botão Direito)**:
   - Clique com o **botão direito** no widget para acessar rapidamente atalhos:
     - ⚙️ **Configurações...**
     - 📌 **Bloquear Posição** (evita arrasto acidental)
     - 🌐 **Unidade de Velocidade** (Mbps / MB/s)
     - 🌓 **Tema** (Sistema / Escuro / Claro)
     - ❌ **Sair**
4. **Verificar Consumo por Aplicativo**:
   - Passe o cursor sobre o widget ou clique com o botão esquerdo para abrir o **Flyout** com os 5 maiores consumidores de rede no momento.

---

## 🛠️ Requisitos de Sistema

- **Sistema Operacional**: Windows 11 ou Windows 10 (64-bit recomendado).
- **Framework**: .NET Framework 4.8 (nativo e já pré-instalado em todas as versões modernas do Windows).
- **Aceleração Gráfica**: Gráficos GDI+/WPF padrão (compatível com qualquer GPU integrada ou dedicada).

---

## 🔨 Compilando a partir do Código-Fonte

O projeto utiliza o compilador nativo `csc.exe` do .NET Framework 4. Não é necessário ter o Visual Studio completo instalado:

```powershell
# No PowerShell, na pasta raiz do NetMonitor:
.\build.ps1
```

O script compila os binários em `bin/` e gera automaticamente o pacote de distribuição na pasta `release/`.

---

## 🤝 Autor e Suporte

Desenvolvido por **Eduardo Noé** ([Noé VFX](https://noevfx.com)).

- 🌐 **Site Oficial**: [noevfx.com](https://noevfx.com)
- 💻 **Repositório de Releases**: [github.com/eduardonoe/NetMonitor](https://github.com/eduardonoe/NetMonitor)
- ✉️ **Contato**: [labs@noevfx.com](mailto:labs@noevfx.com)
- ☕ **Apoie o Projeto**: Se o NetMonitor é útil no seu dia a dia, você pode apoiar via **Pix** na aba *Sobre* do aplicativo:
  - Chave Pix: `a772dce2-7cf6-45db-a699-2102e189b30b` (Noé VFX - Nubank PJ)

---

## 📄 Licença

Distribuído sob a licença **MIT**. Veja o arquivo `LICENSE` para mais informações.
