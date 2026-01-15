using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Manager References")]
    public SimulationManager simulationManager;

    [Header("Tab Controls")]
    public Button policyTabButton;
    public Button comparisonTabButton;
    public Color activeTabColour;
    public Color inactiveTabColour;
    
    [Header("Simulation Controls")]
    public Button nextPolicyButton;
    public Button resetButton;

    [Header("Comparison Controls")]
    public Button optionAButton;
    public Button optionBButton;

    [Header("Panels")]
    public GameObject policyInfoPanel;
    public GameObject comparisonPanel;
    
    [Header("Policy Rules Pop-up")]
    public GameObject rulesPopupPanel;
    public TMP_Text rulesTitleText;       
    public TMP_Text rulesTextBody;        
    public Button viewRulesButton;

    [Header("Policy Info Text")]
    public TMP_Text policyTitleText;
    public TMP_Text policyDescText;
    public TMP_Text policyStatsText;

    [Header("Comparison Text")]
    public TMP_Text comparisonSubtitleText;
    public TMP_Text comparisonBodyText;

    [Header("Graph Controls")]
    public TMP_Dropdown xAxisDropdown;
    public TMP_Dropdown yAxisDropdown;

    // Determine the entire state of the UI
    private bool _isPolicyTabActive = true; 
    private bool _isRulesPopupOpen = false;

    private Policy _currentPolicy; // Data cache

    void Start()
    {
        InitialiseDropdowns();

        // 1. Navigation Buttons (Change State)
        if (policyTabButton) policyTabButton.onClick.AddListener(() => SetTab(true));
        if (comparisonTabButton) comparisonTabButton.onClick.AddListener(() => SetTab(false));
        if (viewRulesButton) viewRulesButton.onClick.AddListener(ToggleRules);

        // 2. Action Buttons (Call Logic)
        if (nextPolicyButton) nextPolicyButton.onClick.AddListener(() => simulationManager.NextPolicy());
        if (resetButton) resetButton.onClick.AddListener(OnResetClicked);
        if (optionAButton) optionAButton.onClick.AddListener(() => simulationManager.PreviewOptionA());
        if (optionBButton) optionBButton.onClick.AddListener(() => simulationManager.PreviewOptionB());

        // 3. Initial Draw
        RefreshAllVisuals();
    }

    // --- STATE CHANGERS ---

    void SetTab(bool isPolicyTab)
    {
        _isPolicyTabActive = isPolicyTab;
        _isRulesPopupOpen = false; // Always close rules when switching tabs
        RefreshAllVisuals();
    }

    void ToggleRules()
    {
        _isRulesPopupOpen = !_isRulesPopupOpen; // Flip state
        
        // If we just opened rules, make sure text is up to date
        if (_isRulesPopupOpen) UpdateRulesText(_currentPolicy);
        
        RefreshAllVisuals();
    }

    void OnResetClicked()
    {
        simulationManager.ResetToDefault();
        // Sync Dropdowns to match Brain
        if (xAxisDropdown) xAxisDropdown.value = (int)simulationManager.xAxis;
        if (yAxisDropdown) yAxisDropdown.value = (int)simulationManager.yAxis;
    }

    // This function enforces the state variables on the Scene.
    void RefreshAllVisuals()
    {
        // 1. Determine Visibility
        // Rules override everything else.
        bool showRules = _isRulesPopupOpen;
        bool showPolicy = !_isRulesPopupOpen && _isPolicyTabActive;
        bool showCompare = !_isRulesPopupOpen && !_isPolicyTabActive;

        // 2. Apply Visibility to Panels
        if (rulesPopupPanel) rulesPopupPanel.SetActive(showRules);
        if (policyInfoPanel) policyInfoPanel.SetActive(showPolicy);
        if (comparisonPanel) comparisonPanel.SetActive(showCompare);

        // 3. Update Tab Colors (Tabs always show which one is "underneath", even if Rules are open)
        if (policyTabButton) 
             policyTabButton.GetComponent<Image>().color = _isPolicyTabActive ? activeTabColour : inactiveTabColour;
        
        if (comparisonTabButton) 
             comparisonTabButton.GetComponent<Image>().color = !_isPolicyTabActive ? activeTabColour : inactiveTabColour;

        // 4. Update "Rules" Button Text
        if (viewRulesButton)
        {
            var txt = viewRulesButton.GetComponentInChildren<TMP_Text>();
            if (txt) txt.text = showRules ? "Hide Rules" : "View Rules";
        }
    }

    // --- DATA UPDATES ---

    public void UpdatePolicyInfo(Policy p)
    {
        _currentPolicy = p;
        UpdateResetButtonVisuals(p);

        // If rules are open while data changes (unlikely but possible), refresh text
        if (_isRulesPopupOpen) UpdateRulesText(p);

        if (p == null)
        {
            if (policyTitleText) policyTitleText.text = "<b>Default (2022-2023)</b>";
            if (policyDescText) policyDescText.text = "The current distribution of life satisfaction in the UK based on ONS data.";
            if (policyStatsText) policyStatsText.text = "<b>Base:</b> ONS 2022-2023 Data\n<b>Impact:</b> None";
        }
        else
        {
            if (policyTitleText) policyTitleText.text = p.policyName;
            if (policyDescText) policyDescText.text = p.description;

            string stats = "";
            stats += $"<b>Rich:</b> {FormatVal(p.baseChangeRich)}\n";
            stats += $"<b>Middle:</b> {FormatVal(p.baseChangeMiddle)}\n";
            stats += $"<b>Poor:</b> {FormatVal(p.baseChangePoor)}";
            if (policyStatsText) policyStatsText.text = stats;
        }
    }

    public void UpdateComparisonInfo(string pName, double baseSoc, double currSoc, double basePers, double currPers, int happyCount, int totalPop)
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
            text += $"Default: {avgSocBase:F3}\n\n";
            text += "<b>Avg Personal Wellbeing:</b>\n";
            text += $"Current: {avgPersCurr:F3} ({ColorDiff(diffPers)})\n";
            text += $"Default: {avgPersBase:F3}";

            comparisonBodyText.text = text;
        }
    }

    void UpdateRulesText(Policy p)
    {
        if (p == null)
        {
            if (rulesTitleText) rulesTitleText.text = "Default State";
            if (rulesTextBody) rulesTextBody.text = "No policy active.\n\nStatus quo (ONS Data).";
            return;
        }

        if (rulesTitleText) rulesTitleText.text = p.policyName;

        if (!rulesTextBody) return;

        string content = "";
        content += $"<b>Base Impact</b>\n";
        content += $"• <b>Rich:</b> {FormatVal(p.baseChangeRich)}\n";
        content += $"• <b>Middle:</b> {FormatVal(p.baseChangeMiddle)}\n";
        content += $"• <b>Poor:</b> {FormatVal(p.baseChangePoor)}\n\n";

        if (p.specificRules != null && p.specificRules.Count > 0)
        {
            content += $"<b>Targeted Rules ({p.specificRules.Count})</b>\n";
            foreach (var rule in p.specificRules)
            {
                content += $"<b>{rule.note}</b>\n";
                content += $"   • <color=#FFD700>Target:</color> LS {rule.minLS} - {rule.maxLS}\n";
                string chanceStr = rule.affectEveryone ? "100%" : $"{(rule.proportion * 100):F0}%";
                content += $"   • <color=#FFD700>Chance:</color> {chanceStr}\n";
                content += $"   • <color=#FFD700>Effect:</color> {FormatVal(rule.impact)}\n\n";
            }
        }
        else
        {
            content += "<i>No specific targeting rules defined.</i>";
        }
        rulesTextBody.text = content;
    }

    // --- HELPERS ---

    string FormatVal(float val)
    {
        if (val > 0.001f) return $"<color=green>+{val:0.##}</color>";
        if (val < -0.001f) return $"<color=red>{val:0.##}</color>";
        return "0";
    }

    void UpdateResetButtonVisuals(Policy activePolicy)
    {
        if (resetButton == null) return;
        var textComp = resetButton.GetComponentInChildren<TMP_Text>();
        if (textComp) textComp.fontStyle = (activePolicy == null) ? FontStyles.Normal : FontStyles.Italic;
    }

    public void UpdateHoverInfo(string info)
    {
        // Don't show hover text if Rules are covering everything
        if (_isRulesPopupOpen) return;

        if (_isPolicyTabActive && policyStatsText) policyStatsText.text = info;
        else if (!_isPolicyTabActive && comparisonBodyText) comparisonBodyText.text = info;
    }

    void InitialiseDropdowns()
    {
        if (!xAxisDropdown || !yAxisDropdown) return;

        string[] enumNames = Enum.GetNames(typeof(AxisVariable));
        List<string> options = new List<string>(enumNames);

        xAxisDropdown.ClearOptions(); xAxisDropdown.AddOptions(options); xAxisDropdown.value = (int)simulationManager.xAxis; 
        yAxisDropdown.ClearOptions(); yAxisDropdown.AddOptions(options); yAxisDropdown.value = (int)simulationManager.yAxis;

        xAxisDropdown.onValueChanged.AddListener(OnXAxisChanged);
        yAxisDropdown.onValueChanged.AddListener(OnYAxisChanged);
    }

    public void OnXAxisChanged(int index) { simulationManager.SetAxisVariables((AxisVariable)index, simulationManager.yAxis); }
    public void OnYAxisChanged(int index) { simulationManager.SetAxisVariables(simulationManager.xAxis, (AxisVariable)index); }
}