# NetMonitor v1.0.3 — Plano de Performance e Consumo

> **Status:** **implementado na 1.0.3** (02/11/2026). As seções 3 a 12 preservam o plano original para consulta e segunda opinião; a seção 17 registra o que foi feito, os números medidos e a regressão corrigida.
> **Objetivo:** reduzir CPU, memória e latência de abertura, preservando 100% da fidelidade visual.
> **Documento para segunda opinião:** as seções 3 a 12 trazem as propostas com código; a seção 14 lista as premissas que eu gostaria que fossem contestadas.

---

## 1. Escopo

Análise de `src\` inteiro:

| Arquivo | Relevância para o plano |
|---|---|
| `NetMonitor.cs` | Widget, timer de 300 ms, amostragem por processo, `LayeredWindow`, flyout |
| `WidgetRenderer.cs` | Desenho do widget (GDI+) |
| `FlyoutRenderer.cs` | Desenho do flyout (GDI+) e conversor SVG |
| `SettingsWindow.cs` | Janela WPF de configurações |
| `SettingsProcess.cs` | Processo separado das configurações e IPC |
| `WpfKit.cs` | Pincéis, ícones e controles WPF |
| `Settings.cs` | Modelo de configurações e matemática de cor (`Pal`) |
| `Data.cs` | Item de processo e formatação de velocidade |
| `Ipc.cs` | `WM_COPYDATA` entre widget e configurações |
| `Icons.cs` | Paths SVG dos ícones |
| `InstallerApp.cs`, `Uninstaller.cs` | Fora do escopo (executados raramente) |

**Fora do escopo:** trocar a arquitetura do widget para Direct2D/WPF, remover o alpha por pixel, mudar o layout, mudar o instalador.

---

## 2. Método e linha de base

Medições feitas na máquina de desenvolvimento, com a v1.0.2 compilada instalada e rodando:

| Cenário | Medição | Como foi medido |
|---|---|---|
| Widget ocioso (sem flyout, sem configurações) | **250 ms de CPU em 15 s = 1,67% de um núcleo** | `TotalProcessorTime` antes/depois de 15 s |
| Widget ocioso — memória | **40,1 MB**, 7 threads, 375 handles | `WorkingSet64`, `Threads.Count`, `HandleCount` |
| Abertura das Configurações | **~1898 ms** até a janela aparecer | `Start-Process` + polling de `MainWindowHandle` a cada 25 ms |
| Configurações logo após abrir | **1969 ms de CPU em ~3 s**; **131,2 MB** | `TotalProcessorTime` e `WorkingSet64` do processo `--settings` |

**Natureza das estimativas.** Os números de linha de base são medidos. Os ganhos por item são estimativas de engenharia, não medições — a seção 13 explica como validar cada fase.

**Ambiente de build.** `scripts\build.ps1` usa `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. Isso limita o código a **C# 5**: nada de interpolação de string, `?.`, `nameof` ou membros com corpo de expressão nas propostas abaixo.

---

## 3. Resumo ordenado por ganho

| # | Ação | Arquivo:linha | Ganho esperado | Risco |
|---|---|---|---|---|
| 1 | Reaproveitar bitmap e fontes do flyout/prévia | `FlyoutRenderer.cs:211-250`, `NetMonitor.cs:2072`, `SettingsWindow.cs:1351` | Elimina ~0,6 MB/s de LOH por flyout aberto (até ~2,5 MB/s com configurações abertas) | Baixo |
| 2 | Parar o "tick storm" das configurações | `SettingsWindow.cs:140, 1427, 1064` | Deve cortar a maior parte dos 1969 ms/3 s | Baixo |
| 3 | Lazy loading das abas | `SettingsWindow.cs:429-470` | 1898 ms → meta 800–1000 ms; −15 a 25 MB | Médio |
| 4 | Menos chamadas de sistema por tick do widget | `NetMonitor.cs:940-994, 542-688` | 1,67% → meta 0,4–0,6% de núcleo | Baixo |
| 5 | Descartar/cachear `StringFormat` | `WidgetRenderer.cs:116, 239`, `FlyoutRenderer.cs:241` | Menos pressão de finalização e de memória nativa GDI+ | Baixo |
| 6 | Cache de superfície do `LayeredWindow` | `NetMonitor.cs:1977-2008` | Elimina DC + DIB por frame da animação (~66/s) | Médio |
| 7 | Reaproveitar buffers do `GetExtendedTcpTable` | `NetMonitor.cs:746-894` | −8 alocações nativas/segundo, −4 chamadas, sem `PtrToStructure` por linha | Médio |
| 8 | Reduzir alocações da amostragem | `NetMonitor.cs:1473-1571` | ~30–50 KB/s → ~10 KB/s | Baixo |
| 9 | Caches de cor, formatação, DPI e tema | `WpfKit.cs:49`, `Settings.cs:67-181`, `WidgetRenderer.cs:36`, `NetMonitor.cs:336` | Corta `ColorConverter`, `FitContrast` e leitura de registro repetidos | Baixo |
| 10 | Ajustes menores | `NetMonitor.cs:1825, 1863, 2199`, `SettingsWindow.cs:146` | Menos objetos finalizáveis, mais fluidez | Baixo |

Os números de linha se referem à v1.0.2 **depois** do patch de `CrashLog` já aplicado.

---

## 4. Por que o flyout é o maior gerador de lixo

`FlyoutRenderer.Render` (`FlyoutRenderer.cs:211`) cria um `Bitmap` de aproximadamente:

```
W = 400 * (dpi/96) + 2*margem
H ≈ 18 + 30 + 10 + 8 + 5*56 + 8 + 24 + 18  ≈ 396
bytes = 400 * 396 * 4  ≈ 633 KB
```

633 KB por render. Como é maior que 85 KB, **cada bitmap vai para o LOH** (Large Object Heap). O flyout é redesenhado:

- a cada segundo enquanto visível (`NetMonitor.cs:1541`, via `BeginInvoke` da amostragem);
- a cada mudança de linha sob o mouse (`NetMonitor.cs:2145`);
- e, nas Configurações, a cada `RefreshPreview` (`SettingsWindow.cs:1351`), que roda a cada 500 ms.

