using JungleMaze.Core.Pooling;
using UnityEngine;

namespace JungleMaze.Presentation.Combat
{
    /// <summary>
    /// Pooled melee swing: the weapon sprite sweeps across the configured arc around the wielder, then the pool
    /// despawns it. Visual only; the sprite never decides hits.
    /// </summary>
    public sealed class MeleeSwingVisual : MonoBehaviour, IPoolable
    {
        /// <summary>Sprite offset from the wielder, as a fraction of the weapon range in cells.</summary>
        private const float ReachFraction = 0.6f;

        [SerializeField] private SpriteRenderer spriteRenderer;

        private Transform _anchor;

        /// <summary>Finds the child sprite when the prefab leaves the field empty.</summary>
        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
        private float _startDegrees;
        private float _arcDegrees;
        private float _duration;
        private float _elapsed;
        private bool _swinging;

        /// <summary>Starts the swing. The anchor is followed every frame so the arc stays on the wielder.</summary>
        public void Play(Sprite sprite, Transform anchor, float aimDegrees, float arcDegrees, float rangeCells, float durationSeconds)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
                spriteRenderer.transform.localPosition = new Vector3(rangeCells * ReachFraction, 0f, 0f);
            }
            _anchor = anchor;
            _startDegrees = aimDegrees - arcDegrees * 0.5f;
            _arcDegrees = arcDegrees;
            _duration = Mathf.Max(0.01f, durationSeconds);
            _elapsed = 0f;
            _swinging = true;
            Apply(0f);
        }

        private void Update()
        {
            if (!_swinging) return;
            _elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(_elapsed / _duration);
            Apply(t);
            if (t >= 1f) _swinging = false;
        }

        private void Apply(float t)
        {
            if (_anchor != null) transform.position = _anchor.position;
            transform.rotation = Quaternion.Euler(0f, 0f, _startDegrees + _arcDegrees * t);
        }

        public void OnSpawned() => _swinging = false;
        public void OnDespawned() => _swinging = false;
    }
}
