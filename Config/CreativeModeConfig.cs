using BepInEx.Configuration;

namespace MineMogul.CreativeMode.Config;

internal sealed class CreativeModeConfig
{
    private readonly ConfigFile _configFile;

    internal ConfigEntry<bool> CreativeModeEnabled { get; }
    internal ConfigEntry<int> TabsPerPage { get; }

    internal CreativeModeConfig(ConfigFile configFile)
    {
        _configFile = configFile;

        CreativeModeEnabled = _configFile.Bind(
            "General",
            "CreativeModeEnabled",
            false,
            "Enables creative mode. While enabled, unlimited building is forced on and the creative inventory UI is shown in the player inventory.");

        TabsPerPage = _configFile.Bind(
            "UI",
            "TabsPerPage",
            8,
            new ConfigDescription(
                "How many category tabs to show in one page before the next-page arrow appears.",
                new AcceptableValueRange<int>(1, 24)));
    }

    internal void SaveSafe()
    {
        try
        {
            _configFile.Save();
        }
        catch
        {
            // Config save can fail during shutdown; ignore and continue.
        }
    }
}
