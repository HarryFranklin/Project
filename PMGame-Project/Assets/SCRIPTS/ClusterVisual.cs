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

        // Logarithmic scaling for better visibility
        float logSize = 0.6f + Mathf.Log10(data.size + 1) * 0.25f; 
        transform.localScale = Vector3.one * logSize;

        var img = GetComponent<Image>();
        if (img && simManager.visuals.clusterColors != null && simManager.visuals.clusterColors.Length > 0)
        {
            // Get the representative colour for this group
            Color clusterColor = simManager.visuals.clusterColors[clusterID % simManager.visuals.clusterColors.Length];

            // Invert the colour for high contrast (1 - component)
            // We keep the alpha low (0.3f) so it remains a background "halo"
            //Color invertedColor = new Color(1f - clusterColor.r, 1f - clusterColor.g, 1f - clusterColor.b, 0.3f);
            Color invertedColor = new Color(0f, 0f, 0f, 0.7f); // black 
            
            img.color = invertedColor;
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