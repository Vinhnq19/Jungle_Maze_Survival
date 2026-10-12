using System;
using System.Collections.Generic;
using System.Linq;
using _Codebase_.Command;
using _Codebase_.EffectSystem;
using _Codebase_.Entity.Weapon;
using _Concrete_.Effects;
using Managers;
using UnityEngine;

namespace _Codebase_.Entity.Player
{
    public partial class PlayerEntity
    {
        private readonly Dictionary<KeyCode, string> debugWeaponKeys = new()
        {
            [KeyCode.Alpha1] = "BANANA",
            [KeyCode.Alpha2] = "ROPE",
            [KeyCode.Alpha3] = "BELL",
            [KeyCode.Alpha4] = "SAHUR_BAT",
        };
        
        // --- Dictionaries for player actions ---
        private readonly Dictionary<KeyCode, Func<ICommand>> meleeActions;
        private readonly Dictionary<KeyCode, Func<ICommand>> shootActions;
        private readonly Dictionary<KeyCode, Func<ICommand>> throwActions;
        private readonly Dictionary<KeyCode, Func<ICommand>> specialActions;
        private readonly Dictionary<KeyCode, Func<ICommand>> miscActions;
        
        public PlayerEntity()
        {
            this.meleeActions = new()
            {
                [KeyCode.LeftArrow] = () => new MeleeCommand(this, 'l'),
                [KeyCode.UpArrow] = () => new MeleeCommand(this, 'u'),
                [KeyCode.DownArrow] = () => new MeleeCommand(this, 'd'),
                [KeyCode.RightArrow] = () => new MeleeCommand(this, 'r'),
            };
            this.shootActions = new()
            {
                [KeyCode.LeftArrow] = () => new ShootCommand(this, 'l'),
                [KeyCode.UpArrow] = () => new ShootCommand(this, 'u'),
                [KeyCode.DownArrow] = () => new ShootCommand(this, 'd'),
                [KeyCode.RightArrow] = () => new ShootCommand(this, 'r'),
            };
            this.throwActions = new()
            {
                [KeyCode.LeftArrow] = () => new ThrowCommand(this, 'l'),
                [KeyCode.UpArrow] = () => new ThrowCommand(this, 'u'),
                [KeyCode.DownArrow] = () => new ThrowCommand(this, 'd'),
                [KeyCode.RightArrow] = () => new ThrowCommand(this, 'r'),
            };
            this.specialActions = new()
            {
                [KeyCode.LeftArrow] = () => new SpecialCommand(this, 'l'),
                [KeyCode.UpArrow] = () => new SpecialCommand(this, 'u'),
                [KeyCode.DownArrow] = () => new SpecialCommand(this, 'd'),
                [KeyCode.RightArrow] = () => new SpecialCommand(this, 'r'),
            };
            this.miscActions = new()
            {
                [KeyCode.E] = () => new PickUpCommand(this),
                [KeyCode.Alpha1] = () => new UseItemCommand(this, "GOD_LEAF"),
                [KeyCode.Alpha2] = () => new UseItemCommand(this, "MERMAID_TAIL"),
                [KeyCode.Alpha3] = () => new UseItemCommand(this, "SPIRIT_TEAR"),
                [KeyCode.Alpha4] = () => new UseItemCommand(this, "PHOENIX_FEATHERS"),
                [KeyCode.Alpha5] = () => new UseItemCommand(this, "UNICORN_BLOOD"),
                [KeyCode.Alpha6] = () => new UseItemCommand(this, "MAGIC"),
                [KeyCode.Alpha7] = () => new UseItemCommand(this, "ELIXIR"),
                [KeyCode.Alpha8] = () => new UseItemCommand(this, "ELIXIR_OF_LIFE"),
                [KeyCode.Alpha9] = () => new UseItemCommand(this, "COMPASS"),
            };
        }
        
        private void DebugUpdate()
        {
            if (!this.debugging || this.IsDead) return;
        
            HandleCombatActions();
            HandleInventoryManagement();
            HandleEffectTesting();
            HandleMovementInput();
        }
        
        private void HandleCombatActions()
        {
            if (Input.GetKey(KeyCode.LeftShift)) ProcessActionMap(this.shootActions);
            else if (Input.GetKey(KeyCode.LeftControl)) ProcessActionMap(this.throwActions);
            else if (Input.GetKey(KeyCode.LeftAlt)) ProcessActionMap(this.specialActions);
            else ProcessActionMap(this.meleeActions);
        }
        
        private void HandleInventoryManagement()
        {
            // Lấy vũ khí nhanh (Shift + Số)
            foreach (var (keyCode, weaponId) in this.debugWeaponKeys)
            {
                if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(keyCode))
                {
                    DebugGetWeapon(weaponId);
                }
            }
        
            // Vứt đồ theo slot (F1-F10)
            if (Input.GetKeyDown(KeyCode.F1)) RevokeWeaponByType(WeaponType.Melee);
            if (Input.GetKeyDown(KeyCode.F2)) RevokeWeaponByType(WeaponType.Ranged);
            if (Input.GetKeyDown(KeyCode.F3)) RevokeWeaponByType(WeaponType.Throwable);
            if (Input.GetKeyDown(KeyCode.F4)) RevokeWeaponByType(WeaponType.Special);
            if (Input.GetKeyDown(KeyCode.F5) && this.items.Count > 0) this.RevokeItem(this.items[0].Id);
            if (Input.GetKeyDown(KeyCode.F6) && this.items.Count > 1) this.RevokeItem(this.items[1].Id);
            if (Input.GetKeyDown(KeyCode.F7) && this.items.Count > 2) this.RevokeItem(this.items[2].Id);
            if (Input.GetKeyDown(KeyCode.F8) && this.items.Count > 3) this.RevokeItem(this.items[3].Id);
            if (Input.GetKeyDown(KeyCode.F9) && this.armors.Count > 0) this.RevokeItem(this.armors[0].Id);
            if (Input.GetKeyDown(KeyCode.F10) && this.armors.Count > 1) this.RevokeItem(this.armors[1].Id);
            
