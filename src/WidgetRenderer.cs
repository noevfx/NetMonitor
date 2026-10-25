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

        public static Color Hex(string hex, int alpha)
        {
            try
            {
                Color c = ColorTranslator.FromHtml(hex);
                return Color.FromArgb(alpha, c.R, c.G, c.B);
            }
            catch { return Color.FromArgb(alpha, 255, 255, 255); }
        }

        [ThreadStatic] static Dictionary<string, Font> fontCache;

        // Fonte reaproveitada entre renderizações (cache por thread: o widget e a prévia desenham em threads diferentes)
        static Font CachedFont(string family, float pt, FontStyle style)
        {
            if (fontCache == null) fontCache = new Dictionary<string, Font>();
            string key = family + "|" + pt.ToString("0.00") + "|" + (int)style;
            Font f;
            if (fontCache.TryGetValue(key, out f)) return f;
            if (fontCache.Count > 24) { foreach (Font old in fontCache.Values) old.Dispose(); fontCache.Clear(); }
            f = MakeFont(family, pt, style);
            fontCache[key] = f;
            return f;
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

            using (Bitmap probe = new Bitmap(1, 1, PixelFormat.Format32bppPArgb))
            {
                Font fNum = CachedFont(numFamily, pt, style);
                Font fUnit = CachedFont(family, Math.Max(6.5f, pt * 0.84f), FontStyle.Regular);
                Font fGlyph = CachedFont("Segoe UI Symbol", Math.Max(6.5f, pt * 0.8f * (float)Math.Max(0.6, Math.Min(1.6, s.ArrowScale))), FontStyle.Regular);
                Font fWord = CachedFont(family, Math.Max(6.5f, pt * 0.8f * (float)Math.Max(0.6, Math.Min(1.6, s.ArrowScale))), FontStyle.Bold);
                probe.SetResolution(dpi, dpi);
                StringFormat tf = StringFormat.GenericTypographic;
                float pxNum = pt * dpi / 72f;

                float wNum, wUnit, lineH;
                float ascNum = AscentPx(fNum, dpi);
                float ascUnit = AscentPx(fUnit, dpi);
                using (Graphics m = Graphics.FromImage(probe))
                {
                    m.TextRenderingHint = TextRenderingHint.AntiAlias;
                    wNum = m.MeasureString("888.8", fNum, PointF.Empty, tf).Width;
                    wUnit = 0;
                    string[] us = new string[] { "Mbps", "Gbps", "Kbps", "MB/s", "GB/s", "KB/s" };
                    for (int i = 0; i < us.Length; i++) wUnit = Math.Max(wUnit, m.MeasureString(us[i], fUnit, PointF.Empty, tf).Width);
                    lineH = fNum.GetHeight(m);
                }
                wNum = (float)Math.Ceiling(wNum);
                wUnit = s.ShowUnit ? (float)Math.Ceiling(wUnit) : 0;

                // Setas: vetor (4 presets) ou texto (TX:/RX: e personalizado)
                string upG = s.UpGlyph(), dnG = s.DownGlyph();
                bool textArrow = (s.ArrowPreset == "text" || s.ArrowPreset == "custom");
                bool showArrow = s.ArrowPosition != "none" && (!textArrow || upG.Length > 0 || dnG.Length > 0);
                float arrowScale = (float)Math.Max(0.6, Math.Min(1.6, s.ArrowScale));
                // triângulos (sólido e vazado) são visualmente menores: +10% e mais +8%
                float arrowBox = pxNum * 0.95f * arrowScale * ((s.ArrowPreset == "triangles" || s.ArrowPreset == "outline") ? 1.188f : 1f);
                float wArrow = 0;
                Font fArrow = null;
                if (showArrow)
                {
                    if (textArrow)
                    {
                        fArrow = (upG.Length <= 1 && dnG.Length <= 1) ? fGlyph : fWord;
                        using (Graphics m = Graphics.FromImage(probe))
                        {
                            m.TextRenderingHint = TextRenderingHint.AntiAlias;
                            wArrow = Math.Max(m.MeasureString(upG, fArrow, PointF.Empty, tf).Width, m.MeasureString(dnG, fArrow, PointF.Empty, tf).Width);
                        }
                        wArrow = (float)Math.Ceiling(wArrow);
                    }
                    else wArrow = (float)Math.Ceiling(arrowBox);
                }

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

                Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
                bmp.SetResolution(dpi, dpi);
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
        }

        static void DrawGroup(Graphics g, AppSettings s, Row r, float top, float rowH, float x0, float groupW,
            bool showArrow, bool textArrow, float wArrow, float arrowBox, float wNum, float wUnit, float gap,
            Font fNum, Font fUnit, Font fArrow, float ascNum, float ascUnit, float lineH, float dpi, string upG, string dnG)
        {
            StringFormat tf = StringFormat.GenericTypographic;
            tf.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;

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
