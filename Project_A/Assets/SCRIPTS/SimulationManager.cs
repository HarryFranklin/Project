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
    private int _policyIndex = -1; // -1 to start

    void Start()
    {
        _respondents = dataReader.GetRespondents(); // Get all the people's information
        
        // 1. Assign Baseline using ONS Data (Weighted Random)
        foreach (var personData in _respondents.Values)
        {
            personData.currentTier = WelfareMetrics.GetWeightedRandomTier();
        }

        // 2. Spawn Visuals (Clear old ones first)
        foreach (Transform child in container) Destroy(child.gameObject);
        
        foreach (var personData in _respondents.Values)
        {
            GameObject go = Instantiate(personPrefab, container);
            RespondentVisual personVis = go.GetComponent<RespondentVisual>();
            personVis.data = personData; 
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
        foreach (var personData in _respondents.Values) baselinePopulation[i++] = personData.currentTier;

        // 2. Snapshot FUTURE State (Policy Tiers)
        int[] futurePopulation = new int[_respondents.Count];
        i = 0;
        if (activePolicy != null)
        {
            foreach (var personData in _respondents.Values) 
                futurePopulation[i++] = activePolicy.ResolveNewTier(personData.currentTier);
        }
        else
        {
            futurePopulation = baselinePopulation; 
        }

        // --- PRE-CALCULATION PASS: Determine Rows based on U_SELF ---
        // Key Change: Row is now determined by PERSONAL UTILITY (Happiness), not just Wealth (Tier).
        // We calculate this first so we know how many people are in each row for spacing.
        
        Dictionary<int, int> respondentRows = new Dictionary<int, int>(); // Map ID -> Row
        int[] totalPerRow = new int[11]; 

        foreach (var personData in _respondents.Values)
        {
            // A. What Tier do they have?
            int tier = (activePolicy != null) ? activePolicy.ResolveNewTier(personData.currentTier) : personData.currentTier;
            
            // B. How much do they enjoy it? (U_self)
            float uSelf = WelfareMetrics.GetSinglePersonUtility(tier, personData.personalUtilities);
            
            // C. Map 0.0-1.0 Utility to 0-10 Row
            int roundedUSelf = Mathf.RoundToInt(uSelf * 10f);
            int row = Mathf.Clamp(roundedUSelf, 0, 10);
            
            respondentRows[personData.id] = row;
            totalPerRow[row]++;
        }

        // --- LAYOUT PASS: Position Everyone ---
        int[] placedSoFar = new int[11]; // (0-11)
        
        foreach (Transform person in container)
        {
            RespondentVisual personVis = person.GetComponent<RespondentVisual>();
            Respondent personData = personVis.data;

            // Retrieve the pre-calculated Row (based on Happiness)
            int row = respondentRows[personData.id];

            // Dynamic Spacing Calculation
            float usableWidth = containerWidth - 40f; 
            int peopleInRow = Mathf.Max(1, totalPerRow[row]);
            
            float spacingX = usableWidth / peopleInRow;
            spacingX = Mathf.Min(spacingX, 41f); 

            // Calculate Positions
            float startX = (-containerWidth / 2f) + 20f; 
            float xPos = startX + (placedSoFar[row] * spacingX);
            float yPos = (-containerHeight / 2f) + (row * dynamicRowHeight) + (dynamicRowHeight / 2f);
            
            personVis.GetComponent<RectTransform>().anchoredPosition = new Vector2(xPos, yPos);
            placedSoFar[row]++; 

            // --- FACE LOGIC (U_OTHERS) ---
            if (activePolicy == null)
            {
                personVis.SetVisuals(faceNeutral);
            }
            else
            {
                float wOld = WelfareMetrics.EvaluateDistribution(baselinePopulation, personData.societalUtilities);
                float wNew = WelfareMetrics.EvaluateDistribution(futurePopulation, personData.societalUtilities);
                float delta = wNew - wOld;

                // Threshold for difference (0.01f was too small)
                if (delta > 0.02f) personVis.SetVisuals(faceHappy);
                else if (delta < -0.02f) personVis.SetVisuals(faceSad);
                else personVis.SetVisuals(faceNeutral);
            }
        }
    }
}