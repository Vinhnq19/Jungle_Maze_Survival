using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Player;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Utils;

namespace _Concrete_.Common
{
    public class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField]
        private Slider hpBar1;

        [SerializeField]
        private Slider hpBar2;

        [SerializeField]
        private PlayerEntity player;

        private float currentPercent;
        private Tween tween;

        private void Update()
        {
            var playerPercent = this.player.Stats.GetStatValue(StatEnum.Health) / 100.0f;

            if (this.currentPercent != playerPercent)
            {
                this.SetPercent(playerPercent);
            }
        }

        private void SetPercent(float percent)
        {
            this.tween?.Complete();

            if (percent < this.currentPercent)
            {
                this.hpBar1.value = percent;

                this.tween = DOTween
                    .To(
                        () => this.hpBar2.value,
                        value => this.hpBar2.value = value,
                        percent,
                        1.0f
                    )
                    .SetEase(Ease.OutSine)
                    .OnComplete(() =>
                    {
                        this.hpBar2.value = percent;
                        this.tween = null;
                    });
            }
            else
            {
                this.hpBar2.value = percent;

                this.tween = DOTween
                    .To(
                        () => this.hpBar1.value,
                        value => this.hpBar1.value = value,
                        percent,
                        1.0f
                    )
                    .SetEase(Ease.OutSine)
                    .OnComplete(() =>
                    {
                        this.hpBar1.value = percent;
                        this.tween = null;
                    });
            }

            this.currentPercent = percent;
        }
    }
}