namespace DotnetProjectKit;

public enum RuntimeArchitecture
{
    X64,
    Arm64,
    X86
}

public sealed record RuntimeTarget(string Rid)
{
    public static RuntimeTarget Linux(RuntimeArchitecture architecture) => FromPlatform("linux", architecture);

    public static RuntimeTarget Windows(RuntimeArchitecture architecture) => FromPlatform("windows", architecture);

    public static RuntimeTarget MacOS(RuntimeArchitecture architecture) => FromPlatform("macos", architecture);

    public static RuntimeTarget FromPlatform(string platform, RuntimeArchitecture architecture)
    {
        var prefix = platform.ToLowerInvariant() switch
        {
            "linux" => "linux",
            "windows" or "win" => "win",
            "macos" or "mac" or "osx" => "osx",
            _ => throw new ArgumentException($"Unknown runtime platform: {platform}", nameof(platform))
        };

        return new RuntimeTarget($"{prefix}-{ToRidSuffix(architecture)}");
    }

    public static RuntimeArchitecture ParseArchitecture(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "x64" or "amd64" or "x86_64" => RuntimeArchitecture.X64,
            "arm64" or "aarch64" => RuntimeArchitecture.Arm64,
            "x86" or "i386" or "i686" => RuntimeArchitecture.X86,
            _ => throw new ArgumentException($"Unknown runtime architecture: {value}", nameof(value))
        };
    }

    public static string ToRidSuffix(RuntimeArchitecture architecture)
    {
        return architecture switch
        {
            RuntimeArchitecture.X64 => "x64",
            RuntimeArchitecture.Arm64 => "arm64",
            RuntimeArchitecture.X86 => "x86",
            _ => throw new ArgumentOutOfRangeException(nameof(architecture), architecture, null)
        };
    }
}
