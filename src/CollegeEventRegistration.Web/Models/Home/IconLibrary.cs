namespace CollegeEventRegistration.Web.Models.Home;

/// <summary>
/// Inline SVG icon paths used by the public shell and homepage. Icons are drawn with
/// <c>currentColor</c> strokes so they inherit text colour and stay crisp without an icon font.
/// </summary>
public static class IconLibrary
{
    private static readonly IReadOnlyDictionary<string, string> Paths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["arrow-right"] = """<path d="M4 12h15"/><path d="M13.2 6.3 20 12l-6.8 5.7"/>""",
            ["chevron-left"] = """<path d="M14.5 5.5 8 12l6.5 6.5"/>""",
            ["chevron-right"] = """<path d="M9.5 5.5 16 12l-6.5 6.5"/>""",
            ["menu"] = """<path d="M4 7h16"/><path d="M4 12h16"/><path d="M4 17h16"/>""",
            ["close"] = """<path d="M6 6l12 12"/><path d="M18 6 6 18"/>""",
            ["code"] = """<path d="M9 7.5 4.5 12 9 16.5"/><path d="M15 7.5 19.5 12 15 16.5"/>""",
            ["trophy"] = """<path d="M8 4.5h8V8a4 4 0 0 1-8 0V4.5Z"/><path d="M8 5.5H5.5V7a3 3 0 0 0 3.1 3"/><path d="M16 5.5h2.5V7a3 3 0 0 1-3.1 3"/><path d="M12 12v4"/><path d="M9 20h6"/><path d="M10.5 16h3l.5 4h-4l.5-4Z"/>""",
            ["mic"] = """<rect x="9" y="3" width="6" height="10.5" rx="3"/><path d="M5.5 11a6.5 6.5 0 0 0 13 0"/><path d="M12 17.5V21"/><path d="M8.5 21h7"/>""",
            ["globe"] = """<circle cx="12" cy="12" r="8.5"/><path d="M3.5 12h17"/><path d="M12 3.5c2.4 2.4 3.6 5.4 3.6 8.5S14.4 18.1 12 20.5C9.6 18.1 8.4 15.1 8.4 12S9.6 5.9 12 3.5Z"/>""",
            ["palette"] = """<path d="M12 3.5a8.5 8.5 0 1 0 0 17c1.1 0 1.8-.8 1.8-1.7 0-.5-.2-.9-.5-1.2-.3-.3-.4-.7-.4-1.1 0-.9.7-1.7 1.7-1.7h1.7a4.2 4.2 0 0 0 4.2-4.2c0-4-4.2-7.1-8.5-7.1Z"/><circle cx="8.2" cy="10.2" r=".9" fill="currentColor" stroke="none"/><circle cx="11.2" cy="7.6" r=".9" fill="currentColor" stroke="none"/><circle cx="15.1" cy="8.2" r=".9" fill="currentColor" stroke="none"/>""",
            ["shield-check"] = """<path d="M12 3.5 5 6v5.4c0 4.2 3 7.2 7 8.9 4-1.7 7-4.7 7-8.9V6l-7-2.5Z"/><path d="m9 12 2.2 2.2L15.6 10"/>""",
            ["flask"] = """<path d="M9.5 3.5h5"/><path d="M10.5 3.5v5L6.2 17a2.4 2.4 0 0 0 2.1 3.5h7.4A2.4 2.4 0 0 0 17.8 17L13.5 8.5v-5"/><path d="M7.7 14.5h8.6"/>""",
            ["award"] = """<circle cx="12" cy="9.5" r="5.5"/><path d="M8.8 14.2 7.5 21l4.5-2.4 4.5 2.4-1.3-6.8"/>""",
            ["check"] = """<path d="m5 12.5 4.5 4.5L19 7"/>""",
            ["map-pin"] = """<path d="M12 21s7-5.6 7-11a7 7 0 1 0-14 0c0 5.4 7 11 7 11Z"/><circle cx="12" cy="10" r="2.5"/>""",
            ["mail"] = """<rect x="3" y="5.5" width="18" height="13" rx="2"/><path d="m3.8 7 8.2 6 8.2-6"/>""",
            ["clock"] = """<circle cx="12" cy="12" r="8.5"/><path d="M12 7.5V12l3.2 1.8"/>""",
            ["calendar"] = """<rect x="3.5" y="5.5" width="17" height="15" rx="2"/><path d="M3.5 10.2h17"/><path d="M8 3.5v4"/><path d="M16 3.5v4"/>""",
            ["building"] = """<path d="M5 20.5V5.2A1.7 1.7 0 0 1 6.7 3.5h6.6A1.7 1.7 0 0 1 15 5.2v15.3"/><path d="M15 9.5h2.6a1.4 1.4 0 0 1 1.4 1.4v9.6"/><path d="M3.5 20.5h17"/><path d="M8.2 7.5h3.6"/><path d="M8.2 11h3.6"/><path d="M8.2 14.5h3.6"/>""",
            ["grid"] = """<rect x="4" y="4" width="7" height="7" rx="1.6"/><rect x="13" y="4" width="7" height="7" rx="1.6"/><rect x="4" y="13" width="7" height="7" rx="1.6"/><rect x="13" y="13" width="7" height="7" rx="1.6"/>""",
            ["dot"] = """<circle cx="12" cy="12" r="3"/>"""
        };

    public static string Get(string? name)
    {
        if (!string.IsNullOrWhiteSpace(name) && Paths.TryGetValue(name, out var paths))
        {
            return paths;
        }

        return Paths["dot"];
    }

    /// <summary>
    /// Maps a stored category to an icon. Categories carry a Bootstrap icon class in the database,
    /// which this project does not load, so the icon is derived from the category identity instead.
    /// </summary>
    public static string ResolveCategoryIcon(string? slug, string? name)
    {
        var haystack = $"{slug} {name}";

        if (haystack.Contains("workshop", StringComparison.OrdinalIgnoreCase)) return "code";
        if (haystack.Contains("hackathon", StringComparison.OrdinalIgnoreCase)) return "trophy";
        if (haystack.Contains("contest", StringComparison.OrdinalIgnoreCase)) return "trophy";
        if (haystack.Contains("lecture", StringComparison.OrdinalIgnoreCase)) return "mic";
        if (haystack.Contains("seminar", StringComparison.OrdinalIgnoreCase)) return "mic";
        if (haystack.Contains("symposium", StringComparison.OrdinalIgnoreCase)) return "globe";
        if (haystack.Contains("cultural", StringComparison.OrdinalIgnoreCase)) return "palette";
        if (haystack.Contains("arts", StringComparison.OrdinalIgnoreCase)) return "palette";

        return "grid";
    }
}
