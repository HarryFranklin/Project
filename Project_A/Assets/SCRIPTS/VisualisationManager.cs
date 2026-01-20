using UnityEngine;
using System.Collections.Generic;

public class VisualisationManager : MonoBehaviour
{
    [Header("Scene References")]
    public Transform personContainer;
    public GameObject personPrefab;
    public GraphGrid graphGrid;
    public GraphAxisVisuals graphAxes;

    [Header("Visual Assets")]
    public Sprite faceGreen;
    public Sprite faceYellow;
    public Sprite faceRed;
    public Sprite faceDead;

    // Cache the instantiated visual objects
    private List<RespondentVisual> _respondentVisuals = new List<RespondentVisual>();

    // 1. Setup: Spawn the prefabs once
    public void CreatePopulation(List<Respondent> population, SimulationManager manager)
    {
        // Clear old if any
        foreach(var v in _respondentVisuals) Destroy(v.gameObject);
        _respondentVisuals.Clear();

        foreach (var respondent in population)
        {
            GameObject obj = Instantiate(personPrefab, personContainer);
            RespondentVisual visual = obj.GetComponent<RespondentVisual>();
            visual.Initialise(respondent, manager);
            _respondentVisuals.Add(visual);
        }
    }

    // 2. The Main Loop
    public void UpdateDisplay(Respondent[] population, float[] currentLS, float[] baselineLS, Policy activePolicy, AxisVariable xAxis, AxisVariable yAxis, FaceMode faceMode)
    {
        graphAxes.UpdateAxisVisuals(xAxis, yAxis);

        // Check if we are in "Comparison Mode" (Is a policy active?)
        bool isComparisonMode = (activePolicy != null);

        for (int i = 0; i < population.Length; i++)
        {
            Respondent r = population[i];
            float ls = currentLS[i];
            
            // --- 1. Calc Metrics
            
            // Current State
            float uSelfCurr = WelfareMetrics.GetUtilityForPerson(ls, r.personalUtilities);
            float uSocCurr = WelfareMetrics.EvaluateDistribution(currentLS, r.societalUtilities);
            
            // Baseline State
            float uSelfBase = WelfareMetrics.GetUtilityForPerson(baselineLS[i], r.personalUtilities);
            float uSocBase = WelfareMetrics.EvaluateDistribution(baselineLS, r.societalUtilities);

            // --- 2. Face sprite logic---
            Sprite leftSprite = faceYellow;
            Sprite rightSprite = faceYellow;

            if (ls <= -0.9f) // Dead
            {
                leftSprite = faceDead;
                rightSprite = faceDead;
            }
            else
            {
                if (isComparisonMode)
                {
                    // Comparison: Relative colours (Better/Worse)
                    leftSprite = GetRelativeSprite(uSelfCurr, uSelfBase);
                    rightSprite = GetRelativeSprite(uSocCurr, uSocBase);
                }
                else
                {
                    // Default: Absolute colours (Happy/Sad)
                    leftSprite = GetAbsoluteSprite(ls);
                    rightSprite = GetAbsoluteSocietySprite(uSocCurr); 
                }

                // Handle Face Modes (Split, or Single View)
                switch (faceMode)
                {
                    case FaceMode.PersonalWellbeing:
                        rightSprite = leftSprite; 
                        break;
                    case FaceMode.SocietalFairness:
                        leftSprite = rightSprite; 
                        break;
                }
            }

            // --- 3. Position logic ---
            Vector2 position;
            if (ls <= -0.9f) // is dead
            {
                position = graphGrid.GetGraveyardPosition(r.id);
            }
            else
            {
                // Pass baseline stats tocalculate Deltas
                float normX = GetNormalisedValue(ls, uSelfCurr, uSocCurr, uSelfBase, uSocBase, xAxis);
                float normY = GetNormalisedValue(ls, uSelfCurr, uSocCurr, uSelfBase, uSocBase, yAxis);
                
                position = graphGrid.GetPlotPosition(normX, normY, r.id);
            }

            // --- 4. Update Visuals ---
            _respondentVisuals[i].UpdateVisuals(position, leftSprite, rightSprite);
        }
    }

    // --- HELPER LOGIC ---

    // Default Mode: Absolute Life Satisfaction (0-10)
    private Sprite GetAbsoluteSprite(float ls)
    {
        // User Logic: Happy >= 6, Sad <= 4
        if (ls >= 6.0f) return faceGreen; 
        if (ls <= 4.0f) return faceRed;
        return faceYellow;
    }

    // Default Mode: Absolute Societal Fairness
    private Sprite GetAbsoluteSocietySprite(float uSoc)
    {
        // Thresholds for good or bad
        if (uSoc >= 0.6f) return faceGreen;
        if (uSoc <= 0.4f) return faceRed;
        return faceYellow;
    }

    // Comparison Mode: Relative Change
    private Sprite GetRelativeSprite(float current, float baseline)
    {
        float diff = current - baseline;
        
        // Use a small epsilon to avoid floating point flicker
        if (diff > 0.001f) return faceGreen;  // Improved
        if (diff < -0.001f) return faceRed;   // Worsened
        return faceYellow;                    // No significant change
    }

    private float GetNormalisedValue(float ls, float uSelf, float uSoc, float uSelfBase, float uSocBase, AxisVariable type)
    {
        float val = 0;
        
        switch(type) {
            case AxisVariable.LifeSatisfaction: 
                val = ls; 
                break;
                
            case AxisVariable.PersonalUtility: 
                val = uSelf; 
                break;
                
            case AxisVariable.SocietalFairness: 
                val = uSoc; 
                break;
                
            case AxisVariable.Wealth: 
                val = ls; 
                break;
            
            case AxisVariable.DeltaPersonalUtility: 
                val = uSelf - uSelfBase; 
                break;
                
            case AxisVariable.DeltaSocietalFairness: 
                val = uSoc - uSocBase; 
                break;
        }
        
        var range = graphAxes.GetRange(type);
        return Mathf.InverseLerp(range.min, range.max, val);
    }
}