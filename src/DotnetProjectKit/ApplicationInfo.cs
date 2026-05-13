namespace DotnetProjectKit;

public sealed record ApplicationInfo
{
    public required string ProjectPath { get; init; }
    public required ProjectMetadata Metadata { get; init; }
    public required ResolvedValue<string> AssemblyName { get; init; }
    public required ResolvedValue<string> ExecutableName { get; init; }
    public required ResolvedValue<string> DisplayName { get; init; }
    public required ResolvedValue<string> PackageName { get; init; }
    public required ResolvedValue<string> Version { get; init; }
    public ResolvedValue<string>? StartupWmClass { get; init; }
    public ResolvedValue<string>? Description { get; init; }
    public ResolvedValue<string>? Authors { get; init; }
    public ResolvedValue<string>? Company { get; init; }
    public ResolvedValue<string>? Copyright { get; init; }
    public ResolvedValue<string>? PackageId { get; init; }
    public ResolvedValue<string>? PackageLicenseExpression { get; init; }
    public ResolvedValue<string>? PackageProjectUrl { get; init; }
    public ResolvedValue<string>? RepositoryUrl { get; init; }
    public ResolvedValue<string>? AssemblyTitle { get; init; }
    public ResolvedValue<string>? OutputType { get; init; }
    public ResolvedValue<string>? TargetFramework { get; init; }
    public ResolvedValue<string>? TargetFrameworks { get; init; }
    public ResolvedValue<string>? AndroidTargetFramework { get; init; }
    public ResolvedProjectAsset? Icon { get; init; }
}
