using CSharpFunctionalExtensions;
using Serilog;

namespace DotnetProjectKit;

internal interface IMsbuildPropertyReader
{
    Result<IReadOnlyDictionary<string, string>> Read(FileInfo projectFile, IReadOnlyCollection<string> properties, ILogger logger);
}