Com configurações abertas, são 2 bitmaps de widget + 2 de flyout por segundo, mais dois `BitmapSource.Create` de tamanho equivalente em `ToSource` (`SettingsWindow.cs:1334`) e um `new WriteableBitmap`-like por chamada. Isso explica em parte os 131 MB e os ~2 s de CPU em 3 s.

Além do bitmap, cada render cria e destrói **10 fontes** (`FlyoutRenderer.cs:242-250`) e 7 `GraphicsPath` de sombra.

---

## 5. Item 1 — Reaproveitar bitmap e fontes

### 5.1 Fontes em cache

Substitui `F()` (`FlyoutRenderer.cs:191-195`) e o bloco `using` de fontes (242-250).

```csharp
[ThreadStatic] static Dictionary<string, Font> fontCache;

static Font CachedFont(string fam, float px, FontStyle st)
{
    if (fontCache == null) fontCache = new Dictionary<string, Font>();
    string key = fam + "|" + px.ToString("0.##") + "|" + (int)st;
    Font f;
    if (fontCache.TryGetValue(key, out f)) return f;
    if (fontCache.Count > 32)
    {
        foreach (Font old in fontCache.Values) old.Dispose();
        fontCache.Clear();
    }
    f = F(fam, px, st);
    fontCache[key] = f;
    return f;
}
```

Uso: trocar `using (Font fTitle = F(...))` por `Font fTitle = CachedFont(...)` e **não** descartar. O bloco `using` externo precisa ser reescrito sem as declarações de fonte.

Ponto de atenção: o laço que reduz o título (`FlyoutRenderer.cs:284-293`) cria e descarta fontes a cada passo de 0,5 px. Com cache, fazer o laço medindo com `CachedFont` e só fixar a fonte final após o laço.

### 5.2 Render em bitmap reaproveitado

```csharp
public static Bitmap Render(AppSettings s, bool dark, List<ProcessTrafficItem> items,
    double totUpBps, double totDnBps, float dpi, int hover, int margin, out FlyoutHit hit)
{
    return RenderTo(null, s, dark, items, totUpBps, totDnBps, dpi, hover, margin, out hit);
}

public static Bitmap RenderTo(Bitmap reuse, AppSettings s, bool dark, List<ProcessTrafficItem> items,
    double totUpBps, double totDnBps, float dpi, int hover, int margin, out FlyoutHit hit)
{
    // ... mesmo cálculo de W, H, cores e fontes de hoje ...
    Bitmap bmp = (reuse != null && reuse.Width == bw && reuse.Height == bh)
        ? reuse
        : new Bitmap(bw, bh, PixelFormat.Format32bppPArgb);
    bmp.SetResolution(dpi, dpi);
    using (Graphics g = Graphics.FromImage(bmp))
    {
        g.Clear(Color.Transparent);   // limpa o conteúdo anterior ao reutilizar
        // ... resto do desenho idêntico ...
    }
    return bmp;
}
```

### 5.3 FlyoutForm

```csharp
private int bmpVersion;

private void Redraw()
{
    FlyoutHit h;
    Bitmap nb = FlyoutRenderer.RenderTo(bmp, s, dark, items, up, dn, dpi, hoverRow, margin, out h);
    if (!ReferenceEquals(nb, bmp))
    {
        if (bmp != null) bmp.Dispose();
        bmp = nb;
    }
    hit = h;
    bmpVersion++;     // sinaliza ao LayeredWindow que os pixels mudaram
    Present();
}
```

O descarte do bitmap antigo precisa ser explícito: quando o tamanho muda (DPI, número de linhas), `RenderTo` devolve outra instância e a antiga vazaria.

### 5.4 Prévia das Configurações

- Só desenhar quando a barra lateral estiver visível. `SizeChanged_` (`SettingsWindow.cs:203`) define `railCol.Width = 0` abaixo de 1280 px; hoje a prévia continua sendo desenhada e convertida em `BitmapSource` mesmo invisível.
- Guardar `Bitmap previewBuf` e `Bitmap flyoutBuf` como campos e usar `RenderTo`. Descartar quando o tamanho/DPI mudar.
- Opcional: reutilizar um `WriteableBitmap` em `ToSource` (`SettingsWindow.cs:1334`) em vez de criar um `BitmapSource` novo por frame. Só vale se as dimensões e o DPI não mudarem.

---

## 6. Item 2 — Tick das Configurações

### 6.1 O problema

`liveTimer` roda a cada 500 ms (`SettingsWindow.cs:140`) e em cada tick:

| Chamada | Custo |
|---|---|
| `RefreshPreview()` | 2 renders GDI+ completos + 2 `BitmapSource.Create` |
| `RebuildApps()` (se aba apps) | reconstrói a árvore visual inteira 2×/s (`1064-1115`) |
| `Process.GetCurrentProcess()` | cria objeto finalizável por tick (`1436`) |
| `host.SystemDark()` | abre chave do registro por tick (`SettingsProcess.cs:33`) |
| `S.IsDark(...)` | idem |

E `RefreshPreview` compara a **referência** da lista (`ReferenceEquals(top, previewTop)`, linha 1364). Como `OnData` cria uma lista nova a cada mensagem (`SettingsProcess.cs:76`), a prévia nunca considera "sem mudança".

### 6.2 Impressão digital da lista

```csharp
static string TopKey(List<ProcessTrafficItem> l)
{
    if (l == null) return "";
    StringBuilder sb = new StringBuilder();
    int n = Math.Min(5, l.Count);
    for (int i = 0; i < n; i++)
        sb.Append(l[i].Name).Append('|').Append(l[i].TxBytes).Append('|').Append(l[i].RxBytes).Append(';');
    return sb.ToString();
}
```

Na chave da prévia (`SettingsWindow.cs:1362`):

```csharp
string key = un + uu + dn + du + dd + TopKey(top);
```

### 6.3 Tema com validade curta

```csharp
bool sysDarkCache = true;
DateTime sysDarkAt = DateTime.MinValue;

bool SysDark()
{
    if ((DateTime.UtcNow - sysDarkAt).TotalSeconds > 3)
    {
        sysDarkCache = host.SystemDark();
        sysDarkAt = DateTime.UtcNow;
    }
    return sysDarkCache;
}
```

