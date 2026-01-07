using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; 
using TMPro;

public class SimulationManager : MonoBehaviour 
{
    public enum ColourMode 
    {
        InequalityAversion, 
        TotalUtility,       
        PolicyHappiness     
    }

    public enum TierDistribution 
    {
        UniformRandom, 
        RealisticUK    
    }

    [Header("Dependencies")]
    public DataReader dataReader; 
    public GameObject personPrefab; 
    public Transform container;     
    
    [Header("UI Output")]
    public TextMeshProUGUI infoText; 
    public TextMeshProUGUI modeText; 

    [Header("Configuration")]
    public ColourMode currentMode = ColourMode.InequalityAversion; 
    public TierDistribution distributionMode = TierDistribution.UniformRandom;
    public int margin = 25;
    public float spacing = 5f;

    [Header("Policies")]
    public List<Policy> availablePolicies; 
    private int _policyIndex = 0;
    private Policy _activePolicy; // Internal tracker

    // Helper for update loop
    private int _lastTax;
    private int _lastGain;

    private Dictionary<int, Respondent> _respondents;

    private bool _isShowingStats = false;

    void Start() 
    {
        if (dataReader != null) _respondents = dataReader.GetRespondents();
        
        DistributeWealth();
        Invoke(nameof(VisualiseRespondents), 0.1f);
    }

    // --- LIVE WATCHER LOOP ---
    void Update() 
    {
        // Watch for live tweaks in the Inspector
        if (_activePolicy != null) 
        {
            if (_activePolicy.taxSeverity != _lastTax || _activePolicy.socialGain != _lastGain) 
            {
                _lastTax = _activePolicy.taxSeverity;
                _lastGain = _activePolicy.socialGain;
                ApplyColourMode();
            }
        }
    }

    // --- BUTTON 1: TOGGLE DATA VIEW ---
    // Switches only between the two "Analysis" modes.
    public void ToggleDataView() 
    {
        if (currentMode == ColourMode.InequalityAversion) 
        {
            currentMode = ColourMode.TotalUtility;
        }
        else 
        {
            currentMode = ColourMode.InequalityAversion;
        }
        ApplyColourMode();
    }

    // --- BUTTON 2: ENTER SIMULATION ---
    // Jumps directly to the Policy Happiness mode.
    public void EnablePolicyView() 
    {
        currentMode = ColourMode.PolicyHappiness;
        ApplyColourMode();
    }

    // --- BUTTON 3: NEXT POLICY ---
    // Cycles through the list of ScriptableObjects you dragged in.
    public void CycleNextPolicy() 
    {
        if (availablePolicies == null || availablePolicies.Count == 0) return;

        _policyIndex++;
        if (_policyIndex >= availablePolicies.Count) _policyIndex = 0;

        _activePolicy = availablePolicies[_policyIndex];
        
        // If we aren't in policy mode, switch to it so the user sees the change
        if (currentMode != ColourMode.PolicyHappiness) 
        {
            currentMode = ColourMode.PolicyHappiness;
        }

        ApplyColourMode();
    }

    // --- BUTTON 4: REROLL POPULATION ---
    public void TogglePopulation() 
    {
        if (distributionMode == TierDistribution.UniformRandom) distributionMode = TierDistribution.RealisticUK;
        else distributionMode = TierDistribution.UniformRandom;

        DistributeWealth();
        ApplyColourMode();
    }

    void DistributeWealth() 
    {
        if (_respondents == null) return;

        foreach(var r in _respondents.Values) 
        {
            // --- OPTION A: RANDOM (GAME BALANCE) ---
            if (distributionMode == TierDistribution.UniformRandom) 
            {
                // Gives an equal chance of being any tier from 1 (Poor) to 5 (Rich).
                // Useful for testing to ensure you have enough data points for every reaction.
                r.currentTier = Random.Range(1, 6); 
            }
            // --- OPTION B: UK DATA (REALISM) ---
            else 
            {
                // This simulates the actual UK wealth spread mentioned in the paper 
                // (Most people are okay, a minority are struggling).
                float dice = Random.value; // Returns a number between 0.0 and 1.0

                if (dice > 0.94f) r.currentTier = 1;      // Top 6% of rolls = Destitute (State E)
                else if (dice > 0.60f) r.currentTier = 3; // Next 34% = Coping (State C)
                else r.currentTier = 5;                   // Bottom 60% = Thriving (State A)
            }
        }
    }
    
    void VisualiseRespondents() 
    {    
        foreach (Transform child in container) Destroy(child.gameObject);

        if (_respondents == null || _respondents.Count == 0) return;

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
        ApplyColourMode();
    }

