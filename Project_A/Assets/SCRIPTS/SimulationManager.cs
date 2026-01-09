using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SimulationManager : MonoBehaviour
{
    [Header("Data")]
    public DataReader dataReader; 
    public List<Policy> policies; 

    [Header("UI References")]
    public TMP_Text policyTitleText;
    public TMP_Text policyDescText;
    public TMP_Text policyStatsText;

    [Header("Visual Configuration")]
    public Transform container;
    public GameObject personPrefab; 
    
    public Sprite faceHappy;
    public Sprite faceNeutral;
    public Sprite faceSad;

    private Dictionary<int, Respondent> respondents;
    private List<RespondentVisual> respondentList = new List<RespondentVisual>();
    private int policyIndex = -1; 

    // Cache current results
    private List<Respondent> _cachedPopulationList; // Cache because dicts are unordered
    private int[] _currentPopulationLS;
    private int[] _baselineLS;

    void Start()
    {
        respondents = dataReader.GetRespondents();

        float[] onsDist = WelfareMetrics.GetBaselineDistribution();
        foreach (var r in respondents.Values)
        {
            r.currentLS = WelfareMetrics.GetWeightedRandomLS(onsDist);
        }
        
        _cachedPopulationList = new List<Respondent>(respondents.Values);

        foreach (var r in _cachedPopulationList)
        {
            GameObject respondent = Instantiate(personPrefab, container);
            RespondentVisual respondentVisual = respondent.GetComponent<RespondentVisual>();
            respondentVisual.Initialise(r, this);
            respondentList.Add(respondentVisual);
        }

        UpdateSimulation();
    }

    public void NextPolicy()
    {
        if (policies.Count == 0) return;
        policyIndex = (policyIndex + 1) % policies.Count;
        UpdateSimulation();
    }

    void UpdateSimulation()
    {
        Respondent[] population = _cachedPopulationList.ToArray();
        
        _baselineLS = new int[population.Length];
        _currentPopulationLS = new int[population.Length];

        for(int i=0; i<population.Length; i++) _baselineLS[i] = population[i].currentLS;

        if (policyIndex == -1)
        {
            // Copy baseline to current
            System.Array.Copy(_baselineLS, _currentPopulationLS, _baselineLS.Length);
            UpdateUI_Default();
        }
        else
        {
            // Store result directly in the class variable
            _currentPopulationLS = policies[policyIndex].ApplyPolicy(population);
            UpdateUI_Policy(policies[policyIndex]);
        }

        // B. Update Visuals
        for (int i = 0; i < population.Length; i++)
        {
            Respondent r = population[i];

            // Use the class variables here
            float uSelf = WelfareMetrics.GetUtilityForPerson(_currentPopulationLS[i], r.personalUtilities);

            float wOld = WelfareMetrics.EvaluateDistribution(_baselineLS, r.societalUtilities);
            float wNew = WelfareMetrics.EvaluateDistribution(_currentPopulationLS, r.societalUtilities);

            Sprite face = faceNeutral;
            if (wNew > wOld + 0.01f) face = faceHappy; 
            else if (wNew < wOld - 0.01f) face = faceSad; 

            respondentList[i].UpdateVisuals(uSelf, face);
        }
    }

    // --- HOVER LOGIC ---
    public void OnHoverEnter(Respondent r)
    {
        // SAFETY CHECKS to prevent crashes
        if (r == null || respondents == null || _currentPopulationLS == null) return;

        int index = 0;
        int currentLS = 0;
        bool found = false;

        // Find the index of this respondent
        foreach(var kvp in respondents)
        {
            if (kvp.Value == r) 
            {
                // Safety check for array bounds
                if(index < _currentPopulationLS.Length)
                {
                    currentLS = _currentPopulationLS[index];
                    found = true;
                }
                break;
            }
            index++;
        }

        if (!found) return; // Should never happen, but safe to ignore

        float uSelf = WelfareMetrics.GetUtilityForPerson(currentLS, r.personalUtilities);
        float uSociety = WelfareMetrics.EvaluateDistribution(_currentPopulationLS, r.societalUtilities);

        // Display
        string info = $"<size=120%><b>Respondent #{r.id}</b></size>\n";
        info += $"<b>Life Satisfaction:</b> {currentLS}/10\n";
        info += $"<b>Personal Utility:</b> {uSelf:F2}\n";
        info += $"<b>Societal Utility:</b> {uSociety:F2}\n";
        
        string selfish = uSelf > uSociety ? "<color=red>Self-Interested</color>" : "<color=green>Altruistic</color>";
        info += $"<b>Type:</b> {selfish}";

        if(policyStatsText) policyStatsText.text = info;
    }

    public void OnHoverExit()
    {
        if (policyIndex == -1) UpdateUI_Default();
        else UpdateUI_Policy(policies[policyIndex]);
    }

    void UpdateUI_Default()
    {
        if (policyTitleText) policyTitleText.text = "<b>Default (2022-2023)</b>";
        if (policyDescText) policyDescText.text = "The current distribution of life satisfaction in the UK based on ONS data.";
        if (policyStatsText) policyStatsText.text = "<b>Base:</b> ONS 2022-2023 Data\n<b>Impact:</b> None";
    }

    void UpdateUI_Policy(Policy p)
    {
        if (policyTitleText) policyTitleText.text = p.policyName;
        if (policyDescText) policyDescText.text = p.description;

        if (policyStatsText)
        {
            string FormatChange(int val)
            {
                if (val > 0) return $"<color=green>+{val}</color>";
                if (val < 0) return $"<color=red>{val}</color>";
                return "0";
            }

            string stats = "";
            stats += $"<b>Rich (Tiers {p.richThreshold}-10):</b> {FormatChange(p.changeForRich)}\n";
            stats += $"<b>Middle Class:</b> {FormatChange(p.changeForMiddle)}\n";
            stats += $"<b>Poor (Tiers 0-{p.poorThreshold}):</b> {FormatChange(p.changeForPoor)}";
            policyStatsText.text = stats;
        }
    }
}