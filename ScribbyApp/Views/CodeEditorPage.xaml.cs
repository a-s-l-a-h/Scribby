using ScribbyApp.Services;
using ScribbyApp.Models;
using System.Text;
using System.Threading.Tasks;

namespace ScribbyApp.Views
{
    [QueryProperty(nameof(ScriptId), "scriptId")]
    public partial class CodeEditorPage : ContentPage
    {
        private readonly DatabaseService _databaseService;
        private UserScript _currentScript;
        private bool _isNewScript = true;

        public string ScriptId
        {
            set
            {
                if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int id))
                {
                    LoadScript(id);
                    _isNewScript = false;
                }
            }
        }

        public CodeEditorPage(DatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            // Initialize with a default script object for new entries
            _currentScript = new UserScript();
        }

        private async void LoadScript(int scriptId)
        {
            var script = await _databaseService.GetScriptsAsync().ContinueWith(t => t.Result.FirstOrDefault(s => s.ID == scriptId));
            if (script != null)
            {
                _currentScript = script;
                NameEntry.Text = _currentScript.Name;
                CodeEditor.Text = _currentScript.Code;
                DeleteButton.IsVisible = true;
            }
        }

        private async Task<bool> SaveScriptAsync()
        {
            if (string.IsNullOrWhiteSpace(NameEntry.Text))
            {
                await DisplayAlert("Error", "Please provide a valid name for the script.", "OK");
                return false;
            }

            _currentScript.Name = NameEntry.Text;
            _currentScript.Code = CodeEditor.Text;

            await _databaseService.SaveScriptAsync(_currentScript);
            _isNewScript = false; // It's no longer new after the first save
            DeleteButton.IsVisible = true;
            return true;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (await SaveScriptAsync())
            {
                await Shell.Current.GoToAsync("..");
            }
        }

        private async void OnPreviewClicked(object sender, EventArgs e)
        {
            // Check if there are unsaved changes
            bool isDirty = _isNewScript || (_currentScript?.Name != NameEntry.Text) || (_currentScript?.Code != CodeEditor.Text);

            if (isDirty)
            {
                bool saveFirst = await DisplayAlert("Unsaved Changes", "You have unsaved changes. Do you want to save before previewing?", "Save and Preview", "Preview Anyway");
                if (saveFirst)
                {
                    if (!await SaveScriptAsync()) return; // Stop if save fails
                }
            }

            var navigationParameter = new Dictionary<string, object>
            {
                { "CodeToPreview", CodeEditor.Text }
            };
            await Shell.Current.GoToAsync(nameof(CodePreviewPage), navigationParameter);
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            if (_currentScript == null || _isNewScript) return;

            bool confirmed = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete '{_currentScript.Name}'?", "Yes", "No");
            if (confirmed)
            {
                await _databaseService.DeleteScriptAsync(_currentScript);
                await Shell.Current.GoToAsync("..");
            }
        }

        private void OnCodeEditorTextChanged(object sender, TextChangedEventArgs e)
        {
            if (CodeEditor.Text == null) return;
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