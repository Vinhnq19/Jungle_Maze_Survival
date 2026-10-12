using UnityEngine;

namespace _Codebase_.Configs
{
    [CreateAssetMenu(fileName = "SoundConfig", menuName = "ScriptableObjects/SoundConfig")]
    public class SoundConfig : ScriptableObject
    {
        [Header("Overall")]
        public AudioClip clickSound;

        public AudioClip waitingLobbyMusic;
        public AudioClip loadingSound;
        public AudioClip timeOutSound;
        public AudioClip rankingSound;

        [Header("Map")]
        public AudioClip parkMapMusic;

        public AudioClip houseMapMusic;
        public AudioClip terraceMapMusic;
    }
}