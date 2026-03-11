using System.Collections.Generic;
using UnityEngine;

public class DataReader : MonoBehaviour 
{
    [Header("CSV Files")]
    public TextAsset personalFile;
    public TextAsset societalFile;

    // Parse the two .csv files, give IDs and make the main dict of all people/all respondents
    public Dictionary<int, Respondent> GetRespondents() 
    {
        Dictionary<int, Respondent> respondents = new Dictionary<int, Respondent>();

        // 1. Parse Personal Utilities
        if (personalFile != null) 
        {
            string[] lines = personalFile.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) 
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cols = lines[i].Split(',');

                if (int.TryParse(cols[0], out int id)) 
                {
                    Respondent r = new Respondent(id);
                    r.personalUtilities[0] = float.Parse(cols[1]); // Death
                    r.personalUtilities[1] = float.Parse(cols[2]); // U2
                    r.personalUtilities[2] = float.Parse(cols[3]); // U4
                    r.personalUtilities[3] = float.Parse(cols[4]); // U6
                    r.personalUtilities[4] = float.Parse(cols[5]); // U8
                    r.personalUtilities[5] = float.Parse(cols[6]); // U10
                    respondents[id] = r;
                }
            }
        }

        // 2. Parse Societal Utilities
        if (societalFile != null) 
        {
            string[] lines = societalFile.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) 
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cols = lines[i].Split(',');

                if (int.TryParse(cols[0], out int id)) 
                {
                    if (respondents.ContainsKey(id)) 
                    {
                        Respondent r = respondents[id];
                        r.societalUtilities[0] = float.Parse(cols[1]);
                        r.societalUtilities[1] = float.Parse(cols[2]);
                        r.societalUtilities[2] = float.Parse(cols[3]);
                        r.societalUtilities[3] = float.Parse(cols[4]);
                        r.societalUtilities[4] = float.Parse(cols[5]);
                        r.societalUtilities[5] = float.Parse(cols[6]);
                    }
                }
            }
        }

        Debug.Log($"DataReader: Parsed {respondents.Count} respondents.");
        return respondents;
    }
}