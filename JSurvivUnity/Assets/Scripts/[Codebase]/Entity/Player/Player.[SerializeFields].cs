using System.Collections.Generic;
using _Codebase_.Configs;
using _Codebase_.Entity._Abstract_;
using _Codebase_.Entity.Armor;
using _Codebase_.Entity.Item;
using _Codebase_.Entity.Weapon;
using TMPro;
using UnityEngine;

namespace _Codebase_.Entity.Player
{
    public partial class PlayerEntity
    {
        //hvu
        [SerializeField]
        private PlayerAnimation playerAnimation;
        //

        [SerializeField]
        private float handDamage;

        [SerializeField]
        private Vector2 handRange = new Vector2(1f, 1f);

        [SerializeField]
        private Animator animator;

        [SerializeField]
        public Transform holdPoint;

        [SerializeField]
        public Transform shootTf;

        [Header("Inventory")]
        [SerializeField]
        public List<BaseWeaponEntity> weapons;

        [SerializeField]
        public List<BaseItemEntity> items;

        [SerializeField]
        public List<ArmorEntity> armors;

        [Header("Canvas")]
        [SerializeField]
        private Rigidbody2D rb;

        [SerializeField]
        private TMP_Text nameTextMeshPro;

        [Space(10)]
        [Header("Visual")]
        [SerializeField]
        private Transform body;

        [SerializeField]
        public Transform lHand;

        [SerializeField]
        public Transform rHand;

        [SerializeField]
        private ParticleSystem useItemPartical;

        [Space(10)]
        [Header("Debug")]
        public bool debugging;

        [field: SerializeField]
        public PlayerConfig playerConfig;

        [field: SerializeField]
        public SpriteRenderer[] renderers;

        [SerializeField]
        private KeyCode[] inputs;

        [SerializeField]
        public UnityEngine.Camera followCamera;

        public Direction playerDirection;

        [field: SerializeField]
        public List<Stat> Stats { get; set; }
    }
}