using System.Collections.Generic;
using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("Architecture")]
    public VisualisationManager visuals; 
    public UIManager uiManager;
    public DataReader dataReader;

    [Header("1. Boot Settings (Intro)")]
    public AxisVariable bootXAxis = AxisVariable.LifeSatisfaction;
    public AxisVariable bootYAxis = AxisVariable.Stack;

    [Header("2. Gameplay Settings (Standard View)")]
    public AxisVariable gameplayXAxis = AxisVariable.LifeSatisfaction;
    public AxisVariable gameplayYAxis = AxisVariable.SocietalFairness;
    
    [Header("3. Preview Settings (On Hover)")]
    public AxisVariable previewXAxis = AxisVariable.DeltaPersonalUtility;
    public AxisVariable previewYAxis = AxisVariable.DeltaSocietalFairness;

    [Header("Current State (Read Only)")]
    public AxisVariable xAxis;
    public AxisVariable yAxis;
    public FaceMode faceMode = FaceMode.Split;

    private bool _previewLocked = false;

    // --- INTERNAL DATA STATE ---
    public List<Respondent> PopulationList { get; private set; }
    public float[] BaselineLS { get; private set; }
    public float[] CurrentLS { get; private set; }
    public Policy ActivePolicy { get; private set; }
    public Policy _currentPreviewPolicy;

    // Cache
    private AxisVariable _cachedX;
    private AxisVariable _cachedY;
    private bool _hasGameStarted = false; // Tracks if we have left the boot state

    void Awake()
    {
        // 1. Apply Boot Defaults
        xAxis = bootXAxis;
        yAxis = bootYAxis;

        _cachedX = bootXAxis;
        _cachedY = bootYAxis;

        _hasGameStarted = false;

        // 2. Load Data
        var respondentMap = dataReader.GetRespondents();
        PopulationList = new List<Respondent>(respondentMap.Values);

        int count = PopulationList.Count;
        BaselineLS = new float[count];
        CurrentLS = new float[count];

        // 3. Generate Initial State (Mirrored Continuous)
        float[] onsDist = WelfareMetrics.GetBaselineDistribution();
        for (int i = 0; i < count; i++)
        {
            float startingLS = WelfareMetrics.GetContinuousWeightedLS(onsDist);
            BaselineLS[i] = startingLS;
            PopulationList[i].currentLS = startingLS; 
        }

        System.Array.Copy(BaselineLS, CurrentLS, count);

        // 4. Create Visuals
        if (visuals != null)
        {
            visuals.CreatePopulation(PopulationList, this);
        }

        UpdateSimulation();
    }

    // --- API FOR TERM MANAGER ---
    public void ApplyPolicyEffect(Policy p)
    {
        ActivePolicy = p;

        // 1. Calculate the new state
        float[] newValues = p.ApplyPolicy(PopulationList.ToArray());
        CurrentLS = newValues;

        for (int i = 0; i < PopulationList.Count; i++)
        {
            PopulationList[i].currentLS = CurrentLS[i];
        }

        // 2. State Transition (Boot -> Gameplay)
        // Once a policy is applied, we switch to the "Gameplay" axes permanently
        if (!_hasGameStarted)
        {
            _hasGameStarted = true;
            xAxis = gameplayXAxis;
            yAxis = gameplayYAxis;

            _cachedX = gameplayXAxis;
            _cachedY = gameplayYAxis;
            
            // Ensure dropdowns reflect the move to Gameplay axes
            if (uiManager != null) uiManager.SyncDropdownsToAxes();
        }

        // Reset zoom
        visuals.ResetZoom();

        // Prevents lingering ghosts
        if (visuals) visuals.SetHoverHighlight(null);
        
        UpdateSimulation();
    }

    // --- METRICS ---
    public float GetCurrentSocietalFairness()
    {
        if (PopulationList == null) return 0f;
        double total = 0;
        for(int i=0; i<PopulationList.Count; i++)
            total += WelfareMetrics.EvaluateDistribution(CurrentLS, PopulationList[i].societalUtilities);
        return (float)(total / PopulationList.Count);
    }
    
    public float GetCurrentAvgLS()
    {
        if (CurrentLS == null || CurrentLS.Length == 0) return 0f;
        float sum = 0;
        foreach (var v in CurrentLS) sum += v;
        return sum / CurrentLS.Length;
    }

    // --- PREVIEW LOGIC ---
    public void PreviewPolicy(Policy p)
    {
        _currentPreviewPolicy = p;

        _cachedX = xAxis;
        _cachedY = yAxis;

        xAxis = previewXAxis; // DeltaPersonalUtility
        yAxis = previewYAxis; // DeltaSocietalFairness

        // Sync the UI dropdowns to reflect the Delta view
        if (uiManager != null) uiManager.SyncDropdownsToAxes();

        float[] tempLS = p.ApplyPolicy(PopulationList.ToArray());

        if (visuals) visuals.UpdateDisplay(PopulationList.ToArray(), tempLS, BaselineLS, p, xAxis, yAxis, faceMode);
        CalculateAndRefreshUI(tempLS, p);
    }

    public void SetPreviewLock(bool isLocked)
    {
        _previewLocked = isLocked;
        // If unlocking, immediately clear the preview
        if (!isLocked) StopPreview(); 
    }

    public void StopPreview()
    {
        if (_previewLocked) return;

        _currentPreviewPolicy = null;

        xAxis = _cachedX;
        yAxis = _cachedY;

        // Sync the UI dropdowns back to the original view (e.g., LS and Stack)
        if (uiManager != null) uiManager.SyncDropdownsToAxes();

        UpdateSimulation();
    }

    public void RefreshGraphOnly()
    {
        // If we are currently looking at a preview (locked or hovered), redraw that
        if (_currentPreviewPolicy != null)
        {
            PreviewPolicy(_currentPreviewPolicy);
        }
        else
        {
            UpdateSimulation();
        }
    }

    // --- INTERNAL UPDATES ---
    public void UpdateSimulation()
    {
        // Safety check
        if (PopulationList == null || PopulationList.Count == 0) return;
        
        if (visuals) visuals.UpdateDisplay(PopulationList.ToArray(), CurrentLS, BaselineLS, ActivePolicy, xAxis, yAxis, faceMode);
        CalculateAndRefreshUI(CurrentLS, ActivePolicy);
    }

    void CalculateAndRefreshUI(float[] targetLS, Policy policyToDisplay)
    {
        if (!uiManager) return;

        int count = PopulationList.Count; 
        double totalBaseSocial = 0, totalCurrSocial = 0;
        double totalBasePersonal = 0, totalCurrPersonal = 0;
        int happyCount = 0;

        for (int i = 0; i < count; i++)
        {
            Respondent r = PopulationList[i];
            float uSocCurr = WelfareMetrics.EvaluateDistribution(targetLS, r.societalUtilities);
            float uSocBase = WelfareMetrics.EvaluateDistribution(BaselineLS, r.societalUtilities);
            float uPersCurr = WelfareMetrics.GetUtilityForPerson(targetLS[i], r.personalUtilities);
            float uPersBase = WelfareMetrics.GetUtilityForPerson(BaselineLS[i], r.personalUtilities);

            totalBaseSocial += uSocBase; totalCurrSocial += uSocCurr;
            totalBasePersonal += uPersBase; totalCurrPersonal += uPersCurr;

            if (targetLS[i] > -0.9f && uSocCurr > uSocBase + 0.01f) happyCount++;
        }

        string pName = policyToDisplay != null ? policyToDisplay.policyName : "Current State";
        uiManager.UpdatePolicyInfo(policyToDisplay);
        uiManager.UpdateComparisonInfo(pName, totalBaseSocial, totalCurrSocial, totalBasePersonal, totalCurrPersonal, happyCount, count);
    }

    // --- CONTROLS ---
    public void SetAxisVariables(AxisVariable x, AxisVariable y) 
    { 
        xAxis = x; 
        yAxis = y; 
        RefreshGraphOnly();
    }

    public void SetFaceMode(FaceMode mode) 
    { 
        faceMode = mode; 
        RefreshGraphOnly();
    }
    public void SetGhostMode(bool enabled) { if (visuals) { visuals.SetGhostMode(enabled); UpdateSimulation(); } }
    public void SetArrowMode(bool enabled) { if (visuals) visuals.SetArrowMode(enabled); }

    public void OnHoverEnter(Respondent r)
    {
        if (r == null || CurrentLS == null || uiManager == null) return;
        
        // Highlight the visual
        if (visuals) visuals.SetHoverHighlight(r);

        int index = PopulationList.IndexOf(r);
        if (index == -1) return;

        // 1. Calculate Stats
        float cLS = CurrentLS[index];
        float uSelf = WelfareMetrics.GetUtilityForPerson(cLS, r.personalUtilities);
        float uSoc = WelfareMetrics.EvaluateDistribution(CurrentLS, r.societalUtilities);

        // 2. Build Rich Text String
        string info = $"<size=120%><b>Respondent #{r.id}</b></size>\n";
        info += $"<b>Life Satisfaction:</b> {cLS:F2}/10\n";
        info += $"<b>Personal Utility:</b> {uSelf:F2}\n";
        info += $"<b>Societal Utility:</b> {uSoc:F2}";
        
        // 3. Send to UI
        uiManager.UpdateHoverInfo(info);
    }

    public void OnHoverExit()
    {
        if (visuals) visuals.SetHoverHighlight(null);
        CalculateAndRefreshUI(CurrentLS, ActivePolicy); 
    }
}