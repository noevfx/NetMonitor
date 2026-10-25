// ==============================================================================
// NetMonitor - Flyout "Tráfego de Rede por App" (GDI+ -> bitmap ARGB pré-multiplicado)
// O mesmo desenho aparece na janela da barra (UpdateLayeredWindow) e na prévia das Configurações.
// Também contém o conversor SVG path -> GraphicsPath usado para desenhar os ícones oficiais da marca.
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using NetMonitor.Ui;

namespace NetMonitor
{
    // ------------------------------------------------------------------ SVG path
    public static class SvgPath
    {
        static readonly Regex Tok = new Regex(@"[A-Za-z]|-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

        static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

        // Constrói o caminho na grade 24x24 deslocado/escalado (x*k+ox, y*k+oy)
        public static GraphicsPath Build(string d, float ox, float oy, float k)
        {
            GraphicsPath gp = new GraphicsPath();
            MatchCollection mc = Tok.Matches(d);
            List<string> t = new List<string>();
            foreach (Match m in mc) t.Add(m.Value);

            int i = 0;
            float cx = 0, cy = 0, sx = 0, sy = 0, lcx = 0, lcy = 0;
            char cmd = 'M';
            bool open = false;
            Func<float, float, PointF> P = (x, y) => new PointF(ox + x * k, oy + y * k);

            while (i < t.Count)
            {
                if (char.IsLetter(t[i][0])) { cmd = t[i][0]; i++; if (cmd == 'Z' || cmd == 'z') { if (open) gp.CloseFigure(); cx = sx; cy = sy; open = false; continue; } }
                bool rel = char.IsLower(cmd);
                char C = char.ToUpper(cmd);
                if (C == 'M')
                {
                    float x = F(t[i++]), y = F(t[i++]);
                    if (rel) { x += cx; y += cy; }
                    gp.StartFigure(); open = true;
                    cx = sx = x; cy = sy = y;
                    cmd = rel ? 'l' : 'L'; // pares seguintes são linhas
                }
                else if (C == 'L')
                {
                    float x = F(t[i++]), y = F(t[i++]);
                    if (rel) { x += cx; y += cy; }
                    gp.AddLine(P(cx, cy), P(x, y)); cx = x; cy = y;
                }
                else if (C == 'H')
                {
                    float x = F(t[i++]); if (rel) x += cx;
                    gp.AddLine(P(cx, cy), P(x, cy)); cx = x;
                }
                else if (C == 'V')
                {
                    float y = F(t[i++]); if (rel) y += cy;
                    gp.AddLine(P(cx, cy), P(cx, y)); cy = y;
                }
                else if (C == 'C')
                {
                    float x1 = F(t[i++]), y1 = F(t[i++]), x2 = F(t[i++]), y2 = F(t[i++]), x = F(t[i++]), y = F(t[i++]);
                    if (rel) { x1 += cx; y1 += cy; x2 += cx; y2 += cy; x += cx; y += cy; }
                    gp.AddBezier(P(cx, cy), P(x1, y1), P(x2, y2), P(x, y));
                    lcx = x2; lcy = y2; cx = x; cy = y;
                }
                else if (C == 'S')
                {
                    float x2 = F(t[i++]), y2 = F(t[i++]), x = F(t[i++]), y = F(t[i++]);
                    if (rel) { x2 += cx; y2 += cy; x += cx; y += cy; }
                    float x1 = 2 * cx - lcx, y1 = 2 * cy - lcy;
                    gp.AddBezier(P(cx, cy), P(x1, y1), P(x2, y2), P(x, y));
                    lcx = x2; lcy = y2; cx = x; cy = y;
                }
                else if (C == 'A')
                {
                    float rx = F(t[i++]), ry = F(t[i++]), rot = F(t[i++]);
                    bool fa = F(t[i++]) != 0, fs = F(t[i++]) != 0;
                    float x = F(t[i++]), y = F(t[i++]);
                    if (rel) { x += cx; y += cy; }
                    Arc(gp, P, cx, cy, rx, ry, rot, fa, fs, x, y);
                    cx = x; cy = y;
                }
                else { i++; }
                if (C != 'C' && C != 'S') { lcx = cx; lcy = cy; }
            }
            return gp;
        }

        // Arco SVG (W3C F.6.5) convertido em béziers cúbicas
        static void Arc(GraphicsPath gp, Func<float, float, PointF> P, float x1, float y1, float rx, float ry, float phiDeg, bool fa, bool fs, float x2, float y2)
        {
            if (rx == 0 || ry == 0) { gp.AddLine(P(x1, y1), P(x2, y2)); return; }
            double phi = phiDeg * Math.PI / 180.0, cp = Math.Cos(phi), sp = Math.Sin(phi);
            double dx = (x1 - x2) / 2.0, dy = (y1 - y2) / 2.0;
            double xp = cp * dx + sp * dy, yp = -sp * dx + cp * dy;
            double rxa = Math.Abs(rx), rya = Math.Abs(ry);
            double lam = (xp * xp) / (rxa * rxa) + (yp * yp) / (rya * rya);
            if (lam > 1) { double sl = Math.Sqrt(lam); rxa *= sl; rya *= sl; }
            double num = rxa * rxa * rya * rya - rxa * rxa * yp * yp - rya * rya * xp * xp;
            double den = rxa * rxa * yp * yp + rya * rya * xp * xp;
            double co = (den == 0) ? 0 : Math.Sqrt(Math.Max(0, num / den));
            if (fa == fs) co = -co;
            double cxp = co * rxa * yp / rya, cyp = -co * rya * xp / rxa;
            double cx = cp * cxp - sp * cyp + (x1 + x2) / 2.0, cy = sp * cxp + cp * cyp + (y1 + y2) / 2.0;
            Func<double, double, double, double, double> ang = (ux, uy, vx, vy) =>
            {
                double a = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
                return a;
            };
            double th1 = ang(1, 0, (xp - cxp) / rxa, (yp - cyp) / rya);
            double dth = ang((xp - cxp) / rxa, (yp - cyp) / rya, (-xp - cxp) / rxa, (-yp - cyp) / rya);
            if (!fs && dth > 0) dth -= 2 * Math.PI;
            else if (fs && dth < 0) dth += 2 * Math.PI;

            int segs = (int)Math.Ceiling(Math.Abs(dth) / (Math.PI / 2) - 1e-9);
            if (segs < 1) segs = 1;
            double delta = dth / segs;
            double tt = 4.0 / 3.0 * Math.Tan(delta / 4.0);
            double a1 = th1;
            PointF pc = P(x1, y1);
            for (int s = 0; s < segs; s++)
            {
                double a2 = a1 + delta;
                double c1 = Math.Cos(a1), s1 = Math.Sin(a1), c2 = Math.Cos(a2), s2 = Math.Sin(a2);
                Func<double, double, PointF> M = (ex, ey) =>
                {
                    double X = cp * rxa * ex - sp * rya * ey + cx;
                    double Y = sp * rxa * ex + cp * rya * ey + cy;
                    return P((float)X, (float)Y);
                };
                PointF q1 = M(c1 - tt * s1, s1 + tt * c1);
                PointF q2 = M(c2 + tt * s2, s2 - tt * c2);
                PointF q3 = M(c2, s2);
                gp.AddBezier(pc, q1, q2, q3);
                pc = q3; a1 = a2;
            }
        }

        // Desenha um ícone da marca (modo S=traço, F=preenchimento) na caixa x,y,size com a cor dada
        public static void DrawIcon(Graphics g, string name, float x, float y, float size, Color color)
        {
            string[] parts;
            if (!IconData.All.TryGetValue(name, out parts)) return;
            float k = size / 24f;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(color, Math.Max(1f, 2f * k)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (SolidBrush br = new SolidBrush(color))
            {
                for (int i = 0; i + 1 < parts.Length; i += 2)
                {
                    using (GraphicsPath gp = Build(parts[i], x, y, k))
                    {
                        string mode = parts[i + 1];
                        if (mode.IndexOf('F') >= 0) g.FillPath(br, gp);
                        if (mode.IndexOf('S') >= 0) g.DrawPath(pen, gp);
                    }
                }
            }
            g.SmoothingMode = old;
        }
    }

    // ------------------------------------------------------------------ Flyout
    public class FlyoutHit
    {
        public Rectangle Separated, Combined, SettingsLink, Body;
        public List<Rectangle> Rows = new List<Rectangle>();
    }

    public static class FlyoutRenderer
    {
        // índice estável (0..4) de cor por nome de app: usado pelas iniciais coloridas da página Top 5
        public static int TagIndex(string name)
        {
            int h = 0;
            for (int i = 0; i < name.Length; i++) h = h * 31 + char.ToLowerInvariant(name[i]);
            return Math.Abs(h) % 5;
        }

        static Font F(string fam, float px, FontStyle st)
        {
            try { return new Font(fam, px, st, GraphicsUnit.Pixel); }
            catch { return new Font("Segoe UI", px, st, GraphicsUnit.Pixel); }
        }

        static GraphicsPath RR(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Max(0.01f, rad * 2);
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // margin > 0 reserva uma borda transparente para a sombra (janela real). hover = índice da linha sob o mouse (-1 nenhum).
        // O cartão sempre reserva espaço para 5 linhas: a altura não muda conforme a atividade de rede.
        public static Bitmap Render(AppSettings s, bool dark, List<ProcessTrafficItem> items, double totUpBps, double totDnBps,
            float dpi, int hover, int margin, out FlyoutHit hit)
        {
            float sc = dpi / 96f;
            Tokens t = Pal.For(dark);
            bool sep = s.FlyoutMode != "combined";
            hit = new FlyoutHit();

            float pad = 18 * sc, W = 400 * sc, hdrH = 30 * sc, rowH = 56 * sc;
            int n = items == null ? 0 : Math.Min(5, items.Count);
            float y = pad;
            float hdrBottom = y + hdrH + 10 * sc;
            float rowsTop = hdrBottom + 8 * sc;
            float rowsBottom = rowsTop + 5 * rowH;
            float footTop = rowsBottom + 8 * sc;
            float footH = 24 * sc;
            float H = footTop + footH + pad;

            int m = margin;
            int bw = (int)Math.Ceiling(W) + m * 2, bh = (int)Math.Ceiling(H) + m * 2;
            Bitmap bmp = new Bitmap(bw, bh, PixelFormat.Format32bppPArgb);
            bmp.SetResolution(dpi, dpi);
            Color cText = WidgetRenderer.Hex(t.TextPri, 255);
            Color cSub = WidgetRenderer.Hex(t.TextSec, 255);
            Color cLine = dark ? Color.FromArgb(22, 255, 255, 255) : Color.FromArgb(22, 0, 0, 0);
            Color cHover = dark ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(12, 0, 0, 0);
            Color cTrack = dark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(24, 0, 0, 0);
            string famText = WidgetRenderer.FontInstalled("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";

            using (Graphics g = Graphics.FromImage(bmp))
            using (StringFormat tf = (StringFormat)StringFormat.GenericTypographic.Clone())
            using (Font fTitle = F(famText, 15 * sc, FontStyle.Bold))
            using (Font fName = F(famText, 14 * sc, FontStyle.Bold))
            using (Font fSub = F(famText, 12.5f * sc, FontStyle.Regular))
            using (Font fVal = F(famText, 14f * sc, FontStyle.Bold))
            using (Font fFoot = F(famText, 12.5f * sc, FontStyle.Regular))
            using (Font fFootB = F(famText, 12.5f * sc, FontStyle.Bold))
            using (Font fTog = F(famText, 12.5f * sc, FontStyle.Bold))
            using (Font fRank = F(famText, 13f * sc, FontStyle.Bold))
            using (Font fSym = F("Segoe UI Symbol", 11f * sc, FontStyle.Regular))
            {
                tf.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.TranslateTransform(m, m);

                RectangleF card = new RectangleF(0, 0, W, H);
                if (m > 0)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        float e = (7 - i) * 1.6f * sc;
                        RectangleF sh = new RectangleF(-e, 5 * sc - e, W + 2 * e, H + 2 * e);
                        using (GraphicsPath p = RR(sh, 12 * sc + e))
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(dark ? 16 : 9, 0, 0, 0)))
                            g.FillPath(b, p);
                    }
                }
                using (GraphicsPath p = RR(card, 12 * sc))
                using (SolidBrush b = new SolidBrush(WidgetRenderer.Hex(t.FlyoutBg, 255)))
                    g.FillPath(b, p);
                hit.Body = new Rectangle(m, m, (int)W, (int)H);

                // ---- cabeçalho: título e alternância Separado / Combinado
                string tSep = "Separado", tCom = "Combinado";
                float wS = g.MeasureString(tSep, fTog, PointF.Empty, tf).Width + 20 * sc;
                float wC = g.MeasureString(tCom, fTog, PointF.Empty, tf).Width + 20 * sc;
                float tgH = 28 * sc, tgX = W - pad - wS - wC - 6 * sc, tgY = y + (hdrH - tgH) / 2f;
                string title = "Tráfego de Rede por App";
                float titleRoom = tgX - pad - 10 * sc;
                float tpx = 15 * sc;
                Font fT = fTitle;
                while (tpx > 11 * sc && g.MeasureString(title, fT, PointF.Empty, tf).Width > titleRoom)
                {
                    tpx -= 0.5f * sc;
                    if (fT != fTitle) fT.Dispose();
                    fT = F(famText, tpx, FontStyle.Bold);
                }
                using (SolidBrush bt = new SolidBrush(cText))
                    g.DrawString(title, fT, bt, pad, y + (hdrH - fT.GetHeight(g)) / 2f, tf);
                if (fT != fTitle) fT.Dispose();

                using (GraphicsPath p = RR(new RectangleF(tgX, tgY, wS + wC + 6 * sc, tgH), 6 * sc))
                using (SolidBrush b = new SolidBrush(dark ? Color.FromArgb(26, 255, 255, 255) : Color.FromArgb(18, 0, 0, 0)))
                    g.FillPath(b, p);
                RectangleF rS = new RectangleF(tgX + 3 * sc, tgY + 3 * sc, wS, tgH - 6 * sc);
                RectangleF rC = new RectangleF(tgX + 3 * sc + wS, tgY + 3 * sc, wC, tgH - 6 * sc);
                RectangleF act = sep ? rS : rC;
                using (GraphicsPath p = RR(act, 4 * sc))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 37, 99, 235)))
                    g.FillPath(b, p);
                DrawCentered(g, tSep, fTog, sep ? Color.White : cSub, rS, tf);
                DrawCentered(g, tCom, fTog, sep ? cSub : Color.White, rC, tf);
                hit.Separated = Rectangle.Round(new RectangleF(rS.X + m, rS.Y + m, rS.Width, rS.Height));
                hit.Combined = Rectangle.Round(new RectangleF(rC.X + m, rC.Y + m, rC.Width, rC.Height));

