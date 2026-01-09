using UnityEngine;
using System.Linq;

public static class WelfareMetrics
{
    // Real ONS Data (Updated UK Distribution)
    // Converted from percentages to normalised floats (0.0 to 1.0)
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

    public static float[] GetBaselineDistribution()
    {
        float[] dist = (float[])ONS_Distribution.Clone();

        // Check LS[0] < LS[1], if not, swap them
        if (dist[0] > dist[1])
        {
            float temp = dist[0]; 
            dist[0] = dist[1];
            dist[1] = temp;
        }

        // Renormalise so sum is 1.0
        float sum = dist.Sum();
        for (int i = 0; i < dist.Length; i++) dist[i] /= sum;

        return dist;
    }

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

    // Ignore Death (0), and extrapolate to 0 using the gap from 4 to 2
    // curve indices: = LS[i*2]
    // Ignore Death (0), and extrapolate to 0 using the gap from 4 to 2
    // curve indices: = LS[i*2]
    public static float GetUtilityForPerson(int lsScore, float[] curve)
    {
        // 1. Fetch known utilities from the CSV
        float uDeath = curve[0]; // State Death (Floor)
        float u2  = curve[1]; // State E
        float u4  = curve[2]; // State D
        float u6  = curve[3]; // State C
        float u8  = curve[4]; // State B
        float u10 = curve[5]; // State A - perfect

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
            
            // Interpolate between my new "Zero" and U2
            return Mathf.Lerp(extrapolatedZero, u2, lsScore / 2.0f);
        }
        
        // Map: 2->1, 4->2, 6->3, 8->4, 10->5
        float t = lsScore / 2.0f; 
        
        int lower = Mathf.FloorToInt(t);
        int upper = Mathf.CeilToInt(t);
        float lerp = t - lower;

        // Safety Clamp
        if (upper >= curve.Length) return u10;

        return Mathf.Lerp(curve[lower], curve[upper], lerp);
    }

    // f: (LS_Array, UtilCurve) -> Avg_Utility
    // Calculates the average utility ONE person perceives from a WHOLE population distribution
    public static float EvaluateDistribution(int[] populationLS, float[] evaluatorCurve)
    {
        double totalUtility = 0; // Double for precision
        for (int i = 0; i < populationLS.Length; i++)
        {
            totalUtility += GetUtilityForPerson(populationLS[i], evaluatorCurve);
        }
        return (float)(totalUtility / populationLS.Length);
    }
}