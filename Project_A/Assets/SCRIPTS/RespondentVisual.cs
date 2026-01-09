using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RespondentVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Debug / Inspector View")]
    public Respondent data;

    [Header("References")]
    public Image faceImage;
    private RectTransform rect;
    private Transform parent;
    private SimulationManager _manager;

    // Link the visual "dot" to the data itself and connect the manager to it
    public void Initialise(Respondent respondent, SimulationManager manager)
    {
        data = respondent;
        rect = GetComponent<RectTransform>();
        parent = transform.parent;
        _manager = manager;
    }

    // Calculate and update the X, Y and face of each person
    // X - based on wealth/LS (L -> R = Poor -> Rich) w/ random jitter
    // Y - u_Self (personal utility)
    // Face - Happy, sad or neutral
    public void UpdateVisuals(float normalisedSelfUtility, Sprite face)
    {
        if (faceImage) faceImage.sprite = face;

        if (parent)
        {
            RectTransform parentRect = parent.GetComponent<RectTransform>();
            float w = parentRect.rect.width;
            float h = parentRect.rect.height;

            // --- (X = Life Satisfaction / Wealth) ---
            // 1. Get their LS (0 to 10)
            int ls = data.currentLS; 
            
            // 2. Normalise to 0->1 (approximate)
            float normalisedLS = ls / 10.0f; 

            // 3. Map to Screen Width (-Width/2 to +Width/2)
            // A noise (data.id * 3.0f) so people with the same LS don't stack perfectly on top of each other
            float jitter = (data.id % 10) * 4.0f; 
            float xPos = ((normalisedLS * (w * 0.8f)) - (w * 0.4f)) + jitter;

            // --- Y AXIS (Personal Utility) ---
            // Map 0->1 utility to Bottom->Top
            float yPos = (normalisedSelfUtility * h) - (h * 0.5f);

            rect.anchoredPosition = new Vector2(xPos, yPos);
        }
    }

    // - Helper Functions -
    // Called when Mouse Enters the face
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Debug.Log("Mouse detected.");
        if (_manager != null)
        {
            _manager.OnHoverEnter(data);
        }
    }

    // Called when Mouse Leaves the face
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_manager != null) 
        {
            _manager.OnHoverExit();
        }
    }
}