using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class WelfareMetrics
{
    // ONS Data (UK Life Satisfaction Distribution 0-10)
    private static readonly float[] ONS_Distribution_Raw = new float[]
    {
        0.0040f, 0.0030f, 0.0100f, 0.0210f, 0.0350f, 0.0750f, 
        0.1000f, 0.2240f, 0.3150f, 0.1510f, 0.0620f
        // Reject first 2, remember it's tiny tiny decimals
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
        return 8; // Fallback to most common if maths fails
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
    // How much empathy would I get if I had the LS of the average person in this population?
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

    // De-banding logic
    public static float GetContinuousWeightedLS(float[] distribution)
    {
        int anchor = GetWeightedRandomLS(distribution);
        float noise = RandomGaussian(0f, 0.4f);
        float result = anchor + noise;

        // Folded normal distribution logic.
        
        if (result > 10.0f)
        {
            float excess = result - 10.0f;
            result = 10.0f - excess;
        }

        if (result < 2.0f)
        {
            float excess = 2.0f - result;
            result = 2.0f + excess;
        }

        return Mathf.Clamp(result, 2.0f, 10.0f);
    }

    private static float RandomGaussian(float mean, float stdDev)
    {
        // Box-Muller Transform
        float u1 = 1.0f - Random.value; // Uniform(0,1]
        float u2 = 1.0f - Random.value;
        
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) *
                              Mathf.Sin(2.0f * Mathf.PI * u2); 
                              
        return mean + stdDev * randStdNormal;
    }
}