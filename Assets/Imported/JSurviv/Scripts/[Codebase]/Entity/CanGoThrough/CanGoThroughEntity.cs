using _Codebase_.Entity.Player;
using Managers;
using UnityEngine;

namespace _Codebase_.Entity.CanGoThrough
{
    public abstract class CanGoThroughEntity : MonoBehaviour
    {

        protected virtual void Start()
        {
            // MapManager.I.CanGoThroughs.Add(this); // Bạn cần tạo danh sách này trong MapManager
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("Player") && other.GetComponentInParent<PlayerEntity>() is {} player)
            {
                this.OnPlayerEnter(player);
            }
        }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("Player") && other.GetComponentInParent<PlayerEntity>() is {} player)
            {
                this.OnPlayerExit(player);
            }
        }
        
        private void OnTriggerStay2D(Collider2D other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("Player") && other.GetComponentInParent<PlayerEntity>() is {} player)
            {
                this.OnPlayerStay(player);
            }
        }

        /// <summary>
        ///     Gọi khi có entity khác đi xuyên qua.
        /// </summary>
        public abstract void OnPlayerEnter(PlayerEntity player);

        public abstract void OnPlayerStay(PlayerEntity player);

        public abstract void OnPlayerExit(PlayerEntity player);
    }
}