using System.Collections.Generic;
using UnityEngine;

namespace _Codebase_.Configs
{
    [CreateAssetMenu(fileName = "PlayerImageConfig", menuName = "ScriptableObjects/Images/PlayerImageConfig")]
    public class PlayerImageConfig : ScriptableObject
    {
        public List<Sprite> playerImages;
        public List<Sprite> playerHands;
    }
}