using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using EvershadeEditor.LM2; // Import du namespace contenant LM2File

namespace AnarkBrowser
{
    public partial class MainWindow : Window
    {
        private LM2File _currentFile;

        public MainWindow()
        {
            InitializeComponent();
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