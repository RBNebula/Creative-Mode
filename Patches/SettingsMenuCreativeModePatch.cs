using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MineMogul.CreativeMode.Core;
using UnityEngine;

namespace MineMogul.CreativeMode.Patches;

[HarmonyPatch(typeof(SettingsMenu))]
internal static class SettingsMenuCreativeModePatch
{
    private static readonly FieldInfo GeneralPageField = AccessTools.Field(typeof(SettingsMenu), "_generalPage");
    private static readonly FieldInfo DisplayNameField = AccessTools.Field(typeof(BaseSettingOption), "displayName");
    private static readonly MethodInfo UpdateLabelMethod = AccessTools.Method(typeof(BaseSettingOption), "UpdateLabel");

    [HarmonyPatch("OnEnable")]
    [HarmonyPostfix]
    private static void OnEnablePostfix(SettingsMenu __instance)
    {
        TryInject(__instance);
    }

    [HarmonyPatch("OnGeneralPagePressed")]
    [HarmonyPostfix]
    private static void OnGeneralPagePressedPostfix(SettingsMenu __instance)
    {
        TryInject(__instance);
    }

    private static void TryInject(SettingsMenu menu)
    {
        try
        {
            Inject(menu);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{ModInfo.LOG_PREFIX} Settings injection failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Inject(SettingsMenu menu)
    {
        if (menu == null || GeneralPageField == null)
        {
            return;
        }

        GameObject generalPage = GeneralPageField.GetValue(menu) as GameObject;
        if (generalPage == null)
        {
            return;
        }

        if (generalPage.GetComponentsInChildren<SettingToggle>(true).Any(IsCreativeModeToggleRow))
        {
            return;
        }

        SettingToggle template = generalPage.GetComponentInChildren<SettingToggle>(true);
        if (template == null || template.transform.parent == null)
        {
            return;
        }

        CreativeModeRuntime runtime = CreativeModeRuntime.Instance;
        bool runtimeDefault = PlayerPrefs.GetInt(ModInfo.SETTINGS_PREF_KEY, 0) == 1;

        if (!PlayerPrefs.HasKey(ModInfo.SETTINGS_PREF_KEY))
        {
            PlayerPrefs.SetInt(ModInfo.SETTINGS_PREF_KEY, runtimeDefault ? 1 : 0);
            PlayerPrefs.Save();
        }

        SettingToggle creativeToggle = CreateToggleRow(
            template.transform.parent,
            template,
            ModInfo.SETTINGS_TOGGLE_OBJECT_NAME,
            "Creative Mode",
            ModInfo.SETTINGS_PREF_KEY,
            runtimeDefault);

        creativeToggle.RefreshFromSaved();

        bool enabledFromUi = PlayerPrefs.GetInt(ModInfo.SETTINGS_PREF_KEY, runtimeDefault ? 1 : 0) == 1;
        runtime?.SetCreativeModeEnabledFromSettings(enabledFromUi);

        creativeToggle.onValueChanged += value =>
        {
            CreativeModeRuntime.Instance?.SetCreativeModeEnabledFromSettings(value);
        };
    }

    private static bool IsCreativeModeToggleRow(SettingToggle row)
    {
        return row != null && row.name == ModInfo.SETTINGS_TOGGLE_OBJECT_NAME;
    }

    private static SettingToggle CreateToggleRow(
        Transform rowParent,
        SettingToggle template,
        string objectName,
        string displayName,
        string settingKey,
        bool defaultValue)
    {
        GameObject row = UnityEngine.Object.Instantiate(template.gameObject, rowParent);
        row.name = objectName;
        row.transform.SetAsLastSibling();
        row.SetActive(true);

        SettingToggle toggle = row.GetComponent<SettingToggle>();
        SetPrivateField(toggle, "settingKey", settingKey);
        SetPrivateField(toggle, "defaultValue", defaultValue);
        SetPrivateField(toggle, "onText", "On");
        SetPrivateField(toggle, "offText", "Off");
        SetDisplayName(row, displayName);

        return toggle;
    }

    private static void SetDisplayName(GameObject rowRoot, string displayName)
    {
        BaseSettingOption option = rowRoot.GetComponent<BaseSettingOption>();
        if (option == null)
        {
            return;
        }

        DisplayNameField?.SetValue(option, displayName);
        UpdateLabelMethod?.Invoke(option, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }
}
