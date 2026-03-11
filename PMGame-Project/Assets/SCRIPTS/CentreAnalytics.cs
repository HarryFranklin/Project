using UnityEngine;
using System.Collections.Generic;

public class ClusterAnalytics : MonoBehaviour
{
    [Header("Dependencies")]
    public SimulationManager simManager;

    [Header("Settings")]
    public int clusterCount = 4;
    
    // OUTPUT DATA:
    // List of "Group Opinions" that the UI can read
    public List<GroupOpinion> currentGroups = new List<GroupOpinion>();

    // Helper class to store the "Thought Bubble" of a cluster
    [System.Serializable]
    public class GroupOpinion
    {
        public int id;
        public int populationSize;
        public Vector2 centerPosition;
        
        // The averages for this group
        public float avgLS;
        public float avgSocietalFairness;
        public float[] avgSocietalUtilities; // The curve
    }

    public void GenerateClusters()
    {
        if (simManager.PopulationList == null || simManager.PopulationList.Count == 0) return;

        List<Respondent> pop = simManager.PopulationList;
        int count = pop.Count;

        // 1. Convert Population to Vector2[]
        // Cluster based on visual position (what the player sees)
        Vector2[] points = new Vector2[count];
        
        // We need to access the cached X/Y values. 
        // Ideally, VisualisationManager exposes these. 
        // For now, we quickly recalculate them to be safe.
        for (int i = 0; i < count; i++)
        {
            points[i] = GetRespondentPoint(pop[i]);
        }

        // 2. Run Efficient K-Means
        KMeans.Result result = KMeans.GetClusters(points, clusterCount);

        // 3. Aggregate Data (Turn indices back into "Thoughts")
        currentGroups.Clear();

        // Initialise groups
        for (int c = 0; c < clusterCount; c++)
        {
            currentGroups.Add(new GroupOpinion 
            { 
                id = c, 
                centerPosition = result.Centres[c],
                avgSocietalUtilities = new float[6] // Assuming 6-point curve
            });
        }

        // Accumulate totals
        for (int i = 0; i < count; i++)
        {
            int clusterIndex = result.Assignments[i];
            GroupOpinion group = currentGroups[clusterIndex];
            Respondent r = pop[i];

            group.populationSize++;
            group.avgLS += r.currentLS;
            
            // Calculate fairness for this person specifically
            float fair = WelfareMetrics.EvaluateDistribution(simManager.CurrentLS, r.societalUtilities);
            group.avgSocietalFairness += fair;

            // Sum up the curve
            for (int k = 0; k < 6; k++)
            {
                group.avgSocietalUtilities[k] += r.societalUtilities[k];
            }
        }

        // Finalise Averages
        foreach (var group in currentGroups)
        {
            if (group.populationSize > 0)
            {
                group.avgLS /= group.populationSize;
                group.avgSocietalFairness /= group.populationSize;
                for (int k = 0; k < 6; k++) group.avgSocietalUtilities[k] /= group.populationSize;
            }
        }
    }

    // Helper: Calculates where a person is on the current graph
    private Vector2 GetRespondentPoint(Respondent r)
    {
        // This logic mimics VisualisationManager.CalculateAxisValue
        // For efficiency, you might want to ask VisualisationManager for the cached values directly.
        // But this is fast enough for <10,000 agents.
        float x = GetVal(simManager.xAxis, r);
        float y = GetVal(simManager.yAxis, r);
        return new Vector2(x, y);
    }

    private float GetVal(AxisVariable axis, Respondent r)
    {
        switch (axis)
        {
            case AxisVariable.LifeSatisfaction: return r.currentLS;
            case AxisVariable.PersonalUtility: return WelfareMetrics.GetUtilityForPerson(r.currentLS, r.personalUtilities);
            case AxisVariable.SocietalFairness: return WelfareMetrics.EvaluateDistribution(simManager.CurrentLS, r.societalUtilities);
            case AxisVariable.Wealth: return r.wealthTier; 
            // Delta values are harder to calc here without baseline, 
            // assume 0 or handle if needed.
            default: return 0;
        }
    }
    
    // Debug Visualisation
    void OnDrawGizmos()
    {
        foreach(var g in currentGroups)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(g.centerPosition, 0.5f);
        }
    }
}