using ScribbyApp.Services;
using ScribbyApp.Models;
using System.Collections.ObjectModel;

namespace ScribbyApp.Views
{
    public partial class CodeListPage : ContentPage
    {
        private readonly DatabaseService _databaseService;
        private readonly Random _random = new();
        private readonly List<string> _icons = new() { "dotnet_bot.png", "icon_code_blue.png", "icon_code_green.png" }; // Ensure these images exist in Resources/Images

        public CodeListPage(DatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadScripts();
        }

        private async Task LoadScripts()
        {
            var scripts = await _databaseService.GetScriptsAsync();
            ScriptsCollectionView.ItemsSource = new ObservableCollection<UserScript>(scripts);
            EmptyLabel.IsVisible = !scripts.Any();
        }

        private async void OnAddClicked(object sender, EventArgs e)
        {
            string scriptName = await DisplayPromptAsync("New Script", "Enter a name for your new script:");

            if (string.IsNullOrWhiteSpace(scriptName))
                return;

            var newScript = new UserScript
            {
                Name = scriptName,
                Code = "<!DOCTYPE html>\n<html>\n<head>\n  <title>My Script</title>\n</head>\n<body>\n\n  <h1>Hello, World!</h1>\n\n</body>\n</html>",
                Icon = _icons[_random.Next(_icons.Count)]
            };

            await _databaseService.SaveScriptAsync(newScript);

            // We need to fetch the script again to ensure we have the correct ID from the DB
            var savedScripts = await _databaseService.GetScriptsAsync();
            var savedScript = savedScripts.LastOrDefault(s => s.Name == scriptName);

            if (savedScript != null)
            {
                await Shell.Current.GoToAsync($"{nameof(CodeEditorPage)}?scriptId={savedScript.ID}");
            }
        }

        private async void OnEditClicked(object sender, EventArgs e)
        {
            if (sender is Button { CommandParameter: UserScript script })
            {
                await Shell.Current.GoToAsync($"{nameof(CodeEditorPage)}?scriptId={script.ID}");
            }
        }

        private async void OnRunClicked(object sender, EventArgs e)
        {
            if (sender is Button { CommandParameter: UserScript script })
            {
                var navigationParameter = new Dictionary<string, object>
                {
                    { "Code", script.Code }
                };
                await Shell.Current.GoToAsync(nameof(CodePreviewPage), navigationParameter);
            }
        }
    }
}