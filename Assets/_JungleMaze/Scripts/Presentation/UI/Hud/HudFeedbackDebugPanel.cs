#if UNITY_EDITOR || DEVELOPMENT_BUILD
using JungleMaze.App.Hud.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.Core.DI;
using JungleMaze.Domain.Hud;
using UnityEngine;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>Development-only F5 event injector, independent of the core F4 panel.</summary>
    public sealed class HudFeedbackDebugPanel:MonoBehaviour
    {
        private MockHudState _hud;private MockInventoryState _inventory;private MockEffectState _effects;private HudFeedbackScenarios _scenarios;private IHudState _read;
        private bool _visible,_cancellable=true,_untimed,_noDirection;private int _slot=4,_effect;
        private string _item="GOD_LEAF",_count="2",_seconds="3",_degrees="0",_fuse="1.5",_id="1",_dx="-20",_dy="0";private Vector2 _scroll;
        private static readonly string[] SlotLabels={"1","2","3","4","5","6","7","8"};
        private static readonly string[] EffectLabels={"Độc","Chậm","Ẩn","Ngất","Trói","Đảo","Miễn"};
        [Inject] public void Construct(MockHudState hud,MockInventoryState inventory,MockEffectState effects,HudFeedbackScenarios scenarios,IHudState read){_hud=hud;_inventory=inventory;_effects=effects;_scenarios=scenarios;_read=read;}
        private void Update(){if(Input.GetKeyDown(KeyCode.F5))_visible=!_visible;}
        private void OnGUI(){if(!_visible||_hud==null||_inventory==null||_effects==null||_scenarios==null)return;GUI.Window(GetInstanceID(),new Rect(Mathf.Max(16,Screen.width-470),16,454,Mathf.Min(680,Screen.height-32)),Draw,"HUD feedback · F5");}
        private void Draw(int id)
        {
            _scroll=GUILayout.BeginScrollView(_scroll);GUILayout.Label("Dùng đồ — thời gian đọc từ equipments.json");_slot=GUILayout.Toolbar(_slot,SlotLabels);_item=GUILayout.TextField(_item);_cancellable=GUILayout.Toggle(_cancellable,"Di chuyển để hủy");
            GUILayout.BeginHorizontal();if(GUILayout.Button("Đặt món vào khe"))_inventory.SetSlot(_slot,_item,2);if(GUILayout.Button("Bắt đầu"))_inventory.BeginUse(_slot,_cancellable);if(GUILayout.Button("Hủy"))_inventory.CancelUse();if(GUILayout.Button("Xong"))_inventory.CompleteUse();GUILayout.EndHorizontal();
            GUILayout.Label("Hiệu ứng");_effect=GUILayout.Toolbar(_effect,EffectLabels);_seconds=GUILayout.TextField(_seconds);_untimed=GUILayout.Toggle(_untimed,"Không thời lượng");var kind=(EffectKind)(_effect+2);
            GUILayout.BeginHorizontal();if(GUILayout.Button("Thêm / làm mới"))_effects.Apply(kind,_untimed?0:Number(_seconds),_untimed?0:Number(_seconds));if(GUILayout.Button("Gỡ"))_effects.Remove(kind);if(GUILayout.Button("Gỡ tất cả"))_effects.Clear();GUILayout.EndHorizontal();
            GUILayout.Label("Nhặt đồ — chỉ thông báo, không đổi túi");_count=GUILayout.TextField(_count);if(GUILayout.Button("Nhặt "+_item))_hud.Pickup(_item,Integer(_count));
            GUILayout.BeginHorizontal();if(GUILayout.Button("Trúng Hero"))_hud.Hit(HitTargetKind.Hero);if(GUILayout.Button("Trúng vật"))_hud.Hit(HitTargetKind.Other);GUILayout.EndHorizontal();
            GUILayout.Label("Nhận sát thương: góc 0=Đông, 90=Bắc");_degrees=GUILayout.TextField(_degrees);_noDirection=GUILayout.Toggle(_noDirection,"Không cho biết hướng");if(GUILayout.Button("Bơm hướng sát thương"))_hud.Damage(_noDirection?(float?)null:Number(_degrees));
            GUILayout.Label("Cảnh báo: id / ngòi / dx / dy (cell)");_id=GUILayout.TextField(_id);_fuse=GUILayout.TextField(_fuse);GUILayout.BeginHorizontal();_dx=GUILayout.TextField(_dx);_dy=GUILayout.TextField(_dy);GUILayout.EndHorizontal();var pose=_read!=null&&_read.HasPose?_read.Pose:default;var x=pose.X+Number(_dx);var y=pose.Y+Number(_dy);
            GUILayout.BeginHorizontal();if(GUILayout.Button("Bom"))_hud.Bomb(Integer(_id),Number(_fuse),x,y);if(GUILayout.Button("Bẫy"))_hud.Trap(Integer(_id),x,y);if(GUILayout.Button("Gỡ cảnh báo"))_hud.ClearDanger(Integer(_id));GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();if(GUILayout.Button("Dồn AC-FB42"))_scenarios.Burst();if(GUILayout.Button("Giao tranh AC-FB50"))_scenarios.Skirmish();if(GUILayout.Button("Dừng"))_scenarios.Stop();GUILayout.EndHorizontal();GUILayout.EndScrollView();
        }
        private static float Number(string text)=>float.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var value)?value:0;
        private static int Integer(string text)=>int.TryParse(text,out var value)?value:0;
    }
}
#endif
