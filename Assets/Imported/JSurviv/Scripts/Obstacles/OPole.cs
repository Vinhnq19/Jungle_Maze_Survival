using System.Collections;
using System.Linq;
using _Codebase_.Entity.Player;
using Game;
using Managers;
using UnityEngine;
using _Codebase_.EffectSystem;
using _Concrete_.Effects;

public class OPole : ObstacleBase
{
    [SerializeField] private GameObject thunder;
    [SerializeField] private float thunderRange;
    [SerializeField] private float rangeRadius;
    [SerializeField] private float thunderInterval;

    protected override void Start()
    {
        base.Start();
        StartCoroutine(IEAutoSpawnThunder());
    }

    private IEnumerator IEAutoSpawnThunder()
    {
        yield return new WaitUntil(() => GameManager.I.WaitingToStartTimer <= 0f);

        while(true)
        {
            yield return new WaitForSeconds(thunderInterval);

            Vector2 randPos = UnityEngine.Random.insideUnitCircle * rangeRadius;
            thunder.SetActive(true);
            thunder.transform.position = randPos;

            yield return new WaitForSeconds(1f);

            var overlap = Physics2D.OverlapCircleAll(transform.position, thunderRange, LayerMask.GetMask("Player"));
            var players = overlap.OfType<Collider2D>().ToArray();

            foreach (var player in players)
            {
                player.gameObject.GetComponentInParent<PlayerEntity>().AddEffect(new BuffDamageEffect()
                {
                    Time = 50f,
                    DamageBonus = 0.5f,
                });
            }

            yield return new WaitForSeconds(2f);

            thunder.SetActive(false);

        }
    }


    #region Thunder
    //Set Danh
    protected override void OnPlayerEnter(PlayerEntity playerEntity)
    {
        base.OnPlayerEnter(playerEntity);

    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        base.OnTriggerEnter2D(collision);
    }

    #endregion

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, rangeRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(thunder.transform.position, thunderRange);
    }
}
