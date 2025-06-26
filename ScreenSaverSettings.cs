namespace ScreenSaver;

public enum TransitionMode
{
    FullReplace,
    PartialOverlay,
    Slide,
    Scale,
    Rotate
}

public enum TransitionEffect
{
    Fade,
    SlideLeft,
    SlideRight,
    ZoomIn,
    ZoomOut,
    Rotate
}

public class ScreenSaverSettings
{
    public string ImageFolderPath { get; set; } = "~/Pictures";
    public int ImageDisplayTimeSeconds { get; set; } = 5;
    public bool Shuffle { get; set; } = true;
    public TransitionMode Mode { get; set; } = TransitionMode.FullReplace;
    public TransitionEffect Effect { get; set; } = TransitionEffect.Fade;
} 