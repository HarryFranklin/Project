using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SimulationManager : MonoBehaviour
{
    [Header("Data")]
    public DataReader dataReader; 
    public List<Policy> policies; 

    [Header("Panels (Drag GameObjects here)")]
    public GameObject policyInfoPanel;
    public GameObject comparisonPanel;

    [Header("Policy Info Tab References")]
    public TMP_Text policyTitleText;
    public TMP_Text policyDescText;
    public TMP_Text policyStatsText;

    [Header("Comparison Tab References")]
    public TMP_Text comparisonSubtitleText; 
    public TMP_Text comparisonBodyText; 

    [Header("Visual Configuration")]
    public Transform container;
    public GameObject personPrefab; 
    public Sprite faceHappy, faceNeutral, faceSad;

    private Dictionary<int, Respondent> respondents;
    private List<RespondentVisual> respondentList = new List<RespondentVisual>();
    private List<Respondent> _cachedPopulationList;
    private int policyIndex = -1; // Default state

    // Cache current results
    private int[] _currentPopulationLS;
    private int[] _baselineLS;


    void Start()
    {
        // Get the CSV data
        respondents = dataReader.GetRespondents();
        // Get the normalised and [0] and [1] swapped ONS data
        float[] ONSDistribution = WelfareMetrics.GetBaselineDistribution();

        // 1. Assign data
        foreach (var r in respondents.Values)
        {
            r.currentLS = WelfareMetrics.GetWeightedRandomLS(ONSDistribution);
        }

        // 2. Cache order
        _cachedPopulationList = new List<Respondent>(respondents.Values);

        // 3. Spawn visuals
        foreach (var singleRespondent in _cachedPopulationList)
        {
            GameObject respondent = Instantiate(personPrefab, container);
            RespondentVisual respondentVisual = respondent.GetComponent<RespondentVisual>();
            respondentVisual.Initialise(singleRespondent, this);
            respondentList.Add(respondentVisual);
        }

        // 4. Default view
        ShowPolicyInfoTab(); // Start on the main info tab
        UpdateSimulation();
    }

    // --- BUTTON FUNCTIONS ---
    public void ShowPolicyInfoTab()
    {
        policyInfoPanel.SetActive(true);
        comparisonPanel.SetActive(false);
    }

    public void ShowComparisonTab()
    {
        policyInfoPanel.SetActive(false);
        comparisonPanel.SetActive(true);
    }

    public void NextPolicy()
    {
        if (policies.Count == 0) 
        {
            return;
        }

        policyIndex = (policyIndex + 1) % policies.Count;
        UpdateSimulation();
    }

    void UpdateSimulation()
    {
        // Re-grab cached population data
        Respondent[] population = _cachedPopulationList.ToArray();
        
        // New arrays as to not overwrite old data
        _baselineLS = new int[population.Length];
        _currentPopulationLS = new int[population.Length];

        // For each person, apply their default LS to them
        for (int i=0; i < population.Length; i++) 
        {
            _baselineLS[i] = population[i].currentLS;
        }

        // Apply Policy
        if (policyIndex == -1) // if first policy cycle
        {
            System.Array.Copy(_baselineLS, _currentPopulationLS, _baselineLS.Length);
            
            // Update both tabs so they are ready when clicked
            UpdateUI_Default();
            UpdateScoreUI_Default();
        }
        else // already clicked through it once
        {
            // Don't just use the baseline information, run it through ApplyPolicy
            _currentPopulationLS = policies[policyIndex].ApplyPolicy(population);
            
            // Update both tabs
            UpdateUI_Policy(policies[policyIndex]);            
        }

        // Calculate totals & visuals
        double totalBaseSocial = 0;
        double totalCurrSocial = 0;
        double totalBasePersonal = 0;
        double totalCurrPersonal = 0;
        int happyCount = 0;

        for (int i = 0; i < population.Length; i++)
        {
            Respondent r = population[i];

            // Current personal utility - get their utility given their current LS
            float uSelf = WelfareMetrics.GetUtilityForPerson(_currentPopulationLS[i], r.personalUtilities);
            // Baseline personal utility - value before any changes, given their default LS
            float uSelfBase = WelfareMetrics.GetUtilityForPerson(_baselineLS[i], r.personalUtilities);
            // Baseline societal utility - how did they judge the fairness of society in the default scenario
            float uOthersOld = WelfareMetrics.EvaluateDistribution(_baselineLS, r.societalUtilities);
            // Current societal utility - how do they judge the fairness of society in the current scenario
            float uOthersNew = WelfareMetrics.EvaluateDistribution(_currentPopulationLS, r.societalUtilities);

            // Add the above values to the total to calculate initial and current societal fairness index, blah
            totalBaseSocial += uOthersOld;
            totalCurrSocial += uOthersNew;
            totalBasePersonal += uSelfBase;
            totalCurrPersonal += uSelf;

            // Face logic - Do they think society is more or less fair than before?
            // Neutral by default
            Sprite face = faceNeutral;
            if (uOthersNew > uOthersOld + 0.01f) 
            {  
                face = faceHappy;
                happyCount++; // Increment # of happy people for the eventual % calculation for approval rating
            }
            else if (uOthersNew < uOthersOld - 0.01f) 
            {
                face = faceSad; 
            }

            // For this person, update their face and pass their uSelf for the y-axis calculation
            respondentList[i].UpdateVisuals(uSelf, face);
        }

        // Update comparison text if not in default mode
        if (policyIndex != -1)
        {
            UpdateScoreUI(policies[policyIndex].policyName, totalBaseSocial, totalCurrSocial, totalBasePersonal, totalCurrPersonal, happyCount, population.Length);
        }
    }

    // --- UI LOGIC ---
    void UpdateScoreUI(string policyName, double baseSoc, double currSoc, double basePers, double currPers, int happyCount, int totalPop)
    {
        // 1. Title Logic
        if (comparisonSubtitleText) comparisonSubtitleText.text = $"Default vs. {policyName}";

        // 2. Body Logic
        if (comparisonBodyText)
        {
            float avgSocBase = (float)(baseSoc / totalPop);
            float avgSocCurr = (float)(currSoc / totalPop);
            float diffSoc = avgSocCurr - avgSocBase;

            float avgPersBase = (float)(basePers / totalPop);
            float avgPersCurr = (float)(currPers / totalPop);
            float diffPers = avgPersCurr - avgPersBase;

            // Red or green based on value
            string ColourDiff(float val)
            {
                string s = val.ToString("F3");
                if (val > 0.001f) return $"<color=green>+{s}</color>";
                if (val < -0.001f) return $"<color=red>{s}</color>";
                return s;
            }

            string text = "";

            // Approval rating for given policy = # of people who're happy as a % of total pop
            float approval = (float)happyCount / totalPop * 100f;
            text += $"<b>Public Approval:</b> {approval:F1}%\n\n";

            text += "<b>Societal Fairness Index:</b>\n";
            text += $"Current: {avgSocCurr:F3} ({ColourDiff(diffSoc)})\n";
            text += $"<size=80%>Default: {avgSocBase:F3}</size>\n\n";

            text += "<b>Avg Personal Wellbeing:</b>\n";
            text += $"Current: {avgPersCurr:F3} ({ColourDiff(diffPers)})\n";
            text += $"<size=80%>Default: {avgPersBase:F3}</size>";

            comparisonBodyText.text = text;
        }
    }

    void UpdateScoreUI_Default()
    {
        if (comparisonSubtitleText) comparisonSubtitleText.text = "Default vs. Default";
        if (comparisonBodyText) comparisonBodyText.text = "No Comparison Available.";
    }

    void UpdateUI_Default()
    {
        if (policyTitleText) policyTitleText.text = "<b>Default (2022-2023)</b>";
        if (policyDescText) policyDescText.text = "The current distribution of life satisfaction in the UK based on ONS data.";
        if (policyStatsText) policyStatsText.text = "<b>Base:</b> ONS 2022-2023 Data\n<b>Impact:</b> None";
    }

    void UpdateUI_Policy(Policy p)
    {
        if (policyTitleText) policyTitleText.text = p.policyName;
        if (policyDescText) policyDescText.text = p.description;

        if (policyStatsText)
        {
            // Red or green based on value
            string FormatChange(int val)
            {
                if (val > 0) return $"<color=green>+{val}</color>";
                if (val < 0) return $"<color=red>{val}</color>";
                return "0";
            }

            string stats = "";
            stats += $"<b>Rich (Tiers {p.richThreshold}-10):</b> {FormatChange(p.changeForRich)}\n";
            stats += $"<b>Middle Class:</b> {FormatChange(p.changeForMiddle)}\n";
            stats += $"<b>Poor (Tiers 0-{p.poorThreshold}):</b> {FormatChange(p.changeForPoor)}";
            policyStatsText.text = stats;
        }
    }

    // --- HOVER LOGIC ---
    public void OnHoverEnter(Respondent r)
    {
        if (r == null || respondents == null || _currentPopulationLS == null) return;

        int index = 0;
        int currentLS = 0;
        bool found = false;

        foreach(var kvp in respondents)
        {
            if (kvp.Value == r) 
            {
                if(index < _currentPopulationLS.Length)
                {
                    currentLS = _currentPopulationLS[index];
                    found = true;
                }
                break;
            }
            index++;
        }

        if (!found) return;

        float uSelf = WelfareMetrics.GetUtilityForPerson(currentLS, r.personalUtilities);
        float uSociety = WelfareMetrics.EvaluateDistribution(_currentPopulationLS, r.societalUtilities);

        string info = $"<size=120%><b>Respondent #{r.id}</b></size>\n";
        info += $"<b>Life Satisfaction:</b> {currentLS}/10\n";
        info += $"<b>Personal Utility:</b> {uSelf:F2}\n";
        info += $"<b>Societal Utility:</b> {uSociety:F2}\n";
        
        string type = uSelf > uSociety ? "<color=red>Self-Interested</color>" : "<color=green>Altruistic</color>";
        info += $"<b>Type:</b> {type}";

        // Show hover info in both panels' body text areas so it's visible regardless of tab
        if(policyStatsText && policyInfoPanel.activeSelf) policyStatsText.text = info;
        if(comparisonBodyText && comparisonPanel.activeSelf) comparisonBodyText.text = info;
    }

    public void OnHoverExit()
    {
        if (policyIndex == -1) 
        {
            UpdateUI_Default();
            UpdateScoreUI_Default();
        }
        else 
        {
            UpdateUI_Policy(policies[policyIndex]);

            UpdateSimulation(); 
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