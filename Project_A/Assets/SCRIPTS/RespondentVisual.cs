using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RespondentVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Debug / Inspector View")]
    public Respondent data;

    [Header("References")]
    public Image leftFaceImage;  
    public Image rightFaceImage;
    
    private RectTransform rect;
    private SimulationManager _manager;

    // Animation
    private Vector2 _targetPosition;
    private float moveSpeed = 5.0f;
    private bool _isInitialised = false;

    // Link the visual "dot" to the data itself and connect the manager to it
    public void Initialise(Respondent respondent, SimulationManager manager)
    {
        data = respondent;
        _manager = manager;
        rect = GetComponent<RectTransform>();
        _isInitialised = true;
    }

    void Update()
    {
        if (_isInitialised)
        {
            // Smoothly lerp to target
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, _targetPosition, Time.deltaTime * moveSpeed);
        }
    }

    public void UpdateVisuals(Vector2 newPosition, Sprite leftSprite, Sprite rightSprite)
    {
        _targetPosition = newPosition;

        if (leftFaceImage) 
        {
            leftFaceImage.sprite = leftSprite;
        }

        if (rightFaceImage) 
        {
            rightFaceImage.sprite = rightSprite;
        }

        if (rect.anchoredPosition == Vector2.zero)
        {
            rect.anchoredPosition = newPosition;
        }
    }

    // --- POINTER METHODS ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_manager) _manager.OnHoverEnter(data);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_manager) _manager.OnHoverExit();
    }
}