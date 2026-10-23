using System.Text.Json;

namespace WA1GONFM.App;

/// <summary>Stores applications manually added to filepilot's Open With list for each MIME type.</summary>
public sealed class OpenWithAssociationStore
{
    private readonly string _path;

    private sealed class Config
    {
        public Dictionary<string, List<string>> Associations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ApplicationNames { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<string>> HiddenApplications { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> PreferredApplications { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public OpenWithAssociationStore()
    {
        var root = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        _path = Path.Combine(root, "filepilot", "open-with-apps.json");
    }

    public string? GetPreferred(string mime) => Load().PreferredApplications.GetValueOrDefault(mime);

    public void SetPreferred(string mime, string? applicationId)
    {
        var config = Load();
        if (string.IsNullOrWhiteSpace(applicationId)) config.PreferredApplications.Remove(mime);
        else config.PreferredApplications[mime] = applicationId;
        Save(config);
    }

    public IReadOnlySet<string> Get(string mime)
    {
        var map = Load().Associations;
        return map.TryGetValue(mime, out var ids)
            ? ids.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public void Add(string mime, string desktopId)
    {
        var map = Load().Associations;
        if (!map.TryGetValue(mime, out var ids)) map[mime] = ids = new List<string>();
        if (!ids.Contains(desktopId, StringComparer.OrdinalIgnoreCase)) ids.Add(desktopId);
        var config = Load();
        config.Associations = map;
        Save(config);
    }

    public void Remove(string mime, string desktopId)
    {
        var map = Load().Associations;
        if (map.TryGetValue(mime, out var ids))
        {
            ids.RemoveAll(id => string.Equals(id, desktopId, StringComparison.OrdinalIgnoreCase));
            if (ids.Count == 0) map.Remove(mime);
            var config = Load();
        config.Associations = map;
        Save(config);
        }
    }

    public IReadOnlySet<string> GetHidden(string mime)
    {
        var hidden = Load().HiddenApplications;
        return hidden.TryGetValue(mime, out var ids)
            ? ids.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public void Hide(string mime, string desktopId)
    {
        var config = Load();
        if (!config.HiddenApplications.TryGetValue(mime, out var ids))
            config.HiddenApplications[mime] = ids = new List<string>();
        if (!ids.Contains(desktopId, StringComparer.OrdinalIgnoreCase)) ids.Add(desktopId);
        Save(config);
    }

    public void Unhide(string mime, string desktopId)
    {
        var config = Load();
        if (config.HiddenApplications.TryGetValue(mime, out var ids))
        {
            ids.RemoveAll(id => string.Equals(id, desktopId, StringComparison.OrdinalIgnoreCase));
            if (ids.Count == 0) config.HiddenApplications.Remove(mime);
            Save(config);
        }
    }

    // Keys are desktop IDs for registered applications, absolute paths for custom executables.
    public string? GetDisplayName(string applicationId) =>
        Load().ApplicationNames.TryGetValue(applicationId, out var name) ? name : null;

    public void SetDisplayName(string applicationId, string? displayName)
    {
        var config = Load();
        if (string.IsNullOrWhiteSpace(displayName)) config.ApplicationNames.Remove(applicationId);
        else config.ApplicationNames[applicationId] = displayName.Trim();
        Save(config);
    }

    public string? GetApplicationName(string executable)
    {
        var names = Load().ApplicationNames;
        return names.TryGetValue(Path.GetFullPath(executable), out var name) ? name : null;
    }

    public void SetApplicationName(string executable, string? displayName)
    {
        var config = Load();
        var key = Path.GetFullPath(executable);
        if (string.IsNullOrWhiteSpace(displayName)) config.ApplicationNames.Remove(key);
        else config.ApplicationNames[key] = displayName.Trim();
        Save(config);
    }

    private Config Load()
    {
        if (!File.Exists(_path)) return new Config();
        try
        {
            var json = File.ReadAllText(_path);
            using var document = JsonDocument.Parse(json);
            // Older filepilot versions saved MIME associations directly as the root object.
            // Read that format and migrate when the next change is saved.
            if (!document.RootElement.TryGetProperty("associations", out _) &&
                !document.RootElement.TryGetProperty("Associations", out _))
            {
                return new Config
                {
                    Associations = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)
                        ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                };
            }
            var loaded = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new Config();
            return new Config
            {
                Associations = new Dictionary<string, List<string>>(loaded.Associations ?? new(), StringComparer.OrdinalIgnoreCase),
                ApplicationNames = new Dictionary<string, string>(loaded.ApplicationNames ?? new(), StringComparer.Ordinal),
                HiddenApplications = new Dictionary<string, List<string>>(loaded.HiddenApplications ?? new(), StringComparer.OrdinalIgnoreCase),
                PreferredApplications = new Dictionary<string, string>(loaded.PreferredApplications ?? new(), StringComparer.OrdinalIgnoreCase)
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Config();
        }
    }

    private void Save(Config config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
            File.Move(temp, _path, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
