using _Codebase_.Entity.Destructible;
using _Codebase_.Entity.Player;
using UnityEngine;

namespace _Concrete_.Entities.Indestructible
{
    public class BrokenVase : DestructibleEntity
    {
        //TODO: set health to -1 in prefab
        [SerializeField]
        private float damage;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                other.gameObject.GetComponentInParent<PlayerEntity>().TakeDamage(this.damage);
            }
        }

        public override void OnBroken()
        {
        }
    }
}