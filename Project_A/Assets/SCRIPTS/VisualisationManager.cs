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

    [Header("Clustering Settings")]
    public int clusterCount = 4;
    public bool showClusterGizmos = true;

    [Header("Cluster Appearance")]
    public Sprite clusterDotSprite;
    public Color[] clusterColors = new Color[] { Color.cyan, Color.magenta, Color.green, Color.yellow };
    
    [Header("Cluster Visuals")]
    public GameObject clusterPrefab;
    private List<ClusterVisual> _spawnedClusters = new List<ClusterVisual>();
    
    // --- CLUSTER DATA ---
    [System.Serializable]
    public class GroupOpinion
    {
        public int id;
        public string name;
        public int size;
        public Vector2 visualCenter;
        public float avgLS;
        public float avgSocietalFairness;
        public float[] avgSocietalUtilities;
    }
    public List<GroupOpinion> CurrentClusters = new List<GroupOpinion>();

    // --- INTERNAL STATE ---
    private int[] _currentAssignments;
    private FaceMode _currentFaceMode;
    
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
        _currentFaceMode = faceMode; // Save for hover logic
        int count = population.Length;
        
        // Ensure Cache
        if (_cacheXValues == null || _cacheXValues.Length != count) {
            _cacheXValues = new float[count];
            _cacheYValues = new float[count];
        }

        // --- PASS 1: CALCULATE POSITIONS (Do not draw yet) ---
        float xMin = float.MaxValue, xMax = float.MinValue;
        float yMin = float.MaxValue, yMax = float.MinValue;
        bool isStackMode = (yAxis == AxisVariable.Stack);
        Dictionary<int, int> binCount = isStackMode ? new Dictionary<int, int>() : null;

        for (int i = 0; i < count; i++)
        {
            Respondent r = population[i];
            float cLS = currentLS[i];
            float bLS = baselineLS[i];

            // Calc X
            float valX = CalculateAxisValue(xAxis, r, cLS, bLS, currentLS, baselineLS);
            float valY = 0;

            // Calc Y
            if (isStackMode) {
                float snappedX = Mathf.Round(valX / STACK_BIN_SIZE) * STACK_BIN_SIZE;
                int binKey = Mathf.RoundToInt(snappedX * 100);
                if (!binCount.ContainsKey(binKey)) binCount[binKey] = 0;
                valY = binCount[binKey];
                binCount[binKey]++;
                valX = snappedX;
            } else {
                valY = CalculateAxisValue(yAxis, r, cLS, bLS, currentLS, baselineLS);            
            }

            _cacheXValues[i] = valX;
            _cacheYValues[i] = valY;

            // Ranges
            if (cLS > -0.9f) {
                if (valX < xMin) xMin = valX; if (valX > xMax) xMax = valX;
                if (valY < yMin) yMin = valY; if (valY > yMax) yMax = valY;
            }
        }

        GetFinalRange(xAxis, ref xMin, ref xMax);
        GetFinalRange(yAxis, ref yMin, ref yMax);
        
        // Update the axis visuals with the zoomed ranges
        graphAxes.UpdateAxisVisuals(xAxis, xMin, xMax, yAxis, yMin, yMax);

        // --- PASS 2: CALCULATE CLUSTERS ---
        // We do this now so we know the colors before we draw the faces
        RecalculateClusters(population, currentLS, xMin, xMax, yMin, yMax);

        // --- PASS 3: DRAW VISUALS ---
        bool isComparisonMode = (activePolicy != null);
        bool enableGhosts = _showGhostOverlay && isComparisonMode && !isStackMode;

        for (int i = 0; i < count; i++)
        {
            Respondent r = population[i];
            float cLS = currentLS[i];
            
            // 1. Calculate Standard Sprites (We do this every frame)
            float cUSelf = WelfareMetrics.GetUtilityForPerson(cLS, r.personalUtilities);
            float cUSoc = WelfareMetrics.EvaluateDistribution(currentLS, r.societalUtilities);
            float bLS = baselineLS[i];
            float bUSelf = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
            float bUSoc = WelfareMetrics.EvaluateDistribution(baselineLS, r.societalUtilities);

            Sprite left = faceYellow, right = faceYellow;

            if (cLS <= -0.9f) { left = right = faceDead; }
            else if (isComparisonMode) {
                left = GetRelativeSprite(cUSelf, bUSelf);
                right = GetRelativeSprite(cUSoc, bUSoc);
            } else {
                left = GetAbsoluteSprite(cLS);
                right = GetAbsoluteSocietySprite(cUSoc); 
            }

            // 2. Positions
            Vector2 currPos = GetPos(r.id, _cacheXValues[i], _cacheYValues[i], xMin, xMax, yMin, yMax);
            Vector2 basePos = currPos; 

            if (!isStackMode) {
                float baseX = CalculateAxisValue(xAxis, r, bLS, bLS, baselineLS, baselineLS); 
                float baseY = CalculateAxisValue(yAxis, r, bLS, bLS, baselineLS, baselineLS);
                basePos = GetPos(r.id, baseX, baseY, xMin, xMax, yMin, yMax);
            }

            // 3. Apply Standard Visuals
            _activeVisualsArray[i].UpdateVisuals(currPos, basePos, left, right, GetAbsoluteSprite(bLS), GetAbsoluteSocietySprite(bUSoc), enableGhosts);

            // 4. OVERRIDE WITH CLUSTER LOOK (If Enabled)
            if (faceMode == FaceMode.Cluster && _currentAssignments != null)
            {
                if (i < _currentAssignments.Length)
                {
                    int cID = _currentAssignments[i];
                    Color c = clusterColors[cID % clusterColors.Length];
                    
                    // Apply the look
                    _activeVisualsArray[i].SetClusterAppearance(clusterDotSprite, c);
                }
            }
            else
            {
                // Reset Colour to White
                _activeVisualsArray[i].ResetAppearance();
            }
        }
    }

    // --- CLUSTERING LOGIC ---
    private void RecalculateClusters(Respondent[] population, float[] currentLS, float xMin, float xMax, float yMin, float yMax)
    {
        int count = population.Length;
        Vector2[] normPoints = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            float nX = Mathf.InverseLerp(xMin, xMax, _cacheXValues[i]);
            float nY = Mathf.InverseLerp(yMin, yMax, _cacheYValues[i]);
            normPoints[i] = new Vector2(nX, nY);
        }

        // 1. Maths
        KMeans.Result result = KMeans.GetClusters(normPoints, clusterCount);
        _currentAssignments = result.Assignments; // Save for colouring

        // 2. Clear & Init
        CurrentClusters.Clear();
        for (int k = 0; k < clusterCount; k++) {
            CurrentClusters.Add(new GroupOpinion { 
                id = k, 
                visualCenter = result.Centres[k], 
                avgSocietalUtilities = new float[6] 
            });
        }

        // 3. Sum Data
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

        // If two clusters are nearly identical positions, hide the smaller one.
        float mergeThreshold = 0.05f; // 5% of screen width

        // We iterate backwards so we can safely "disable" clusters
        for (int i = 0; i < CurrentClusters.Count; i++)
        {
            if (CurrentClusters[i].size == 0) continue;

            for (int j = i + 1; j < CurrentClusters.Count; j++)
            {
                if (CurrentClusters[j].size == 0) continue;

                float dist = Vector2.Distance(CurrentClusters[i].visualCenter, CurrentClusters[j].visualCenter);
                
                if (dist < mergeThreshold)
                {
                    // Merge J into I
                    CurrentClusters[i].size += CurrentClusters[j].size;
                    // (You could average the stats here too if you want perfect accuracy)
                    
                    // Kill J
                    CurrentClusters[j].size = 0; 
                }
            }
        }

        // 4. Finalise Averages (Standard)
        foreach (var g in CurrentClusters)
        {
            if (g.size > 0)
            {
                g.avgLS /= g.size; // Note: If you merged sizes above, this average might be slightly off for the merged group, 
                                   // but usually acceptable for visuals.
                
                // Naming
                if (g.avgLS >= 7.5f) g.name = "The Thriving";
                else if (g.avgLS <= 4.5f) g.name = "The Struggling";
                else if (g.avgSocietalFairness < 0.3f) g.name = "The Cynics";
                else g.name = "The Moderates";
            }
        }

        UpdateClusterObjects();
    }

    private void UpdateClusterObjects()
    {
        if (!clusterPrefab) return;

        if (_currentFaceMode != FaceMode.Cluster)
        {
            foreach (var c in _spawnedClusters) if(c) c.gameObject.SetActive(false);
            return;
        }

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
                    Vector3 plotPos = graphGrid.GetPlotPosition(g.visualCenter.x, g.visualCenter.y, 0);
                    vis.transform.localPosition = new Vector3(plotPos.x, plotPos.y, 0);
                    vis.transform.rotation = Quaternion.identity;
                    vis.transform.SetAsLastSibling();
                }
                
                vis.UpdateVisuals(g, FindAnyObjectByType<SimulationManager>());
            }
            else
            {
                vis.gameObject.SetActive(false); 
            }
        }
    }

    // --- INTERACTION LOGIC ---

    public int GetClusterID(int respondentIndex)
    {
        if (_currentAssignments != null && respondentIndex >= 0 && respondentIndex < _currentAssignments.Length)
        {
            return _currentAssignments[respondentIndex];
        }
        return -1;
    }

    public void SetClusterHighlight(int activeClusterID)
    {
        if (_activeVisualsArray == null || _currentAssignments == null) return;

        bool isReset = (activeClusterID == -1);

        // 1. Update People
        for (int i = 0; i < _activeVisualsArray.Length; i++)
        {
            RespondentVisual vis = _activeVisualsArray[i];
            
            if (isReset)
            {
                // RESET: No target, No hover context.
                // This tells the visual to return to its default state.
                vis.SetFocusState(false, false); 
            }
            else
            {
                bool isMember = (_currentAssignments[i] == activeClusterID);
                
                // HIGHLIGHT LOGIC:
                // - Arg 1 (isTarget): If I am a member, treat me as a "Target" (Show Arrow/Ghost)
                // - Arg 2 (isHovered): The system is in hover mode (so fade out if I'm NOT a target)
                vis.SetFocusState(isMember, true);
            }
        }

        // 2. Update Cluster Bubbles (Hide the other bubbles to reduce clutter)
        for (int i = 0; i < _spawnedClusters.Count; i++)
        {
            var clusterVis = _spawnedClusters[i];
            if (clusterVis == null) continue;

            // Show bubble if it matches the active ID, OR if we are resetting everything
            bool showBubble = isReset || (clusterVis.clusterID == activeClusterID);
            clusterVis.gameObject.SetActive(showBubble);
        }
    }

    public void SetHoverHighlight(Respondent target) 
    {
        // Use the CACHED _currentFaceMode here
        if (_currentFaceMode == FaceMode.Cluster)
        {
            if (target != null)
            {
                int cID = GetClusterID(target.id);
                SetClusterHighlight(cID);
            }
            else
            {
                SetClusterHighlight(-1);
            }
        }
        else
        {
            // Standard ghost logic
            bool anyone = (target != null);
            if (_activeVisualsArray != null) 
            {
                foreach (var v in _activeVisualsArray) 
                {
                    v.SetFocusState(target != null && v.data.id == target.id, anyone);
                }
            }
        }
    }

    // Helpers
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
        float baseMin = 0, baseMax = 0;

        if (type == AxisVariable.LifeSatisfaction || type == AxisVariable.Wealth) 
        { baseMin = 0; baseMax = 10; }
        else if (type == AxisVariable.PersonalUtility || type == AxisVariable.SocietalFairness) 
        { baseMin = 0; baseMax = 1; }
        // Enforce +/- 1 as the base for zoom calculations
        else if (type == AxisVariable.DeltaPersonalUtility || type == AxisVariable.DeltaSocietalFairness) 
        { baseMin = -1f; baseMax = 1f; }
        else if (type == AxisVariable.Stack) 
        { baseMin = 0; baseMax = Mathf.Max(max, 5f); }

        // Apply zoom around the center of the base range
        float range = baseMax - baseMin;
        float center = baseMin + (range / 2f);
        float zoomedRange = range * _zoomScale;

        min = center - (zoomedRange / 2f);
        max = center + (zoomedRange / 2f);
    }

    private Vector2 GetPos(int id, float valX, float valY, float xMin, float xMax, float yMin, float yMax)
    {        
        float normX = Mathf.Clamp01(Mathf.InverseLerp(xMin, xMax, valX));
        float normY = Mathf.Clamp01(Mathf.InverseLerp(yMin, yMax, valY));
        return graphGrid.GetPlotPosition(normX, normY, id);
    }

    // --- ZOOM SETTINGS ---
    private float _zoomScale = 1.0f; // 1.0 = 100%

    public void ZoomIn()
    {
        _zoomScale *= 0.75f;
        if (_zoomScale < 0.05f) _zoomScale = 0.05f;
        
        FindFirstObjectByType<SimulationManager>().RefreshGraphOnly();
    }

    public void ZoomOut()
    {
        _zoomScale *= 1.25f;
        if (_zoomScale > 10.0f) _zoomScale = 10.0f;
        
        FindFirstObjectByType<SimulationManager>().RefreshGraphOnly();
    }

    public void ResetZoom()
    {
        _zoomScale = 1.0f; // Return to 100% view
        
        // Trigger a redraw through the SimulationManager
        var sim = FindAnyObjectByType<SimulationManager>();
        if (sim != null)
        {
            sim.UpdateSimulation();
        }
    }

    private Sprite GetAbsoluteSprite(float ls) { if (ls >= 6.0f) return faceGreen; if (ls <= 4.0f) return faceRed; return faceYellow; }
    private Sprite GetAbsoluteSocietySprite(float uSoc) { if (uSoc >= 0.6f) return faceGreen; if (uSoc <= 0.4f) return faceRed; return faceYellow; }
    private Sprite GetRelativeSprite(float current, float baseline) { float diff = current - baseline; if (diff > 0.001f) return faceGreen; if (diff < -0.001f) return faceRed; return faceYellow; }

    void OnDrawGizmos()
    {
        if (!showClusterGizmos || CurrentClusters == null || graphGrid == null) return;
        foreach (var g in CurrentClusters)
        {
            if (g.size == 0) continue;
            Vector3 worldPos = graphGrid.GetPlotPosition(g.visualCenter.x, g.visualCenter.y, 0);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(worldPos, 0.5f);
        }
    }
    public void SetGhostMode(bool enable) { _showGhostOverlay = enable; }
    public void SetArrowMode(bool showAll) { if (_activeVisualsArray != null) foreach (var v in _activeVisualsArray) v.SetArrowState(showAll); }
}