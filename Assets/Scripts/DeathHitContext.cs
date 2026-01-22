using UnityEngine;

public readonly struct DeathHitContext
{
    public readonly Vector3 PlayerWorldPos;
    public readonly Vector3 HitPointWorld;
    public readonly Vector3 CutNormalWorld;   // normalized
    public readonly Transform AttackerTip;    // optional

    public DeathHitContext(Vector3 playerWorldPos, Vector3 hitPointWorld, Vector3 cutNormalWorld, Transform attackerTip)
    {
        PlayerWorldPos = playerWorldPos;
        HitPointWorld = hitPointWorld;
        CutNormalWorld = cutNormalWorld.normalized;
        AttackerTip = attackerTip;
    }
}
