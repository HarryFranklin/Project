using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ClusterVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public int clusterID;
    public SimulationManager simManager;
    private VisualisationManager.GroupOpinion _data;

    public void UpdateVisuals(VisualisationManager.GroupOpinion data, SimulationManager manager)
    {
        _data = data;
        simManager = manager;
        this.clusterID = data.id; 
        
        // Scale based on population
        float size = 1.0f + (data.size / 80f); 
        transform.localScale = Vector3.one * size;
        
        // Transparency
        var img = GetComponent<Image>();
        if (img) {
            Color c = img.color;
            c.a = 0.4f; 
            img.color = c;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (simManager)
        {
            string info = $"<size=120%>{_data.name}</size>\n";
            info += $"Population: {_data.size}";
            simManager.uiManager.UpdateHoverInfo(info);

            simManager.visuals.SetClusterHighlight(clusterID);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (simManager)
        {
            simManager.uiManager.UpdateHoverInfo("");
            simManager.visuals.SetClusterHighlight(-1);
        }
    }
}