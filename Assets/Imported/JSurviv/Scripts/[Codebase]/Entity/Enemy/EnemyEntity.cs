using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _Codebase_.Command;
using _Codebase_.Configs;
using _Codebase_.EffectSystem;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using _Concrete_.Effects;
using DG.Tweening;
using Managers;
using UnityEngine;
using Utils;

namespace _Codebase_.Entity.Enemy
{
    public class EnemyEntity : BaseMovableEntity, ICommandResolver
    {
        [SerializeField]
        private int moveRange;

        [SerializeField]
        private Orthogonal orthogonal;

        [SerializeField]
        private EnemyConfig enemyConfig;

        [field: SerializeField] public string Type { get; set; }
        // Nhap vao loai enemy tuong ung voi so thu tu o gdd
        [SerializeField]
        private int typeEnemy;

        private Vector2 _initialPosition;
        private Vector2 _moveValue;
        private float _timer;
        private Tween _tween;
        private Animator _animator;
        private bool _isVisualOnCooldown; // Cờ để đảm bảo coroutine chỉ chạy một lần

        //
        private LineRenderer _attackRangeRenderer;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private int circleSegmentCount = 40;
        //
        
        public bool is_cooldown { get; private set; }
        public int cooldown_step_left { get; private set; }
        // ------------------------------------

        public string TypeEnemy => this.Type;
        public EnemyConfig EnemyConfig => this.enemyConfig;
        
        private void Start()
        {
            MapManager.I.Enemies.Add(this);
            this.Visual = this.transform;
            this.Position = new(this.transform.position.x - 0.5f, this.transform.position.y - 0.5f);
            this._initialPosition = this.Position;
            this._moveValue = this.orthogonal == Orthogonal.Horizontal ? new(0, 1) : new Vector2(1, 0);
            this._animator = this.Visual.GetComponent<Animator>();
            this.CreateAttackRangeCircle();
        }

        private void Update()
        {
            this._timer += Time.deltaTime;

            if (this._timer >= GameConfig.I.stepTime)
            {
                this._timer = 0;

                if (this.is_cooldown)
                {
                    this.cooldown_step_left--;
                    if (this.cooldown_step_left <= 0)
                    {
                        this.is_cooldown = false;
                    }
                }

                if (Vector2.Distance(this.Position, this._initialPosition) >= this.moveRange || !this.CanMove(this.Position + this._moveValue))
                {
                    this.SwitchDirection();
                }

                this.Move(this._moveValue);
            }
        }

        private void OnEnable()
        {
            CommandManager.I.RegisterResolver(this);
        }

        private void OnDisable()
        {
            CommandManager.I.UnregisterResolver(this);
        }

        public void OnFrameUpdate()
        {
            this.impactPlayers();
        }

        public override bool CanMove(Vector2 newPosition)
        {
            return !(
                MapManager.I.IndestructiblePositions.Any(v => v == newPosition)
             || MapManager.I.Destructibles.Any(v => v.Positions.Contains(newPosition))
            );
        }

        public void impactPlayers()
        {
            if (this._animator != null)
            {
                this._animator.SetBool("isAttack", false);
            }
            var contacts = new List<Collider2D>();

            Physics2D.OverlapCircle(
                new(this.transform.position.x, this.transform.position.y),
                1.5f,
                new()
                {
                    layerMask = LayerMask.GetMask("Player"),
                    useLayerMask = true,
                },
                contacts
            );

            foreach (var c in contacts)
            {
                if (!c) continue;
                if (this._tween != null && this._tween.IsPlaying()) return;

                var target = c.GetComponentInParent<PlayerEntity>();
                if (!target) continue;

                if(this.is_cooldown) return; 
                
                this.is_cooldown = true;
                this.cooldown_step_left = this.enemyConfig.cooldownStep; 
                
                // Bắt đầu hiệu ứng hình ảnh và cooldown
                StartCoroutine(VisualizeActionAndCooldown()); 

                // Logic cho từng loại NPC
                if (typeEnemy == 1 || typeEnemy == 2 || typeEnemy == 5)
                {
                    target.TakeDamage(this.enemyConfig.damage);
                }
                if (typeEnemy == 3)
                {
                    target.AddEffect(new BleedEffect { Time = 3f, Interval = 1.0f, Damage = 5f });
                }
                if (typeEnemy == 4)
                {
                    target.AddEffect(new PoisonsEffect { Time = 5f, IntervalTime = 1.0f, Damage = 5f });
                }
                if (typeEnemy == 6)
                {
                    Debug.Log("Enemy 6: Hồi HP khi chạm");
                    var Hp = target.Stats.GetStatValue(_Codebase_.Entity._Abstract_.StatEnum.Health);
                    if (Hp < 50 && Hp > 0) Hp += 50;
                    else Hp = 100;
                    target.Stats.SetStatValue(_Codebase_.Entity._Abstract_.StatEnum.Health, Hp);
                }
                if (typeEnemy == 7)
                {
                    target.AddEffect(new StunEffect { Time = 1f });
                    target.TakeDamage(this.enemyConfig.damage);
                }
            }
        }

