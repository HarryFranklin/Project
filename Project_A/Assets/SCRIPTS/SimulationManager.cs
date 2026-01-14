using System.Collections.Generic;
using UnityEngine;
using TMPro;

// What can we plot?
public enum AxisVariable
{
    LifeSatisfaction, // 0-10
    PersonalUtility, // 0-1 (uSelf)
    SocietalFairness, // 0-1 (uOthers) (evaldist())
    Wealth // 0-10 (For later, or not?)
}

public class SimulationManager : MonoBehaviour
{
    [Header("Plotting Configuration")]
    public GraphGrid graphGrid; // Panel object
    public AxisVariable xAxis = AxisVariable.LifeSatisfaction; // Default
    public AxisVariable yAxis = AxisVariable.SocietalFairness; // Default

    [Header("Data")]
    public DataReader dataReader; 
    public List<Policy> policies; 

    [Header("UI: Panels")]
    public GameObject policyInfoPanel; // The panel with Description/Stats
    public GameObject comparisonPanel; // The new panel with the comparison data

    [Header("UI: Policy Info Tab")]
    public TMP_Text policyTitleText;
    public TMP_Text policyDescText;
    public TMP_Text policyStatsText;

    [Header("UI: Comparison Tab")]
    public TMP_Text comparisonSubtitleText; // e.g. "Default vs. Wealth Tax"
    public TMP_Text comparisonBodyText;     // Other stats, fairness index etc.

    [Header("Choice Mode (Drag Policies Here)")]
    public Policy policyOptionA;
    public Policy policyOptionB;

    [Header("Visual Configuration")]
    public Transform container;
    public GameObject personPrefab; 
    public Sprite faceHappy, faceNeutral, faceSad, faceDead;

    // Internal data
    private Dictionary<int, Respondent> respondents;
    private List<RespondentVisual> respondentList = new List<RespondentVisual>();
    private List<Respondent> _cachedPopulationList; // Keeps order fixed
    private int policyIndex = -1; // -1 = default
    
    // Data Caches
    private int[] _currentPopulationLS;
    private int[] _baselineLS;

    // Track the currently active policy object so the UI reacts correctly
    private Policy _activePolicy = null; 

    void Start()
    {
        // 1. Load data
        respondents = dataReader.GetRespondents();
        float[] onsDist = WelfareMetrics.GetBaselineDistribution();

        // 2. Assign initial LS
        foreach (var r in respondents.Values)
        {
            r.currentLS = WelfareMetrics.GetWeightedRandomLS(onsDist);
        }

        // 3. Cache order (stop that bug)
        _cachedPopulationList = new List<Respondent>(respondents.Values);

        // 4. Spawn visuals
        foreach (var r in _cachedPopulationList)
        {
            GameObject respondent = Instantiate(personPrefab, container);
            RespondentVisual respondentVisual = respondent.GetComponent<RespondentVisual>();
            respondentVisual.Initialise(r, this);
            respondentList.Add(respondentVisual);
        }

        // 5. Initial state
        ShowPolicyInfoTab(); 
        UpdateSimulation();
    }

    // --- TAB SWITCHING UI ---

    public void ShowPolicyInfoTab()
    {
        if(policyInfoPanel) policyInfoPanel.SetActive(true);
        if(comparisonPanel) comparisonPanel.SetActive(false);
    }

    public void ShowComparisonTab()
    {
        if(policyInfoPanel) policyInfoPanel.SetActive(false);
        if(comparisonPanel) comparisonPanel.SetActive(true);
    }

    // --- POLICY CONTROL ---
    public void NextPolicy()
    {
        if (policies.Count == 0) return;
        policyIndex = (policyIndex + 1) % policies.Count;
        
        // This is the standard "Cycle" mode
        ApplyAndAnimate(policies[policyIndex]);
    }

    public void ResetToDefault()
    {
        policyIndex = -1;
        UpdateSimulation(); 
    }

    // --- CORE SIMULATION LOOP ---
    void UpdateSimulation()
    {
        Respondent[] population = _cachedPopulationList.ToArray();
        
        // Initialise arrays if null
        if (_baselineLS == null || _baselineLS.Length != population.Length)
        {
            _baselineLS = new int[population.Length];
            _currentPopulationLS = new int[population.Length];
            for(int i=0; i<population.Length; i++) _baselineLS[i] = population[i].currentLS;
        }

        if (policyIndex == -1)
        {
            // Reset to default
            System.Array.Copy(_baselineLS, _currentPopulationLS, _baselineLS.Length);
            _activePolicy = null; // No policy active

            UpdateUI_DefaultText();
            UpdateScoreUI_DefaultText();
            
            // Trigger visuals update
            CalculateAndRefreshVisuals();
        }
        else
        {
            // Apply current cycled policy
            ApplyAndAnimate(policies[policyIndex]);
        }
    }

