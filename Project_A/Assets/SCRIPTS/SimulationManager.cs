using System.Collections.Generic;
using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("Architecture")]
    public VisualisationManager visuals; 
    public UIManager uiManager;
    public DataReader dataReader;

    [Header("Plotting Settings")]
    public AxisVariable xAxis = AxisVariable.LifeSatisfaction;
    public AxisVariable yAxis = AxisVariable.SocietalFairness;
    public FaceMode faceMode = FaceMode.Split;

    [Header("Policy Data")]
    public List<Policy> policies;
    public Policy policyOptionA;
    public Policy policyOptionB;

    // --- INTERNAL DATA STATE ---
    private Dictionary<int, Respondent> _respondentMap;
    private List<Respondent> _populationList;
    
    private float[] _baselineLS;
    private float[] _currentLS;
    
    private int _policyIndex = -1;
    private Policy _activePolicy = null;

    void Start()
    {
        _respondentMap = dataReader.GetRespondents();
        _populationList = new List<Respondent>(_respondentMap.Values);

        int count = _populationList.Count;
        _baselineLS = new float[count];
        _currentLS = new float[count];

        float[] onsDist = WelfareMetrics.GetBaselineDistribution();
        for (int i = 0; i < count; i++)
        {
            // ONS weighted random returns int, implicitly casts to float
            float startingLS = WelfareMetrics.GetWeightedRandomLS(onsDist);
            _baselineLS[i] = startingLS;
            _populationList[i].currentLS = startingLS; 
        }

        System.Array.Copy(_baselineLS, _currentLS, count);

        if (visuals != null)
        {
            visuals.CreatePopulation(_populationList, this);
        }

        UpdateSimulation();
    }

    // --- CONTROLS ---
    public void NextPolicy()
    {
        if (policies.Count == 0) return;
        _policyIndex = (_policyIndex + 1) % policies.Count;
        ApplyPolicy(policies[_policyIndex]);
    }

    public void ResetToDefault()
    {
        _policyIndex = -1;
        _activePolicy = null;
        
        xAxis = AxisVariable.LifeSatisfaction;
        yAxis = AxisVariable.SocietalFairness;
        faceMode = FaceMode.Split;

        System.Array.Copy(_baselineLS, _currentLS, _baselineLS.Length);
        
        UpdateSimulation();
    }

    public void PreviewOptionA() { if (policyOptionA) ApplyPolicy(policyOptionA); }
    public void PreviewOptionB() { if (policyOptionB) ApplyPolicy(policyOptionB); }

    // --- LOGIC ---

    void ApplyPolicy(Policy p)
    {
        _activePolicy = p;
        _currentLS = p.ApplyPolicy(_populationList.ToArray());
        UpdateSimulation();
    }

    void UpdateSimulation()
    {
        // 1. Update Visuals
        if (visuals != null)
        {
            visuals.UpdateDisplay(
                _populationList.ToArray(),
                _currentLS,
                _baselineLS,
                _activePolicy,
                xAxis,
                yAxis,
                faceMode
            );
        }

        // 2. Update UI
        CalculateAndRefreshUI();
    }

    void CalculateAndRefreshUI()
    {
        if (!uiManager) return;

        int count = _populationList.Count; 
        
        double totalBaseSocial = 0; 
        double totalCurrSocial = 0;
        double totalBasePersonal = 0; 
        double totalCurrPersonal = 0;
        int happyCount = 0;

        for (int i = 0; i < count; i++)
        {
            Respondent r = _populationList[i];
            float cLS = _currentLS[i];
            float bLS = _baselineLS[i];

            float uSelfCurr = WelfareMetrics.GetUtilityForPerson(cLS, r.personalUtilities);
            float uSelfBase = WelfareMetrics.GetUtilityForPerson(bLS, r.personalUtilities);
            float uSocCurr = WelfareMetrics.EvaluateDistribution(_currentLS, r.societalUtilities);
            float uSocBase = WelfareMetrics.EvaluateDistribution(_baselineLS, r.societalUtilities);

            totalBaseSocial += uSocBase; 
            totalCurrSocial += uSocCurr;
            totalBasePersonal += uSelfBase; 
            totalCurrPersonal += uSelfCurr;

            // Simple Happiness Logic
            bool isHappy = false;
            // Death Check (float safe comparison)
            if (cLS > -0.9f)
            {
                // Has society become fairer?
                if (uSocCurr > uSocBase + 0.01f) isHappy = true;
            }
            if (isHappy) happyCount++;
        }

        if (_activePolicy == null)
        {
            uiManager.UpdatePolicyInfo(null);
            uiManager.UpdateComparisonInfo("Default", totalBaseSocial, totalCurrSocial, totalBasePersonal, totalCurrPersonal, happyCount, count);
        }
        else
        {
            uiManager.UpdatePolicyInfo(_activePolicy);
            uiManager.UpdateComparisonInfo(_activePolicy.policyName, totalBaseSocial, totalCurrSocial, totalBasePersonal, totalCurrPersonal, happyCount, count);
        }
    }

    public void SetAxisVariables(AxisVariable x, AxisVariable y)
    {
        xAxis = x;
        yAxis = y;
        UpdateSimulation(); 
    }

    public void SetFaceMode(FaceMode mode)
    {
        faceMode = mode;
        UpdateSimulation();
    }

    public void SetGhostMode(bool enabled)
    {
        if (visuals) 
        {
            visuals.SetGhostMode(enabled);
            // Force a refresh so it appears instantly
            UpdateSimulation(); 
        }
    }

    public void SetArrowMode(bool enabled)
    {
        if (visuals) visuals.SetArrowMode(enabled);
    }

    // --- HOVER LOGIC ---
    public void OnHoverEnter(Respondent r)
    {
        if (r == null || _currentLS == null || uiManager == null) return;

        if (visuals) visuals.SetHoverHighlight(r); // For showing the ghost arrow on hover

        int index = _populationList.IndexOf(r);
        if (index == -1) return;

        float currentLS = _currentLS[index];

        float uSelfCurrent = WelfareMetrics.GetUtilityForPerson(currentLS, r.personalUtilities);
        float uSociety = WelfareMetrics.EvaluateDistribution(_currentLS, r.societalUtilities);

        string info = $"<size=120%><b>Respondent #{r.id}</b></size>\n";
        info += $"<b>Life Satisfaction:</b> {currentLS:F2}/10\n";
        info += $"<b>Personal Utility:</b> {uSelfCurrent:F2}\n";
        info += $"<b>Societal Utility:</b> {uSociety:F2}\n";
        
        uiManager.UpdateHoverInfo(info);
    }

    public void OnHoverExit()
    {
        if (visuals) visuals.SetHoverHighlight(null); // for hovering off of the face
        CalculateAndRefreshUI(); 
    }

    void OnValidate()
    {
        if (Application.isPlaying && visuals != null && _populationList != null)
        {
            visuals.UpdateDisplay(_populationList.ToArray(), _currentLS, _baselineLS, _activePolicy, xAxis, yAxis, faceMode);
        }
    }
}