using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; 
using TMPro;

public class SimulationManager : MonoBehaviour
{
    [Header("Data & Assets")]
    public DataReader dataReader;
    public List<Policy> policies;
    public GameObject personPrefab;
    public Transform container; // Ensure this is a RectTransform

    [Header("UI References")]
    public TMP_Text policyNameText;       
    public TMP_Text policyDescriptionText;
    public TMP_Text policyStatsText;      

    [Header("Visuals")]
    public Sprite faceHappy;   // Green sprite
    public Sprite faceNeutral; // Yellow/Grey sprite
    public Sprite faceSad;     // Red sprite

    [Header("Debug / Info")]
    public Policy activePolicy; // Visible in Inspector for debugging

    private Dictionary<int, Respondent> _respondents;
    private int _policyIndex = -1;

    void Start()
    {
        _respondents = dataReader.GetRespondents();
        
        // 1. Assign Baseline using ONS Data (Weighted Random)
        foreach (var r in _respondents.Values)
        {
            r.currentTier = WelfareMetrics.GetWeightedRandomTier();
        }

        // 2. Spawn Visuals (Clear old ones first)
        foreach (Transform child in container) Destroy(child.gameObject);
        
        foreach (var r in _respondents.Values)
        {
            GameObject go = Instantiate(personPrefab, container);
            RespondentVisual vis = go.GetComponent<RespondentVisual>();
            vis.data = r; 
        }

        // 3. Initial State (No Policy)
        UpdateUI();
        UpdateSimulation();
    }

    public void NextPolicy()
    {
        if (policies.Count == 0) return;
        
        // Cycle through policies
        _policyIndex = (_policyIndex + 1) % policies.Count;
        activePolicy = policies[_policyIndex];
        
        UpdateUI();        
        UpdateSimulation(); 
    }

    void UpdateUI()
    {
        if (activePolicy == null)
        {
            if (policyNameText) policyNameText.text = "Status Quo";
            if (policyDescriptionText) policyDescriptionText.text = "Current UK Baseline (2023).";
            if (policyStatsText) policyStatsText.text = "";
            return;
        }

        // Set Text
        if (policyNameText) policyNameText.text = activePolicy.policyName;
        if (policyDescriptionText) policyDescriptionText.text = activePolicy.description;

        // Set Stats Box
        if (policyStatsText)
        {
            string stats = "";
            if (activePolicy.isRedistributive)
            {
                stats += $"<b>Tax Threshold:</b> Top {CalculatePercentage(activePolicy.taxThreshold)}%\n";
                stats += $"<b>Benefit Threshold:</b> Bottom {CalculatePercentage(activePolicy.benefitThreshold)}%\n";
                stats += $"<b>Wealth Transfer:</b> {activePolicy.wealthChange} Steps\n";
            }
            else
            {
                stats += $"<b>Wealth Impact:</b> {activePolicy.wealthChange} (Everyone)\n";
            }
            stats += $"<b>Societal Lift:</b> {activePolicy.societalBaseLift}";
            policyStatsText.text = stats;
        }
    }

    // Helper to convert Tier -> % of Population for UI
    string CalculatePercentage(int tierThreshold)
    {
        if (tierThreshold >= 10) return "10"; // Elite
        if (tierThreshold >= 9) return "27";  // Wealthy
        if (tierThreshold >= 8) return "59";  // Middle Class +
        if (tierThreshold >= 7) return "79";  // Comfortable +
        if (tierThreshold <= 5) return "13";  // Struggling
        return "Unknown";
    }

    void UpdateSimulation()
    {
        // 0. Get Container Dimensions for Dynamic Layout
        RectTransform containerRect = container.GetComponent<RectTransform>();
        float containerWidth = containerRect.rect.width;
        float containerHeight = containerRect.rect.height;
        float dynamicRowHeight = containerHeight / 11f; // 11 Rows (0-10)

        // 1. Snapshot CURRENT State (Baseline Tiers)
        int[] baselinePopulation = new int[_respondents.Count];
        int i = 0;
        foreach (var r in _respondents.Values) baselinePopulation[i++] = r.currentTier;

        // 2. Snapshot FUTURE State (Policy Tiers)
        int[] futurePopulation = new int[_respondents.Count];
        i = 0;
        if (activePolicy != null)
        {
            foreach (var r in _respondents.Values) 
                futurePopulation[i++] = activePolicy.ResolveNewTier(r.currentTier);
        }
        else
        {
            futurePopulation = baselinePopulation; 
        }

        // --- LAYOUT PASS 1: Count totals per row ---
        int[] totalPerRow = new int[11]; 
        foreach (var r in _respondents.Values)
        {
            int tier = (activePolicy != null) ? activePolicy.ResolveNewTier(r.currentTier) : r.currentTier;
            int clampedTier = Mathf.Clamp(tier, 0, 10);
            totalPerRow[clampedTier]++;
        }

        // --- LAYOUT PASS 2: Position Everyone ---
        int[] placedSoFar = new int[11]; 
        
        foreach (Transform child in container)
        {
            RespondentVisual vis = child.GetComponent<RespondentVisual>();
            Respondent r = vis.data;

            // Determine Row
            int tier = (activePolicy != null) ? activePolicy.ResolveNewTier(r.currentTier) : r.currentTier;
            int row = Mathf.Clamp(tier, 0, 10);

            // Dynamic Spacing Calculation
            // Leave 40px padding total (20 left, 20 right)
            float usableWidth = containerWidth - 40f; 
            int peopleInRow = Mathf.Max(1, totalPerRow[row]);
            
            // Calculate gap. Max width is 41f (Face size + 1px padding). 
            // If row is crowded, this number drops, causing overlap (deck of cards effect).
            float spacingX = usableWidth / peopleInRow;
            spacingX = Mathf.Min(spacingX, 41f); 

            // Calculate Positions
            // Start from Left Edge + 20px padding
            float startX = (-containerWidth / 2f) + 20f; 
            float xPos = startX + (placedSoFar[row] * spacingX);
            // Center row vertically in its slot
            float yPos = (-containerHeight / 2f) + (row * dynamicRowHeight) + (dynamicRowHeight / 2f);
            
            vis.GetComponent<RectTransform>().anchoredPosition = new Vector2(xPos, yPos);
            placedSoFar[row]++; 

            // --- FACE LOGIC ---
            if (activePolicy == null)
            {
                vis.SetVisuals(faceNeutral);
            }
            else
            {
                float wOld = WelfareMetrics.EvaluateDistribution(baselinePopulation, r.societalUtilities);
                float wNew = WelfareMetrics.EvaluateDistribution(futurePopulation, r.societalUtilities);
                float delta = wNew - wOld;

                // Threshold 0.02 (2% change required to care)
                if (delta > 0.02f) 
                {
                    vis.SetVisuals(faceHappy); // Shows Green sprite
                }
                else if (delta < -0.02f) 
                {
                    vis.SetVisuals(faceSad);   // Shows Red sprite
                }
                else 
                {
                    vis.SetVisuals(faceNeutral); // Shows Yellow sprite
                }
            }
        }
    }
}