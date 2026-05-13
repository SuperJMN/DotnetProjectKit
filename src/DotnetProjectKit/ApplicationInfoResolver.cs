using System.Globalization;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using Serilog;

namespace DotnetProjectKit;

public sealed class ApplicationInfoResolver
{
    private readonly IProjectMetadataReader metadataReader;
    private readonly ProjectAssetResolver assetResolver;

    public ApplicationInfoResolver() : this(new ProjectMetadataReader(), new ProjectAssetResolver())
    {
    }

    internal ApplicationInfoResolver(IProjectMetadataReader metadataReader, ProjectAssetResolver? assetResolver = null)
    {
        this.metadataReader = metadataReader;
        this.assetResolver = assetResolver ?? new ProjectAssetResolver();
    }

    public Result<ApplicationInfo> Resolve(
        string projectPath,
        ApplicationInfoOverrides? overrides = null,
        ApplicationInfoOverrides? settings = null,
        ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return Result.Failure<ApplicationInfo>("Project path is required.");
        }

        var projectFile = new FileInfo(projectPath);
        if (!projectFile.Exists)
        {
            return Result.Failure<ApplicationInfo>($"Project file not found: {projectFile.FullName}");
        }

        var log = logger ?? Log.Logger;
        return metadataReader.Read(projectFile, log)
            .Map(metadata => Resolve(projectFile, metadata, overrides ?? new ApplicationInfoOverrides(), settings ?? new ApplicationInfoOverrides(), log));
    }

    private ApplicationInfo Resolve(
        FileInfo projectFile,
        ProjectMetadata metadata,
        ApplicationInfoOverrides overrides,
        ApplicationInfoOverrides settings,
        ILogger logger)
    {
        var projectBaseName = Path.GetFileNameWithoutExtension(projectFile.Name);
        var assemblyName = ResolveText(
            null,
            null,
            metadata.Get("AssemblyName"),
            projectBaseName,
            ApplicationInfoSource.Msbuild,
            ApplicationInfoSource.Convention);

        var executableName = ResolveText(
            overrides.ExecutableName,
            settings.ExecutableName,
            metadata.Get("AssemblyName"),
            projectBaseName,
            ApplicationInfoSource.Msbuild,
            ApplicationInfoSource.Convention);

        var displayName = ResolveDisplayName(overrides, settings, metadata, assemblyName.Value, projectBaseName);
        var packageName = ResolvePackageName(overrides, settings, metadata, assemblyName.Value, projectBaseName);
        var version = ResolveText(
            overrides.Version,
            settings.Version,
            metadata.Get("Version"),
            "1.0.0",
            ApplicationInfoSource.Msbuild,
            ApplicationInfoSource.Default);

        var targetFramework = OptionalMsbuild(metadata, "TargetFramework");
        var targetFrameworks = OptionalMsbuild(metadata, "TargetFrameworks");

        return new ApplicationInfo
        {
            ProjectPath = projectFile.FullName,
            Metadata = metadata,
            AssemblyName = assemblyName,
            ExecutableName = executableName,
            DisplayName = displayName,
            PackageName = packageName,
            Version = version,
            StartupWmClass = ResolveStartupWmClass(overrides, settings, assemblyName.Value),
            Description = ResolveOptional(overrides.Description, settings.Description, metadata.Get("Description")),
            Authors = ResolveOptional(overrides.Authors, settings.Authors, metadata.Get("Authors")),
            Company = ResolveOptional(overrides.Company, settings.Company, metadata.Get("Company")),
            Copyright = OptionalMsbuild(metadata, "Copyright"),
            PackageId = ResolveOptional(overrides.PackageId, settings.PackageId, metadata.Get("PackageId")),
            PackageLicenseExpression = ResolveOptional(overrides.License, settings.License, metadata.Get("PackageLicenseExpression")),
            PackageProjectUrl = ResolveOptional(overrides.Url, settings.Url, metadata.Get("PackageProjectUrl")),
            RepositoryUrl = OptionalMsbuild(metadata, "RepositoryUrl"),
            AssemblyTitle = OptionalMsbuild(metadata, "AssemblyTitle"),
            OutputType = OptionalMsbuild(metadata, "OutputType"),
            TargetFramework = targetFramework,
            TargetFrameworks = targetFrameworks,
            AndroidTargetFramework = ResolveAndroidTargetFramework(targetFramework, targetFrameworks),
            Icon = assetResolver.ResolveIcon(projectFile, metadata, logger),
            Logo = assetResolver.ResolveLogo(projectFile, metadata, logger)
        };
    }

    private static ResolvedValue<string> ResolveDisplayName(
        ApplicationInfoOverrides overrides,
        ApplicationInfoOverrides settings,
        ProjectMetadata metadata,
        string assemblyName,
        string projectBaseName)
    {
        if (!string.IsNullOrWhiteSpace(overrides.DisplayName))
        {
            return new ResolvedValue<string>(overrides.DisplayName, ApplicationInfoSource.Override);
        }

        if (!string.IsNullOrWhiteSpace(settings.DisplayName))
        {
            return new ResolvedValue<string>(settings.DisplayName, ApplicationInfoSource.Config);
        }

        var product = metadata.Get("Product");
        if (!string.IsNullOrWhiteSpace(product) && !IsImplicitSdkDisplayName(product, assemblyName))
        {
            return new ResolvedValue<string>(product, ApplicationInfoSource.Msbuild);
        }

        var title = metadata.Get("AssemblyTitle");
        if (!string.IsNullOrWhiteSpace(title) && !IsImplicitSdkDisplayName(title, assemblyName))
        {
            return new ResolvedValue<string>(title, ApplicationInfoSource.Msbuild);
        }

        return new ResolvedValue<string>(Humanize(DesktopHostIdentity.StripSuffix(assemblyName ?? projectBaseName)), ApplicationInfoSource.Convention);
    }

    private static ResolvedValue<string> ResolvePackageName(
        ApplicationInfoOverrides overrides,
        ApplicationInfoOverrides settings,
        ProjectMetadata metadata,
        string assemblyName,
        string projectBaseName)
    {
        if (!string.IsNullOrWhiteSpace(overrides.PackageName))
        {
            return new ResolvedValue<string>(overrides.PackageName, ApplicationInfoSource.Override);
        }

        if (!string.IsNullOrWhiteSpace(settings.PackageName))
        {
            return new ResolvedValue<string>(settings.PackageName, ApplicationInfoSource.Config);
        }

        var packageId = metadata.Get("PackageId");
        if (!string.IsNullOrWhiteSpace(packageId) && !IsImplicitSdkDisplayName(packageId, assemblyName))
        {
            return new ResolvedValue<string>(packageId, ApplicationInfoSource.Msbuild);
        }

        return new ResolvedValue<string>(DesktopHostIdentity.StripSuffix(assemblyName ?? projectBaseName), ApplicationInfoSource.Convention);
    }

    private static ResolvedValue<string>? ResolveStartupWmClass(ApplicationInfoOverrides overrides, ApplicationInfoOverrides settings, string assemblyName)
    {
        if (!string.IsNullOrWhiteSpace(overrides.StartupWmClass))
        {
            return new ResolvedValue<string>(overrides.StartupWmClass, ApplicationInfoSource.Override);
        }

        if (!string.IsNullOrWhiteSpace(settings.StartupWmClass))
        {
            return new ResolvedValue<string>(settings.StartupWmClass, ApplicationInfoSource.Config);
        }

        var stripped = DesktopHostIdentity.TryStripSuffix(assemblyName);
        return stripped is null
            ? null
            : new ResolvedValue<string>(stripped, ApplicationInfoSource.Convention);
    }

    private static ResolvedValue<string> ResolveText(
        string? overrideValue,
        string? configValue,
        string? msbuildValue,
        string defaultValue,
        ApplicationInfoSource msbuildSource,
        ApplicationInfoSource defaultSource)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            return new ResolvedValue<string>(overrideValue, ApplicationInfoSource.Override);
        }

        if (!string.IsNullOrWhiteSpace(configValue))
        {
            return new ResolvedValue<string>(configValue, ApplicationInfoSource.Config);
        }

        return !string.IsNullOrWhiteSpace(msbuildValue)
            ? new ResolvedValue<string>(msbuildValue, msbuildSource)
            : new ResolvedValue<string>(defaultValue, defaultSource);
    }

    private static ResolvedValue<string>? ResolveOptional(string? overrideValue, string? configValue, string? msbuildValue)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            return new ResolvedValue<string>(overrideValue, ApplicationInfoSource.Override);
        }

        if (!string.IsNullOrWhiteSpace(configValue))
        {
            return new ResolvedValue<string>(configValue, ApplicationInfoSource.Config);
        }

        return !string.IsNullOrWhiteSpace(msbuildValue)
            ? new ResolvedValue<string>(msbuildValue, ApplicationInfoSource.Msbuild)
            : null;
    }

    private static ResolvedValue<string>? OptionalMsbuild(ProjectMetadata metadata, string propertyName)
    {
        var value = metadata.Get(propertyName);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : new ResolvedValue<string>(value, ApplicationInfoSource.Msbuild);
    }

    private static ResolvedValue<string>? ResolveAndroidTargetFramework(ResolvedValue<string>? targetFramework, ResolvedValue<string>? targetFrameworks)
    {
        var candidates = new[] { targetFramework, targetFrameworks }
            .Where(value => value is not null)
            .SelectMany(value => value!.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var androidTarget = candidates.FirstOrDefault(candidate => candidate.Contains("-android", StringComparison.OrdinalIgnoreCase));
        return androidTarget is null
            ? null
            : new ResolvedValue<string>(androidTarget, ApplicationInfoSource.Msbuild);
    }

    private static bool IsImplicitSdkDisplayName(string value, string assemblyName)
    {
        return DesktopHostIdentity.TryStripSuffix(assemblyName) is not null
               && string.Equals(value, assemblyName, StringComparison.Ordinal);
    }

    private static string Humanize(string value)
    {
        var stripped = RemoveExecutableExtension(value);
        var cleaned = Regex.Replace(stripped, "[._-]+", " ");
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "Application";
        }

        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        return string.Join(" ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => HumanizePart(part, textInfo)));
    }

    private static string HumanizePart(string part, TextInfo textInfo)
    {
        return part.Any(char.IsUpper)
            ? part
            : textInfo.ToTitleCase(part.ToLowerInvariant());
    }

    private static string RemoveExecutableExtension(string value)
    {
        var extension = Path.GetExtension(value);
        return IsExecutableExtension(extension) ? value[..^extension.Length] : value;
    }

    private static bool IsExecutableExtension(string extension)
    {
        return string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
               || string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase);
    }
}
