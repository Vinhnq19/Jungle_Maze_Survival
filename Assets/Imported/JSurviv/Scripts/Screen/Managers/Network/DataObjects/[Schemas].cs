namespace Managers.DataObjects
{
    public class ObstacleSchema
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public dynamic Attributes { get; set; }
    }
    
    public class IndestructiblePositionSchema
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Id { get; set; }
    }

    public class BuildingSchema
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Id { get; set; }
        public dynamic Attributes { get; set; }
    }

    public class EntitySchema
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public dynamic Attributes { get; set; }
    }

    public class PlayerSchema
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public dynamic Attributes { get; set; }
        public float Health { get; set; }
        public float Score { get; set; }
        public string[] CanBeSeenBy { get; set; }
    }

    public class ItemSchema
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public int Level { get; set; }
        public float Durability { get; set; }
        public dynamic Attributes { get; set; }
    }

    public class EffectSchema
    {
        public string Id { get; set; }
        public int Level { get; set; }
        public float Duration { get; set; }
    }

    public class DirectionEnum
    {
        public const string UP    = "u";
        public const string DOWN  = "d";
        public const string LEFT  = "l";
        public const string RIGHT = "r";
    }

    public class InventorySlotEnum
    {
        public const string MELEE   = "MELEE";
        public const string RANGED  = "GUN";
        public const string THROWN  = "THROWABLE";
        public const string SPECIAL = "SPECIAL";
        public const string HELMET  = "HELMET";
        public const string ARMOR   = "ARMOR";

        public const string ITEM1   = "item1";
        public const string ITEM2   = "item2";
        public const string ITEM3   = "item3";
        public const string ITEM4   = "item4";
    }
}