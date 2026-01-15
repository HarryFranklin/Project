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
    public Color activeTabColour = new Color(0.8f, 0.8f, 0.8f);
    public Color inactiveTabColour = new Color(0.6f, 0.6f, 0.6f); 
    
    [Header("Simulation Controls")]
    public Button nextPolicyButton;
    public Button resetButton;

    [Header("Comparison Controls")]
    public Button optionAButton;
    public Button optionBButton;

    [Header("Panels")]
    public GameObject policyInfoPanel;
    public GameObject comparisonPanel;

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

    private bool _showPolicyTab = true; 

    void Start()
    {
        InitialiseDropdowns();

        // Wire up buttons
        if (nextPolicyButton) nextPolicyButton.onClick.AddListener(() => simulationManager.NextPolicy());
        if (resetButton) resetButton.onClick.AddListener(OnResetClicked);
        if (optionAButton) optionAButton.onClick.AddListener(() => simulationManager.PreviewOptionA());
        if (optionBButton) optionBButton.onClick.AddListener(() => simulationManager.PreviewOptionB());

        if (policyTabButton) policyTabButton.onClick.AddListener(OnPolicyTabClicked);
        if (comparisonTabButton) comparisonTabButton.onClick.AddListener(OnComparisonTabClicked);

        RefreshTabVisuals();
    }

    void OnResetClicked()
    {
        simulationManager.ResetToDefault();
        if (xAxisDropdown) xAxisDropdown.value = (int)simulationManager.xAxis;
        if (yAxisDropdown) yAxisDropdown.value = (int)simulationManager.yAxis;
    }

    // --- TAB SYSTEM ---
    public void OnPolicyTabClicked() { _showPolicyTab = true; RefreshTabVisuals(); }
    public void OnComparisonTabClicked() { _showPolicyTab = false; RefreshTabVisuals(); }

    void RefreshTabVisuals()
    {
        if (policyInfoPanel) policyInfoPanel.SetActive(_showPolicyTab);
        if (comparisonPanel) comparisonPanel.SetActive(!_showPolicyTab);

        if (policyTabButton) 
        {
            var img = policyTabButton.GetComponent<Image>();
            if (img) img.color = _showPolicyTab ? activeTabColour : inactiveTabColour;
        }

        if (comparisonTabButton)
        {
            var img = comparisonTabButton.GetComponent<Image>();
            if (img) img.color = !_showPolicyTab ? activeTabColour : inactiveTabColour;
        }
    }

    // --- DATA UPDATES ---

    public void UpdatePolicyInfo(Policy p)
    {
        UpdateResetButtonVisuals(p);

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

            string FormatChange(float val)
            {
                if (val > 0.001f) return $"<color=green>+{val:0.##}</color>";
                if (val < -0.001f) return $"<color=red>{val:0.##}</color>";
                return "0";
            }

            string stats = "";
            stats += $"<b>Rich:</b> {FormatChange(p.baseChangeRich)}\n";
            stats += $"<b>Middle:</b> {FormatChange(p.baseChangeMiddle)}\n";
            stats += $"<b>Poor:</b> {FormatChange(p.baseChangePoor)}";
            
            if (policyStatsText) policyStatsText.text = stats;
        }
    }

    void UpdateResetButtonVisuals(Policy activePolicy)
    {
        if (resetButton == null) return;
        var textComp = resetButton.GetComponentInChildren<TMP_Text>();

        if (textComp)
        {
            textComp.fontStyle = (activePolicy == null) ? FontStyles.Normal : FontStyles.Italic;
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
            text += $"<size=80%>Default: {avgSocBase:F3}</size>\n\n";

            text += "<b>Avg Personal Wellbeing:</b>\n";
            text += $"Current: {avgPersCurr:F3} ({ColorDiff(diffPers)})\n";
            text += $"<size=80%>Default: {avgPersBase:F3}</size>";

            comparisonBodyText.text = text;
        }
    }

    public void UpdateHoverInfo(string info)
    {
        if (_showPolicyTab && policyStatsText) policyStatsText.text = info;
        else if (!_showPolicyTab && comparisonBodyText) comparisonBodyText.text = info;
    }

    // --- DROPDOWNS ---
    void InitialiseDropdowns()
    {
        if (!xAxisDropdown || !yAxisDropdown) return;

        string[] enumNames = Enum.GetNames(typeof(AxisVariable));
        List<string> options = new List<string>(enumNames);

        xAxisDropdown.ClearOptions();
        xAxisDropdown.AddOptions(options);
        xAxisDropdown.value = (int)simulationManager.xAxis; 
        
        yAxisDropdown.ClearOptions();
        yAxisDropdown.AddOptions(options);
        yAxisDropdown.value = (int)simulationManager.yAxis;

        xAxisDropdown.onValueChanged.AddListener(OnXAxisChanged);
        yAxisDropdown.onValueChanged.AddListener(OnYAxisChanged);
    }

    public void OnXAxisChanged(int index)
    {
        simulationManager.SetAxisVariables((AxisVariable)index, simulationManager.yAxis);
    }

    public void OnYAxisChanged(int index)
    {
        simulationManager.SetAxisVariables(simulationManager.xAxis, (AxisVariable)index);
    }
}