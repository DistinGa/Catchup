using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class Player : NetworkBehaviour
{
    [SerializeField] Transform Body;
    [SerializeField] Transform CamMountPoint;
    [SerializeField] Transform UIPanel;
    [SerializeField] Text HitCountPanel;
    [SerializeField] Text NametPanel;
    [SerializeField] ParticleSystem Particles;

    [SyncVar]
    public string PlayerName;
    [SyncVar]
    public int HitCount;
    public float BurstPeriod { get; private set; }
    public bool InBurst { get; private set; }
    public bool InHouse { get; private set; }

    float Speed, BurstSpeed;
    float burstDistance;
    float InHousePeriod;
    CharacterController controller;
    Material mat;
    Camera cam;
    Vector3 moveDirection;
    Action<Player, Player> transferCollision;   //для передачи столкновения (первыый - нападающий, второй - жертва)

    private void Start()
    {
        if (isServer)
        {
            AddListener(GameManager.Instance.ReceiveCollision);
        }

        Speed = GameManager.Instance.Speed;
        BurstSpeed = GameManager.Instance.BurstSpeed;
        burstDistance = GameManager.Instance.burstDistance;
        InHousePeriod = GameManager.Instance.InHousePeriod;
        mat = Body.GetComponent<MeshRenderer>().material;
        Particles = GetComponentInChildren<ParticleSystem>();
        HitCount = 0;
        NametPanel.text = PlayerName;
    }

    void Update()
    {
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
                controller.SimpleMove(moveDirection * BurstSpeed);
                BurstPeriod -= Time.deltaTime;
                if (BurstPeriod <= 0)
                {
                    CmdInBurst(false);
                }
            }
            else
            {
                moveDirection = (transform.forward * Input.GetAxis("Vertical") + transform.right * Input.GetAxis("Horizontal")).normalized;
                controller.SimpleMove(moveDirection * Speed);

                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    CmdInBurst(true);
                }
            }
        }

        //Для всех
        HitCountPanel.text = HitCount.ToString();
        UIPanel.LookAt(Camera.main.transform);
    }

    #region Server side
    [Command]
    public void CmdInBurst(bool State)
    {
        if(State)
            BurstPeriod = burstDistance / BurstSpeed;

        InBurst = State;
        RpcParticlesOn(State);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (isServer)
        {
            if (InBurst && hit.gameObject.tag == "Player")
            {
                transferCollision?.Invoke(this, hit.gameObject.GetComponentInParent<Player>());
            }
        }
    }

    [Server]
    public void AddListener(Action<Player, Player> lstnr)
    {
        transferCollision += lstnr;
    }

    /// <summary>
    /// Респаун при начале нового раунда (Сервер).
    /// </summary>
    /// <param name="lstnr"></param>
    [Server]
    public void InGameRespawn(Vector3 Position)
    {
        InHouse = false;
        InBurst = false;
        HitCount = 0;

        transform.SetPositionAndRotation(Position, transform.rotation);
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
        RpcChangePlayerColor(new Color(0, 1, 0));
        float counter = Duration;
        yield return new WaitForSeconds(Duration); ;
        InHouse = false;
        RpcChangePlayerColor(new Color(1, 1, 1));
    }

    [Command]
    public void CmdSetPlayerName(string Name)
    {
        PlayerName = Name;
        RpcSetPlayerName(Name);
    }

    #endregion

    #region Client side
    public override void OnStartLocalPlayer()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main;
        cam.transform.parent = CamMountPoint;
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = CamMountPoint.localRotation;
        Cursor.lockState = CursorLockMode.Confined;
        //Cursor.visible = false;
        CmdSetPlayerName(FindObjectOfType<StartHUD>().playerName);

        base.OnStartLocalPlayer();
    }

    [ClientRpc]
    public void RpcChangePlayerColor(Color newColor)
    {
        if (isClient)
            mat.color = newColor;
    }

    [ClientRpc]
    public void RpcParticlesOn(bool State)
    {
        if (State)
            Particles.Play();
        else
            Particles.Stop();
    }

    [ClientRpc]
    public void RpcSetPlayerName(string Name)
    {
        PlayerName = Name;
        NametPanel.text = PlayerName;
    }

    #endregion
}
