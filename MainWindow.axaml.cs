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
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;

namespace ScreenSaver;

public partial class MainWindow : Window
{
    private readonly List<string> _imagePaths = new();
    private int _currentImageIndex = -1;
    private Bitmap? _preloadedBitmap;
    private DispatcherTimer? _timer;
    private ScreenSaverSettings _settings = new();
    private readonly string _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "settings.json");
    private readonly Random _random = new();
    
    private Button _settingsButton;
    private DispatcherTimer _hideButtonTimer;
    private Image _image1, _image2;
    
    private bool _isImage1Primary = true;
    private bool _isAnimating;
    private CancellationTokenSource _cts = new();

    public MainWindow()
    {
        InitializeComponent();
        
        _settingsButton = this.FindControl<Button>("SettingsButton")!;
        _image1 = this.FindControl<Image>("Image1")!;
        _image2 = this.FindControl<Image>("Image2")!;

        _hideButtonTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _hideButtonTimer.Tick += (s, e) => { _settingsButton.IsVisible = false; _hideButtonTimer.Stop(); };
        _settingsButton.Click += SettingsButton_Click;
        this.PointerPressed += MainWindow_PointerPressed;
        
        WindowState = WindowState.FullScreen;
        _ = RestartAsync();
    }

    private async Task RestartAsync()
    {
        _timer?.Stop();
        _isAnimating = false;
        
        _cts.Cancel();
        _cts = new CancellationTokenSource();

        LoadSettings();
        
        LoadImagePaths();
        Console.WriteLine($"Found {_imagePaths.Count} images.");

        if (_imagePaths.Count == 0)
        {
            Console.WriteLine("No images found. Check settings.");
            return;
        }

        _currentImageIndex = _imagePaths.Count - 1;
        await PreloadNextImageAsync(_cts.Token);
        
        await ShowNextImageAsync(true, _cts.Token);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_settings.ImageDisplayTimeSeconds) };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }
    
    private async Task PreloadNextImageAsync(CancellationToken token)
    {
        if (_imagePaths.Count < 2 || _preloadedBitmap is not null) return;
        
        var nextIndex = (_currentImageIndex + 1) % _imagePaths.Count;
        var nextImagePath = _imagePaths[nextIndex];

        if (token.IsCancellationRequested) return;
        
        try
        {
            Console.WriteLine($"Preloading image {nextIndex}: {nextImagePath}");
            var loadedBitmap = await LoadBitmapAsync(nextImagePath, token);
            if (!token.IsCancellationRequested)
            {
                _preloadedBitmap = loadedBitmap;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to preload '{nextImagePath}'. It will be removed. Error: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() => _imagePaths.Remove(nextImagePath));
        }
    }

    private async Task ShowNextImageAsync(bool isInitial = false, CancellationToken token = default)
    {
        if (_imagePaths.Count == 0 || _isAnimating) return;
        
        if (!isInitial && _preloadedBitmap is null)
        {
            Console.WriteLine("Next image not ready, skipping tick.");
            _ = PreloadNextImageAsync(token); 
            return;
        }
        
        _isAnimating = true;

        var primaryImage = _isImage1Primary ? _image1 : _image2;
        var secondaryImage = _isImage1Primary ? _image2 : _image1;
        
        Bitmap? bmp = _preloadedBitmap;
        _preloadedBitmap = null; 
        
        _currentImageIndex = (_currentImageIndex + 1) % _imagePaths.Count;
        Console.WriteLine($"Showing image {_currentImageIndex}");
        secondaryImage.Source = bmp;

        if (isInitial)
        {
            primaryImage.Opacity = 1;
            primaryImage.RenderTransform = null;
            secondaryImage.RenderTransform = null;
            primaryImage.Source = bmp;
        }
        else
        {
            var mode = _settings.Mode;
            var effect = _settings.Effect;
            Animation? animOut = null;
            Animation? animIn = null;
            // Сброс трансформаций
            primaryImage.RenderTransform = null;
            secondaryImage.RenderTransform = null;
            switch (mode)
            {
                case TransitionMode.FullReplace:
                    switch (effect)
                    {
                        case TransitionEffect.Fade:
                            animOut = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0.0) } } } };
                            animIn = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1.0) } } } };
                            break;
                        case TransitionEffect.SlideLeft:
                        case TransitionEffect.SlideRight:
                        {
                            double width = primaryImage.Bounds.Width > 0 ? primaryImage.Bounds.Width : primaryImage.Width;
                            if (width == 0) width = this.Bounds.Width;
                            var outTransform = new TranslateTransform();
                            var inTransform = new TranslateTransform();
                            primaryImage.RenderTransform = outTransform;
                            secondaryImage.RenderTransform = inTransform;
                            double outTo = effect == TransitionEffect.SlideLeft ? -width : width;
                            double inFrom = effect == TransitionEffect.SlideLeft ? width : -width;
                            animOut = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, outTo) } } } };
                            animIn = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.XProperty, inFrom) } }, new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, 0.0) } } } };
                            break;
                        }
                        case TransitionEffect.ZoomIn:
                        case TransitionEffect.ZoomOut:
                        {
                            var outTransform = new ScaleTransform(1,1);
                            var inTransform = new ScaleTransform(1,1);
                            primaryImage.RenderTransform = outTransform;
                            secondaryImage.RenderTransform = inTransform;
                            double outTo = effect == TransitionEffect.ZoomIn ? 0.5 : 1.5;
                            double inFrom = effect == TransitionEffect.ZoomIn ? 0.5 : 1.5;
                            animOut = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(1), Setters = { new Setter(ScaleTransform.ScaleXProperty, outTo), new Setter(ScaleTransform.ScaleYProperty, outTo), new Setter(OpacityProperty, 0.0) } } } };
                            animIn = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(0), Setters = { new Setter(ScaleTransform.ScaleXProperty, inFrom), new Setter(ScaleTransform.ScaleYProperty, inFrom), new Setter(OpacityProperty, 0.0) } }, new KeyFrame { Cue = new Cue(1), Setters = { new Setter(ScaleTransform.ScaleXProperty, 1.0), new Setter(ScaleTransform.ScaleYProperty, 1.0), new Setter(OpacityProperty, 1.0) } } } };
                            break;
                        }
                        case TransitionEffect.Rotate:
                        {
                            var outTransform = new RotateTransform(0);
                            var inTransform = new RotateTransform(0);
                            primaryImage.RenderTransform = outTransform;
                            secondaryImage.RenderTransform = inTransform;
                            animOut = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(1), Setters = { new Setter(RotateTransform.AngleProperty, 90.0), new Setter(OpacityProperty, 0.0) } } } };
                            animIn = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(0), Setters = { new Setter(RotateTransform.AngleProperty, -90.0), new Setter(OpacityProperty, 0.0) } }, new KeyFrame { Cue = new Cue(1), Setters = { new Setter(RotateTransform.AngleProperty, 0.0), new Setter(OpacityProperty, 1.0) } } } };
                            break;
                        }
                    }
                    break;
                case TransitionMode.PartialOverlay:
                {
                    var outTransform = new RotateTransform(0);
                    var inTransform = new RotateTransform(0);
                    primaryImage.RenderTransform = outTransform;
                    secondaryImage.RenderTransform = inTransform;
                    animOut = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0.5), new Setter(RotateTransform.AngleProperty, 10.0) } } } };
                    animIn = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.0), new Setter(RotateTransform.AngleProperty, -10.0) } }, new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1.0), new Setter(RotateTransform.AngleProperty, 0.0) } } } };
                    break;
                }
                case TransitionMode.Slide:
                {
                    double width = primaryImage.Bounds.Width > 0 ? primaryImage.Bounds.Width : primaryImage.Width;
                    if (width == 0) width = this.Bounds.Width;
                    var outTransform = new TranslateTransform();
                    var inTransform = new TranslateTransform();
                    primaryImage.RenderTransform = outTransform;
                    secondaryImage.RenderTransform = inTransform;
                    animOut = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, -width) } } } };
                    animIn = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.XProperty, width) } }, new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, 0.0) } } } };
                    break;
                }
                case TransitionMode.Scale:
                {
                    var outTransform = new ScaleTransform(1,1);
                    var inTransform = new ScaleTransform(1,1);
                    primaryImage.RenderTransform = outTransform;
                    secondaryImage.RenderTransform = inTransform;
                    animOut = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(1), Setters = { new Setter(ScaleTransform.ScaleXProperty, 0.5), new Setter(ScaleTransform.ScaleYProperty, 0.5), new Setter(OpacityProperty, 0.0) } } } };
                    animIn = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(0), Setters = { new Setter(ScaleTransform.ScaleXProperty, 0.5), new Setter(ScaleTransform.ScaleYProperty, 0.5), new Setter(OpacityProperty, 0.0) } }, new KeyFrame { Cue = new Cue(1), Setters = { new Setter(ScaleTransform.ScaleXProperty, 1.0), new Setter(ScaleTransform.ScaleYProperty, 1.0), new Setter(OpacityProperty, 1.0) } } } };
                    break;
                }
                case TransitionMode.Rotate:
                {
                    var outTransform = new RotateTransform(0);
                    var inTransform = new RotateTransform(0);
                    primaryImage.RenderTransform = outTransform;
                    secondaryImage.RenderTransform = inTransform;
                    animOut = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(1), Setters = { new Setter(RotateTransform.AngleProperty, 90.0), new Setter(OpacityProperty, 0.0) } } } };
                    animIn = new Animation { Duration = TimeSpan.FromSeconds(1.5), Children = { new KeyFrame { Cue = new Cue(0), Setters = { new Setter(RotateTransform.AngleProperty, -90.0), new Setter(OpacityProperty, 0.0) } }, new KeyFrame { Cue = new Cue(1), Setters = { new Setter(RotateTransform.AngleProperty, 0.0), new Setter(OpacityProperty, 1.0) } } } };
                    break;
                }
            }
            if (animOut != null && animIn != null)
                await Task.WhenAll(animOut.RunAsync(primaryImage, token), animIn.RunAsync(secondaryImage, token));
            else
                await Task.Delay(500, token); // fallback
        }
        
        primaryImage.Source = null;
        _isImage1Primary = !_isImage1Primary;
        _isAnimating = false;

        _ = PreloadNextImageAsync(token);
    }
    
    private async void Timer_Tick(object? sender, EventArgs e) => await ShowNextImageAsync(false, _cts.Token);
    
    #region Helper and Setup Methods
    private void LoadImagePaths()
    {
        _imagePaths.Clear();
        var imageFolderPath = _settings.ImageFolderPath;
        if (imageFolderPath.StartsWith("~"))
        {
            imageFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), imageFolderPath.Substring(2));
        }

        if (!Directory.Exists(imageFolderPath))
        {
            Console.WriteLine($"Image directory not found: {imageFolderPath}");
            return;
        }

        var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };
        var files = Directory.EnumerateFiles(imageFolderPath, "*.*", SearchOption.TopDirectoryOnly)
                             .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

        _imagePaths.AddRange(files);

        if (_settings.Shuffle)
        {
            int n = _imagePaths.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                (_imagePaths[k], _imagePaths[n]) = (_imagePaths[n], _imagePaths[k]);
            }
        }
    }
    
    private Task<Bitmap> LoadBitmapAsync(string path, CancellationToken token)
    {
        return Task.Run(() => new Bitmap(path), token);
    }
    
    private void MainWindow_PointerPressed(object? sender, PointerPressedEventArgs e) { if (e.Source is not Button) Close(); }
    protected override void OnKeyDown(KeyEventArgs e) => Close();
    protected override void OnPointerMoved(PointerEventArgs e) { _settingsButton.IsVisible = true; _hideButtonTimer.Stop(); _hideButtonTimer.Start(); }
    
    private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        _hideButtonTimer.Stop();
        var settingsWindow = new SettingsWindow(_settings);
        if (await settingsWindow.ShowDialog<bool>(this)) { SaveSettings(); await RestartAsync(); }
        else { if (_imagePaths.Count > 0) _timer?.Start(); }
        _settingsButton.IsVisible = false;
    }
    
    private void LoadSettings()
    {
        if (File.Exists(_settingsFilePath))
        {
            try { _settings = JsonSerializer.Deserialize<ScreenSaverSettings>(File.ReadAllText(_settingsFilePath)) ?? new(); }
            catch (Exception ex) { Console.WriteLine($"Error reading settings file: {ex.Message}"); _settings = new(); }
        }
    }
    
    private void SaveSettings() => File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
    #endregion
}