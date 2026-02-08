using UnityEngine;
using UnityEngine.EventSystems;
using System;

// This sits on policy buttons so you can preview their effects.
public class ButtonHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Policy policy;
    public Action<Policy> onHover;
    public Action onExit;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (policy != null && onHover != null)
        {
            onHover.Invoke(policy);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (onExit != null)
        {
            onExit.Invoke();
        }
    }
}