Trocar as chamadas de `host.SystemDark()` em `RefreshPreview`, `RebuildApps`, `LiveTick` e nos delegates `upd` de `PageCores`.

### 6.4 `LiveTick` enxuto

```csharp
string appsKey = "";
DateTime ramWinAt = DateTime.MinValue;

void LiveTick()
{
    try
    {
        if (SysDark() != K.Dark) { Changed(); return; }

        if (railVisible) RefreshPreview();

        double cpu, ram; host.PerfInfo(out cpu, out ram);
        lblCpu.Text = cpu.ToString("0.0") + "%";
        lblRam.Text = ram.ToString("0.0") + " MB";

        if ((DateTime.UtcNow - ramWinAt).TotalSeconds > 2)
        {
            ramWinAt = DateTime.UtcNow;
            lblRamWin.Text = (Process.GetCurrentProcess().WorkingSet64 / 1048576.0).ToString("0") + " MB";
        }

        if (curPage == "apps")
        {
            string k = TopKey(host.TopApps());
            if (k != appsKey) { appsKey = k; RebuildApps(); }
        }
    }
    catch (Exception ex) { K.Log(ex); }
}
```

`railVisible` é campo novo, atualizado em `SizeChanged_`.

### 6.5 `Changed()` sem `UpdateLayout` síncrono

Hoje (`SettingsWindow.cs:216-226`) cada `Changed()` força `root.InvalidateVisual(); UpdateLayout();`. O slider de fonte (`SettingsWindow.cs:911-912`) chama `Changed` a cada movimento do mouse; com layout síncrono e 16 conversões de cor por preset, o arrasto engasga.

Proposta:

- remover as duas linhas de layout forçado — o WPF faz o layout no frame seguinte de qualquer forma;
- adiar `host.Apply(...)` e `RefreshPreview()` com um `DispatcherTimer` de 60–80 ms (debounce), para não enviar um `WM_COPYDATA` e redesenhar a prévia a cada pixel de arrasto;
- manter `ApplyAll()` síncrono, para os controles refletirem o valor imediatamente.

```csharp
DispatcherTimer changeDebounce;

void Changed()
{
    K.SetTheme(S.IsDark(SysDark()));
    ApplyAll();

    if (changeDebounce == null)
    {
        changeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
        changeDebounce.Tick += delegate
        {
            changeDebounce.Stop();
            ApplyChrome();
            host.Apply(S.Clone(), false);
            previewDirty = true;
            RefreshPreview();
        };
    }
    changeDebounce.Stop();
    changeDebounce.Start();
}
```

---

## 7. Item 3 — Lazy loading das abas

### 7.1 O problema

`BuildContent` (`SettingsWindow.cs:429-470`) constrói as 7 abas de uma só vez (linhas 436-442) e `ApplyAll` roda os 21 delegates `upd` antes da janela aparecer. A árvore visual inicial é grande, e cada `Ico` cria um `Canvas` com vários `Path` (o cache em `WpfKit.cs:213` guarda as geometrias, não os visuais).

### 7.2 Proposta

Campo novo:

```csharp
StackPanel pagesHost;
```

Em `BuildContent`, substituir as linhas 436-444 por:

```csharp
pagesHost = new StackPanel();
```

E no `ScrollViewer` (linha 446), usar `pagesHost` no lugar de `body`.

Método novo:

```csharp
void EnsurePage(string k)
{
    if (pages.ContainsKey(k)) return;
    int before = upd.Count;

    FrameworkElement pg = null;
    if (k == "cores") pg = PageCores();
    else if (k == "layout") pg = PageLayout();
    else if (k == "fonte") pg = PageFonte();
    else if (k == "setas") pg = PageSetas();
    else if (k == "apps") pg = PageApps();
    else if (k == "geral") pg = PageGeral();
    else if (k == "sobre") pg = PageSobre();

    if (pg == null) return;
    pg.Visibility = Visibility.Collapsed;
    pages[k] = pg;
    pagesHost.Children.Add(pg);

    for (int i = before; i < upd.Count; i++) { try { upd[i](); } catch { } }
}
```

Em `ApplyPage` (linha 401), primeira linha:

```csharp
EnsurePage(curPage);
```

### 7.3 Por que a fidelidade se mantém

Cada aba continua sendo o mesmo `FrameworkElement`, no mesmo `StackPanel`, com a mesma ordem visual. A única diferença é o momento de criação. `ApplyPage` já usa `TryGetValue` para páginas ausentes (linha 409), então não precisa de mais guardas.

### 7.4 Riscos conhecidos

- `PageApps` e `PageSobre` usam campos de instância (`appsList`, `appsCount`, `imgFlyout`, etc.). `EnsurePage` garante que são criados antes de `RebuildApps` — `ApplyPage` chama `EnsurePage` antes de `RebuildApps`.
- Delegates `upd` registrados por uma aba só existem depois que ela é construída. `EnsurePage` executa os novos delegates na hora, o que mantém o estado correto.
- `Titles` (linhas ~390) continua sendo a fonte de verdade da navegação; nada muda.

---

## 8. Item 4 — Widget: menos chamadas de sistema por tick

### 8.1 O problema

O timer de 300 ms (`NetMonitor.cs:940-994`) executa, a cada tick:

| Chamada | Linha | Frequência |
|---|---|---|
| `IsFullscreenAppActive()` | 952 | 3,3×/s |
| `SetWindowPos(TOPMOST)` | 989 | 3,3×/s |
| `IsWindowsDarkTheme()` (registro) | 979 | ~1,1×/s |
| `AutoAnchor()` (`FindWindow` + 2 `GetWindowRect`) | 982 | ~1,1×/s |
| `IsAnyScreensaverActive()` | 950 | ~1,1×/s |

`IsFullscreenAppActive` (`542-566`) faz `FindWindow`, `SHQueryUserNotificationState`, `GetForegroundWindow`, `GetClassName`, `MonitorFromWindow`, `GetMonitorInfo`, `GetWindowRect`; `IsTaskbarAutoHidden` (`569-580`) soma outro `FindWindow` e mais `GetWindowRect`/`GetMonitorInfo`.

