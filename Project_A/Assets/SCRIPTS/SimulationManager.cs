using System.Collections.Generic;
using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("Architecture")]
    public VisualisationManager visuals; 
    public UIManager uiManager;
    public DataReader dataReader;

    [Header("Axis Configuration")]
    // Set these in the Inspector to control Boot state vs Hover state
    public AxisVariable defaultXAxis = AxisVariable.LifeSatisfaction;
    public AxisVariable defaultYAxis = AxisVariable.SocietalFairness;
    
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

    // Cache to restore state after hovering
    private AxisVariable _cachedX;
    private AxisVariable _cachedY;

    void Start()
    {
        // 1. Apply Defaults
        xAxis = defaultXAxis;
        yAxis = defaultYAxis;

        // 2. Load Data
        var respondentMap = dataReader.GetRespondents();
        PopulationList = new List<Respondent>(respondentMap.Values);

        int count = PopulationList.Count;
        BaselineLS = new float[count];
        CurrentLS = new float[count];

        // 3. Generate Initial State (Continuous ONS Data)
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
        
        // 2. Update the Visual Array
        CurrentLS = newValues;

        // 3. COMMIT: Save these changes to the People
        for (int i = 0; i < PopulationList.Count; i++)
        {
            PopulationList[i].currentLS = CurrentLS[i];
        }
        
        UpdateSimulation();
    }

    // --- METRIC HELPERS ---
    public float GetCurrentAvgLS()
    {
        if (CurrentLS == null || CurrentLS.Length == 0) return 0f;
        float sum = 0;
        foreach (var v in CurrentLS) sum += v;
        return sum / CurrentLS.Length;
    }

    public float GetCurrentSocietalFairness()
    {
        if (PopulationList == null || PopulationList.Count == 0) return 0f;
        
        double totalFairness = 0;
        for(int i=0; i<PopulationList.Count; i++)
        {
            // Calculate fairness for each person and average it
            totalFairness += WelfareMetrics.EvaluateDistribution(CurrentLS, PopulationList[i].societalUtilities);
        }
        return (float)(totalFairness / PopulationList.Count);
    }

    // --- PREVIEW LOGIC (Auto-Switches Axes) ---
    public void PreviewPolicy(Policy p)
    {
        // 1. Cache current state (so we can revert later)
        _cachedX = xAxis;
        _cachedY = yAxis;

        // 2. Switch to Preview Axes (Show Deltas)
        xAxis = previewXAxis;
        yAxis = previewYAxis;

        // 3. Calculate hypothetical future
        float[] tempLS = p.ApplyPolicy(PopulationList.ToArray());

        // 4. Show it visually
        if (visuals != null)
        {
            visuals.UpdateDisplay(PopulationList.ToArray(), tempLS, BaselineLS, p, xAxis, yAxis, faceMode);
        }

        // 5. Update Text stats
        CalculateAndRefreshUI(tempLS, p);
    }

    public void StopPreview()
    {
        // 1. Restore Axes to what they were before hovering
        xAxis = _cachedX;
        yAxis = _cachedY;

        // 2. Revert visuals to reality
        UpdateSimulation();
    }

    // --- INTERNAL UPDATES ---
    void UpdateSimulation()
    {
        // 1. Update Visuals
        if (visuals != null)
        {
            visuals.UpdateDisplay(PopulationList.ToArray(), CurrentLS, BaselineLS, ActivePolicy, xAxis, yAxis, faceMode);
        }

        // 2. Update UI
        CalculateAndRefreshUI(CurrentLS, ActivePolicy);
    }

    void CalculateAndRefreshUI(float[] targetLS, Policy policyToDisplay)
    {
        if (!uiManager) return;

        int count = PopulationList.Count; 
        
        double totalBaseSocial = 0; 
        double totalCurrSocial = 0;
        double totalBasePersonal = 0; 
        double totalCurrPersonal = 0;
        int happyCount = 0;

        for (int i = 0; i < count; i++)
        {
            Respondent r = PopulationList[i];
            float cLS = targetLS[i];
            float bLS = BaselineLS[i];

            float uSelfCurr = WelfareMetrics.GetUtilityForPerson(cLS, r.personalUtilities);
            float uSelfBase = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
            float uSocCurr = WelfareMetrics.EvaluateDistribution(targetLS, r.societalUtilities);
            float uSocBase = WelfareMetrics.EvaluateDistribution(BaselineLS, r.societalUtilities);

            totalBaseSocial += uSocBase; 
            totalCurrSocial += uSocCurr;
            totalBasePersonal += uSelfBase; 
            totalCurrPersonal += uSelfCurr;

            bool isHappy = false;
            if (cLS > -0.9f)
            {
                if (uSocCurr > uSocBase + 0.01f) isHappy = true;
            }
            if (isHappy) happyCount++;
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
        UpdateSimulation(); 
    }

    public void SetFaceMode(FaceMode mode)
    {
        faceMode = mode;
        UpdateSimulation();
    }

    public void SetGhostMode(bool enabled)
    {
        if (visuals) { visuals.SetGhostMode(enabled); UpdateSimulation(); }
    }

    public void SetArrowMode(bool enabled)
    {
        if (visuals) visuals.SetArrowMode(enabled);
    }

    public void OnHoverEnter(Respondent r)
    {
        if (r == null || CurrentLS == null || uiManager == null) return;
        if (visuals) visuals.SetHoverHighlight(r);

        int index = PopulationList.IndexOf(r);
        if (index == -1) return;

        float currentLS = CurrentLS[index];
        float uSelfCurrent = WelfareMetrics.GetUtilityForPerson(currentLS, r.personalUtilities);
        float uSociety = WelfareMetrics.EvaluateDistribution(CurrentLS, r.societalUtilities);

        string info = $"<size=120%><b>Respondent #{r.id}</b></size>\n";
        info += $"<b>Life Satisfaction:</b> {currentLS:F2}/10\n";
        info += $"<b>Personal Utility:</b> {uSelfCurrent:F2}\n";
        info += $"<b>Societal Utility:</b> {uSociety:F2}\n";
        
        uiManager.UpdateHoverInfo(info);
    }

    public void OnHoverExit()
    {
        if (visuals) visuals.SetHoverHighlight(null);
        CalculateAndRefreshUI(CurrentLS, ActivePolicy); 
    }
}