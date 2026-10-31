using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Combat;
using JungleMaze.App.Ports;
using JungleMaze.App.Pooling;
using JungleMaze.App.Player;
using JungleMaze.App.World;
using JungleMaze.Core.DI;
using JungleMaze.Presentation.UI.Hud;
using JungleMaze.Presentation.Combat;
using JungleMaze.Presentation.World;
using JungleMaze.World;
using UnityEngine;

namespace JungleMaze.Presentation.World.Phase2
{
    public sealed partial class WorldDemoController
    {
        private IBalanceConfigProvider _balance;
        private IItemIconProvider _icons;
        private WorldDemoCombatSession _combat;
        [SerializeField] private SpriteRenderer heroVisual;
        [SerializeField] private CoverProbeProjectile projectilePrefab;
        [SerializeField] private MeleeSwingVisual swingPrefab;
        [SerializeField] private GameObject[] weaponPrefabs=new GameObject[CombatWeaponIds.Count];
        private IPoolService _pool;
        private PooledPrefabFactory<CoverProbeProjectile> _projectiles;
        private PooledPrefabFactory<MeleeSwingVisual> _swings;
        private readonly Sprite[] _weaponSprites=new Sprite[CombatWeaponIds.Count];
        private readonly System.Collections.Generic.Dictionary<ushort,BoxCollider2D> _demoCovers=new System.Collections.Generic.Dictionary<ushort,BoxCollider2D>();
        private SpriteRenderer _heldWeapon;
        private readonly float[] _pelletAngles=new float[16];
        private float _aimDegrees, _swingUntil;
        private uint _hitSequence;
        public WorldDemoCombatSession DemoCombat=>_combat;
        public DestructibleService Destructibles=>_destructibles;

