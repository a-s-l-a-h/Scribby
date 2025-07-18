using ScribbyApp.Services;
using ScribbyApp.Models;
using System.Text;

namespace ScribbyApp.Views
{
    [QueryProperty(nameof(ScriptId), "scriptId")]
    public partial class CodeEditorPage : ContentPage
    {
        private readonly DatabaseService _databaseService;
        private UserScript _currentScript;

        public string ScriptId
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                    LoadScript(int.Parse(value));
            }
        }

        public CodeEditorPage(DatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;
        }

        private async void LoadScript(int scriptId)
        {
            var scripts = await _databaseService.GetScriptsAsync();
            _currentScript = scripts.FirstOrDefault(s => s.ID == scriptId);

            if (_currentScript != null)
            {
                NameEntry.Text = _currentScript.Name;
                CodeEditor.Text = _currentScript.Code;
                DeleteButton.IsVisible = true;
            }
        }

        // Helper method to handle the save logic
        private async Task<bool> SaveScriptAsync()
        {
            if (_currentScript == null || string.IsNullOrWhiteSpace(NameEntry.Text))
            {
                await DisplayAlert("Error", "Please provide a valid name.", "OK");
                return false;
            }

            _currentScript.Name = NameEntry.Text;
            _currentScript.Code = CodeEditor.Text;

            await _databaseService.SaveScriptAsync(_currentScript);
            return true;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            bool saved = await SaveScriptAsync();
            if (saved)
            {
                await Shell.Current.GoToAsync(".."); // Navigate back to the list
            }
        }

        // UPDATED: This method now checks for unsaved changes before previewing.
        private async void OnPreviewClicked(object sender, EventArgs e)
        {
            bool hasUnsavedChanges = (_currentScript?.Name != NameEntry.Text) || (_currentScript?.Code != CodeEditor.Text);

            if (hasUnsavedChanges)
            {
                bool saveFirst = await DisplayAlert("Unsaved Changes", "You have unsaved changes. Do you want to save before previewing?", "Save and Preview", "Cancel");
                if (saveFirst)
                {
                    bool saved = await SaveScriptAsync();
                    if (!saved) return; // Stop if save failed
                }
                else
                {
                    return; // User cancelled
                }
            }

            // Proceed to preview
            var navigationParameter = new Dictionary<string, object>
            {
                { "Code", CodeEditor.Text }
            };
            await Shell.Current.GoToAsync(nameof(CodePreviewPage), navigationParameter);
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            if (_currentScript == null) return;

            bool confirmed = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete '{_currentScript.Name}'?", "Yes", "No");
            if (confirmed)
            {
                await _databaseService.DeleteScriptAsync(_currentScript);
                await Shell.Current.GoToAsync("..");
            }
        }

        private void OnCodeEditorTextChanged(object sender, TextChangedEventArgs e)
        {
            var lineCount = CodeEditor.Text.Split('\n').Length;
            var sb = new StringBuilder();
            for (int i = 1; i <= lineCount; i++)
            {
                sb.AppendLine(i.ToString());
            }
            LineNumberEditor.Text = sb.ToString();
        }
    }
}