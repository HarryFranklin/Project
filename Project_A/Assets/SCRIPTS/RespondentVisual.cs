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

    // --- Optimisation ---
    private const float SNAP_DISTANCE = 0.1f; 
    private const float SNAP_SQR = SNAP_DISTANCE * SNAP_DISTANCE; 
    private bool _isMoving = false;
    
    // State
    private SimulationManager _manager;
    private bool _isGhostModeEnabled = false;
    private bool _isHovered = false;

    public void Initialise(Respondent respondent, SimulationManager manager)
    {
        data = respondent;
        _manager = manager;

        if (ghostGroup) ghostGroup.gameObject.SetActive(false);
        if (arrowLine) arrowLine.gameObject.SetActive(false);
        
        // Safety: calculate arrow once at spawn so it's ready
        UpdateArrowGeometry();
    }

    // --- Movement loop ---
    public bool ManualUpdate(float dt)
    {
        if (!_isMoving) return false;

        bool faceDone = MoveRect(faceGroup, currentPosition, dt);
        
        // We always move the ghost (even if invisible) so its position is ready for the arrow
        bool ghostDone = true;
        if (ghostGroup) 
        {
            ghostDone = MoveRect(ghostGroup, previousPosition, dt);
        }

        if (faceDone && ghostDone)
        {
            _isMoving = false;
            // Calculate the arrow geometry once it stopped moving
            UpdateArrowGeometry();
        }
        return true; 
    }

    private bool MoveRect(RectTransform rect, Vector2 target, float dt)
    {
        if (!rect) return true;
        
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, dt * 5f);
        
        if ((rect.anchoredPosition - target).sqrMagnitude < SNAP_SQR)
        {
            rect.anchoredPosition = target;
            return true;
        }
        return false;
    }

    public void UpdateVisuals(Vector2 currPos, Vector2 prevPos, Sprite fLeft, Sprite fRight, Sprite gLeft, Sprite gRight, bool ghostMode)
    {
        if (currPos != currentPosition || prevPos != previousPosition) 
        {
            _isMoving = true;
        }

        currentPosition = currPos; 
        previousPosition = prevPos;
        _isGhostModeEnabled = ghostMode;
        
        if (faceLeft) faceLeft.sprite = fLeft;
        if (faceRight) faceRight.sprite = fRight;
        if (ghostLeft) ghostLeft.sprite = gLeft;
        if (ghostRight) ghostRight.sprite = gRight;

        if (ghostGroup) ghostGroup.gameObject.SetActive(_isGhostModeEnabled);
        if (arrowLine) arrowLine.gameObject.SetActive(false);
    }

    // --- Hover logic ---
    public void SetFocusState(bool isTarget, bool isSomeoneHovered)
    {
        _isHovered = isTarget;

        // 1. Reset (no one is hovered over)
        if (!isSomeoneHovered)
        {
            if (rootCanvasGroup) rootCanvasGroup.alpha = 1f;
            if (arrowLine) arrowLine.gameObject.SetActive(false);
            if (ghostGroup) ghostGroup.gameObject.SetActive(_isGhostModeEnabled);
            return;
        }

        // 2. Target (show arrows)
        if (isTarget)
        {
            if (rootCanvasGroup) rootCanvasGroup.alpha = 1f;
            transform.SetAsLastSibling();

            // If I'm the target, I always show the arrow.
            // Ignore '_isGhostModeEnabled' here because the user is specifically pointing at this person.
            bool showDetails = true; 

            if (ghostGroup && showDetails) ghostGroup.gameObject.SetActive(true);
            if (arrowLine && showDetails) arrowLine.gameObject.SetActive(true);
        }
        // 3. Fade out
        else
        {
            float fadeAlpha = (_isGhostModeEnabled || isSomeoneHovered) ? 0.05f : 0.1f; 
            if (rootCanvasGroup) rootCanvasGroup.alpha = fadeAlpha;
            if (arrowLine) arrowLine.gameObject.SetActive(false);
        }
    }

    private void UpdateArrowGeometry()
    {
        if (!ghostGroup || !faceGroup || !arrowLine) return;

        Vector2 start = ghostGroup.anchoredPosition;
        Vector2 end = faceGroup.anchoredPosition;
        Vector2 diff = end - start;

        // If distance is zero, hide arrow to prevent glitches
        if (diff.magnitude < 1f)
        {
            arrowLine.sizeDelta = new Vector2(0, arrowLine.sizeDelta.y);
            return;
        }

        // 1. Position (midpoint)
        arrowLine.anchoredPosition = start + (diff / 2f);        

        // 2. Size (length)
        arrowLine.sizeDelta = new Vector2(diff.magnitude, arrowLine.sizeDelta.y);        
        
        // 3. Rotation
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        arrowLine.localRotation = Quaternion.Euler(0, 0, angle);
    }

    // --- Hovering ---
    public void OnPointerEnter(PointerEventData e) => _manager.OnHoverEnter(data);
    public void OnPointerExit(PointerEventData e) => _manager.OnHoverExit();
}