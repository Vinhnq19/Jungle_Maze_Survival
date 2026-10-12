using _Codebase_.Entity.Player;
using System.Collections.Generic; // Thêm vào để sử dụng HashSet
using UnityEngine;

namespace Map
{
    public class DarkArea : MonoBehaviour
    {
        private BorderSystem _borderSystem;
        private float _timer;

        private void Awake()
        {
            _borderSystem = GetComponentInParent<BorderSystem>();
            if (_borderSystem == null)
            {
                Debug.LogError("DarkArea cannot find BorderSystem in parent!", this);
                this.enabled = false;
            }
        }

        private void Update()
        {
            if (_borderSystem == null) return;

            _timer += Time.deltaTime;

            if (_timer >= _borderSystem.TimeToDamagePlayers)
            {
                DamagePlayers();
                _timer = 0; // Reset timer
            }
        }

        private void DamagePlayers()
        {
            float damageAmount = _borderSystem.DarkAreaDamage * _borderSystem.TimeToDamagePlayers; //
            if (damageAmount <= 0) return;

            var contacts = Physics2D.OverlapBoxAll(
                (Vector2)transform.position,
                transform.localScale,
                0f,
                LayerMask.GetMask("Player")
            ); //

            HashSet<PlayerEntity> damagedPlayersThisTick = new HashSet<PlayerEntity>();

            foreach (var c in contacts)
            {
                if (c.gameObject.name == "Body")
                {
                    var player = c.GetComponentInParent<PlayerEntity>(); 
                    if (player != null) 
                    {
                        if (damagedPlayersThisTick.Add(player))
                        {
                            player.TakeDamage(damageAmount);
                        }
                    }
                }
            }
        }
    }
}