        [Inject] public void ConfigureCombat(IBalanceConfigProvider balance,IItemIconProvider icons,IPoolService pool)
        {_balance=balance;_icons=icons;_pool=pool;}
        /// <summary>Editor composition copies visual references from Game, never its movement/network scripts.</summary>
        public void ConfigureVisualSources(SpriteRenderer body,CoverProbeProjectile projectile,MeleeSwingVisual swing,GameObject[] weapons)
        {heroVisual=body;projectilePrefab=projectile;swingPrefab=swing;weaponPrefabs=weapons;}
        private void CreateHeroVisual()
        {
            var hero=new GameObject("World Demo Hero");_hero=hero.transform;
            var body=new GameObject("Body");body.transform.SetParent(_hero,false);
            var renderer=body.AddComponent<SpriteRenderer>();
            renderer.sprite=heroVisual.sprite;renderer.sharedMaterials=heroVisual.sharedMaterials;
            renderer.color=heroVisual.color;renderer.flipX=heroVisual.flipX;renderer.flipY=heroVisual.flipY;
            renderer.sortingLayerID=heroVisual.sortingLayerID;renderer.sortingOrder=heroVisual.sortingOrder;
            body.transform.localPosition=heroVisual.transform.localPosition;
            body.transform.localRotation=heroVisual.transform.localRotation;
            body.transform.localScale=heroVisual.transform.localScale;
        }
        private void InitializeCombat()
        {
            _combat=new WorldDemoCombatSession(_balance,_hud,_items);_combat.Confirmed+=PreviewHit;
            _projectiles=new PooledPrefabFactory<CoverProbeProjectile>(_pool,projectilePrefab,16);
            _swings=new PooledPrefabFactory<MeleeSwingVisual>(_pool,swingPrefab,4);
            for(var i=0;i<weaponPrefabs.Length&&i<_weaponSprites.Length;i++)
                if(weaponPrefabs[i]!=null)_weaponSprites[i]=weaponPrefabs[i].GetComponentInChildren<SpriteRenderer>(true)?.sprite;
            var held=new GameObject("Demo held weapon");held.transform.SetParent(transform,false);
            _heldWeapon=held.AddComponent<SpriteRenderer>();_heldWeapon.sortingOrder=10;
        }
        private void UpdateCombat()
        {
            _combat.Tick(Time.deltaTime);
            var cursor=_camera.ScreenToWorldPoint(Input.mousePosition);
            _aimDegrees=Mathf.Atan2(cursor.y-_position.Y,cursor.x-_position.X)*Mathf.Rad2Deg;
            if(_hp>0&&Input.GetMouseButton(0))_combat.TryAttack(_aimDegrees);
            if(_hp>0&&Input.GetKeyDown(KeyCode.R))_combat.TryReload();
            var held=_items.HeldWeapon(0);_heldWeapon.sprite=held.IsEmpty?null:_weaponSprites[(int)_combat.Controller.Selected];
            _heldWeapon.enabled=Time.time>=_swingUntil;
            _hero.rotation=Quaternion.Euler(0,0,_aimDegrees);
            if(_heldWeapon.sprite!=null)
            {
                var size=_heldWeapon.sprite.bounds.size;_heldWeapon.transform.localScale=Vector3.one*(.8f/Mathf.Max(size.x,size.y));
                var angle=_aimDegrees;
                var direction=new Vector3(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
                _heldWeapon.transform.position=new Vector3(_position.X,_position.Y,0)+direction*.55f;
                _heldWeapon.transform.rotation=Quaternion.Euler(0,0,angle);
            }
        }
        private void PreviewHit(CombatWeaponId weapon,float aimDegrees)
        {
            var slot=_combat.Controller.Slot(weapon);var stats=slot.Definition.Stats;
            var origin=new Vector2(_position.X,_position.Y);
            var aim=new Vector2(Mathf.Cos(aimDegrees*Mathf.Deg2Rad),Mathf.Sin(aimDegrees*Mathf.Deg2Rad));
            if(slot.IsRanged)
            {
                var count=CombatShotMath.PelletAngles(aimDegrees,slot.Pellets,slot.SpreadDegrees,_pelletAngles);
                var shot=new ConfiguredShot(_balance,CombatWeaponIds.ToJsonId(weapon));
                var pose=new SpawnPose(new Vector3(origin.x,origin.y,0),Quaternion.identity,transform);
                for(var i=0;i<count;i++)
                {
                    var direction=new Vector2(Mathf.Cos(_pelletAngles[i]*Mathf.Deg2Rad),Mathf.Sin(_pelletAngles[i]*Mathf.Deg2Rad));
                    float distance;var hit=NearestHit(origin,direction,slot.Range,out distance);
                    if(hit!=null&&hit.IsDestructible)_destructibles.DamageConfirmed(hit.Id,Mathf.RoundToInt(stats.Damage),0,++_hitSequence);
                    var projectile=_projectiles.Create(in pose);projectile.Launch(in shot,direction);
                }
                return;
            }
            var duration=Mathf.Max(.1f,slot.CadenceSeconds*.6f);
            var swingPose=new SpawnPose(new Vector3(origin.x,origin.y,0),Quaternion.identity,transform);
            var swing=_swings.Create(in swingPose);
            swing.Play(_weaponSprites[(int)weapon],_hero,aimDegrees,slot.ArcDegrees,slot.Range,duration);
            swing.GetComponentInChildren<SpriteRenderer>(true).sortingOrder=10;
            _pool.Despawn(swing,duration+.05f);_swingUntil=Time.time+duration;
            foreach(var target in _map.Objects)
            {
                if(!target.IsDestructible||_destructibles.IsBroken(target.Id))continue;
                var b=target.Bounds;var closest=new Vector2(Mathf.Clamp(origin.x,b.MinX,b.MaxX),Mathf.Clamp(origin.y,b.MinY,b.MaxY));
                var delta=closest-origin;if(delta.magnitude>slot.Range||Vector2.Angle(aim,delta)>slot.ArcDegrees*.5f)continue;
                float distance;var blocker=NearestHit(origin,delta.normalized,delta.magnitude+.001f,out distance);
                if(blocker!=null&&blocker.Id!=target.Id)continue;
                var damage=stats.Damage*(weapon==CombatWeaponId.Axe?1.5f:1f);
                _destructibles.DamageConfirmed(target.Id,Mathf.RoundToInt(damage),0,++_hitSequence);
            }
        }
        private WorldObjectDefinition NearestHit(Vector2 origin,Vector2 direction,float range,out float distance)
        {
            distance=range;WorldObjectDefinition hit=null;
            foreach(var item in _map.Objects)
            {
                if(!item.IsHardCover&&!(item.IsDestructible&&!_destructibles.IsBroken(item.Id))&&!(item.Kind==WorldObjectKind.Door&&_hazards.DoorsClosed))continue;
                var near=0f;var far=range;var b=item.Bounds;
                if(!Clip(origin.x,direction.x,b.MinX,b.MaxX,ref near,ref far)||!Clip(origin.y,direction.y,b.MinY,b.MaxY,ref near,ref far)||near>=distance)continue;
                distance=near;hit=item;
            }
            return hit;
        }
        private static bool Clip(float origin,float direction,float min,float max,ref float near,ref float far)
        {
            if(Mathf.Abs(direction)<.000001f)return origin>=min&&origin<=max;
            var a=(min-origin)/direction;var b=(max-origin)/direction;
            if(a>b){var swap=a;a=b;b=swap;}near=Mathf.Max(near,a);far=Mathf.Min(far,b);return near<=far;
        }
        private void RenderItemIcons()
        {
            foreach(var item in _floor.Items)if(_views.TryGetValue(item.Id,out var view))
            {
                var icon=_icons.GetIcon(item.Stack.ItemId);if(icon==null)continue;
                var renderer=view.GetComponent<SpriteRenderer>();renderer.sprite=icon;renderer.color=Color.white;
                var size=icon.bounds.size;view.transform.localScale=Vector3.one*(.45f/Mathf.Max(size.x,size.y));
            }
        }
        private void UpdateDemoCollision()
        {
            foreach(var item in _map.Objects)
            {
                var solid=item.IsHardCover||(item.IsDestructible&&!_destructibles.IsBroken(item.Id))||(item.Kind==WorldObjectKind.Door&&_hazards.DoorsClosed);
                if(!solid){if(_demoCovers.TryGetValue(item.Id,out var old)){old.enabled=false;Destroy(old.gameObject);_demoCovers.Remove(item.Id);}continue;}
                if(_demoCovers.ContainsKey(item.Id))continue;
                var go=new GameObject("Demo collision "+item.Id){layer=LayerMask.NameToLayer("HardCover")};go.transform.SetParent(transform,false);
                var bounds=item.Bounds;var center=bounds.Center;go.transform.position=new Vector3(center.X,center.Y,0);
                var collider=go.AddComponent<BoxCollider2D>();collider.size=new Vector2(bounds.MaxX-bounds.MinX,bounds.MaxY-bounds.MinY);_demoCovers.Add(item.Id,collider);
            }
        }
        private void DisposeCombat()
        {
            if(_combat!=null){_combat.Confirmed-=PreviewHit;_combat.Dispose();}
            if(_pool==null)return;
            foreach(var projectile in GetComponentsInChildren<CoverProbeProjectile>())_pool.Despawn(projectile);
            foreach(var swing in GetComponentsInChildren<MeleeSwingVisual>())_pool.Despawn(swing);
        }
    }
}
