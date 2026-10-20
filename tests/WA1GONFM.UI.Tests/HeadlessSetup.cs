using Avalonia;
using Avalonia.Headless;
[assembly: AvaloniaTestApplication(typeof(WA1GONFM.UI.Tests.HeadlessSetup))]
namespace WA1GONFM.UI.Tests;
public static class HeadlessSetup
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<global::WA1GONFM.App.App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
