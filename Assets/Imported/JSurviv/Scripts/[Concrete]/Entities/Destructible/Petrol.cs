using System.Linq;
using _Codebase_.Entity.Destructible;
using _Codebase_.Entity.Player;
using UnityEngine;

namespace _Concrete_.Entities.Destructible
{
    public class Petrol : DestructibleEntity
    {
        [SerializeField]
        private float _explosionRange;

        [SerializeField]
        private LayerMask _playerLayer;

        [SerializeField]
        private int _explosionDamage;

        [SerializeField]
        private GameObject _explosionEffect;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(this.transform.position, this._explosionRange);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            var overlap = Physics2D.OverlapCircleAll(this.transform.position, this._explosionRange, this._playerLayer);
            var players = overlap.OfType<CircleCollider2D>().ToArray();

            foreach (var player in players)
            {
                player.gameObject.GetComponentInParent<PlayerEntity>().TakeDamage(this._explosionDamage);
            }

            if (this._explosionEffect != null)
            {
                Instantiate(this._explosionEffect, this.transform.position, Quaternion.identity);
            }

            Destroy(this.gameObject);
        }

        public override void OnBroken()
        {
        }
    }
}