using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using Serilog;

namespace DotnetProjectKit;

public sealed class ProjectMetadataReader : IProjectMetadataReader
{
    public static readonly IReadOnlyCollection<string> PropertiesToRead =
    [
        "Product",
        "ApplicationTitle",
        "Title",
        "Company",
        "Description",
        "PackageDescription",
        "Authors",
        "Copyright",
        "PackageLicenseExpression",
        "PackageProjectUrl",
        "PackageId",
        "Version",
        "RepositoryUrl",
        "AssemblyName",
        "AssemblyTitle",
        "OutputType",
        "TargetFramework",
        "TargetFrameworks",
        "ApplicationIcon",
        "PackageIcon",
        "ApplicationLogo",
        "PackageLogo",
        "IsPackable"
    ];

    private readonly IMsbuildPropertyReader msbuildReader;

    public ProjectMetadataReader() : this(new DotnetMsbuildPropertyReader())
    {
    }

    internal ProjectMetadataReader(IMsbuildPropertyReader msbuildReader)
    {
        this.msbuildReader = msbuildReader;
    }

    public Result<ProjectMetadata> Read(FileInfo projectFile)
    {
        return Read(projectFile, Serilog.Core.Logger.None);
    }

    public Result<ProjectMetadata> Read(FileInfo projectFile, ILogger logger)
    {
        if (!projectFile.Exists)
        {
            return Result.Failure<ProjectMetadata>($"Project file not found: {projectFile.FullName}");
        }

        var msbuild = msbuildReader.Read(projectFile, PropertiesToRead, logger);
        if (msbuild.IsSuccess)
        {
            return ProjectMetadata.FromValues(msbuild.Value);
        }

        logger.Debug("MSBuild metadata read failed for {ProjectFile}: {Error}. Falling back to XML.", projectFile.FullName, msbuild.Error);
        return ReadFromXml(projectFile)
            .MapError(error => $"Failed to read project metadata from {projectFile.FullName}: {error}");
    }

    public Maybe<ProjectMetadata> TryRead(FileInfo projectFile, ILogger logger)
    {
        var result = Read(projectFile, logger);
        if (result.IsFailure)
        {
            logger.Warning("Unable to read project metadata from {ProjectFile}: {Error}", projectFile.FullName, result.Error);
            return Maybe<ProjectMetadata>.None;
        }

        return Maybe<ProjectMetadata>.From(result.Value);
    }

    private static Result<ProjectMetadata> ReadFromXml(FileInfo projectFile)
    {
        return Result.Try(
            () =>
            {
                var document = XDocument.Load(projectFile.FullName);
                var values = PropertiesToRead
                    .Select(property => new KeyValuePair<string, string?>(property, ReadXmlProperty(document, property)))
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                    .ToDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.OrdinalIgnoreCase);

                return ProjectMetadata.FromValues(values);
            },
            ex => ex.Message);
    }

    private static string? ReadXmlProperty(XDocument document, string propertyName)
    {
        return document
            .Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, propertyName, StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim();
    }

    private sealed class DotnetMsbuildPropertyReader : IMsbuildPropertyReader
    {
        public Result<IReadOnlyDictionary<string, string>> Read(FileInfo projectFile, IReadOnlyCollection<string> properties, ILogger logger)
        {
            return Result.Try(
                () =>
                {
                    using var process = StartProcess(projectFile, properties);
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        var message = string.IsNullOrWhiteSpace(error) ? output : error;
                        throw new InvalidOperationException(message.Trim());
                    }

                    return ParseOutput(output, properties);
                },
                ex => ex.Message);
        }

        private static Process StartProcess(FileInfo projectFile, IReadOnlyCollection<string> properties)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = "dotnet",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.StartInfo.ArgumentList.Add("msbuild");
            process.StartInfo.ArgumentList.Add(projectFile.FullName);
            process.StartInfo.ArgumentList.Add("-nologo");
            process.StartInfo.ArgumentList.Add("-v:q");
            process.StartInfo.ArgumentList.Add($"-getProperty:{string.Join(";", properties)}");

            if (!process.Start())
            {
                throw new InvalidOperationException("Unable to start dotnet msbuild.");
            }

            return process;
        }

        private static IReadOnlyDictionary<string, string> ParseOutput(string output, IReadOnlyCollection<string> properties)
        {
            var trimmed = output.Trim();
            if (trimmed.StartsWith('{'))
            {
                using var json = JsonDocument.Parse(trimmed);
                var propertyElement = json.RootElement.GetProperty("Properties");
                return properties
                    .Where(property => propertyElement.TryGetProperty(property, out _))
                    .ToDictionary(
                        property => property,
                        property => propertyElement.GetProperty(property).GetString() ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase);
            }

            return ParseTextOutput(output, properties);
        }

        private static IReadOnlyDictionary<string, string> ParseTextOutput(string output, IReadOnlyCollection<string> properties)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var propertyNames = new HashSet<string>(properties, StringComparer.OrdinalIgnoreCase);
            string? pending = null;

            foreach (var rawLine in output.Split(["\r\n", "\n"], StringSplitOptions.None))
            {
                if (pending is not null)
                {
                    if (!string.IsNullOrWhiteSpace(rawLine) && char.IsWhiteSpace(rawLine[0]))
                    {
                        values[pending] = rawLine.Trim();
                    }

                    pending = null;
                    continue;
                }

                var trimmed = rawLine.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex > 0)
                {
                    var name = trimmed[..colonIndex].Trim();
                    if (!propertyNames.Contains(name))
                    {
                        continue;
                    }

                    var value = trimmed[(colonIndex + 1)..].Trim();
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        pending = name;
                    }
                    else
                    {
                        values[name] = value;
                    }

                    continue;
                }

                if (propertyNames.Contains(trimmed.TrimEnd(':')))
                {
                    pending = trimmed.TrimEnd(':');
                }
            }

            return values;
        }
    }
}
