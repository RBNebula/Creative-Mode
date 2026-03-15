using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using MineMogul.CreativeMode.Config;
using MineMogul.CreativeMode.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MineMogul.CreativeMode.Core;

internal sealed class CreativeModeRuntime : System.IDisposable
{
    private readonly CreativeModeConfig _config;
    private readonly CreativeInventoryController _creativeInventoryController;

    private bool _creativeModeEnabled;

    internal static CreativeModeRuntime Instance { get; private set; }

    internal CreativeModeRuntime(ConfigFile configFile, ManualLogSource logger)
    {
        _config = new CreativeModeConfig(configFile);
        _creativeInventoryController = new CreativeInventoryController(logger);

        Instance = this;
    }

    internal void Initialize()
    {
        bool prefValue = LoadCreativeModeStateFromPlayerPrefs(_config.CreativeModeEnabled.Value);
        SetCreativeModeEnabled(prefValue, persistConfig: true, persistPlayerPrefs: true);
    }

    internal void Tick()
    {
        bool isGameplayContext = IsGameplayContext();
        SyncFromPlayerPrefsIfChanged();
        _creativeInventoryController.Tick(
            tabsPerPage: _config.TabsPerPage.Value,
            isGameplayContext: isGameplayContext,
            inventoryVisible: IsInventoryVisible());
        ApplyUnlimitedBuildingFlag();
    }

    internal bool ShouldSuppressNativeInventoryInfo()
    {
        return _creativeModeEnabled && IsInventoryVisible();
    }

    internal void SetCreativeModeEnabledFromSettings(bool enabled)
    {
        SetCreativeModeEnabled(enabled, persistConfig: true, persistPlayerPrefs: true);
    }

    internal bool HasPendingCursorItem()
    {
        return _creativeInventoryController.HasPendingCursorItem();
    }

    internal bool TryPlacePendingCursorItem(InventorySlotUI slotUi)
    {
        return _creativeInventoryController.TryPlacePendingCursorItem(slotUi);
    }

    public void Dispose()
    {
        _creativeInventoryController.Dispose();

        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }

    private bool LoadCreativeModeStateFromPlayerPrefs(bool configDefault)
    {
        if (PlayerPrefs.HasKey(ModInfo.SETTINGS_PREF_KEY))
        {
            return PlayerPrefs.GetInt(ModInfo.SETTINGS_PREF_KEY, configDefault ? 1 : 0) == 1;
        }

        PlayerPrefs.SetInt(ModInfo.SETTINGS_PREF_KEY, configDefault ? 1 : 0);
        PlayerPrefs.Save();
        return configDefault;
    }

    private void SetCreativeModeEnabled(bool enabled, bool persistConfig, bool persistPlayerPrefs)
    {
        _creativeModeEnabled = enabled;

        if (persistConfig && _config.CreativeModeEnabled.Value != enabled)
        {
            _config.CreativeModeEnabled.Value = enabled;
            _config.SaveSafe();
        }

        if (persistPlayerPrefs)
        {
            PlayerPrefs.SetInt(ModInfo.SETTINGS_PREF_KEY, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        _creativeInventoryController.SetCreativeModeEnabled(enabled);
        ApplyUnlimitedBuildingFlag();
    }

    private void ApplyUnlimitedBuildingFlag()
    {
        DebugManager debugManager = Singleton<DebugManager>.Instance;
        if (debugManager == null)
        {
            return;
        }

        if (debugManager.UnlimitedBuilding != _creativeModeEnabled)
        {
            debugManager.UnlimitedBuilding = _creativeModeEnabled;
        }
    }

    private void SyncFromPlayerPrefsIfChanged()
    {
        bool prefValue = PlayerPrefs.GetInt(ModInfo.SETTINGS_PREF_KEY, _creativeModeEnabled ? 1 : 0) == 1;
        if (prefValue == _creativeModeEnabled)
        {
            return;
        }

        SetCreativeModeEnabled(prefValue, persistConfig: true, persistPlayerPrefs: false);
    }

    private static bool IsGameplayContext()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return false;
        }

        if (string.Equals(activeScene.name, "MainMenu", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Singleton<InventoryUIManager>.Instance != null)
        {
            return true;
        }

        if (GameObject.FindWithTag("Player") != null)
        {
            return true;
        }

        if (UnityEngine.Object.FindAnyObjectByType<PlayerController>() != null)
        {
            return true;
        }

        return false;
    }

    private static bool IsInventoryVisible()
    {
        InventoryUIManager inventoryUi = Singleton<InventoryUIManager>.Instance;
        return inventoryUi != null && inventoryUi.IsShowingInventory();
    }

}
