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

    // For OnHover Ghosting
    private bool _showGhostOverlay = false;

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
        bool isComparison = (activePolicy != null);
        
        // RULE: Ghost Mode only works if we are actually comparing something
        bool enableGhosts = _showGhostOverlay && isComparison; 

        for (int i = 0; i < population.Length; i++)
        {
            Respondent r = population[i];
            
            // 1. Current Math
            float cLS = currentLS[i];
            float cUSelf = WelfareMetrics.GetUtilityForPerson(cLS, r.personalUtilities);
            float cUSoc = WelfareMetrics.EvaluateDistribution(currentLS, r.societalUtilities);
            
            // 2. Baseline Maths (Step 1: Calculate "Previous" position)
            float bLS = baselineLS[i];
            float bUSelf = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
            float bUSoc = WelfareMetrics.EvaluateDistribution(baselineLS, r.societalUtilities);

            // 3. Sprites
            Sprite leftSpr, rightSpr;
            if (isComparison) {
                float bUSelf_ForSpr = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
                leftSpr = GetRelativeSprite(cUSelf, bUSelf_ForSpr);
                rightSpr = GetRelativeSprite(cUSoc, bUSoc);
            } else {
                leftSpr = GetAbsoluteSprite(cLS);
                rightSpr = GetAbsoluteSocietySprite(cUSoc);
            }
            
            // Ghost Sprites (Always Absolute "How I was")
            Sprite gLeft = GetAbsoluteSprite(bLS);
            Sprite gRight = GetAbsoluteSocietySprite(bUSoc);

            // 4. Positions
            // Calulate two positions: Current and Baseline
            Vector2 currPos = GetPos(r.id, cLS, cUSelf, cUSoc, cUSelf, cUSoc, xAxis, yAxis); // Pass Current for Current
            Vector2 basePos = GetPos(r.id, bLS, bUSelf, bUSoc, bUSelf, bUSoc, xAxis, yAxis); // Pass Baseline for Baseline

            // 5. Update Visual
            _respondentVisuals[i].UpdateVisuals(currPos, basePos, leftSpr, rightSpr, gLeft, gRight, enableGhosts);
        }
    }

    // --- HELPER LOGIC ---

    // Used for 
    private Vector2 GetPos(int id, float ls, float self, float soc, float baseSelf, float baseSoc, AxisVariable x, AxisVariable y)
    {
        if (ls <= -0.9f) return graphGrid.GetGraveyardPosition(id);
        float nx = GetNormalisedValue(ls, self, soc, baseSelf, baseSoc, x); 
        float ny = GetNormalisedValue(ls, self, soc, baseSelf, baseSoc, y);
        return graphGrid.GetPlotPosition(nx, ny, id);
    }

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

    public void SetGhostMode(bool enable)
    {
        // SimulationManager will trigger a refresh next frame
        _showGhostOverlay = enable;
    }
}