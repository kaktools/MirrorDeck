namespace MirrorDeck.WinUI.Helpers;

public static class VersionHelper
{
    public static string GetDisplayVersion()
    {
        try
        {
            var versionFile = Path.Combine(AppContext.BaseDirectory, "version.txt");
            if (File.Exists(versionFile))
            {
                var text = File.ReadAllText(versionFile).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? text : $"v{text}";
                }
            }
        }
        catch
        {
            // Fallback below.
        }

        var assemblyVersion = typeof(VersionHelper).Assembly.GetName().Version;
        if (assemblyVersion is null)
        {
            return "v1.0.0";
        }

        return $"v{assemblyVersion.Major}.{assemblyVersion.Minor}.{Math.Max(0, assemblyVersion.Build)}";
    }

    public static bool IsNewer(string? latest, string? current)
    {
        if (string.IsNullOrWhiteSpace(latest))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(current))
        {
            return true;
        }

        static List<int> Parse(string v)
        {
            return v
                .Split(['.', '-', 'v'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var n) ? n : 0)
                .ToList();
        }

        var l = Parse(latest);
        var c = Parse(current);
        var length = Math.Max(l.Count, c.Count);

        for (var i = 0; i < length; i++)
        {
            var lv = i < l.Count ? l[i] : 0;
            var cv = i < c.Count ? c[i] : 0;
            if (lv > cv)
            {
                return true;
            }

            if (lv < cv)
            {
                return false;
            }
        }

        return false;
    }
}
