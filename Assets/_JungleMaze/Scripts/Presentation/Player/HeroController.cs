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

        [Inject]
        public void Construct(IBalanceConfigProvider balance) => _balance = balance;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _aimDegrees = transform.eulerAngles.z;

            var radius = _balance != null ? _balance.Match.HeroColliderRadius : FallbackColliderRadius;
            if (TryGetComponent<CircleCollider2D>(out var collider)) collider.radius = radius;
        }

        private void Update()
        {
            _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            var aimScreen = (Vector2)Input.mousePosition - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var targetDegrees = Mathf.Atan2(aimScreen.y, aimScreen.x) * Mathf.Rad2Deg;
            var aimLerpTime = _balance != null ? _balance.Match.AimLerpTimeSeconds : FallbackAimLerpTime;
            _aimDegrees = HeroMotion.SmoothAimDegrees(_aimDegrees, targetDegrees, aimLerpTime, Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, _aimDegrees);
        }

        private void FixedUpdate()
        {
            var speed = _balance != null ? _balance.Match.HeroBaseSpeed : FallbackSpeed;
            // deltaTime=1: ComputeMoveDelta returns a per-second rate here, i.e. a velocity, not a per-frame offset.
            var (vx, vy) = HeroMotion.ComputeMoveDelta(_moveInput.x, _moveInput.y, speed, 1f);
            _body.linearVelocity = new Vector2(vx, vy);
        }
    }
}