`IsAnyScreensaverActive` (`595-688`) sempre chega ao passo 4 no caso normal (sem protetor ativo): `CreateToolhelp32Snapshot` + varredura de todos os processos com `PROCESSENTRY32`, que tem um campo string `[MarshalAs(ByValTStr)]` — ou seja, **uma string gerenciada por processo, a cada ~900 ms**.

### 8.2 DPI em cache

```csharp
private float dpiCache = 0f;

private float GetDpi()
{
    if (dpiCache > 0f) return dpiCache;
    try { using (Graphics g = this.CreateGraphics()) dpiCache = g.DpiX; }
    catch { dpiCache = 96f; }
    return dpiCache;
}
```

Invalidar com `dpiCache = 0f` no `WM_DPICHANGED` (`NetMonitor.cs:1028`), antes de `MeasureWidget()`.

### 8.3 Tela cheia só quando o primeiro plano muda

```csharp
private IntPtr lastForeground = IntPtr.Zero;
private bool fullscreenCache;
private DateTime fullscreenAt = DateTime.MinValue;

private bool FullscreenNow()
{
    IntPtr fg = GetForegroundWindow();
    bool stale = (DateTime.UtcNow - fullscreenAt).TotalSeconds > 2;
    if (fg != lastForeground || stale)
    {
        lastForeground = fg;
        fullscreenAt = DateTime.UtcNow;
        fullscreenCache = IsFullscreenAppActive();
    }
    return fullscreenCache;
}
```

Na linha 952: `bool fullscreen = FullscreenNow();`.

### 8.4 Tema sem polling

Já existem `SystemEvents.UserPreferenceChanged` (`315`) e `WM_SETTINGCHANGE` (`1010`). A leitura de registro na linha 979 pode ser removida ou mantida como rede de segurança a cada 10 s.

### 8.5 TOPMOST e âncora

`SetWindowPos` na linha 989 e `AutoAnchor` na 982 podem seguir a mesma lógica de "mudou o primeiro plano ou passaram 2 s". `WM_WINDOWPOSCHANGING` (`1035-1043`) já reafirma o TOPMOST em qualquer movimentação da janela.

### 8.6 Protetor de tela

Separar as verificações baratas (SPI `SPI_GETSCREENSAVERRUNNING`, `FindWindow("WindowsScreenSaverClass")`, processo em primeiro plano) das caras (varredura Toolhelp, linhas 638-683). As caras passam a rodar a cada 10 s. Dentro da varredura, substituir `Process.GetProcessById` (`654`) por `OpenProcess` + `QueryFullProcessImageName`, ambos já declarados no arquivo.

---

## 9. Item 5 — `StringFormat` não descartado

**Fatos:**

- `WidgetRenderer.cs:116`: `StringFormat tf = StringFormat.GenericTypographic;` — a propriedade cria uma instância nova a cada acesso, e ela não é descartada.
- `WidgetRenderer.cs:239` (em `DrawGroup`): o mesmo, uma vez por linha desenhada (2 por render).
- `FlyoutRenderer.cs:241`: `using (StringFormat tf = (StringFormat)StringFormat.GenericTypographic.Clone())` descarta o clone, mas **não** o original retornado pela propriedade.

Resultado: 3 objetos GDI+ com finalizador por render do widget, até ~10/s em tráfego alto, além de 1 por render do flyout.

**Proposta:**

```csharp
[ThreadStatic] static StringFormat tfTypo;

static StringFormat Typo()
{
    if (tfTypo == null)
    {
        tfTypo = (StringFormat)StringFormat.GenericTypographic.Clone();
        tfTypo.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
    }
    return tfTypo;
}
```

Usar `StringFormat tf = Typo();` em `WidgetRenderer.Render`, em `DrawGroup` e em `FlyoutRenderer.Render`. Não descartar (é cache de vida longa do thread).

Observação: isso explica pressão de memória nativa e de finalização, não necessariamente um vazamento ilimitado — o GC do .NET Framework finaliza esses objetos. Mas, em picos, a criação de recursos GDI+ pode falhar com `OutOfMemoryException`, que é um dos sintomas investigados no crash silencioso.

---

## 10. Item 6 — Superfície do `LayeredWindow`

`Present` (`NetMonitor.cs:1977-2008`) cria `GetDC`, `CreateCompatibleDC`, `CreateDIBSection`, copia os pixels, e destrói tudo — a cada chamada. Durante a animação do flyout (`AnimTick`, 15 ms) são ~66 por segundo.

Proposta: uma superfície por janela, recriada só quando o tamanho muda.

```csharp
class Surface
{
    public IntPtr Mem, Dib, Old, Bits;
    public int W, H, LastVersion = -1;
}

static readonly Dictionary<IntPtr, Surface> surfaces = new Dictionary<IntPtr, Surface>();

public static void Present(IntPtr hwnd, Bitmap bmp, int x, int y, byte alpha, int version)
{
    IntPtr screen = GetDC(IntPtr.Zero);
    try
    {
        Surface sf;
        if (!surfaces.TryGetValue(hwnd, out sf) || sf.W != bmp.Width || sf.H != bmp.Height)
        {
            if (sf != null) FreeSurface(sf);
            sf = CreateSurface(screen, bmp.Width, bmp.Height);
            if (sf == null) return;
            surfaces[hwnd] = sf;
        }

        if (sf.LastVersion != version)
        {
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try { CopyMemory(sf.Bits, bd.Scan0, (UIntPtr)(uint)(bd.Stride * bmp.Height)); }
            finally { bmp.UnlockBits(bd); }
            sf.LastVersion = version;
        }

        PT dst = new PT { x = x, y = y };
        PT src = new PT { x = 0, y = 0 };
        SZ sz = new SZ { cx = bmp.Width, cy = bmp.Height };
        BLEND bf = new BLEND { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
        UpdateLayeredWindow(hwnd, screen, ref dst, ref sz, sf.Mem, ref src, 0, ref bf, 2);
    }
    finally { ReleaseDC(IntPtr.Zero, screen); }
}

static Surface CreateSurface(IntPtr screen, int w, int h)
{
    IntPtr mem = CreateCompatibleDC(screen);
    BMIH bi = new BMIH();
    bi.biSize = (uint)Marshal.SizeOf(typeof(BMIH));
    bi.biWidth = w;
    bi.biHeight = -h;
    bi.biPlanes = 1;
    bi.biBitCount = 32;
    IntPtr bits;
    IntPtr dib = CreateDIBSection(screen, ref bi, 0, out bits, IntPtr.Zero, 0);
    if (dib == IntPtr.Zero) { DeleteDC(mem); return null; }
    IntPtr old = SelectObject(mem, dib);
    return new Surface { Mem = mem, Dib = dib, Old = old, Bits = bits, W = w, H = h };
}

static void FreeSurface(Surface sf)
{
    if (sf.Old != IntPtr.Zero) SelectObject(sf.Mem, sf.Old);
    if (sf.Dib != IntPtr.Zero) DeleteObject(sf.Dib);
    if (sf.Mem != IntPtr.Zero) DeleteDC(sf.Mem);
}

public static void Release(IntPtr hwnd)
{
    Surface sf;
    if (surfaces.TryGetValue(hwnd, out sf)) { FreeSurface(sf); surfaces.Remove(hwnd); }
}
```

