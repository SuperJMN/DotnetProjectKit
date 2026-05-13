using CSharpFunctionalExtensions;
using Serilog;

namespace DotnetProjectKit;

public interface IProjectMetadataReader
{
    Result<ProjectMetadata> Read(FileInfo projectFile, ILogger logger);
}
