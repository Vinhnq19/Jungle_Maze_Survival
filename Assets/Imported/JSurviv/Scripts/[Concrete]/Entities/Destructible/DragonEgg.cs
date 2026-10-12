using System.Collections.Generic;
using _Codebase_.Entity.Armor;
using _Codebase_.Entity.Item;
using _Codebase_.Entity.Weapon;
using _Codebase_.Utils;
using _Concrete_.Entities.Destructible;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Linq;
using Managers;
using System.Collections;
using DG.Tweening;
using System;

public class DragonEgg : MonoBehaviour
{
    // public static DragonEgg I { get; private set; }

    [SerializeField]
    private float timeToSpawn;

    [SerializeField]
    private GameObject dragon;

    [SerializeField]
    public GameObject EggObject; 

    private void Awake()
    {
        // I = this;
        MapManager.I.RegisterDragonEgg(this);
    }

    private void Start()
    {
        this.EggObject.SetActive(false);

        this.OnSpawn();
    }

    private void OnSpawn()
    {
        StartCoroutine(WaitForSpawn());
    }

    private IEnumerator WaitForSpawn()
    {
        float timer = 0f;

        yield return new WaitUntil(() =>
        {
            timer += Time.deltaTime;
            return timer >= timeToSpawn;
        });

        DragonFly();
    }

    private void DragonFly()
    {
        float targetY = (float)MapManager.I.Map.mapConfig.mapSize + 25f;
        
        var chestComponent = this.EggObject.GetComponent<Chest>();
        if (chestComponent == null)
        {
            Debug.LogError("DragonEgg không có component Chest!", this.gameObject);
            return;
        }
    
        Vector2 pos = MapManager.I.GetSafeSpawnClusterPosition(chestComponent._tookPositions);

        this.dragon.transform.position = new Vector3(-25f, pos.y, 0f);

        float speed = 5f;

        dragon.transform.DOMoveX(targetY + 0.5f, (targetY + 0.5f) / speed).OnUpdate(() =>
        {
            if (Mathf.Abs(this.dragon.transform.position.x - pos.x) < 0.5f && !this.EggObject.activeSelf)
            {
                this.EggObject.transform.position = pos;
                this.EggObject.SetActive(true);

                if (chestComponent != null)
                {
                    chestComponent.Positions.Clear();
            
                    MapManager.I.Destructibles.Add(chestComponent);
                }
            }
        }).OnComplete(() =>
        {
            this.dragon.SetActive(false);
        });
    }
    private void OnDestroy()
    {
        if (MapManager.I != null)
        {
            MapManager.I.UnregisterDragonEgg(this);
        }
    }
}