Chamar `LayeredWindow.Release(this.Handle)` em `NetMonitorApp.OnFormClosing` (linha 1878) e em `FlyoutForm.Dispose` (linha 2163).

**Por que a versão é necessária:** com o bitmap reaproveitado, a animação só muda posição e alpha. Sem a versão, o `Present` não sabería distinguir "mesmo bitmap, pixels iguais" de "mesmo bitmap, pixels redesenhados". A versão resolve isso sem `ReferenceEquals` (que daria falso positivo quando o buffer é reutilizado).

**Thread safety:** `surfaces` é acessado apenas na thread de UI (widget e flyout). Não é thread-safe por design; documentar isso.

---

## 11. Item 7 — Buffers do `GetExtendedTcpTable`

### 11.1 O problema

`GetActiveNetworkPids` (`NetMonitor.cs:746-894`) faz, para cada uma das 4 tabelas:

1. uma chamada só para descobrir o tamanho;
2. `AllocHGlobal`;
3. uma segunda chamada;
4. `Marshal.SizeOf` (recalculado a cada chamada);
5. `PtrToStructure` por linha — nas tabelas IPv6 a struct tem `[MarshalAs(ByValArray)]`, o que aloca arrays gerenciados por linha;
6. `FreeHGlobal`.

São 8 chamadas nativas, 4 alocações e dezenas de marshallings por segundo enquanto o flyout está visível.

### 11.2 Offsets das structs (conferem com `NetMonitor.cs:136-189`)

| Struct | Tamanho | Campos |
|---|---|---|
| `MIB_TCPROW_OWNER_PID` | 24 | state 0, localAddr 4, localPort 8, remoteAddr 12, remotePort 16, pid 20 |
| `MIB_TCP6ROW_OWNER_PID` | 56 | localAddr 0–15, localScope 16, localPort 20, remoteAddr 24–39, remoteScope 40, remotePort 44, state 48, pid 52 |
| `MIB_UDPROW_OWNER_PID` | 12 | localAddr 0, localPort 4, pid 8 |
| `MIB_UDP6ROW_OWNER_PID` | 28 | localAddr 0–15, scope 16, localPort 20, pid 24 |

### 11.3 Proposta

```csharp
static IntPtr tcp4Buf, tcp6Buf, udp4Buf, udp6Buf;
static int tcp4Cap, tcp6Cap, udp4Cap, udp6Cap;
const int TCP4_ROW = 24, TCP6_ROW = 56, UDP4_ROW = 12, UDP6_ROW = 28;

static bool Fetch(bool udp, int af, ref IntPtr buf, ref int cap, out int count)
{
    count = 0;
    int need = 0;

    if (udp) GetExtendedUdpTable(IntPtr.Zero, ref need, false, af, UDP_TABLE_OWNER_PID, 0);
    else GetExtendedTcpTable(IntPtr.Zero, ref need, false, af, TCP_TABLE_OWNER_PID_ALL, 0);

    if (need > cap)
    {
        if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
        buf = Marshal.AllocHGlobal(need);
        cap = need;
    }
    if (cap <= 0) return false;

    int size = cap;
    uint r = udp
        ? GetExtendedUdpTable(buf, ref size, false, af, UDP_TABLE_OWNER_PID, 0)
        : GetExtendedTcpTable(buf, ref size, false, af, TCP_TABLE_OWNER_PID_ALL, 0);

    if (r != 0) return false;   // 122 (ERROR_INSUFFICIENT_BUFFER) = tabela cresceu; próxima chamada realoca

    count = Marshal.ReadInt32(buf);
    return true;
}
```

Leitura sem marshalling (exemplo TCP IPv4):

```csharp
if (Fetch(false, AF_INET, ref tcp4Buf, ref tcp4Cap, out int n))
{
    IntPtr row = (IntPtr)((long)tcp4Buf + 4);
    for (int i = 0; i < n; i++, row = (IntPtr)((long)row + TCP4_ROW))
    {
        int pid = Marshal.ReadInt32(row, 20);
        if (pid <= 4) continue;
        if (Marshal.ReadInt32(row, 0) != 5) continue;      // ESTABLISHED

        int rem = Marshal.ReadInt32(row, 12);
        int loc = Marshal.ReadInt32(row, 4);

        if (rem == 0) continue;
        if (filterLoopback && ((rem & 0xFF) == 127 || (loc & 0xFF) == 127)) continue;

        pids.Add(pid);
    }
}
```

Para IPv6, ler os 16 bytes com `Marshal.ReadByte(row, deslocamento + b)` até achar um byte diferente de zero, preservando exatamente a lógica atual (`isZero`, loopback `::1`, link-local `fe80::`).

### 11.4 Cuidados

- **Semântica intacta.** O teste `(rem & 0xFF) == 127` é o mesmo de hoje. Apesar de o endereço estar em ordem de rede, o teste atual funciona porque o Windows entrega o `DWORD` já na ordem que faz o byte baixo corresponder ao primeiro octeto. Não "corrigir" isso sem um teste dedicado.
- **Buffer estático não é thread-safe.** Só é válido porque a amostragem está serializada por `QueueSample` (patch já aplicado).
- **Reutilizar o `HashSet<int>`** como campo, com `Clear()` por amostra, em vez de alocar um novo.

