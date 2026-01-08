using UnityEngine;

[CreateAssetMenu(fileName = "NewPolicy", menuName = "Simulation/Policy")]
public class Policy : ScriptableObject
{
    public string policyName;

    public string description;
    
    [Header("Wealth Transform")]
    [Tooltip("Amount of steps to move UP or DOWN.")]
    public int wealthChange = 0;
    
    [Header("Redistribution Logic")]
    public bool isRedistributive = false;
    // Standard redistributive thresholds (e.g. Tax rich, help poor)
    public int taxThreshold = 0; 
    public int benefitThreshold = 0;

    [Header("Societal Lift")]
    [Tooltip("Global improvement (e.g. better NHS) applied to everyone.")]
    public int societalBaseLift = 0;

    // The Function: f(LS) -> LS
    public int ResolveNewTier(int currentTier)
    {
        int delta = 0;

        // 1. Wealth Redistribution Logic
        if (isRedistributive)
        {
            if (currentTier >= taxThreshold) // Rich enough to be taxed for it
            {
                delta -= Mathf.Abs(wealthChange);
            }
            else if (currentTier <= benefitThreshold)  // Poor enough to benefit from it
            {
                delta += Mathf.Abs(wealthChange);
            }
        }
        else
        {
            // Flat impact (e.g. Austerity hits everyone)
            delta += wealthChange;
        }

        // 2. Societal Lift
        delta += societalBaseLift;

        // 3. Return valid 0-10 tier
        return Mathf.Clamp(currentTier + delta, 0, 10);
    }
}