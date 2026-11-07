# NetMonitor

<div align="center">

<img src="src/res/icone_128.png" alt="NetMonitor" width="96" height="96" />

### Upload e download em tempo real, direto na barra de tarefas do Windows 11

[![Windows 11](https://img.shields.io/badge/Windows-11%20%7C%2010-0078d4?style=flat-square&logo=windows&logoColor=white)](https://github.com/noevfx/NetMonitor)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8%20nativo-512bd4?style=flat-square&logo=dotnet&logoColor=white)](https://github.com/noevfx/NetMonitor)
[![Versão](https://img.shields.io/badge/Versão-1.0.5-success?style=flat-square)](https://github.com/noevfx/NetMonitor/releases/tag/v1.0.5)
[![Licença](https://img.shields.io/badge/Licença-MIT-blue?style=flat-square)](LICENSE)

[**Instalador (.exe)**](https://github.com/noevfx/NetMonitor/releases/download/v1.0.5/NetMonitorSetup.exe) &nbsp;|&nbsp; [**Portátil (.zip)**](https://github.com/noevfx/NetMonitor/releases/download/v1.0.5/NetMonitor-Portable-v1.0.5.zip) &nbsp;|&nbsp; [**Pacote completo (.zip)**](https://github.com/noevfx/NetMonitor/releases/download/v1.0.5/NetMonitor-v1.0.5.zip)

</div>

---

## Visão geral

O **NetMonitor** mostra as taxas de **upload (TX)** e **download (RX)** ao lado do relógio da barra de tarefas e, ao passar o mouse, abre um painel com os **5 apps que mais usam a rede**.

É um aplicativo **100% nativo**: C# + WPF + GDI+, compilado com o `csc.exe` que já vem no Windows. Sem navegador embutido, sem WebView2, sem dependências.

### Dois processos, pouca memória

| Processo | Função | RAM |
| :--- | :--- | :--- |
| `NetMonitor.exe` | Widget, flyout e menu (Windows Forms + GDI+, nunca carrega WPF) | **~22 MB privados** (working set de 6 a 35 MB) |
| `NetMonitor.exe --settings` | Janela de Configurações em WPF, aberta só quando você pede | **~125 a 135 MB**, liberados ao fechar |

Os dois conversam por mensagens do Windows (`WM_COPYDATA`): a janela mostra a prévia ao vivo e o widget aplica as mudanças na hora.

---

## Recursos

### Widget na barra de tarefas
- Encaixa sozinho **à esquerda da área de notificação** e acompanha mudanças na barra. Pode ser arrastado para qualquer lugar; **Redefinir posição** volta ao encaixe automático.
- Formato **duas linhas** ou **uma linha**, ordem upload/download configurável.
- Fundo **transparente**, **acrílico** ou **sólido**, com borda opcional.
- Unidades em **bits** (Kbps / Mbps / Gbps) ou **bytes** (KB/s / MB/s / GB/s), escala **automática** ou **individual** por sentido; a unidade pode ser ocultada.
- Clique com o botão direito em qualquer ponto do widget abre o menu (não só em cima do texto).
- Acompanha a barra de tarefas: some em tela cheia (F11, jogos, vídeos, preview do Premiere/After Effects) e quando a barra se oculta automaticamente.
- Some sozinho com o protetor de tela ou com o Windows bloqueado.

### Cores & tema
- **8 combinações de cores**, desenhadas no tema escuro e recalculadas para o claro com contraste mínimo garantido:

  | | | | |
  | :--- | :--- | :--- | :--- |
  | **Monocromático** (padrão) | **Cor de Destaque** (segue o Windows) | **Lima & Ciano** | **Âmbar & Céu** (seguro p/ daltonismo) |
  | **Aurora** | **Rosa & Esmeralda** | **Fogo & Gelo** | **Azul & Magenta** (azul da marca + neon) |

- **Cores personalizadas** para upload e download (clique no quadrado ou em *Alterar*).
- **Inverter cores**: troca a cor do upload com a do download, no widget e nas cores personalizadas.
- Tema **Seguir Windows** (padrão), **Escuro** ou **Claro**. Ao trocar o tema do Windows, widget, flyout e menu (mesmo aberto) mudam na hora.

### Tipografia
- Fonte padrão **Segoe UI Variable** (a do Windows 11), também Segoe UI Variable Display, Cascadia Code e Consolas.
- Tamanho (padrão **10,5 pt**) e peso ajustáveis.

### Setas
- 6 estilos: **Triângulos Sólidos**, **Setas Finas**, **Triângulos Vazados**, **Chevrons**, **TX:/RX:** e **Personalizado**.
- Caractere próprio aceita texto ou código (`U+2191`, `↑`, `0x2191`, `&#x2191;`).
- Setas à esquerda ou à direita dos números; tamanho ajustável com **− / +**.

### Flyout dos apps (Top 5)
- Os 5 apps com mais tráfego, numerados, com barra proporcional e colunas TX/RX alinhadas.
- Modo **Separado** (TX e RX) ou **Combinado**.
- Abre **ao passar o mouse** ou fica **fixado aberto**; pode ser **desativado**.
- Rodapé com atalho para **Configurações** e o total da máquina.
- Ignora tráfego de loopback (opcional).

### Menu de contexto
Organizado em seções: **Unidade**, **Escala**, **Flyout** (ativado, abrir ao passar o mouse, fixado aberto) e **Mais opções** (placa de rede, posição das setas, idioma, separado/combinado, travar posição, iniciar com o Windows, redefinir posição), além de Configurações, Sobre e Sair.

### Janela de Configurações
- 7 páginas: **Cores & Tema**, **Layout & System Tray**, **Tipografia**, **Ícones de Seta**, **Top 5 Apps**, **Geral**, **Sobre**.
- Prévia ao vivo do widget e do flyout na lateral, com o consumo de CPU/RAM do próprio NetMonitor.
- **Salvar**, **Cancelar** (desfaz tudo) e **Restaurar** (padrão de fábrica).

### Instalador
- Assistente nativo em WPF que segue o tema do Windows, em português e inglês, com atalhos, início com o Windows e desinstalador.

---

## Telas

### Na barra de tarefas

| Barra escura | Barra clara |
| :---: | :---: |
| ![Monocromático, barra escura](docs/screenshots/mono_dark_taskbar.png) | ![Monocromático, barra clara](docs/screenshots/mono_light_taskbar.png) |
| ![Lima & Ciano, barra escura](docs/screenshots/lima_dark_taskbar.png) | ![Lima & Ciano, barra clara](docs/screenshots/lima_light_taskbar.png) |

### Configurações (Monocromático)

| Escuro | Claro |
| :---: | :---: |
| ![](docs/screenshots/mono_dark_win_cores.png) | ![](docs/screenshots/mono_light_win_cores.png) |
| ![](docs/screenshots/mono_dark_win_layout.png) | ![](docs/screenshots/mono_light_win_layout.png) |
| ![](docs/screenshots/mono_dark_win_fonte.png) | ![](docs/screenshots/mono_light_win_fonte.png) |
| ![](docs/screenshots/mono_dark_win_setas.png) | ![](docs/screenshots/mono_light_win_setas.png) |
| ![](docs/screenshots/mono_dark_win_apps.png) | ![](docs/screenshots/mono_light_win_apps.png) |
| ![](docs/screenshots/mono_dark_win_geral.png) | ![](docs/screenshots/mono_light_win_geral.png) |
| ![](docs/screenshots/mono_dark_win_sobre.png) | ![](docs/screenshots/mono_light_win_sobre.png) |

### Configurações (Lima & Ciano)

| Escuro | Claro |
| :---: | :---: |
| ![](docs/screenshots/lima_dark_win_cores.png) | ![](docs/screenshots/lima_light_win_cores.png) |
| ![](docs/screenshots/lima_dark_win_layout.png) | ![](docs/screenshots/lima_light_win_layout.png) |
| ![](docs/screenshots/lima_dark_win_fonte.png) | ![](docs/screenshots/lima_light_win_fonte.png) |
| ![](docs/screenshots/lima_dark_win_setas.png) | ![](docs/screenshots/lima_light_win_setas.png) |
| ![](docs/screenshots/lima_dark_win_apps.png) | ![](docs/screenshots/lima_light_win_apps.png) |
| ![](docs/screenshots/lima_dark_win_geral.png) | ![](docs/screenshots/lima_light_win_geral.png) |
| ![](docs/screenshots/lima_dark_win_sobre.png) | ![](docs/screenshots/lima_light_win_sobre.png) |

Todas as telas são geradas por `scripts\build_rendertest.ps1 -Gallery`.

<div align="center">
<img src="docs/screenshots/installer_pt.png" alt="Instalador" width="520" />
</div>

---

## Instalação

1. **Instalador** (`NetMonitorSetup.exe`): instala em `%LocalAppData%\Programs\NetMonitor` (sem administrador), cria atalhos e o desinstalador.
2. **Portátil** (`NetMonitor-Portable-v1.0.5.zip`): descompacte e abra `NetMonitor.exe`. As preferências ficam em `HKCU\Software\NetMonitor`.

**Requisitos:** Windows 11 ou 10, .NET Framework 4.8 (já vem no Windows).

> [!NOTE]
> No primeiro uso, o Windows pode exibir o aviso do **SmartScreen** por ser um app independente novo, ainda sem assinatura digital. Clique em **Mais informações** e depois em **Executar assim mesmo**.

---

## Segurança

O NetMonitor é código aberto (MIT), não coleta dados e não acessa a internet: só lê os contadores de rede do próprio Windows. Confira se o arquivo que você baixou é o original comparando o SHA-256:

```powershell
Get-FileHash .\NetMonitorSetup.exe -Algorithm SHA256
```

<!-- hashes:start -->
| Arquivo (v1.0.5) | SHA-256 | VirusTotal |
| :--- | :--- | :--- |
| `NetMonitorSetup.exe` | `ab2d195084ec27734c3c0171c63c27aee60996dd3b9f12b779e14820c387bd19` | [relatório](https://www.virustotal.com/gui/file/ab2d195084ec27734c3c0171c63c27aee60996dd3b9f12b779e14820c387bd19) |
| `NetMonitor.exe` | `c0c7f7a6eb0a49424b306874f508ae0952f905d78de58b8138e685b22dd06d29` | [relatório](https://www.virustotal.com/gui/file/c0c7f7a6eb0a49424b306874f508ae0952f905d78de58b8138e685b22dd06d29) |
<!-- hashes:end -->

**Resultado no VirusTotal em 06/11/2026 (v1.0.5):** `NetMonitorSetup.exe` **4/70** (CrowdStrike, Cylance, APEX e Trapmine) e `NetMonitor.exe` **2/71** (APEX e Trapmine). Não há detecção de família de malware: todos os alertas são de *machine learning* por falta de assinatura digital. Na 1.0.3 a própria Microsoft analisou o executável no portal de segurança (WDSI, submission `ff1b8e70-808d-45bf-8ccf-2ede834c4fad`) e atestou **No malware detected**; a 1.0.5 também foi enviada à Microsoft em 06/11/2026 (submission `276f452b-5e63-4ad1-a770-050cc5b1a40c`) e o analista não encontrou detecção nem telemetria; o Defender com a definição 1.463.166.0 também não acusa o instalador.

Todos os executáveis da Noé VFX Labs ficam na [coleção pública no VirusTotal](https://www.virustotal.com/gui/collection/4baaa336745245003c10c490999bab6c0e45ccbd5579ea716ce6b25407cacc65).

Os hashes dos zips e os relatórios completos estão em [`docs/SECURITY_AND_HASHES_v1.0.5.md`](docs/SECURITY_AND_HASHES_v1.0.5.md). A auditoria técnica, o que foi corrigido e o que ainda depende de assinatura de código estão em [`docs/SECURITY_AUDIT_v1.0.3.md`](docs/SECURITY_AUDIT_v1.0.3.md).

> [!IMPORTANT]
> A correção definitiva para SmartScreen e para os alertas de *machine learning* é a **assinatura Authenticode**. Até lá, o Windows pode exibir "Editor desconhecido". O `build.ps1` já assina automaticamente quando `NETMONITOR_SIGN_PFX` e `NETMONITOR_SIGN_PASSWORD` estão configurados.

---

## Compilar

```powershell
# na pasta NetMonitor
.\build.ps1
```

Gera `bin/` e os pacotes em `release/v1.0.5/` (instalador, portátil e pacote completo, cada zip com este README).

Assinatura Authenticode (opcional, recomendada para distribuição):

```powershell
$env:NETMONITOR_SIGN_PFX = "C:\caminho\certificado.pfx"
$env:NETMONITOR_SIGN_PASSWORD = "senha-do-certificado"
.\build.ps1
```

O script assina `NetMonitor.exe` e `uninstall.exe` **antes** de embuti-los no instalador e assina o `NetMonitorSetup.exe` no final. Requer `signtool.exe` no PATH.

---

## Autor

**Noé VFX** — [noevfx.com](https://noevfx.com) · [noevfx.com/scripts](https://noevfx.com/scripts) · [labs@noevfx.com](mailto:labs@noevfx.com)

Gostou? Apoie pelo **Pix** na aba *Sobre* do app — chave `a772dce2-7cf6-45db-a699-2102e189b30b` (Noé VFX).

## Licença

MIT — veja [LICENSE](LICENSE).
