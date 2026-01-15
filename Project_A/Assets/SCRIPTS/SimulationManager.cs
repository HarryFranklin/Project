using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SimulationManager : MonoBehaviour
{
    [Header("Architecture")]
    public VisualisationManager visuals;
    public UIManager uiManager;
    public DataReader dataReader;

    [Header("Plotting Settings")]
    public AxisVariable xAxis = AxisVariable.LifeSatisfaction;
    public AxisVariable yAxis = AxisVariable.SocietalUtility;

    [Header("Policy Data")]
    public List<Policy> policies;
    [Tooltip("For drag-and-drop comparison buttons")]
    public Policy policyOptionA;
    [Tooltip("For drag-and-drop comparison buttons")]
    public Policy policyOptionB;

    [Header("UI: Panels")]
    public GameObject policyInfoPanel;
    public GameObject comparisonPanel;

    [Header("UI: Policy Info Tab")]
    public TMP_Text policyTitleText;
    public TMP_Text policyDescText;
    public TMP_Text policyStatsText;

    [Header("UI: Comparison Tab")]
    public TMP_Text comparisonSubtitleText;
    public TMP_Text comparisonBodyText;

    // --- INTERNAL DATA STATE ---
    private Dictionary<int, Respondent> _respondentMap;
    private List<Respondent> _populationList;
    
    // Arrays for fast processing
    private int[] _baselineLS;
    private int[] _currentLS;
    
    // State Tracking
    private int _policyIndex = -1; // -1 = Default
    private Policy _activePolicy = null;

    void Start()
    {
        // 1. Load Data
        _respondentMap = dataReader.GetRespondents();
        _populationList = new List<Respondent>(_respondentMap.Values);

        // 2. Initialise Arrays
        int count = _populationList.Count;
        _baselineLS = new int[count];
        _currentLS = new int[count];

        // 3. Generate Initial Status Quo (ONS Distribution)
        float[] onsDist = WelfareMetrics.GetBaselineDistribution();
        for (int i = 0; i < count; i++)
        {
            int startingLS = WelfareMetrics.GetWeightedRandomLS(onsDist);
            _baselineLS[i] = startingLS;
            _populationList[i].currentLS = startingLS; // Sync object data
        }

        // Initially, current = baseline
        System.Array.Copy(_baselineLS, _currentLS, count);

        // 4. Create the Visuals (Delegate to View)
        if (visuals != null)
        {
            visuals.CreatePopulation(_populationList, this);
        }

        // 5. Run Initial Update
        ShowPolicyInfoTab();
        UpdateSimulation();
    }

    // --- CONTROLS ---

    public void NextPolicy()
    {
        if (policies.Count == 0) return;
        _policyIndex = (_policyIndex + 1) % policies.Count;
        ApplyPolicy(policies[_policyIndex]);
    }

    public void ResetToDefault()
    {
        _policyIndex = -1;
        _activePolicy = null;
        
        // 1. Reset Axes to desired defaults
        xAxis = AxisVariable.LifeSatisfaction;
        yAxis = AxisVariable.SocietalUtility;

        // 2. Reset Population Data
        System.Array.Copy(_baselineLS, _currentLS, _baselineLS.Length);
        
        UpdateSimulation();
    }

    public void PreviewOptionA() { if (policyOptionA) ApplyPolicy(policyOptionA); }
    public void PreviewOptionB() { if (policyOptionB) ApplyPolicy(policyOptionB); }

    // --- CORE LOGIC ---

    // 1. Calculate the New Data
    void ApplyPolicy(Policy p)
    {
        _activePolicy = p;
        // Ask the Policy script to calculate the new numbers
        _currentLS = p.ApplyPolicy(_populationList.ToArray());
        
        UpdateSimulation();
    }

    // 2. Update the World (Visuals + UI)
    void UpdateSimulation()
    {
        // A. Update the Visuals (Pass data to the View)
        if (visuals != null)
        {
            visuals.UpdateDisplay(
                _populationList.ToArray(),
                _currentLS,
                _baselineLS,
                _activePolicy,
                xAxis,
                yAxis
            );
        }

        // B. Update the UI Text (Calculate totals here in the Controller)
        CalculateAndRefreshUI();
    }

    // Calculates aggregates (Approval, Averages) for the UI panels
    void CalculateAndRefreshUI()
    {
        if (!uiManager) return;

        int count = _populationList.Count; 
        
        // Initialise totals
        double totalBaseSocial = 0; 
        double totalCurrSocial = 0;
        double totalBasePersonal = 0; 
        double totalCurrPersonal = 0;
        int happyCount = 0;

        // Loop
        for (int i = 0; i < count; i++)
        {
            Respondent r = _populationList[i];
            int cLS = _currentLS[i];
            int bLS = _baselineLS[i];

            // Metrics
            float uSelfCurr = WelfareMetrics.GetUtilityForPerson(cLS, r.personalUtilities);
            float uSelfBase = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
            float uSocCurr = WelfareMetrics.EvaluateDistribution(_currentLS, r.societalUtilities);
            float uSocBase = WelfareMetrics.EvaluateDistribution(_baselineLS, r.societalUtilities);

            totalBaseSocial += uSocBase; 
            totalCurrSocial += uSocCurr;
            totalBasePersonal += uSelfBase; 
            totalCurrPersonal += uSelfCurr;

            // Happiness Logic
            bool isHappy = false;
            if (cLS != -1)
            {
                // Simple check: Is society fairer now than before?
                if (uSocCurr > uSocBase + 0.01f) isHappy = true;
            }
            if (isHappy) happyCount++;
        }

        // Send to the UI
        if (_activePolicy == null)
        {
            // Default State
            uiManager.UpdatePolicyInfo(null);
            uiManager.UpdateComparisonInfo(
                "Default", 
                totalBaseSocial, totalCurrSocial, 
                totalBasePersonal, totalCurrPersonal, 
                happyCount, count
            );
        }
        else
        {
            // Policy State
            uiManager.UpdatePolicyInfo(_activePolicy);
            uiManager.UpdateComparisonInfo(
                _activePolicy.policyName, 
                totalBaseSocial, totalCurrSocial, 
                totalBasePersonal, totalCurrPersonal, 
                happyCount, count
            );
        }
    }

    // --- UI HELPERS ---

    // Called by UIManager when the dropdowns change
    public void SetAxisVariables(AxisVariable x, AxisVariable y)
    {
        xAxis = x;
        yAxis = y;
        UpdateSimulation(); // Refresh graph immediately
    }

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

    void UpdateUI_DefaultText()
    {
        if (policyTitleText) policyTitleText.text = "<b>Default (2022-2023)</b>";
        if (policyDescText) policyDescText.text = "The current distribution of life satisfaction in the UK based on ONS data.";
        if (policyStatsText) policyStatsText.text = "<b>Base:</b> ONS 2022-2023 Data\n<b>Impact:</b> None";
    }

    void UpdateUI_PolicyText(Policy p)
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
            stats += $"<b>Rich:</b> {FormatChange(p.changeForRich)}\n";
            stats += $"<b>Middle:</b> {FormatChange(p.changeForMiddle)}\n";
            stats += $"<b>Poor:</b> {FormatChange(p.changeForPoor)}";
            policyStatsText.text = stats;
        }
    }

    void UpdateScoreUI_DefaultText()
    {
        if (comparisonSubtitleText) comparisonSubtitleText.text = "Default vs. Default";
        if (comparisonBodyText) comparisonBodyText.text = "No Comparison Available.\n(Status Quo)";
    }

    void UpdateScoreUI_Comparison(string pName, double baseSoc, double currSoc, double basePers, double currPers, int happyCount, int totalPop)
    {
        if (comparisonSubtitleText) comparisonSubtitleText.text = $"Default vs. {pName}";

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

    // --- HOVER LOGIC ---
    
    // Called by the View when mouse enters a person
    public void OnHoverEnter(Respondent r)
    {
        if (r == null || _currentLS == null) return;

        // Find the index of this respondent
        int index = _populationList.IndexOf(r);
        if (index == -1) return;

        int currentLS = _currentLS[index];

        float uSelfCurrent = WelfareMetrics.GetUtilityForPerson(currentLS, r.personalUtilities);
        float uSociety = WelfareMetrics.EvaluateDistribution(_currentLS, r.societalUtilities);

        string info = $"<size=120%><b>Respondent #{r.id}</b></size>\n";
        info += $"<b>Life Satisfaction:</b> {currentLS}/10\n";
        info += $"<b>Personal Utility:</b> {uSelfCurrent:F2}\n";
        info += $"<b>Societal Utility:</b> {uSociety:F2}\n";
        
        // Update the active panel text
        if(policyInfoPanel.activeSelf && policyStatsText) policyStatsText.text = info;
        if(comparisonPanel.activeSelf && comparisonBodyText) comparisonBodyText.text = info;
    }

    public void OnHoverExit()
    {
        // Restore default text
        if (_activePolicy == null) 
        {
            UpdateUI_DefaultText();
            UpdateScoreUI_DefaultText();
        }
        else 
        {
            // Re-calculate totals to restore the comparison text
            CalculateAndRefreshUI(); 
        }
    }

    // --- EDITOR ---
    
    // Automatically refresh if we change Axis settings in the Inspector
    void OnValidate()
    {
        if (Application.isPlaying && visuals != null && _populationList != null)
        {
            // Just redraw, don't re-calculate logic
            visuals.UpdateDisplay(_populationList.ToArray(), _currentLS, _baselineLS, _activePolicy, xAxis, yAxis);
        }
    }
}