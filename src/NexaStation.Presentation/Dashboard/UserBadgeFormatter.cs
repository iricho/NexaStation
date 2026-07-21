using System.Text;

namespace NexaStation.Presentation.Dashboard;

public static class UserBadgeFormatter
{
    public static string CreateInitials(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return "USR";

        string initials = string.Concat(userName
            .EnumerateRunes()
            .Where(Rune.IsLetterOrDigit)
            .Take(3)
            .Select(rune => rune.ToString()));

        return string.IsNullOrWhiteSpace(initials) ? "USR" : initials.ToUpperInvariant();
    }
}
