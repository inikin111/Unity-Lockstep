using TMPro;
using UnityEngine;

public class UIManager : MonoSingleton<UIManager>
{
    [SerializeField] TextMeshProUGUI idText;
    [SerializeField] TextMeshProUGUI frameText;

    void Awake()
    {
        if (idText == null)
        {
            Debug.LogError("ID Text is not assigned in the inspector.");
        }
        if (frameText == null)
        {
            Debug.LogError("Frame Text is not assigned in the inspector.");
        }
    }

    public void SetClientId(uint clientId)
    {
        idText.text = $"Client ID: {clientId}";
    }

    public void UpdateFrame(uint frame)
    {
        frameText.text = $"Current Frame: {frame}";
    }
}
