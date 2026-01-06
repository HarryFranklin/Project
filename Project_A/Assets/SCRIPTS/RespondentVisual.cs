using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RespondentVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler 
{
    public Respondent data;
    private Image _image;
    private Color _originalColor; 

    private Action<Respondent> _onHoverEnter;
    private Action _onHoverExit;
    private Action<Respondent> _onClick;

    public void Initialise(Respondent respondentData, Action<Respondent> onHover, Action onExit, Action<Respondent> onClick) 
    {
        data = respondentData;
        _onHoverEnter = onHover;
        _onHoverExit = onExit;
        _onClick = onClick;

        _image = GetComponent<Image>();
    }

    public void SetColour(Color c) 
    {
        if (_image != null) {
            _image.color = c;
            _originalColor = c;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => _onHoverEnter?.Invoke(data);
    public void OnPointerExit(PointerEventData eventData) => _onHoverExit?.Invoke();
}