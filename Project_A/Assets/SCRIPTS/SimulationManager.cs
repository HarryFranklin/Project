using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; 
using TMPro;

public class SimulationManager : MonoBehaviour 
{
    public enum ColourMode 
    {
        InequalityAversion, // Focuses on the gap between Self and Society
        TotalUtility        // Focuses on overall Life Satisfaction
    }

    [Header("Dependencies")]
    public DataReader dataReader; 
    public GameObject personPrefab; 
    public Transform container;     
    
    [Header("UI Output")]
    public TextMeshProUGUI infoText; // Hover details go here
    public TextMeshProUGUI modeText; // The Legend/Mode explanation goes here

    [Header("Configuration")]
    public ColourMode currentMode = ColourMode.InequalityAversion; // Default launch mode
    public int margin = 25;
    public float spacing = 5f;

    private Dictionary<int, Respondent> _respondents;
    private bool _isShowingStats = false; 

    void Start() 
    {
        if (dataReader != null) _respondents = dataReader.GetRespondents();
        
        // This will build the grid AND set the default text automatically
        Invoke(nameof(VisualiseRespondents), 0.1f);
    }

    // --- VISUALISATION ---
    void VisualiseRespondents() 
    {    
        // 1. Clear old objects
        foreach (Transform child in container) Destroy(child.gameObject);

        if (_respondents == null || _respondents.Count == 0) return;

        // 2. Setup Grid & Rect logic
        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        RectTransform rect = container.GetComponent<RectTransform>();

        if (grid == null || rect == null) return;

        grid.padding = new RectOffset(margin, margin, margin, margin);
        grid.spacing = new Vector2(spacing, spacing);

        float usableWidth = rect.rect.width - (margin * 2);
        float usableHeight = rect.rect.height - (margin * 2);

        float count = _respondents.Count;
        float screenRatio = usableWidth / usableHeight;

        int cols = Mathf.CeilToInt(Mathf.Sqrt(count * screenRatio));
        int rows = Mathf.CeilToInt(count / cols);

        float maxCellWidth = (usableWidth - ((cols - 1) * spacing)) / cols;
        float maxCellHeight = (usableHeight - ((rows - 1) * spacing)) / rows;
        float finalSize = Mathf.Min(maxCellWidth, maxCellHeight);
        finalSize = Mathf.Clamp(finalSize, 5f, 60f); 

        grid.cellSize = new Vector2(finalSize, finalSize);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;
        grid.childAlignment = TextAnchor.MiddleCenter;

        // 3. Spawn Objects
        foreach (var kvp in _respondents) 
        {
            GameObject go = Instantiate(personPrefab, container);
            go.name = $"Respondent_{kvp.Value.id}";
            
            RespondentVisual visual = go.GetComponent<RespondentVisual>();
            if (visual != null) 
            {
                visual.Initialise(kvp.Value, OnPersonHover, OnPersonExit, OnPersonClick);
            }
        }
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        // 4. APPLY COLORS & TEXT IMMEDIATELY
        ApplyColourMode();
    }

    public void ToggleColourMode() 
    {
        if (currentMode == ColourMode.InequalityAversion) currentMode = ColourMode.TotalUtility;
        else currentMode = ColourMode.InequalityAversion;

        ApplyColourMode();
    }

