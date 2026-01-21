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

    // Cache arrays
    private float[] _cacheXValues;
    private float[] _cacheYValues;

    // Optimisation - array is faster than list
    private RespondentVisual[] _activeVisualsArray; 
    private List<RespondentVisual> _visualsList = new List<RespondentVisual>(); // Keep list for easy adding/removing

    // State
    private bool _showGhostOverlay = false;

    // --- 1. Setup ---
    public void CreatePopulation(List<Respondent> population, SimulationManager manager)
    {
        // Clear old objects
        if (_visualsList.Count > 0)
        {
            foreach(var v in _visualsList) if(v) Destroy(v.gameObject);
            _visualsList.Clear();
        }

        // Spawn new ones
        foreach (var respondent in population)
        {
            GameObject obj = Instantiate(personPrefab, personContainer);
            RespondentVisual visual = obj.GetComponent<RespondentVisual>();
            visual.Initialise(respondent, manager);
            _visualsList.Add(visual);
        }

        // Convert to array as they're faster
        _activeVisualsArray = _visualsList.ToArray();

        // Init Cache
        int count = population.Count;
        _cacheXValues = new float[count];
        _cacheYValues = new float[count];
    }

    // --- 2. Manager Update Loop ---
    void Update()
    {
        // If we haven't spawned yet, do nothing
        if (_activeVisualsArray == null) return;

        // Cache DeltaTime once per frame
        float dt = Time.deltaTime; 

        // Loop through the Array
        for (int i = 0; i < _activeVisualsArray.Length; i++)
        {
            // Call the manual update on the visual.
            _activeVisualsArray[i].ManualUpdate(dt);
        }
    }

    // --- 3. Display Logic
    public void UpdateDisplay(Respondent[] population, float[] currentLS, float[] baselineLS, Policy activePolicy, AxisVariable xAxis, AxisVariable yAxis, FaceMode faceMode)
    {
        int count = population.Length;
        if (_cacheXValues == null || _cacheXValues.Length != count)
        {
            _cacheXValues = new float[count];
            _cacheYValues = new float[count];
        }

        // Pass 1: Calculcate Values and Find Ranges
        float xMin = float.MaxValue, xMax = float.MinValue;
        float yMin = float.MaxValue, yMax = float.MinValue;

        for (int i = 0; i < count; i++)
        {
            Respondent r = population[i];

            // 1. Calculate Metrics ONCE
            float cLS = currentLS[i];
            float bLS = baselineLS[i];

            // Only calculate costly metrics if we actually need them for the axis
            // (Helper function handles the math)
            float valX = CalculateAxisValue(xAxis, r, cLS, bLS, currentLS, baselineLS);
            float valY = CalculateAxisValue(yAxis, r, cLS, bLS, currentLS, baselineLS);

            // 2. Store in Cache
            _cacheXValues[i] = valX;
            _cacheYValues[i] = valY;

            // 3. Track Min/Max (Ignore dead people for range calculation to keep it clean?)
            if (cLS > -0.9f) 
            {
                if (valX < xMin) xMin = valX;
                if (valX > xMax) xMax = valX;
                if (valY < yMin) yMin = valY;
                if (valY > yMax) yMax = valY;
            }
        }

        // --- Process Ranges, Dynamic vs Fixed
        GetFinalRange(xAxis, ref xMin, ref xMax);
        GetFinalRange(yAxis, ref yMin, ref yMax);

        // --- Update Axes ---
        graphAxes.UpdateAxisVisuals(xAxis, xMin, xMax, yAxis, yMin, yMax);

        // --- Pass 2: Draw Visuals ---
        bool isComparisonMode = (activePolicy != null);
        bool enableGhosts = _showGhostOverlay && isComparisonMode; 

        for (int i = 0; i < count; i++)
        {
            Respondent r = population[i];
            float cLS = currentLS[i];
            
            float cUSelf = WelfareMetrics.GetUtilityForPerson(cLS, r.personalUtilities);
            float cUSoc = WelfareMetrics.EvaluateDistribution(currentLS, r.societalUtilities);
            float bLS = baselineLS[i];
            float bUSelf = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
            float bUSoc = WelfareMetrics.EvaluateDistribution(baselineLS, r.societalUtilities);

            // Sprite Selection
            Sprite leftSprite = faceYellow;
            Sprite rightSprite = faceYellow;
            if (cLS <= -0.9f) { leftSprite = rightSprite = faceDead; }
            else
            {
                if (isComparisonMode)
                {
                    float bUSelfForSpr = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
                    leftSprite = GetRelativeSprite(cUSelf, bUSelfForSpr);
                    rightSprite = GetRelativeSprite(cUSoc, bUSoc);
                }
                else
                {
                    leftSprite = GetAbsoluteSprite(cLS);
                    rightSprite = GetAbsoluteSocietySprite(cUSoc); 
                }
                if (faceMode == FaceMode.PersonalWellbeing) rightSprite = leftSprite;
                if (faceMode == FaceMode.SocietalFairness) leftSprite = rightSprite;
            }

            Sprite gLeft = GetAbsoluteSprite(bLS);
            Sprite gRight = GetAbsoluteSocietySprite(bUSoc);

            // Positioning: Use the Cached values and the Calculated Ranges
            float baseX = CalculateAxisValue(xAxis, r, bLS, bLS, baselineLS, baselineLS); // Baseline args
            float baseY = CalculateAxisValue(yAxis, r, bLS, bLS, baselineLS, baselineLS);

            Vector2 currPos = GetPos(r.id, _cacheXValues[i], _cacheYValues[i], xMin, xMax, yMin, yMax);
            Vector2 basePos = GetPos(r.id, baseX, baseY, xMin, xMax, yMin, yMax);

            _activeVisualsArray[i].UpdateVisuals(currPos, basePos, leftSprite, rightSprite, gLeft, gRight, enableGhosts);
        }
    }

    public void SetHoverHighlight(Respondent target)
    {
        bool anyoneHovered = (target != null);
        
        // Loop through array for speed
        if (_activeVisualsArray != null)
        {
            for (int i = 0; i < _activeVisualsArray.Length; i++)
            {
                var v = _activeVisualsArray[i];
                bool isTarget = (target != null && v.data.id == target.id);
                v.SetFocusState(isTarget, anyoneHovered);
            }
        }
    }

    public void SetGhostMode(bool enable)
    {
        _showGhostOverlay = enable;
    }

    public void SetArrowMode(bool showAll)
    {
        if (_activeVisualsArray == null) return;

        for (int i = 0; i < _activeVisualsArray.Length; i++)
        {
            _activeVisualsArray[i].SetArrowState(showAll);
        }
    }

    // --- HELPERS ---

    // Calculate the axis value
    private float CalculateAxisValue(AxisVariable type, Respondent r, float ls, float baseLS, float[] popLS, float[] popBaseLS)
    {
        switch (type)
        {
            case AxisVariable.LifeSatisfaction: return ls;
            case AxisVariable.PersonalUtility: return WelfareMetrics.GetUtilityForPerson(ls, r.personalUtilities);
            case AxisVariable.SocietalFairness: return WelfareMetrics.EvaluateDistribution(popLS, r.societalUtilities);
            case AxisVariable.Wealth: return ls; // Placeholder
            
            case AxisVariable.DeltaPersonalUtility: 
                return WelfareMetrics.GetUtilityForPerson(ls, r.personalUtilities) - 
                       WelfareMetrics.GetUtilityForPerson(baseLS, r.personalUtilities);
            
            case AxisVariable.DeltaSocietalFairness:
                return WelfareMetrics.EvaluateDistribution(popLS, r.societalUtilities) - 
                       WelfareMetrics.EvaluateDistribution(popBaseLS, r.societalUtilities);
            
            default: return 0;
        }
    }

    // Calculate the range
    private void GetFinalRange(AxisVariable type, ref float min, ref float max)
    {
        // 1. Fixed Ranges (Absolute)
        if (type == AxisVariable.LifeSatisfaction || type == AxisVariable.Wealth) { min = 0; max = 10; return; }
        if (type == AxisVariable.PersonalUtility || type == AxisVariable.SocietalFairness) { min = 0; max = 1; return; }

        // 2. Dynamic Ranges (Deltas)
        // If everything is zero (no policy), default to -0.1 to 0.1
        if (min == float.MaxValue || max == float.MinValue || (min == 0 && max == 0))
        {
            min = -0.1f; max = 0.1f;
            return;
        }

        // Symmetry: Delta graphs look best if 0 is in the middle.
        // Find the biggest deviation from zero.
        float absMax = Mathf.Max(Mathf.Abs(min), Mathf.Abs(max));
        
        // Add padding so points aren't on the edge
        absMax *= 1.05f;

        // Clamp to a sensible minimum so we don't zoom in to tiny deltas
        if (absMax < 0.05f)
        {
            // Tiny values - clamp minimum to 0.05
            absMax = 0.05f; 
        }
        else
        {
            // Standard values - Round to nearest 0.1
            absMax = Mathf.Ceil(absMax * 10f) / 10f;
        }

        min = -absMax;
        max = absMax;
    }

    // Accepts baseline args for Delta graphs
    private Vector2 GetPos(int id, float valX, float valY, float xMin, float xMax, float yMin, float yMax)
    {        
        float normX = Mathf.InverseLerp(xMin, xMax, valX);
        float normY = Mathf.InverseLerp(yMin, yMax, valY);
        return graphGrid.GetPlotPosition(normX, normY, id);
    }

    // Sprite Helpers
    private Sprite GetAbsoluteSprite(float ls)
    {
        if (ls >= 6.0f) return faceGreen; 
        if (ls <= 4.0f) return faceRed;
        return faceYellow;
    }

    private Sprite GetAbsoluteSocietySprite(float uSoc)
    {
        if (uSoc >= 0.6f) return faceGreen;
        if (uSoc <= 0.4f) return faceRed;
        return faceYellow;
    }

    private Sprite GetRelativeSprite(float current, float baseline)
    {
        float diff = current - baseline;
        if (diff > 0.001f) return faceGreen;
        if (diff < -0.001f) return faceRed;
        return faceYellow;
    }
}