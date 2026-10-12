namespace _Codebase_.EffectSystem
{
    public abstract class TimeBasedEffect : IEffect
    {
        public int Time { get; set; }

        void IEffect.Apply(IAffectable target)
        {
            if (this.Time > 0 && this.Apply(target))
            {
                this.Time--;
            }

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

        public abstract bool Apply(IAffectable target);

        public virtual bool ShouldStacking(IAffectable target)
        {
            return true;
        }
    }
}