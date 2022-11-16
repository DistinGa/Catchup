using UnityEngine;
using Mirror;

public interface IPlayer
{
    int HitCount { get; set; }
    string PlayerName { get; set; }
    bool InBurst { get; set; }
    bool InHouse { get; set; }
    float BurstPeriod { get; set; }
    void TakeHit();
    void TargetInGameRespawn(NetworkConnection con, Vector3 Position);
}
