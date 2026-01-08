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
        return 8; // Fallback to Mode (Tier 8)
    }

    // FUNCTION: f(LSarray, UtilCurve) -> Utility
    // Given a specific preference curve, calculates the average utility of a population 
    // Take everyone's status (population tiers of LS - i.e. 1, 5, 3 : means the three people are in tiers 1, 5 and 3), and use one person's specific curve to turn everyone's LS tier into a utility, and average them.
    // What utility does one derive from a given wealth distribution?
    public static float EvaluateDistribution(int[] populationTiers, float[] utilityCurve)
    {
        float totalUtility = 0;
        int maxTierONS = 10; // (0-10)
        int maxCurveIndex = 5; // is utilityCurve.Length - 1. (0-5)

        foreach (int tier in populationTiers) // foreach person, grab their LS tier ()
        {
            // 1. Calc the ratio, i.e. a 5/10 on the ONS is a 2.5/5 on the curve
            float ratio = (float)tier / maxTierONS;

            // 2. Map this to the curve
            // e.g. 0.5 * 5 = 2.5 (so we want the value halfway between 2 and 3)
            float mappedIndex = ratio * maxCurveIndex;

            // 3. Find upper and lower
            int lowerIndex = Mathf.FloorToInt(mappedIndex); // floor, so 2
            int upperIndex = Mathf.CeilToInt(mappedIndex); // ceil, so 3

            // 4. Difference between true and lower (grab the decimal)
            float t = mappedIndex - lowerIndex; // 0.5

            // 5. Lerp between upper and lower
            float valLower = utilityCurve[lowerIndex];
            float valUpper = utilityCurve[upperIndex];

            // Finally giving the utility value that isn't clamped to an integer
            totalUtility += Mathf.Lerp(valLower, valUpper, t); // start at lower, and lerp t towards upper (i.e. 50%)
        }

        return totalUtility / populationTiers.Length;
    }
}