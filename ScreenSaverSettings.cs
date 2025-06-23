namespace ScreenSaver;

public class ScreenSaverSettings
{
    public string ImageFolderPath { get; set; } = "~/Pictures";
    public int ImageDisplayTimeSeconds { get; set; } = 5;
    public bool Shuffle { get; set; } = true;
} 