namespace WA1GONFM.App;
public sealed class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings, SettingsStore store)
    {
        Title = "WA1GONFM Settings";
        Width = 510; Height = 300;
        var theme = new ComboBox { ItemsSource = new[] { "System", "Light", "Dark" }, SelectedItem = settings.Theme };
        theme.SelectionChanged += (_, _) =>
        {
            settings.Theme = theme.SelectedItem as string ?? "System";
            ThemeManager.Apply(settings.Theme);
            store.Save(settings);
        };
        var executable = new TextBox { Text = settings.TerminalExecutable, Watermark = "e.g. xfce4-terminal" };
        var arguments = new TextBox { Text = settings.TerminalArguments, Watermark = "Optional command-line arguments" };
        var save = new Button { Content = "Save", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(executable.Text)) return;
            settings.TerminalExecutable = executable.Text.Trim();
            settings.TerminalArguments = arguments.Text ?? "";
            store.Save(settings);
            Close();
        };
        Content = new StackPanel { Margin = new Thickness(20), Spacing = 8, Children =
        {
            new TextBlock { Text = "Theme (System follows your desktop preference)" }, theme,
            new TextBlock { Text = "Preferred terminal executable" }, executable,
            new TextBlock { Text = "Arguments (simple space-separated options)" }, arguments, save
        }};
    }
}
