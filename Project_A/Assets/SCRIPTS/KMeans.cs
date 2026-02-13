using UnityEngine;

public static class KMeans
{
    public struct Result
    {
        public Vector2[] Centres;
        public int[] Assignments;
    }

    public static Result GetClusters(Vector2[] points, int k, int maxIterations = 10)
    {
        // Save the old state so we don't mess up the rest of the game's RNG
        Random.State oldState = Random.state;
        
        // Force a specific seed. This ensures the random starting points
        // are identical every time you run this, eliminating the "wobble".
        Random.InitState(42); 

        int count = points.Length;
        if (count == 0) 
        {
            Random.state = oldState;
            return new Result();
        }

        // Allocations
        Vector2[] centres = new Vector2[k];
        int[] assignments = new int[count];
        int[] pixelCounts = new int[k];
        Vector2[] runningSums = new Vector2[k];

        // 1. Initialise with random points (Now Deterministic)
        for (int i = 0; i < k; i++)
            centres[i] = points[Random.Range(0, count)];

        bool changed = true;
        int iter = 0;

        // 2. The Loop
        while (changed && iter < maxIterations)
        {
            changed = false;
            System.Array.Clear(pixelCounts, 0, k);
            System.Array.Clear(runningSums, 0, k);

            // A. Assign
            for (int i = 0; i < count; i++)
            {
                Vector2 p = points[i];
                int bestC = 0;
                float bestSq = float.MaxValue;

                for (int c = 0; c < k; c++)
                {
                    float d = (p - centres[c]).sqrMagnitude;
                    if (d < bestSq) { bestSq = d; bestC = c; }
                }

                if (assignments[i] != bestC) { assignments[i] = bestC; changed = true; }
                
                pixelCounts[bestC]++;
                runningSums[bestC] += p;
            }

            // B. Recenter
            for (int c = 0; c < k; c++)
            {
                if (pixelCounts[c] > 0) centres[c] = runningSums[c] / pixelCounts[c];
            }
            iter++;
        }

        // Restore randomness for the rest of the game
        Random.state = oldState;

        return new Result { Centres = centres, Assignments = assignments };
    }
}