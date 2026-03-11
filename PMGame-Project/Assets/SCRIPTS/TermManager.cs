using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class TermManager : MonoBehaviour
{
    public enum TurnState { Drafting, Locked }

    [Header("Game Rules")]
    public int maxTurns = 12; 
    public int basePower = 10;       
    public int powerPerTurn = 5;     
    public int powerVariance = 3;    

    [Header("State")]
    public TurnState currentState = TurnState.Drafting;
    public int currentTurn = 1;
    public int currentPower;
    public bool isGameActive = true;

    [Header("Data")]
    public List<Policy> allPoliciesPool; 

    [Header("References")]
    public SimulationManager simManager;
    public VisualisationManager visualisationManager;
    
    [Header("UI Connections")]
    public TMP_Text turnText;        
    public TMP_Text powerText;       
    public Button[] choiceButtons;   
    public GameObject gameOverPanel; 
    public TMP_Text gameOverText;

    [Header("Confirmation UI")]
    public GameObject confirmPanel;   // Drag your new "Confirm/Cancel" panel here
    public Button confirmButton;
    public Button cancelButton;

    // Internal State
    private List<Policy> _currentHand = new List<Policy>();
    private List<Policy> _policyDeck;
    private DatabaseManager.GameSessionData _sessionData;
    private Policy _selectedPolicy; // The policy currently locked in

    void Start()
    {
        _sessionData = new DatabaseManager.GameSessionData();
        _sessionData.playerID = DatabaseManager.Instance ? "Player" : "Offline";
        _sessionData.timestamp = System.DateTime.Now.ToString();

        _policyDeck = new List<Policy>(allPoliciesPool);

        if (confirmPanel) confirmPanel.SetActive(false);
        if (confirmButton) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton) cancelButton.onClick.AddListener(OnCancelClicked);

        StartNewTurn();
    }

    void StartNewTurn()
    {
        if (!isGameActive) return;

        // Reset State
        currentState = TurnState.Drafting;
        _selectedPolicy = null;
        simManager.SetPreviewLock(false);
        if (confirmPanel) confirmPanel.SetActive(false);

        // Calc power
        currentPower = GetPowerForTurn(currentTurn);
        UpdateResourceUI();

        // Show Options
        DraftPolicies();
    }

    int GetPowerForTurn(int turn)
    {
        int linearGrowth = (turn - 1) * powerPerTurn;
        int variance = Random.Range(-powerVariance, powerVariance + 1);
        int calculatedPower = basePower + linearGrowth + variance;
        return Mathf.Max(5, calculatedPower);
    }

    void DraftPolicies()
    {
        _currentHand.Clear();
        if (_policyDeck.Count == 0) { AssignPoliciesToUI(); return; }

        var shuffledPool = _policyDeck.OrderBy(x => Random.value).ToList();
        var affordable = shuffledPool.Where(p => p.politicalCost <= currentPower).ToList();
        var others = shuffledPool.Where(p => p.politicalCost > currentPower).ToList();

        // 1. Affordable
        if (affordable.Count > 0) { _currentHand.Add(affordable[0]); affordable.RemoveAt(0); }
        else if (others.Count > 0) { _currentHand.Add(others[0]); others.RemoveAt(0); }

        // 2. Affordable
        if (affordable.Count > 0) { _currentHand.Add(affordable[0]); affordable.RemoveAt(0); }
        else if (others.Count > 0) { _currentHand.Add(others[0]); others.RemoveAt(0); }

        // 3. Wildcard
        var closeReach = others.Where(p => p.politicalCost <= currentPower + 10).ToList();
        var wildcardPool = affordable.Concat(closeReach).OrderBy(x => Random.value).ToList();
        if (wildcardPool.Count > 0) _currentHand.Add(wildcardPool[0]);
        else if (others.Count > 0) _currentHand.Add(others[0]);

        _currentHand = _currentHand.OrderBy(x => Random.value).ToList();
        AssignPoliciesToUI();
    }

    void AssignPoliciesToUI()
    {
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (i < _currentHand.Count)
            {
                choiceButtons[i].gameObject.SetActive(true);
                choiceButtons[i].interactable = true; // Make clickable

                Policy p = _currentHand[i];
                Button btn = choiceButtons[i];
                TMP_Text btnText = btn.GetComponentInChildren<TMP_Text>();

                // Colour for Cost text
                string costColour = "red";
                // string costColour = (p.politicalCost > currentPower) ? "red" : "white";
                if (btnText) btnText.text = $"{p.policyName}\n<size=80%><color={costColour}>(Cost: {p.politicalCost})</color></size>";

                
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnPolicySelected(p)); // Select, don't buy yet

                // Hover Logic
                var hover = btn.GetComponent<ButtonHoverHandler>();
                if (hover == null) hover = btn.gameObject.AddComponent<ButtonHoverHandler>();
                hover.policy = p;
                hover.onHover = (pol) => simManager.PreviewPolicy(pol);
                hover.onExit = () => simManager.StopPreview();
            }
            else
            {
                choiceButtons[i].gameObject.SetActive(false);
            }
        }
    }

    // --- PHASE 1 - SELECT TO PREVIEW ---
    void OnPolicySelected(Policy p)
    {
        if (currentPower < p.politicalCost)
        {
            Debug.Log("Too expensive!");
            return;
        }

        // 1. Lock State
        currentState = TurnState.Locked;
        _selectedPolicy = p;

        // 2. Lock Visuals
        simManager.PreviewPolicy(p);     // Force show it one last time
        simManager.SetPreviewLock(true); // Prevent it from disappearing

        // 3. Update UI
        // Hide all policy buttons so they can't change their mind without canceling
        foreach (var btn in choiceButtons) btn.gameObject.SetActive(false);
        
        // Show Confirm Panel
        if (confirmPanel) confirmPanel.SetActive(true);
    }

    // --- PHASE 2 - CONFIRMATION ---
    void OnConfirmClicked()
    {
        if (_selectedPolicy == null) return;

        // 1. Pay & Act
        currentPower -= _selectedPolicy.politicalCost;
        simManager.SetPreviewLock(false); // Unlock so Apply can run normally
        simManager.ApplyPolicyEffect(_selectedPolicy);
        
        // 2. Remove from Deck
        if (_policyDeck.Contains(_selectedPolicy)) _policyDeck.Remove(_selectedPolicy);

        // 3. Log
        LogTurn(_selectedPolicy);

        // 4. Next Turn
        currentTurn++;
        if (currentTurn > maxTurns) EndGame("Term Finished");
        else StartNewTurn();
    }

    void OnCancelClicked()
    {
        currentState = TurnState.Drafting;
        _selectedPolicy = null;
        simManager.SetPreviewLock(false);
        simManager.StopPreview();
        
        // Add this to reset zoom visually when going back to drafting
        visualisationManager.ResetZoom(); 

        if (confirmPanel) confirmPanel.SetActive(false);
        AssignPoliciesToUI();
    }

    // --- LOGGING & END GAME ---
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
        if (DatabaseManager.Instance) DatabaseManager.Instance.UploadGameResult(_sessionData);
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