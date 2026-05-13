using FluentAssertions;
using Serilog.Core;

namespace DotnetProjectKit.Tests;

public sealed class ProjectAssetResolverTests : IDisposable
{
    private readonly string tempDir = Path.Combine(Path.GetTempPath(), $"DotnetProjectKit.Assets.{Guid.NewGuid():N}");

    public ProjectAssetResolverTests()
    {
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveIcon_PrefersPackageIconBeforeApplicationIcon()
    {
        var projectDir = Directory.CreateDirectory(Path.Combine(tempDir, "src", "Sample.Desktop")).FullName;
        var packageIcon = Path.Combine(projectDir, "package-icon.png");
        File.WriteAllText(Path.Combine(projectDir, "app.ico"), "ico");
        File.WriteAllText(packageIcon, "png");
        var projectPath = WriteProject(projectDir, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ApplicationIcon>app.ico</ApplicationIcon>
                <PackageIcon>package-icon.png</PackageIcon>
              </PropertyGroup>
            </Project>
            """);
        var metadata = ProjectMetadata.FromValues(new Dictionary<string, string>
        {
            ["ApplicationIcon"] = "app.ico",
            ["PackageIcon"] = "package-icon.png"
        });

        var result = new ProjectAssetResolver().ResolveIcon(new FileInfo(projectPath), metadata, Logger.None);

        result.Should().NotBeNull();
        result!.Path.Should().Be(packageIcon);
        result.Source.Should().Be(ApplicationInfoSource.Msbuild);
    }

    [Fact]
    public void ResolveIcon_SearchesProjectReferenceDirectoriesBeforeRepositoryRoot()
    {
        Directory.CreateDirectory(Path.Combine(tempDir, ".git"));
        File.WriteAllText(Path.Combine(tempDir, "icon.png"), "root");
        var appDir = Directory.CreateDirectory(Path.Combine(tempDir, "src", "Sample")).FullName;
        var assetDir = Directory.CreateDirectory(Path.Combine(appDir, "Assets")).FullName;
        var referencedIcon = Path.Combine(assetDir, "icon.png");
        File.WriteAllText(referencedIcon, "referenced");
        var appProject = WriteProject(appDir, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var desktopDir = Directory.CreateDirectory(Path.Combine(tempDir, "src", "Sample.Desktop")).FullName;
        var desktopProject = WriteProject(desktopDir, $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="{{Path.GetRelativePath(desktopDir, appProject)}}" />
              </ItemGroup>
            </Project>
            """);

        var result = new ProjectAssetResolver().ResolveIcon(new FileInfo(desktopProject), ProjectMetadata.Empty, Logger.None);

        result.Should().NotBeNull();
        result!.Path.Should().Be(referencedIcon);
        result.Source.Should().Be(ApplicationInfoSource.Convention);
    }

    [Fact]
    public void ResolveIcon_SearchesLowercaseAssetsAtRepositoryRoot()
    {
        Directory.CreateDirectory(Path.Combine(tempDir, ".git"));
        var assetDir = Directory.CreateDirectory(Path.Combine(tempDir, "assets")).FullName;
        var pngIcon = Path.Combine(assetDir, "logo.png");
        File.WriteAllText(pngIcon, "png");
        File.WriteAllText(Path.Combine(assetDir, "logo.ico"), "ico");
        var desktopDir = Directory.CreateDirectory(Path.Combine(tempDir, "src", "Sample.Desktop")).FullName;
        var desktopProject = WriteProject(desktopDir, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ApplicationIcon>..\..\assets\logo.ico</ApplicationIcon>
              </PropertyGroup>
            </Project>
            """);
        var metadata = ProjectMetadata.FromValues(new Dictionary<string, string>
        {
            ["ApplicationIcon"] = @"..\..\assets\logo.ico"
        });

        var result = new ProjectAssetResolver().ResolveIcon(
            new FileInfo(desktopProject),
            metadata,
            path => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase),
            Logger.None);

        result.Should().NotBeNull();
        result!.Path.Should().Be(pngIcon);
        result.Source.Should().Be(ApplicationInfoSource.Convention);
    }

    [Fact]
    public void ResolveLogo_UsesExplicitApplicationLogoBeforeConventions()
    {
        var projectDir = Directory.CreateDirectory(Path.Combine(tempDir, "src", "Sample.Desktop")).FullName;
        var logo = Path.Combine(projectDir, "installer-logo.png");
        File.WriteAllText(logo, "logo");
        File.WriteAllText(Path.Combine(projectDir, "icon.png"), "icon");
        var projectPath = WriteProject(projectDir, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var metadata = ProjectMetadata.FromValues(new Dictionary<string, string>
        {
            ["ApplicationLogo"] = "installer-logo.png",
            ["PackageIcon"] = "icon.png"
        });

        var result = new ProjectAssetResolver().ResolveLogo(new FileInfo(projectPath), metadata, Logger.None);

        result.Should().NotBeNull();
        result!.Path.Should().Be(logo);
        result.Source.Should().Be(ApplicationInfoSource.Msbuild);
    }

    [Fact]
    public void ResolveLogo_SearchesLogoConventionsBeforeIconFallback()
    {
        Directory.CreateDirectory(Path.Combine(tempDir, ".git"));
        var assetDir = Directory.CreateDirectory(Path.Combine(tempDir, "assets")).FullName;
        var logo = Path.Combine(assetDir, "logo.png");
        File.WriteAllText(logo, "logo");
        File.WriteAllText(Path.Combine(assetDir, "icon.png"), "icon");
        var projectDir = Directory.CreateDirectory(Path.Combine(tempDir, "src", "Sample.Desktop")).FullName;
        var projectPath = WriteProject(projectDir, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var result = new ProjectAssetResolver().ResolveLogo(new FileInfo(projectPath), ProjectMetadata.Empty, Logger.None);

        result.Should().NotBeNull();
        result!.Path.Should().Be(logo);
        result.Source.Should().Be(ApplicationInfoSource.Convention);
    }

    [Fact]
    public void ResolveLogo_FallsBackToIconWhenNoLogoExists()
    {
        var projectDir = Directory.CreateDirectory(Path.Combine(tempDir, "src", "Sample.Desktop")).FullName;
        var icon = Path.Combine(projectDir, "icon.png");
        File.WriteAllText(icon, "icon");
        var projectPath = WriteProject(projectDir, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var result = new ProjectAssetResolver().ResolveLogo(new FileInfo(projectPath), ProjectMetadata.Empty, Logger.None);

        result.Should().NotBeNull();
        result!.Path.Should().Be(icon);
        result.Source.Should().Be(ApplicationInfoSource.Convention);
    }

    private static string WriteProject(string directory, string content)
    {
        var path = Path.Combine(directory, Path.GetFileName(directory) + ".csproj");
        File.WriteAllText(path, content);
        return path;
    }
}
