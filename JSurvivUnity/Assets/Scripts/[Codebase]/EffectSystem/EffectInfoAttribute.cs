
namespace _Codebase_.EffectSystem
{
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    sealed class EffectInfoAttribute : System.Attribute
    {
        public string Id { get; set; }
        public bool IsBuff { get; }

        public EffectInfoAttribute(string id, bool isBuff)
        {
            this.Id = id;
            this.IsBuff = isBuff;
        }
    }
}