using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScreenSaver;

public partial class MainWindow : Window
{
    private List<string> _imageFiles = new List<string>();
    private int _currentImageIndex = -1;
    private DispatcherTimer? _timer;
    private ScreenSaverSettings _settings = new ScreenSaverSettings();
    private readonly string _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "settings.json");
    private Random _random = new Random();

    private Button _settingsButton;
    private DispatcherTimer _hideButtonTimer;

    private Image _image1;
    private Image _image2;
    private bool _isImage1Primary = true;
    private bool _isAnimating;

    public MainWindow()
    {
        Console.WriteLine("Starting ScreenSaver...");
        InitializeComponent();
        WindowState = WindowState.FullScreen;

        _settingsButton = this.FindControl<Button>("SettingsButton")!;
        _settingsButton.Click += SettingsButton_Click;

        _image1 = this.FindControl<Image>("Image1")!;
        _image2 = this.FindControl<Image>("Image2")!;

        this.PointerPressed += MainWindow_PointerPressed;

        _hideButtonTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _hideButtonTimer.Tick += (s, e) =>
        {
            _settingsButton.IsVisible = false;
            _hideButtonTimer.Stop();
        };

        Restart();
    }

    private void Restart()
    {
        Console.WriteLine("Restarting with new settings...");
        _timer?.Stop();

        LoadSettings();
        Console.WriteLine($"Settings loaded: Shuffle={_settings.Shuffle}, Interval={_settings.ImageDisplayTimeSeconds}s, Path='{_settings.ImagesPath}'");

        LoadImages();
        Console.WriteLine($"Found {_imageFiles.Count} images.");

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_settings.ImageDisplayTimeSeconds)
        };
        _timer.Tick += Timer_Tick;

        if (_imageFiles.Count > 1) // Need at least 2 images for transitions
        {
            _timer.Start();
            ShowNextImage(true); // Initial image without animation
        }
        else if (_imageFiles.Count == 1)
        {
            ShowNextImage(true);
        }
        else
        {
            Console.WriteLine("No images found. The application will close on user input.");
        }
    }

    private void MainWindow_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Button)
        {
            Console.WriteLine("Background clicked, closing.");
            Close();
        }
    }

    private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        _hideButtonTimer.Stop();

        var settingsWindow = new SettingsWindow(_settings);
        var result = await settingsWindow.ShowDialog<bool>(this);

        if (result)
        {
            Console.WriteLine("Settings saved.");
            SaveSettings();
            Restart();
        }
        else
        {
            Console.WriteLine("Settings cancelled.");
            if (_imageFiles.Count > 0) _timer?.Start();
        }
        _settingsButton.IsVisible = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        Console.WriteLine("Key down event, closing.");
        base.OnKeyDown(e);
        Close();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_settingsButton.IsVisible)
        {
            _settingsButton.IsVisible = true;
        }
        _hideButtonTimer.Stop();
        _hideButtonTimer.Start();
    }

    private void LoadSettings()
    {
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                _settings = JsonSerializer.Deserialize<ScreenSaverSettings>(json) ?? new ScreenSaverSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading settings file: {ex.Message}");
                _settings = new ScreenSaverSettings();
            }
        }

        if (string.IsNullOrWhiteSpace(_settings.ImagesPath))
        {
            _settings.ImagesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }
        SaveSettings();
    }
    
    private void SaveSettings()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var newJson = JsonSerializer.Serialize(_settings, options);
        File.WriteAllText(_settingsFilePath, newJson);
    }

    private void LoadImages()
    {
        _imageFiles.Clear();
        var picturesPath = _settings.ImagesPath ?? "";
        if (Directory.Exists(picturesPath))
        {
            try
            {
                _imageFiles = Directory.GetFiles(picturesPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (_settings.Shuffle)
                {
                    _imageFiles = _imageFiles.OrderBy(x => _random.Next()).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading image directory: {ex.Message}");
            }
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        ShowNextImage();
    }

    private async void ShowNextImage(bool isInitial = false)
    {
        if (_imageFiles.Count == 0 || _isAnimating) return;

        _isAnimating = true;
        
        _currentImageIndex = (_currentImageIndex + 1) % _imageFiles.Count;
        var imagePath = _imageFiles[_currentImageIndex];

        var primaryImage = _isImage1Primary ? _image1 : _image2;
        var secondaryImage = _isImage1Primary ? _image2 : _image1;

        try
        {
            secondaryImage.Source = new Bitmap(imagePath);
            Console.WriteLine($"Showing image: {imagePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load image '{imagePath}'. Removing from list. Error: {ex.Message}");
            _imageFiles.RemoveAt(_currentImageIndex);
            if(_currentImageIndex > 0) _currentImageIndex--;
            _isAnimating = false;
            // Immediately try to show the next image if there are any left
            if(_imageFiles.Count > 0) ShowNextImage();
            return;
        }

        if (isInitial)
        {
            primaryImage.Opacity = 1;
            primaryImage.Source = secondaryImage.Source;
            secondaryImage.Source = null;
            _isAnimating = false;
            return;
        }

        var fadeOutAnimation = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.5),
            Children = { new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0.0) } } }
        };

        var fadeInAnimation = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.5),
            Children = { new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1.0) } } }
        };

        await Task.WhenAll(
            fadeOutAnimation.RunAsync(primaryImage),
            fadeInAnimation.RunAsync(secondaryImage)
        );

        primaryImage.Source = null;
        _isImage1Primary = !_isImage1Primary;
        _isAnimating = false;
    }
}