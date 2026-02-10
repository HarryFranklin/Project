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

    // --- INTERNAL DATA STATE ---
    public List<Respondent> PopulationList { get; private set; }
    public float[] BaselineLS { get; private set; }
    public float[] CurrentLS { get; private set; }
    public Policy ActivePolicy { get; private set; }

    // Cache
    private AxisVariable _cachedX;
    private AxisVariable _cachedY;
    private bool _hasGameStarted = false; // Tracks if we have left the boot state

    void Start()
    {
        // 1. Apply Boot Defaults
        xAxis = bootXAxis;
        yAxis = bootYAxis;
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
        }

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
        _cachedX = xAxis;
        _cachedY = yAxis;

        xAxis = previewXAxis;
        yAxis = previewYAxis;

        float[] tempLS = p.ApplyPolicy(PopulationList.ToArray());

        if (visuals) visuals.UpdateDisplay(PopulationList.ToArray(), tempLS, BaselineLS, p, xAxis, yAxis, faceMode);
        CalculateAndRefreshUI(tempLS, p);
    }

    public void StopPreview()
    {
        xAxis = _cachedX;
        yAxis = _cachedY;
        UpdateSimulation();
    }

    // --- INTERNAL UPDATES ---
    void UpdateSimulation()
    {
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
    public void SetAxisVariables(AxisVariable x, AxisVariable y) { xAxis = x; yAxis = y; UpdateSimulation(); }
    public void SetFaceMode(FaceMode mode) { faceMode = mode; UpdateSimulation(); }
    public void SetGhostMode(bool enabled) { if (visuals) { visuals.SetGhostMode(enabled); UpdateSimulation(); } }
    public void SetArrowMode(bool enabled) { if (visuals) visuals.SetArrowMode(enabled); }

    public void OnHoverEnter(Respondent r)
    {
        if (r == null || CurrentLS == null || uiManager == null) return;
        if (visuals) visuals.SetHoverHighlight(r);
        int index = PopulationList.IndexOf(r);
        if (index == -1) return;

        float cLS = CurrentLS[index];
        string info = $"<size=120%><b>Respondent #{r.id}</b></size>\nLS: {cLS:F2}";
        uiManager.UpdateHoverInfo(info);
    }

    public void OnHoverExit()
    {
        if (visuals) visuals.SetHoverHighlight(null);
        CalculateAndRefreshUI(CurrentLS, ActivePolicy); 
    }
}