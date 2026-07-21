namespace NexaStation.Presentation.Dashboard;

public sealed class HomeDashboardDensityPolicy : IHomeDashboardDensityPolicy
{
    public HomeDashboardLayout Resolve(double viewportWidth, double viewportHeight)
    {
        if (double.IsNaN(viewportWidth) || double.IsInfinity(viewportWidth) || viewportWidth < 0) throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        if (double.IsNaN(viewportHeight) || double.IsInfinity(viewportHeight) || viewportHeight < 0) throw new ArgumentOutOfRangeException(nameof(viewportHeight));

        if (viewportWidth >= 2200 && viewportHeight >= 1200)
            return new(HomeDashboardDensity.Large, false, 100, 80, 60, 450, 435, 200, 160, 260, 160, 600, 74, 112, 5, 5, 5, 5, 3, 2);
        if (viewportWidth >= 1500 && viewportHeight >= 900)
            return new(HomeDashboardDensity.Standard, false, 82, 64, 48, 300, 330, 150, 112, 184, 112, 440, 52, 96, 5, 5, 5, 5, 3, 2);
        return new(HomeDashboardDensity.Compact, true, 68, 56, 28, 218, 236, 124, 88, 164, 100, 320, 43, 84, 3, 4, 4, 4, 3, 2);
    }
}
