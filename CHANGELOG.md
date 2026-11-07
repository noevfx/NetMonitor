# Changelog — NetMonitor

Versionamento semântico (`MAJOR.MINOR.PATCH`). Cada versão tem sua pasta em `release/vX.Y.Z/`.

---

## [1.0.5] — 2026-11-06

### Corrigido
- A versão interna do executável e do instalador agora diz **1.0.5** (a 1.0.4 ainda se identificava como 1.0.3 em "Programas" e nas propriedades do arquivo).

### Alterado
- Fabricante, publicador e copyright dos executáveis e da `LICENSE` passam a ser **Noé VFX** (antes, o nome pessoal do autor). Licença continua MIT.

---

## [1.0.4] — 2026-11-06

### Alterado
- O link do GitHub no rodapé e em Sobre agora aponta para `github.com/noevfx` (nova conta). Nenhuma outra mudança. (Build enviado à Microsoft; internamente ainda se identificava como 1.0.3, corrigido na 1.0.5.)

---

## [1.0.3] — 2026-11-02

### Desempenho
- Widget ocioso: **~38% menos CPU** (1,67% → 1,04% de um núcleo) e **~14% menos memória** (40,1 → 34,3 MB). A verificação de tela cheia só refaz o trabalho quando o primeiro plano muda; o tema deixou de ser lido do registro a cada segundo; TOPMOST e reposicionamento foram espaçados.
- Flyout e prévia das Configurações reaproveitam bitmap e fontes. Fim da alocação de ~633 KB por segundo no LOH a cada flyout aberto.
- Configurações: abas criadas sob demanda, tick sem redesenho quando nada muda, tema com validade curta e `Changed()` sem `UpdateLayout` síncrono (arrastar sliders não engasga mais).
- Coleta: buffers nativos reaproveitados em `GetExtendedTcpTable`/`GetExtendedUdpTable`, leitura por offset (sem `PtrToStructure` por linha) e menos alocações por amostra.
- `LayeredWindow` reutiliza DC/DIB por janela; o widget reaproveita bitmap e métricas de layout.
- Caches de cor (`Pal.Resolve`, `K.C`, `Hex`), formatação de velocidade e DPI.

### Corrigido
- Widget travado: `RenderIfChanged` agora atualiza a formatação antes de comparar a chave.
- Normalização da amostragem não altera mais o dicionário durante a enumeração (exceção a cada segundo no .NET Framework).
- Log de emergência em `%LOCALAPPDATA%\NetMonitor\crash.log` para exceções de UI, threads de fundo e tarefas.
- Instalador preserva as preferências existentes em atualizações (idioma, trava de posição e posição customizada).
- Desinstalador solicita elevação e remove a pasta sem `cmd.exe`; o que estiver em uso sai no próximo reinício.
- `Mem.Trim` não força mais `GC.Collect` na inicialização.

### Segurança e transparência
- Atalhos criados por `IShellLink`/`IPersistFile` estáticos; removidos `WScript.Shell`, `ProgID`, `Activator` e `dynamic`.
- Removido `cmd.exe` oculto do desinstalador (T1059) e o uso de WMI do script de desenvolvimento (T1047).
- Encerramento gracioso de instâncias (`CloseMainWindow` antes de `Kill`).
- Manifestos explícitos: `asInvoker` no app/instalador e `requireAdministrator` no desinstalador; `supportedOS` Windows 8.1/10/11.
- Versão, empresa e descrição presentes nos três executáveis.
- `build.ps1` assina automaticamente quando `NETMONITOR_SIGN_PFX`/`NETMONITOR_SIGN_PASSWORD` estão configurados.
- Auditoria completa em `docs/SECURITY_AUDIT_v1.0.3.md` e plano de performance em `docs/PLANO_PERFORMANCE.md`.

---

## [1.0.2] — 2026-10-31

