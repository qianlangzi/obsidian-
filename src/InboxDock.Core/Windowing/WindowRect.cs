namespace InboxDock.Core.Windowing;

public readonly record struct WindowRect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}
