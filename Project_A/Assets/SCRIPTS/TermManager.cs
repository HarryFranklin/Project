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
    private List<Policy> _policyDeck;
    private DatabaseManager.GameSessionData _sessionData;

    void Start()
    {
        // 1. Init Database
        _sessionData = new DatabaseManager.GameSessionData();
        _sessionData.playerID = DatabaseManager.Instance ? "Player" : "Offline";
        _sessionData.timestamp = System.DateTime.Now.ToString();

        // 2. Create the Deck
        // We copy the pool so we can remove items without deleting them from the Project
        _policyDeck = new List<Policy>(allPoliciesPool);

        // 3. Start Game
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

    // 1. Power Curve Function
    int GetPowerForTurn(int turn)
    {
        int linearGrowth = (turn - 1) * powerPerTurn;
        int variance = Random.Range(-powerVariance, powerVariance + 1);
        int calculatedPower = basePower + linearGrowth + variance;

        return Mathf.Max(5, calculatedPower);
    }

    // 2. Policy Drafting Function
    void DraftPolicies()
    {
        _currentHand.Clear();

        // Safety: If deck is empty, stop.
        if (_policyDeck.Count == 0)
        {
            Debug.Log("No policies left.");
            AssignPoliciesToUI();
            return;
        }

        // A. Shuffle the entire remaining deck
        // This ensures random selection. We work with this temporary list to select cards.
        var shuffledPool = _policyDeck.OrderBy(x => Random.value).ToList();
        
        // B. Categorise (Affordable vs Expensive)
        var affordable = shuffledPool.Where(p => p.politicalCost <= currentPower).ToList();
        var others = shuffledPool.Where(p => p.politicalCost > currentPower).ToList();

        // C. Fill the 3 Slots
        // Logic: Try to get 2 Affordable + 1 Wildcard (Affordable or Close-Reach)

        // Slot 1: Must be affordable
        if (affordable.Count > 0)
        {
            _currentHand.Add(affordable[0]);
            affordable.RemoveAt(0); // Remove so we don't pick it again for Slot 2
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

        // Slot 3: Wildcard (Affordable or Expensive but within 10 of max)
        // Filter 'others' to only show things within +10 cost
        var closeReach = others.Where(p => p.politicalCost <= currentPower + 10).ToList();
        
        // Combine remaining affordable + close reach
        var wildcardPool = affordable.Concat(closeReach).OrderBy(x => Random.value).ToList();

        if (wildcardPool.Count > 0)
        {
            _currentHand.Add(wildcardPool[0]);
        }
        else if (others.Count > 0) // Fallback: just show any expensive card if nothing else exists
        {
             _currentHand.Add(others[0]);
        }

        // D. Shuffle the Hand (Visuals)
        // We shuffle the final 3 cards so the "Expensive" one isn't always on the right button
        _currentHand = _currentHand.OrderBy(x => Random.value).ToList();

        // E. Assign to Buttons
        AssignPoliciesToUI();
    }

    void AssignPoliciesToUI()
    {
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (i < _currentHand.Count)
            {
                // Show Button
                choiceButtons[i].gameObject.SetActive(true);

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
                btn.interactable = true; 

                // Hover Logic
                var hover = btn.GetComponent<ButtonHoverHandler>();
                if (hover == null) hover = btn.gameObject.AddComponent<ButtonHoverHandler>();
                hover.policy = p;
                hover.onHover = (pol) => simManager.PreviewPolicy(pol);
                hover.onExit = () => simManager.StopPreview();
            }
            else
            {
                // Hide unused buttons (e.g. if we ran out of cards and only have 2 left)
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
            simManager.ApplyPolicyEffect(p);
            
            // 2. Remove from Deck (No duplicates)
            if (_policyDeck.Contains(p))
            {
                _policyDeck.Remove(p);
            }

            // 3. Log
            LogTurn(p);

            // 4. Next Turn
            currentTurn++;
            if (currentTurn > maxTurns) EndGame("Term Finished");
            else StartNewTurn();
        }
        else
        {
            Debug.Log("Too expensive!");
            // Or shake button
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