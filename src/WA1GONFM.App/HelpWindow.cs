using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace WA1GONFM.App;

/// <summary>Readable, scrollable user guide organized by topic.</summary>
public sealed class HelpWindow : Window
{
    public HelpWindow()
    {
        Title = "filepilot Help";
        Width = 760;
        Height = 620;
        MinWidth = 550;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var content = new StackPanel { Spacing = 18, Margin = new Thickness(24, 18) };
        content.Children.Add(new TextBlock
        {
            Text = "filepilot Help",
            FontSize = 26,
            FontWeight = FontWeight.Bold
        });
        content.Children.Add(new TextBlock
        {
            Text = "A quick guide to navigating and managing files on Linux.",
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap
        });

        AddSection(content, "Navigation",
            ("Open a folder", "Double-click a folder (or single-click when enabled in Settings)"),
            ("Navigate", "Use Back, Forward, Up, Home, or type a path in the location bar"),
            ("New tab", "Ctrl+T"));

        AddSection(content, "Views and selection",
            ("Details", "Ctrl+1"),
            ("Icons", "Ctrl+2"),
            ("Compact", "Ctrl+3"),
            ("Select individual files", "Ctrl+Click"),
            ("Select a range", "Shift+Click"),
            ("Select all", "Ctrl+A"));

        AddSection(content, "File operations",
            ("Copy", "Ctrl+C"),
            ("Cut", "Ctrl+X"),
            ("Paste", "Ctrl+V"),
            ("Rename", "F2"),
            ("Move to Trash", "Delete"),
            ("Delete permanently", "Shift+Delete"),
            ("Drag and drop", "Drag files between filepilot and other Linux file managers"));

        AddSection(content, "Sidebar",
            ("Shortcuts", "Expand or collapse to show saved locations"),
            ("Directories", "Expand or collapse to browse the folder tree"),
            ("Resize panels", "Drag the divider when both sections are expanded"));

        AddSection(content, "File type identification",
            ("Detected type", "filepilot examines file contents using libmagic"),
            ("Extensionless files", "Files such as LICENSE and .bash_history can still be identified"),
            ("ADIF logs", "Validated amateur radio ADIF files are identified by their contents"));

        AddSection(content, "File Type Integrity warnings",
            ("Red highlight", "Potentially dangerous mismatch, such as executable content disguised as a document"),
            ("Amber highlight", "Other unexpected mismatches between the extension and contents"),
            ("No highlight", "No identified mismatch; this does not guarantee that a file is safe"),
            ("Empty files", "Cannot be content-verified and are not flagged simply for being empty"));

        content.Children.Add(new TextBlock
        {
            Text = "Important: A mismatch is a reason to investigate, not proof of malware. Never open an unexpected executable merely because its extension looks harmless.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.85
        });

        var close = new Button
        {
            Content = "Close",
            MinWidth = 90,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        close.Click += (_, _) => Close();

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Thickness(12),
            RowSpacing = 10
        };
        layout.Children.Add(new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        Grid.SetRow(close, 1);
        layout.Children.Add(close);
        Content = layout;
    }

    private static void AddSection(StackPanel parent, string heading,
        params (string Action, string Description)[] entries)
    {
        var section = new StackPanel { Spacing = 8 };
        section.Children.Add(new TextBlock
        {
            Text = heading,
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 5, 0, 2)
        });
        foreach (var (action, description) in entries)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("190,*"),
                ColumnSpacing = 14
            };
            row.Children.Add(new TextBlock
            {
                Text = action,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            var detail = new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(detail, 1);
            row.Children.Add(detail);
            section.Children.Add(row);
        }
        parent.Children.Add(section);
    }
}
