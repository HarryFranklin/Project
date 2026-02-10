using UnityEngine;
using System.Collections.Generic;

public class VisualisationManager : MonoBehaviour
{
    [Header("Scene References")]
    public Transform personContainer;
    public GameObject personPrefab;
    public GraphGrid graphGrid;
    public GraphAxisVisuals graphAxes;

    [Header("Graph Settings")]
    [Tooltip("For Delta views, the axis will be locked to +/- this value (e.g. -1 to +1).")]
    public float deltaScaleRange = 1.0f;

    [Header("Visual Assets")]
    public Sprite faceGreen, faceYellow, faceRed, faceDead;

    // Cache
    private float[] _cacheXValues;
    private float[] _cacheYValues;
    private const float STACK_BIN_SIZE = 0.25f; 
    private RespondentVisual[] _activeVisualsArray; 
    private List<RespondentVisual> _visualsList = new List<RespondentVisual>(); 
    private bool _showGhostOverlay = false;

    // --- Setup ---
    public void CreatePopulation(List<Respondent> population, SimulationManager manager)
    {
        if (_visualsList.Count > 0)
        {
            foreach(var v in _visualsList) if(v) Destroy(v.gameObject);
            _visualsList.Clear();
        }

        foreach (var respondent in population)
        {
            GameObject obj = Instantiate(personPrefab, personContainer);
            RespondentVisual visual = obj.GetComponent<RespondentVisual>();
            visual.Initialise(respondent, manager);
            _visualsList.Add(visual);
        }

        _activeVisualsArray = _visualsList.ToArray();
        _cacheXValues = new float[population.Count];
        _cacheYValues = new float[population.Count];
    }

    // --- Update ---
    void Update()
    {
        if (_activeVisualsArray == null) return;
        float dt = Time.deltaTime; 
        for (int i = 0; i < _activeVisualsArray.Length; i++) _activeVisualsArray[i].ManualUpdate(dt);
    }

    // --- Display Logic ---
    public void UpdateDisplay(Respondent[] population, float[] currentLS, float[] baselineLS, Policy activePolicy, AxisVariable xAxis, AxisVariable yAxis, FaceMode faceMode)
    {
        int count = population.Length;
        if (_cacheXValues == null || _cacheXValues.Length != count)
        {
            _cacheXValues = new float[count];
            _cacheYValues = new float[count];
        }

        bool isStackMode = (yAxis == AxisVariable.Stack);
        Dictionary<int, int> binCount = isStackMode ? new Dictionary<int, int>() : null;

        float xMin = float.MaxValue, xMax = float.MinValue;
        float yMin = float.MaxValue, yMax = float.MinValue;

        // Pass 1: Calculation
        for (int i = 0; i < count; i++)
        {
            Respondent r = population[i];
            float cLS = currentLS[i];
            float bLS = baselineLS[i];

            float valX = CalculateAxisValue(xAxis, r, cLS, bLS, currentLS, baselineLS);
            float valY = 0;

            if (isStackMode)
            {
                float snappedX = Mathf.Round(valX / STACK_BIN_SIZE) * STACK_BIN_SIZE;
                int binKey = Mathf.RoundToInt(snappedX * 100);

                if (!binCount.ContainsKey(binKey)) binCount[binKey] = 0;
                valY = binCount[binKey];
                binCount[binKey]++;
                valX = snappedX;
            }
            else
            {
                valY = CalculateAxisValue(yAxis, r, cLS, bLS, currentLS, baselineLS);            
            }

            _cacheXValues[i] = valX;
            _cacheYValues[i] = valY;

            if (cLS > -0.9f) 
            {
                if (valX < xMin) xMin = valX;
                if (valX > xMax) xMax = valX;
                if (valY < yMin) yMin = valY;
                if (valY > yMax) yMax = valY;
            }
        }

        GetFinalRange(xAxis, ref xMin, ref xMax);
        GetFinalRange(yAxis, ref yMin, ref yMax);
        graphAxes.UpdateAxisVisuals(xAxis, xMin, xMax, yAxis, yMin, yMax);

        // Pass 2: Draw
        bool isComparisonMode = (activePolicy != null);
        
        // Disable ghosts in stack mode
        bool enableGhosts = _showGhostOverlay && isComparisonMode && !isStackMode;

        for (int i = 0; i < count; i++)
        {
            Respondent r = population[i];
            float cLS = currentLS[i];
            
            float cUSelf = WelfareMetrics.GetUtilityForPerson(cLS, r.personalUtilities);
            float cUSoc = WelfareMetrics.EvaluateDistribution(currentLS, r.societalUtilities);
            float bLS = baselineLS[i];
            float bUSelf = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
            float bUSoc = WelfareMetrics.EvaluateDistribution(baselineLS, r.societalUtilities);

            Sprite leftSprite = faceYellow, rightSprite = faceYellow;
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

            Vector2 currPos = GetPos(r.id, _cacheXValues[i], _cacheYValues[i], xMin, xMax, yMin, yMax);
            Vector2 basePos = currPos; // Default to no movement

            if (!isStackMode)
            {
                // Only calculate previous position if NOT in stack mode
                float baseX = CalculateAxisValue(xAxis, r, bLS, bLS, baselineLS, baselineLS); 
                float baseY = CalculateAxisValue(yAxis, r, bLS, bLS, baselineLS, baselineLS);
                basePos = GetPos(r.id, baseX, baseY, xMin, xMax, yMin, yMax);
            }
            // In Stack Mode, basePos == currPos, so magnitude is 0, so Arrows are hidden automatically.

            _activeVisualsArray[i].UpdateVisuals(currPos, basePos, leftSprite, rightSprite, gLeft, gRight, enableGhosts);
        }
    }

