using System.Xml.Linq;
using Serilog;

namespace DotnetProjectKit;

public sealed record ResolvedProjectAsset(string Path, ApplicationInfoSource Source);

public sealed class ProjectAssetResolver
{
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

    private static readonly string[] AssetDirectories =
    [
        "",
        "Assets",
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
        foreach (var propertyName in new[] { "PackageIcon", "ApplicationIcon" })
        {
            var explicitIcon = ResolveExplicitPath(projectFile, metadata.Get(propertyName));
            if (explicitIcon is not null && IsSupported(explicitIcon, isSupported))
            {
                return new ResolvedProjectAsset(explicitIcon, ApplicationInfoSource.Msbuild);
            }
        }

        return FindConventionIcon(projectFile, isSupported);
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

    private static ResolvedProjectAsset? FindConventionIcon(FileInfo projectFile, Func<string, bool>? isSupported)
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
            var icon = FindIconIn(root, isSupported);
            if (icon is not null)
            {
                return new ResolvedProjectAsset(icon, ApplicationInfoSource.Convention);
            }
        }

        return null;
    }

    private static string? FindIconIn(string directory, Func<string, bool>? isSupported)
    {
        foreach (var assetDirectory in AssetDirectories)
        {
            foreach (var fileName in IconFileNames)
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
