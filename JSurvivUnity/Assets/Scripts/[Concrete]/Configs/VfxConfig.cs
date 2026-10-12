using UnityEngine;

namespace _Concrete_.Configs
{
    [CreateAssetMenu(fileName = "VfxConfig", menuName = "ScriptableObjects/VfxConfig", order = 2)]    public class VfxConfig : ScriptableObject
    {
        public ParticleSystem bleedVfx;
        public ParticleSystem poisonVfx;
        public ParticleSystem burnVfx;
        public ParticleSystem blindVfx;
        public ParticleSystem waterVfx;
        public ParticleSystem immunityVfx;
        public ParticleSystem stunnedVfx;
        public ParticleSystem reverseVfx;
        public ParticleSystem immortalVfx;
        public ParticleSystem buffDamageVfx;
        public ParticleSystem cantUseWeaponVfx;
        
        #region Singleton
        public static VfxConfig I { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            I = Resources.Load<VfxConfig>("VfxConfig");

            if (!I)
            {
                Debug.LogError("VfxConfig not found in Resources folder.");
            }
        }

        #endregion
    }
}