using JungleMaze.App.Ports;
using JungleMaze.Core.Pooling;
using JungleMaze.Domain.Player;
using UnityEngine;

namespace JungleMaze.Presentation.Player
{
    /// <summary>
    /// Hiển thị một người chơi (không đọc packet). Vị trí lấy từ <see cref="IPlayerPoseSource"/>: prediction cho nhân vật
    /// của mình (MOV-02), interpolation cho người khác (MOV-03). Chưa có pose thì đặt thẳng theo snapshot mới nhất.
    /// Được lấy từ pool nên reset trạng thái trong OnSpawned/OnDespawned.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private float followSharpness = 15f;

        private PlayerModel _model;
        private IPlayerPoseSource _poses;
        private bool _snapNext;

        public byte NetId => _model?.NetId ?? 0;

        public void Bind(PlayerModel model, IPlayerPoseSource poses = null)
        {
            _model = model;
            _poses = poses;
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

        public void OnDespawned()
        {
            _model = null;
            _poses = null;
        }

        // LateUpdate: prediction/interpolation đã được tick trong Update của frame này.
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (_model == null) return;
            if (_poses != null && _poses.TryGetPose(_model.NetId, out var pose))
            {
                // Pose đã mượt sẵn: đặt thẳng, không làm mượt thêm (sẽ cộng độ trễ).
                transform.SetPositionAndRotation(new Vector3(pose.X, pose.Y, 0f), Quaternion.Euler(0f, 0f, pose.RotationDegrees));
                if (body != null) body.enabled = !pose.IsDead;
                _snapNext = false;
                return;
            }
            if (!_model.HasState) return;
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
