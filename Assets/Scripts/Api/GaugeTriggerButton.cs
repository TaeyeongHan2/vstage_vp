using UnityEngine;

public class GaugeTriggerButton : MonoBehaviour
{
    public WebSocketVoiceClient voiceClient;

    public void OnGaugeFullButtonClick()
    {
        if (voiceClient == null)
        {
            Debug.LogError("WebSocketVoiceClient가 연결되지 않았습니다.");
            return;
        }

        if (voiceClient.IsTriggerConnected)
        {
            voiceClient.SendGaugeSignal();
        }
        else
        {
            Debug.LogWarning("Trigger WebSocket이 아직 연결되지 않았습니다. 전송 중단");
        }
    }
}