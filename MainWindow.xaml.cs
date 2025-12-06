using EvershadeEditor.LM2; // Import du namespace contenant LM2File
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexaEditor.Core;

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

            // DEBUG: Voir ce qui est vraiment sélectionné
            if (_selectedChunk != null)
            {
                System.Diagnostics.Debug.WriteLine($"Clic sur: Type={_selectedChunk.Type}, Size={_selectedChunk.Size}, HasData={_selectedChunk.Data != null}");
            }

            // On n'édite que les chunks qui ont des données et PAS d'enfants
            // (Les parents sont juste des conteneurs logiques)
            if (_selectedChunk != null && _selectedChunk.Data != null && !_selectedChunk.HasChildren)
            {
                EditorPanel.Visibility = Visibility.Visible;
                NoSelectionText.Visibility = Visibility.Collapsed;

                // On charge les données dans l'éditeur via un MemoryStream
                HexEdit.Stream = new MemoryStream(_selectedChunk.Data);
            }
            else
            {
                EditorPanel.Visibility = Visibility.Collapsed;
                NoSelectionText.Visibility = Visibility.Visible;
                HexEdit.Stream = null;
            }
        }

        // 2. Appliquer les modifications en mémoire (RAM)
        private void ApplyChunkChanges_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFile == null || _selectedChunk == null)
            {
                MessageBox.Show($"{_currentFile == null} || {_selectedChunk == null}");
                return;
            }

            try
            {
                // Récupérer les bytes modifiés depuis l'éditeur
                byte[] newData = HexEdit.GetAllBytes();

                // Utiliser la méthode SetChunkData de LM2File.cs existante
                // ATTENTION : Cette méthode s'attend à recevoir 'parentIndex' pour décaler les suivants.
                // Dans votre structure actuelle, 'Index' semble être l'index global ou l'index du fichier.
                // Il faut trouver l'index du chunk dans la liste plate _currentFile.Chunks

                int globalIndex = _currentFile.Chunks.IndexOf(_selectedChunk);

                if (globalIndex != -1)
                {
                    // Cette méthode est PRÉCIEUSE : elle calcule la différence de taille
                    // et appelle OffsetChunk() pour décaler tout ce qui se trouve après.
                    // Elle met aussi à jour le Dictionary.
                    // Note: J'ai dû rendre SetChunkData 'public' dans LM2File.cs pour l'appeler ici.
                    _currentFile.SetChunkData(_selectedChunk, newData, globalIndex);

                    StatusText.Text = $"Updated chunk {globalIndex}. Size diff: {newData.Length - _selectedChunk.Size} bytes.";

                    // Rafraichir l'UI (La taille a changé)
                    // Une astuce simple pour forcer le rafraichissement visuel si le binding n'est pas observable
                    MainTree.Items.Refresh();
                    MessageBox.Show("Data patché avec succès !");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'application : {ex.Message}");
            }
        }

        // 3. Sauvegarder sur le disque
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFile == null) return;

            var sfd = new SaveFileDialog
            {
                Filter = "LM2 Data (*.data)|*.data",
                FileName = Path.GetFileName(_currentFile.DataPath)
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
                string dictPath = Path.ChangeExtension(path, ".dict");
                if (!File.Exists(dictPath))
                {
                    MessageBox.Show($"Le fichier dictionnaire est manquant :\n{dictPath}\n\nIl est requis pour lire le .data.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = "Loading...";

                // Utilisation de la classe fournie LM2File
                var _currentFile = new LM2File(path);

                // La propriété 'Chunks' de LM2File contient la liste racine chargée par LoadData()
                MainTree.ItemsSource = _currentFile.Chunks;

                StatusText.Text = $"Loaded: {Path.GetFileName(path)} ({_currentFile.Chunks.Count} root chunks)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors du chargement : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error.";
            }
        }
    }
}