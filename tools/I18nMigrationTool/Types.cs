namespace I18nMigrationTool;

record ResxEntry(string Key, string Value, string Comment, bool PreserveSpace);

class KeyClassification
{
    public required string Key { get; init; }
    public required string Dictionary { get; init; }
    public required string Assembly { get; init; }
    public required string Reason { get; init; }
    public bool IsDynamic { get; init; }
}
