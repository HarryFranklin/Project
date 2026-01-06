using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimulationManager : MonoBehaviour {
    [Header("Data Files")]
    public TextAsset personalFile;
    public TextAsset societalFile;

    [Header("Simulation Settings")]
    public GameObject personPrefab; 
    public Transform container;

    [Header("UI References")]
    public TextMeshProUGUI infoText;

    private Dictionary<int, Respondent> _respondents = new Dictionary<int, Respondent>();
    private bool _isShowingStats = false; 

    void Start() {
        LoadData();
        
        // Wait a tiny bit to let the UI system calculate the scree nsize before doing our maths.
        Invoke(nameof(VisualiseRespondents), 0.1f);
    }

    void LoadData() {
        _respondents.Clear();

        // 1. Parse Personal Utilities
        if (personalFile != null) {
            string[] lines = personalFile.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cols = lines[i].Split(',');

                // Format: ID, Death, U2, U4, U6, U8, U10
                if (int.TryParse(cols[0], out int id)) {
                    Respondent r = new Respondent(id);
                    r.personalUtilities[0] = float.Parse(cols[1]); // Death
                    r.personalUtilities[1] = float.Parse(cols[2]); // U2
                    r.personalUtilities[2] = float.Parse(cols[3]); // U4
                    r.personalUtilities[3] = float.Parse(cols[4]); // U6
                    r.personalUtilities[4] = float.Parse(cols[5]); // U8
                    r.personalUtilities[5] = float.Parse(cols[6]); // U10
                    _respondents[id] = r;
                }
            }
        }

        // 2. Parse Societal Utilities
        if (societalFile != null) {
            string[] lines = societalFile.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cols = lines[i].Split(',');

                if (int.TryParse(cols[0], out int id)) {
                    if (_respondents.ContainsKey(id)) {
                        Respondent r = _respondents[id];
                        r.societalUtilities[0] = float.Parse(cols[1]);
                        r.societalUtilities[1] = float.Parse(cols[2]);
                        r.societalUtilities[2] = float.Parse(cols[3]);
                        r.societalUtilities[3] = float.Parse(cols[4]);
                        r.societalUtilities[4] = float.Parse(cols[5]);
                        r.societalUtilities[5] = float.Parse(cols[6]);
                    }
                }
            }
        }

        Debug.Log($"Loaded {_respondents.Count} respondents.");
    }

    void VisualiseRespondents() {
        // 1. Clean up old objects
        foreach (Transform child in container) Destroy(child.gameObject);

        // 2. Get UI Components
        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        RectTransform rect = container.GetComponent<RectTransform>();

        if (grid != null && rect != null && _respondents.Count > 0) {
            
            float width = rect.rect.width;
            float height = rect.rect.height;
            
            // Calculate total area available
            float totalArea = width * height;
            
            // Calculate area per person
            float areaPerPerson = totalArea / _respondents.Count;
            
            // Calculate side length (Square Root of Area)
            // Subtract spacing so they don't touch
            float spacing = grid.spacing.x;
            float sideLength = Mathf.Sqrt(areaPerPerson) - spacing;

            // Apply the new size to the grid cells
            // We clamp it to a minimum of 5px so they don't vanish if the screen is tiny
            sideLength = Mathf.Max(sideLength, 5f);
            grid.cellSize = new Vector2(sideLength, sideLength);
            // -------------------------
        }

        // 3. Spawn the dots
        foreach (var kvp in _respondents) {
            GameObject go = Instantiate(personPrefab, container);
            go.name = $"Respondent_{kvp.Value.id}";
            
            RespondentVisual visual = go.GetComponent<RespondentVisual>();
            if(visual != null) visual.Initialise(kvp.Value, this);
        }
    }

    // --- INTERACTION LOGIC ---

    public void ShowRespondentInfo(Respondent r) {
        // Always show individual info on hover
        string msg = $"<b>Respondent {r.id}</b>\n";
        msg += $"Personal U4: {r.personalUtilities[2]:F2}\n";
        msg += $"Societal U4: {r.societalUtilities[2]:F2}";
        infoText.text = msg;
    }

    public void ClearInfo() {
        // When unhovering:
        // If stats mode is ON, show stats.
        // If stats mode is OFF, show "Hover" prompt.
        if (_isShowingStats) {
            DisplayGroupStats();
        } else {
            infoText.text = "Hover over a person to see details.";
        }
    }

    public void ToggleGroupStats() {
        _isShowingStats = !_isShowingStats;

        if (_isShowingStats) {
            DisplayGroupStats();
        } else {
            infoText.text = "Hover over a person to see details.";
        }
    }

    private void DisplayGroupStats() {
        if (_respondents.Count == 0) return;

        float avgPersonal = 0;
        float avgSocietal = 0;

        foreach(var r in _respondents.Values) {
            avgPersonal += r.personalUtilities[2]; // U4
            avgSocietal += r.societalUtilities[2]; // U4
        }

        avgPersonal /= _respondents.Count;
        avgSocietal /= _respondents.Count;

        string msg = $"<b>Group Analysis ({_respondents.Count})</b>\n";
        msg += $"Avg Personal U4: {avgPersonal:F2}\n";
        msg += $"Avg Societal U4: {avgSocietal:F2}\n\n";
        msg += "<i>(Hover over a person to inspect them temporarily)</i>";
        
        infoText.text = msg;
    }
}