    // --- Helpers ---
    private float CalculateAxisValue(AxisVariable type, Respondent r, float ls, float baseLS, float[] popLS, float[] popBaseLS)
    {
        switch (type)
        {
            case AxisVariable.LifeSatisfaction: return ls;
            case AxisVariable.PersonalUtility: return WelfareMetrics.GetUtilityForPerson(ls, r.personalUtilities);
            case AxisVariable.SocietalFairness: return WelfareMetrics.EvaluateDistribution(popLS, r.societalUtilities);
            case AxisVariable.Wealth: return ls;
            case AxisVariable.DeltaPersonalUtility: 
                return WelfareMetrics.GetUtilityForPerson(ls, r.personalUtilities) - 
                       WelfareMetrics.GetUtilityForPerson(baseLS, r.personalUtilities);
            case AxisVariable.DeltaSocietalFairness:
                return WelfareMetrics.EvaluateDistribution(popLS, r.societalUtilities) - 
                       WelfareMetrics.EvaluateDistribution(popBaseLS, r.societalUtilities);
            default: return 0;
        }
    }

    private void GetFinalRange(AxisVariable type, ref float min, ref float max)
    {
        // 1. Fixed Axes (Absolute)
        if (type == AxisVariable.LifeSatisfaction || type == AxisVariable.Wealth) 
        { 
            min = 0; max = 10; return; 
        }
        if (type == AxisVariable.PersonalUtility || type == AxisVariable.SocietalFairness) 
        { 
            min = 0; max = 1; return; 
        }

        // 2. Fixed Delta Scales
        // Instead of calculating min/max from the data, force it to a fixed window.
        if (type == AxisVariable.DeltaPersonalUtility || type == AxisVariable.DeltaSocietalFairness) 
        { 
            min = -deltaScaleRange; 
            max = deltaScaleRange; 
            return; 
        }

        // 3. Stack Range (Dynamic Y)
        if (type == AxisVariable.Stack)
        {
            min = 0; 
            max = Mathf.Max(max, 5f); 
            return;
        }

        // 4. Fallback (Dynamic Auto-Scaling for unknown types)
        if (min == float.MaxValue || max == float.MinValue || (min == 0 && max == 0)) 
        { 
            min = -0.1f; max = 0.1f; return; 
        }
        
        float absMax = Mathf.Max(Mathf.Abs(min), Mathf.Abs(max)) * 1.05f;
        min = -absMax; max = absMax;
    }

    private Vector2 GetPos(int id, float valX, float valY, float xMin, float xMax, float yMin, float yMax)
    {        
        // Clamp values to the visual range so dots don't fly off-screen
        float clampedX = Mathf.Clamp(valX, xMin, xMax);
        float clampedY = Mathf.Clamp(valY, yMin, yMax);

        float normX = Mathf.InverseLerp(xMin, xMax, clampedX);
        float normY = Mathf.InverseLerp(yMin, yMax, clampedY);
        
        return graphGrid.GetPlotPosition(normX, normY, id);
    }

    // Sprite Helpers
    private Sprite GetAbsoluteSprite(float ls) { if (ls >= 6.0f) return faceGreen; if (ls <= 4.0f) return faceRed; return faceYellow; }
    private Sprite GetAbsoluteSocietySprite(float uSoc) { if (uSoc >= 0.6f) return faceGreen; if (uSoc <= 0.4f) return faceRed; return faceYellow; }
    private Sprite GetRelativeSprite(float current, float baseline) { float diff = current - baseline; if (diff > 0.001f) return faceGreen; if (diff < -0.001f) return faceRed; return faceYellow; }

    public void SetHoverHighlight(Respondent target)
    {
        bool anyoneHovered = (target != null);
        if (_activeVisualsArray != null)
        {
            for (int i = 0; i < _activeVisualsArray.Length; i++)
            {
                var v = _activeVisualsArray[i];
                v.SetFocusState(target != null && v.data.id == target.id, anyoneHovered);
            }
        }
    }
    public void SetGhostMode(bool enable) { _showGhostOverlay = enable; }
    public void SetArrowMode(bool showAll) { if (_activeVisualsArray != null) foreach(var v in _activeVisualsArray) v.SetArrowState(showAll); }
}