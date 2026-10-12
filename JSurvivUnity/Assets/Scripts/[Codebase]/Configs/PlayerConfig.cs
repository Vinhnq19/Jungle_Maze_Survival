using UnityEngine;

namespace _Codebase_.Configs
{
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "ScriptableObjects/PlayerConfig", order = 2)]
    public class PlayerConfig : ScriptableObject
    {
        public float   health;
        public Vector2 viewSight;
        public float   baseDamage;
        public float   obstacleDamage;
        public float   attackCooldown;
        public float   attackRange;
    }
}