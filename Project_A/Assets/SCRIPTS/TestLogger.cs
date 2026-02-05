using UnityEngine;
using System.Collections.Generic;

public class TestLogger : MonoBehaviour
{
    // Local list to store clicks before uploading
    private List<string> _tempLog = new List<string>();

    public void OnRedPressed()
    {
        _tempLog.Add("RED");
        Debug.Log("Registered: RED");
    }

    public void OnBluePressed()
    {
        _tempLog.Add("BLUE");
        Debug.Log("Registered: BLUE");
    }

    // Call this with a third button "UPLOAD"
    public void UploadTestLog()
    {
        if (_tempLog.Count == 0)
        {
            Debug.LogWarning("Nothing to upload!");
            return;
        }

        // 1. Pack the data
        DatabaseManager.GameSessionData payload = new DatabaseManager.GameSessionData();
        payload.timestamp = System.DateTime.Now.ToString();
        payload.buttonLog = new List<string>(_tempLog); // Copy the list

        // 2. Send it
        DatabaseManager.Instance.UploadGameResult(payload);
    }
}