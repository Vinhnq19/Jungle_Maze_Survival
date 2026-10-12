using UnityEngine;

[CreateAssetMenu(fileName = "MapConfig", menuName = "ScriptableObjects/MapConfig", order = 1)]
public class MapConfig : ScriptableObject
{
    public int mapSize;

    [Header("Map General Info")]
    public float TotalMapTime = 300f;
    public int InitialWidth = 70;

    [Header("Border Shrink Behavior")]
    public int NumberOfShrinks = 3;
    public float TimeToStartBorder = 30f;
    public float ShrinkDelay = 50f;
    public float ShrinkDuration = 16f;
    public int ShrinkSize = 32;

    [Header("Legacy Fields (Optional)")]
    public float baseReviveTime;
    public float limitTimeToRevive;
    
    [Header("Weapon Spawn Config")]
    public float WeaponsPerBotPerMinute = 1.5f;
    
    public int NumberOfWeaponSpawnWaves = 4;
    
    public int InitialWeaponSpawnCount = 20;
}