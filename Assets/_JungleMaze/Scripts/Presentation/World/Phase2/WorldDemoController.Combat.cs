using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Combat;
using JungleMaze.App.Ports;
using JungleMaze.App.World;
using JungleMaze.Core.DI;
using JungleMaze.Presentation.UI.Hud;
using JungleMaze.World;
using UnityEngine;

namespace JungleMaze.Presentation.World.Phase2
{
    public sealed partial class WorldDemoController
    {
        private IBalanceConfigProvider _balance;
        private IItemIconProvider _icons;
        private WorldDemoCombatSession _combat;
        private SpriteRenderer _heldWeapon;
        private readonly SpriteRenderer[] _traces=new SpriteRenderer[16];
        private readonly float[] _pelletAngles=new float[16];
        private float _aimDegrees, _swingUntil, _traceUntil;
        private uint _hitSequence;
        public WorldDemoCombatSession DemoCombat=>_combat;
        public DestructibleService Destructibles=>_destructibles;

        [Inject] public void ConfigureCombat(IBalanceConfigProvider balance,IItemIconProvider icons)
        {_balance=balance;_icons=icons;}
        private void InitializeCombat()
        {
            _combat=new WorldDemoCombatSession(_balance,_hud,_items);_combat.Confirmed+=PreviewHit;
            var held=new GameObject("Demo held weapon");held.transform.SetParent(transform,false);
            _heldWeapon=held.AddComponent<SpriteRenderer>();_heldWeapon.sortingOrder=10;
            for(var i=0;i<_traces.Length;i++)
            {
                var trace=new GameObject("Demo shot trace "+i);trace.transform.SetParent(transform,false);
                _traces[i]=trace.AddComponent<SpriteRenderer>();_traces[i].sprite=_square;
                _traces[i].color=Color.yellow;_traces[i].sortingOrder=11;_traces[i].enabled=false;
            }
        }
        private void UpdateCombat()
        {
            _combat.Tick(Time.deltaTime);
            var cursor=_camera.ScreenToWorldPoint(Input.mousePosition);
            _aimDegrees=Mathf.Atan2(cursor.y-_position.Y,cursor.x-_position.X)*Mathf.Rad2Deg;
            if(_hp>0&&Input.GetMouseButton(0))_combat.TryAttack(_aimDegrees);
            if(_hp>0&&Input.GetKeyDown(KeyCode.R))_combat.TryReload();
            var held=_items.HeldWeapon(0);_heldWeapon.sprite=held.IsEmpty?null:_icons.GetIcon(held.ItemId);
            if(_heldWeapon.sprite!=null)
            {
                var size=_heldWeapon.sprite.bounds.size;_heldWeapon.transform.localScale=Vector3.one*(.8f/Mathf.Max(size.x,size.y));
                var angle=_aimDegrees;
                if(Time.time<_swingUntil)angle+=Mathf.Lerp(-50,50,1-(_swingUntil-Time.time)/.2f);
                var direction=new Vector3(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
                _heldWeapon.transform.position=new Vector3(_position.X,_position.Y,0)+direction*.55f;
                _heldWeapon.transform.rotation=Quaternion.Euler(0,0,angle);
            }
            if(Time.time>=_traceUntil)foreach(var trace in _traces)trace.enabled=false;
        }
        private void PreviewHit(CombatWeaponId weapon,float aimDegrees)
        {
            var slot=_combat.Controller.Slot(weapon);var stats=slot.Definition.Stats;
            var origin=new Vector2(_position.X,_position.Y);
            var aim=new Vector2(Mathf.Cos(aimDegrees*Mathf.Deg2Rad),Mathf.Sin(aimDegrees*Mathf.Deg2Rad));
            if(slot.IsRanged)
            {
                var count=CombatShotMath.PelletAngles(aimDegrees,slot.Pellets,slot.SpreadDegrees,_pelletAngles);
                for(var i=0;i<count;i++)
                {
                    var direction=new Vector2(Mathf.Cos(_pelletAngles[i]*Mathf.Deg2Rad),Mathf.Sin(_pelletAngles[i]*Mathf.Deg2Rad));
                    float distance;var hit=NearestHit(origin,direction,slot.Range,out distance);
                    if(hit!=null&&hit.IsDestructible)_destructibles.DamageConfirmed(hit.Id,Mathf.RoundToInt(stats.Damage),0,++_hitSequence);
                    var trace=_traces[i];trace.enabled=true;trace.transform.position=origin+direction*(distance*.5f);
                    trace.transform.rotation=Quaternion.Euler(0,0,_pelletAngles[i]);trace.transform.localScale=new Vector3(distance,.035f,1);
                }
                _traceUntil=Time.time+.1f;return;
            }
            _swingUntil=Time.time+.2f;
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
        private void DisposeCombat(){if(_combat!=null){_combat.Confirmed-=PreviewHit;_combat.Dispose();}}
    }
}
