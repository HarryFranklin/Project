using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class SimulationManager : MonoBehaviour
{
    [Header("Architecture")]
    public VisualisationManager visuals; 
    public UIManager uiManager; // Kept for updating the graphs/faces
    public DataReader dataReader;

    [Header("Plotting Settings")]
    public AxisVariable xAxis = AxisVariable.LifeSatisfaction;
    public AxisVariable yAxis = AxisVariable.SocietalFairness;
    public FaceMode faceMode = FaceMode.Split;

    // --- INTERNAL DATA STATE ---
    // Made properties public (read-only) so TermManager can read stats
    public List<Respondent> PopulationList { get; private set; }
    public float[] CurrentLS { get; private set; }
    public float[] BaselineLS { get; private set; }
    
    public Policy ActivePolicy { get; private set; }

    void Start()
    {
        // 1. Load Data
        var respondentMap = dataReader.GetRespondents();
        PopulationList = new List<Respondent>(respondentMap.Values);

        int count = PopulationList.Count;
        BaselineLS = new float[count];
        CurrentLS = new float[count];

        // 2. Generate Initial State (ONS Data)
        float[] onsDist = WelfareMetrics.GetBaselineDistribution();
        for (int i = 0; i < count; i++)
        {
            float startingLS = WelfareMetrics.GetContinuousWeightedLS(onsDist);            
            BaselineLS[i] = startingLS;
            PopulationList[i].currentLS = startingLS; 
        }

        System.Array.Copy(BaselineLS, CurrentLS, count);

        // 3. Create Visuals
        if (visuals != null)
        {
            visuals.CreatePopulation(PopulationList, this);
        }

        UpdateSimulation();
    }

    // --- TERM MANAGER METHODS ---
    public void ApplyPolicyEffect(Policy p)
    {
        ActivePolicy = p;

        // 1. Calculate the new state
        float[] newValues = p.ApplyPolicy(PopulationList.ToArray());
        
        // 2. Update the Visual Array
        CurrentLS = newValues;

        // 3: Commit these changes to the People
        // This ensures next turn's preview starts from THIS turn's result.
        for (int i = 0; i < PopulationList.Count; i++)
        {
            PopulationList[i].currentLS = CurrentLS[i];
        }
        
        UpdateSimulation();
    }

    // --- VISUAL UPDATES ---

    void UpdateSimulation()
    {
        // 1. Update Visuals (Dots/Faces) using Real Data
        if (visuals != null)
        {
            visuals.UpdateDisplay(
                PopulationList.ToArray(),
                CurrentLS,
                BaselineLS,
                ActivePolicy,
                xAxis,
                yAxis,
                faceMode
            );
        }
        
        // 2. Update UI Text (Stats) using Real Data
        CalculateAndRefreshUI(CurrentLS, ActivePolicy);
    }

    // --- METRIC HELPERS ---
    
    public float GetCurrentAvgLS()
    {
        float sum = 0;
        foreach (var v in CurrentLS) sum += v;
        return sum / CurrentLS.Length;
    }

    public float GetCurrentSocietalFairness()
    {
        // Calculate average fairness across population
        double totalFairness = 0;
        for(int i=0; i<PopulationList.Count; i++)
        {
            totalFairness += WelfareMetrics.EvaluateDistribution(CurrentLS, PopulationList[i].societalUtilities);
        }
        return (float)(totalFairness / PopulationList.Count);
    }

    // --- PREVIEW LOGIC ---
    public void PreviewPolicy(Policy p)
    {
        // 1. Calculate hypothetical future
        float[] tempLS = p.ApplyPolicy(PopulationList.ToArray());

        // 2. Update Visuals using temp data
        if (visuals != null)
        {
            visuals.UpdateDisplay(
                PopulationList.ToArray(),
                tempLS,         // <--- Moving the dots
                BaselineLS,
                p,
                xAxis,
                yAxis,
                faceMode
            );
        }

        // 3. Update UI Text using temp data
        // This fixes the issue: Now the text stats will calculate based on tempLS
        CalculateAndRefreshUI(tempLS, p);
    }

    public void StopPreview()
    {
        // Revert everything to reality
        UpdateSimulation();
    }

    // We pass the LS array in as an argument so we can pass 'tempLS' or 'CurrentLS'
    private void CalculateAndRefreshUI(float[] targetLS, Policy policyToDisplay)
    {
        if (!uiManager) return;

        // Update the Static Description (Name, Cost, Rules)
        uiManager.UpdatePolicyInfo(policyToDisplay);

        // Calculate the Dynamic Stats (Fairness, Approval)
        int count = PopulationList.Count;
        double totalBaseSocial = 0; 
        double totalCurrSocial = 0;
        double totalBasePersonal = 0; 
        double totalCurrPersonal = 0;
        int happyCount = 0;

        for (int i = 0; i < count; i++)
        {
            Respondent r = PopulationList[i];
            float cLS = targetLS[i];       // <--- Using the argument (Temp or Real)
            float bLS = BaselineLS[i];     // Always comparing against Game Start

            // Calculate Utilities
            float uSelfCurr = WelfareMetrics.GetUtilityForPerson(cLS, r.personalUtilities);
            float uSelfBase = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
            float uSocCurr = WelfareMetrics.EvaluateDistribution(targetLS, r.societalUtilities);
            float uSocBase = WelfareMetrics.EvaluateDistribution(BaselineLS, r.societalUtilities);

            // Accumulate
            totalBaseSocial += uSocBase;
            totalCurrSocial += uSocCurr;
            totalBasePersonal += uSelfBase;
            totalCurrPersonal += uSelfCurr;

            // Simple Approval Logic (Example: Happy if Fairness improved)
            // You can make this complex later (e.g., Happy if LS > 6 OR Fairness > +0.1)
            bool isHappy = false;
            if (cLS > -0.9f) // If alive
            {
                if (uSocCurr > uSocBase + 0.01f) isHappy = true;
            }
            if (isHappy) happyCount++;
        }

        // Send to UI
        string pName = policyToDisplay != null ? policyToDisplay.policyName : "Default";
        
        uiManager.UpdateComparisonInfo(
            pName, 
            totalBaseSocial, 
            totalCurrSocial, 
            totalBasePersonal, 
            totalCurrPersonal, 
            happyCount, 
            count
        );
    }
    
    // --- VISUAL CONTROLS (Passthrough) ---
    public void SetAxisVariables(AxisVariable x, AxisVariable y) { xAxis = x; yAxis = y; UpdateSimulation(); }
    public void SetFaceMode(FaceMode mode) { faceMode = mode; UpdateSimulation(); }
    public void SetGhostMode(bool enabled) { if (visuals) { visuals.SetGhostMode(enabled); UpdateSimulation(); } }
    public void SetArrowMode(bool enabled) { if (visuals) visuals.SetArrowMode(enabled); }
    
    public void OnHoverEnter(Respondent r) { if(uiManager) uiManager.UpdateHoverInfo($"ID: {r.id}"); if(visuals) visuals.SetHoverHighlight(r); } // Simplified for brevity
    public void OnHoverExit() { if(visuals) visuals.SetHoverHighlight(null); }
}