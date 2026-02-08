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
    public TMP_Dropdown faceModeDropdown;

    [Header("Visual Tools")]
    public Toggle ghostModeToggle;
    public Toggle arrowModeToggle;

    // State
    private bool _isPolicyTabActive = true; 
    private bool _isRulesPopupOpen = false;
    private Policy _currentPolicy; 

    void Start()
    {
        InitialiseDropdowns();

        // 1. Navigation Buttons
        if (policyTabButton) policyTabButton.onClick.AddListener(() => SetTab(true));
        if (comparisonTabButton) comparisonTabButton.onClick.AddListener(() => SetTab(false));
        if (viewRulesButton) viewRulesButton.onClick.AddListener(ToggleRules);

        // 2. Visual Toggles
        if (ghostModeToggle) ghostModeToggle.onValueChanged.AddListener(OnGhostToggleChanged);
        if (arrowModeToggle) arrowModeToggle.onValueChanged.AddListener((val) => simulationManager.SetArrowMode(val));

        RefreshAllVisuals();
    }

    // --- VISUAL LOGIC ONLY ---

    void SetTab(bool isPolicyTab)
    {
        _isPolicyTabActive = isPolicyTab;
        _isRulesPopupOpen = false; 
        RefreshAllVisuals();
    }

    void ToggleRules()
    {
        _isRulesPopupOpen = !_isRulesPopupOpen;
        if (_isRulesPopupOpen) UpdateRulesText(_currentPolicy);
        RefreshAllVisuals();
    }

    void OnGhostToggleChanged(bool isOn) 
    {
        simulationManager.SetGhostMode(isOn);
    }

    void RefreshAllVisuals()
    {
        bool showRules = _isRulesPopupOpen;
        bool showPolicy = !_isRulesPopupOpen && _isPolicyTabActive;
        bool showCompare = !_isRulesPopupOpen && !_isPolicyTabActive;

        if (rulesPopupPanel) rulesPopupPanel.SetActive(showRules);
        if (policyInfoPanel) policyInfoPanel.SetActive(showPolicy);
        if (comparisonPanel) comparisonPanel.SetActive(showCompare);

        if (policyTabButton) policyTabButton.GetComponent<Image>().color = _isPolicyTabActive ? activeTabColour : inactiveTabColour;
        if (comparisonTabButton) comparisonTabButton.GetComponent<Image>().color = !_isPolicyTabActive ? activeTabColour : inactiveTabColour;

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
        if (_isRulesPopupOpen) UpdateRulesText(p);

        if (p == null)
        {
            if (policyTitleText) policyTitleText.text = "<b>No Policy Active</b>";
            if (policyDescText) policyDescText.text = "The simulation is currently showing the baseline state.";
            if (policyStatsText) policyStatsText.text = "";
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

    // Helper to format text green/red
    string FormatVal(float val)
    {
        if (val > 0.001f) return $"<color=green>+{val:0.##}</color>";
        if (val < -0.001f) return $"<color=red>{val:0.##}</color>";
        return "0";
    }

    void UpdateRulesText(Policy p)
    {
        if (p == null)
        {
            if (rulesTitleText) rulesTitleText.text = "Default State";
            if (rulesTextBody) rulesTextBody.text = "No policy active.";
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
        rulesTextBody.text = content;
    }

    public void UpdateHoverInfo(string info)
    {
        if (_isRulesPopupOpen) return;
        if (_isPolicyTabActive && policyStatsText) policyStatsText.text = info;
        else if (!_isPolicyTabActive && comparisonBodyText) comparisonBodyText.text = info;
    }

    // --- DROPDOWNS ---
    void InitialiseDropdowns()
    {
        if (xAxisDropdown)
        {
            xAxisDropdown.ClearOptions();
            xAxisDropdown.AddOptions(new List<string>(Enum.GetNames(typeof(AxisVariable))));
            xAxisDropdown.value = (int)simulationManager.xAxis;
            xAxisDropdown.onValueChanged.AddListener((i) => simulationManager.SetAxisVariables((AxisVariable)i, simulationManager.yAxis));
        }

        if (yAxisDropdown)
        {
            yAxisDropdown.ClearOptions();
            yAxisDropdown.AddOptions(new List<string>(Enum.GetNames(typeof(AxisVariable))));
            yAxisDropdown.value = (int)simulationManager.yAxis;
            yAxisDropdown.onValueChanged.AddListener((i) => simulationManager.SetAxisVariables(simulationManager.xAxis, (AxisVariable)i));
        }

        if (faceModeDropdown)
        {
            faceModeDropdown.ClearOptions();
            faceModeDropdown.AddOptions(new List<string>(Enum.GetNames(typeof(FaceMode))));
            faceModeDropdown.value = (int)simulationManager.faceMode;
            faceModeDropdown.onValueChanged.AddListener((i) => simulationManager.SetFaceMode((FaceMode)i));
        }
    }
}