[System.Serializable]
public class Respondent 
{
    public int id;
    
    // The "Utility Curves" from the CSV (Death, 2, 4, 6, 8, 10)
    public float[] personalUtilities; 
    public float[] societalUtilities;
    
    // State
    public float currentLS;      // 0-10 Scale (now a float, but is an int from ONS data)
    public int wealthTier;     // 0-10 Scale (For future policies like Wealth Tax)

    // Each person has an ID and two utility arrays, u_Self and u_Others
    public Respondent(int id) 
    {
        this.id = id;
        personalUtilities = new float[6];
        societalUtilities = new float[6];
    }
}