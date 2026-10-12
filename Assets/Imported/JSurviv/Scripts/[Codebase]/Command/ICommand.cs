using _Codebase_.Configs;
using _Codebase_.Entity.Player;
using UnityEngine;

namespace _Codebase_.Command
{
    public interface ICommand
    {
        void Execute();
    }


    public abstract class PlayerCommand : ICommand
    {
        protected PlayerEntity _player;


        public PlayerCommand(PlayerEntity player)
        {
            this._player = player;
        }


        public abstract void Execute();
    }


    public class MoveCommand : PlayerCommand
    {
        private readonly Vector2 _direction;


        public MoveCommand(PlayerEntity player, char dir) : base(player)
        {
            this._direction = dir switch
            {
                'u' => new(0, 1),
                'r' => new(1, 0),
                'd' => new(0, -1),
                'l' => new(-1, 0),
                _ => Vector2.zero,
            };
        }


        public override void Execute()
        {
            if (this._direction == Vector2.zero) return;

            var newPosition = this._player.Position + this._direction;

            if (this._player.CanMove(newPosition))
            {
                this._player.Move(this._direction);
            }
            else
            {
                Debug.Log($"[Movement] Cannot move to {newPosition}. Path is blocked.");
                this._player.Look(this._direction, 0.15f);
                this._player.CantMoveAction();
            }
        }
    }


    public class MeleeCommand : PlayerCommand
    {
        private readonly Vector2 _direction;

        public MeleeCommand(PlayerEntity player, char direction) : base(player)
        {
            this._direction = direction switch
            {
                'u' => new(0, 1),
                'r' => new(1, 0),
                'd' => new(0, -1),
                'l' => new(-1, 0),
                _ => this._direction,
            };
        }

        public override void Execute()
        {
            this._player.Attack(this._direction);
        }
    }

    public class ShootCommand : PlayerCommand
    {
        private readonly Vector2 _direction;

        public ShootCommand(PlayerEntity player, char direction) : base(player)
        {
            this._direction = direction switch
            {
                'u' => new(0, 1),
                'r' => new(1, 0),
                'd' => new(0, -1),
                'l' => new(-1, 0),
                _ => this._direction,
            };
        }

        public override void Execute()
        {
            this._player.Shoot(this._direction);
        }
    }

    public class ThrowCommand : PlayerCommand
    {
        private readonly Vector2 _direction;

        public ThrowCommand(PlayerEntity player, char direction) : base(player)
        {
            this._direction = direction switch
            {
                'u' => new(0, 1),
                'r' => new(1, 0),
                'd' => new(0, -1),
                'l' => new(-1, 0),
                _ => this._direction,
            };
        }

        public override void Execute()
        {
            this._player.Throw(this._direction);
        }
    }

    public class SpecialCommand : PlayerCommand
    {
        private readonly Vector2 _direction;

        public SpecialCommand(PlayerEntity player, char direction) : base(player)
        {
            this._direction = direction switch
            {
                'u' => new(0, 1),
                'r' => new(1, 0),
                'd' => new(0, -1),
                'l' => new(-1, 0),
                _ => this._direction, 
            };
        }

        public override void Execute()
        {
            this._player.UseSpecial(this._direction);
        }
    }
    public class PickUpCommand : PlayerCommand
    {
        public PickUpCommand(PlayerEntity player) : base(player)
        {
        }


        public override void Execute()
        {
            this._player.OnPickUp();
        }
    }


    public class UseItemCommand : PlayerCommand
    {
        private readonly string _id;


        public UseItemCommand(PlayerEntity player, string id) : base(player)
        {
            this._id = id;
        }


        public override void Execute()
        {
            this._player.UseItem(this._id);
        }
    }

    public class RevokeCommand : PlayerCommand
    {
        private readonly string _id;


        public RevokeCommand(PlayerEntity player, string id) : base(player)
        {
            this._id = id;
        }


        public override void Execute()
        {
            this._player.RevokeItem(this._id);
        }
    }
}