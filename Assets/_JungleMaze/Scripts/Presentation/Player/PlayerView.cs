using JungleMaze.Core.Pooling;
using JungleMaze.Domain.Player;
using UnityEngine;

namespace JungleMaze.Presentation.Player
{
    /// <summary>
    /// Hiển thị một người chơi từ PlayerModel (không đọc packet). Nội suy về vị trí server để mượt giữa các tick 30Hz.
    /// Được lấy từ pool nên reset trạng thái trong OnSpawned/OnDespawned.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private float followSharpness = 15f;

        private PlayerModel _model;
        private bool _snapNext;

        public byte NetId => _model?.NetId ?? 0;

        public void Bind(PlayerModel model)
        {
            _model = model;
            _snapNext = true;
            if (body != null)
            {
                body.color = Color.HSVToRGB(model.NetId * 0.1f % 1f, 0.6f, 1f);
                // Built-in UI sprites have a very small native size. Match the server's 0.8-cell diameter.
                if (body.sprite != null)
                {
                    var size = body.sprite.bounds.size;
                    if (size.x > 0 && size.y > 0) body.transform.localScale = new Vector3(.8f / size.x, .8f / size.y, 1);
                }
            }
            Refresh();
        }

        public void OnSpawned() => _snapNext = true;

        public void OnDespawned() => _model = null;

        private void Update() => Refresh();

        private void Refresh()
        {
            if (_model == null || !_model.HasState) return;
            var state = _model.Current;
            var target = new Vector3(state.X, state.Y, 0f);
            var t = _snapNext ? 1f : 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target, t);
            transform.rotation = Quaternion.Euler(0f, 0f, state.RotationDegrees);
            if (body != null) body.enabled = !state.IsDead;
            _snapNext = false;
        }
    }
}
