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
            float startingLS = WelfareMetrics.GetWeightedRandomLS(onsDist);
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
        // The Policy class logic calculates the new LS array
        CurrentLS = p.ApplyPolicy(PopulationList.ToArray());
        
        UpdateSimulation();
    }

    // --- VISUAL UPDATES ---

    void UpdateSimulation()
    {
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
        
        // Update the Graph UI (Hover text, comparisons)
        if (uiManager) uiManager.UpdatePolicyInfo(ActivePolicy);
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
    
    // --- VISUAL CONTROLS (Passthrough) ---
    public void SetAxisVariables(AxisVariable x, AxisVariable y) { xAxis = x; yAxis = y; UpdateSimulation(); }
    public void SetFaceMode(FaceMode mode) { faceMode = mode; UpdateSimulation(); }
    public void SetGhostMode(bool enabled) { if (visuals) { visuals.SetGhostMode(enabled); UpdateSimulation(); } }
    public void SetArrowMode(bool enabled) { if (visuals) visuals.SetArrowMode(enabled); }
    
    public void OnHoverEnter(Respondent r) { if(uiManager) uiManager.UpdateHoverInfo($"ID: {r.id}"); if(visuals) visuals.SetHoverHighlight(r); } // Simplified for brevity
    public void OnHoverExit() { if(visuals) visuals.SetHoverHighlight(null); }
}