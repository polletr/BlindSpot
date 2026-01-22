using UnityEngine;

public class DeathCircleVisual : MonoBehaviour
{
    [Header("Renderers")]
    [SerializeField] private SpriteRenderer halfPos;
    [SerializeField] private SpriteRenderer halfNeg;

    [Header("Tuning")]
    [SerializeField] private float edgeWidth = 0.06f;
    [SerializeField] private float edgeGlow = 2.0f;
    [SerializeField] private Color edgeColor = Color.white;

    private MaterialPropertyBlock _mpbPos;
    private MaterialPropertyBlock _mpbNeg;

    private static readonly int CutCenterID = Shader.PropertyToID("_CutCenter");
    private static readonly int CutNormalID = Shader.PropertyToID("_CutNormal");
    private static readonly int HitPointID = Shader.PropertyToID("_HitPoint");
    private static readonly int ProgressID = Shader.PropertyToID("_Progress");
    private static readonly int SeparationID = Shader.PropertyToID("_Separation");
    private static readonly int EdgeWidthID = Shader.PropertyToID("_EdgeWidth");
    private static readonly int EdgeGlowID = Shader.PropertyToID("_EdgeGlow");
    private static readonly int EdgeColorID = Shader.PropertyToID("_EdgeColor");
    private static readonly int SideID = Shader.PropertyToID("_Side");

    public void InitFromPlayer(Sprite playerSprite, Color playerTint)
    {
        halfPos.sprite = playerSprite;
        halfNeg.sprite = playerSprite;
        halfPos.color = playerTint;
        halfNeg.color = playerTint;

        _mpbPos = new MaterialPropertyBlock();
        _mpbNeg = new MaterialPropertyBlock();

        // Ensure side values are correct (so both halves can share the same material asset)
        ApplyCommon(_mpbPos, side: 1f);
        ApplyCommon(_mpbNeg, side: -1f);

        halfPos.SetPropertyBlock(_mpbPos);
        halfNeg.SetPropertyBlock(_mpbNeg);
    }

    private void ApplyCommon(MaterialPropertyBlock mpb, float side)
    {
        mpb.SetFloat(SideID, side);
        mpb.SetFloat(EdgeWidthID, edgeWidth);
        mpb.SetFloat(EdgeGlowID, edgeGlow);
        mpb.SetColor(EdgeColorID, edgeColor);
        mpb.SetFloat(ProgressID, 0f);
        mpb.SetFloat(SeparationID, 0f);
    }

    public void SetCut(in DeathHitContext ctx)
    {
        // Slice plane now originates from the attacker tip so the gap starts where the hit happened.
        _mpbPos.SetVector(CutCenterID, ctx.HitPointWorld);
        _mpbNeg.SetVector(CutCenterID, ctx.HitPointWorld);

        _mpbPos.SetVector(CutNormalID, ctx.CutNormalWorld);
        _mpbNeg.SetVector(CutNormalID, ctx.CutNormalWorld);

        _mpbPos.SetVector(HitPointID, ctx.HitPointWorld);
        _mpbNeg.SetVector(HitPointID, ctx.HitPointWorld);

        halfPos.SetPropertyBlock(_mpbPos);
        halfNeg.SetPropertyBlock(_mpbNeg);
    }

    public void SetProgress(float p)
    {
        _mpbPos.SetFloat(ProgressID, p);
        _mpbNeg.SetFloat(ProgressID, p);
        halfPos.SetPropertyBlock(_mpbPos);
        halfNeg.SetPropertyBlock(_mpbNeg);
    }

    public void SetSeparation(float s)
    {
        _mpbPos.SetFloat(SeparationID, s);
        _mpbNeg.SetFloat(SeparationID, s);
        halfPos.SetPropertyBlock(_mpbPos);
        halfNeg.SetPropertyBlock(_mpbNeg);
    }
}
