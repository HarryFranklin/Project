using UnityEngine;

public class GraphGrid : MonoBehaviour
{
    [Header("Container References")]
    public RectTransform personContainer;

    [Header("Layout Settings")]
    [Range(0f, 1f)] public float widthPadding = 0.1f;  // Padding on sides
    [Range(0f, 1f)] public float heightPadding = 0.1f; // Padding on top/bottom
    public float jitterAmount = 15f;

    void Awake()
    {
        personContainer = GetComponent<RectTransform>();
    }

    // Convert normalised coordinates into a local position based on the dimensions of the personContainer
    public Vector2 GetPlotPosition(float xNorm, float yNorm, int seedID)
    {
        // Safety Check
        if (personContainer == null)
        {
            Debug.LogError("GraphGrid: 'Person Container' is missing.");
            return Vector2.zero;
        }

        float width = personContainer.rect.width;
        float height = personContainer.rect.height;

        // Calculate the usable area
        float usableWidth = width * (1f - (widthPadding * 2));
        float usableHeight = height * (1f - (heightPadding * 2));

        // Map 0->1 to the usable area
        float xPosition = (xNorm * usableWidth) - (usableWidth * 0.5f);
        float yPosition = (yNorm * usableHeight) - (usableHeight * 0.5f);

        // Add jitter (deterministic? so they jitter to the same spot?)
        float jitterX = ((seedID * 132.1f) % jitterAmount) - (jitterAmount * 0.5f);
        float jitterY = ((seedID * 75.3f) % jitterAmount) - (jitterAmount * 0.5f);

        return new Vector2 (xPosition + jitterX, yPosition + jitterY);
    }

    // Helper for graveyard/special zones, i.e. at the bottom
    public Vector2 GetGraveyardPosition(int seedID)
    {
        float width = personContainer.rect.width;
        float height = personContainer.rect.height;

        // Spread along the bottom
        float x = ((seedID * 50f) % width - (width * 0.5f));

        return new Vector2(x, -(height*0.5f) + 20f); // + 20px from the bottom
    }
}