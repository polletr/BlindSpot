using UnityEngine;

public class EnemyTipKill : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        if (!player.IsDead)
            player.KillPlayer();
    }
}
