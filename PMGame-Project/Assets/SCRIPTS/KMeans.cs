using UnityEngine;
using System.Collections.Generic; // Needed for Lists

public static class KMeans
{
    public struct Result
    {
        public Vector2[] Centres;
        public int[] Assignments;
    }

    public static Result GetClusters(Vector2[] points, int k, int maxIterations = 10)
    {
        // 1. Determinism
        Random.State oldState = Random.state;
        Random.InitState(42); 

        int count = points.Length;
        if (count == 0) { Random.state = oldState; return new Result(); }

        // Allocations
        Vector2[] centres = new Vector2[k];
        int[] assignments = new int[count];
        int[] pixelCounts = new int[k];
        Vector2[] runningSums = new Vector2[k];

        // Prevents clusters from spawning inside each other
        List<Vector2> centerList = new List<Vector2>();
        
        // 1. Pick first center randomly
        centerList.Add(points[Random.Range(0, count)]);

        // 2. Pick remaining centers based on distance
        for (int i = 1; i < k; i++)
        {
            float maxDistSq = -1f;
            Vector2 bestCandidate = Vector2.zero;

            // Check every point to find the one furthest from EXISTING centers
            for (int p = 0; p < count; p++)
            {
                Vector2 point = points[p];
                float distToNearest = float.MaxValue;

                // Find distance to closest existing center
                foreach (Vector2 c in centerList)
                {
                    float d = (point - c).sqrMagnitude;
                    if (d < distToNearest) distToNearest = d;
                }

                // Keep the point that is furthest away from everyone else
                if (distToNearest > maxDistSq)
                {
                    maxDistSq = distToNearest;
                    bestCandidate = point;
                }
            }
            centerList.Add(bestCandidate);
        }
        
        // Copy list to array
        for(int i=0; i<k; i++) centres[i] = centerList[i];

        bool changed = true;
        int iter = 0;

        // 3. The Loop (Standard Logic)
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

        Random.state = oldState;
        return new Result { Centres = centres, Assignments = assignments };
    }
}