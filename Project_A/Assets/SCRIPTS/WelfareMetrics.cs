using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class WelfareMetrics
{
    // ONS Data (UK Life Satisfaction Distribution 0-10)
    private static readonly float[] ONS_Distribution_Raw = new float[]
    {
        // Remember it's tiny percentages. 0.06 is 6%.
        0.0040f, // 0 - Discard
        0.0030f, // 1 - Discard
        0.0100f, // 2
        0.0210f, // 3
        0.0350f, // 4
        0.0750f, // 5
        0.1000f, // 6
        0.2240f, // 7
        0.3150f, // 8
        0.1510f, // 9
        0.0620f  // 10
    };

    // Get ONS data, fix and normalise it
    public static float[] GetBaselineDistribution()
    {
        float[] dist = new float[11];
        float validTotal = 0;

        // 1. Calculate the total probability of valid states (2-10)
        for (int i = 2; i < 11; i++)
        {
            validTotal += ONS_Distribution_Raw[i];
        }

        // 2. Assign renormalised probabilities
        // Set 0 and 1 to 0
        dist[0] = 0.0f; 
        dist[1] = 0.0f;

        for (int i = 2; i < 11; i++)
        {
            // Renormalise: (Raw / Total valid) -> scales it up to 100%
            dist[i] = ONS_Distribution_Raw[i] / validTotal;
        }

        return dist;
    }

    // Weighted dice roll for each person, weighted by ONS distribution
    public static int GetWeightedRandomLS(float[] distribution)
    {
        float r = Random.value;
        float cumulative = 0f;
        for (int i = 2; i < distribution.Length; i++) // Start at 2 as there's no chance of anyone being 0 and 1
        {
            cumulative += distribution[i];
            if (r <= cumulative) return i;
        }
        return 10; // Fallback
    }

    // --- UTILITY FUNCTIONS ---
    // f:(LS) -> U 
    public static float GetUtilityForPerson(float lsScore, float[] curve)
    {
        // Case A: Death
        if (lsScore <= -0.9f) 
        {
            return curve[0]; // State F = death
        }

        // Case B: Alive (2-10)
        // Clamp to 2 to be safe
        float score = Mathf.Max(lsScore, 2f); // We have no utility data for below LS = 2, so it should be 2 at minimum

        // MAPPING LOGIC:
        // LS X -> Index X/2
        
        float exactIndex = score / 2.0f;
        
        // Interpolate so we can get values between the data points
        int lowerIndex = Mathf.FloorToInt(exactIndex);
        int upperIndex = Mathf.CeilToInt(exactIndex);
        
        // Safety clamp to valid range (1 to 5)
        if (lowerIndex < 1) lowerIndex = 1;
        if (upperIndex > 5) upperIndex = 5;
        if (lowerIndex > 5) lowerIndex = 5;

        float t = exactIndex - lowerIndex; 

        float valA = curve[lowerIndex];
        float valB = curve[upperIndex];

        return Mathf.Lerp(valA, valB, t);
    }

    // f: (LS_Array, UtilCurve) -> AvgUtility
    // Calculate U_others - take everyone's LS scores and that person's uOthers curve and calculate the average utility each person would get in this scenario
    public static float EvaluateDistribution(float[] populationLS, float[] respondentUOthersCurve)
    {
        double totalUtility = 0;

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