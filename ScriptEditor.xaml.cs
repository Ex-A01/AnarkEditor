using EvershadeEditor.LM2;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AnarkBrowser
{
    public partial class ScriptEditor : Window
    {
        private ScriptChunk _scriptChunk;

        // Constructeur typé
        public ScriptEditor(ScriptChunk scriptChunk)
        {
            InitializeComponent();
            _scriptChunk = scriptChunk;

            LoadScript();
        }

        private void LoadScript()
        {
            // Plus besoin de parser ici, le Chunk l'a déjà fait !
            // On bind directement les données du Chunk.

            var allFuncs = _scriptChunk.Scripts.SelectMany(s => s.Functions).ToList();
            FunctionList.ItemsSource = allFuncs;

            if (allFuncs.Count > 0)
                FunctionList.SelectedIndex = 0;
        }

        private void FunctionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Attention au namespace interne de ScriptChunk vs celui de l'ancien fichier
            if (FunctionList.SelectedItem is ScriptChunk.Function func)
            {
                CodeView.Text = func.DecompiledCode;
                VarGrid.ItemsSource = func.Variables.Values.ToList();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Note: La sauvegarde complète du bytecode est très complexe.
            // Pour l'instant, on notifie juste que les valeurs en mémoire (dans l'objet C#) sont modifiées.
            // Pour impacter le binaire, il faudrait réécrire le chunk ScriptData.

            MessageBox.Show("Valeurs mises à jour dans l'objet ScriptFormat.\n(Note: La sérialisation binaire complète requiert une implémentation avancée 'WriteScriptData').", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}