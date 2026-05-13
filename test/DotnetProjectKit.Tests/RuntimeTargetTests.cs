using FluentAssertions;

namespace DotnetProjectKit.Tests;

public sealed class RuntimeTargetTests
{
    [Theory]
    [InlineData("linux", RuntimeArchitecture.X64, "linux-x64")]
    [InlineData("windows", RuntimeArchitecture.X64, "win-x64")]
    [InlineData("macos", RuntimeArchitecture.Arm64, "osx-arm64")]
    public void FromPlatform_MapsCommonRuntimeIdentifiers(string platform, RuntimeArchitecture architecture, string expectedRid)
    {
        RuntimeTarget.FromPlatform(platform, architecture).Should().Be(new RuntimeTarget(expectedRid));
    }

    [Fact]
    public void PlatformFactories_MapCanonicalRuntimeIdentifiers()
    {
        RuntimeTarget.Linux(RuntimeArchitecture.X64).Should().Be(new RuntimeTarget("linux-x64"));
        RuntimeTarget.Windows(RuntimeArchitecture.Arm64).Should().Be(new RuntimeTarget("win-arm64"));
        RuntimeTarget.MacOS(RuntimeArchitecture.X86).Should().Be(new RuntimeTarget("osx-x86"));
    }

    [Theory]
    [InlineData("amd64", RuntimeArchitecture.X64)]
    [InlineData("x86_64", RuntimeArchitecture.X64)]
    [InlineData("aarch64", RuntimeArchitecture.Arm64)]
    [InlineData("arm64", RuntimeArchitecture.Arm64)]
    public void ParseArchitecture_AcceptsEquivalentNames(string raw, RuntimeArchitecture expected)
    {
        RuntimeTarget.ParseArchitecture(raw).Should().Be(expected);
    }
}
