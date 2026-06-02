using TMPro;
using UnityEngine;

public class UIManager : MonoSingleton<UIManager>
{
    [SerializeField] TextMeshProUGUI idText;
    [SerializeField] TextMeshProUGUI frameText;

    public void SetClientId(uint clientId)
    {
        idText.text = $"Client ID: {clientId}";
    }

    public void UpdateFrame(uint frame)
    {
        frameText.text = $"Current Frame: {frame}";
    }
}
