using System;
using System.Threading;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using Cysharp.Threading.Tasks;
using Managers;
using UnityEngine;
using Utils;

namespace _Codebase_.Entity.Item
{
    public abstract class BaseItemEntity : MonoBehaviour, IHasRarity
    {
        [field: Header("Visuals")]

        #region Visuals

        [field: SerializeField]
        public SpriteRenderer SpriteRenderer { get; set; }

        #endregion

        private CancellationTokenSource cts;

        public Vector2 Position => this.transform.position - Vector3.one * 0.5f;

        public bool IsOwned { get; private set; }

        public bool IsUsing => this.cts is
        {
            IsCancellationRequested: false,
        };

        public PlayerEntity MountedPlayer { get; private set; }

        private void OnEnable()
        {
            ItemManager.I.Items.Add(this);
        }

        private void OnDisable()
        {
            this.TryCancel();
            ItemManager.I.Items.Remove(this);
        }

        public void TryUse()
        {
            if (!this.IsOwned) return;
            if (this.IsUsing) return;

            this.TryCancel();
            this.cts = new();

            this.OnBeforeUsed();

            UniTask
                .Delay(TimeSpan.FromSeconds(this.UsageTime * this.GetStepTime()), cancellationToken: this.cts.Token)
                .SuppressCancellationThrow()
                .ContinueWith(failed =>
                {
                    this.TryCancel();
                    this.OnAfterUsed(!failed);
                    this.MountedPlayer.OnUseItemEnded(!failed, this);
                })
                .Forget();
        }

        public virtual void Mount(Transform mountedTf)
        {
            if (this.IsOwned) return;

            this.MountedPlayer = mountedTf.GetComponentInParent<PlayerEntity>();
            if (!this.MountedPlayer) return;

            this.IsOwned = true;

            this.SpriteRenderer.enabled = false;
            this.transform.SetParent(mountedTf);
        }

        public void TryCancel()
        {
            if (!this.IsOwned) return;
            if (!this.IsUsing) return;

            this.cts?.Cancel();
            this.cts?.Dispose();
            this.cts = null;
        }

        public void Drop()
        {
            if (!this.IsOwned) return;
            if (this.IsUsing) return;
            Destroy(this.gameObject);
        }

        protected abstract void OnBeforeUsed();
        protected abstract void OnAfterUsed(bool success);

        [field: Header("Config")]

        #region Config

        [field: SerializeField]
        public string Id { get; private set; }
        
        [field: SerializeField]
        public string Type { get; private set; }

        [field: SerializeField]
        public float UsageTime { get; private set; }

        [field: SerializeField]
        public float Rarity { get; private set; }

        [field: SerializeField]
        public int UsageScore { get; private set; }

        #endregion

    }
}