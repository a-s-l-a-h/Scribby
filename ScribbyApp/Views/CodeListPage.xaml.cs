using ScribbyApp.Models;
using ScribbyApp.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ScribbyApp.Views
{
    public partial class CodeListPage : ContentPage
    {
        private readonly DatabaseService _databaseService;
        private readonly Random _random = new();
        private readonly List<string> _icons = new() { "script_image.jpg" };

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
                // --- THIS IS THE FIX ---
                // Define the default code using a VERBATIM STRING (@"...")
                // This ensures all quotes and newlines are preserved perfectly,
                // just like in your working WebViewPage.xaml.cs.
                Code = @"
<!DOCTYPE html>
<html>
<head>
    <title>My New Script</title>
    
</head>
<body>
    
    
        
       
    </div>
</body>
</html>",
                Icon = _icons[_random.Next(_icons.Count)]
            };

            await _databaseService.SaveScriptAsync(newScript);
            var savedScript = (await _databaseService.GetScriptsAsync()).LastOrDefault(s => s.Name == scriptName);
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
                var encodedCode = Uri.EscapeDataString(script.Code);
                var navigationParameter = new Dictionary<string, object>
                {
                    { "CodeToPreview", encodedCode }
                };
                await Shell.Current.GoToAsync(nameof(CodePreviewPage), navigationParameter);
            }
        }
    }
}