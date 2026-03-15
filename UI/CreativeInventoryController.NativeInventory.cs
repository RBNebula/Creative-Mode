using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MineMogul.CreativeMode.UI;

internal sealed partial class CreativeInventoryController
{
    private void ApplyNativeInventorySuppression()
    {
        InventoryUIManager inventoryUi = Singleton<InventoryUIManager>.Instance;
        if (inventoryUi == null || inventoryUi.InventoryPanel == null)
        {
            return;
        }

        SuppressInventoryPanelBackground(inventoryUi);
        UpdateInventoryBackgroundReplacement(inventoryUi);

        if (inventoryUi.SelectedItemInfo != null)
        {
            SuppressNativeInventoryObject(inventoryUi.SelectedItemInfo);
        }

        SuppressInventoryObjectByName(inventoryUi, "ClothesPanel");
        SuppressInventoryObjectByName(inventoryUi, "SelectedItemPanel");
        SuppressInventoryObjectByName(inventoryUi, "ItemName (1)");

        Transform[] descendants = inventoryUi.InventoryPanel.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform child = descendants[i];
            if (child == null || child == inventoryUi.InventoryPanel.transform)
            {
                continue;
            }

            if (IsProtectedInventoryObject(inventoryUi, child.gameObject))
            {
                continue;
            }

            string lowerName = child.name.ToLowerInvariant();
            if (!NativeInventorySuppressNameTokens.Any(lowerName.Contains))
            {
                continue;
            }

            SuppressNativeInventoryObject(child.gameObject);
        }

