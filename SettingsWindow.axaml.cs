using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenSaver;

public partial class SettingsWindow : Window
{
    private readonly ScreenSaverSettings _settings;

    public SettingsWindow()
    {
        // This constructor is for the XAML previewer.
        InitializeComponent();
        _settings = new ScreenSaverSettings(); 
    }

    public SettingsWindow(ScreenSaverSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        // Load current settings into the UI
        var shuffleCheckBox = this.FindControl<CheckBox>("ShuffleCheckBox")!;
        var timeUpDown = this.FindControl<NumericUpDown>("TimeUpDown")!;
        var pathTextBox = this.FindControl<TextBox>("PathTextBox")!;
        
        shuffleCheckBox.IsChecked = _settings.Shuffle;
        timeUpDown.Value = _settings.ImageDisplayTimeSeconds;
        pathTextBox.Text = _settings.ImagesPath;

        // Attach event handlers
        var browseButton = this.FindControl<Button>("BrowseButton")!;
        browseButton.Click += BrowseButton_Click;
        
        var saveButton = this.FindControl<Button>("SaveButton")!;
        saveButton.Click += SaveButton_Click;

        var cancelButton = this.FindControl<Button>("CancelButton")!;
        cancelButton.Click += CancelButton_Click;
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        var pathTextBox = this.FindControl<TextBox>("PathTextBox")!;
        var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку с изображениями",
            AllowMultiple = false
        });

        if (folder.Any())
        {
            pathTextBox.Text = folder.First().Path.LocalPath;
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        var shuffleCheckBox = this.FindControl<CheckBox>("ShuffleCheckBox")!;
        var timeUpDown = this.FindControl<NumericUpDown>("TimeUpDown")!;
        var pathTextBox = this.FindControl<TextBox>("PathTextBox")!;

        _settings.Shuffle = shuffleCheckBox.IsChecked ?? true;
        _settings.ImageDisplayTimeSeconds = (int)timeUpDown.Value;
        _settings.ImagesPath = pathTextBox.Text;
        
        Close(true); // Close and indicate success
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false); // Close and indicate cancellation
    }
} 