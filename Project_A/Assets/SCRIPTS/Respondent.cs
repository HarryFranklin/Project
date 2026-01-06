using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Respondent
{
    public int id;

    // Utilities: Death(0), 2, 4, 6, 8, 10
    //      Stored as an array of 6 ints
    // Indexes: 0 = Death, 1 = U_2, 2 = U_4, ...

    public float[] personalUtilities;
    public float[] societalUtilities;

    public Respondent(int id) 
    {
        this.id = id;
        personalUtilities = new float[6];
        societalUtilities = new float[6];
    }
}