using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenSaver;

public partial class SettingsWindow : Window
{
    private readonly ScreenSaverSettings _settings;
    
    private TextBox _imageFolderPathTextBox = null!;
    private NumericUpDown _imageDisplayTimeNumeric = null!;
    private CheckBox _shuffleCheckBox = null!;
    private Button _browseButton = null!;
    private Button _saveButton = null!;
    private Button _cancelButton = null!;
    private ComboBox _modeComboBox = null!;
    private ComboBox _effectComboBox = null!;

    public SettingsWindow() // Required for the previewer
    {
        InitializeComponent();
        _settings = new ScreenSaverSettings(); // Use dummy settings for preview
        BindControls();
        LoadSettings();
    }

    public SettingsWindow(ScreenSaverSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        BindControls();
        LoadSettings();
    }

    private void BindControls()
    {
        _imageFolderPathTextBox = this.FindControl<TextBox>("ImageFolderPathTextBox")!;
        _imageDisplayTimeNumeric = this.FindControl<NumericUpDown>("ImageDisplayTimeNumeric")!;
        _shuffleCheckBox = this.FindControl<CheckBox>("ShuffleCheckBox")!;
        _browseButton = this.FindControl<Button>("BrowseButton")!;
        _saveButton = this.FindControl<Button>("SaveButton")!;
        _cancelButton = this.FindControl<Button>("CancelButton")!;
        _modeComboBox = this.FindControl<ComboBox>("ModeComboBox")!;
        _effectComboBox = this.FindControl<ComboBox>("EffectComboBox")!;

        _browseButton.Click += BrowseButton_Click;
        _saveButton.Click += (s, e) => { SaveSettings(); Close(true); };
        _cancelButton.Click += (s, e) => Close(false);
    }
    
    private void LoadSettings()
    {
        _imageFolderPathTextBox.Text = _settings.ImageFolderPath;
        _imageDisplayTimeNumeric.Value = _settings.ImageDisplayTimeSeconds;
        _shuffleCheckBox.IsChecked = _settings.Shuffle;
        // Устанавливаем выбранный режим
        foreach (ComboBoxItem item in _modeComboBox.Items!)
        {
            if ((string?)item.Tag == _settings.Mode.ToString())
            {
                _modeComboBox.SelectedItem = item;
                break;
            }
        }
        // Устанавливаем выбранный эффект
        foreach (ComboBoxItem item in _effectComboBox.Items!)
        {
            if ((string?)item.Tag == _settings.Effect.ToString())
            {
                _effectComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void SaveSettings()
    {
        _settings.ImageFolderPath = _imageFolderPathTextBox.Text ?? string.Empty;
        _settings.ImageDisplayTimeSeconds = (int)(_imageDisplayTimeNumeric.Value ?? 5);
        _settings.Shuffle = _shuffleCheckBox.IsChecked ?? true;
        // Сохраняем выбранный режим
        if (_modeComboBox.SelectedItem is ComboBoxItem modeItem && Enum.TryParse<TransitionMode>((string)modeItem.Tag!, out var mode))
            _settings.Mode = mode;
        // Сохраняем выбранный эффект
        if (_effectComboBox.SelectedItem is ComboBoxItem effectItem && Enum.TryParse<TransitionEffect>((string)effectItem.Tag!, out var effect))
            _settings.Effect = effect;
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel is null) return;

        var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку с изображениями",
            AllowMultiple = false
        });

        if (folder.Any())
        {
            _imageFolderPathTextBox.Text = folder[0].Path.LocalPath;
        }
    }
} 