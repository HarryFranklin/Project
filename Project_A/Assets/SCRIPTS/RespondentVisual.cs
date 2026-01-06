using System;
using UnityEngine;
using UnityEngine.UI; // For UI Image
using UnityEngine.EventSystems;

public class RespondentVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler 
{    
    private Respondent _data;
    private Vector3 _originalScale = Vector3.zero;

    private Action<Respondent> _onHoverEnter;
    private Action _onHoverExit;
    private Action<Respondent> _onClick;

    public void Initialise(Respondent data, Action<Respondent> onHover, Action onExit, Action<Respondent> onClick) 
    {
        _data = data;
        _onHoverEnter = onHover;
        _onHoverExit = onExit;
        _onClick = onClick;

        // Try to find a renderer to colour it
        SetRandomColour();

        // Capture scale for the hover effect
        _originalScale = transform.localScale;
    }

    private void SetRandomColour() {
        Color randomCol = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
        
        Image img = GetComponent<Image>();
        if (img != null) 
        {
            img.color = randomCol;
            return;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) 
    {
        if (_originalScale == Vector3.zero) _originalScale = transform.localScale;
        transform.localScale = _originalScale * 1.2f; // Pop effect
        _onHoverEnter?.Invoke(_data);
    }

    public void OnPointerExit(PointerEventData eventData) 
    {
        if (_originalScale == Vector3.zero) _originalScale = transform.localScale;
        transform.localScale = _originalScale; // Reset
        _onHoverExit?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData) 
    {
        _onClick?.Invoke(_data);
    }
}