    // Method to group together the appllying and animation of a policy
    public void ApplyAndAnimate(Policy p)
    {
        if (p == null) return;

        _activePolicy = p; // For hover exit logic

        Respondent[] population = _cachedPopulationList.ToArray();
        
        // 1. Calculate new LS scores
        _currentPopulationLS = p.ApplyPolicy(population);

        // 2. Update policy info panel
        UpdateUI_Policy(p);

        // 3. Calculate happiness and move dots
        CalculateAndRefreshVisuals();
    }

    // Helper to calculate totals and trigger RespondentVisual updates
    void CalculateAndRefreshVisuals()
    {
         Respondent[] population = _cachedPopulationList.ToArray();
         
         double totalBaseSocial = 0;
         double totalCurrSocial = 0;
         double totalBasePersonal = 0;
         double totalCurrPersonal = 0;
         int happyCount = 0;

         for (int i = 0; i < population.Length; i++)
         {
            // This person and their current and default/base LS
            Respondent r = population[i];
            int currentLS = _currentPopulationLS[i];
            int baselineLS = _baselineLS[i];

            // 1. Calculate metrics
            
            // Given current LS...
            float uSelfCurrent = WelfareMetrics.GetUtilityForPerson(currentLS, r.personalUtilities);
            float uSocietyCurrent = WelfareMetrics.EvaluateDistribution(_currentPopulationLS, r.societalUtilities);
            // For default LS...
            float uSelfBase = WelfareMetrics.GetUtilityForPerson(baselineLS, r.personalUtilities);
            float uSocietyBase = WelfareMetrics.EvaluateDistribution(_baselineLS, r.societalUtilities);

            // 2. Add to totals
            
            totalBaseSocial += uSocietyBase;
            totalCurrSocial += uSocietyCurrent;
            totalBasePersonal += uSelfBase;
            totalCurrPersonal += uSelfCurrent;

            // 3. Determine facial expression and count it

            // Logic extracted to helper method below to keep this loop clean
            Sprite face = DetermineFaceExpression(r, currentLS, uSelfCurrent, uSocietyBase, uSocietyCurrent);

            // Use the face to gather whether they're happy rather than doing another calculation
            if (face == faceHappy) happyCount++;

            // 4. Calculate plot coordinates
            Vector2 plotPos;

            if (currentLS == -1) // Dead - for later
            {
                plotPos = graphGrid.GetGraveyardPosition(r.id);
            }
            else
            {
                // Fix the scales by translating/normalising it all onto a 0-1 scale
                // what proportion of the way along the scale are the values we are using
                float normX = GetNormalisedValue(r, currentLS, uSelfBase, uSocietyCurrent, xAxis);
                float normY = GetNormalisedValue(r, currentLS, uSelfBase, uSocietyCurrent, yAxis);

                // Calc the grid position for this point
                plotPos = graphGrid.GetPlotPosition(normX, normY, r.id);
            }

            // 5. Trigger visuals
            respondentList[i].UpdateVisuals(plotPos, face);
         }
         
         // 6. Update the comparison panel
         string pName = _activePolicy != null ? _activePolicy.policyName : "Default"; // Safety check for policy name, "default" if null
         UpdateScoreUI(pName, totalBaseSocial, totalCurrSocial, totalBasePersonal, totalCurrPersonal, happyCount, population.Length);
    }

    // Helper to determine face expresision based on LS, self and social utilities for now and before
    Sprite DetermineFaceExpression(Respondent r, int currentLS, float uSelfCurrent, float uSocietyBase, float uSocietyCurrent)
    {
        // Case: Death
        if (currentLS == -1)
        {
            return faceDead;
        }
        // if looking at status quo
        else if (_activePolicy == null)
        {
            // Assume anyone with 8+ LS is happy        
            if (r.currentLS >= 8)
            {
                return faceHappy;
            }
            // Assume anyone 3- LS is sad
            else if (r.currentLS <= 3)
            {
                return faceSad;
            }
            else
            {
                // For those in the middle, calc the gap between uSelfCurrent and uOthers
                float gap = uSelfCurrent - uSocietyBase; 
                float buffer = 0.05f;

                if (gap > buffer) return faceHappy; 
                else if (gap < -buffer) return faceSad;   
                else return faceNeutral;
            }
        }
        // if comparing two policies
        else
        {
            // Happy because society is fairer
            if (uSocietyCurrent > uSocietyBase + 0.01f) return faceHappy;
            // Unappy because society is less fair
            else if (uSocietyCurrent < uSocietyBase - 0.01f) return faceSad; 
        }
        
        return faceNeutral;
    }

