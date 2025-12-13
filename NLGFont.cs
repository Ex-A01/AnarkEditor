using System;
using System.Collections.Generic;
using System.Text;

namespace AnarkBrowser
{
    public class NlgFont
    {
        // --- Propriétés XAML ---
        public string FontName { get; set; } = "Unknown";
        public int FontSize { get; set; }
        public int Height { get; set; }
        public int RenderHeight { get; set; }
        public int PageSize { get; set; }
        public int CharSpacing { get; set; }

        // --- Autres propriétés ---
        public string HeaderVersion { get; set; } = "NLG Font Description File..Version 1.1";
        public byte R { get; set; } = 255;
        public byte G { get; set; } = 255;
        public byte B { get; set; } = 255;
        public int PageCount { get; set; }
        public string TextType { get; set; } = "color";
        public string Distribution { get; set; } = "english";
        public int Ascent { get; set; }
        public int RenderAscent { get; set; }
        public int IL { get; set; }
        public int LineHeight { get; set; }

        public List<NlgGlyph> Glyphs { get; set; } = new List<NlgGlyph>();

        public static NlgFont FromBytes(byte[] data)
        {
            var font = new NlgFont();
            // Utiliser UTF8 est plus sûr, mais Default fonctionne souvent si c'est de l'ASCII
            string text = Encoding.UTF8.GetString(data);

            // Séparation par blocs ".."
            var parts = text.Split(new[] { ".." }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                string t = part.Trim();
                if (string.IsNullOrEmpty(t) || t == "END") continue;

                // CORRECTION MAJEURE : On découpe en ignorant les espaces vides/multiples/tabs
                var tokens = t.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                if (tokens.Length == 0) continue;

                string key = tokens[0];

                if (key == "Version" || t.StartsWith("NLG Font"))
                {
                    // Entête
                }
                else if (key == "Font")
                {
                    // Format: Font "Nom" Taille color R G B
                    if (tokens.Length >= 2) font.FontName = tokens[1].Replace("\"", "");

                    if (tokens.Length >= 3 && int.TryParse(tokens[2], out int fs))
                        font.FontSize = fs;

                    if (tokens.Length >= 7 && tokens[3] == "color")
                    {
                        if (byte.TryParse(tokens[4], out byte r)) font.R = r;
                        if (byte.TryParse(tokens[5], out byte g)) font.G = g;
                        if (byte.TryParse(tokens[6], out byte b)) font.B = b;
                    }
                }
                else if (key == "PageSize")
                {
                    font.PageSize = ParseInt(tokens, "PageSize");
                    font.PageCount = ParseInt(tokens, "PageCount");
                    font.TextType = ParseString(tokens, "TextType");
                    font.Distribution = ParseString(tokens, "Distribution");
                }
                else if (key == "Height")
                {
                    font.Height = ParseInt(tokens, "Height");
                    font.RenderHeight = ParseInt(tokens, "RenderHeight");
                    font.Ascent = ParseInt(tokens, "Ascent");
                    font.RenderAscent = ParseInt(tokens, "RenderAscent");
                    font.IL = ParseInt(tokens, "IL");
                }
                else if (key == "CharSpacing")
                {
                    font.CharSpacing = ParseInt(tokens, "CharSpacing");
                    font.LineHeight = ParseInt(tokens, "LineHeight");
                }
                else if (key == "Glyph")
                {
                    // Format: Glyph [ID] Width [A] [B] [C]
                    // Comme on a retiré les espaces vides, les index sont stables :
                    // tokens[0]="Glyph", tokens[1]=ID, tokens[2]="Width", etc.

                    if (tokens.Length >= 6)
                    {
                        var g = new NlgGlyph();

                        // Parsing de l'ID (ex: "32" ou "!")
                        string idStr = tokens[1];
                        if (int.TryParse(idStr, out int idVal))
                            g.CodePoint = (uint)idVal;
                        else if (idStr.Length > 0)
                            g.CodePoint = (uint)idStr[0];

                        // Parsing des largeurs
                        if (tokens[2] == "Width")
                        {
                            if (int.TryParse(tokens[3], out int tw)) g.TexWidth = tw;
                            if (int.TryParse(tokens[4], out int adv)) g.Advance = adv;
                            if (int.TryParse(tokens[5], out int off)) g.Offset = off;
                        }
                        font.Glyphs.Add(g);
                    }
                }
            }
            return font;
        }

        public byte[] ToBytes()
        {
            var sb = new StringBuilder();
            sb.Append(HeaderVersion).Append("..");

            sb.Append($"Font \"{FontName}\" {FontSize} color {R} {G} {B}..");
            sb.Append($"PageSize {PageSize} PageCount {PageCount} TextType {TextType} Distribution {Distribution}..");
            sb.Append($"Height {Height} RenderHeight {RenderHeight} Ascent {Ascent} RenderAscent {RenderAscent} IL {IL}..");
            sb.Append($"CharSpacing {CharSpacing} LineHeight {LineHeight}..");

            foreach (var g in Glyphs)
            {
                // Si c'est un caractère simple (ex: A, !, ?) on l'écrit tel quel pour la lisibilité
                // Sinon on écrit son code (ex: 32 pour Espace)
                string idStr = (g.CodePoint > 32 && g.CodePoint < 127 && !char.IsDigit((char)g.CodePoint))
                    ? ((char)g.CodePoint).ToString()
                    : g.CodePoint.ToString();

                sb.Append($"Glyph {idStr} Width {g.TexWidth} {g.Advance} {g.Offset}..");
            }

            sb.Append("END..");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static int ParseInt(string[] tokens, string key)
        {
            for (int i = 0; i < tokens.Length - 1; i++)
            {
                if (tokens[i] == key && int.TryParse(tokens[i + 1], out int val))
                    return val;
            }
            return 0;
        }

        private static string ParseString(string[] tokens, string key)
        {
            for (int i = 0; i < tokens.Length - 1; i++)
            {
                if (tokens[i] == key) return tokens[i + 1];
            }
            return "";
        }
    }

    public class NlgGlyph
    {
        public uint CodePoint { get; set; }
        public int TexWidth { get; set; }
        public int Advance { get; set; }
        public int Offset { get; set; }

        public string CharDisplay
        {
            get
            {
                try { return char.ConvertFromUtf32((int)CodePoint); }
                catch { return "?"; }
            }
        }
    }
}