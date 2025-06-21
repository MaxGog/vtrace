using vtrace;

namespace vtrace.Controls;

public class FluentCard : Frame
{
    public FluentCard()
    {
        HasShadow = true;
        BorderColor = Colors.Transparent;
        BackgroundColor = Application.Current.RequestedTheme == AppTheme.Light
            ? Color.FromArgb("#F3F3F3")
            : Color.FromArgb("#2B2B2B");
    }
}