using BepInEx;
using HarmonyLib;
using MineMogul.CreativeMode.Core;

namespace MineMogul.CreativeMode;

[BepInPlugin(ModInfo.PLUGIN_GUID, ModInfo.PLUGIN_NAME, ModInfo.PLUGIN_VERSION)]
public sealed class CreativeMode : BaseUnityPlugin
{
    private Harmony _harmony;
    private CreativeModeRuntime _runtime;

    private void Awake()
    {
        _runtime = new CreativeModeRuntime(Config, Logger);
        _runtime.Initialize();

        _harmony = new Harmony(ModInfo.HARMONY_ID);
        _harmony.PatchAll(typeof(CreativeMode).Assembly);
    }

    private void Update()
    {
        if (_runtime != null)
        {
            _runtime.Tick();
        }
    }

    private void OnDestroy()
    {
        if (_harmony != null)
        {
            _harmony.UnpatchSelf();
            _harmony = null;
        }

        if (_runtime != null)
        {
            _runtime.Dispose();
            _runtime = null;
        }
    }
}
