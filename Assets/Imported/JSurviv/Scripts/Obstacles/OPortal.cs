using System.Collections;
using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using UnityEngine;

public class OPortal : ObstacleBase
{

    [SerializeField] private int type;
    [SerializeField] private Transform portalA;
    [SerializeField] private Transform portalB;

    private bool canTeleport = true;

    protected override void OnPlayerEnter(PlayerEntity playerEntity)
    {
        if (!canTeleport) return;

        base.OnPlayerEnter(playerEntity);

        Vector2 playerPos = playerEntity.transform.position; 

        if (Vector2.Distance(playerPos, portalA.position) < 0.5f)
        {
            Teleport(playerEntity, portalB.position);
        }
        else if (Vector2.Distance(playerPos, portalB.position) < 0.5f)
        {
            Teleport(playerEntity, portalA.position);
        }
    }

    private void Teleport(PlayerEntity player, Vector2 targetPos)
    {
        //them visual vao sau*

        player.transform.position = targetPos;
        StartCoroutine(TeleportCooldown());
    }

    private IEnumerator TeleportCooldown()
    {
        canTeleport = false;
        yield return new WaitForSeconds(5f);
        canTeleport = true;
    }
}
