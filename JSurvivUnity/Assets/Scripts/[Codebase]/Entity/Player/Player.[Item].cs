using System.Linq;
using _Codebase_.Entity.Item;
using System.Collections.Generic;
using Managers;
using Managers.DataObjects;

namespace _Codebase_.Entity.Player
{
    public partial class PlayerEntity
    {

        public void DropItems()
        {
            if (!this.isAlive) return;

            this.items.ForEach(item => item.Drop());
            this.items.Clear();
        }

        public string PickUpItem(BaseItemEntity item)
        {
            if (this.items.Count >= LIMIT_ITEM_COUNT) return null;
            this.items.Add(item);
            item.Mount(this.Visual);

            return new[]
            {
                InventorySlotEnum.ITEM1, InventorySlotEnum.ITEM2, InventorySlotEnum.ITEM3, InventorySlotEnum.ITEM4,
            }[this.items.Count-1];
        }
        
        public List<BaseItemEntity> GetItems() => this.items;
        public void UseItem(string itemID)
        {
            if (!this.isAlive) return;

            var item = this.items.FirstOrDefault(item => itemID == item.Id);

            if (item) this.UseItem(item);
        }

        public void UseItem(BaseItemEntity item)
        {
            if (this.items.Contains(item) && item && !this.usingItem)
            {
                item.TryUse();
                this.RunUseItemAnimation(true);
                this.usingItem = item;
            }
        }

        public void CancelUseItem()
        {
            if (this.usingItem)
            {
                this.usingItem.TryCancel();
                this.RunUseItemAnimation(false);
                this.usingItem = null;
            }
        }

        public void OnUseItemEnded(bool success, BaseItemEntity item)
        {
            if (!this.isAlive) return;
            this.RunUseItemAnimation(false);
            this.usingItem = null;

            if (!success)
            {
                return;
            }
            
            NetworkManager.I.Send(new GamePlayerInventoryClear.Request
            {
                playerId = this.Id,
                id = item.Id,
                type = item.Type,
            });
            
            this.items.Remove(item);
            this.Score += item.UsageScore;
        }

        public void RevokeItem(string itemId)
        {
            if (!this.isAlive) return;

            if (this.items.Count > 0)
            {
                for (var i = 0; i < this.items.Count; i++)
                {
                    if (this.items[i].Id == itemId)
                    {
                        NetworkManager.I.Send(new GamePlayerInventoryClear.Request
                        {
                            playerId = this.Id,
                            id = items[i].Id,
                            type = items[i].Type,
                        });

                        this.items.RemoveAt(i);
                        return;
                    }
                }
            }

            if (this.weapons.Count > 0)
            {
                for (var i = 0; i < this.weapons.Count; i++)
                {
                    if (this.weapons[i].Id == itemId)
                    {
                        NetworkManager.I.Send(new GamePlayerInventoryClear.Request
                        {
                            playerId = this.Id,
                            id = itemId,
                            type = this.weapons[i].GetWeaponTypeAsString(),
                            
                        });

                        this.weapons.RemoveAt(i);

                        if (this.currentWeapon && this.currentWeapon.Id == itemId)
                        {
                            this.currentWeapon.Unmount();
                            this.weapons.Remove(this.currentWeapon);
                            this.UseWeapon(null);
                        }

                        return;
                    }
                }
            }

            if (this.armors.Count > 0)
            {
                for (var i = 0; i < this.armors.Count; i++)
                {
                    if (this.armors[i].Id == itemId)
                    {
                        NetworkManager.I.Send(new GamePlayerInventoryClear.Request
                        {
                            playerId = this.Id,
                            id =armors[i].Id,
                            type = armors[i].GetArmorTypeAsString(),
                        });

                        this.armors.RemoveAt(i);
                        return;
                    }
                }
            }
        }
    }
}