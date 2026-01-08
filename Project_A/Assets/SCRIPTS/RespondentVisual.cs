using UnityEngine;
using UnityEngine.UI;

public class RespondentVisual : MonoBehaviour
{
    public Respondent data; // Reference to the data
    public Image faceImage; // Drag the UI Image here in Inspector

    // Called by Manager to update appearance
    public void SetVisuals(Sprite sprite)
    {
        if (faceImage != null)
        {
            faceImage.sprite = sprite;
        }
    }
}