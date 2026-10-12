using System;
using System.Collections.Generic;

namespace _Codebase_.Entity._Abstract_
{

    public interface IEntity
    {
        public List<Stat> Stats { get; set; }
    }

    [Serializable]
    public enum StatEnum
    {
        MoveSpeed,
        Health,
        Damage,
        AttackSpeed,
        BonusSpeed,
        Armor,
    }

    [Serializable]
    public class Stat
    {
        public StatEnum statEnum;
        public float    value;
    }


}