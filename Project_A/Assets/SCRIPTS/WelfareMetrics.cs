using UnityEngine;
using System.Linq;

public static class WelfareMetrics
{
    // Real ONS Data (Updated UK Distribution)
    // Converted from percentages to normalised floats (0.0 to 1.0)
    public static readonly float[] ONS_Distribution = new float[] 
    {
        // Remember it's tiny numbers - 11% is 0.11, 0.4% is 0.004
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

    // Get ONS data, fix and normalise it
    public static float[] GetBaselineDistribution()
    {
        float[] distribution = (float[])ONS_Distribution.Clone();

        // If distribution[1] < distribution[0]
        if (distribution[0] > distribution[1])
        {
            // Swap them
            float temp = distribution[0]; 
            distribution[0] = distribution[1];
            distribution[1] = temp;
        }

        // Renormalise so sum is 1.0
        float sum = distribution.Sum();
        for (int i = 0; i < distribution.Length; i++) 
        {
            distribution[i] /= sum;
        }

        return distribution;
    }

    // Weighted dice roll for each person, weighted by ONS distribution
    public static int GetWeightedRandomLS(float[] distribution)
    {
        float r = Random.value;
        float cumulative = 0f;
        for (int i = 0; i < distribution.Length; i++)
        {
            cumulative += distribution[i];
            if (r <= cumulative) return i;
        }
        return 8; // Fallback
    }

    // --- 2. UTILITY FUNCTIONS ---
    // uSelf and uOthers calculations

    // Calculate U_self - ignore death, extrapolate the 0 value by using the slop from 4 to 2
    // Map ONS state to utility personal uSelf curve
    public static float GetUtilityForPerson(int lsScore, float[] uSelfCurve)
    {
        // 1. Fetch known utilities from the CSV
        float uDeath = uSelfCurve[0]; // State Death (Floor)
        float u2  = uSelfCurve[1]; // State E
        float u4  = uSelfCurve[2]; // State D
        float u6  = uSelfCurve[3]; // State C
        float u8  = uSelfCurve[4]; // State B
        float u10 = uSelfCurve[5]; // State A - perfect

        // Excluding death?
        // Exclude death because LS[0] means alive but miserable. Even if a policy pushes someone to LS[0], they're still alive
        // So don't assign people the death utility

        // 2. Handle the gap at LS 0 and LS 1 (Extrapolation)
        if (lsScore < 2) // is 1 or 0
        {
            // Calculate the slope between LS 4 and LS 2
            float delta = (u4 - u2) / 2.0f; 

            // If I am at LS 0, I am 2 steps below LS 2.
            // U_0 = U_2 - (Slope * 2)
            float extrapolatedZero = u2 - (delta * 2.0f);
            
            // Ensure it never drops below Death
            extrapolatedZero = Mathf.Max(extrapolatedZero, uDeath);
            
            // Lerp between zero and U2
            return Mathf.Lerp(extrapolatedZero, u2, lsScore / 2.0f);
        }
        
        // Map: 2->1, 4->2, 6->3, 8->4, 10->5
        float t = lsScore / 2.0f; 
        
        int lower = Mathf.FloorToInt(t);
        int upper = Mathf.CeilToInt(t);
        float lerp = t - lower;

        // Safety Clamp
        if (upper >= uSelfCurve.Length) return u10;

        return Mathf.Lerp(uSelfCurve[lower], uSelfCurve[upper], lerp);
    }

    // f: (LS_Array, UtilCurve) -> AvgUtility
    // Calculate U_others - take everyone's LS scores and that person's uOthers curve and calculate the average utility each person would get in this scenario
    public static float EvaluateDistribution(int[] populationLS, float[] respondentUOthersCurve)
    {
        double totalUtility = 0; // Double for precision

        // for every person in the society
        for (int i = 0; i < populationLS.Length; i++)
        {
            // how much utility would I get if I were this person?
            totalUtility += GetUtilityForPerson(populationLS[i], respondentUOthersCurve);
        }
        
        // Return the average
        return (float)(totalUtility / populationLS.Length);
    }
}