using System.Xml.Linq;
using Serilog;

namespace DotnetProjectKit;

public sealed record ResolvedProjectAsset(string Path, ApplicationInfoSource Source);

public sealed class ProjectAssetResolver
{
    private static readonly string[] IconProperties =
    [
        "PackageIcon",
        "ApplicationIcon"
    ];

    private static readonly string[] LogoProperties =
    [
        "ApplicationLogo",
        "PackageLogo"
    ];

    private static readonly string[] IconFileNames =
    [
        "icon-512.png",
        "icon-256.png",
        "icon.png",
        "Icon.png",
        "icon.svg",
        "logo.svg",
        "logo.png",
        "app.png",
        "app.ico"
    ];

    private static readonly string[] LogoFileNames =
    [
        "logo.png",
        "Logo.png",
        "logo.svg",
        "splash.png",
        "Splash.png",
        "installer-logo.png",
        "installer-logo.svg",
        "icon-512.png",
        "icon-256.png",
        "icon.png",
        "Icon.png",
        "app.png",
        "app.ico",
        "icon.svg"
    ];

    private static readonly string[] AssetDirectories =
    [
        "",
        "Assets",
        "assets",
        "Resources",
        "wwwroot"
    ];

    public ResolvedProjectAsset? ResolveIcon(FileInfo projectFile, ProjectMetadata metadata, ILogger? logger = null)
    {
        return ResolveIcon(projectFile, metadata, null, logger);
    }

    public ResolvedProjectAsset? ResolveIcon(
        FileInfo projectFile,
        ProjectMetadata metadata,
        Func<string, bool>? isSupported,
        ILogger? logger = null)
    {
        return Resolve(projectFile, metadata, IconProperties, IconFileNames, isSupported);
    }

    public ResolvedProjectAsset? ResolveLogo(FileInfo projectFile, ProjectMetadata metadata, ILogger? logger = null)
    {
        return ResolveLogo(projectFile, metadata, null, logger);
    }

    public ResolvedProjectAsset? ResolveLogo(
        FileInfo projectFile,
        ProjectMetadata metadata,
        Func<string, bool>? isSupported,
        ILogger? logger = null)
    {
        return Resolve(projectFile, metadata, LogoProperties, LogoFileNames, isSupported);
    }

    private static ResolvedProjectAsset? Resolve(
        FileInfo projectFile,
        ProjectMetadata metadata,
        IReadOnlyCollection<string> explicitProperties,
        IReadOnlyCollection<string> conventionFileNames,
        Func<string, bool>? isSupported)
    {
        foreach (var propertyName in explicitProperties)
        {
            var explicitAsset = ResolveExplicitPath(projectFile, metadata.Get(propertyName));
            if (explicitAsset is not null && IsSupported(explicitAsset, isSupported))
            {
                return new ResolvedProjectAsset(explicitAsset, ApplicationInfoSource.Msbuild);
            }
        }

        return FindConventionAsset(projectFile, conventionFileNames, isSupported);
    }

    private static string? ResolveExplicitPath(FileInfo projectFile, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
        {
            return File.Exists(normalized) ? normalized : null;
        }

        var projectDirectory = projectFile.Directory?.FullName;
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return null;
        }

        var projectRelativePath = Path.GetFullPath(Path.Combine(projectDirectory, normalized));
        return File.Exists(projectRelativePath) ? projectRelativePath : null;
    }

    private static ResolvedProjectAsset? FindConventionAsset(
        FileInfo projectFile,
        IReadOnlyCollection<string> conventionFileNames,
        Func<string, bool>? isSupported)
    {
        var projectDirectory = projectFile.Directory?.FullName;
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return null;
        }

        var roots = new[] { projectDirectory }
            .Concat(ProjectReferenceDirectories(projectFile))
            .Concat(SearchRoots(projectDirectory).Skip(1));

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var asset = FindAssetIn(root, conventionFileNames, isSupported);
            if (asset is not null)
            {
                return new ResolvedProjectAsset(asset, ApplicationInfoSource.Convention);
            }
        }

        return null;
    }

    private static string? FindAssetIn(
        string directory,
        IReadOnlyCollection<string> conventionFileNames,
        Func<string, bool>? isSupported)
    {
        foreach (var assetDirectory in AssetDirectories)
        {
            foreach (var fileName in conventionFileNames)
            {
                var path = string.IsNullOrEmpty(assetDirectory)
                    ? Path.Combine(directory, fileName)
                    : Path.Combine(directory, assetDirectory, fileName);

                if (File.Exists(path) && IsSupported(path, isSupported))
                {
                    return path;
                }
            }
        }

        return null;
    }

    private static bool IsSupported(string path, Func<string, bool>? isSupported)
    {
        return isSupported is null || isSupported(path);
    }

    private static IEnumerable<string> ProjectReferenceDirectories(FileInfo projectFile)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(projectFile.FullName);
        }
        catch
        {
            yield break;
        }

        var projectDirectory = projectFile.Directory?.FullName;
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            yield break;
        }

        var includes = document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value));

        foreach (var include in includes)
        {
            var normalized = include!.Replace('\\', Path.DirectorySeparatorChar);
            var referencePath = Path.IsPathRooted(normalized)
                ? normalized
                : Path.GetFullPath(Path.Combine(projectDirectory, normalized));
            var referenceDirectory = Path.GetDirectoryName(referencePath);

            if (!string.IsNullOrWhiteSpace(referenceDirectory) && Directory.Exists(referenceDirectory))
            {
                yield return referenceDirectory;
            }
        }
    }

    private static IEnumerable<string> SearchRoots(string projectDirectory)
    {
        var current = new DirectoryInfo(projectDirectory);
        while (current is not null)
        {
            yield return current.FullName;

            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                yield break;
            }

            current = current.Parent;
        }
    }
}
