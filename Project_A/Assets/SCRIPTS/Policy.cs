using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPolicy", menuName = "Simulation/Policy")]
public class Policy : ScriptableObject
{
    public string policyName;
    [TextArea] public string description;

    public int politicalCost = 10;

    // 1. Broad Impact 
    [Header("1. Broad Base Impact")]
    [Tooltip("This applies to everyone based on their wealth tier, before the specific rules below.")]
    public float baseChangeRich = 0f;
    public float baseChangeMiddle = 0f;
    public float baseChangePoor = 0f;

    [Space(10)]
    [Header("Wealth Thresholds")]
    public float richThreshold = 8f;
    public float poorThreshold = 4f;

    // 2. Specific Targeting/Allocation Rules

    [System.Serializable]
    public struct TargetedRule
    {
        public string note; // e.g. "Help the desperate"
        
        [Header("Who?")]
        [Tooltip("Minimum Life Satisfaction to qualify")]
        public float minLS; 
        [Tooltip("Maximum Life Satisfaction to qualify")]
        public float maxLS;

        [Header("How Many?")]
        [Tooltip("If true, ignores the percentage and hits 100% of people in this bracket.")]
        public bool affectEveryone; 
        
        [Range(0f, 1f)]
        [Tooltip("0.5 = 50% of people in this bracket will get the effect.")]
        public float proportion; 

        [Header("What Effect?")]
        public float impact; 
    }

    [Header("2. Targeted Rules")]
    [Tooltip("Add specifics here (e.g. '50% of people with LS=2 get +4')")]
    public List<TargetedRule> specificRules;

    // --- LOGIC ---

    // f(LS[]) -> newLS
    public float[] ApplyPolicy(Respondent[] population)
    {
        float[] newLS = new float[population.Length];

        for (int i = 0; i < population.Length; i++)
        {
            Respondent r = population[i];
            float current = r.currentLS;
            float totalDelta = 0f;

            // 1. Apply Base Impact (Rich/Middle/Poor)
            if (current >= richThreshold) totalDelta += baseChangeRich;
            else if (current <= poorThreshold) totalDelta += baseChangePoor;
            else totalDelta += baseChangeMiddle;

            // 2. Process Targeted Rules
            foreach (var rule in specificRules)
            {
                if (current >= rule.minLS && current <= rule.maxLS)
                {
                    bool isSelected = false;
                    
                    if (rule.affectEveryone)
                    {
                        isSelected = true;
                    }
                    else
                    {
                        // Instead of Random.value, we create a pseudo-random 0-1 value 
                        // based on the person's ID and the policy's name.
                        // This ensures the same people are chosen every time you click/preview.
                        float seed = (float)((r.id * 1.58f + policyName.GetHashCode() * 0.72f) % 1.0f);
                        if (Mathf.Abs(seed) <= rule.proportion) isSelected = true;
                    }

                    if (isSelected)
                    {
                        totalDelta += rule.impact;
                    }
                }
            }

            newLS[i] = Mathf.Clamp(current + totalDelta, 0f, 10f);
        }

        return newLS;
    }
}