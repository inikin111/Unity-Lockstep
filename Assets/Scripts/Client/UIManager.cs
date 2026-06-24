using TMPro;
using UnityEngine;

public class UIManager : MonoSingleton<UIManager>
{
    uint clientId;
    [SerializeField] TextMeshProUGUI idText;
    [SerializeField] TextMeshProUGUI frameText;
    [SerializeField] TextMeshProUGUI checksumText;

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
        if (checksumText == null)
        {
            Debug.LogError("Checksum Text is not assigned in the inspector.");
        }
    }

    public void SetClientId(uint clientId)
    {
        this.clientId = clientId;
        idText.text = $"Client ID: {clientId}";
    }

    public uint GetClientId()
    {
        return clientId;
    }

    public void UpdateFrame(uint frame)
    {
        frameText.text = $"Current Frame: {frame}";
    }

    public void UpdateChecksum(int checksum)
    {
        checksumText.text = $"GameState Checksum: {checksum}";
    }

}
