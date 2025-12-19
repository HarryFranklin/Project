using UnityEngine;
using UnityEngine.UI; // Required for Image
using UnityEngine.EventSystems; 

public class RespondentVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public Respondent data;
    private SimulationManager _manager;
    private Image _uiImage; // Changed from SpriteRenderer
    
    private Vector3 _originalScale = Vector3.zero;

    public void Initialise(Respondent respondentData, SimulationManager manager) {
        data = respondentData;
        _manager = manager;
        _uiImage = GetComponent<Image>();
        
        // Random colour
        if (_uiImage != null)
            _uiImage.color = new Color(Random.value, Random.value, Random.value);

        // Capture the scale
        _originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData) {
        if (_originalScale == Vector3.zero) _originalScale = transform.localScale;

        transform.localScale = _originalScale * 1.2f;
        
        if (_manager != null) _manager.ShowRespondentInfo(data);
    }

    public void OnPointerExit(PointerEventData eventData) {
        if (_originalScale == Vector3.zero) _originalScale = transform.localScale;

        transform.localScale = _originalScale;
        
        if (_manager != null) _manager.ClearInfo();
    }
    
    private void OnDisable() {
        if (_originalScale != Vector3.zero) transform.localScale = _originalScale;
    }
}