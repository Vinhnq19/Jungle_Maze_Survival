using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using UnityEngine;

namespace JungleMaze.Presentation.Player
{
    /// <summary>
    /// Local-only Hero movement (MOV-01): WASD (diagonal-normalized) drives a Rigidbody2D so Unity's
    /// physics engine blocks movement against solid colliders (Hard Cover, see ENV-01) for free, and the
    /// mouse smoothly aims the Hero using <see cref="HeroMotion"/>. Independent of the network stack on
    /// purpose: it does not read/write PlayerModel or send intent (see PlayerInputReader/PlayerInputService
    /// for that). Merging this local simulation with server state (prediction/reconciliation) is MOV-02's
    /// scope, not this component's.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class HeroController : MonoBehaviour
    {
        private const float FallbackSpeed = 4f;
        private const float FallbackAimLerpTime = 0.08f;
        private const float FallbackColliderRadius = 0.4f;

        private Rigidbody2D _body;
        private IBalanceConfigProvider _balance;
        private Vector2 _moveInput;
        private float _aimDegrees;
        private Camera _camera;

        public Vector2 MoveInput => _moveInput;
        public float AimDegrees => _aimDegrees;

        [Inject]
        public void Construct(IBalanceConfigProvider balance)
        {
            _balance = balance;
            // Scene objects are injected AFTER Awake; pooled objects may be injected BEFORE Awake.
            // Apply here as well so both creation paths use the configured radius.
            ApplyColliderConfig();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _aimDegrees = transform.eulerAngles.z;
            ApplyColliderConfig();
        }

        private void ApplyColliderConfig()
        {
            var radius = _balance != null ? _balance.Match.HeroColliderRadius : FallbackColliderRadius;
            if (TryGetComponent<CircleCollider2D>(out var collider)) collider.radius = radius;
        }

        private void Update()
        {
            _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (_camera == null) _camera = Camera.main;
            ApplyAim(_camera, Input.mousePosition, Time.deltaTime);
        }

        public void ApplyAim(Camera camera, Vector2 mouseScreen, float deltaTime)
        {
            if (!HeroAim.TryGetTargetDegrees(camera, transform.position, mouseScreen, out var targetDegrees)) return;
            var aimLerpTime = _balance != null ? _balance.Match.AimLerpTimeSeconds : FallbackAimLerpTime;
            _aimDegrees = HeroMotion.SmoothAimDegrees(_aimDegrees, targetDegrees, aimLerpTime, deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, _aimDegrees);
        }

        private void FixedUpdate() => ApplyMovement(_moveInput);

        /// <summary>Shared kinematics entry point; called by FixedUpdate, also usable by local acceptance tools.</summary>
        public void ApplyMovement(Vector2 input)
        {
            var speed = _balance != null ? _balance.Match.HeroBaseSpeed : FallbackSpeed;
            // deltaTime=1: ComputeMoveDelta returns a per-second rate here, i.e. a velocity, not a per-frame offset.
            var (vx, vy) = HeroMotion.ComputeMoveDelta(input.x, input.y, speed, 1f);
            _body.linearVelocity = new Vector2(vx, vy);
        }

        private void OnDisable()
        {
            _moveInput = Vector2.zero;
            if (_body != null) _body.linearVelocity = Vector2.zero;
        }
    }
}
