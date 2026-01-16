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
    public Sprite faceHappy, faceNeutral, faceSad, faceDead;

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

    // 2. Update positions and sprites based on data provided by SimManager
    public void UpdateDisplay(Respondent[] population, float[] currentLS, float[] baselineLS, Policy activePolicy, AxisVariable xAxis, AxisVariable yAxis)
    {
        // Update Axes first
        graphAxes.UpdateAxisVisuals(xAxis, yAxis);

        for (int i = 0; i < population.Length; i++)
        {
            Respondent r = population[i];
            float ls = currentLS[i];
            
            // 1. Calculate Data for Plotting (Using Helper)
            float uSelf = WelfareMetrics.GetUtilityForPerson(ls, r.personalUtilities);
            float uSocCurr = WelfareMetrics.EvaluateDistribution(currentLS, r.societalUtilities);
            
            // 2. Determine Sprite (Visual Logic)
            // Calc logic here for now but SimManager could also do it
            Sprite face = DetermineFace(r, ls, uSelf, baselineLS, currentLS, activePolicy);

            // 3. Determine Position (Graph Logic)
            Vector2 position;
            if (ls <= -0.9f) // Death Check
            {
                position = graphGrid.GetGraveyardPosition(r.id);
            }
            else
            {
                float normX = GetNormalisedValue(ls, uSelf, uSocCurr, xAxis);
                float normY = GetNormalisedValue(ls, uSelf, uSocCurr, yAxis);
                position = graphGrid.GetPlotPosition(normX, normY, r.id);
            }

            // D. Apply
            _respondentVisuals[i].UpdateVisuals(position, face);
        }
    }

    // Visual Helpers
    private float GetNormalisedValue(float ls, float uSelf, float uSoc, AxisVariable type)
    {
        float val = 0;
        switch(type) {
            case AxisVariable.LifeSatisfaction: val = ls; break;
            case AxisVariable.PersonalUtility: val = uSelf; break;
            case AxisVariable.SocietalFairness: val = uSoc; break;
        }
        var range = graphAxes.GetRange(type);
        return Mathf.InverseLerp(range.min, range.max, val);
    }

    private Sprite DetermineFace(Respondent r, float ls, float uSelf, float[] baseLS, float[] currLS, Policy policy)
    {
        // 1. Death Check (Float safety)
        if (ls <= -0.9f) return faceDead;

        float uSocBase = WelfareMetrics.EvaluateDistribution(baseLS, r.societalUtilities);
        float uSocCurr = WelfareMetrics.EvaluateDistribution(currLS, r.societalUtilities);

        if (policy == null) // Status Quo
        {
            // Absolute Happiness (Thresholds are now floats)
            if (ls >= 8.0f) return faceHappy;
            if (ls <= 3.0f) return faceSad;

            // Relative Happiness (Gap between Self and Society)
            float gap = uSelf - uSocBase;
            if (gap > 0.05f) return faceHappy; 
            if (gap < -0.05f) return faceSad; 
            return faceNeutral;
        }
        else // Comparison Mode
        {
            // Has society improved?
            if (uSocCurr > uSocBase + 0.01f) return faceHappy;
            if (uSocCurr < uSocBase - 0.01f) return faceSad;
            return faceNeutral;
        }
    }
}