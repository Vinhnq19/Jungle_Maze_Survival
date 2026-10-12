namespace _Codebase_.EffectSystem
{
    public abstract class ClockBasedEffect : IEffect
    {
        public float Time { get; set; }

        void IEffect.Apply(IAffectable target)
        {
            if (this.Time > 0) this.Apply(target);
            this.Time -= UnityEngine.Time.deltaTime;

            if (this.Time <= 0)
            {
                target.RemoveEffect(this);
            }
        }

        public virtual void OnAttached(IAffectable target)
        {
        }

        public virtual void OnDetached(IAffectable target)
        {
        }

        public abstract void Apply(IAffectable target);

        public virtual bool ShouldStacking(IAffectable target)
        {
            return true;
        }
    }
}