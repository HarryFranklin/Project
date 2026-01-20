using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RespondentVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Data")]
    public Respondent data;

    [Header("Coordinate Storage")]
    public Vector2 currentPosition;
    public Vector2 previousPosition;

    [Header("Visual Groups")]
    public RectTransform faceGroup;  
    public RectTransform ghostGroup;
    public RectTransform arrowLine;

    [Header("Images")]
    public Image faceLeft;
    public Image faceRight;
    public Image ghostLeft;
    public Image ghostRight;
    
    [Header("Fading")]
    public CanvasGroup rootCanvasGroup;

    // Optimisation Settings
    // 0.1f is the sweetspot for pixel snapping - 0.5 is too much as it could snap an object up to the next pixel.
    // This cuts the infinite tail off of the Lerp
    private const float SNAP_DISTANCE = 0.1f; 
    private const float SNAP_SQR = SNAP_DISTANCE * SNAP_DISTANCE; // Pre-calculate for speed

    private bool _isMoving;

    // State
    private SimulationManager _manager;
    private bool _isGhostModeEnabled = false;
    private bool _isHovered = false;

    public void Initialise(Respondent respondent, SimulationManager manager)
    {
        data = respondent;
        _manager = manager;
        
        // Default state: hidden
        if (ghostGroup) ghostGroup.gameObject.SetActive(false);
        if (arrowLine) arrowLine.gameObject.SetActive(false);
    }

    public bool ManualUpdate(float deltaTime)
    {
        // 1. Sleep Optimisation
        if (!_isMoving) return false; // Return False to say "I am sleeping"

        bool faceDone = MoveRect(faceGroup, currentPosition, deltaTime);
        
        bool ghostDone = true;
        if (ghostGroup && ghostGroup.gameObject.activeSelf)
        {
            ghostDone = MoveRect(ghostGroup, previousPosition, deltaTime);
        }

        if (faceDone && ghostDone)
        {
            _isMoving = false;
        }

        if (arrowLine && arrowLine.gameObject.activeSelf) 
        {
            UpdateArrowGeometry();
        }
        
        return true; // Return True to say "I am still active"
    }

    // Return True if we have reached the destination, and snapped
    private bool MoveRect(RectTransform rect, Vector2 target, float deltaTime)
    {
        if (!rect) return true;
        
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, deltaTime * 5f);

        if ((rect.anchoredPosition - target).sqrMagnitude < SNAP_SQR)
        {
            rect.anchoredPosition = target;
            return true;
        }
        return false;
    }

    public void UpdateVisuals(Vector2 currPos, Vector2 prevPos, Sprite fLeft, Sprite fRight, Sprite gLeft, Sprite gRight, bool ghostMode)
    {
        // Wake the script only if the target position has changed
        if (currPos != currentPosition || prevPos != previousPosition)
        {
            _isMoving = true;
        }

        currentPosition = currPos;
        previousPosition = prevPos;
        _isGhostModeEnabled = ghostMode;

        // Sprites
        if (faceLeft) faceLeft.sprite = fLeft;
        if (faceRight) faceRight.sprite = fRight;
        if (ghostLeft) ghostLeft.sprite = gLeft;
        if (ghostRight) ghostRight.sprite = gRight;

        // Visibility logic (default without hover)
        // If ghost mode is on, show the ghost faintly. If off, hide it.
        if (ghostGroup) ghostGroup.gameObject.SetActive(_isGhostModeEnabled);
        
        // Arrow is hidden until hover
        if (arrowLine) arrowLine.gameObject.SetActive(false); 
        
        // Reset alpha
        if (!_isHovered && rootCanvasGroup) rootCanvasGroup.alpha = 1f;
    }

    public void SetFocusState(bool isTarget, bool isSomeoneHovered)
    {
        _isHovered = isTarget;

        // 1. Normal View
        if (!isSomeoneHovered)
        {
            if (rootCanvasGroup) rootCanvasGroup.alpha = 1f;
            if (arrowLine) arrowLine.gameObject.SetActive(false);
            if (ghostGroup) ghostGroup.gameObject.SetActive(_isGhostModeEnabled);
            return;
        }

        // 2. Target
        if (isTarget)
        {
            if (rootCanvasGroup) rootCanvasGroup.alpha = 1f;
            transform.SetAsLastSibling(); 

            if (_isGhostModeEnabled)
            {
                if (ghostGroup) ghostGroup.gameObject.SetActive(true);
                if (arrowLine) 
                {
                    arrowLine.gameObject.SetActive(true);
                    UpdateArrowGeometry(); 
                }
            }
        }
        // C. Background
        else
        {
            float fadeAlpha = _isGhostModeEnabled ? 0.05f : 0.1f; 
            if (rootCanvasGroup) rootCanvasGroup.alpha = fadeAlpha;
            if (arrowLine) arrowLine.gameObject.SetActive(false);
        }
    }

    public void SetHoverState(bool isTarget, bool isSomeoneHovered)
    {
        _isHovered = isTarget;

        // No one is hovering -> Show normal state
        if (!isSomeoneHovered)
        {
            if (rootCanvasGroup) rootCanvasGroup.alpha = 1f;
            if (arrowLine) arrowLine.gameObject.SetActive(false);
            if (ghostGroup) ghostGroup.gameObject.SetActive(_isGhostModeEnabled); // Revert to global setting
            return;
        }

        // I am the target -> isolate mode
        if (isTarget)
        {
            if (rootCanvasGroup) rootCanvasGroup.alpha = 1f;
            transform.SetAsLastSibling(); // Bring to front

            if (_isGhostModeEnabled)
            {
                // Ensure ghost is visible
                if (ghostGroup) ghostGroup.gameObject.SetActive(true);
                // Turn on the arrow
                if (arrowLine) arrowLine.gameObject.SetActive(true);
            }
        }
        // I am not the target, fadem e out
        else
        {
            // If Ghost mode is on, fade out fully to focus on the trajectory
            // If Ghost mode is off, dim slightly (standard behaviour)
            float fadeAlpha = _isGhostModeEnabled ? 0.02f : 0.1f; 
            
            if (rootCanvasGroup) rootCanvasGroup.alpha = fadeAlpha;
            if (arrowLine) arrowLine.gameObject.SetActive(false);
        }
    }

    private void UpdateArrowGeometry()
    {
        // Draw line from ghost -> face
        // They have the same parent, so use local anchored positions
        Vector2 start = ghostGroup.anchoredPosition;
        Vector2 end = faceGroup.anchoredPosition;
        Vector2 diff = end - start;

        if (diff.magnitude < 1f) return; // Hide if too close

        arrowLine.anchoredPosition = start + (diff / 2f);
        arrowLine.sizeDelta = new Vector2(diff.magnitude, arrowLine.sizeDelta.y);
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        arrowLine.localRotation = Quaternion.Euler(0, 0, angle);
    }

    public void OnPointerEnter(PointerEventData e) => _manager.OnHoverEnter(data);
    public void OnPointerExit(PointerEventData e) => _manager.OnHoverExit();
}