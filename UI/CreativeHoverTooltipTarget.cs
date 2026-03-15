using UnityEngine;
using UnityEngine.EventSystems;

namespace MineMogul.CreativeMode.UI;

internal sealed class CreativeHoverTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private CreativeHoverTooltip _tooltip;
    private string _text;

    internal void Initialize(CreativeHoverTooltip tooltip, string text)
    {
        _tooltip = tooltip;
        _text = text;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _tooltip?.Show(_text, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tooltip?.Hide();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        _tooltip?.UpdatePosition(eventData.position);
    }

    private void OnDisable()
    {
        _tooltip?.Hide();
    }
}
