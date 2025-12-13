using EvershadeEditor.LM2;
using System;
using System.Linq; // Nécessaire pour .Contains()
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging; // Pour BitmapSource

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

    }
}