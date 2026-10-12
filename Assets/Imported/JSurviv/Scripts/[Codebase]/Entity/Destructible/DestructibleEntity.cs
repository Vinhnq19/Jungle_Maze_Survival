using System.Collections.Generic;
using System.Linq;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using _Concrete_.Entities.Destructible;
using DG.Tweening;
using Managers;
using UnityEngine;

namespace _Codebase_.Entity.Destructible
{
    public abstract class DestructibleEntity : MonoBehaviour, IDamageable
    {
        [SerializeField]
        protected float _health;
        
        [SerializeField] private float _punchStrength = 0.2f;
        [SerializeField] private float _punchDuration = 0.2f;
        // ------------------------------------------------------

        [SerializeField]
        public Vector2Int[] _tookPositions =
        {
            new(0, 0),
        };

        protected float MaxHealth;
        private Tween _hurtTween; 

        public List<Vector2> Positions { get; } = new();

        public Vector2? PublicPosition => this.Positions.Count > 0 ? this.Positions[0] : null;
        public float PublicHealth => this._health;

        private void Start()
        {
            this.MaxHealth = this._health;
            var originX = this.transform.position.x - .5f;
            var originY = this.transform.position.y - .5f;

            this.Positions.Clear();

            this.Positions.AddRange(
                this._tookPositions.Select(pos => new Vector2(
                    originX + pos.x,
                    originY + pos.y
                ))
            );
        }

        public void TakeDamage(float damage, PlayerEntity src = null)
        {
            if (this is Petrol) return;
            this._health -= damage;

            if (this._health <= 0)
            { 
                Debug.Log("hello world");
                this.SelfDestroy(); 
            }
            else this.RunHurtAnim();
        }

        public abstract void OnBroken();

        public void SelfDestroy()
        {
            MapManager.I.Destructibles.Remove(this);
            this.OnBroken();
            Destroy(this.gameObject);
        }

        public virtual void RunHurtAnim()
        {
            _hurtTween?.Kill();

            _hurtTween = this.transform.DOPunchScale(
                Vector3.one * -_punchStrength, 
                _punchDuration, 
                vibrato: 0, 
                elasticity: 1
            );
        }
        
        private void OnDestroy()
        {
            if (MapManager.I != null)
            {
                MapManager.I.Destructibles.Remove(this);
            }
            _hurtTween?.Kill();
        }
    }
}