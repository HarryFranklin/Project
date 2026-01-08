using UnityEngine;

public static class WelfareMetrics
{
    // Real ONS Data (Updated UK Distribution)
    // Converted from percentages to normalized floats (0.0 to 1.0)
    public static readonly float[] ONS_Distribution = new float[] 
    {
        // Remember it's tiny numbers - 11% is 0.11.

        0.0040f, // Tier 0: 0.40%
        0.0062f, // Tier 1: 0.62%
        0.0085f, // Tier 2: 0.85%
        0.0139f, // Tier 3: 1.39%
        0.0247f, // Tier 4: 2.47%
        0.0749f, // Tier 5: 7.49%
        0.0864f, // Tier 6: 8.64%
        0.2129f, // Tier 7: 21.29%
        0.3256f, // Tier 8: 32.56% (The Mode - Middle Class)
        0.1315f, // Tier 9: 13.15%
        0.1124f  // Tier 10: 11.24% (The Elite)
    };

    // Helper to get a random tier based on real UK stats
    public static int GetWeightedRandomTier()
    {
        float dice = Random.value; // Returns 0.0 to 1.0
        float cumulative = 0f;
        
        for (int i = 0; i < ONS_Distribution.Length; i++)
        {
            cumulative += ONS_Distribution[i];
            if (dice <= cumulative) return i;
        }
        return 8; // Otherwise, assume they're in the mode (tier 8)
    }

    // Calculates utility for ONE person based on their Tier + Curve
    public static float GetSinglePersonUtility(int tier, float[] utilityCurve)
    {
        int maxTierONS = 10;
        int maxCurveIndex = utilityCurve.Length - 1; // Usually 5

        // Map 0-10 Tier to 0-5 Curve
        float ratio = (float)tier / maxTierONS;
        float mappedIndex = ratio * maxCurveIndex;

        int lowerIndex = Mathf.FloorToInt(mappedIndex);
        int upperIndex = Mathf.CeilToInt(mappedIndex);
        float t = mappedIndex - lowerIndex;

        float valLower = utilityCurve[lowerIndex];
        float valUpper = utilityCurve[upperIndex];

        return Mathf.Lerp(valLower, valUpper, t);
    }

    // FUNCTION: f(LSarray, UtilCurve) -> Utility
    // Given a specific preference curve, calculates the average utility of a population 
    // Take everyone's status (population tiers of LS - i.e. 1, 5, 3 : means the three people are in tiers 1, 5 and 3), and use one person's specific curve to turn everyone's LS tier into a utility, and average them.
    // What utility does one derive from a given wealth distribution?
    public static float EvaluateDistribution(int[] populationTiers, float[] utilityCurve)
    {
        float totalUtility = 0;

        foreach (int tier in populationTiers)
        {
            totalUtility += GetSinglePersonUtility(tier, utilityCurve);
        }

        return totalUtility / populationTiers.Length;
    }
}