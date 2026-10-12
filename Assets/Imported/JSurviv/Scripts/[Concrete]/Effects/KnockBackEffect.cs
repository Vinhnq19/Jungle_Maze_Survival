using System.Collections.Generic;
using System.Linq;
using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using Managers;
using UnityEngine;

namespace _Concrete_.Effects
{
    [EffectInfo("KNOCK_BACK", false)]
    public class KnockBackEffect : TimeBasedEffect
    {
        public Vector2 Direction { get; set; } = Vector2.up;

        public int KnockbackPower { get; set; } = 3;

        public float StunOnBlockedDuration { get; set; } = 2f;

        public override bool Apply(IAffectable target)
        {
            if (this.Direction == Vector2.zero) return false;

            switch (target)
            {
                case PlayerEntity player:
                    this.ApplyOnPlayer(player);
                    return true;
            }

            return false;
        }

        private void ApplyOnPlayer(PlayerEntity player)
        {
            player.MovementQueue.Clear();
            player.PlayerAction = null;

            if (this.IsBlockedOnKnockback(player.Position, player, out var lastValidPosition))
            {
                player.AddEffect(new StunEffect
                {
                    Time = this.StunOnBlockedDuration,
                });
            }

            player.Move(lastValidPosition - player.Position, false);
        }

        private bool HasObstacleAtPosition(Vector2 position, PlayerEntity currentPlayer)
        {
            var obstaclePositions = new List<Vector2>()
                .Concat(MapManager.I.IndestructiblePositions)
                .Concat(MapManager.I.Destructibles.SelectMany(d => d.Positions))
                .Concat(MapManager.I.Enemies.Select(e => e.Position))
                .Concat(PlayerManager.I.Players.Where(p => p != currentPlayer && !p.IsDead).Select(p => p.Position))
                .Select(p => new Vector2(Mathf.Floor(p.x) + 0.5f, Mathf.Floor(p.y) + 0.5f))
                .ToHashSet();

            var normalizedPosition = new Vector2(Mathf.Floor(position.x) + 0.5f, Mathf.Floor(position.y) + 0.5f);

            return obstaclePositions.Contains(normalizedPosition);
        }

        private bool IsBlockedOnKnockback(Vector2 origin, PlayerEntity currentPlayer, out Vector2 lastValidPosition)
        {
            var normalizedDirection = this.Direction.normalized;

            for (var i = 0; i < this.KnockbackPower; i++)
            {
                // Nếu vị trí tiếp theo có vật cản thì trả về vị trí trước đó
                if (this.HasObstacleAtPosition(origin + normalizedDirection * (i + 1), currentPlayer))
                {
                    lastValidPosition = origin + normalizedDirection * i;
                    return true;
                }
            }

            // Nếu không bị cản thì trả về vị trí cuối cùng
            lastValidPosition = origin + normalizedDirection * this.KnockbackPower;
            return false;
        }
    }
}