        TMP_Text[] texts = inventoryUi.InventoryPanel.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || string.IsNullOrWhiteSpace(text.text))
            {
                continue;
            }

            if (!string.Equals(text.text.Trim(), "Inventory", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            SuppressNativeInventoryObject(text.gameObject);
        }

        InventoryItemPreview preview = Singleton<InventoryItemPreview>.Instance;
        if (preview != null)
        {
            if (preview.PreviouslyShownTool != null)
            {
                preview.StopPreview();
            }

            SetNativePreviewActive(false);
        }
    }

    private bool IsProtectedInventoryObject(InventoryUIManager inventoryUi, GameObject target)
    {
        if (inventoryUi == null || target == null)
        {
            return false;
        }

        if (target == inventoryUi.InventorySlotsPanel || target == inventoryUi.HotbarPanel)
        {
            return true;
        }

        if (inventoryUi.InventorySlotsPanel != null && target.transform.IsChildOf(inventoryUi.InventorySlotsPanel.transform))
        {
            return true;
        }

        if (inventoryUi.InventorySlotsPanel != null && inventoryUi.InventorySlotsPanel.transform.IsChildOf(target.transform))
        {
            return true;
        }

        if (inventoryUi.HotbarPanel != null && target.transform.IsChildOf(inventoryUi.HotbarPanel.transform))
        {
            return true;
        }

        if (inventoryUi.HotbarPanel != null && inventoryUi.HotbarPanel.transform.IsChildOf(target.transform))
        {
            return true;
        }

        return false;
    }

    private void SuppressNativeInventoryObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (!_suppressedNativeInventoryObjects.ContainsKey(target))
        {
            _suppressedNativeInventoryObjects[target] = target.activeSelf;
        }

        if (target.activeSelf)
        {
            target.SetActive(false);
        }
    }

    private void SuppressNativeInventoryGraphic(Graphic target)
    {
        if (target == null)
        {
            return;
        }

        if (!_suppressedNativeInventoryGraphics.ContainsKey(target))
        {
            _suppressedNativeInventoryGraphics[target] = target.enabled;
        }

        if (target.enabled)
        {
            target.enabled = false;
        }
    }

    private void RestoreSuppressedNativeInventoryObjects()
    {
        RestoreInventoryBackgroundReplacement();

        if (_suppressedNativeInventoryGraphics.Count > 0)
        {
            List<Graphic> graphics = _suppressedNativeInventoryGraphics.Keys.ToList();
            for (int i = 0; i < graphics.Count; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                if (_suppressedNativeInventoryGraphics[graphic])
                {
                    graphic.enabled = true;
                }
            }

            _suppressedNativeInventoryGraphics.Clear();
        }

        if (_suppressedNativeInventoryObjects.Count == 0)
        {
            return;
        }

        List<GameObject> keys = _suppressedNativeInventoryObjects.Keys.ToList();
        for (int i = 0; i < keys.Count; i++)
        {
            GameObject target = keys[i];
            if (target == null)
            {
                continue;
            }

            if (_suppressedNativeInventoryObjects[target])
            {
                target.SetActive(true);
            }
        }

        _suppressedNativeInventoryObjects.Clear();
    }

    private void SuppressInventoryPanelBackground(InventoryUIManager inventoryUi)
    {
        if (inventoryUi == null || inventoryUi.InventoryPanel == null)
        {
            return;
        }

        Transform panelTransform = FindInventoryCanvasChild(inventoryUi, "Panel");
        if (panelTransform == null)
        {
            Image fallbackImage = inventoryUi.InventoryPanel.GetComponent<Image>();
            if (fallbackImage != null)
            {
                SuppressNativeInventoryGraphic(fallbackImage);
            }

            return;
        }

        Image panelImage = panelTransform.GetComponent<Image>();
        if (panelImage != null)
        {
            SuppressNativeInventoryGraphic(panelImage);
        }
    }

    private void UpdateInventoryBackgroundReplacement(InventoryUIManager inventoryUi)
    {
        if (inventoryUi == null || inventoryUi.InventoryPanel == null)
        {
            return;
        }

        Transform inventoryCanvasRoot = ResolveInventoryCanvasRoot(inventoryUi);
        RectTransform inventoryRect = inventoryCanvasRoot as RectTransform;
        if (inventoryRect == null)
        {
            inventoryRect = inventoryUi.InventoryPanel.GetComponent<RectTransform>();
        }

        if (inventoryRect == null)
        {
            return;
        }

        bool hasBounds = TryGetLocalRectFromInventorySlots(inventoryRect, inventoryUi.InventorySlotsPanel, out Rect combinedRect);
        if (inventoryUi.HotbarPanel != null && TryGetLocalRectFromInventorySlots(inventoryRect, inventoryUi.HotbarPanel, out Rect hotbarRect))
        {
            combinedRect = hasBounds ? Encapsulate(combinedRect, hotbarRect) : hotbarRect;
            hasBounds = true;
        }

        if (!hasBounds)
        {
            return;
        }

        const float horizontalPadding = 14f;
        const float verticalPadding = 10f;
        combinedRect.xMin -= horizontalPadding;
        combinedRect.xMax += horizontalPadding;
        combinedRect.yMin -= verticalPadding;
        combinedRect.yMax += verticalPadding;

        EnsureInventoryBackgroundReplacement(inventoryRect);
        if (_nativeInventoryBackgroundReplacement == null || _nativeInventoryBackgroundReplacementImage == null)
        {
            return;
        }

        Image sourceImage = null;
        Transform panelTransform = FindInventoryCanvasChild(inventoryUi, "Panel");
        if (panelTransform != null)
        {
            sourceImage = panelTransform.GetComponent<Image>();
        }
        else
        {
            sourceImage = inventoryUi.InventoryPanel.GetComponent<Image>();
        }

        _nativeInventoryBackgroundReplacementImage.color = sourceImage != null ? sourceImage.color : new Color(0f, 0f, 0f, 0.6f);
        _nativeInventoryBackgroundReplacementImage.enabled = true;
        _nativeInventoryBackgroundReplacement.SetActive(true);
        _nativeInventoryBackgroundReplacement.transform.SetAsFirstSibling();

        RectTransform backgroundRect = (RectTransform)_nativeInventoryBackgroundReplacement.transform;
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = combinedRect.center;
        backgroundRect.sizeDelta = combinedRect.size;
    }

    private void EnsureInventoryBackgroundReplacement(RectTransform parent)
    {
        if (_nativeInventoryBackgroundReplacement != null)
        {
            return;
        }

        _nativeInventoryBackgroundReplacement = CreateUiObject("CreativeModeInventoryBackgroundReplacement", parent, typeof(Image));
        RectTransform replacementRect = (RectTransform)_nativeInventoryBackgroundReplacement.transform;
        replacementRect.anchorMin = new Vector2(0.5f, 0.5f);
        replacementRect.anchorMax = new Vector2(0.5f, 0.5f);
        replacementRect.pivot = new Vector2(0.5f, 0.5f);
        replacementRect.anchoredPosition = Vector2.zero;
        replacementRect.sizeDelta = Vector2.zero;

        _nativeInventoryBackgroundReplacementImage = _nativeInventoryBackgroundReplacement.GetComponent<Image>();
        _nativeInventoryBackgroundReplacementImage.raycastTarget = false;
        _nativeInventoryBackgroundReplacement.SetActive(false);
    }

    private void RestoreInventoryBackgroundReplacement()
    {
        if (_nativeInventoryBackgroundReplacement != null)
        {
            _nativeInventoryBackgroundReplacement.SetActive(false);
        }
    }

    private void SuppressInventoryObjectByName(InventoryUIManager inventoryUi, string objectName)
    {
        if (inventoryUi == null || inventoryUi.InventoryPanel == null || string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        Transform inventoryCanvasRoot = ResolveInventoryCanvasRoot(inventoryUi);
        if (inventoryCanvasRoot == null)
        {
            inventoryCanvasRoot = inventoryUi.InventoryPanel.transform;
        }

        Transform[] descendants = inventoryCanvasRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform transform = descendants[i];
            if (transform == null)
            {
                continue;
            }

            if (!string.Equals(transform.name, objectName, System.StringComparison.Ordinal))
            {
                continue;
            }

            SuppressNativeInventoryObject(transform.gameObject);
        }
    }

    private static Transform ResolveInventoryCanvasRoot(InventoryUIManager inventoryUi)
    {
        if (inventoryUi == null || inventoryUi.InventoryPanel == null)
        {
            return null;
        }

        Transform current = inventoryUi.InventoryPanel.transform;
        while (current != null)
        {
            if (string.Equals(current.name, "Canvas", System.StringComparison.Ordinal))
            {
                return current;
            }

            current = current.parent;
        }

        return inventoryUi.InventoryPanel.transform;
    }

    private static Transform FindInventoryCanvasChild(InventoryUIManager inventoryUi, string objectName)
    {
        Transform inventoryCanvasRoot = ResolveInventoryCanvasRoot(inventoryUi);
        if (inventoryCanvasRoot == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        if (string.Equals(inventoryCanvasRoot.name, objectName, System.StringComparison.Ordinal))
        {
            return inventoryCanvasRoot;
        }

        Transform directChild = inventoryCanvasRoot.Find(objectName);
        if (directChild != null)
        {
            return directChild;
        }

        Transform[] descendants = inventoryCanvasRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform transform = descendants[i];
            if (transform == null)
            {
                continue;
            }

            if (string.Equals(transform.name, objectName, System.StringComparison.Ordinal))
            {
                return transform;
            }
        }

        return null;
    }

    private static bool TryGetLocalRectFromInventorySlots(RectTransform parentRect, GameObject panelObject, out Rect localRect)
    {
        localRect = default;
        if (parentRect == null || panelObject == null)
        {
            return false;
        }

        InventorySlotUI[] slots = panelObject.GetComponentsInChildren<InventorySlotUI>(true);
        if (slots == null || slots.Length == 0)
        {
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            return TryGetLocalRect(parentRect, panelRect, out localRect);
        }

        bool hasBounds = false;
        Rect combinedRect = default;
        for (int i = 0; i < slots.Length; i++)
        {
            InventorySlotUI slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            if (slotRect == null)
            {
                continue;
            }

            if (!TryGetLocalRect(parentRect, slotRect, out Rect slotLocalRect))
            {
                continue;
            }

            combinedRect = hasBounds ? Encapsulate(combinedRect, slotLocalRect) : slotLocalRect;
            hasBounds = true;
        }

        if (!hasBounds)
        {
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            return TryGetLocalRect(parentRect, panelRect, out localRect);
        }

        localRect = combinedRect;
        return true;
    }

    private static bool TryGetLocalRect(RectTransform parentRect, RectTransform targetRect, out Rect localRect)
    {
        localRect = default;
        if (parentRect == null || targetRect == null)
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            RectTransformUtility.WorldToScreenPoint(null, corners[0]),
            null,
            out Vector2 bottomLeft))
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            RectTransformUtility.WorldToScreenPoint(null, corners[2]),
            null,
            out Vector2 topRight))
        {
            return false;
        }

        localRect = Rect.MinMaxRect(
            Mathf.Min(bottomLeft.x, topRight.x),
            Mathf.Min(bottomLeft.y, topRight.y),
            Mathf.Max(bottomLeft.x, topRight.x),
            Mathf.Max(bottomLeft.y, topRight.y));
        return true;
    }

    private static Rect Encapsulate(Rect a, Rect b)
    {
        return Rect.MinMaxRect(
            Mathf.Min(a.xMin, b.xMin),
            Mathf.Min(a.yMin, b.yMin),
            Mathf.Max(a.xMax, b.xMax),
            Mathf.Max(a.yMax, b.yMax));
    }

    private void SetNativePreviewActive(bool active)
    {
        InventoryItemPreview preview = Singleton<InventoryItemPreview>.Instance;
        if (preview == null || preview.gameObject == null)
        {
            return;
        }

        if (!active)
        {
            _nativePreviewWasActive = preview.gameObject.activeSelf;
            if (preview.gameObject.activeSelf)
            {
                preview.gameObject.SetActive(false);
            }

            return;
        }

        if (_nativePreviewWasActive && !preview.gameObject.activeSelf)
        {
            preview.gameObject.SetActive(true);
        }
    }
}
