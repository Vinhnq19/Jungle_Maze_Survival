using System;
using _Codebase_.Utils;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Codebase_.ObjectPool
{
    public class VfxInfo : IDisposable
    {

        public VfxInfo(ParticleSystem vfx)
        {
            this.Vfx = vfx;
        }

        public ParticleSystem Vfx { get; }

        public void Dispose()
        {
            ObjectPoolManager.I.Free(this.Vfx.gameObject);
        }
    }

    public static class VfxPoolExtensions
    {
        public static VfxInfo SpawnVfx(this ParticleSystem prefab, PartialTransform partialTransform)
        {
            var vfx = ObjectPoolManager.I
                .GetOrAddPool(prefab.gameObject)
                .Take<ParticleSystem>();

            if (!vfx)
            {
                return null;
            }

            partialTransform.ApplyToTransform(vfx.transform);

            var vfxInfo = new VfxInfo(vfx);

            UniTask
                .DelayFrame(1)
                .ContinueWith(() =>
                {
                    UniTask.WaitUntil(() => !vfx || !vfx.isPlaying)
                        .ContinueWith(() =>
                        {
                            vfxInfo.Dispose();
                        })
                        .Forget();
                }).Forget();


            return vfxInfo;
        }
    }
}