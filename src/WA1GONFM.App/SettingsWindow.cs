namespace WA1GONFM.App;
public sealed class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings, SettingsStore store)
    {
        Title = "WA1GONFM Settings";
        Width = 540; Height = 465;
        var theme = new ComboBox { ItemsSource = new[] { "System", "Light", "Dark" }, SelectedItem = settings.Theme };
        theme.SelectionChanged += (_, _) =>
        {
            settings.Theme = theme.SelectedItem as string ?? "System";
            ThemeManager.Apply(settings.Theme);
            store.Save(settings);
        };
        var defaultView = new ComboBox { ItemsSource = new[] { "Details", "Icons", "Compact" }, SelectedItem = settings.DefaultViewMode };
        defaultView.SelectionChanged += (_, _) =>
        {
            settings.DefaultViewMode = defaultView.SelectedItem as string ?? "Details";
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
        var confirmMove = new CheckBox { Content = "Confirm moving files", IsChecked = settings.ConfirmMove };
        var confirmOverwrite = new CheckBox { Content = "Confirm overwriting existing files", IsChecked = settings.ConfirmOverwrite };
        var confirmTrash = new CheckBox { Content = "Confirm moving to Trash", IsChecked = settings.ConfirmTrash };
        var confirmPermanent = new CheckBox { Content = "Confirm permanent deletion", IsChecked = settings.ConfirmPermanentDelete };
        confirmMove.Checked += (_, _) => { settings.ConfirmMove = true; store.Save(settings); };
        confirmMove.Unchecked += (_, _) => { settings.ConfirmMove = false; store.Save(settings); };
        confirmOverwrite.Checked += (_, _) => { settings.ConfirmOverwrite = true; store.Save(settings); };
        confirmOverwrite.Unchecked += (_, _) => { settings.ConfirmOverwrite = false; store.Save(settings); };
        confirmTrash.Checked += (_, _) => { settings.ConfirmTrash = true; store.Save(settings); };
        confirmTrash.Unchecked += (_, _) => { settings.ConfirmTrash = false; store.Save(settings); };
        confirmPermanent.Checked += (_, _) => { settings.ConfirmPermanentDelete = true; store.Save(settings); };
        confirmPermanent.Unchecked += (_, _) => { settings.ConfirmPermanentDelete = false; store.Save(settings); };
        Content = new ScrollViewer { Content = new StackPanel { Margin = new Thickness(20), Spacing = 8, Children =
        {
            new TextBlock { Text = "Theme (System follows desktop preference)" }, theme,
            new TextBlock { Text = "Default file view (new tabs)" }, defaultView,
            new TextBlock { Text = "Confirmations", FontWeight = Avalonia.Media.FontWeight.Bold },
            confirmMove, confirmOverwrite, confirmTrash, confirmPermanent,
            new TextBlock { Text = "Preferred terminal executable" }, executable,
            new TextBlock { Text = "Arguments (simple space-separated options)" }, arguments, save
        }}};
    }
}