---

## 12. Itens 8 a 10 — Amostragem, caches e ajustes

### 12.1 Amostragem (`NetMonitor.cs:1473-1571`)

```csharp
struct AppAcc { public ulong Tx, Rx; public int Pid; }
private readonly Dictionary<string, AppAcc> appAcc =
    new Dictionary<string, AppAcc>(StringComparer.OrdinalIgnoreCase);
```

No início de `DoProcessSampleWork`: `appAcc.Clear();`. Elimina 3 dicionários por segundo.

Cachear o nome unificado por PID (`cachedUnified`), porque `GetUnifiedAppName` (`1415-1457`) chama `GetProcessPath` — que abre o processo — para todo app desconhecido, a cada amostra.

Em `Normalize` (`1560-1571`), remover a `List<string> keys`: alterar o valor de uma chave existente durante `foreach (var k in d.Keys)` não invalida o enumerador (a versão só muda em add/remove).

Reutilizar `List<ProcessTrafficItem> items` e a lista temporária de remoção de PIDs como campos.

### 12.2 Caches de cor

`WpfKit.C` (`WpfKit.cs:49`) usa `ColorConverter.ConvertFromString` toda vez — inclusive em `SetTheme` (25 cores) e nos `upd` de `PageCores` (16 conversões por `Changed`).

```csharp
static readonly Dictionary<string, Color> colorCache = new Dictionary<string, Color>();

public static Color C(string hex)
{
    if (string.IsNullOrEmpty(hex)) return Colors.Transparent;
    Color c;
    if (colorCache.TryGetValue(hex, out c)) return c;
    try { c = (Color)ColorConverter.ConvertFromString(hex); }
    catch { c = Colors.Transparent; }
    colorCache[hex] = c;
    return c;
}
```

`WidgetRenderer.Hex` (`WidgetRenderer.cs:36`) recebe o mesmo tratamento, com chave `hex + "|" + alpha`.

`Pal.Resolve` (`Settings.cs:67`) pode chamar `WindowsAccent()` (leitura de registro) e `FitContrast`, que itera até 100 vezes com `Math.Pow`. Cachear o resultado por `key|up|dark` e memoizar o destaque do Windows por 30 s:

```csharp
static readonly Dictionary<string, string> resolved = new Dictionary<string, string>();
static string accentHex;
static DateTime accentAt = DateTime.MinValue;

public static void ClearCache()
{
    resolved.Clear();
    accentHex = null;
    accentAt = DateTime.MinValue;
}
```

Chamar `Pal.ClearCache()` em `ApplySystemTheme` (`NetMonitor.cs:394`) e quando `K.SetTheme` mudar de tema de fato.

### 12.3 Formatação duplicada

`RenderIfChanged` (`NetMonitor.cs:1227`) chama `RenderKey` (`491`), que formata as velocidades; `RenderWidget` chama `MakeBitmap` (`454`), que formata de novo; e no fim `RenderKey` outra vez (`486`). Guardar `upNum/upUnit/dnNum/dnUnit` como campos, calculados uma vez em `UpdateTraffic`, e usar em ambos.

### 12.4 `PerfInfo`

`Process.GetCurrentProcess()` (`NetMonitor.cs:1867`) cria um objeto com finalizador a cada chamada — a cada 300 ms com configurações abertas. Guardar `private readonly Process self = Process.GetCurrentProcess();` e reutilizar.

### 12.5 `Res` (Configurações)

`SettingsWindow.Res` (`146`) decodifica o PNG do recurso a cada chamada. Cachear `Dictionary<string, BitmapImage>`. Todas as imagens são `Freeze()`d, então podem ser compartilhadas.

### 12.6 Menu de contexto

`BuildContextMenu` (`NetMonitor.cs:1576`) reconstrói ~30 itens a cada clique direito. Marcar `menuDirty` em `Edit`, `ApplyInternal` e `RethemeMenu` e só reconstruir quando necessário.

### 12.7 `Mem.Trim`

`Mem.Trim` (`NetMonitor.cs:2199`) roda 6 s após o início e faz `GC.Collect()` + `WaitForPendingFinalizers` + `SetProcessWorkingSetSize`. Recomendo remover o `GC.Collect` (causa pausa e não reduz memória real) e manter apenas o ajuste de working set, ou remover o timer. O ajuste de working set melhora o número no Gerenciador de Tarefas, mas as páginas voltam a faltar no próximo uso.

### 12.8 `Ipc.Send`

`Ipc.cs:42` aloca um buffer nativo e uma string por mensagem (a cada ~300 ms com configurações abertas). Reutilizar um buffer fixo com `Marshal.Copy` elimina 2-3 alocações por segundo. Ganho pequeno.

---

## 13. Fases, validação e critérios de aceite

### Fase A — Configurações e flyout (itens 1, 2, 3)

Aceite:
- abertura das configurações ≤ 1000 ms na mesma máquina (baseline 1898 ms);
- CPU das configurações nos primeiros 3 s ≤ 900 ms (baseline 1969 ms);
- memória das configurações ≤ 110 MB (baseline 131 MB);
- flyout aberto por 60 s: `WorkingSet` estável, sem crescimento monotônico.

### Fase B — Widget e apresentação (itens 4, 5, 6)

Aceite:
- widget ocioso ≤ 0,7% de um núcleo (baseline 1,67%);
- handles estáveis por 30 min com o flyout abrindo e fechando;
- animação do flyout sem picos de CPU (medir `TotalProcessorTime` durante `ShowAnimated`).

### Fase C — Coleta (itens 7, 8)

Aceite:
- conteúdo do Top 5 idêntico ao da v1.0.2 em três cenários: navegador aberto, torrent, ocioso;
- nenhuma exceção no `crash.log`;
- alocação por amostra medida com `GC.GetTotalAllocatedBytes` (ou `GC.GetTotalMemory(false)` em intervalo) reduzida em pelo menos 50%.

### Fase D — Caches e ajustes (itens 9, 10)

Aceite:
- nenhuma diferença visual em claro/escuro, 100%/125%/150%/200% de DPI e nas 4 posições de seta;
- arrastar o slider de fonte não engasga (comparação visual).