    void ApplyColourMode() 
    {
        if (_respondents == null || _respondents.Count == 0) return;

        // 1. Recalculate Policy Impact
        // Because Update() calls this, dragging the slider in the inspector 
        // will immediately re-run this math for everyone.
        if (_activePolicy != null) 
        {
            foreach (var r in _respondents.Values) 
            {
                r.happinessWithPolicy = _activePolicy.CalculateImpact(r);
            }
        }

        // 2. FIND RANGE
        float minVal = float.MaxValue;
        float maxVal = float.MinValue;

        foreach (var r in _respondents.Values) 
        {
            float val = 0;
            if (currentMode == ColourMode.InequalityAversion) 
            {
                val = r.societalUtilities[2] - r.personalUtilities[2];
            } 
            else if (currentMode == ColourMode.TotalUtility)
            {
                foreach(float u in r.personalUtilities) val += u;
            }

            if (val < minVal) minVal = val;
            if (val > maxVal) maxVal = val;
        }

        if (Mathf.Approximately(minVal, maxVal)) maxVal = minVal + 1f;

        // 3. UPDATE THE MODE TEXT
        if (modeText != null) 
        {
            if (currentMode == ColourMode.InequalityAversion) 
            {
                modeText.text = $"<b>Mode: Inequality Aversion</b>\n<size=90%>Valuation Gap: {minVal:F2} to {maxVal:F2}</size>\n\n" +
                                "<i>Personal Risk vs. Societal Fairness</i>\n\n" +
                                "<color=#0099FF><b>Blue (Guardian)</b></color>: \nPrioritises <b>Societal Equality</b>. " +
                                "Willing to sacrifice efficiency to help the poorest.\n" +
                                "<color=#FF6600><b>Orange (Survivor)</b></color>: \nPrioritises <b>Personal Safety</b>. " +
                                "More tolerant of societal inequality than personal risk.";
            } 
            else if (currentMode == ColourMode.TotalUtility)
            {
                modeText.text = $"<b>Mode: Total Utility</b>\n<size=90%>Utility Sum: {minVal:F1} to {maxVal:F1}</size>\n\n" +
                                "<i>Overall Life Satisfaction & Optimism</i>\n\n" +
                                "<color=yellow><b>Yellow (Optimist)</b></color>: \n<b>High Satisfaction</b>. " +
                                "Derives high value from most life outcomes.\n" +
                                "<color=#4B0082><b>Purple (Pessimist)</b></color>: \n<b>Low Satisfaction</b>. " +
                                "Derives value only from near-perfect outcomes.";
            }
            else if (currentMode == ColourMode.PolicyHappiness)
            {
                string pName = _activePolicy != null ? _activePolicy.policyName : "None";
                string distName = distributionMode == TierDistribution.UniformRandom ? "Uniform" : "UK Realistic";
                
                // Show dynamic values in the text so you can confirm they are updating
                int tax = _activePolicy != null ? _activePolicy.taxSeverity : 0;
                int gain = _activePolicy != null ? _activePolicy.socialGain : 0;

                modeText.text = $"<b>Mode: Policy Impact</b>\n" +
                                $"<size=90%>Policy: {pName} | \nPopulation: {distName}</size>\n" +
                                $"<size=80%>(Tax: {tax} | Gain: {gain})</size>\n\n" +
                                "<i>Who supports this policy?</i>\n\n" +
                                "<color=green><b>Green (Supporter)</b></color>: \nNet Happiness Increase.\n" +
                                "<color=red><b>Red (Opponent)</b></color>: \nNet Happiness Decrease.";
            }
        }

        // 4. PAINT THE DOTS
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
            else if (currentMode == ColourMode.TotalUtility)
            {
                foreach(float u in r.personalUtilities) currentVal += u;
                float t = Mathf.InverseLerp(minVal, maxVal, currentVal);
                targetColour = Color.Lerp(new Color(0.2f, 0f, 0.4f), Color.yellow, t);
            }
            else if (currentMode == ColourMode.PolicyHappiness)
            {
                float t = Mathf.InverseLerp(-2f, 2f, r.happinessWithPolicy);
                targetColour = Color.Lerp(Color.red, Color.green, t);

                if (Mathf.Abs(r.happinessWithPolicy) < 0.05f) targetColour = Color.grey;
            }

            visual.SetColour(targetColour);
        }
    }

    private void OnPersonHover(Respondent r) 
    {
        if (infoText != null) 
        {
            string extraInfo = "";
            if (currentMode == ColourMode.PolicyHappiness) 
            {
                extraInfo = $"\nTier: {r.currentTier} | Impact: {r.happinessWithPolicy:F2}";
            }
            infoText.text = $"<b>Respondent {r.id}</b>\nPersonal U4: {r.personalUtilities[2]:F2}\nSocietal U4: {r.societalUtilities[2]:F2}{extraInfo}";
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