using UnityEngine;

namespace _Codebase_.Configs
{
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "ScriptableObjects/EnemyConfig")]
    public class EnemyConfig : ScriptableObject
    {
        public EnemyId EnemyId;
        public float    damage;
        
        [Header("Cooldown Settings")]
        public int cooldownStep; 
    }

    public enum EnemyId
    {
        NATIVE,
        GHOST,
        LEOPARD,
        ANACONDA,
        RHINO,
        SPIRIT,
        GOLEM,
    }
}