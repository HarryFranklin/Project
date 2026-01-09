using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SimulationManager : MonoBehaviour
{
    [Header("Data")]
    public DataReader dataReader; 
    public List<Policy> policies; 

    [Header("Panels (Drag GameObjects here)")]
    public GameObject policyInfoPanel; // The panel with Description/Stats
    public GameObject comparisonPanel; // The new panel with the comparison data

    [Header("Policy Info Tab References")]
    public TMP_Text policyTitleText;
    public TMP_Text policyDescText;
    public TMP_Text policyStatsText;

    [Header("Comparison Tab References")]
    // 1. "Policy Comparison" (Static Title) - You can just set this in Editor
    // 2. "Default vs. POLICYNAME"
    public TMP_Text comparisonSubtitleText; 
    // 3. The Information
    public TMP_Text comparisonBodyText; 

    [Header("Visual Configuration")]
    public Transform container;
    public GameObject personPrefab; 
    public Sprite faceHappy, faceNeutral, faceSad;

    private Dictionary<int, Respondent> respondents;
    private List<RespondentVisual> respondentList = new List<RespondentVisual>();
    private List<Respondent> _cachedPopulationList;
    private int policyIndex = -1; 

    // Cache current results
    private int[] _currentPopulationLS;
    private int[] _baselineLS;

    void Start()
    {
        respondents = dataReader.GetRespondents();
        float[] onsDist = WelfareMetrics.GetBaselineDistribution();

        // 1. Assign Data
        foreach (var r in respondents.Values)
        {
            r.currentLS = WelfareMetrics.GetWeightedRandomLS(onsDist);
        }

        // 2. Cache Order
        _cachedPopulationList = new List<Respondent>(respondents.Values);

        // 3. Spawn Visuals
        foreach (var r in _cachedPopulationList)
        {
            GameObject respondent = Instantiate(personPrefab, container);
            RespondentVisual respondentVisual = respondent.GetComponent<RespondentVisual>();
            respondentVisual.Initialise(r, this);
            respondentList.Add(respondentVisual);
        }

        // 4. Default View
        ShowPolicyInfoTab(); // Start on the main info tab
        UpdateSimulation();
    }

    // --- BUTTON FUNCTIONS ---
    // Link these to your two new buttons
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

    public void NextPolicy()
    {
        if (policies.Count == 0) return;
        policyIndex = (policyIndex + 1) % policies.Count;
        UpdateSimulation();
    }

    void UpdateSimulation()
    {
        Respondent[] population = _cachedPopulationList.ToArray();
        
        _baselineLS = new int[population.Length];
        _currentPopulationLS = new int[population.Length];

        for(int i=0; i<population.Length; i++) _baselineLS[i] = population[i].currentLS;

        // Apply Policy
        if (policyIndex == -1)
        {
            System.Array.Copy(_baselineLS, _currentPopulationLS, _baselineLS.Length);
            
            // Update BOTH tabs so they are ready when clicked
            UpdateUI_Default();
            UpdateScoreUI_Default();
        }
        else
        {
            _currentPopulationLS = policies[policyIndex].ApplyPolicy(population);
            
            // Update BOTH tabs
            UpdateUI_Policy(policies[policyIndex]);
            
            // We calculate totals inside the loop below, then update the UI
        }

        // Calculate Totals & Visuals
        double totalBaseSocial = 0;
        double totalCurrSocial = 0;
        double totalBasePersonal = 0;
        double totalCurrPersonal = 0;
        int happyCount = 0;

        for (int i = 0; i < population.Length; i++)
        {
            Respondent r = population[i];

            // 1. Math
            float uSelf = WelfareMetrics.GetUtilityForPerson(_currentPopulationLS[i], r.personalUtilities);
            float uSelfBase = WelfareMetrics.GetUtilityForPerson(_baselineLS[i], r.personalUtilities);
            float wOld = WelfareMetrics.EvaluateDistribution(_baselineLS, r.societalUtilities);
            float wNew = WelfareMetrics.EvaluateDistribution(_currentPopulationLS, r.societalUtilities);

            // 2. Totals
            totalBaseSocial += wOld;
            totalCurrSocial += wNew;
            totalBasePersonal += uSelfBase;
            totalCurrPersonal += uSelf;

            // 3. Visuals
            Sprite face = faceNeutral;
            if (wNew > wOld + 0.01f) 
            {
                face = faceHappy;
                happyCount++;
            }
            else if (wNew < wOld - 0.01f) face = faceSad; 

            respondentList[i].UpdateVisuals(uSelf, face);
        }

        // Update Comparison Text if we are not in default mode
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
    }
}