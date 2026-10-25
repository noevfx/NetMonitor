// Gerado por scripts/gen_icons_cs.py a partir dos SVGs da marca (Logomarca). Nao editar a mao.
using System.Collections.Generic;

namespace NetMonitor.Ui
{
    public static class IconData
    {
        // Cada icone: pares { geometria SVG (grade 24x24), modo: S=traco, F=preenchimento }
        public static readonly Dictionary<string, string[]> All = new Dictionary<string, string[]>
        {
            { "acao-atualizar", new string[] { "M19.5 12a7.5 7.5 0 1 1-2.2-5.3L20 9.5M20 4.5v5h-5", "S" } },
            { "acao-baixar", new string[] { "M12 4v11M7.5 10.5 12 15l4.5-4.5M5 19.5h14", "S" } },
            { "acao-confirmar", new string[] { "M5 12.5l4.5 4.5L19 7.5", "S" } },
            { "acao-copiar", new string[] { "M10,8 H18 A2,2 0 0 1 20,10 V18 A2,2 0 0 1 18,20 H10 A2,2 0 0 1 8,18 V10 A2,2 0 0 1 10,8 Z", "S", "M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2", "S" } },
            { "acao-dropdown", new string[] { "M7 10l5 5 5-5", "S" } },
            { "acao-fixar", new string[] { "M9 4h6M10 4v5l-3 3.5h10L14 9V4M12 12.5V20", "S" } },
            { "acao-restaurar", new string[] { "M4.5 12a7.5 7.5 0 1 0 2.2-5.3L4 9.5M4 4.5v5h5", "S" } },
            { "borda-contorno", new string[] { "M7.5 17A5 5 0 0 1 7.5 7M16.5 7a5 5 0 0 1 0 10M10.5 7h3M10.5 17h3", "S" } },
            { "borda-cor", new string[] { "M3.5,12 A8.5,8.5 0 1 0 20.5,12 A8.5,8.5 0 1 0 3.5,12 Z", "S", "M12 3.5a8.5 8.5 0 0 1 0 17z", "SF" } },
            { "dica", new string[] { "M9 18h6M10 21h4M12 3a6 6 0 0 0-3.5 10.9c.6.5 1 1.2 1 2V16h5v-.1c0-.8.4-1.5 1-2A6 6 0 0 0 12 3z", "S" } },
            { "fundo-pill-solido", new string[] { "M8,4 H16 A4,4 0 0 1 20,8 V16 A4,4 0 0 1 16,20 H8 A4,4 0 0 1 4,16 V8 A4,4 0 0 1 8,4 Z", "S" } },
            { "fundo-pill-translucido", new string[] { "M12 4 3 8.5l9 4.5 9-4.5z", "S", "M3 13l9 4.5 9-4.5", "S" } },
            { "fundo-transparente", new string[] { "M4 8V7.5A3.5 3.5 0 0 1 7.5 4H8M16 4h.5A3.5 3.5 0 0 1 20 7.5V8M20 16v.5a3.5 3.5 0 0 1-3.5 3.5H16M8 20h-.5A3.5 3.5 0 0 1 4 16.5V16M11 4h2M20 11v2M11 20h2M4 11v2", "S" } },
            { "layout-duas-linhas", new string[] { "M4 9.5 7 6.5l3 3M13 8h7M4 14.5l3 3 3-3M13 16h7", "S" } },
            { "layout-linha-unica", new string[] { "M3 14.5 6.5 10l3.5 4.5M12 7v10M14 10l3.5 4.5L21 10", "S" } },
            { "link-externo", new string[] { "M14 4h6v6M20 4l-9 9M18 14v4a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4", "S" } },
            { "link-repositorio", new string[] { "M4.3,5.5 A2.2,2.2 0 1 0 8.7,5.5 A2.2,2.2 0 1 0 4.3,5.5 Z", "S", "M4.3,18.5 A2.2,2.2 0 1 0 8.7,18.5 A2.2,2.2 0 1 0 4.3,18.5 Z", "S", "M15.3,8 A2.2,2.2 0 1 0 19.7,8 A2.2,2.2 0 1 0 15.3,8 Z", "S", "M6.5 7.7v8.6M17.5 10.2c0 3.6-3.6 4.6-8.6 6.4", "S" } },
            { "link-scripts", new string[] { "M8.5 7 3.5 12l5 5M15.5 7l5 5-5 5", "S" } },
            { "link-site", new string[] { "M3,12 A9,9 0 1 0 21,12 A9,9 0 1 0 3,12 Z", "S", "M3 12h18M12 3c2.5 2.6 3.8 5.6 3.8 9s-1.3 6.4-3.8 9c-2.5-2.6-3.8-5.6-3.8-9S9.5 5.6 12 3z", "S" } },
            { "nav-cores-tema", new string[] { "M12 3a9 9 0 1 0 0 18c1.1 0 1.7-.8 1.7-1.7 0-.4-.2-.8-.5-1.1-.3-.3-.5-.7-.5-1.2 0-.9.7-1.6 1.6-1.6H16a5 5 0 0 0 5-5c0-4.1-4-7.4-9-7.4z", "S", "M6.2,11.5 A1.3,1.3 0 1 0 8.8,11.5 A1.3,1.3 0 1 0 6.2,11.5 Z", "F", "M8.5,7.6 A1.3,1.3 0 1 0 11.1,7.6 A1.3,1.3 0 1 0 8.5,7.6 Z", "F", "M13.1,7.6 A1.3,1.3 0 1 0 15.7,7.6 A1.3,1.3 0 1 0 13.1,7.6 Z", "F" } },
            { "nav-geral", new string[] { "M18.92 10.02 L21.48 10.50 L21.48 13.50 L18.92 13.98 L18.30 15.49 L19.77 17.64 L17.64 19.77 L15.49 18.30 L13.98 18.92 L13.50 21.48 L10.50 21.48 L10.02 18.92 L8.51 18.30 L6.36 19.77 L4.23 17.64 L5.70 15.49 L5.08 13.98 L2.52 13.50 L2.52 10.50 L5.08 10.02 L5.70 8.51 L4.23 6.36 L6.36 4.23 L8.51 5.70 L10.02 5.08 L10.50 2.52 L13.50 2.52 L13.98 5.08 L15.49 5.70 L17.64 4.23 L19.77 6.36 L18.30 8.51Z", "S", "M9,12 A3,3 0 1 0 15,12 A3,3 0 1 0 9,12 Z", "S" } },
            { "nav-icones-seta", new string[] { "M8 19V5M4.5 8.5 8 5l3.5 3.5M16 5v14M12.5 15.5 16 19l3.5-3.5", "S" } },
            { "nav-layout-barra", new string[] { "M5.5,4.5 H18.5 A2.5,2.5 0 0 1 21,7 V17 A2.5,2.5 0 0 1 18.5,19.5 H5.5 A2.5,2.5 0 0 1 3,17 V7 A2.5,2.5 0 0 1 5.5,4.5 Z", "S", "M3 15h18", "S", "M7 17.5h2", "S" } },
            { "nav-sobre", new string[] { "M3,12 A9,9 0 1 0 21,12 A9,9 0 1 0 3,12 Z", "S", "M12 11v5.5", "S", "M10.75,7.8 A1.25,1.25 0 1 0 13.25,7.8 A1.25,1.25 0 1 0 10.75,7.8 Z", "F" } },
            { "nav-tipografia", new string[] { "M5 7V5h14v2M12 5v14M9.5 19h5", "S" } },
            { "nav-top5", new string[] { "M4 8h15M15.5 4.5 19 8l-3.5 3.5M20 16H5M8.5 12.5 5 16l3.5 3.5", "S" } },
            { "ordem-taxas", new string[] { "M7 18V6M4 9l3-3 3 3M17 6v12M14 15l3 3 3-3", "S" } },
            { "seta-acronimo", new string[] { "M3 7.5h6.5M6.25 7.5v9M13 7.5l7 9M20 7.5l-7 9", "S" } },
            { "seta-personalizada", new string[] { "M11 4H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-5", "S", "M17.5 3.5a2.1 2.1 0 0 1 3 3L12 15l-4 1 1-4z", "S" } },
            { "seta-rx-chevron", new string[] { "M5 7.5l7 9 7-9", "S" } },
            { "seta-rx-fina", new string[] { "M12 5v14M6.5 13.5 12 19l5.5-5.5", "S" } },
            { "seta-rx-solida", new string[] { "M12 18 5 7h14z", "SF" } },
            { "seta-rx-vazada", new string[] { "M12 18 5 7h14z", "S" } },
            { "seta-tx-chevron", new string[] { "M5 16.5 12 7.5l7 9", "S" } },
            { "seta-tx-fina", new string[] { "M12 19V5M6.5 10.5 12 5l5.5 5.5", "S" } },
            { "seta-tx-solida", new string[] { "M12 6 19 17H5z", "SF" } },
            { "seta-tx-vazada", new string[] { "M12 6 19 17H5z", "S" } },
            { "sistema-cpu", new string[] { "M8,6 H16 A2,2 0 0 1 18,8 V16 A2,2 0 0 1 16,18 H8 A2,2 0 0 1 6,16 V8 A2,2 0 0 1 8,6 Z", "S", "M10.5,9.5 H13.5 A1,1 0 0 1 14.5,10.5 V13.5 A1,1 0 0 1 13.5,14.5 H10.5 A1,1 0 0 1 9.5,13.5 V10.5 A1,1 0 0 1 10.5,9.5 Z", "S", "M9.5 3v3M14.5 3v3M9.5 18v3M14.5 18v3M3 9.5h3M3 14.5h3M18 9.5h3M18 14.5h3", "S" } },
            { "sistema-filtro", new string[] { "M4 5h16l-6.2 7.5V19l-3.6-2v-4.5z", "S" } },
            { "sistema-iniciar", new string[] { "M12 3.5v8", "S", "M7.2 6.3a7.5 7.5 0 1 0 9.6 0", "S" } },
            { "sistema-ram", new string[] { "M4.5,7 H19.5 A1.5,1.5 0 0 1 21,8.5 V14.5 A1.5,1.5 0 0 1 19.5,16 H4.5 A1.5,1.5 0 0 1 3,14.5 V8.5 A1.5,1.5 0 0 1 4.5,7 Z", "S", "M7 16v3M12 16v3M17 16v3M7.5 10v3M12 10v3M16.5 10v3", "S" } },
            { "sobre-cafe", new string[] { "M5 9h11v5a5 5 0 0 1-5 5h-1a5 5 0 0 1-5-5z", "S", "M16 10.5h1.5a2.5 2.5 0 0 1 0 5H16M8.5 3.5V6M12 3.5V6", "S" } },
            { "sobre-chave", new string[] { "M4,15 A4,4 0 1 0 12,15 A4,4 0 1 0 4,15 Z", "S", "M10.8 12.2 19 4M15.5 7.5 18 10", "S" } },
            { "sobre-email", new string[] { "M5.5,5 H18.5 A2.5,2.5 0 0 1 21,7.5 V16.5 A2.5,2.5 0 0 1 18.5,19 H5.5 A2.5,2.5 0 0 1 3,16.5 V7.5 A2.5,2.5 0 0 1 5.5,5 Z", "S", "M3.5 7l8.5 6 8.5-6", "S" } },
            { "sobre-qr", new string[] { "M5.2,4 H8.8 A1.2,1.2 0 0 1 10,5.2 V8.8 A1.2,1.2 0 0 1 8.8,10 H5.2 A1.2,1.2 0 0 1 4,8.8 V5.2 A1.2,1.2 0 0 1 5.2,4 Z", "S", "M15.2,4 H18.8 A1.2,1.2 0 0 1 20,5.2 V8.8 A1.2,1.2 0 0 1 18.8,10 H15.2 A1.2,1.2 0 0 1 14,8.8 V5.2 A1.2,1.2 0 0 1 15.2,4 Z", "S", "M5.2,14 H8.8 A1.2,1.2 0 0 1 10,15.2 V18.8 A1.2,1.2 0 0 1 8.8,20 H5.2 A1.2,1.2 0 0 1 4,18.8 V15.2 A1.2,1.2 0 0 1 5.2,14 Z", "S", "M14 14h2.5v2.5M20 14v.01M14 20h.01M17 20h3v-3", "S" } },
            { "status-ao-vivo", new string[] { "M8,12 A4,4 0 1 0 16,12 A4,4 0 1 0 8,12 Z", "F", "M3.5,12 A8.5,8.5 0 1 0 20.5,12 A8.5,8.5 0 1 0 3.5,12 Z", "S" } },
            { "tema-claro", new string[] { "M8,12 A4,4 0 1 0 16,12 A4,4 0 1 0 8,12 Z", "S", "M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2M5.3 5.3l1.4 1.4M17.3 17.3l1.4 1.4M5.3 18.7l1.4-1.4M17.3 6.7l1.4-1.4", "S" } },
            { "tema-escuro", new string[] { "M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z", "S" } },
            { "tema-sistema", new string[] { "M5,4 H19 A2,2 0 0 1 21,6 V14 A2,2 0 0 1 19,16 H5 A2,2 0 0 1 3,14 V6 A2,2 0 0 1 5,4 Z", "S", "M8.5 20h7M12 16v4", "S" } },
        };
    }
}
