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
    [Tooltip("�������� �����")]
    public float BurstSpeed;
    [Tooltip("���������, �� ������� ���������� �����")]
    public float burstDistance = 15;
    [Tooltip("������� ������ ����� ��� ������")]
    public int VictoryCondition = 3;
    [Tooltip("����� ������������ ����� ��������� �����")]
    public float InHousePeriod = 3f;

    private void Awake()
    {
        if (Instance != null)
            Destroy(gameObject);

        Instance = this;
    }

    #region Server side
    [Server]
    public void ReceiveCollision(IPlayer Attacker, IPlayer Victim)
    {
        //��� ������ �������� �����, ��� � �������.
        if (!Victim.InHouse && (!Victim.InBurst || Attacker.BurstPeriod < Victim.BurstPeriod))
        {
            Attacker.HitCount++;
            Victim.TakeHit();

            if (Attacker.HitCount == VictoryCondition)
                StartCoroutine("DelayedRestart", Attacker.PlayerName);
        }
    }

    IEnumerator DelayedRestart(string WinnerName)
    {
        RpcShowWinner(WinnerName);

        yield return (new WaitForSeconds(5));

        List<Transform> _respawnPoints = GameObject.FindGameObjectsWithTag("Respawn").Select(item => item.transform).ToList();
        Transform _curResp;
        IPlayer _p;
        foreach (GameObject item in GameObject.FindGameObjectsWithTag("Player"))
        {
            //� ������ ��� �������� ����� ��� "Player". ������� ���� ������ �������� �������.
            if (item.transform.parent != null)
                continue;

            _p = item.GetComponent<IPlayer>();
            _curResp = _respawnPoints[Random.Range(0, _respawnPoints.Count)];
            _p.InHouse = false;
            _p.InBurst = false;
            _p.HitCount = 0;
            _p.TargetInGameRespawn(item.GetComponent<NetworkIdentity>().connectionToClient, _curResp.position);

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
