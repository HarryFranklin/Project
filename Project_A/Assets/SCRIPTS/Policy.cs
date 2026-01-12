using UnityEngine;

[CreateAssetMenu(fileName = "NewPolicy", menuName = "Simulation/Policy")]
public class Policy : ScriptableObject
{
    public string policyName;
    [TextArea] public string description;

    [Header("Thresholds")]
    [Tooltip("Anyone with this Life Satisfaction (LS) or HIGHER is considered 'Rich'")]
    public int richThreshold = 8; 

    [Tooltip("Anyone with this LS or LOWER is considered 'Poor'")]
    public int poorThreshold = 4;

    [Header("Impact Values")]
    [Tooltip("Change applied to the Rich")]
    public int changeForRich = 0;

    [Tooltip("Change applied to the Middle (everyone else)")]
    public int changeForMiddle = 0;

    [Tooltip("Change applied to the Poor")]
    public int changeForPoor = 0;


    // FUNCTION: f(LSarray) -> LSarray
    // Take the current LS data and change it based on this policy's parameters
    public int[] ApplyPolicy(Respondent[] population)
    {
        int[] newLS = new int[population.Length];

        for (int i = 0; i < population.Length; i++)
        {
            int current = population[i].currentLS;
            int delta = 0;

            if (current >= richThreshold)
            {
                delta = changeForRich;
            }
            else if (current <= poorThreshold)
            {
                delta = changeForPoor;
            }
            else
            {
                delta = changeForMiddle;
            }

            // Clamp ensures we stay within the 0-10 scale
            newLS[i] = Mathf.Clamp(current + delta, 0, 10);
        }

        return newLS;
    }
}