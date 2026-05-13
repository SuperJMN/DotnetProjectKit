namespace DotnetProjectKit;

public sealed record ProjectMetadata
{
    private readonly IReadOnlyDictionary<string, string> values;

    public ProjectMetadata(IReadOnlyDictionary<string, string> values)
    {
        this.values = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    public static ProjectMetadata Empty { get; } = new(new Dictionary<string, string>());

    public IReadOnlyDictionary<string, string> Values => values;

    public static ProjectMetadata FromValues(IReadOnlyDictionary<string, string> values)
    {
        var normalized = values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        return new ProjectMetadata(normalized);
    }

    public string? Get(string name)
    {
        return values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    public bool GetBool(string name)
    {
        return bool.TryParse(Get(name), out var value) && value;
    }
}
