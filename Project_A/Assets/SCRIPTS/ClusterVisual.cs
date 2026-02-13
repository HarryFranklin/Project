using UnityEngine;
using UnityEngine.UI;

public class ClusterVisual : MonoBehaviour
{
    public int clusterID;
    public SimulationManager simManager;
    private VisualisationManager.GroupOpinion _data;

    public void UpdateVisuals(VisualisationManager.GroupOpinion data, SimulationManager manager)
    {
        _data = data;
        simManager = manager;
        
        this.clusterID = data.id; 
        
        // --- Scaling ---
        // UI uses pixels, so "1.0" scale is tiny. We might need to keep scale at 1,
        // and change sizeDelta (Width/Height) instead. 
        // OR: If your Respondents use Scale, we can use Scale too.
        // Let's stick to Scale for now, but be gentle.
        float size = 0.8f + (data.size / 100f); 
        transform.localScale = Vector3.one * size;
        
        // --- FIX 3: Transparency on UI Image ---
        var img = GetComponent<Image>();
        if (img) {
            Color c = img.color;
            c.a = 0.4f; // 40% Transparent
            img.color = c;
        }
    }

    void OnMouseEnter()
    {
        if (_data == null || simManager == null) return;

        string info = $"<size=120%>{_data.name}</size>\n";
        info += $"<b>Population:</b> {_data.size}\n";
        info += $"<b>Avg LS:</b> {_data.avgLS:F2}\n";
        info += $"<b>Avg Fairness:</b> {_data.avgSocietalFairness:F2}";

        simManager.uiManager.UpdateHoverInfo(info);
    }

    void OnMouseExit()
    {
        if (simManager) simManager.uiManager.UpdateHoverInfo("");
    }
}