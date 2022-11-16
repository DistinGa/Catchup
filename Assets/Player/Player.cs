using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class Player : NetworkBehaviour, IPlayer
{
    [SerializeField] Transform Body;
    [SerializeField] Transform CamMountPoint;
    [SerializeField] Transform UIPanel;
    [SerializeField] Text HitCountPanel;
    [SerializeField] Text NametPanel;
    [SerializeField] ParticleSystem Particles;

    [SyncVar(hook = "SetPlayerName")][HideInInspector]
    public string SyncPlayerName;

    [SyncVar(hook = "SetHitCount")][HideInInspector]
    public int SyncHitCount;

    [SyncVar(hook = "StartParticles")][HideInInspector]
    public bool SyncInBurst;

    [SyncVar(hook = "ChangePlayerColor")][HideInInspector]
    public bool SyncInHouse;

    public float BurstPeriod { get; set; }
    public bool InHouse { get => SyncInHouse; set => SyncInHouse = value; }
    public int HitCount { get => SyncHitCount; set => SyncHitCount = value; }
    public string PlayerName { get => SyncPlayerName; set => SyncPlayerName = value; }
    public bool InBurst { get => SyncInBurst; set => SyncInBurst = value; }

    float Speed, BurstSpeed;
    float burstDistance;
    float InHousePeriod;
    CharacterController controller;
    Material mat;
    Camera cam;
    Vector3 moveDirection;
    Action<IPlayer, IPlayer> transferCollision;   //Оповещение о столкновении с другим игроком (первыый - нападающий, второй - жертва).

    private void Start()
    {
        AddListener(GameManager.Instance.ReceiveCollision);

        Speed = GameManager.Instance.Speed;
        BurstSpeed = GameManager.Instance.BurstSpeed;
        burstDistance = GameManager.Instance.burstDistance;
        InHousePeriod = GameManager.Instance.InHousePeriod;
        mat = Body.GetComponent<MeshRenderer>().material;
        HitCount = 0;
    }

    void Update()
    {
        //Для всех.
        UIPanel.LookAt(Camera.main.transform);

        //Только для игрока.
        if (isLocalPlayer)
        {
            transform.Rotate(Vector3.up, Input.GetAxis("Mouse X") * Time.deltaTime);

            cam.transform.Rotate(Vector3.left, Input.GetAxis("Mouse Y") * Time.deltaTime);

            Vector3 eulerRotation = cam.transform.localRotation.eulerAngles;
            if (eulerRotation.x > 60f && eulerRotation.x < 180f)
                cam.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            if (eulerRotation.x < 300f && eulerRotation.x > 180f)
                cam.transform.localRotation = Quaternion.Euler(300f, 0f, 0f);

            if (InBurst)
            {
                //if (moveDirection == Vector3.zero)
                //    moveDirection = transform.forward;

                controller.SimpleMove(moveDirection * BurstSpeed);
            }
            else
            {
                moveDirection = (transform.forward * Input.GetAxis("Vertical") + transform.right * Input.GetAxis("Horizontal")).normalized;
                controller.SimpleMove(moveDirection * Speed);

                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    CmdSetInBurst(true);
                }
            }
        }

        if (isServer)
        {
            if (InBurst)
            {
                BurstPeriod -= Time.deltaTime;
                if (BurstPeriod <= 0)
                    SetInBurst(false);
            }
        }
    }

    void OnDestroy()
    {
        Destroy(mat);
    }

    #region Server side
    [Command]
    public void CmdSetInBurst(bool State)
    {
        SetInBurst(State);
    }

    [Server]
    void SetInBurst(bool State)
    {
        if (State)
            BurstPeriod = burstDistance / BurstSpeed;

        InBurst = State;
    }

    [Command]
    void CmdCollision(uint AttackerId, uint VictimId)
    {
        IPlayer _attacker = NetworkClient.spawned[AttackerId].GetComponent<IPlayer>();
        IPlayer _victim = NetworkClient.spawned[VictimId].GetComponent<IPlayer>();
        transferCollision?.Invoke(_attacker, _victim);
    }

    public void AddListener(Action<IPlayer, IPlayer> lstnr)
    {
        transferCollision += lstnr;
    }

    /// <summary>
    /// Получение удара (Сервер).
    /// </summary>
    [Server]
    public void TakeHit()
    {
        StartCoroutine("StayInHouse", InHousePeriod);
    }

    //Поддержание состояния неуязвимости.
    IEnumerator StayInHouse(float Duration)
    {
        InHouse = true;
        float counter = Duration;
        yield return new WaitForSeconds(Duration); ;
        InHouse = false;
    }

    [Command]
    public void CmdSetPlayerName(string Name)
    {
        PlayerName = Name;
    }

    #endregion

    #region Client side
    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Confined;
        //Cursor.visible = false;
        controller = GetComponent<CharacterController>();
        cam = Camera.main;
        cam.transform.parent = CamMountPoint;
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = CamMountPoint.localRotation;
        Particles = GetComponentInChildren<ParticleSystem>();
        CmdSetPlayerName(FindObjectOfType<StartHUD>().playerName);  //Имя нужно закинуть на сервер, чтобы он мог определить победителя.

        base.OnStartLocalPlayer();
    }
    
    public override void OnStopLocalPlayer()
    {
        cam.transform.parent = null;

        base.OnStopLocalPlayer();
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (InBurst && hit.gameObject.tag == "Player")
        {
            CmdCollision(GetComponent<NetworkIdentity>().netId, hit.gameObject.GetComponentInParent<NetworkIdentity>().netId);
        }
    }

    void ChangePlayerColor(bool oldInHouse, bool newInHouse)
    {
        if(newInHouse)
            mat.color = new Color(0, 1, 0);
        else
            mat.color = new Color(1, 1, 1);
    }

    void StartParticles(bool oldValue, bool newValue)
    {
        if (newValue)
            Particles.Play();
        else
            Particles.Stop();
    }

    void SetPlayerName(string oldName, string newName)
    {
        NametPanel.text = newName;
    }

    void SetHitCount(int oldValue, int newValue)
    {
        HitCountPanel.text = newValue.ToString();
    }

    /// <summary>
    /// Респаун при начале нового раунда. Клиент, т.к. NetworkTransform управляется с клиента.
    /// </summary>
    /// <param name="lstnr"></param>
    [TargetRpc]
    public void TargetInGameRespawn(NetworkConnection con, Vector3 Position)
    {
        transform.SetPositionAndRotation(Position, transform.rotation);
    }

    #endregion
}
