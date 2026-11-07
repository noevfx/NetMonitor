// ==============================================================================
// NetMonitor - Renderizador do widget (GDI+ -> bitmap ARGB pré-multiplicado)
// Usado pela janela da barra de tarefas (UpdateLayeredWindow) e pela prévia da janela de configurações,
// então os dois sempre mostram exatamente o mesmo desenho.
// Layout em colunas travadas: seta | número (à direita, tabular) | unidade (à esquerda).
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace NetMonitor
{
    public static class WidgetRenderer
    {
        static HashSet<string> installed;

        public static bool FontInstalled(string family)
        {
            if (installed == null)
            {
                installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using (InstalledFontCollection c = new InstalledFontCollection())
                        foreach (FontFamily f in c.Families) installed.Add(f.Name);
                }
                catch { }
            }
            return installed.Contains(family);
        }

        static readonly Dictionary<string, Color> hexCache = new Dictionary<string, Color>();

        public static Color Hex(string hex, int alpha)
        {
            string key = hex + "|" + alpha;
            Color c;
            if (hexCache.TryGetValue(key, out c)) return c;
            try
            {
                Color b = ColorTranslator.FromHtml(hex);
                c = Color.FromArgb(alpha, b.R, b.G, b.B);
            }
            catch { c = Color.FromArgb(alpha, 255, 255, 255); }
            hexCache[key] = c;
            return c;
        }

        [ThreadStatic] static Dictionary<string, Font> fontCache;

        // Fonte reaproveitada entre renderizações (cache por thread: o widget e a prévia desenham em threads diferentes).
        // Nunca descarta fontes em uso: acima do limite devolve uma instância avulsa (o GC finaliza).
        static Font CachedFont(string family, float pt, FontStyle style)
        {
            if (fontCache == null) fontCache = new Dictionary<string, Font>();
            string key = family + "|" + pt.ToString("0.00") + "|" + (int)style;
            Font f;
            if (fontCache.TryGetValue(key, out f)) return f;
            if (fontCache.Count >= 256) return MakeFont(family, pt, style);
            f = MakeFont(family, pt, style);
            fontCache[key] = f;
            return f;
        }

        [ThreadStatic] static StringFormat typo;

        static StringFormat Typo()
        {
            if (typo == null)
            {
                typo = (StringFormat)StringFormat.GenericTypographic.Clone();
                typo.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
            }
            return typo;
        }

        [ThreadStatic] static Bitmap probeBmp;
        [ThreadStatic] static Graphics probeG;
        [ThreadStatic] static float probeDpi;

        // Medição usa um bitmap/Graphics fixos em vez de criar um novo por render.
        static Graphics Probe(float dpi)
        {
            if (probeG == null || Math.Abs(probeDpi - dpi) > 0.01f)
            {
                if (probeG != null) { probeG.Dispose(); probeG = null; }
                if (probeBmp != null) { probeBmp.Dispose(); probeBmp = null; }
                probeBmp = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);
                probeBmp.SetResolution(dpi, dpi);
                probeG = Graphics.FromImage(probeBmp);
                probeG.TextRenderingHint = TextRenderingHint.AntiAlias;
                probeDpi = dpi;
            }
            return probeG;
        }

        // Métricas de layout dependem só de fonte/DPI/configuração: medir uma vez e reaproveitar.
        class Metrics
        {
            public float WNum, WUnit, LineH, PxNum, AscNum, AscUnit, WArrow, ArrowBox;
            public bool TextArrow, ShowArrow;
            public Font FNum, FUnit, FGlyph, FWord, FArrow;
        }

        [ThreadStatic] static Dictionary<string, Metrics> metricsCache;

        static Metrics GetMetrics(AppSettings s, float dpi, string family, string numFamily, float pt, FontStyle style)
        {
            string upG = s.UpGlyph(), dnG = s.DownGlyph();
            bool textArrow = (s.ArrowPreset == "text" || s.ArrowPreset == "custom");
            bool showArrow = s.ArrowPosition != "none" && (!textArrow || upG.Length > 0 || dnG.Length > 0);
            float arrowScale = (float)Math.Max(0.6, Math.Min(1.6, s.ArrowScale));

            string key = numFamily + "|" + family + "|" + pt.ToString("0.##") + "|" + (int)style + "|" +
                         dpi.ToString("0.##") + "|" + s.ShowUnit + "|" + s.ArrowPreset + "|" +
                         arrowScale.ToString("0.##") + "|" + s.ArrowPosition + "|" + upG + "|" + dnG;

            if (metricsCache == null) metricsCache = new Dictionary<string, Metrics>();
            Metrics m;
            if (metricsCache.TryGetValue(key, out m)) return m;

            m = new Metrics();
            m.TextArrow = textArrow;
            m.ShowArrow = showArrow;
            m.FNum = CachedFont(numFamily, pt, style);
            m.FUnit = CachedFont(family, Math.Max(6.5f, pt * 0.84f), FontStyle.Regular);
            m.FGlyph = CachedFont("Segoe UI Symbol", Math.Max(6.5f, pt * 0.8f * arrowScale), FontStyle.Regular);
            m.FWord = CachedFont(family, Math.Max(6.5f, pt * 0.8f * arrowScale), FontStyle.Bold);
            m.FArrow = (upG.Length <= 1 && dnG.Length <= 1) ? m.FGlyph : m.FWord;

            StringFormat tf = Typo();
            Graphics g = Probe(dpi);
            m.PxNum = pt * dpi / 72f;
            m.AscNum = AscentPx(m.FNum, dpi);
            m.AscUnit = AscentPx(m.FUnit, dpi);
            m.WNum = (float)Math.Ceiling(g.MeasureString("888.8", m.FNum, PointF.Empty, tf).Width);
            float wUnit = 0;
            string[] us = new string[] { "Mbps", "Gbps", "Kbps", "MB/s", "GB/s", "KB/s" };
            for (int i = 0; i < us.Length; i++) wUnit = Math.Max(wUnit, g.MeasureString(us[i], m.FUnit, PointF.Empty, tf).Width);
            m.WUnit = s.ShowUnit ? (float)Math.Ceiling(wUnit) : 0;
            m.LineH = m.FNum.GetHeight(g);

            m.ArrowBox = m.PxNum * 0.95f * arrowScale * ((s.ArrowPreset == "triangles" || s.ArrowPreset == "outline") ? 1.188f : 1f);
            if (showArrow)
            {
                if (textArrow)
                    m.WArrow = (float)Math.Ceiling(Math.Max(
                        g.MeasureString(upG, m.FArrow, PointF.Empty, tf).Width,
                        g.MeasureString(dnG, m.FArrow, PointF.Empty, tf).Width));
                else m.WArrow = (float)Math.Ceiling(m.ArrowBox);
            }

            if (metricsCache.Count >= 128) metricsCache.Clear();
            metricsCache[key] = m;
            return m;
        }

        static Font MakeFont(string family, float pt, FontStyle style)
        {
            try { return new Font(family, pt, style, GraphicsUnit.Point); }
            catch { return new Font("Segoe UI", pt, style, GraphicsUnit.Point); }
        }

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        class Row
        {
            public string Num, Unit, Hex;
            public bool Up;
        }

        // Ascendente da fonte em pixels (para alinhar número e unidade pela linha de base)
        static float AscentPx(Font f, float dpi)
        {
            float em = f.FontFamily.GetEmHeight(f.Style);
            float asc = f.FontFamily.GetCellAscent(f.Style);
            return asc / em * f.SizeInPoints * dpi / 72f;
        }

        public static Bitmap Render(AppSettings s, bool dark, string upNum, string upUnit, string dnNum, string dnUnit, float dpi)
        {
            return RenderTo(null, s, dark, upNum, upUnit, dnNum, dnUnit, dpi);
        }

        // reuse: bitmap devolvido por uma chamada anterior; reaproveitado se o tamanho bater.
        public static Bitmap RenderTo(Bitmap reuse, AppSettings s, bool dark, string upNum, string upUnit, string dnNum, string dnUnit, float dpi)
        {
            float sc = dpi / 96f;

            string family = s.FontFamily;
            if (!FontInstalled(family)) family = "Segoe UI";
            FontStyle style = FontStyle.Regular;
            string numFamily = family;
            if (s.FontWeight == "bold") style = FontStyle.Bold;
            else if (s.FontWeight == "600")
            {
                if (family.StartsWith("Segoe UI", StringComparison.OrdinalIgnoreCase) && FontInstalled("Segoe UI Semibold")) numFamily = "Segoe UI Semibold";
                else style = FontStyle.Bold;
            }
            float pt = (float)s.FontSize;

            Metrics m = GetMetrics(s, dpi, family, numFamily, pt, style);
            Font fNum = m.FNum, fUnit = m.FUnit, fArrow = m.FArrow;
            StringFormat tf = Typo();
            float lineH = m.LineH, wNum = m.WNum, wUnit = m.WUnit, arrowBox = m.ArrowBox;
            float ascNum = m.AscNum, ascUnit = m.AscUnit, wArrow = m.WArrow;
            bool textArrow = m.TextArrow, showArrow = m.ShowArrow;
            string upG = s.UpGlyph(), dnG = s.DownGlyph();

            bool boxed = s.BorderEnabled || s.BgMode != "transparent";
            float pad = (boxed ? 9 : 7) * sc, gap = 6 * sc, between = 16 * sc, vpad = (boxed ? 4 : 3) * sc;
            float pitch = lineH * 0.9f;          // distância entre as duas linhas (mais junta que a altura da fonte)
            bool up1 = s.MetricsOrder != "down-first";
            Row rUp = new Row { Num = upNum, Unit = upUnit, Hex = s.UpHex(dark), Up = true };
            Row rDn = new Row { Num = dnNum, Unit = dnUnit, Hex = s.DownHex(dark), Up = false };
            Row first = up1 ? rUp : rDn, second = up1 ? rDn : rUp;

            bool two = s.Layout != "one-line";
            float groupW = (showArrow ? wArrow + gap : 0) + wNum + (s.ShowUnit ? gap + wUnit : 0);
            int W, H;
            if (two)
            {
                W = (int)Math.Ceiling(pad * 2 + groupW);
                H = (int)Math.Ceiling(Math.Max(34 * sc, lineH + pitch + vpad * 2));
            }
            else
            {
                W = (int)Math.Ceiling(pad * 2 + groupW * 2 + between);
                H = (int)Math.Ceiling(Math.Max(26 * sc, lineH + vpad * 2 + 4 * sc));
            }

            Bitmap bmp = (reuse != null && reuse.Width == W && reuse.Height == H)
                ? reuse
                : new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            if (!ReferenceEquals(bmp, reuse)) bmp.SetResolution(dpi, dpi);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;

                // Fundo: transparente recebe alpha 1 para o Windows entregar cliques (hit-test) em toda a área
                RectangleF full = new RectangleF(0, 0, W, H);
                float rad = 8 * sc;
                if (s.BgMode == "solid")
                {
                    using (GraphicsPath p = RoundRect(full, rad))
                    using (SolidBrush b = new SolidBrush(Hex(Pal.For(dark).WidgetBg, 255)))
                        g.FillPath(b, p);
                }
                else if (s.BgMode == "acrylic")
                {
                    using (GraphicsPath p = RoundRect(full, rad))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(dark ? 22 : 166, 255, 255, 255)))
                        g.FillPath(b, p);
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(1, 0, 0, 0)))
                        g.FillRectangle(b, 0, 0, W, H);
                }

                if (two)
                {
                    float top0 = (H - (lineH + pitch)) / 2f;
                    DrawGroup(g, s, first, top0, lineH, pad, groupW, showArrow, textArrow, wArrow, arrowBox, wNum, wUnit, gap, fNum, fUnit, fArrow, ascNum, ascUnit, lineH, dpi, upG, dnG);
                    DrawGroup(g, s, second, top0 + pitch, lineH, pad, groupW, showArrow, textArrow, wArrow, arrowBox, wNum, wUnit, gap, fNum, fUnit, fArrow, ascNum, ascUnit, lineH, dpi, upG, dnG);
                }
                else
                {
                    DrawGroup(g, s, first, 0, H, pad, groupW, showArrow, textArrow, wArrow, arrowBox, wNum, wUnit, gap, fNum, fUnit, fArrow, ascNum, ascUnit, lineH, dpi, upG, dnG);
                    DrawGroup(g, s, second, 0, H, pad + groupW + between, groupW, showArrow, textArrow, wArrow, arrowBox, wNum, wUnit, gap, fNum, fUnit, fArrow, ascNum, ascUnit, lineH, dpi, upG, dnG);
                }

                if (s.BorderEnabled)
                {
                    float bw = Math.Max(1.5f, 1.5f * sc);   // traço um pouco mais grosso e suavizado: sem aspecto serrilhado
                    RectangleF br = new RectangleF(bw / 2f, bw / 2f, W - bw, H - bw);
                    using (GraphicsPath p = RoundRect(br, rad - bw / 2f))
                    using (Pen pen = new Pen(Hex(s.BorderHex(dark), (int)Math.Round(255 * Math.Max(0, Math.Min(1, s.BorderOpacity)))), bw))
                        g.DrawPath(pen, p);
                }
            }
            return bmp;
        }

        static void DrawGroup(Graphics g, AppSettings s, Row r, float top, float rowH, float x0, float groupW,
            bool showArrow, bool textArrow, float wArrow, float arrowBox, float wNum, float wUnit, float gap,
            Font fNum, Font fUnit, Font fArrow, float ascNum, float ascUnit, float lineH, float dpi, string upG, string dnG)
        {
            StringFormat tf = Typo();

            float yNum = top + (rowH - lineH) / 2f;
            float baseline = yNum + ascNum;
            float x = x0;
            bool left = s.ArrowPosition != "right";

            float xArrow, xNum, xUnit;
            if (showArrow && left)
            {
                xArrow = x; xNum = x + wArrow + gap; xUnit = xNum + wNum + (s.ShowUnit ? gap : 0);
            }
            else if (showArrow)
            {
                xNum = x; xUnit = xNum + wNum + (s.ShowUnit ? gap : 0); xArrow = xUnit + wUnit + gap;
            }
            else
            {
                xArrow = x; xNum = x; xUnit = xNum + wNum + (s.ShowUnit ? gap : 0);
            }

            using (SolidBrush bNum = new SolidBrush(WidgetRenderer.Hex(r.Hex, 255)))
            using (SolidBrush bUnit = new SolidBrush(WidgetRenderer.Hex(r.Hex, 230)))   // unidade a 90% do número
            {
                // número alinhado à direita da sua coluna
                float nw = g.MeasureString(r.Num, fNum, PointF.Empty, tf).Width;
                g.DrawString(r.Num, fNum, bNum, xNum + wNum - nw, yNum, tf);
                if (s.ShowUnit) g.DrawString(r.Unit, fUnit, bUnit, xUnit, baseline - ascUnit, tf);

                if (showArrow)
                {
                    float capMid = baseline - (fNum.SizeInPoints * dpi / 72f) * 0.36f;
                    if (textArrow)
                    {
                        string t = r.Up ? upG : dnG;
                        if (t.Length > 0)
                        {
                            float aw = g.MeasureString(t, fArrow, PointF.Empty, tf).Width;
                            float ah = fArrow.GetHeight(g);
                            g.DrawString(t, fArrow, bNum, xArrow + (wArrow - aw) / 2f, capMid - ah / 2f, tf);
                        }
                    }
                    else
                    {
                        float ab = arrowBox;
                        DrawVectorArrow(g, s.ArrowPreset, r.Up, xArrow + (wArrow - ab) / 2f, capMid - ab / 2f, ab, r.Hex);
                    }
                }
            }
        }

        // Geometria na grade 24x24 dos ícones da marca (seta-tx/rx-*), escalada para a caixa
        static void DrawVectorArrow(Graphics g, string preset, bool up, float x, float y, float box, string hex)
        {
            float k = box / 24f;
            Func<float, float, PointF> P = (px, py) => new PointF(x + px * k, y + py * k);
            Color c = Hex(hex, 255);
            float w = Math.Max(1f, 2f * k);

            if (preset == "triangles" || preset == "outline")
            {
                PointF[] pts = up
                    ? new PointF[] { P(12, 6), P(19, 17), P(5, 17) }
                    : new PointF[] { P(12, 18), P(5, 7), P(19, 7) };
                if (preset == "triangles")
                {
                    using (SolidBrush b = new SolidBrush(c)) g.FillPolygon(b, pts);
                }
                else
                {
                    using (Pen p = new Pen(c, w) { LineJoin = LineJoin.Round })
                        g.DrawPolygon(p, pts);
                }
                return;
            }

            using (Pen p = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (preset == "thin")
                {
                    if (up)
                    {
                        g.DrawLine(p, P(12, 19), P(12, 5));
                        g.DrawLines(p, new PointF[] { P(6.5f, 10.5f), P(12, 5), P(17.5f, 10.5f) });
                    }
                    else
                    {
                        g.DrawLine(p, P(12, 5), P(12, 19));
                        g.DrawLines(p, new PointF[] { P(6.5f, 13.5f), P(12, 19), P(17.5f, 13.5f) });
                    }
                }
                else // chevrons
                {
                    if (up) g.DrawLines(p, new PointF[] { P(5, 16.5f), P(12, 7.5f), P(19, 16.5f) });
                    else g.DrawLines(p, new PointF[] { P(5, 7.5f), P(12, 16.5f), P(19, 7.5f) });
                }
            }
        }
    }
}
