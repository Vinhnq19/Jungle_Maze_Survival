using System.Linq;
using _Codebase_.EffectSystem;
using _Codebase_.Entity.Player;
using Managers;
using UnityEngine;

namespace _Concrete_.Effects
{
    [EffectInfo("PULL", false)]
    public class PullEffect : TimeBasedEffect
    {
        public Vector2 Direction { get; set; } = Vector2.up;
        public int Range { get; set; } = 6;
        public float StunDuration { get; set; } = 1f; // 1 second stun

        private Vector2 NormalizeToCellCenter(Vector2 position)
        {
            return new(Mathf.Floor(position.x), Mathf.Floor(position.y));
        }

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

        private void ApplyOnPlayer(PlayerEntity currentPlayer)
        {
            currentPlayer.MovementQueue.Clear();
            currentPlayer.PlayerAction = null;

            var normalizedDirection = this.Direction.normalized;

            for (var i = 1; i <= this.Range; i++)
            {
                var currentWorldCheckPosition = currentPlayer.Position + normalizedDirection * i;
                var checkCellCenter = this.NormalizeToCellCenter(currentWorldCheckPosition);

                var otherPlayerFound = PlayerManager.I.Players.FirstOrDefault(p =>
                    p != currentPlayer &&
                    !p.IsDead && this.NormalizeToCellCenter(p.Position) == checkCellCenter);

                if (otherPlayerFound != null)
                {
                    var targetPullPositionForOtherPlayer = currentPlayer.Position + normalizedDirection; // Vẫn là vị trí trước mặt currentPlayer
                    var deltaMoveForOtherPlayer = targetPullPositionForOtherPlayer - otherPlayerFound.Position;

                    otherPlayerFound.Move(deltaMoveForOtherPlayer, false);

                    otherPlayerFound.AddEffect(new StunEffect
                    {
                        Time = this.StunDuration,
                    });

                    return;
                }

                var obstacleFound = MapManager.I.Destructibles.FirstOrDefault(d =>
                    d.Positions.Any(pos => this.NormalizeToCellCenter(pos) == checkCellCenter));

                if (obstacleFound != null)
                {
                    var actualObstacleCellCenter = obstacleFound.Positions
                        .Select(p => this.NormalizeToCellCenter(p))
                        .OrderBy(p => Vector2.Distance(p, checkCellCenter))
                        .First();

                    var targetPullPositionForCurrentPlayer = actualObstacleCellCenter - normalizedDirection;
                    var deltaMoveForCurrentPlayer = targetPullPositionForCurrentPlayer - currentPlayer.Position;

                    if (currentPlayer.CanMove(deltaMoveForCurrentPlayer))
                    {
                        currentPlayer.Move(deltaMoveForCurrentPlayer, false);
                    }

                    return;
                }
                
                if (MapManager.I.IndestructiblePositions.Any(indPos => indPos == checkCellCenter))
                {
                    var targetPullPositionForCurrentPlayer = checkCellCenter - normalizedDirection;
                    var deltaMoveForCurrentPlayer = targetPullPositionForCurrentPlayer - currentPlayer.Position;

                    if (currentPlayer.CanMove(deltaMoveForCurrentPlayer))
                    {
                        currentPlayer.Move(deltaMoveForCurrentPlayer, false);
                    }

                    return;
                }

                var npcFound = MapManager.I.Enemies.FirstOrDefault(e => this.NormalizeToCellCenter(e.Position) == checkCellCenter);

                if (npcFound != null)
                {
                    Debug.Log($"PullEffect.ApplyOnPlayer: Found NPC (EnemyEntity) {npcFound.name} at cell {checkCellCenter}");
                    var targetPullPositionForCurrentPlayer = this.NormalizeToCellCenter(npcFound.Position) - normalizedDirection;
                    var deltaMoveForCurrentPlayer = targetPullPositionForCurrentPlayer - currentPlayer.Position;

                    if (currentPlayer.CanMove(deltaMoveForCurrentPlayer))
                    {
                        currentPlayer.Move(deltaMoveForCurrentPlayer, false);
                    }

                    return;
                }

                var cellCornerToCheck = new Vector2(Mathf.Floor(checkCellCenter.x), Mathf.Floor(checkCellCenter.y));

                if (MapManager.I.IndestructiblePositions.Any(indPos => indPos == cellCornerToCheck))
                {
                    var targetPullPositionForCurrentPlayer = checkCellCenter - normalizedDirection;
                    var deltaMoveForCurrentPlayer = targetPullPositionForCurrentPlayer - currentPlayer.Position;

                    if (currentPlayer.CanMove(deltaMoveForCurrentPlayer))
                    {
                        currentPlayer.Move(deltaMoveForCurrentPlayer, false);
                    }

                    return;
                }
            }
        }
    }
}