            // Dùng vật phẩm và nhặt đồ (Không dùng phím bổ trợ)
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.LeftAlt))
            {
                 ProcessActionMap(this.miscActions);
            }
        }
        
        private void HandleEffectTesting()
        {
            // Hiệu ứng theo hướng (Q/P + Mũi tên)
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.P))
            {
                Vector2 direction = Vector2.zero;
                if (Input.GetKeyDown(KeyCode.UpArrow)) direction = Vector2.up;
                else if (Input.GetKeyDown(KeyCode.DownArrow)) direction = Vector2.down;
                else if (Input.GetKeyDown(KeyCode.LeftArrow)) direction = Vector2.left;
                else if (Input.GetKeyDown(KeyCode.RightArrow)) direction = Vector2.right;
        
                if (direction != Vector2.zero)
                {
                    if (Input.GetKey(KeyCode.Q)) this.AddEffect(new KnockBackEffect { Direction = direction, KnockbackPower = 3, StunOnBlockedDuration = 2f });
                    else if (Input.GetKey(KeyCode.P)) this.AddEffect(new PullEffect { Direction = direction, Range = 6, StunDuration = 1f });
                }
            }
        
            // Kích hoạt hiệu ứng cụ thể (Right Ctrl + Số)
            if (Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) this.AddEffect(new BleedEffect { Time = 5, Damage = 2 });
                if (Input.GetKeyDown(KeyCode.Alpha2)) this.AddEffect(new BlindEffect { Time = 5 });
                if (Input.GetKeyDown(KeyCode.Alpha3)) this.AddEffect(new CantUseWeaponEffect { Time = 5 });
                if (Input.GetKeyDown(KeyCode.Alpha4)) this.AddEffect(new ImmortalEffect { Time = 5 });
                if (Input.GetKeyDown(KeyCode.Alpha5)) this.AddEffect(new InvisibleEffect { Time = 10 });
                if (Input.GetKeyDown(KeyCode.Alpha6)) this.AddEffect(new PoisonsEffect { Time = 5, Damage = 2 });
                if (Input.GetKeyDown(KeyCode.Alpha7)) this.AddEffect(new ReviveEffect { Time = 1 });
                if (Input.GetKeyDown(KeyCode.Alpha8)) this.AddEffect(new StunEffect { Time = 3 });
                if (Input.GetKeyDown(KeyCode.Alpha9)) this.AddEffect(new BuffDamageEffect { Time = 10, DamageBonus = 0.5f });
            }
        }
        
        private void HandleMovementInput()
        {
            var direction = Vector2.zero;
            if (Input.GetKeyDown(this.inputs[0])) direction += Vector2.up;
            if (Input.GetKeyDown(this.inputs[2])) direction += Vector2.down;
            if (Input.GetKeyDown(this.inputs[3])) direction += Vector2.left;
            if (Input.GetKeyDown(this.inputs[1])) direction += Vector2.right;
        
            if (direction != Vector2.zero)
            {
                this.MovementQueue.Clear();
                var directionNormalized = direction.normalized;
                if (directionNormalized == Vector2.up) this.MovementQueue.Enqueue(new(this, 'u'));
                else if (directionNormalized == Vector2.down) this.MovementQueue.Enqueue(new(this, 'd'));
                else if (directionNormalized == Vector2.left) this.MovementQueue.Enqueue(new(this, 'l'));
                else if (directionNormalized == Vector2.right) this.MovementQueue.Enqueue(new(this, 'r'));
            }
        }
        
        private void ProcessActionMap(Dictionary<KeyCode, Func<ICommand>> actionMap)
        {
            foreach (var (code, commandFunc) in actionMap)
            {
                if (Input.GetKeyDown(code))
                {
                    this.PlayerAction = commandFunc();
                    return;
                }
            }
        }
        
        private void RevokeWeaponByType(WeaponType weaponType)
        {
            var weaponToDrop = this.weapons.FirstOrDefault(w => w.GetWeaponType() == weaponType);
            if (weaponToDrop != null)
            {
                this.RevokeItem(weaponToDrop.Id);
                Debug.Log($"[DEBUG] Đã vứt vũ khí loại: {weaponType}");
            }
        }
        
        private void DebugGetWeapon(string weaponId)
        {
            var pickupWeapon = WeaponManager.I.Weapons.FirstOrDefault(w => w.Id == weaponId && !w.IsMounted);
            if (!pickupWeapon)
            {
                Debug.LogWarning($"[DEBUG] Không tìm thấy vũ khí {weaponId}");
                return;
            }
            var existingWeapon = this.GetWeaponOfType(pickupWeapon.GetWeaponType());
            if (existingWeapon)
            {
                this.RevokeItem(existingWeapon.Id);
            }
            this.PickUpWeapon(pickupWeapon);
            Debug.Log($"[DEBUG] Đã nhận vũ khí {weaponId}");
        }
    }
}