using UnityEngine;

[CreateAssetMenu(fileName = "NewPolicy", menuName = "Simulation/Policy", order = 1)]
public class Policy : ScriptableObject 
{
    public string policyName = "New Policy";
    
    [Header("Impact Settings")]
    [Tooltip("Positive = Cost to individual. Negative = Benefit to individual.")]
    [Range(-5, 5)] public int taxSeverity = 0;   

    [Tooltip("Positive = Society improves. Negative = Society worsens.")]
    [Range(-5, 5)] public int socialGain = 0;    

    [Header("Advanced Logic")]
    [Tooltip("If TRUE, the 'Tax Severity' is applied based on wealth.\nRich people pay the tax.\nPoor people RECEIVE the tax value as a benefit.")]
    public bool isRedistributive = false;

    public float CalculateImpact(Respondent r) 
    {
        // 1. DETERMINE PERSONAL IMPACT
        int calculatedTax = taxSeverity;

        if (isRedistributive)
        {
            // REDISTRIBUTIVE LOGIC (UBI)
            // Tier 0, 1, 2 (Poor/Mid): They GAIN wealth (Tax becomes negative)
            // Tier 3, 4, 5 (Rich): They LOSE wealth (Tax stays positive)
            
            if (r.currentTier < 3) 
            {
                // I am poor, so the 'Tax' is actually a payment TO me.
                // We invert the tax severity.
                calculatedTax = -Mathf.Abs(taxSeverity); 
            }
            else 
            {
                // I am rich, so I pay the full tax.
                calculatedTax = Mathf.Abs(taxSeverity);
            }
        }

        // 2. APPLY TO PERSONAL LADDER
        int currentPersonalTier = r.currentTier;
        // Subtracting a negative tax means adding wealth
        int newPersonalTier = Mathf.Clamp(currentPersonalTier - calculatedTax, 0, 5);
        
        float personalUtilityOld = r.personalUtilities[currentPersonalTier];
        float personalUtilityNew = r.personalUtilities[newPersonalTier];
        
        float personalChange = personalUtilityNew - personalUtilityOld; 

        // 3. APPLY TO SOCIETAL LADDER
        // UBI usually has a high social gain (poverty reduction)
        int currentSocietalTier = 2; 
        int newSocietalTier = Mathf.Clamp(currentSocietalTier + socialGain, 0, 5);

        float societalUtilityOld = r.societalUtilities[currentSocietalTier];
        float societalUtilityNew = r.societalUtilities[newSocietalTier];

        float societalChange = societalUtilityNew - societalUtilityOld; 

        // 4. THE TRADE-OFF
        return personalChange + societalChange;
    }
}