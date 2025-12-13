using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

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
            // Utiliser UTF8 pour récupérer le texte
            string text = Encoding.UTF8.GetString(data);

            // Normalisation : Remplacer les ".." par des sauts de ligne pour un traitement ligne par ligne
            string normalizedText = text.Replace("..", "\n");

            // Découpage propre
            var lines = normalizedText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                string t = line.Trim();
                if (string.IsNullOrEmpty(t) || t == "END") continue;

                // Tokenization par espaces/tabs
                var tokens = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0) continue;

                string key = tokens[0];

                if (key == "Version" || t.StartsWith("NLG Font"))
                {
                    // Header (Optionnel)
                }
                else if (key == "Font")
                {
                    // Recherche du mot clé "color" pour se repérer, car le nom de la police peut contenir des espaces
                    int colorIndex = Array.IndexOf(tokens, "color");

                    if (colorIndex > 1)
                    {
                        // Taille = juste avant "color"
                        if (int.TryParse(tokens[colorIndex - 1], out int fs))
                            font.FontSize = fs;

                        // Couleurs = juste après "color"
                        if (tokens.Length > colorIndex + 3)
                        {
                            byte.TryParse(tokens[colorIndex + 1], out byte r);
                            byte.TryParse(tokens[colorIndex + 2], out byte g);
                            byte.TryParse(tokens[colorIndex + 3], out byte b);
                            font.R = r; font.G = g; font.B = b;
                        }

                        // Nom = Tout ce qui est entre l'index 1 et (colorIndex - 1)
                        StringBuilder nameBuilder = new StringBuilder();
                        for (int i = 1; i < colorIndex - 1; i++)
                        {
                            if (nameBuilder.Length > 0) nameBuilder.Append(" ");
                            nameBuilder.Append(tokens[i]);
                        }
                        font.FontName = nameBuilder.ToString().Replace("\"", "");
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
                    // On cherche "Width" pour savoir où commencent les chiffres
                    int widthIndex = Array.IndexOf(tokens, "Width");

                    if (widthIndex > 1 && tokens.Length > widthIndex + 3)
                    {
                        var g = new NlgGlyph();

                        // CORRECTION ID : Gérer le cas où l'ID est un chiffre (ex: "0") qui doit être lu comme char '0' (48)
                        string idStr = tokens[1];

                        // Si la chaine fait 1 caractère (ex: "!", "A", "0"), c'est toujours un char
                        if (idStr.Length == 1)
                        {
                            g.CodePoint = (uint)idStr[0];
                        }
                        // Sinon (ex: "32", "161"), c'est un entier brut
                        else if (int.TryParse(idStr, out int idVal))
                        {
                            g.CodePoint = (uint)idVal;
                        }

                        // Lecture des propriétés
                        // CORRECTION : On remplit TextureWidth pour correspondre au XAML
                        if (int.TryParse(tokens[widthIndex + 1], out int tw)) g.TextureWidth = tw;
                        if (int.TryParse(tokens[widthIndex + 2], out int adv)) g.Advance = adv;
                        if (int.TryParse(tokens[widthIndex + 3], out int off)) g.Offset = off;

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
                // Logique inverse : Si c'est un caractère imprimable simple, on l'écrit en char, sinon en int
                string idStr = (g.CodePoint > 32 && g.CodePoint < 127 && !char.IsDigit((char)g.CodePoint))
                    ? ((char)g.CodePoint).ToString()
                    : g.CodePoint.ToString();

                // Notez l'utilisation de TextureWidth ici aussi
                sb.Append($"Glyph {idStr} Width {g.TextureWidth} {g.Advance} {g.Offset}..");
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

        // CORRECTION IMPORTANTE : Renommé de TexWidth à TextureWidth 
        // pour correspondre au Binding XAML dans FontEditor.xaml
        public int TextureWidth { get; set; }

        public int Advance { get; set; }
        public int Offset { get; set; }

        public string Character
        {
            get
            {
                try { return char.ConvertFromUtf32((int)CodePoint); }
                catch { return "?"; }
            }
        }
    }
}