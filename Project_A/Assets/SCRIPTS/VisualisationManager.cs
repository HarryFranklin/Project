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
            
            // Calculate Metrics
            float uSelfCurr = WelfareMetrics.GetUtilityForPerson(ls, r.personalUtilities);
            float uSelfBase = WelfareMetrics.GetUtilityForPerson(baselineLS[i], r.personalUtilities);
            
            float uSocCurr = WelfareMetrics.EvaluateDistribution(currentLS, r.societalUtilities);
            float uSocBase = WelfareMetrics.EvaluateDistribution(baselineLS, r.societalUtilities);

            // Default
            Sprite leftSprite = faceYellow;
            Sprite rightSprite = faceYellow;

            if (ls <= -0.9f) // Dead for now
            {
                leftSprite = faceDead;
                rightSprite = faceDead;
            }
            else
            {
                // 1. Determine logic
                if (isComparisonMode)
                {
                    // --- COMPARISON MODE ---
                    // Left: Did *I* get better/worse? (Delta uSelf)
                    // Right: Did *Society* get better/worse? (Delta uOthers)
                    leftSprite = GetRelativeSprite(uSelfCurr, uSelfBase);
                    rightSprite = GetRelativeSprite(uSocCurr, uSocBase);
                }
                else
                {
                    // --- DEFAULT MODE  ---
                    // Left: Am I happy? (Absolute LS)
                    // Right: Is society fair? (Absolute uOthers)
                    leftSprite = GetAbsoluteSprite(ls);
                    rightSprite = GetAbsoluteSocietySprite(uSocCurr); 
                }

                // 2. Logic for Left or Right
                switch (faceMode)
                {
                    case FaceMode.PersonalWellbeing:
                        rightSprite = leftSprite; // Show Self on both sides
                        break;

                    case FaceMode.SocietalFairness:
                        leftSprite = rightSprite; // Show Society on both sides
                        break;
                    
                    // Case: Do nothing, keep them separate.
                }
            }

            // 3. Position Logic
            Vector2 position;
            if (ls <= -0.9f)
            {
                position = graphGrid.GetGraveyardPosition(r.id);
            }
            else
            {
                float normX = GetNormalisedValue(ls, uSelfCurr, uSocCurr, xAxis);
                float normY = GetNormalisedValue(ls, uSelfCurr, uSocCurr, yAxis);
                position = graphGrid.GetPlotPosition(normX, normY, r.id);
            }

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

    private float GetNormalisedValue(float ls, float uSelf, float uSoc, AxisVariable type)
    {
        float val = 0;
        switch(type) {
            case AxisVariable.LifeSatisfaction: val = ls; break;
            case AxisVariable.PersonalUtility: val = uSelf; break;
            case AxisVariable.SocietalFairness: val = uSoc; break;
            case AxisVariable.Wealth: val = ls; break; 
        }
        var range = graphAxes.GetRange(type);
        return Mathf.InverseLerp(range.min, range.max, val);
    }
}