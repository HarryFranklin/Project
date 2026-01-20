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

    // Optimisation - array is faster than list
    private RespondentVisual[] _activeVisualsArray; 
    private List<RespondentVisual> _visualsList = new List<RespondentVisual>(); // Keep list for easy adding/removing

    // State
    private bool _showGhostOverlay = false;

    // --- 1. SETUP ---

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
    }

    // --- 2. MANAGER PATTERN UPDATE LOOP ---
    
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

    // --- 3. DISPLAY LOGIC ---
    public void UpdateDisplay(Respondent[] population, float[] currentLS, float[] baselineLS, Policy activePolicy, AxisVariable xAxis, AxisVariable yAxis, FaceMode faceMode)
    {
        // 1. Update background elements
        graphAxes.UpdateAxisVisuals(xAxis, yAxis);

        // 2. Determine modes
        bool isComparisonMode = (activePolicy != null);
        bool enableGhosts = _showGhostOverlay && isComparisonMode; 

        // 3. Main Calculation Loop
        for (int i = 0; i < population.Length; i++)
        {
            Respondent r = population[i];
            
            // --- 1. Calculate metrics (current) ---
            float cLS = currentLS[i];
            float cUSelf = WelfareMetrics.GetUtilityForPerson(cLS, r.personalUtilities);
            float cUSoc = WelfareMetrics.EvaluateDistribution(currentLS, r.societalUtilities);
            
            // --- 2. Calculate metrics (baseline) ---
            float bLS = baselineLS[i];
            float bUSelf = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
            float bUSoc = WelfareMetrics.EvaluateDistribution(baselineLS, r.societalUtilities);

            // --- 3. Sprite selection ---
            Sprite leftSprite = faceYellow;
            Sprite rightSprite = faceYellow;

            if (cLS <= -0.9f) // Dead
            {
                leftSprite = faceDead;
                rightSprite = faceDead;
            }
            else
            {
                if (isComparisonMode)
                {
                    // Compare mode: Relative change
                    // Use baseline uSelf
                    float bUSelfForSpr = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
                    
                    leftSprite = GetRelativeSprite(cUSelf, bUSelfForSpr);
                    rightSprite = GetRelativeSprite(cUSoc, bUSoc);
                }
                else
                {
                    // Default mode: Absolute State
                    leftSprite = GetAbsoluteSprite(cLS);
                    rightSprite = GetAbsoluteSocietySprite(cUSoc); 
                }

                // Apply Face Mode Filters
                switch (faceMode)
                {
                    case FaceMode.PersonalWellbeing: rightSprite = leftSprite; break;
                    case FaceMode.SocietalFairness: leftSprite = rightSprite; break;
                }
            }

            // Ghost Sprites are always "Absolute history" (how I was before)
            Sprite gLeft = GetAbsoluteSprite(bLS);
            Sprite gRight = GetAbsoluteSocietySprite(bUSoc);

            // --- c. Position calculation ---
            // Calculate both positions so the visual can interpolate/ghost between them
            Vector2 currPos = GetPos(r.id, cLS, cUSelf, cUSoc, bUSelf, bUSoc, xAxis, yAxis);
            Vector2 basePos = GetPos(r.id, bLS, bUSelf, bUSoc, bUSelf, bUSoc, xAxis, yAxis);

            // --- 5. Push to Visual ---
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

    // Updated to accept baseline args for Delta graphs
    private Vector2 GetPos(int id, float ls, float self, float soc, float baseSelf, float baseSoc, AxisVariable x, AxisVariable y)
    {
        if (ls <= -0.9f) return graphGrid.GetGraveyardPosition(id);

        float nx = GetNormalisedValue(ls, self, soc, baseSelf, baseSoc, x);
        float ny = GetNormalisedValue(ls, self, soc, baseSelf, baseSoc, y);
        
        return graphGrid.GetPlotPosition(nx, ny, id);
    }

    private float GetNormalisedValue(float ls, float uSelf, float uSoc, float uSelfBase, float uSocBase, AxisVariable type)
    {
        float val = 0;
        switch(type) {
            case AxisVariable.LifeSatisfaction: val = ls; break;
            case AxisVariable.PersonalUtility: val = uSelf; break;
            case AxisVariable.SocietalFairness: val = uSoc; break;
            case AxisVariable.Wealth: val = ls; break; 
            
            // Delta Logic
            case AxisVariable.DeltaPersonalUtility: val = uSelf - uSelfBase; break;
            case AxisVariable.DeltaSocietalFairness: val = uSoc - uSocBase; break;
        }

        var range = graphAxes.GetRange(type);
        return Mathf.InverseLerp(range.min, range.max, val);
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