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
    public Sprite faceGreen, faceYellow, faceRed, faceDead;

    [Header("Clustering Settings")]
    public int clusterCount = 4;
    public bool showClusterGizmos = true;

    [Header("Cluster Visuals")]
    public GameObject clusterPrefab;
    private List<ClusterVisual> _spawnedClusters = new List<ClusterVisual>();
    
    // --- CLUSTER DATA (Read this from the UI) ---
    [System.Serializable]
    public class GroupOpinion
    {
        public int id;
        public string name; // e.g., "The Disenfranchised"
        public int size;
        public Vector2 visualCenter; // Normalized 0-1 position
        
        // The "Thoughts"
        public float avgLS;
        public float avgSocietalFairness;
        public float[] avgSocietalUtilities; // The collective curve
    }
    public List<GroupOpinion> CurrentClusters = new List<GroupOpinion>();

    // Cache
    private float[] _cacheXValues;
    private float[] _cacheYValues;
    private const float STACK_BIN_SIZE = 0.25f; 
    private RespondentVisual[] _activeVisualsArray; 
    private List<RespondentVisual> _visualsList = new List<RespondentVisual>(); 
    private bool _showGhostOverlay = false;

    // --- 1. Setup ---
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

    // --- 2. Update ---
    void Update()
    {
        if (_activeVisualsArray == null) return;
        float dt = Time.deltaTime; 
        for (int i = 0; i < _activeVisualsArray.Length; i++) _activeVisualsArray[i].ManualUpdate(dt);
    }

    // --- 3. Display Logic ---
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

        // --- PASS 1: CALC VALUES ---
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

        // --- PASS 2: DRAW & UPDATE VISUALS ---
        bool isComparisonMode = (activePolicy != null);
        bool enableGhosts = _showGhostOverlay && isComparisonMode && !isStackMode;

        for (int i = 0; i < count; i++)
        {
            Respondent r = population[i];
            float cLS = currentLS[i];

            // Sprites
            float cUSelf = WelfareMetrics.GetUtilityForPerson(cLS, r.personalUtilities);
            float cUSoc = WelfareMetrics.EvaluateDistribution(currentLS, r.societalUtilities);
            float bLS = baselineLS[i];
            float bUSelf = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
            float bUSoc = WelfareMetrics.EvaluateDistribution(baselineLS, r.societalUtilities);

            Sprite left = faceYellow, right = faceYellow;
            if (cLS <= -0.9f) { left = right = faceDead; }
            else
            {
                if (isComparisonMode) {
                    left = GetRelativeSprite(cUSelf, bUSelf);
                    right = GetRelativeSprite(cUSoc, bUSoc);
                } else {
                    left = GetAbsoluteSprite(cLS);
                    right = GetAbsoluteSocietySprite(cUSoc); 
                }
            }

            // Positions
            Vector2 currPos = GetPos(r.id, _cacheXValues[i], _cacheYValues[i], xMin, xMax, yMin, yMax);
            Vector2 basePos = currPos; 

            if (!isStackMode) {
                float baseX = CalculateAxisValue(xAxis, r, bLS, bLS, baselineLS, baselineLS); 
                float baseY = CalculateAxisValue(yAxis, r, bLS, bLS, baselineLS, baselineLS);
                basePos = GetPos(r.id, baseX, baseY, xMin, xMax, yMin, yMax);
            }

            _activeVisualsArray[i].UpdateVisuals(currPos, basePos, left, right, GetAbsoluteSprite(bLS), GetAbsoluteSocietySprite(bUSoc), enableGhosts);
        }

        // --- PASS 3: CLUSTERING ---
        // Pass the Min/Max ranges so we can normalise data before clustering
        RecalculateClusters(population, currentLS, xMin, xMax, yMin, yMax);
    }

    // --- CLUSTERING LOGIC ---
    private void RecalculateClusters(Respondent[] population, float[] currentLS, float xMin, float xMax, float yMin, float yMax)
    {
        int count = population.Length;
        Vector2[] normPoints = new Vector2[count];

        // 1. Prepare Normalized Points
        for (int i = 0; i < count; i++)
        {
            float nX = Mathf.InverseLerp(xMin, xMax, _cacheXValues[i]);
            float nY = Mathf.InverseLerp(yMin, yMax, _cacheYValues[i]);
            normPoints[i] = new Vector2(nX, nY);
        }

        // 2. Run Math
        KMeans.Result result = KMeans.GetClusters(normPoints, clusterCount);

        // 3. Update Data Structure
        CurrentClusters.Clear();
        for (int k = 0; k < clusterCount; k++) {
            CurrentClusters.Add(new GroupOpinion { 
                id = k, 
                visualCenter = result.Centres[k], 
                avgSocietalUtilities = new float[6] 
            });
        }

        // 4. Sum Data
        for (int i = 0; i < count; i++)
        {
            int cId = result.Assignments[i];
            GroupOpinion g = CurrentClusters[cId];
            Respondent r = population[i];

            g.size++;
            g.avgLS += r.currentLS;
            g.avgSocietalFairness += WelfareMetrics.EvaluateDistribution(currentLS, r.societalUtilities);
            for(int j=0; j<6; j++) g.avgSocietalUtilities[j] += r.societalUtilities[j];
        }

        // 5. Finalize & Name
        foreach (var g in CurrentClusters)
        {
            if (g.size > 0)
            {
                g.avgLS /= g.size;
                g.avgSocietalFairness /= g.size;
                for(int j=0; j<6; j++) g.avgSocietalUtilities[j] /= g.size;

                // Simple Naming Logic
                if (g.avgLS >= 7.5f) g.name = "The Thriving";
                else if (g.avgLS <= 4.5f) g.name = "The Struggling";
                else if (g.avgSocietalFairness < 0.3f) g.name = "The Cynics";
                else g.name = "The Moderates";
            }
        }

        // --- SPAWN/UPDATE VISUALS ---
        UpdateClusterObjects();
    }

    private void UpdateClusterObjects()
    {
        if (!clusterPrefab) return;

        // 1. Spawn logic
        while (_spawnedClusters.Count < CurrentClusters.Count)
        {
            GameObject obj = Instantiate(clusterPrefab, personContainer);
            ClusterVisual component = obj.GetComponent<ClusterVisual>();
            if (component == null) { Destroy(obj); return; }
            _spawnedClusters.Add(component);
        }

        // 2. Position logic
        for (int i = 0; i < _spawnedClusters.Count; i++)
        {
            ClusterVisual vis = _spawnedClusters[i];
            if (vis == null) continue;

            if (i < CurrentClusters.Count && CurrentClusters[i].size > 0)
            {
                vis.gameObject.SetActive(true);
                GroupOpinion g = CurrentClusters[i];

                if (graphGrid != null)
                {
                    // Get the plotted position (This is likely a Local Coordinate)
                    Vector3 plotPos = graphGrid.GetPlotPosition(g.visualCenter.x, g.visualCenter.y, 0);
                    
                    // We treat the plotPos as local to the container, just like the faces.
                    vis.transform.localPosition = new Vector3(plotPos.x, plotPos.y, 0);
                    
                    vis.transform.rotation = Quaternion.identity;
                    vis.transform.SetAsLastSibling(); // Draw on top
                }
                
                vis.UpdateVisuals(g, FindObjectOfType<SimulationManager>());
            }
            else
            {
                vis.gameObject.SetActive(false); 
            }
        }
    }

    // --- HELPERS ---
    private float CalculateAxisValue(AxisVariable type, Respondent r, float ls, float baseLS, float[] popLS, float[] popBaseLS)
    {
        switch (type) {
            case AxisVariable.LifeSatisfaction: return ls;
            case AxisVariable.PersonalUtility: return WelfareMetrics.GetUtilityForPerson(ls, r.personalUtilities);
            case AxisVariable.SocietalFairness: return WelfareMetrics.EvaluateDistribution(popLS, r.societalUtilities);
            case AxisVariable.Wealth: return ls;
            case AxisVariable.DeltaPersonalUtility: return WelfareMetrics.GetUtilityForPerson(ls, r.personalUtilities) - WelfareMetrics.GetUtilityForPerson(baseLS, r.personalUtilities);
            case AxisVariable.DeltaSocietalFairness: return WelfareMetrics.EvaluateDistribution(popLS, r.societalUtilities) - WelfareMetrics.EvaluateDistribution(popBaseLS, r.societalUtilities);
            default: return 0;
        }
    }

    private void GetFinalRange(AxisVariable type, ref float min, ref float max)
    {
        if (type == AxisVariable.LifeSatisfaction || type == AxisVariable.Wealth) { min = 0; max = 10; return; }
        if (type == AxisVariable.PersonalUtility || type == AxisVariable.SocietalFairness) { min = 0; max = 1; return; }
        if (type == AxisVariable.DeltaPersonalUtility || type == AxisVariable.DeltaSocietalFairness) { min = -1f; max = 1f; return; }
        if (type == AxisVariable.Stack) { min = 0; max = Mathf.Max(max, 5f); return; }
        
        if (min == float.MaxValue || max == float.MinValue || (min == 0 && max == 0)) { min = -0.1f; max = 0.1f; return; }
        float absMax = Mathf.Max(Mathf.Abs(min), Mathf.Abs(max)) * 1.05f;
        min = -absMax; max = absMax;
    }

    private Vector2 GetPos(int id, float valX, float valY, float xMin, float xMax, float yMin, float yMax)
    {        
        float normX = Mathf.Clamp01(Mathf.InverseLerp(xMin, xMax, valX));
        float normY = Mathf.Clamp01(Mathf.InverseLerp(yMin, yMax, valY));
        return graphGrid.GetPlotPosition(normX, normY, id);
    }

    // Sprites
    private Sprite GetAbsoluteSprite(float ls) { if (ls >= 6.0f) return faceGreen; if (ls <= 4.0f) return faceRed; return faceYellow; }
    private Sprite GetAbsoluteSocietySprite(float uSoc) { if (uSoc >= 0.6f) return faceGreen; if (uSoc <= 0.4f) return faceRed; return faceYellow; }
    private Sprite GetRelativeSprite(float current, float baseline) { float diff = current - baseline; if (diff > 0.001f) return faceGreen; if (diff < -0.001f) return faceRed; return faceYellow; }

    // --- DEBUG GIZMOS ---
    void OnDrawGizmos()
    {
        if (!showClusterGizmos || CurrentClusters == null || graphGrid == null) return;

        foreach (var g in CurrentClusters)
        {
            if (g.size == 0) continue;
            // Convert normalized center back to world position for drawing
            Vector3 worldPos = graphGrid.GetPlotPosition(g.visualCenter.x, g.visualCenter.y, 0);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(worldPos, 0.5f);
        }
    }
    
    // Pass-throughs
    public void SetHoverHighlight(Respondent target) {
        bool anyone = (target != null);
        if (_activeVisualsArray != null) foreach (var v in _activeVisualsArray) v.SetFocusState(target != null && v.data.id == target.id, anyone);
    }
    public void SetGhostMode(bool enable) { _showGhostOverlay = enable; }
    public void SetArrowMode(bool showAll) { if (_activeVisualsArray != null) foreach (var v in _activeVisualsArray) v.SetArrowState(showAll); }
}