        /// <summary>
        /// Coroutine hợp nhất để xử lý hiệu ứng hình ảnh cho hành động và cooldown.
        /// Nó làm mờ nhân vật và chỉ hết mờ khi cooldown logic kết thúc.
        /// </summary>
        private IEnumerator VisualizeActionAndCooldown()
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            Color originalColor = sr.color;

            // Giai đoạn 1: Bắt đầu hành động và làm mờ nhân vật
            Color fadedColor = originalColor;
            fadedColor.a = 0.5f; // Làm mờ 50%
            sr.color = fadedColor;

            // Chạy animation tấn công/hồi máu
            _animator.SetBool("isAttack", true);
            yield return new WaitForSeconds(1.0f); // Thời gian của animation
            _animator.SetBool("isAttack", false);

            // Giai đoạn 2: Chờ cho đến khi cooldown logic kết thúc
            // Nhân vật sẽ duy trì trạng thái mờ trong suốt thời gian này.
            while (this.is_cooldown)
            {
                yield return null; // Chờ đến frame tiếp theo
            }

            // Giai đoạn 3: Cooldown đã hết, trả lại màu sắc ban đầu
            sr.color = originalColor;
        }

        // XÓA CÁC COROUTINE CŨ NÀY ĐI:
        // private IEnumerator WaitForHealingCoolDown() { ... }
        // private IEnumerator WaitForAttackingCoolDown() { ... }

        private void SwitchDirection()
        {
            this._moveValue *= -1;
        }

        private void CreateAttackRangeCircle()
        {
            int segments = circleSegmentCount;
            float angleStep = 2 * Mathf.PI / segments;

            for (int i = 0; i < segments; i++)
            {
                if (i % 2 == 0) continue;

                float angleStart = i * angleStep;
                float angleEnd = (i + 1) * angleStep;

                Vector3 start = new Vector3(Mathf.Cos(angleStart), Mathf.Sin(angleStart), 0) * attackRange;
                Vector3 end = new Vector3(Mathf.Cos(angleEnd), Mathf.Sin(angleEnd), 0) * attackRange;

                var segmentGO = new GameObject($"DashSegment_{i}");
                segmentGO.transform.SetParent(this.transform);
                segmentGO.transform.localPosition = Vector3.zero;

                var segmentRenderer = segmentGO.AddComponent<LineRenderer>();
                segmentRenderer.useWorldSpace = false;
                segmentRenderer.widthMultiplier = 0.05f;
                segmentRenderer.material = new Material(Shader.Find("Sprites/Default"));
                segmentRenderer.startColor = Color.red;
                segmentRenderer.endColor = Color.red;
                segmentRenderer.positionCount = 2;
                segmentRenderer.SetPosition(0, start);
                segmentRenderer.SetPosition(1, end);
            }
        }

        public List<Vector2> GetDangerZone()
        {
            var dangerPositions = new HashSet<Vector2>();

            for (int i = -moveRange; i <= moveRange; i++)
            {
                Vector2 patrolPos = this._initialPosition + this._moveValue * i;
                dangerPositions.Add(patrolPos);
            }

            var patrolPath = new List<Vector2>(dangerPositions); 
            foreach (var pathPoint in patrolPath)
            {
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if(Vector2.Distance(Vector2.zero, new Vector2(x, y)) <= attackRange)
                        {
                            dangerPositions.Add(pathPoint + new Vector2(x, y));
                        }
                    }
                }
            }
    
            return dangerPositions.ToList();
        }
    }
    
    public enum Orthogonal
    {
        Vertical,
        Horizontal,
    }
}