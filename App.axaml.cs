using System;
using System.IO;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using csp.ViewModels;
using csp.Views;

namespace csp;

public partial class App : Application
{
    public static string CurrentTheme = Config.GetEntry("theme") ?? "avares://csp/Styles/light.axaml";
    
    public static bool SavedIsDark =
        Config.GetEntry("isDarkTheme") == null || bool.Parse(Config.GetEntry("isDarkTheme") ?? "true");
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        LoadTheme(CurrentTheme, SavedIsDark);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private static void ThemeFailed()
    {
        // This function is for if it failed to load a theme, will revert to previous, or will decide to use darkmode if all else fails.
        try
        {
            // Tries loading back to our original loaded theme.
            var Resource = (IStyle)AvaloniaXamlLoader.Load(
                new Uri(CurrentTheme)
            );
            Current.RequestedThemeVariant = SavedIsDark ? ThemeVariant.Dark : ThemeVariant.Light;
            if (Current.Styles.Count > 3)
                Current.Styles.Remove(Current.Styles[3]);
            Current.Styles.Add(Resource);
        }
        catch
        {
            // Tries loading our default theme. Purpose of this is if a theme somehow vanished.
            var Resource = (IStyle)AvaloniaXamlLoader.Load(
                new Uri("avares://csp/Styles/dark.axaml")
            );
            Current.RequestedThemeVariant = ThemeVariant.Dark;
            if (Current.Styles.Count > 4)
                Current.Styles.Remove(Current.Styles[4]);
            Current.Styles.Add(Resource);
            CurrentTheme = "avares://csp/Styles/dark.axaml";
            SavedIsDark = true;
        }
    }

    public static string? LoadThemeFromString(string themeText, bool isDark = true, string themeUri = "")
    {
        var OutputMessage = "";
        try
        {
            // Tries loading as runtime uncompiled.
            var TextInput = themeText;
            TextInput = Regex.Replace(TextInput, @"file:./", AppDomain.CurrentDomain.BaseDirectory);
            if (themeUri != "")
            {
                TextInput = Regex.Replace(TextInput, @"style:./",
                    Regex.Replace(themeUri, @"\\(?:.(?!\\))+$", "") + "\\");
            }
            else
            {
                OutputMessage += "- You have not saved this theme, so it won't parse style:./.\n\n";
            }
            Match isCodeDark = Regex.Match(TextInput, @"<!--#DarkTheme-->");
            Match isCodeLight = Regex.Match(TextInput, @"<!--#LightTheme-->");
            if (isCodeDark.Success && isCodeLight.Success) throw new Exception("My brother in christ, you cannot have both DarkTheme and LightTheme.");
            if (isCodeDark.Success) isDark = true;
            if (isCodeLight.Success) isDark = false;

            /*var Resource = AvaloniaRuntimeXamlLoader.Parse<Styles>(
                TextInput
            );*/
            var Resource = AvaloniaRuntimeXamlLoader.Parse<Styles>(
                TextInput
            );
            Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
            Current.Styles.Remove(Current.Styles[4]);
            Current.Styles.Add(Resource);
            if (themeUri != "")
            {
                CurrentTheme = themeUri;
                SavedIsDark = isDark;
            }
        }
        catch (Exception ex)
        {
            ThemeFailed();
            OutputMessage += "# Theme has failed to load successfully due to an error.\n" + ex.Message;
            return OutputMessage;
        }

        OutputMessage += "# Theme loaded successfully!\n";
        return OutputMessage;
    }
    
    public static string? LoadTheme(string themeUri, bool isDark = true)
    {
        // I tried to do this before it was initialised, but "Replace" would prevent the "GetEntry" from ever being
        // - null, which means it would never "default" to dark theme if there is no entry. 
        if (themeUri.Contains("avares://Autodraw")) themeUri = themeUri.Replace("avares://Autodraw", "avares://csp");
        // Behold, terrible bruteforce-ey code! Performance be damned!
        try
        {
            Console.WriteLine("Loading Compiled Code");
            // Tries loading as Compiled.
            var Resource = (IStyle)AvaloniaXamlLoader.Load(
                new Uri(themeUri)
            );
            Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
            //Current.Styles.Remove(Current.Styles[4]);
            Current.Styles.Add(Resource);
            CurrentTheme = themeUri;
            SavedIsDark = isDark;
        }
        catch
        {
           try
           {
                // Tries loading as runtime uncompiled.
                Console.WriteLine("Loading Uncompiled Runtime Code");
                var TextInput = File.ReadAllText(themeUri);
                return LoadThemeFromString(TextInput, isDark, themeUri);
            }
            catch (Exception ex)
            {
                Console.WriteLine(":(");
                ThemeFailed();
                return ex.Message;
            }
        }
        return null;
    }
}