    // Helper method to select the right data based on the axisvariable type and normalises it right
    float GetNormalisedValue(Respondent r, int ls, float uSelf, float uOthers, AxisVariable type)
    {
        if (type == AxisVariable.LifeSatisfaction)
        {
            return ls / 10.0f; // Map 0-10 to 0.0-1.0
        }
        else if (type == AxisVariable.PersonalUtility)
        {
            return uSelf; // already 0-1
        }
        else if (type == AxisVariable.SocietalFairness)
        {
            return uOthers; // already 0-1, calculated from EvaluateDistribution
        }
        else
        {
            return 0.5f; // fallback
        }
    }

    // --- CHOICE HELPERS ---
    public void PreviewOptionA()
    {
        if (policyOptionA != null) ApplyAndAnimate(policyOptionA);
    }

    public void PreviewOptionB()
    {
        if (policyOptionB != null) ApplyAndAnimate(policyOptionB);
    }

    // --- UI HELPERS ---
    void UpdateScoreUI(string policyName, double baseSoc, double currSoc, double basePers, double currPers, int happyCount, int totalPop)
    {
        if (comparisonSubtitleText) comparisonSubtitleText.text = $"Default vs. {policyName}";

        if (comparisonBodyText)
        {
            float avgSocBase = (float)(baseSoc / totalPop);
            float avgSocCurr = (float)(currSoc / totalPop);
            float diffSoc = avgSocCurr - avgSocBase;

            float avgPersBase = (float)(basePers / totalPop);
            float avgPersCurr = (float)(currPers / totalPop);
            float diffPers = avgPersCurr - avgPersBase;

            string ColorDiff(float val)
            {
                string s = val.ToString("F3");
                if (val > 0.001f) return $"<color=green>+{s}</color>";
                if (val < -0.001f) return $"<color=red>{s}</color>";
                return s;
            }

            string text = "";
            float approval = (float)happyCount / totalPop * 100f;
            text += $"<b>Public Approval:</b> {approval:F1}%\n\n";

            text += "<b>Societal Fairness:</b>\n";
            text += $"Current: {avgSocCurr:F3} ({ColorDiff(diffSoc)})\n";
            text += $"<size=80%>Default: {avgSocBase:F3}</size>\n\n";

            text += "<b>Avg Personal Wellbeing:</b>\n";
            text += $"Current: {avgPersCurr:F3} ({ColorDiff(diffPers)})\n";
            text += $"<size=80%>Default: {avgPersBase:F3}</size>";

            comparisonBodyText.text = text;
        }
    }

    void UpdateScoreUI_DefaultText()
    {
        if (comparisonSubtitleText) comparisonSubtitleText.text = "Default vs. Default";
        if (comparisonBodyText) comparisonBodyText.text = "No Comparison Available.\n(Status Quo)";
    }

    void UpdateUI_DefaultText()
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

        float uSelfCurrent = WelfareMetrics.GetUtilityForPerson(currentLS, r.personalUtilities);
        float uSociety = WelfareMetrics.EvaluateDistribution(_currentPopulationLS, r.societalUtilities);

        string info = $"<size=120%><b>Respondent #{r.id}</b></size>\n";
        info += $"<b>Life Satisfaction:</b> {currentLS}/10\n";
        info += $"<b>Personal Utility:</b> {uSelfCurrent:F2}\n";
        info += $"<b>Societal Utility:</b> {uSociety:F2}\n";
        
        string type = uSelfCurrent > uSociety ? "<color=red>Self-Interested</color>" : "<color=green>Altruistic</color>";
        info += $"<b>Type:</b> {type}";

        // Update both panels so the info is visible regardless of which tab is open
        if(policyStatsText && policyInfoPanel.activeSelf) policyStatsText.text = info;
        if(comparisonBodyText && comparisonPanel.activeSelf) comparisonBodyText.text = info;
    }

    public void OnHoverExit()
    {
        if (_activePolicy == null) 
        {
            UpdateUI_DefaultText();
            UpdateScoreUI_DefaultText();
        }
        else 
        {
            // Restore the text for the active policy
            UpdateUI_Policy(_activePolicy);
            
            // Restore the comparison text
            CalculateAndRefreshVisuals(); 
        }
    }
}