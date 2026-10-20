using Avalonia.Styling;
namespace WA1GONFM.App;
public static class ThemeManager
{
    public static void Apply(string? setting)
    {
        if (Application.Current is null) return;
        Application.Current.RequestedThemeVariant = setting switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
