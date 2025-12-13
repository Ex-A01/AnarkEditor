using EvershadeEditor.LM2;
using System;
using System.Linq; // Nécessaire pour .Contains()
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes; // Pour BitmapSource

namespace AnarkBrowser
{
    public partial class FontEditor : Window
    {
        private ChunkEntry _chunk;
        private NlgFont _font;
        private LM2File _context; // Référence à l'archive globale pour chercher les textures

        // Constructeur mis à jour pour accepter LM2File
        public FontEditor(ChunkEntry chunk, LM2File context = null)
        {
            InitializeComponent();
            _chunk = chunk;
            _context = context;

            LoadFromChunk();
        }

        private void LoadFromChunk()
        {
            try
            {
                // SCÉNARIO 1 : On a reçu le Wrapper "FontChunk"
                if (_chunk is FontChunk fontChunk)
                {
                    // Lecture (parse FontObject et TexturesHash)
                    fontChunk.Read();
                    _font = fontChunk.FontObject;

                    // --- LOGIQUE D'AFFICHAGE DE TEXTURE ---
                    // Si on a le contexte global et des hashs de textures
                    if (_context != null && fontChunk.TexturesHash != null && fontChunk.TexturesHash.Count > 0)
                    {
                        LoadFontTexture(fontChunk);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Pas de contexte ou pas de hashs de textures pour la police.");
                        MessageBox.Show("Impossible de charger la texture de la police (contexte ou hashs manquants).");
                    }
                }


                // SCÉNARIO 2 : Chunk brut
                else if (_chunk.Data != null && _chunk.Data.Length > 0)
                {
                    _font = NlgFont.FromBytes(_chunk.Data);
                }

                // Liaison des données
                if (_font != null)
                {
                    this.DataContext = _font;
                }
                else
                {
                    MessageBox.Show("Impossible de charger la police.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}");
            }
        }

        private void LoadFontTexture(FontChunk fontChunk)
        {
            // On cherche parmi tous les fichiers chargés dans l'archive
            foreach (var file in _context.Files)
            {
                // On vérifie seulement les ChunkFileEntry (les fichiers nommés/hashés)
                if (file is ChunkFileEntry fileEntry)
                {
                    // "si l'entier à l'offset 4 == un des hash"
                    // ChunkFileEntry.FileHash correspond exactement à l'entier à l'offset 4
                    if (fontChunk.TexturesHash.Contains(fileEntry.FileHash))
                    {
                        // On a trouvé le fichier texture correspondant !
                        // Son contenu (DataChunk) devrait être un TextureChunk3DS
                        if (fileEntry.DataChunk is TextureChunk3DS texChunk)
                        {
                            try
                            {
                                // Génération du bitmap via votre décodeur existant
                                var bitmap = texChunk.MakeBitmap();
                                TextureHolder.Source = bitmap;

                                // Si vous avez plusieurs pages, vous pourriez vouloir les gérer ici.
                                // Pour l'instant, on affiche la première trouvée et on s'arrête.
                                return;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine("Erreur chargement texture font: " + ex.Message);
                                MessageBox.Show("Erreur lors du chargement de la texture de la police.");
                            }
                        }
                    }
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null) return;

            try
            {
                if (_chunk is FontChunk fontChunk)
                {
                    fontChunk.Write();
                }
                else
                {
                    byte[] newData = _font.ToBytes();
                    _chunk.Data = newData;
                    _chunk.Size = (uint)newData.Length;
                }

                MessageBox.Show("Police appliquée en mémoire !");
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur sauvegarde : {ex.Message}");
            }
        }

        private void GlythList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // On vérifie que tout est prêt : un glyphe est sélectionné, la police est chargée, et le canvas a une taille
            if (GlythList.SelectedItem is NlgGlyph selectedGlyph && _font != null && OverlayCanvas.ActualWidth > 0)
            {
                OverlayCanvas.Children.Clear();

                // Calcul de la position logique (basée sur PageSize = 256)
                var rectInfo = CalculateGlyphRect(selectedGlyph);

                if (rectInfo.HasValue)
                {
                    Rect r = rectInfo.Value;

                    // --- CORRECTION MAJEURE : CALCUL DU FACTEUR D'ÉCHELLE ---

                    // 1. On détermine le ratio entre la taille affichée (Canvas) et la taille réelle (PageSize)
                    // Par exemple, si Canvas fait 512px et PageSize fait 256, scale = 2.0
                    double scaleX = OverlayCanvas.ActualWidth / _font.PageSize;
                    double scaleY = OverlayCanvas.ActualHeight / _font.PageSize;

                    // Sécurité : si le PageSize est 0 ou incorrect, on évite des valeurs infinies
                    if (double.IsNaN(scaleX) || double.IsInfinity(scaleX)) scaleX = 1;
                    if (double.IsNaN(scaleY) || double.IsInfinity(scaleY)) scaleY = 1;

                    // 2. On crée le rectangle en multipliant ses dimensions par le facteur d'échelle
                    Rectangle visualRect = new Rectangle
                    {
                        Width = r.Width * scaleX,   // Largeur mise à l'échelle
                        Height = r.Height * scaleY, // Hauteur mise à l'échelle
                        Stroke = Brushes.Red,
                        StrokeThickness = 2,
                        Fill = Brushes.Transparent
                    };

                    // 3. On positionne le rectangle en multipliant ses coordonnées par le facteur d'échelle
                    Canvas.SetLeft(visualRect, r.X * scaleX);
                    Canvas.SetTop(visualRect, r.Y * scaleY);

                    OverlayCanvas.Children.Add(visualRect);
                }
            }
        }

        /// <summary>
        /// Calcule la position du glyphe en simulant le remplissage de la texture
        /// </summary>
        private Rect? CalculateGlyphRect(NlgGlyph targetGlyph)
        {
            // On part du coin haut-gauche
            double currentX = 0;
            double currentY = 0;

            // Largeur totale de l'image (PageSize)
            double textureWidth = _font.PageSize;

            // Hauteur d'une ligne de glyphes (souvent Height ou LineHeight selon le format)
            // Sécurité : si Height est 0, on évite la boucle infinie ou division par 0
            double rowHeight = _font.Height > 0 ? _font.Height : 20;

            foreach (var glyph in _font.Glyphs)
            {
                // Vérification de dépassement de ligne (Wrap)
                // Si la position actuelle + la largeur du glyphe dépasse la largeur de l'image...
                if (currentX + glyph.TextureWidth > textureWidth)
                {
                    // ... on passe à la ligne suivante
                    currentX = 0;
                    currentY += rowHeight;
                }

                // Est-ce le glyphe que l'on cherche ?
                if (glyph == targetGlyph)
                {
                    // On retourne le rectangle calculé
                    return new Rect(currentX, currentY, glyph.TextureWidth, rowHeight);
                }

                // Avancer le curseur X pour le prochain glyphe
                currentX += glyph.TextureWidth;
            }

            return null; // Glyphe non trouvé
        }
    }
}