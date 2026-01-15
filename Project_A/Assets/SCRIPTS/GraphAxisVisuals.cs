using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GraphAxisVisuals : MonoBehaviour
{
    [Header("Dependencies")]
    public GraphGrid grid;

    [Header("UI Objects (Lines)")]
    public RectTransform xAxisLine; 
    public RectTransform yAxisLine;

    [Header("UI Objects (Labels)")]
    public TMP_Text xAxisTitle;
    public TMP_Text yAxisTitle;
    public TMP_Text xMinText, xMaxText;
    public TMP_Text yMinText, yMaxText;

    // Struct to define the min-max-label etc. of an axis
    public struct AxisRange
    {
        public float min;
        public float max;
        public string label;

        public AxisRange(float min, float max, string label)
        {
            this.min = min;
            this.max = max;
            this.label = label;
        }
    }

    // Called by SimulationManager
    public void UpdateAxisVisuals(AxisVariable xType, AxisVariable yType)
    {
        // 1. Get the ranges based on the axes enum in GraphGrid
        AxisRange xRange = GetRange(xType);
        AxisRange yRange = GetRange(yType);

        // 2. Update text
        if (xAxisTitle) xAxisTitle.text = xRange.label;
        if (yAxisTitle) yAxisTitle.text = yRange.label;
        if (xMinText) xMinText.text = xRange.min.ToString("0.##");
        if (xMaxText) xMaxText.text = xRange.max.ToString("0.##");
        if (yMinText) yMinText.text = yRange.min.ToString("0.##");
        if (yMaxText) yMaxText.text = yRange.max.ToString("0.##");

        // 3. Calculate where zero is in normalised space (0-1)
        float xZeroNorm = Mathf.InverseLerp(xRange.min, xRange.max, 0f); // e.g. -5 to 5 returns 0.5
        float yZeroNorm = Mathf.InverseLerp(yRange.min, yRange.max, 0f);

        // 4. Ask GraphGrid where those points are in pixels
        Vector2 originPixel = grid.GetPlotPosition(xZeroNorm, yZeroNorm, 0); // 0 as axes don't jitter

        // 5. Move the lines
        if (xAxisLine)
        {
            // X-Axis sits at Y = 0
            xAxisLine.anchoredPosition = new Vector2(0, originPixel.y);
        }
        if (yAxisLine)
        {
            // Y-Axis sits at X = 0
            yAxisLine.anchoredPosition = new Vector2(originPixel.x, 0);
        }
    }

    // Defining data ranges
    public AxisRange GetRange(AxisVariable type)
    {
        if (type == AxisVariable.LifeSatisfaction)
        {
            return new AxisRange(0, 10, "Life Satisfaction (0-10)");
        }
        else if (type == AxisVariable.PersonalUtility)
        {
            return new AxisRange(0, 1, "Personal Utility");
        }
        else if (type == AxisVariable.SocietalUtility)
        {
            return new AxisRange(0, 1, "Societal Utility");
        }
        
        // Future example: 
        // else if (type == AxisVariable.SocietalUtility)
        // {
        //      return new AxisRange(-3, 3, "Income Change (£)"); 
        // }

        else
        {
            return new AxisRange(0, 1, "Unknown");
        }
    }
}