### Comandos de medição

```powershell
# CPU e memória do widget
$p = Get-Process NetMonitor | Select-Object -First 1
$a = $p.TotalProcessorTime
Start-Sleep 15
$p.Refresh()
"Widget: {0:N2}% de 1 nucleo, {1:N1} MB, {2} handles" -f `
  ((($p.TotalProcessorTime - $a).TotalMilliseconds / 15000) * 100), `
  ($p.WorkingSet64 / 1MB), $p.HandleCount

# Tempo de abertura das Configurações
$exe = $p.Path
$sw = [Diagnostics.Stopwatch]::StartNew()
$s = Start-Process -FilePath $exe -ArgumentList "--settings","0","cores" -PassThru
while ($sw.ElapsedMilliseconds -lt 8000) {
    Start-Sleep -Milliseconds 25
    $s.Refresh()
    if ($s.HasExited -or $s.MainWindowHandle -ne [IntPtr]::Zero) { break }
}
$sw.Stop()
"Configuracoes em $($sw.ElapsedMilliseconds) ms, $([math]::Round($s.WorkingSet64/1MB,1)) MB"
if (-not $s.HasExited) { $s.Kill() }
```

Para GDI+: comparar `GetGuiResources(hProcess, 0)` (GDI) e `1` (USER) no início e depois de 10 minutos com o flyout aberto. Valores estáveis indicam que o item 5 resolveu a pressão.

---

## 14. Premissas que eu gostaria que fossem contestadas

1. **O gargalo das Configurações é o tick de 500 ms, não o layout inicial?** Medi CPU alta nos primeiros 3 s, mas não perfilei. Um perfil com `dotnet-trace`/`PerfView` (ou ETW do WPF) confirmaria a divisão entre construção, layout e ticks.
2. **Lazy loading das abas compensa o risco?** A janela é um processo descartável, aberta sob demanda. Se o usuário abre e fecha com frequência, o custo de construir a aba sob demanda se repete; se fica aberta, o lazy só adia. Minha hipótese é que a percepção de "abertura instantânea" importa mais, mas é uma decisão de produto.
3. **Reaproveitar bitmap no flyout é seguro com `UpdateLayeredWindow`?** A limpeza com `g.Clear(Color.Transparent)` antes de redesenhar deveria bastar, mas se houver qualquer caminho de desenho que dependa do estado anterior, haveria fantasma visual. Vale um teste com fundo transparente e com borda.
4. **O cache de `LayeredWindow` por HWND pode vazar se uma janela for destruída sem `Release`?** Sim. Por isso a proposta inclui `Release` explícito. Uma alternativa mais segura é não cachear e aceitar o custo, ou cachear apenas durante a animação.
5. **A leitura direta por offset é portável?** As structs do Windows são estáveis em x86/x64 (mesmos tamanhos por serem `DWORD`/`int`), e o build é `anycpu`. Mas convém um teste de sanidade comparando `PtrToStructure` e leitura por offset em uma amostra.
6. **O `StringFormat` é mesmo relevante?** Não medi handles GDI+ antes/depois. Pode ser ruído perto do custo do bitmap. Está no plano pelo vínculo com o crash silencioso, não pelo ganho de CPU.
7. **Vale trocar `Timer` de 300 ms por algo orientado a evento?** Uma alternativa mais agressiva é reagir a `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` e desligar a verificação de tela cheia do tick. É mais complexo e tem risco de callback em thread errada; não incluí.
8. **O ganho de memória do lazy loading é real?** WPF cria o `Visual` tree de qualquer forma quando a aba aparece; o ganho é no pico inicial, não no estado estacionário. Se o objetivo for memória em repouso, o lazy ajuda pouco depois que o usuário visitou todas as abas.

---

## 15. Anexo — Mapa rápido de hotspots

| Local | O que acontece | Frequência |
|---|---|---|
| `NetMonitor.cs:940-994` | tick do widget: tela cheia, TOPMOST, tema, âncora | 3,3×/s |
| `NetMonitor.cs:595-688` | varredura de protetor de tela (Toolhelp) | ~1,1×/s |
| `NetMonitor.cs:746-894` | 4 tabelas de rede, 8 chamadas nativas | 1×/s (flyout visível) |
| `NetMonitor.cs:1473-1571` | amostragem por processo: dicionários, abertura de processos | 1×/s |
| `NetMonitor.cs:211` (via 1541) | render do flyout: bitmap LOH + 10 fontes | 1×/s |
| `NetMonitor.cs:2119` | animação: `Present` (DC + DIB) | 66×/s por 280 ms |
| `WidgetRenderer.cs:93` | render do widget: bitmap + probe + medidas | até 3,3×/s |
| `SettingsWindow.cs:1427` | tick das configurações: prévia + lista + registro | 2×/s |
| `SettingsWindow.cs:429` | construção das 7 abas | 1× por abertura |
| `FlyoutRenderer.cs:437` | `Fit` mede o texto caractere a caractere | por linha visível |

---

## 16. Resumo executivo

- O widget parado já consome 1,67% de um núcleo; a causa principal são verificações de sistema repetidas a cada 300 ms.
- As Configurações gastam ~2 s de CPU em 3 s e 131 MB porque redesenham duas prévias completas e reconstroem a lista de apps a cada 500 ms, além de construir todas as abas antes de aparecer.
- O flyout aloca ~633 KB no LOH por segundo enquanto visível; reutilizar o bitmap e as fontes é o maior ganho isolado de GC.
- Há um uso de `StringFormat` sem descarte que aumenta a pressão de memória nativa e merece correção independentemente do ganho de CPU.
- As propostas preservam fontes, cores, espaçamentos e ordem de desenho; a fidelidade visual é mantida por construção.

Nenhum arquivo de código foi alterado. Este documento é a proposta para revisão.

---

## 17. Anexo — o que foi implementado e medido (v1.0.2 → v1.0.3)

> Esta seção foi escrita **depois** da implementação. O plano das seções 3 a 12 foi mantido intacto para a segunda opinião.

### 17.1 Itens implementados

