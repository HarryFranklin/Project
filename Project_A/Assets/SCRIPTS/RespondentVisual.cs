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

    // Animation Settings
    private float moveSpeed = 5.0f; // Higher = Faster movement
    private Vector2 _targetPosition;
    private bool _isInitialised = false;

    // Link the visual "dot" to the data itself and connect the manager to it
    public void Initialise(Respondent respondent, SimulationManager manager)
    {
        data = respondent;
        rect = GetComponent<RectTransform>();
        parent = transform.parent;
        _manager = manager;

        // Initial setup: Snap to position immediately so they don't fly in from (0,0)
        _targetPosition = CalculatePosition(data.currentLS, 0); // Start at 0 utility visual
        rect.anchoredPosition = _targetPosition;
        _isInitialised = true;
    }

    void Update()
    {
        if (_isInitialised)
        {
            // Smoothly move from current position -> target position
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, _targetPosition, Time.deltaTime * moveSpeed);
        }
    }

    // Calculate and update the X, Y and face of each person
    // X - based on wealth/LS (L -> R = Poor -> Rich) w/ random jitter
    // Y - u_Self (personal utility)
    // Face - Happy, sad or neutral
    public void UpdateVisuals(int currentLS, float normalisedSelfUtility, Sprite face)
    {
        if (faceImage) faceImage.sprite = face;

        if (parent)
        {
            // Instead of setting position directly, set the target for it to lerp to
            _targetPosition = CalculatePosition(currentLS, normalisedSelfUtility);
        }
    }

    // --- HELPER FUNCTIONS ---

    // X,Y Maths Helper
    private Vector2 CalculatePosition(int lsScore, float selfUtility)
    {
        RectTransform parentRect = parent.GetComponent<RectTransform>();
        float w = parentRect.rect.width;
        float h = parentRect.rect.height;

        // X-Axis: Wealth (0-10)
        float normalisedLS = lsScore / 10.0f; 
        float jitter = (data.id % 10) * 4.0f; 
        float xPos = ((normalisedLS * (w * 0.8f)) - (w * 0.4f)) + jitter;

        // Y-Axis: Utility
        float yPos = (selfUtility * h) - (h * 0.5f);

        return new Vector2(xPos, yPos);
    }

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