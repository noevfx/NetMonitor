# Changelog — NetMonitor

Versionamento semântico (`MAJOR.MINOR.PATCH`). Cada versão tem sua pasta em `release/vX.Y.Z/`.

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
