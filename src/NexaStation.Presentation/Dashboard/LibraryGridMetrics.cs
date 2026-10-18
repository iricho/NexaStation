namespace NexaStation.Presentation.Dashboard;

public sealed record LibraryGridMetrics(int Columns, double CardWidth, double CardHeight)
{
    public static LibraryGridMetrics Resolve(double viewportWidth, bool covers = false, bool compact = false)
    {
        double available = Math.Max(1, viewportWidth - 32); // Focus padding and the vertical scrollbar.
        int columns = compact ? 1 : Math.Max(1, (int)Math.Floor(available / (covers ? 210 : 190)));
        double width = Math.Max(1, available / columns - 18);
        double height = compact ? 112 : covers ? width * 1.4 : width * 0.65;
        return new(columns, width, height);
    }
}
