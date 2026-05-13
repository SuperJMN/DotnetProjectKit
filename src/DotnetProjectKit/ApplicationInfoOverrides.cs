namespace DotnetProjectKit;

public sealed record ApplicationInfoOverrides
{
    public string? DisplayName { get; init; }
    public string? PackageName { get; init; }
    public string? StartupWmClass { get; init; }
    public string? ExecutableName { get; init; }
    public string? Version { get; init; }
    public string? Description { get; init; }
    public string? Authors { get; init; }
    public string? Company { get; init; }
    public string? PackageId { get; init; }
    public string? Url { get; init; }
    public string? License { get; init; }
}
