using EvershadeEditor.LM2;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AnarkBrowser
{
    public partial class ScriptEditor : Window
    {
        private ScriptChunk _chunk;

        public ScriptEditor(ScriptChunk chunk)
        {
            InitializeComponent();
            _chunk = chunk;

            // Afficher le type de script dans le titre
            this.Title = $"Script Editor - Type: {_chunk.ScriptTypeName} (Hash: {_chunk.HashType:X})";

            LoadData();
        }

        private void LoadData()
        {
            // On récupère toutes les fonctions pour les lister
            var allFunctions = _chunk.Scripts.SelectMany(s => s.Functions).ToList();
            FunctionList.ItemsSource = allFunctions;

            if (allFunctions.Count > 0)
                FunctionList.SelectedIndex = 0;
            else
                MessageBox.Show("Aucune fonction trouvée dans ce script.");
        }

        private void FunctionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FunctionList.SelectedItem is ScriptChunk.Function func)
            {
                // 1. Afficher le code décompilé
                CodeView.Text = func.DecompiledCode;

                // 2. Afficher les variables dans la grille
                // Grâce à la correction { get; set; }, le binding va marcher
                VarGrid.ItemsSource = func.Variables.Values.ToList();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Les variables ont été modifiées dans l'objet en mémoire.\n\nNote : La réécriture binaire (Repack) de scripts COG complexes n'est pas encore implémentée pour éviter la corruption de données.", "Sauvegarde");
        }
    }
}