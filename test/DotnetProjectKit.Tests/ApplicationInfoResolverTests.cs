using CSharpFunctionalExtensions;
using FluentAssertions;
using Serilog.Core;

namespace DotnetProjectKit.Tests;

public sealed class ApplicationInfoResolverTests : IDisposable
{
    private readonly string tempDir = Path.Combine(Path.GetTempPath(), $"DotnetProjectKit.Info.{Guid.NewGuid():N}");

    public ApplicationInfoResolverTests()
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

    [Theory]
    [InlineData("Sample.Desktop")]
    [InlineData("Sample-desktop")]
    [InlineData("Sample_desktop")]
    [InlineData("Sample desktop")]
    public void Resolve_NormalizesDesktopHostIdentity(string assemblyName)
    {
        var projectPath = WriteProject("Sample.Desktop.csproj");
        var metadata = ProjectMetadata.FromValues(new Dictionary<string, string>
        {
            ["AssemblyName"] = assemblyName,
            ["Product"] = assemblyName,
            ["PackageId"] = assemblyName
        });
        var resolver = new ApplicationInfoResolver(new StubMetadataReader(metadata));

        var result = resolver.Resolve(projectPath, logger: Logger.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : "");
        result.Value.DisplayName.Should().Be(new ResolvedValue<string>("Sample", ApplicationInfoSource.Convention));
        result.Value.PackageName.Should().Be(new ResolvedValue<string>("Sample", ApplicationInfoSource.Convention));
        result.Value.StartupWmClass.Should().Be(new ResolvedValue<string>("Sample", ApplicationInfoSource.Convention));
    }

    [Fact]
    public void Resolve_PreservesAssemblyNameAsExecutableNameUnlessExplicitlyOverridden()
    {
        var projectPath = WriteProject("Sample.Desktop.csproj");
        var metadata = ProjectMetadata.FromValues(new Dictionary<string, string>
        {
            ["AssemblyName"] = "Sample.Desktop",
            ["Product"] = "Sample.Desktop"
        });
        var resolver = new ApplicationInfoResolver(new StubMetadataReader(metadata));

        var result = resolver.Resolve(projectPath, logger: Logger.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : "");
        result.Value.ExecutableName.Should().Be(new ResolvedValue<string>("Sample.Desktop", ApplicationInfoSource.Msbuild));
    }

    [Fact]
    public void Resolve_UsesOverrideThenConfigThenMsbuildThenConvention()
    {
        var projectPath = WriteProject("Sample.Desktop.csproj");
        var metadata = ProjectMetadata.FromValues(new Dictionary<string, string>
        {
            ["AssemblyName"] = "Sample.Desktop",
            ["Product"] = "MSBuild Product",
            ["Version"] = "1.0.0"
        });
        var resolver = new ApplicationInfoResolver(new StubMetadataReader(metadata));

        var result = resolver.Resolve(
            projectPath,
            overrides: new ApplicationInfoOverrides { DisplayName = "Override Name" },
            settings: new ApplicationInfoOverrides { Version = "2.0.0", PackageName = "config-package" },
            logger: Logger.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : "");
        result.Value.DisplayName.Should().Be(new ResolvedValue<string>("Override Name", ApplicationInfoSource.Override));
        result.Value.PackageName.Should().Be(new ResolvedValue<string>("config-package", ApplicationInfoSource.Config));
        result.Value.Version.Should().Be(new ResolvedValue<string>("2.0.0", ApplicationInfoSource.Config));
        result.Value.ExecutableName.Should().Be(new ResolvedValue<string>("Sample.Desktop", ApplicationInfoSource.Msbuild));
    }

    [Fact]
    public void Resolve_ExposesIconAndLogoSeparately()
    {
        var projectPath = WriteProject("Sample.Desktop.csproj");
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var icon = Path.Combine(projectDir, "icon.png");
        var logo = Path.Combine(projectDir, "logo.png");
        File.WriteAllText(icon, "icon");
        File.WriteAllText(logo, "logo");
        var metadata = ProjectMetadata.FromValues(new Dictionary<string, string>
        {
            ["PackageIcon"] = "icon.png",
            ["ApplicationLogo"] = "logo.png"
        });
        var resolver = new ApplicationInfoResolver(new StubMetadataReader(metadata));

        var result = resolver.Resolve(projectPath, logger: Logger.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : "");
        result.Value.Icon.Should().NotBeNull();
        result.Value.Icon!.Path.Should().Be(icon);
        result.Value.Logo.Should().NotBeNull();
        result.Value.Logo!.Path.Should().Be(logo);
    }

    private string WriteProject(string name)
    {
        var path = Path.Combine(tempDir, name);
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        return path;
    }

    private sealed class StubMetadataReader(ProjectMetadata metadata) : IProjectMetadataReader
    {
        public Result<ProjectMetadata> Read(FileInfo projectFile, ILogger logger)
        {
            return Result.Success(metadata);
        }
    }
}
