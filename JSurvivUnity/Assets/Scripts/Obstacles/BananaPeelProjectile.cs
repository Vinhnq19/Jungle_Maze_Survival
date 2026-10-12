using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using _Concrete_.Effects;
using System.Linq;
using UnityEngine;
public class BananaPeelProjectile : ObstacleBase {
    protected override void Start()
    {
        base.Start();
    }

    protected override void OnPlayerEnter(PlayerEntity playerEntity)
    {
        playerEntity.TakeDamage(peelDamage);

        // add effect
        var stunEffect = new StunEffect
        {
            Time = this.StunTime
        };

        playerEntity.AddEffect(stunEffect);

        //nga~

        Debug.Log("Banana peel hit player: " + playerEntity.name);
        Destroy(this.gameObject);
    }

    [field: SerializeField]
    public int peelDamage { get; private set; }

    [field: SerializeField]
    public int StunTime { get; private set; }
}