using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class TermManager : MonoBehaviour
{
    [Header("Game Rules")]
    public int maxTurns = 12; // 3 Years (4 quarters)
    public int powerPerTurn = 50;

    [Header("State")]
    public int currentTurn = 1;
    public int currentPower;
    public bool isGameActive = true;

    [Header("Data")]
    public List<Policy> allPoliciesPool; // Drag all Policy objects here

    [Header("References")]
    public SimulationManager simManager;
    
    // --- UI REFERENCES (Drag these in from Inspector) ---
    [Header("UI Connections")]
    public TMP_Text turnText;        // "Turn 1/12"
    public TMP_Text powerText;       // "Power: 50"
    public Button[] choiceButtons;   // The 3 buttons to select policies
    public GameObject gameOverPanel; // Panel to show at end
    public TMP_Text gameOverText;

    // Internal State
    private List<Policy> _currentHand = new List<Policy>();
    private DatabaseManager.GameSessionData _sessionData;

    void Start()
    {
        // 1. Init Database Session
        _sessionData = new DatabaseManager.GameSessionData();
        _sessionData.playerID = DatabaseManager.Instance ? "Player" : "Offline"; // Safety check
        _sessionData.timestamp = System.DateTime.Now.ToString();

        // 2. Start Game
        StartNewTurn();
    }

    void StartNewTurn()
    {
        if (!isGameActive) return;

        // Reset Power
        currentPower = powerPerTurn;
        UpdateResourceUI();

        // Draft 3 Policies
        DraftPolicies();
    }

    void DraftPolicies()
    {
        _currentHand.Clear();
        
        var shuffled = allPoliciesPool.OrderBy(x => Random.value).ToList();
        int count = Mathf.Min(3, shuffled.Count);

        for (int i = 0; i < count; i++)
        {
            Policy p = shuffled[i];
            _currentHand.Add(p);
            
            if (i < choiceButtons.Length)
            {
                Button btn = choiceButtons[i];

                // 1. Setup Text
                TMP_Text btnText = btn.GetComponentInChildren<TMP_Text>();
                if (btnText) btnText.text = $"{p.policyName}\n<size=80%>(Cost: {p.politicalCost})</size>";
                
                // 2. Setup Click (Select)
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnPolicyClicked(p));
                btn.interactable = true;

                // 3. Setup Hover (Preview)
                // Check if the script exists, add it if not
                ButtonHoverHandler hover = btn.GetComponent<ButtonHoverHandler>();
                if (hover == null) hover = btn.gameObject.AddComponent<ButtonHoverHandler>();

                // Assign the data
                hover.policy = p;
                hover.onHover = (pol) => simManager.PreviewPolicy(pol);
                hover.onExit = () => simManager.StopPreview();
            }
        }
    }
    

    void OnPolicyClicked(Policy p)
    {
        if (currentPower >= p.politicalCost)
        {
            // 1. Pay Cost
            currentPower -= p.politicalCost;
            
            // 2. Execute Policy in Simulation
            simManager.ApplyPolicyEffect(p);

            // 3. Log the Turn
            LogTurn(p);

            // 4. Advance Turn
            currentTurn++;
            if (currentTurn > maxTurns)
            {
                EndGame("Term Finished");
            }
            else
            {
                StartNewTurn();
            }
        }
        else
        {
            Debug.Log("Not enough power!");
            // Optional: Shake button or show red text
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
        
        // Final Metrics
        _sessionData.gameOverReason = reason;
        _sessionData.totalTurnsPlayed = currentTurn;
        _sessionData.endAverageLS = simManager.GetCurrentAvgLS();
        _sessionData.endSocietalFairness = simManager.GetCurrentSocietalFairness();

        // Upload
        if (DatabaseManager.Instance) 
            DatabaseManager.Instance.UploadGameResult(_sessionData);

        // Show Game Over UI
        if (gameOverPanel) 
        {
            gameOverPanel.SetActive(true);
            if (gameOverText) gameOverText.text = $"Term Ended.\nAvg Fairness: {_sessionData.endSocietalFairness:F2}";
        }
    }

    void UpdateResourceUI()
    {
        if (turnText) turnText.text = $"Turn {currentTurn}/{maxTurns}";
        if (powerText) powerText.text = $"Power: {currentPower}";
    }
}