                using (Pen pn = new Pen(cLine, 1)) g.DrawLine(pn, pad, hdrBottom, W - pad, hdrBottom);

                // ---- linhas dos processos: nome, categoria, valores e barrinha proporcional ao maior consumo
                string upHex = s.UpHex(dark), dnHex = s.DownHex(dark);
                if (n == 0)
                {
                    using (SolidBrush b = new SolidBrush(cSub))
                    using (StringFormat sc2 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString("Aguardando atividade de rede...", fName, b, new RectangleF(pad, rowsTop, W - pad * 2, 5 * rowH), sc2);
                }
                ulong max = 1;
                for (int i = 0; i < n; i++) if (items[i].Total > max) max = items[i].Total;
                float nameX = pad + 28 * sc;           // coluna do nome/barra, depois da numeração 1-5
                float barW = W - pad - nameX;
                // larguras máximas das colunas de valores: alinham as setas e os números entre as linhas
                float wtMax = 0, wrMax = 0;
                for (int i = 0; i < n; i++)
                {
                    wtMax = Math.Max(wtMax, g.MeasureString(SpeedFmt.ForBytes(s, items[i].TxBytes, true), fVal, PointF.Empty, tf).Width);
                    wrMax = Math.Max(wrMax, g.MeasureString(SpeedFmt.ForBytes(s, items[i].RxBytes, false), fVal, PointF.Empty, tf).Width);
                }
                for (int i = 0; i < n; i++)
                {
                    ProcessTrafficItem it = items[i];
                    float ry = rowsTop + i * rowH;
                    RectangleF rr = new RectangleF(pad - 8 * sc, ry + 1 * sc, W - pad * 2 + 16 * sc, rowH - 2 * sc);
                    hit.Rows.Add(Rectangle.Round(new RectangleF(rr.X + m, rr.Y + m, rr.Width, rr.Height)));
                    if (i == hover)
                        using (GraphicsPath p = RR(rr, 6 * sc))
                        using (SolidBrush b = new SolidBrush(cHover))
                            g.FillPath(b, p);

                    float rx = W - pad;
                    float nameY = ry + 7 * sc;
                    float valY = nameY + (fName.GetHeight(g) - fVal.GetHeight(g)) / 2f;
                    float leftOfValues;
                    if (sep)
                    {
                        string vt = SpeedFmt.ForBytes(s, it.TxBytes, true), vr = SpeedFmt.ForBytes(s, it.RxBytes, false);
                        float wr = g.MeasureString(vr, fVal, PointF.Empty, tf).Width;
                        float wt = g.MeasureString(vt, fVal, PointF.Empty, tf).Width;
                        float symW = 15 * sc;
                        float xrArrow = rx - wrMax - symW;          // coluna RX: seta fixa, número à direita
                        using (SolidBrush b = new SolidBrush(WidgetRenderer.Hex(dnHex, 255)))
                        {
                            g.DrawString(vr, fVal, b, rx - wr, valY, tf);
                            g.DrawString("▼", fSym, b, xrArrow, valY + 1 * sc, tf);
                        }
                        float xtRight = xrArrow - 20 * sc;          // coluna TX
                        float xtArrow = xtRight - wtMax - symW;
                        using (SolidBrush b = new SolidBrush(WidgetRenderer.Hex(upHex, 255)))
                        {
                            g.DrawString(vt, fVal, b, xtRight - wt, valY, tf);
                            g.DrawString("▲", fSym, b, xtArrow, valY + 1 * sc, tf);
                        }
                        leftOfValues = xtArrow;
                    }
                    else
                    {
                        string vc = SpeedFmt.ForBytes(s, it.Total, false);
                        float wc = g.MeasureString(vc, fVal, PointF.Empty, tf).Width;
                        using (SolidBrush b = new SolidBrush(cText))
                            g.DrawString(vc, fVal, b, rx - wc, valY, tf);
                        leftOfValues = rx - wc;
                    }

                    float room = leftOfValues - nameX - 12 * sc;
                    using (SolidBrush b = new SolidBrush(cText))
                        g.DrawString(Fit(g, it.Name + ".exe", fName, room, tf), fName, b, nameX, nameY, tf);
                    using (SolidBrush br = new SolidBrush(cSub))
                        g.DrawString((i + 1).ToString(), fRank, br, pad, nameY + (fName.GetHeight(g) - fRank.GetHeight(g)) / 2f, tf);
                    using (SolidBrush b = new SolidBrush(cSub))
                        g.DrawString(Fit(g, string.IsNullOrEmpty(it.Sub) ? "Aplicativo" : it.Sub, fSub, room, tf), fSub, b, nameX, nameY + fName.GetHeight(g) - 1 * sc, tf);

                    // barrinha: comprimento = consumo relativo ao maior; divide em Upload (esq.) e Download
                    float by = ry + rowH - 11 * sc, bh2 = 4 * sc;
                    using (GraphicsPath p = RR(new RectangleF(nameX, by, barW, bh2), bh2 / 2))
                    using (SolidBrush b = new SolidBrush(cTrack))
                        g.FillPath(b, p);
                    float fillW = Math.Max(bh2, barW * (float)((double)it.Total / max));
                    float txW = it.Total == 0 ? 0 : fillW * (float)((double)it.TxBytes / it.Total);
                    GraphicsState gs = g.Save();
                    using (GraphicsPath clip = RR(new RectangleF(nameX, by, fillW, bh2), bh2 / 2))
                    {
                        g.SetClip(clip, CombineMode.Replace);
                        if (sep)
                        {
                            using (SolidBrush b = new SolidBrush(WidgetRenderer.Hex(upHex, 255))) g.FillRectangle(b, nameX, by, txW, bh2);
                            using (SolidBrush b = new SolidBrush(WidgetRenderer.Hex(dnHex, 255))) g.FillRectangle(b, nameX + txW, by, fillW - txW, bh2);
                        }
                        else
                        {
                            using (SolidBrush b = new SolidBrush(cText)) g.FillRectangle(b, nameX, by, fillW, bh2);
                        }
                    }
                    g.Restore(gs);
                }

                // ---- rodapé: "Configurações" à esquerda, totais alinhados à direita (mesma coluna dos valores)
                using (Pen pn = new Pen(cLine, 1)) g.DrawLine(pn, pad, footTop, W - pad, footTop);
                float fy = footTop + (footH - fFoot.GetHeight(g)) / 2f + 2 * sc;
                string cfg = "Configurações";
                float cw = g.MeasureString(cfg, fFoot, PointF.Empty, tf).Width;
                Color cLink = WidgetRenderer.Hex(Pal.FitContrast(Pal.Brand, t.FlyoutBg, 4.5, dark), 255);
                SvgPath.DrawIcon(g, "nav-geral", pad, fy - 1 * sc, 15 * sc, cLink);
                using (SolidBrush b = new SolidBrush(cLink)) g.DrawString(cfg, fFoot, b, pad + 20 * sc, fy, tf);
                hit.SettingsLink = Rectangle.Round(new RectangleF(pad - 6 * sc + m, fy - 6 * sc + m, cw + 32 * sc, 26 * sc));

                string tu = SpeedFmt.Compact(s, totUpBps, true), td = SpeedFmt.Compact(s, totDnBps, false);
                float wtu = g.MeasureString(tu, fFootB, PointF.Empty, tf).Width, wtd = g.MeasureString(td, fFootB, PointF.Empty, tf).Width;
                float sym = 15 * sc;
                float xd = W - pad - wtd;
                using (SolidBrush b = new SolidBrush(WidgetRenderer.Hex(dnHex, 255)))
                {
                    g.DrawString(td, fFootB, b, xd, fy, tf);
                    g.DrawString("▼", fSym, b, xd - sym, fy + 1 * sc, tf);
                }
                float xu = xd - sym - 18 * sc - wtu;
                using (SolidBrush b = new SolidBrush(WidgetRenderer.Hex(upHex, 255)))
                {
                    g.DrawString(tu, fFootB, b, xu, fy, tf);
                    g.DrawString("▲", fSym, b, xu - sym, fy + 1 * sc, tf);
                }
            }
            return bmp;
        }

        // Corta o texto com reticências para caber na largura
        static string Fit(Graphics g, string text, Font f, float max, StringFormat tf)
        {
            if (max <= 0) return "";
            if (g.MeasureString(text, f, PointF.Empty, tf).Width <= max) return text;
            string t = text;
            while (t.Length > 1 && g.MeasureString(t + "…", f, PointF.Empty, tf).Width > max) t = t.Substring(0, t.Length - 1);
            return t + "…";
        }

        static void DrawCentered(Graphics g, string text, Font f, Color c, RectangleF r, StringFormat tf)
        {
            float w = g.MeasureString(text, f, PointF.Empty, tf).Width;
            float h = f.GetHeight(g);
            using (SolidBrush b = new SolidBrush(c))
                g.DrawString(text, f, b, r.X + (r.Width - w) / 2f, r.Y + (r.Height - h) / 2f, tf);
        }
    }
}
