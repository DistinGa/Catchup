using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System.Linq;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public Text WinnerNamePanel;
    public float Speed;
    [Tooltip("—корость рывка")]
    public float BurstSpeed;
    [Tooltip("ƒистанци€, на которую происходит рывок")]
    public float burstDistance = 15;
    [Tooltip("—колько ударов нужно дл€ победы")]
    public int VictoryCondition = 3;
    [Tooltip("¬рем€ неу€звимости после получени€ удара")]
    public float InHousePeriod = 3f;

    private void Awake()
    {
        if (Instance != null)
            Destroy(gameObject);

        Instance = this;
    }

    #region Server side
    [Server]
    public void ReceiveCollision(Player Attacker, Player Victim)
    {
        // то раньше запустил рывок, тот и выиграл.
        if (!Victim.InHouse && (!Victim.InBurst || Attacker.BurstPeriod < Victim.BurstPeriod))
        {
            Attacker.HitCount++;
            Victim.TakeHit();

            if (Attacker.HitCount == VictoryCondition)
                StartCoroutine("DelayedRestart", Attacker.PlayerName);
        }
    }

    [Server]
    public void StopTheGame(string WinnerName)
    {
        StartCoroutine("DelayedRestart", WinnerName);
    }

    IEnumerator DelayedRestart(string WinnerName)
    {
        RpcShowWinner(WinnerName);

        yield return (new WaitForSeconds(5));

        List<Transform> _respawnPoints = GameObject.FindGameObjectsWithTag("Respawn").Select(item => item.transform).ToList();
        Transform _curResp;
        foreach (Player item in FindObjectsOfType(typeof(Player)))
        {
            _curResp = _respawnPoints[Random.Range(0, _respawnPoints.Count)];
            item.InGameRespawn(_curResp.position);
            _respawnPoints.Remove(_curResp);
        }

        RpcHideWinner();
    }
    #endregion

    #region Client side
    [ClientRpc]
    void RpcShowWinner(string WinnerName)
    {
        WinnerNamePanel.text = WinnerName;
        WinnerNamePanel.transform.parent.gameObject.SetActive(true);
    }

    [ClientRpc]
    void RpcHideWinner()
    {
        WinnerNamePanel.transform.parent.gameObject.SetActive(false);
    }
    #endregion
}