| # | Item | Arquivos |
|---|---|---|
| 1 | Bitmap/fontes do flyout e da prévia reaproveitados | `FlyoutRenderer.cs`, `NetMonitor.cs`, `SettingsWindow.cs` |
| 2 | Tick das Configurações sem re-render/reset desnecessário | `SettingsWindow.cs` |
| 3 | Lazy loading das 7 abas | `SettingsWindow.cs` |
| 4 | Tick do widget: DPI em cache, tela cheia por primeiro plano, tema sem polling, TOPMOST/âncora espaçados, Toolhelp a cada 10 s | `NetMonitor.cs` |
| 5 | `StringFormat` cacheado (fim do descarte ausente) | `WidgetRenderer.cs`, `FlyoutRenderer.cs` |
| 6 | Superfície DC+DIB por janela no `LayeredWindow`, com versão de conteúdo | `NetMonitor.cs` |
| 7 | Buffers do `GetExtendedTcpTable` reaproveitados e leitura por offset | `NetMonitor.cs` |
| 8 | Amostragem: 1 dicionário, nomes unificados por PID, normalização sem mutação | `NetMonitor.cs` |
| 9 | Caches de cor (`Pal.Resolve`, `K.C`, `Hex`), formatação única e DPI | `Settings.cs`, `WpfKit.cs`, `WidgetRenderer.cs`, `NetMonitor.cs` |
| 10 | `PerfInfo` reutiliza `Process`, `BuildDataText` reutiliza `StringBuilder`, menu só reconstrói quando sujo, `Mem.Trim` sem `GC.Collect`, `Res` em cache | `NetMonitor.cs`, `SettingsWindow.cs` |
| — | `Ipc.Send` com buffer nativo reaproveitado | `Ipc.cs` |

### 17.2 Bug encontrado durante a validação (e corrigido)

A primeira versão da normalização da amostragem gravava valores no dicionário durante o `foreach`.
No .NET Framework isso **incrementa a versão mesmo ao sobrescrever uma chave existente**, e o
enumerador passa a lançar `InvalidOperationException` a cada segundo. O `crash.log` recém-criado
registrou o problema imediatamente. Correção: a escala passou a ser aplicada na leitura, sem
mutar o dicionário. O mesmo cuidado foi aplicado à limpeza de PIDs antigos (lista temporária
antes de remover).

Esse episódio valida o item de log de emergência implementado antes: sem ele, o app teria
"falhado silenciosamente" de novo.

### 17.3 Medições após a implementação

| Cenário | Antes | Depois |
|---|---|---|
| Widget ocioso (CPU) | 1,67% de um núcleo | **1,04%** (−38%) |
| Widget ocioso (memória) | 40,1 MB | **34,3 MB** (−14%) |
| Widget ocioso (handles) | 375 | **358** |
| Configurações ociosas (CPU) | 203 ms / 15 s (1,35%) | **~0 ms / 15 s** (sem redesenho quando nada muda) |
| Abertura das Configurações (morno) | ~1678 ms (média de 3) | **~1622 ms** (média de 3) |
| Widget com flyout aberto | — | **0,78%** de um núcleo |

A abertura das Configurações melhorou menos que a estimativa original: a inicialização do
WPF/.NET domina o tempo, e as abas adiadas eram mais baratas do que o previsto. O ganho real
concentrou-se no consumo ocioso, no GC e na ausência de redesenho.

### 17.4 Validação funcional

- As 7 abas abriram e responderam (`cores`, `layout`, `fonte`, `setas`, `apps`, `geral`, `sobre`).
- Flyout abriu e fechou por hover, sem exceções.
- Top 5 validado com tráfego real (download): `msedge.exe`, `svchost.exe`,
  `language_server.exe`, `codex.exe`, `TeamViewer_Service.exe`, com categorias e barras.
- `crash.log` e `error.log` ausentes após os testes.
- Widget e flyout conferidos por captura de tela; layout, cores e alinhamento preservados.

### 17.5 Instalação

- Configurações do usuário exportadas antes da desinstalação e reimportadas depois.
- Desinstalação silenciosa (`uninstall.exe /quiet`), reinstalação por `NetMonitorSetup.exe`.
- Hash SHA-256 do `NetMonitor.exe` instalado idêntico ao de `bin\NetMonitor.exe`.
- `scripts\build.ps1`: o `Stop-Process` passou a usar `-ErrorAction SilentlyContinue` para não
  abortar o build quando a instância em execução está elevada (UAC).

### 17.6 Regressão encontrada após a instalação (e corrigida)

Depois da primeira instalação o widget ficou travado: os números não mudavam. Causa: `RenderKey()`
passou a ler os campos formatados (`upNum`, `dnNum`…), mas esses campos só eram atualizados
dentro de `RenderWidget()`. Como a chave era calculada antes do redesenho, ela nunca mudava e o
redesenho nunca era chamado — ciclo fechado.

Correção: `RenderIfChanged()` agora chama `UpdateFormattedSpeeds()` antes de montar a chave.
Validado com tráfego real: 2,1 Mbps / 169 Mbps durante o download e 11,4 / 12,4 Kbps depois,
com troca automática de unidade.

---

## 18. Fechamento da versão 1.0.3

- Build: `release\v1.0.3\` (instalador, portátil e pacote completo).
- Hashes e VirusTotal: `docs/SECURITY_AND_HASHES_v1.0.3.md`.
- Auditoria de segurança e instalador: `docs/SECURITY_AUDIT_v1.0.3.md`.
- Notas de lançamento: `docs/RELEASE_NOTES_v1.0.3.md`.

Resumo do que a 1.0.3 entrega em performance:

| Métrica | 1.0.2 | 1.0.3 |
| :--- | :--- | :--- |
| Widget ocioso (CPU) | 1,67% de 1 núcleo | 1,04% |
| Widget ocioso (working set) | 40,1 MB | 34,3 MB |
| Configurações ociosas (CPU) | 203 ms/15 s | ~0 ms/15 s |
| Abertura das Configurações (morno) | ~1678 ms | ~1622 ms |
| Flyout aberto | — | 0,78% de 1 núcleo |

O ganho de abertura ficou abaixo da estimativa inicial porque a inicialização do WPF/.NET domina o tempo; os maiores ganhos foram no consumo ocioso, no GC e na eliminação de redesenhos desnecessários.
