using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class StartHUD : NetworkManagerHUD
{
    public string playerName;

    void OnGUI()
    {
        if (!NetworkClient.isConnected && !NetworkServer.active)
        {
            GUILayout.BeginArea(new Rect(10 + offsetX, 10, 215, 20));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Player name");
            playerName = GUILayout.TextField(playerName);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        base.OnGUI();
    }
}
