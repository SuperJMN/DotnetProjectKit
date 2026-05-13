using CSharpFunctionalExtensions;
using FluentAssertions;
using Serilog.Core;

namespace DotnetProjectKit.Tests;

public sealed class ProjectMetadataReaderTests : IDisposable
{
    private readonly string tempDir = Path.Combine(Path.GetTempPath(), $"DotnetProjectKit.Metadata.{Guid.NewGuid():N}");

    public ProjectMetadataReaderTests()
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
    public void Read_UsesSingleMsbuildEvaluationForAllProperties()
    {
        var projectPath = WriteProject("Sample.Desktop.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        var msbuild = new RecordingMsbuildReader(new Dictionary<string, string>
        {
            ["AssemblyName"] = "Sample.Desktop",
            ["Version"] = "2.3.4",
            ["Product"] = "Sample.Desktop"
        });
        var reader = new ProjectMetadataReader(msbuild);

        var result = reader.Read(new FileInfo(projectPath), Logger.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : "");
        msbuild.Calls.Should().Be(1);
        msbuild.RequestedProperties.Should().Contain(["AssemblyName", "Version", "Product", "PackageIcon", "ApplicationIcon", "ApplicationLogo", "PackageLogo"]);
        result.Value.Get("AssemblyName").Should().Be("Sample.Desktop");
        result.Value.Get("Version").Should().Be("2.3.4");
    }

    [Fact]
    public void Read_FallsBackToXml_WhenMsbuildEvaluationFails()
    {
        var projectPath = WriteProject("BrokenSdk.csproj", """
            <Project Sdk="Missing.Sdk/1.0.0">
              <PropertyGroup>
                <AssemblyName>XmlAssembly</AssemblyName>
                <Version>9.8.7</Version>
              </PropertyGroup>
            </Project>
            """);
        var reader = new ProjectMetadataReader(new FailingMsbuildReader("SDK not found"));

        var result = reader.Read(new FileInfo(projectPath), Logger.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : "");
        result.Value.Get("AssemblyName").Should().Be("XmlAssembly");
        result.Value.Get("Version").Should().Be("9.8.7");
    }

    private string WriteProject(string name, string content)
    {
        var path = Path.Combine(tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private sealed class RecordingMsbuildReader(IReadOnlyDictionary<string, string> values) : IMsbuildPropertyReader
    {
        public int Calls { get; private set; }
        public IReadOnlyCollection<string> RequestedProperties { get; private set; } = [];

        public Result<IReadOnlyDictionary<string, string>> Read(FileInfo projectFile, IReadOnlyCollection<string> properties, ILogger logger)
        {
            Calls++;
            RequestedProperties = properties.ToArray();
            return Result.Success(values);
        }
    }

    private sealed class FailingMsbuildReader(string error) : IMsbuildPropertyReader
    {
        public Result<IReadOnlyDictionary<string, string>> Read(FileInfo projectFile, IReadOnlyCollection<string> properties, ILogger logger)
        {
            return Result.Failure<IReadOnlyDictionary<string, string>>(error);
        }
    }
}
