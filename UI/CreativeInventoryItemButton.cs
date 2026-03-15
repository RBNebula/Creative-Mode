using System;
using TMPro;
using MineMogul.CreativeMode.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace MineMogul.CreativeMode.UI;

internal sealed class CreativeInventoryItemButton : MonoBehaviour
{
    private static readonly Color BackgroundColor = new Color(0.20f, 0.20f, 0.20f, 0.92f);
    private static readonly Color HoverColor = new Color(0.38f, 0.38f, 0.38f, 0.95f);
    private static readonly Color PressedColor = new Color(0.52f, 0.52f, 0.52f, 0.98f);

    private ShopItem _shopItem;
    private Action<ShopItem> _onPressed;
    private Button _button;

    internal static CreativeInventoryItemButton Create(
        Transform parent,
        ShopItem shopItem,
        Action<ShopItem> onPressed,
        CreativeHoverTooltip hoverTooltip)
    {
        GameObject root = new GameObject(
            "CreativeItem_" + CreativeNameUtils.SanitizeName(shopItem.GetName(), "Item"),
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement),
            typeof(CreativeInventoryItemButton));
        root.transform.SetParent(parent, false);

        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = 72f;
        layout.preferredHeight = 72f;

        Image background = root.GetComponent<Image>();
        background.color = BackgroundColor;

        Button button = root.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = BackgroundColor;
        colors.highlightedColor = HoverColor;
        colors.selectedColor = HoverColor;
        colors.pressedColor = PressedColor;
        colors.disabledColor = new Color(0.10f, 0.10f, 0.10f, 0.50f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        CreateIconImage(root.transform, shopItem);

        if (hoverTooltip != null)
        {
            CreativeHoverTooltipTarget tooltipTarget = root.AddComponent<CreativeHoverTooltipTarget>();
            tooltipTarget.Initialize(hoverTooltip, shopItem.GetName());
        }

        CreativeInventoryItemButton component = root.GetComponent<CreativeInventoryItemButton>();
        component.Initialize(shopItem, onPressed, button);

        return component;
    }

    private static void CreateIconImage(Transform parent, ShopItem shopItem)
    {
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(parent, false);

        RectTransform iconRect = (RectTransform)iconGo.transform;
        iconRect.anchorMin = new Vector2(0.12f, 0.22f);
        iconRect.anchorMax = new Vector2(0.88f, 0.94f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        Image icon = iconGo.GetComponent<Image>();
        icon.sprite = shopItem.Definition.GetIcon();
        icon.enabled = icon.sprite != null;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }

    private void Initialize(ShopItem shopItem, Action<ShopItem> onPressed, Button button)
    {
        _shopItem = shopItem;
        _onPressed = onPressed;
        _button = button;
        _button.onClick.AddListener(HandlePressed);
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandlePressed);
        }
    }

    private void HandlePressed()
    {
        _onPressed?.Invoke(_shopItem);
    }
}
