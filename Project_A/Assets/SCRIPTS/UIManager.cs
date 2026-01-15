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
    public Color activeTabColour = new Color(0.8f, 0.8f, 0.8f); // Default panel colour
    public Color inactiveTabColour = new Color(0.6f, 0.6f, 0.6f); // Slightly darker/grey
    
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

    // State: Which tab is currently selected?
    private bool _showPolicyTab = true; 

    void Start()
    {
        InitialiseDropdowns();

        // The UI manager listens for the click and either handles it or tells simulationmanager what to do
        if (nextPolicyButton) nextPolicyButton.onClick.AddListener(() => simulationManager.NextPolicy());
        if (optionAButton) optionAButton.onClick.AddListener(() => simulationManager.PreviewOptionA());
        if (optionBButton) optionBButton.onClick.AddListener(() => simulationManager.PreviewOptionB());

        // Tab buttons
        if (policyTabButton) policyTabButton.onClick.AddListener(OnPolicyTabClicked);
        if (comparisonTabButton) comparisonTabButton.onClick.AddListener(OnComparisonTabClicked);

        RefreshTabVisuals();
    }

    // --- METHODS TO CONNECT UI TO SIM ---
    void OnResetClicked()
    {
        simulationManager.ResetToDefault();

        // Reset axes to default
        if (xAxisDropdown) xAxisDropdown.value = (int)simulationManager.xAxis;
        if (yAxisDropdown) yAxisDropdown.value = (int)simulationManager.yAxis;
    }

    void OnNextPolicyClicked()
    {        
        simulationManager.NextPolicy(); // Forward the command to the brain
    }

    // --- TAB SYSTEM ---

    // 1. User Clicks "Policy Info"
    public void OnPolicyTabClicked()
    {
        _showPolicyTab = true;
        RefreshTabVisuals();
    }

    // 2. User Clicks "Comparison"
    public void OnComparisonTabClicked()
    {
        _showPolicyTab = false;
        RefreshTabVisuals();
    }

    // 3. Update the Buttons and Panels based on state
    void RefreshTabVisuals()
    {
        // A. Toggle Panels
        if (policyInfoPanel) policyInfoPanel.SetActive(_showPolicyTab);
        if (comparisonPanel) comparisonPanel.SetActive(!_showPolicyTab);

        // B. Color Buttons (Visual Feedback)
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

    // --- DATA UPDATES (Called by simulationManager) ---

    // simulationManager pushes data here. It doesn't care which tab is open.
    public void UpdatePolicyInfo(Policy p)
    {
        // Update the reset button
        UpdateResetButtonVisuals(p);

        if (p == null)
        {
            policyTitleText.text = "<b>Default (2022-2023)</b>";
            policyDescText.text = "The current distribution of life satisfaction in the UK based on ONS data.";
            policyStatsText.text = "<b>Base:</b> ONS 2022-2023 Data\n<b>Impact:</b> None";
        }
        else
        {
            policyTitleText.text = p.policyName;
            policyDescText.text = p.description;

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

    // Handles the reset button logic
    void UpdateResetButtonVisuals(Policy activePolicy)
    {
        if (resetButton == null) return;

        var textComp = resetButton.GetComponentInChildren<TMP_Text>();

        if (activePolicy == null)
        {
            // Not italic
            if (textComp) textComp.fontStyle = FontStyles.Normal;            
        }
        else
        {   
            // Italic
            if (textComp) textComp.fontStyle = FontStyles.Italic;

        }
    }

    public void UpdateComparisonInfo(string pName, double baseSoc, double currSoc, double basePers, double currPers, int happyCount, int totalPop)
    {
        comparisonSubtitleText.text = $"Default vs. {pName}";

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

    // --- DROPDOWNS ---
    void InitialiseDropdowns()
    {
        if (!xAxisDropdown || !yAxisDropdown) return;

        // 1. Get all values from the AxisVariable Enum
        string[] enumNames = Enum.GetNames(typeof(AxisVariable));
        List<string> options = new List<string>(enumNames);

        // 2. Clear and Populate X Axis (Direct mapping)
        xAxisDropdown.ClearOptions();
        xAxisDropdown.AddOptions(options);
        // Set visual selection to match the actual logic in simulationManager
        xAxisDropdown.value = (int)simulationManager.xAxis; 
        
        // 3. Clear and Populate Y Axis
        yAxisDropdown.ClearOptions();
        yAxisDropdown.AddOptions(options);
        yAxisDropdown.value = (int)simulationManager.yAxis;

        // 4. Add Listeners
        xAxisDropdown.onValueChanged.AddListener(OnXAxisChanged);
        yAxisDropdown.onValueChanged.AddListener(OnYAxisChanged);
    }

    public void OnXAxisChanged(int index)
    {
        AxisVariable selected = (AxisVariable)index;
        simulationManager.SetAxisVariables(selected, simulationManager.yAxis);
    }

    public void OnYAxisChanged(int index)
    {
        AxisVariable selected = (AxisVariable)index;
        simulationManager.SetAxisVariables(simulationManager.xAxis, selected);
    }
    
    // Hover logic
    public void UpdateHoverInfo(string info)
    {
        // Only update the active one so we don't overwrite hidden text unnecessarily
        if (_showPolicyTab) policyStatsText.text = info;
        else comparisonBodyText.text = info;
    }
}