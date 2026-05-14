namespace CbtExam.Desktop.Models;

public record SearchItemRecord
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? Action { get; init; } = null;
    public string? Url { get; init; } = null;
}

public record QuickActionRecord : SearchItemRecord
{
    public Action? Run { get; init; } = null;
}

public record PageRecord : SearchItemRecord
{
    public string Key { get; init; } = string.Empty;
}