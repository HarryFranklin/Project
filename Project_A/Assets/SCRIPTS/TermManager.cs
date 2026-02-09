using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class TermManager : MonoBehaviour
{
    [Header("Game Rules")]
    public int maxTurns = 12; // 3 Years (4 quarters)
    
    [Header("Power Curve Settings")]
    public int basePower = 10;       // Starting power
    public int powerPerTurn = 5;     // How much it goes up each turn (on average)
    public int powerVariance = 3;    // Random +/- a bit (e.g. +/- 3)

    [Header("State")]
    public int currentTurn = 1;
    public int currentPower;
    public bool isGameActive = true;

    [Header("Data")]
    public List<Policy> allPoliciesPool; // Drag all Policy objects here

    [Header("References")]
    public SimulationManager simManager;
    
    [Header("UI Connections")]
    public TMP_Text turnText;        
    public TMP_Text powerText;       
    public Button[] choiceButtons;   
    public GameObject gameOverPanel; 
    public TMP_Text gameOverText;

    // Internal State
    private List<Policy> _currentHand = new List<Policy>();
    private DatabaseManager.GameSessionData _sessionData;

    void Start()
    {
        // 1. Init Database
        _sessionData = new DatabaseManager.GameSessionData();
        _sessionData.playerID = DatabaseManager.Instance ? "Player" : "Offline";
        _sessionData.timestamp = System.DateTime.Now.ToString();

        // 2. Start Game
        StartNewTurn();
    }

    void StartNewTurn()
    {
        if (!isGameActive) return;

        // Calc power
        currentPower = GetPowerForTurn(currentTurn);
        UpdateResourceUI();

        // Gather policy options
        DraftPolicies();
    }

    // 1. The Power Curve Function
    int GetPowerForTurn(int turn)
    {
        // Formula: Base + (Turn * Scale) + Random Variance
        // Turn 1 (index 0 for math): 10 + 0 + Rnd
        // Turn 2: 10 + 5 + Rnd...
        
        int linearGrowth = (turn - 1) * powerPerTurn;
        int variance = Random.Range(-powerVariance, powerVariance + 1);
        
        int calculatedPower = basePower + linearGrowth + variance;

        // Safety: Never let power be negative
        return Mathf.Max(5, calculatedPower);
    }

    // 2. Policy Drafting Function
    // 2. Policy Drafting Function
    void DraftPolicies()
    {
        _currentHand.Clear();
        
        // A. Separate policies into "Can Afford" and "Too Expensive"
        // We shuffle them first so we don't always pick the same 'cheap' ones
        var shuffledPool = allPoliciesPool.OrderBy(x => Random.value).ToList();
        
        var affordable = shuffledPool.Where(p => p.politicalCost <= currentPower).ToList();
        var others = shuffledPool.Where(p => p.politicalCost > currentPower).ToList();

        // B. Select 3 Cards
        
        // Slot 1: Must be affordable (Fallback to expensive if absolutely nothing else)
        if (affordable.Count > 0)
        {
            _currentHand.Add(affordable[0]);
            affordable.RemoveAt(0);
        }
        else if (others.Count > 0)
        {
            _currentHand.Add(others[0]);
            others.RemoveAt(0);
        }

        // Slot 2: Must be affordable
        if (affordable.Count > 0)
        {
            _currentHand.Add(affordable[0]);
            affordable.RemoveAt(0);
        }
        else if (others.Count > 0)
        {
            _currentHand.Add(others[0]);
            others.RemoveAt(0);
        }

        // Slot 3: Wildcard (The "Temptation" Slot)
        // Rule: Can be affordable OR slightly expensive (within +10 of current power)
        
        // 1. Filter the remaining expensive cards to only those within reach
        var closeReachOptions = others.Where(p => p.politicalCost <= currentPower + 10).ToList();
        
        // 2. Combine with remaining affordable cards
        var remaining = affordable.Concat(closeReachOptions).OrderBy(x => Random.value).ToList();
        
        if (remaining.Count > 0)
        {
            _currentHand.Add(remaining[0]);
        }

        // C. Shuffle the Hand
        // (So the "Expensive" card isn't always the 3rd button)
        _currentHand = _currentHand.OrderBy(x => Random.value).ToList();

        // D. Assign to Buttons
        AssignPoliciesToUI();
    }

    void AssignPoliciesToUI()
    {
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (i < _currentHand.Count)
            {
                Policy p = _currentHand[i];
                Button btn = choiceButtons[i];

                // Text Setup
                TMP_Text btnText = btn.GetComponentInChildren<TMP_Text>();
            
                // Colour for Cost text
                string costColour = "red";
                // string costColour = (p.politicalCost > currentPower) ? "red" : "white";
                if (btnText) btnText.text = $"{p.policyName}\n<size=80%><color={costColour}>(Cost: {p.politicalCost})</color></size>";
                
                // Interaction Setup
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnPolicyClicked(p));
                
                // If it's too expensive, you can see it but not click it (or click gives feedback)
                btn.interactable = true; 

                // Hover Logic (Preview)
                var hover = btn.GetComponent<ButtonHoverHandler>();
                if (hover == null) hover = btn.gameObject.AddComponent<ButtonHoverHandler>();
                hover.policy = p;
                hover.onHover = (pol) => simManager.PreviewPolicy(pol);
                hover.onExit = () => simManager.StopPreview();
            }
            else
            {
                // Hide unused buttons if we have less than 3 policies
                choiceButtons[i].gameObject.SetActive(false);
            }
        }
    }

    void OnPolicyClicked(Policy p)
    {
        if (currentPower >= p.politicalCost)
        {
            // 1. Pay & Act
            currentPower -= p.politicalCost;
            // Can change in future, depending on "use it or lose it" vs draining power, etc.
            
            simManager.ApplyPolicyEffect(p);
            LogTurn(p);

            // 2. Next Turn
            currentTurn++;
            if (currentTurn > maxTurns) EndGame("Term Finished");
            else StartNewTurn();
        }
        else
        {
            Debug.Log("Too expensive!");
            // Feedback: shake button? grey out?
        }
    }

    void LogTurn(Policy chosen)
    {
        DatabaseManager.TurnLog log = new DatabaseManager.TurnLog();
        log.turnNumber = currentTurn;
        log.chosenPolicy = chosen.policyName;
        log.costPaid = chosen.politicalCost;
        log.availableOptions = _currentHand.Select(p => p.policyName).ToArray();
        log.resultingFairness = simManager.GetCurrentSocietalFairness(); 

        _sessionData.turnHistory.Add(log);
    }

    void EndGame(string reason)
    {
        isGameActive = false;
        
        _sessionData.gameOverReason = reason;
        _sessionData.totalTurnsPlayed = currentTurn;
        // _sessionData.endAverageLS = simManager.GetCurrentAvgLS();
        
        if (DatabaseManager.Instance) 
            DatabaseManager.Instance.UploadGameResult(_sessionData);

        if (gameOverPanel) 
        {
            gameOverPanel.SetActive(true);
            if (gameOverText) gameOverText.text = "Term Ended.";
        }
    }

    void UpdateResourceUI()
    {
        if (turnText) turnText.text = $"Turn {currentTurn}/{maxTurns}";
        if (powerText) powerText.text = $"Political Capital: {currentPower}";
    }
}