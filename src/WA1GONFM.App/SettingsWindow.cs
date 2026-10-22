namespace WA1GONFM.App;
public sealed class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings, SettingsStore store)
    {
        Title = "filepilot Settings";
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
        var clickMode = new ComboBox { ItemsSource = new[] { "Single", "Double" }, SelectedItem = settings.ClickMode };
        clickMode.SelectionChanged += (_, _) =>
        {
            settings.ClickMode = clickMode.SelectedItem as string ?? "Single";
            store.Save(settings);
        };
        var executable = new TextBox { Text = settings.TerminalExecutable, PlaceholderText = "e.g. xfce4-terminal" };
        var arguments = new TextBox { Text = settings.TerminalArguments, PlaceholderText = "Optional command-line arguments" };
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
        confirmMove.IsCheckedChanged += (_, _) => { settings.ConfirmMove = confirmMove.IsChecked == true; store.Save(settings); };
        confirmOverwrite.IsCheckedChanged += (_, _) => { settings.ConfirmOverwrite = confirmOverwrite.IsChecked == true; store.Save(settings); };
        confirmTrash.IsCheckedChanged += (_, _) => { settings.ConfirmTrash = confirmTrash.IsChecked == true; store.Save(settings); };
        confirmPermanent.IsCheckedChanged += (_, _) => { settings.ConfirmPermanentDelete = confirmPermanent.IsChecked == true; store.Save(settings); };
        Content = new ScrollViewer { Content = new StackPanel { Margin = new Thickness(20), Spacing = 8, Children =
        {
            new TextBlock { Text = "Theme (System follows desktop preference)" }, theme,
            new TextBlock { Text = "Default file view (new tabs)" }, defaultView,
            new TextBlock { Text = "Open files and folders with" }, clickMode,
            new TextBlock { Text = "Confirmations", FontWeight = Avalonia.Media.FontWeight.Bold },
            confirmMove, confirmOverwrite, confirmTrash, confirmPermanent,
            new TextBlock { Text = "Preferred terminal executable" }, executable,
            new TextBlock { Text = "Arguments (simple space-separated options)" }, arguments, save
        }}};
    }
}
