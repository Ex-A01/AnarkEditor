using EvershadeEditor.LM2;
using System;
using System.Text;
using System.Windows;

namespace AnarkBrowser
{
    public partial class FontEditor : Window
    {
        private ChunkEntry _chunk;
        private NlgFont _font;

        public FontEditor(ChunkEntry chunk)
        {
            InitializeComponent();
            _chunk = chunk;

            LoadFromChunk();
        }

        private void LoadFromChunk()
        {
            try
            {
                // SCÉNARIO 1 : On a reçu le Wrapper "FontChunk" (Le dossier parent)
                // C'est le cas recommandé si vous ouvrez depuis l'arbre principal
                if (_chunk is FontChunk fontChunk)
                {
                    // Si l'objet n'a pas encore été lu, on force la lecture
                    fontChunk.Read();

                    _font = fontChunk.FontObject;
                }
                // SCÉNARIO 2 : On a reçu le Chunk de données brutes (Le fichier enfant 0x7011)
                else if (_chunk.Data != null && _chunk.Data.Length > 0)
                {
                    // On utilise la nouvelle méthode statique FromBytes
                    _font = NlgFont.FromBytes(_chunk.Data);
                }

                // Liaison des données à l'interface
                if (_font != null)
                {
                    this.DataContext = _font;
                    MessageBox.Show("FONT LOADED");
                }
                else
                {
                    MessageBox.Show("Impossible de charger la police (Données vides ou Chunk incorrect).");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors du parsing de la police : {ex.Message}");
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_font == null) return;

            try
            {
                // SCÉNARIO 1 : Mise à jour via le Wrapper
                if (_chunk is FontChunk fontChunk)
                {
                    // La méthode Write() du FontChunk va générer les bytes et mettre à jour l'enfant automatiquement
                    // (Voir votre implémentation dans Chunk.cs)
                    fontChunk.Write();
                }
                // SCÉNARIO 2 : Mise à jour manuelle du Chunk de données
                else
                {
                    // On génère les octets avec la nouvelle méthode ToBytes()
                    byte[] newData = _font.ToBytes();

                    _chunk.Data = newData;
                    _chunk.Size = (uint)newData.Length;
                }

                MessageBox.Show("Police appliquée en mémoire !\n(N'oubliez pas de sauvegarder le fichier .data global)");
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la génération de la police : {ex.Message}");
            }
        }
    }
}