### Corrigido
- Unificação de processos por aplicativo: múltiplos processos do mesmo aplicativo (como `ollama.exe`, `ollama app.exe` e `llama-server.exe`, ou múltiplos processos do Edge, Chrome e VS Code) agora são agregados e unificados em uma única linha no Flyout, somando o tráfego total de rede de todos os seus processos filhos e auxiliares.
- Calibração bidirecional de Upload e Download: a amostragem de processos agora é calibrada proporcionalmente pela velocidade real da placa de rede física (`physRx` e `physTx`), garantindo que o consumo exibido por aplicativo feche com 100% de precisão com o total da placa, do widget e do rodapé.
- Medição de sockets assíncronos no Windows: adicionado suporte a `OtherTransferCount` (Winsock IOCP / AFD) para capturar o fluxo de rede de aplicações assíncronas (como Ollama, Node, Go e navegadores), eliminando a leitura de download zerada (0,0 Mbps).
- Isolamento de I/O de disco: gravação de arquivos baixados em disco é isolada da rede, eliminando falsos picos de upload decorrentes de escrita no SSD.

---

## [1.0.1] — 2026-10-25

### Corrigido
- O widget agora acompanha a barra de tarefas: some quando ela some e volta junto. Antes ficava por cima de tudo em tela cheia. Cobre navegador com F11, jogos (exclusivos ou em janela sem borda), vídeos, preview em tela cheia do Premiere/After Effects, apresentações e a barra com "ocultar automaticamente" recolhida. Vale só para o monitor do widget.
- O assunto do e-mail de feedback (aba Sobre) usa a versão atual em vez de "v1.0.0" fixo.

---

## [1.0.0] — 2026-10-25

Primeira versão oficial: o protótipo em HTML (Kimi) e a versão que só abria esse HTML no Edge (Gemini) foram substituídos por um app 100% nativo. Os fontes da fase anterior estão em `_backups/2026-10-24_pre-wpf/`.

### Arquitetura
- C# 5 + WPF + GDI+, compilado com o `csc.exe` do .NET Framework 4.8. Sem WebView2, sem HTML em tempo de execução, sem dependências.
- Dois processos: widget (Windows Forms + GDI+, ~12–18 MB) e janela de Configurações WPF sob demanda (`--settings`, ~135 MB, liberados ao fechar), ligados por `WM_COPYDATA`.
- Widget e flyout como janelas *layered* (`UpdateLayeredWindow`, ARGB pré-multiplicado): bordas suaves e área inteira clicável — corrige o menu de contexto que só abria em um pixel específico.
- Versão em fonte única: `version.txt` + `src/Version.g.cs`.

### Widget
- Encaixe automático à esquerda da área de notificação; arrastar grava posição própria; *Redefinir posição* volta ao automático.
- Duas linhas ou uma, ordem das taxas, fundo transparente/acrílico/sólido, borda opcional.
- Bits ou bytes, escala automática ou individual, opção de ocultar a unidade (unidade a 90% de opacidade).
- Fonte padrão Segoe UI Variable 10,5 pt.

### Cores & tema
- 8 combinações (Monocromático padrão, Cor de Destaque, Lima & Ciano, Âmbar & Céu, Aurora, Rosa & Esmeralda, Fogo & Gelo, Azul & Magenta), criadas no escuro e recalculadas para o claro com contraste mínimo; Lima & Ciano com tons do claro definidos à mão (#7AC600 / #0AA6E6).
- Cores personalizadas por sentido e switch *Inverter cores* (vale para o widget e as cores personalizadas, não para os cartões das combinações).
- Tema Seguir Windows / Escuro / Claro; widget, flyout e menu de contexto (mesmo aberto) trocam na hora.

### Setas
- 6 estilos (sólidos, finas, vazados, chevrons, TX:/RX:, personalizado); caractere próprio aceita `U+`, `\u`, `0x` e `&#x;`.
- Posição à esquerda/direita e tamanho com − / +.

### Flyout dos apps
- Top 5 com numeração, barras e colunas TX/RX alinhadas; modo separado ou combinado.
- Ao passar o mouse ou fixado aberto; pode ser desativado.
- Rodapé: *Configurações* (no azul da marca) à esquerda e total da máquina à direita.

### Menu de contexto
- Seções Unidade, Escala, Flyout e Mais opções, com o visual da paleta do app (cantos arredondados, ícones próprios).

### Configurações
- 7 páginas com prévia ao vivo, consumo do próprio app, Salvar / Cancelar / Restaurar.

### Instalador
- Assistente WPF que segue o tema do Windows, PT/EN, instala sem administrador em `%LocalAppData%\Programs\NetMonitor`, com desinstalador.
