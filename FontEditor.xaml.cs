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
        private int _currentPageIndex = 0;

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
                        _currentPageIndex = 0;
                        LoadPage(_currentPageIndex);
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

        private void LoadPage(int pageIndex)
        {
            // Sécurité de base
            if (!(_chunk is FontChunk fontChunk) || fontChunk.TexturesHash == null || fontChunk.TexturesHash.Count == 0)
            {
                TxtPageInfo.Text = "No Texture";
                return;
            }

            // 1. Validation de l'index
            if (pageIndex < 0) pageIndex = 0;
            if (pageIndex >= fontChunk.TexturesHash.Count) pageIndex = fontChunk.TexturesHash.Count - 1;

            _currentPageIndex = pageIndex;

            // 2. Mise à jour de l'interface (Boutons et Texte)
            TxtPageInfo.Text = $"Page {_currentPageIndex + 1} / {fontChunk.TexturesHash.Count}";
            BtnPrev.IsEnabled = _currentPageIndex > 0;
            BtnNext.IsEnabled = _currentPageIndex < fontChunk.TexturesHash.Count - 1;

            // 3. Nettoyer le canvas (on change d'image, l'ancien rectangle n'est plus valide)
            OverlayCanvas.Children.Clear();

            // 4. Récupérer le Hash spécifique à cette page
            uint targetHash = fontChunk.TexturesHash[_currentPageIndex];

            // 5. Chercher la texture correspondante dans le fichier global
            bool textureFound = false;

            if (_context != null)
            {
                foreach (var file in _context.Files)
                {
                    if (file is ChunkFileEntry fileEntry && fileEntry.FileHash == targetHash)
                    {
                        if (fileEntry.DataChunk is TextureChunk3DS texChunk)
                        {
                            try
                            {
                                var bitmap = texChunk.MakeBitmap();
                                TextureHolder.Source = bitmap;
                                textureFound = true;
                                break; // On a trouvé, on sort de la boucle
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine("Erreur texture: " + ex.Message);
                            }
                        }
                    }
                }
            }

            if (!textureFound)
            {
                // Optionnel : Mettre une image vide ou un placeholder si la texture est introuvable
                TextureHolder.Source = null;
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

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            LoadPage(_currentPageIndex - 1);
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            LoadPage(_currentPageIndex + 1);
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
            // 1. Initialisation
            // IL (11) est le décalage initial pour la PREMIÈRE ligne uniquement.
            double currentX = _font.IL;
            double currentY = 0;

            double textureWidth = _font.PageSize;

            // Hauteur de ligne (RenderHeight semble être la hauteur totale, Height la hauteur de ligne)
            // D'après le header : Height 23, RenderHeight 34. Essayons Height pour le saut de ligne.
            double rowHeight = _font.Height > 0 ? _font.Height : 23;

            foreach (var glyph in _font.Glyphs)
            {
                // --- FILTRE CRITIQUE ---
                // L'espace (32) n'est jamais dessiné sur la texture, on le saute.
                if (glyph.CodePoint == 32) continue;

                // La place réelle occupée par un glyphe dans la texture est :
                // Son décalage vide (Offset/C) + Ses pixels (Width/A) + L'espace global (Spacing)
                double textureSlotWidth = glyph.Offset + glyph.TextureWidth + _font.CharSpacing;

                // --- GESTION DU RETOUR À LA LIGNE ---
                // Si le glyphe dépasse la largeur de l'image
                if (currentX + textureSlotWidth > textureWidth)
                {
                    currentX = 0; // Retour à 0 (ou 1 pour la marge de sécurité)
                    currentY += rowHeight;
                }

                // --- EST-CE LE GLYPHE RECHERCHÉ ? ---
                if (glyph == targetGlyph)
                {
                    // Position X exacte : Le dessin commence APRÈS l'Offset
                    double drawX = currentX + glyph.Offset;

                    return new Rect(drawX, currentY, glyph.TextureWidth, rowHeight);
                }

                // --- AVANCEMENT ---
                // On avance du bloc complet (Offset + Width + Spacing)
                currentX += textureSlotWidth;
            }

            return null; // Glyphe non trouvé
        }
    }
}