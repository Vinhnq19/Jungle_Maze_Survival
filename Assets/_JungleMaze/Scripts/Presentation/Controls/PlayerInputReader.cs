using JungleMaze.App.Player;
using JungleMaze.Core.DI;
using JungleMaze.Domain.Player;
using UnityEngine;

namespace JungleMaze.Presentation.Controls
{
    /// <summary>
    /// Đọc bàn phím/chuột mỗi frame và đưa intent cho PlayerInputService (Application tự gửi 30Hz).
    /// WASD/mũi tên: di chuyển · chuột: hướng ngắm · chuột trái: tấn công · E/R/Space/Q: tương tác/nạp/dash/item.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private PlayerInputService _input;

        [Inject]
        public void Construct(PlayerInputService input) => _input = input;

        private void Update()
        {
            if (_input == null || !_input.Enabled) return;
            var move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (move.sqrMagnitude > 1f) move.Normalize();
            var aim = (Vector2)Input.mousePosition - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _input.SetIntent(new PlayerIntent
            {
                MoveX = move.x,
                MoveY = move.y,
                AimDegrees = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg,
                Attack = Input.GetMouseButton(0),
                Interact = Input.GetKey(KeyCode.E),
                Reload = Input.GetKey(KeyCode.R),
                Dash = Input.GetKey(KeyCode.Space),
                UseItem = Input.GetKey(KeyCode.Q)
            });
        }
    }
}
