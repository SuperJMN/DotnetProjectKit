namespace DotnetProjectKit;

internal static class DesktopHostIdentity
{
    private static readonly string[] Suffixes =
    [
        ".Desktop",
        "-desktop",
        "_desktop",
        " desktop"
    ];

    public static string StripSuffix(string value)
    {
        return TryStripSuffix(value) ?? value;
    }

    public static string? TryStripSuffix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var suffix in Suffixes)
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && value.Length > suffix.Length)
            {
                return value[..^suffix.Length];
            }
        }

        return null;
    }
}
