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
    public void UpdateAxisVisuals(AxisVariable xType, float xMin, float xMax, AxisVariable yType, float yMin, float yMax)
    {
        // 1. Get Labels
        AxisRange xInfo = GetDefaultSettings(xType);
        AxisRange yInfo = GetDefaultSettings(yType);

        // 2. Update Titles
        if (xAxisTitle) xAxisTitle.text = xInfo.label;
        if (yAxisTitle) yAxisTitle.text = yInfo.label;

        // 3. Update Min/Max Text
        if (xMinText) xMinText.text = FormatLabel(xMin);
        if (xMaxText) xMaxText.text = FormatLabel(xMax);
        if (yMinText) yMinText.text = FormatLabel(yMin);
        if (yMaxText) yMaxText.text = (yType == AxisVariable.Stack) ? yMax.ToString("0") : FormatLabel(yMax);

        // 4. Move Axes Lines (Zero lines)
        // We need to know where "0" is within the dynamic ranges
        float xZeroNorm = Mathf.InverseLerp(xMin, xMax, 0f); 
        float yZeroNorm = Mathf.InverseLerp(yMin, yMax, 0f);

        // If Stack, the zero line is at 0
        if (yType == AxisVariable.Stack) yZeroNorm = 0f;

        Vector2 originPixel = grid.GetPlotPosition(xZeroNorm, yZeroNorm, 0); 

        if (xAxisLine) xAxisLine.anchoredPosition = new Vector2(0, originPixel.y);
        if (yAxisLine) yAxisLine.anchoredPosition = new Vector2(originPixel.x, 0);
    }

    private string FormatLabel(float val)
    {
        // If small delta, show more decimal places
        if (Mathf.Abs(val) < 1f && val != 0) return val.ToString("0.###");
        return val.ToString("0.#");
    }

    public AxisRange GetDefaultSettings(AxisVariable type)
    {
        switch (type)
        {
            case AxisVariable.LifeSatisfaction: return new AxisRange(0, 10, "Life Satisfaction");
            case AxisVariable.PersonalUtility: return new AxisRange(0, 1, "Personal Utility");
            case AxisVariable.SocietalFairness: return new AxisRange(0, 1, "Societal Fairness");
            case AxisVariable.Wealth: return new AxisRange(0, 10, "Wealth Tier");
            
            // For deltas, return dummy 0-0 ranges because they're calculated dynamically
            case AxisVariable.DeltaPersonalUtility: return new AxisRange(-1, 1, "Δ Personal Utility");
            case AxisVariable.DeltaSocietalFairness: return new AxisRange(-1, 1, "Δ Societal Fairness");

            // Stack
            case AxisVariable.Stack: return new AxisRange(0, 0, "Count (People)"); // Min/Max dynamic
            
            default: return new AxisRange(0, 1, "Unknown");
        }
    }
}