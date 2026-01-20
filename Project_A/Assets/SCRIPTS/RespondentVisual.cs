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
    private bool _showAllArrows = false;
    private bool _isHovered = false;

    public void Initialise(Respondent respondent, SimulationManager manager)
    {
        data = respondent;
        _manager = manager;

        if (ghostGroup) ghostGroup.gameObject.SetActive(false);
        if (arrowLine) arrowLine.gameObject.SetActive(false);
        
        // Safety: calculate arrow once at spawn (will likely be size 0 initially, which is fine)
        UpdateArrowGeometry();
    }

    // --- Movement Loop ---
    public bool ManualUpdate(float dt)
    {
        // Sleep check
        if (!_isMoving) return false;

        bool faceDone = MoveRect(faceGroup, currentPosition, dt);
        
        // Always move ghost so it's ready for the arrow
        bool ghostDone = true;
        if (ghostGroup) 
        {
            ghostDone = MoveRect(ghostGroup, previousPosition, dt);
        }

        if (faceDone && ghostDone)
        {
            _isMoving = false; // Sleep
            
            // Now that we have arrived, calculate the arrow geometry
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

        // Store new data
        currentPosition = currPos; 
        previousPosition = prevPos;
        _isGhostModeEnabled = ghostMode;
        
        if (faceLeft) faceLeft.sprite = fLeft;
        if (faceRight) faceRight.sprite = fRight;
        if (ghostLeft) ghostLeft.sprite = gLeft;
        if (ghostRight) ghostRight.sprite = gRight;

        // Reset visibility for the new state
        if (ghostGroup) ghostGroup.gameObject.SetActive(_isGhostModeEnabled);
        if (arrowLine) arrowLine.gameObject.SetActive(_showAllArrows);
    }

    // --- Toggle Arrow Handler ---
    public void SetArrowState(bool showAll)
    {
        _showAllArrows = showAll;

        // Apply immediately
        if (arrowLine) 
        {
            arrowLine.gameObject.SetActive(_showAllArrows);
            if (_showAllArrows) UpdateArrowGeometry();
        }
    }

    // --- Hover Logic ---
    public void SetFocusState(bool isTarget, bool isSomeoneHovered)
    {
        _isHovered = isTarget;

        // 1. Reset (normal view)
        if (!isSomeoneHovered)
        {
            if (rootCanvasGroup) rootCanvasGroup.alpha = 1f;
            if (arrowLine) arrowLine.gameObject.SetActive(_showAllArrows);
            if (ghostGroup) ghostGroup.gameObject.SetActive(_isGhostModeEnabled);
            return;
        }

        // 2. Target (hovered)
        if (isTarget)
        {
            if (rootCanvasGroup) rootCanvasGroup.alpha = 1f;
            transform.SetAsLastSibling();

            // If I'm the target, force the arrow to show
            bool showDetails = true; 

            if (ghostGroup && showDetails) ghostGroup.gameObject.SetActive(true);
            
            if (arrowLine && showDetails) 
            {
                arrowLine.gameObject.SetActive(true);
                
                // Safety: if the arrow is somehow size 0 (e.g. didn't update yet), force it now.
                if (arrowLine.sizeDelta.x < 0.1f) UpdateArrowGeometry();
            }
        }
        // 3. Fade Out (background)
        else
        {
            float fadeAlpha = (_isGhostModeEnabled || isSomeoneHovered) ? 0.05f : 0.1f; 
            if (rootCanvasGroup) rootCanvasGroup.alpha = fadeAlpha;
            if (arrowLine) arrowLine.gameObject.SetActive(_showAllArrows);
        }
    }

    private void UpdateArrowGeometry()
    {
        if (!ghostGroup || !faceGroup || !arrowLine) return;

        Vector2 start = ghostGroup.anchoredPosition;
        Vector2 end = faceGroup.anchoredPosition;
        Vector2 diff = end - start;

        // If distance is basically zero, squash the arrow so it's invisible
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

    // --- Events ---
    public void OnPointerEnter(PointerEventData e) => _manager.OnHoverEnter(data);
    public void OnPointerExit(PointerEventData e) => _manager.OnHoverExit();
}