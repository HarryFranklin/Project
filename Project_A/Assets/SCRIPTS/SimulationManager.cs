using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // REQUIRED for GridLayoutGroup
using TMPro;

public class SimulationManager : MonoBehaviour {
    
    [Header("Dependencies")]
    public DataReader dataReader; 

    [Header("Visual Setup")]
    public GameObject personPrefab; 
    public Transform container; 
    public TextMeshProUGUI infoText;

    [Header("Layout Configuration")]
    public int margin = 25;      // Margin around the edges (pixels)
    public float spacing = 5f;   // Spacing between dots (pixels)

    private Dictionary<int, Respondent> _respondents;
    private bool _isShowingStats = false; 

    void Start() 
    {
        if (dataReader == null) {
            Debug.LogError("No DataReader assigned!");
            return;
        }

        _respondents = dataReader.GetRespondents();
        
        // Wait 0.1s for Unity's UI system to initialise the RectTransform dimensions
        Invoke(nameof(VisualiseRespondents), 0.1f);
    }

    void VisualiseRespondents() {
        // 1. Clear old objects
        foreach (Transform child in container) Destroy(child.gameObject);

        if (_respondents.Count == 0) return;

        // 2. Get UI Components
        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        RectTransform rect = container.GetComponent<RectTransform>();

        if (grid == null || rect == null) 
        {
            Debug.LogError("Container needs a RectTransform and a GridLayoutGroup!");
            return;
        }

        // A. Define usable space
        grid.padding = new RectOffset(margin, margin, margin, margin);
        grid.spacing = new Vector2(spacing, spacing);

        float usableWidth = rect.rect.width - (margin * 2);
        float usableHeight = rect.rect.height - (margin * 2);

        // B. Calculate best Row/Column distribution
        // We want the grid aspect ratio to match the container aspect ratio
        float count = _respondents.Count;
        float containerRatio = usableWidth / usableHeight;

        // Calculate ideal columns based on ratio
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count * containerRatio));
        // Calculate resulting rows needed to hold everyone
        int rows = Mathf.CeilToInt(count / cols);

        // C. Calculate the maximum size a cell can be to fit width-wise
        // Available Width = (Cols * Size) + ((Cols - 1) * Spacing)
        // Therefore: Size = (Width - ((Cols - 1) * Spacing)) / Cols
        float maxCellWidth = (usableWidth - ((cols - 1) * spacing)) / cols;

        // D. Calculate the maximum size a cell can be to fit height-wise
        float maxCellHeight = (usableHeight - ((rows - 1) * spacing)) / rows;

        // E. Pick the smaller dimension (so it fits in both width and height)
        float finalSize = Mathf.Min(maxCellWidth, maxCellHeight);

        // F. Apply Settings
        grid.cellSize = new Vector2(finalSize, finalSize);
        
        // Force the Grid to use exactly this many columns
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;

        // -----------------------------------

        // 3. Spawn the objects
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

        // 4. Force UI Update
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    // --- INTERACTION LOGIC ---

    private void OnPersonHover(Respondent r) 
    {
        infoText.text = $"<b>Respondent {r.id}</b>\nPersonal U4: {r.personalUtilities[2]:F2}\nSocietal U4: {r.societalUtilities[2]:F2}";
    }

    private void OnPersonExit() 
    {
        if (_isShowingStats) DisplayGroupStats();
        else infoText.text = "Hover over a person to see details.";
    }

    private void OnPersonClick(Respondent r) 
    {
        Debug.Log($"Clicked {r.id}");
    }

    public void ToggleGroupStats() 
    {
        _isShowingStats = !_isShowingStats;
        if (_isShowingStats) DisplayGroupStats();
        else infoText.text = "Hover over a person to see details.";
    }

    private void DisplayGroupStats() 
    {
        if (_respondents == null || _respondents.Count == 0) return;
        float avgPersonal = 0, avgSocietal = 0;
        foreach(var r in _respondents.Values) {
            avgPersonal += r.personalUtilities[2]; 
            avgSocietal += r.societalUtilities[2];
        }
        
        string msg = $"<b>Group Analysis ({_respondents.Count})</b>\n";
        msg += $"Avg Personal U4: {(avgPersonal/_respondents.Count):F2}\n";
        msg += $"Avg Societal U4: {(avgSocietal/_respondents.Count):F2}";
        infoText.text = msg;
    }
}