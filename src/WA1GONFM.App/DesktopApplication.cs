namespace WA1GONFM.App;

/// <summary>A Linux desktop application discovered through XDG application registrations.</summary>
public sealed class DesktopApplication(string desktopId, string name, bool recommended, bool isDefault)
{
    public string DesktopId { get; } = desktopId;
    public string Name { get; } = name;
    public bool Recommended { get; } = recommended;
    public bool IsDefault { get; } = isDefault;
    public string DisplayName => $"{Name}{(IsDefault ? " (default)" : "")}";
}
