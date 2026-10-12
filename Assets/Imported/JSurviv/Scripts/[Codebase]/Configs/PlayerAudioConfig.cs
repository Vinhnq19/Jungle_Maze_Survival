using UnityEngine;

namespace _Codebase_.Configs
{
    [CreateAssetMenu(fileName = "PlayerAudioConfig", menuName = "ScriptableObjects/PlayerAudioConfig")]
    public class PlayerAudioConfig : ScriptableObject
    {
        [Header("Attack")]
        public AudioClip meleeAttackSound;

        public AudioClip rangedAttackSound;
        public AudioClip throwablesAttackSound;
        public AudioClip exploitSound;

        [Header("Healing")]
        public AudioClip useItemSound;

        [Header("Interact")]
        public AudioClip pickupGunSound;

        public AudioClip pickupMeleeSound;
        public AudioClip pickupThrowableSound;
        public AudioClip putOnArmorSound;
        public AudioClip putOnHelmetSound;
        public AudioClip pickupItemSound;

        [Header("Vunerable")]
        public AudioClip bleedSound;

        public AudioClip stunSound;

        [Header("Health")]
        public AudioClip reviveSound;

        public AudioClip deathSound;
    }
}