    void ApplyColourMode() 
    {
        if (_respondents == null || _respondents.Count == 0) return;

        // A. FIND RANGE
        float minVal = float.MaxValue;
        float maxVal = float.MinValue;

        foreach (var r in _respondents.Values) {
            float val = 0;
            if (currentMode == ColourMode.InequalityAversion) 
            {
                val = r.societalUtilities[2] - r.personalUtilities[2];
            } else 
            {
                foreach(float u in r.personalUtilities) val += u;
            }

            if (val < minVal) minVal = val;
            if (val > maxVal) maxVal = val;
        }

        if (Mathf.Approximately(minVal, maxVal)) maxVal = minVal + 1f;

        // B. UPDATE THE MODE TEXT
        if (modeText != null) {
            if (currentMode == ColourMode.InequalityAversion) 
            {
                modeText.text = $"<b>Mode: Inequality Aversion</b>\n<size=90%>Valuation Gap: {minVal:F2} to {maxVal:F2}</size>\n\n" +
                                "<i>Personal Risk vs. Societal Fairness</i>\n\n" +
                                "<color=#0099FF><b>Blue (Guardian)</b></color>: \nPrioritises <b>Societal Equality</b>. " +
                                "Willing to sacrifice efficiency to help the poorest.\n" +
                                "<color=#FF6600><b>Orange (Survivor)</b></color>: \nPrioritises <b>Personal Safety</b>. " +
                                "More tolerant of societal inequality than personal risk.";
            } 
            else 
            {
                modeText.text = $"<b>Mode: Total Utility</b>\n<size=90%>Utility Sum: {minVal:F1} to {maxVal:F1}</size>\n\n" +
                                "<i>Overall Life Satisfaction & Optimism</i>\n\n" +
                                "<color=yellow><b>Yellow (Optimist)</b></color>: \n<b>High Satisfaction</b>. " +
                                "Derives high value from most life outcomes.\n" +
                                "<color=#4B0082><b>Purple (Pessimist)</b></color>: \n<b>Low Satisfaction</b>. " +
                                "Derives value only from near-perfect outcomes.";
            }
        }

        // C. PAINT THE DOTS
        foreach (Transform child in container) 
        {
            RespondentVisual visual = child.GetComponent<RespondentVisual>();
            if (visual == null) continue;

            Color targetColour = Color.white;
            Respondent r = visual.data;
            float currentVal = 0;

            if (currentMode == ColourMode.InequalityAversion) 
            {
                currentVal = r.societalUtilities[2] - r.personalUtilities[2];
                float t = Mathf.InverseLerp(minVal, maxVal, currentVal);
                targetColour = Color.Lerp(new Color(1f, 0.4f, 0f), new Color(0f, 0.6f, 1f), t);
            }
            else 
            {
                foreach(float u in r.personalUtilities) currentVal += u;
                float t = Mathf.InverseLerp(minVal, maxVal, currentVal);
                targetColour = Color.Lerp(new Color(0.2f, 0f, 0.4f), Color.yellow, t);
            }

            visual.SetColour(targetColour);
        }
    }

    // --- INTERACTION ---
    private void OnPersonHover(Respondent r) 
    {
        if (infoText != null) 
        {
            infoText.text = $"<b>Respondent {r.id}</b>\nPersonal U4: {r.personalUtilities[2]:F2}\nSocietal U4: {r.societalUtilities[2]:F2}";
        }
    }

    private void OnPersonExit() 
    {
        if (infoText != null) 
        {
            if (_isShowingStats) DisplayGroupStats();
            else infoText.text = "Hover over a person to see details.";
        }
    }

    private void OnPersonClick(Respondent r) 
    {
        Debug.Log($"Clicked {r.id}");
    }

    public void ToggleGroupStats() 
    {
        _isShowingStats = !_isShowingStats;
        if (_isShowingStats) DisplayGroupStats();
        else if (infoText != null) infoText.text = "Hover over a person to see details.";
    }

    private void DisplayGroupStats() 
    {
        if (_respondents == null || _respondents.Count == 0 || infoText == null) return;
        
        float avgPersonal = 0, avgSocietal = 0;
        foreach(var r in _respondents.Values) 
        {
            avgPersonal += r.personalUtilities[2]; 
            avgSocietal += r.societalUtilities[2];
        }
        
        infoText.text = $"<b>Group Avg ({_respondents.Count})</b>\n" +
                        $"Avg Personal U4: {(avgPersonal/_respondents.Count):F2}\n" +
                        $"Avg Societal U4: {(avgSocietal/_respondents.Count):F2}";
    }
}