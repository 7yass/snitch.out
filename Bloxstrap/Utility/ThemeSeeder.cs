using System.Reflection;

namespace Bloxstrap.Utility
{
    // snitch.out: ships a built-in launcher theme so fresh installs
    // boot into our identity instead of a generic dialog
    static class ThemeSeeder
    {
        public const string BuiltInThemeName = "snitch";

        public static void EnsureBuiltInTheme()
        {
            const string LOG_IDENT = "ThemeSeeder::EnsureBuiltInTheme";

            try
            {
                string themeDir = Path.Combine(Paths.CustomThemes, BuiltInThemeName);
                string themeFile = Path.Combine(themeDir, "Theme.xml");

                if (File.Exists(themeFile))
                    return;

                Directory.CreateDirectory(themeDir);

                using Stream? stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("Bloxstrap.Resources.CustomBootstrapperTheme_Snitch.xml");

                if (stream is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Built-in theme resource not found");
                    return;
                }

                using var output = File.Create(themeFile);
                stream.CopyTo(output);

                App.Logger.WriteLine(LOG_IDENT, $"Seeded built-in theme to {themeFile}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }
    }
}
