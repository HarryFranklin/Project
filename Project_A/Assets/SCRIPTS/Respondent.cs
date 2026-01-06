[System.Serializable]
public class Respondent 
{
    public int id;
    public float[] personalUtilities; // Indices: 0=Death, 1=E, 2=D, 3=C, 4=B, 5=A
    public float[] societalUtilities;
    
    // 0=Death, 1=Destitute ... 5=Thriving
    public int currentTier = 2; // Default to middle
    public float happinessWithPolicy = 0f; 

    public Respondent(int id) 
    {
        this.id = id;
        personalUtilities = new float[6];
        societalUtilities = new float[6];
    }
}