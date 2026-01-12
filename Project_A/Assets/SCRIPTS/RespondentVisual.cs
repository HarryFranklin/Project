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
    // Face - Happy, sad or neutral - or dead
    public void UpdateVisuals(int currentLS, float normalisedSelfUtility, Sprite face)
    {
        if (faceImage) // if not null
        {
            faceImage.sprite = face;
            faceImage.color = Color.white; // Just to check
        }

        if (parent)
        {
            _targetPosition = CalculatePosition(currentLS, normalisedSelfUtility);
        }
    }

    // --- HELPER FUNCTIONS ---
    // X,Y Maths Helper
    private Vector2 CalculatePosition(int lsScore, float uSelf)
    {  
        // Grab data of the container the faces go in
        RectTransform parentRect = parent.GetComponent<RectTransform>();
        float w = parentRect.rect.width;
        float h = parentRect.rect.height;

        // Case: Death (-1)
        if (lsScore == -1)
        {
            // Drop to the absolute bottom margin
            // Spread randomly across X so they don't stack on one pixel
            float jitterDeath = (data.id % 20) * (w / 20.0f) - (w / 2.0f); // ?
            
            // Position: X = Random, Y = Bottom edge + padding
            return new Vector2(jitterDeath, -(h * 0.5f) + 15f); 
        }

        // Case: Alive (2-10)
        float normalisedLS = lsScore / 10.0f; 
        
        float jitter = (data.id % 10) * 4.0f; 
        float xPos = ((normalisedLS * (w * 0.8f)) - (w * 0.4f)) + jitter;

        float yPos = (uSelf * h) - (h * 0.5f);

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