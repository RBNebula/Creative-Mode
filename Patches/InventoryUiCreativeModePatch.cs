using HarmonyLib;
using MineMogul.CreativeMode.Core;
using UnityEngine.EventSystems;

namespace MineMogul.CreativeMode.Patches;

[HarmonyPatch]
internal static class InventoryUiCreativeModePatch
{
    [HarmonyPatch(typeof(InventoryUIManager), nameof(InventoryUIManager.UpdateSelectedItemInfo))]
    [HarmonyPrefix]
    private static bool UpdateSelectedItemInfoPrefix(InventoryUIManager __instance, BaseHeldTool tool)
    {
        if (!ShouldSuppress())
        {
            return true;
        }

        __instance.SelectedTool = tool;
        if (__instance.SelectedItemInfo != null)
        {
            __instance.SelectedItemInfo.SetActive(false);
        }

        return false;
    }

    [HarmonyPatch(typeof(InventoryItemPreview), nameof(InventoryItemPreview.StartPreview))]
    [HarmonyPrefix]
    private static bool StartPreviewPrefix()
    {
        return !ShouldSuppress();
    }

    [HarmonyPatch(typeof(InventorySlotUI), nameof(InventorySlotUI.OnPointerDown))]
    [HarmonyPrefix]
    private static bool InventorySlotPointerDownPrefix(InventorySlotUI __instance, PointerEventData eventData)
    {
        CreativeModeRuntime runtime = CreativeModeRuntime.Instance;
        if (runtime == null || !runtime.HasPendingCursorItem())
        {
            return true;
        }

        runtime.TryPlacePendingCursorItem(__instance);
        return false;
    }

    private static bool ShouldSuppress()
    {
        CreativeModeRuntime runtime = CreativeModeRuntime.Instance;
        if (runtime == null)
        {
            return false;
        }

        return runtime.ShouldSuppressNativeInventoryInfo();
    }
}
