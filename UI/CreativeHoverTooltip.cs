using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineMogul.CreativeMode.UI;

internal sealed class CreativeHoverTooltip
{
    private const float HorizontalPadding = 28f;
    private const float VerticalPadding = 20f;
    private const float MinWidth = 140f;
    private const float MinHeight = 38f;

    private readonly RectTransform _canvasRect;
    private readonly RectTransform _rootRect;
    private readonly RectTransform _contentRect;
    private readonly TextMeshProUGUI _label;

    internal CreativeHoverTooltip(RectTransform canvasRect, Transform parent)
    {
        _canvasRect = canvasRect;

        GameObject root = new GameObject(
            "CreativeHoverTooltip",
            typeof(RectTransform),
            typeof(Image));
        root.transform.SetParent(parent, false);
        root.SetActive(false);

        _rootRect = (RectTransform)root.transform;
        _rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        _rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        _rootRect.pivot = new Vector2(0.5f, 1f);
        _rootRect.sizeDelta = new Vector2(160f, 44f);

        Image border = root.GetComponent<Image>();
        border.type = Image.Type.Simple;
        border.color = Color.white;
        border.raycastTarget = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(Image));
        contentObject.transform.SetParent(root.transform, false);

        _contentRect = (RectTransform)contentObject.transform;
        _contentRect.anchorMin = Vector2.zero;
        _contentRect.anchorMax = Vector2.one;
        _contentRect.offsetMin = Vector2.one;
        _contentRect.offsetMax = -Vector2.one;

        Image background = contentObject.GetComponent<Image>();
        background.type = Image.Type.Simple;
        background.color = new Color(0.06f, 0.06f, 0.06f, 0.98f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(contentObject.transform, false);

        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 10f);
        textRect.offsetMax = new Vector2(-14f, -10f);

        _label = textObject.GetComponent<TextMeshProUGUI>();
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontSize = 18f;
        _label.color = Color.white;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        _label.raycastTarget = false;
    }

    internal void Show(string text, Vector2 screenPosition)
    {
        if (_rootRect == null)
        {
            return;
        }

        _label.text = text ?? string.Empty;
        ResizeToFitLabel();
        _rootRect.gameObject.SetActive(true);
        UpdatePosition(screenPosition);
    }

    internal void UpdatePosition(Vector2 screenPosition)
    {
        if (_rootRect == null || _canvasRect == null || !_rootRect.gameObject.activeSelf)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPosition, null, out Vector2 localPoint))
        {
            return;
        }

        const float offsetX = 0f;
        const float offsetY = -20f;
        Vector2 target = localPoint + new Vector2(offsetX, offsetY);

        float width = _rootRect.rect.width;
        float height = _rootRect.rect.height;
        Rect canvasRect = _canvasRect.rect;

        float halfWidth = width * 0.5f;
        target.x = Mathf.Clamp(target.x, canvasRect.xMin + halfWidth + 10f, canvasRect.xMax - halfWidth - 10f);
        target.y = Mathf.Clamp(target.y, canvasRect.yMin + height + 10f, canvasRect.yMax - 10f);

        _rootRect.anchoredPosition = target;
        _rootRect.SetAsLastSibling();
    }

    internal void Hide()
    {
        if (_rootRect != null)
        {
            _rootRect.gameObject.SetActive(false);
        }
    }

    internal void EnsureOnTop()
    {
        if (_rootRect != null && _rootRect.gameObject.activeSelf)
        {
            _rootRect.SetAsLastSibling();
        }
    }

    private void ResizeToFitLabel()
    {
        _label.ForceMeshUpdate();
        Vector2 preferredSize = _label.GetPreferredValues(_label.text);
        float width = Mathf.Max(MinWidth, preferredSize.x + HorizontalPadding);
        float height = Mathf.Max(MinHeight, preferredSize.y + VerticalPadding);
        _rootRect.sizeDelta = new Vector2(width, height);
    }
}
