using CompressionTools;
using EvershadeEditor.LM2; // Import du namespace contenant LM2File
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.MethodExtention;

namespace AnarkBrowser
{
    public partial class MainWindow : Window
    {
        private LM2File _currentFile;
        private ChunkEntry _selectedChunk; // Chunk en cours d'édition

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Chunk_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is StackPanel panel)
            {
                // 1. On retrouve le TreeViewItem qui contient ce StackPanel
                var treeViewItem = FindParent<TreeViewItem>(panel);

                if (treeViewItem != null)
                {
                    // 2. On force la sélection et le focus sur cet item précis
                    treeViewItem.IsSelected = true;
                    treeViewItem.Focus();

                    // 3. LA CLÉ : On stoppe la propagation vers le parent !
                    e.Handled = true;
                }
            }
        }

        // Petite fonction utilitaire pour remonter l'arbre visuel
        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
        }

        // 1. Gestion de la sélection dans l'arbre
        private void MainTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _selectedChunk = e.NewValue as ChunkEntry;

            if (_selectedChunk != null && _selectedChunk.Data != null && !_selectedChunk.HasChildren)
            {
                EditorPanel.Visibility = Visibility.Visible;
                NoSelectionText.Visibility = Visibility.Collapsed;

                // --- CORRECTION : Créer un MemoryStream pleinement éditable et extensible ---
                var ms = new MemoryStream();
                ms.Write(_selectedChunk.Data, 0, _selectedChunk.Data.Length);
                ms.Position = 0; // Rembobiner au début pour que l'éditeur puisse lire

                HexEdit.Stream = ms;
            }
            else
            {
                EditorPanel.Visibility = Visibility.Collapsed;
                NoSelectionText.Visibility = Visibility.Visible;
                HexEdit.Stream = null;
            }

            // 2. Gestion du bouton Texture (NOUVEAU)
            // On active le bouton seulement si c'est une Texture ou un FileEntry contenant une Texture
            bool isTexture = _selectedChunk is TextureChunk3DS;
            if (isTexture)
            {
                OpenTextureViewer(_selectedChunk as TextureChunk3DS);
            }
            else if (_selectedChunk is ChunkFileEntry cfe && cfe.DataChunk is TextureChunk3DS)
            {
                OpenTextureViewer(cfe.DataChunk as TextureChunk3DS);
            }
        }

        // HELPERS FOR APPLYI?G ELECTED CHUNK CHANGES
        // Trouve l'index dans _currentFile.Chunks (racine) qui contient le target
        private int FindRootChunkIndex(ChunkEntry target)
        {
            if (_currentFile == null) return -1;

            for (int i = 0; i < _currentFile.Chunks.Count; i++)
            {
                ChunkEntry root = _currentFile.Chunks[i];

                // Cas 1: Le chunk sélectionné EST un chunk racine
                if (root == target) return i;

                // Cas 2: Le chunk est un enfant de ce racine (recherche en profondeur)
                if (root.HasChildren && IsChildOf(root, target))
                {
                    return i;
                }
            }
            return -1;
        }

        // Vérifie récursivement si 'parent' contient 'target' dans ses descendants
        private bool IsChildOf(ChunkEntry parent, ChunkEntry target)
        {
            if (parent.Children == null) return false;

            foreach (var child in parent.Children)
            {
                if (child == target) return true;
                // Si l'enfant a lui-même des enfants, on descend encore
                if (child.HasChildren && IsChildOf(child, target)) return true;
            }
            return false;
        }

        // 2. Appliquer les modifications en mémoire (RAM)
        private void ApplyChunkChanges_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFile == null || _selectedChunk == null) return;

            try
            {
                // 1. IMPORTANT : On demande à l'éditeur d'écrire son buffer dans le Stream
                // Si cette méthode n'existe pas dans votre version, essayez HexEdit.CommitChanges() ou HexEdit.ApplyChanges()
                // Mais SubmitChanges() est le standard pour WpfHexaEditor.
                HexEdit.SubmitChanges();

                byte[] newData;

                if (HexEdit.Stream is MemoryStream ms)
                {
                    // On récupère les données du stream qui vient d'être mis à jour
                    newData = ms.ToArray();
                }
                else
                {
                    // Fallback
                    newData = HexEdit.GetAllBytes();
                }

                // --- DEBUG : Vérifiez ici si la valeur est bonne ---
                // if (newData.Length > 0) MessageBox.Show($"Premier octet : {newData[0]:X2}");

                int globalIndex = FindRootChunkIndex(_selectedChunk);

                if (globalIndex != -1)
                {
                    _currentFile.SetChunkData(_selectedChunk, newData, globalIndex);

                    StatusText.Text = $"Chunk updated. Size: {newData.Length} bytes.";

                    // On recharge propre pour confirmer
                    var refreshMs = new MemoryStream();
                    refreshMs.Write(_selectedChunk.Data, 0, _selectedChunk.Data.Length);
                    refreshMs.Position = 0;
                    HexEdit.Stream = refreshMs;

                    MessageBox.Show("Changements appliqués !");
                }
                else
                {
                    MessageBox.Show("Erreur : Impossible de retrouver le chunk racine.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur critique : {ex.Message}");
            }
        }

        // 3. Sauvegarder sur le disque
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFile == null) return;

            var sfd = new SaveFileDialog
            {
                Filter = "LM2 Data (*.data)|*.data",
                FileName = System.IO.Path.GetFileName(_currentFile.DataPath)
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    _currentFile.Save(sfd.FileName);
                    MessageBox.Show("Fichier sauvegardé avec succès !");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur de sauvegarde : {ex.Message}");
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "LM2 Data (*.data)|*.data|All Files (*.*)|*.*",
                Title = "Select the .data file"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadFile(openFileDialog.FileName);
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                // LM2File cherche automatiquement le .dict correspondant
                // Vérifions qu'il existe pour donner un message d'erreur clair si besoin
                string dictPath = System.IO.Path.ChangeExtension(path, ".dict");
                if (!File.Exists(dictPath))
                {
                    MessageBox.Show($"Le fichier dictionnaire est manquant :\n{dictPath}\n\nIl est requis pour lire le .data.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = "Loading...";

                // Utilisation de la classe fournie LM2File
                _currentFile = new LM2File(path);

                // La propriété 'Chunks' de LM2File contient la liste racine chargée par LoadData()
                MainTree.ItemsSource = _currentFile.Chunks;

                StatusText.Text = $"Loaded: {System.IO.Path.GetFileName(path)} ({_currentFile.Chunks.Count} root chunks)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors du chargement : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error.";
            }
        }

        // Méthode helper pour ouvrir la fenêtre proprement
        private void OpenTextureViewer(TextureChunk3DS texChunk)
        {
            try
            {
                // Génération de l'image via la méthode de TextureChunk3DS
                var bitmap = texChunk.MakeBitmap();

                // Création des infos (vérifiez que les propriétés existent bien dans votre TextureChunk3DS)
                // Note: texChunk.GetCompression() doit exister dans TextureChunk3DS
                string info = $"Format: {texChunk.GetCompression()} | Mips: {texChunk.MipLevel}";

                // Ouverture de la fenêtre TextureViewer (celle créée à l'étape précédente)
                var viewer = new TextureViewer(bitmap, info);
                viewer.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors du décodage de la texture : {ex.Message}", "Erreur Texture");
            }
        }

        private void Decomp_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "LM2 Data (*.data)|*.data|All Files (*.*)|*.*",
                Title = "Select the .data file"
            };

            int[] blocs = [ 0, 2, 3 ];

            if (openFileDialog.ShowDialog() == true)
            {

                try
                {
                    LM2Tools.LM2DataExtractor.RebuildCompositeData(openFileDialog.FileName, System.IO.Path.ChangeExtension(openFileDialog.FileName,"REPACK"), blocs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Erreur critique : " + ex.Message);
                }
            }
        }
    }
}