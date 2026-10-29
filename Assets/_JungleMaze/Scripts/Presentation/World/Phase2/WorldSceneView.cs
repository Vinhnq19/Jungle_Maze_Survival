using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.World;
using JungleMaze.App.Match;
using JungleMaze.App.MatchUi;
using JungleMaze.World.Inventory;
using JungleMaze.Core.DI;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace JungleMaze.Presentation.World.Phase2
{
    /// <summary>Online authoritative world presentation and item intent only.</summary>
    public sealed class WorldSceneView : MonoBehaviour
    {
        private WorldClientSession _world;private MatchSession _match;
        private IMatchMenuState _menu;private ItemCatalog _catalog;private float _autoPickupAt;
        private Transform _root;private Sprite _square;private Texture2D _texture;private WorldVisualSet _visuals;
        private readonly Dictionary<ushort,WorldObjectView> _views=new Dictionary<ushort,WorldObjectView>();
        [Inject] public void Construct(WorldClientSession world,MatchSession match){_world=world;_match=match;}
        [Inject] public void Configure(IMatchMenuState menu,ItemCatalog catalog){_menu=menu;_catalog=catalog;}
        private void Start(){_world.Changed+=Refresh;_visuals=Resources.Load<WorldVisualSet>("WorldVisualSet");Refresh();}
        private void Update()
        {
            if(_world.Layout==null||SceneManager.GetActiveScene().name=="WorldDemo"||(_menu?.BlocksGameplayInput??false))return;
            if(Time.unscaledTime>=_autoPickupAt&&_catalog!=null)
            {
                _autoPickupAt=Time.unscaledTime+.25f;
                foreach(var o in _world.Objects)if(o.Kind==100&&Vector2.Distance(new Vector2(_match.Prediction.PredictedX,_match.Prediction.PredictedY),new Vector2(o.X,o.Y))<=.6f&&_catalog.TryGet(o.ItemId,out var item)&&
                    (item.Kind==ItemKind.Ammo||item.Kind==ItemKind.Helmet||item.Kind==ItemKind.Armor))_world.Send(WorldActionKind.Pickup,o.Id);
            }
            if(Input.GetKeyDown(KeyCode.F))
            {
                var camera=Camera.main;if(camera==null)return;var cursor=camera.ScreenToWorldPoint(Input.mousePosition);ushort target=0;var best=float.MaxValue;
                var own=new Vector2(_match.Prediction.PredictedX,_match.Prediction.PredictedY);
                foreach(var o in _world.Objects)if(o.Kind==100&&Vector2.Distance(own,new Vector2(o.X,o.Y))<=1.2f)
                {var d=((Vector2)cursor-new Vector2(o.X,o.Y)).sqrMagnitude;if(d<best){best=d;target=o.Id;}}
                if(target!=0)_world.Send(WorldActionKind.Pickup,target);
            }
            for(var i=0;i<4;i++)
            {
                if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i)))_world.Send(WorldActionKind.Select,0,(byte)(_world.Inventory!=null&&_world.Inventory.ActiveWeapon==i?255:i));
                if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha5+i)))_world.Send(WorldActionKind.Use,0,(byte)i);
            }
            if(Input.GetKeyDown(KeyCode.Backspace)&&_world.Inventory!=null&&_world.Inventory.ActiveWeapon>=0)_world.Send(WorldActionKind.Drop,0,(byte)_world.Inventory.ActiveWeapon);
            if(Input.GetAxisRaw("Horizontal")!=0||Input.GetAxisRaw("Vertical")!=0||Input.GetMouseButtonDown(0))
                if(_world.Inventory!=null&&_world.Inventory.IsUsing)_world.Send(WorldActionKind.CancelUse);
        }
        private void Refresh()
        {
            if(_root==null){_views.Clear();var go=new GameObject("Authoritative World Views");_root=go.transform;}
            if(_square==null){_texture=new Texture2D(1,1);_texture.SetPixel(0,0,Color.white);_texture.Apply();_square=Sprite.Create(_texture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);}
            ApplyViews(_world.Objects,_root,_views,_visuals,_square);
        }
        public static void ApplyViews(WorldObjectWire[] states,Transform root,Dictionary<ushort,WorldObjectView> views,WorldVisualSet set,Sprite fallback)
        {
            var seen=new HashSet<ushort>();foreach(var s in states){seen.Add(s.Id);if(!views.TryGetValue(s.Id,out var view)){var go=new GameObject("World "+s.Kind+" #"+s.Id);go.transform.SetParent(root,false);view=go.AddComponent<WorldObjectView>();views.Add(s.Id,view);}view.Apply(s,set!=null?set.Find(s.Kind)??fallback:fallback);}
            var gone=new List<ushort>();foreach(var entry in views)if(!seen.Contains(entry.Key))gone.Add(entry.Key);
            foreach(var id in gone){if(views[id]!=null)Object.Destroy(views[id].gameObject);views.Remove(id);}
        }
        private void OnDestroy(){if(_world!=null)_world.Changed-=Refresh;if(_root!=null)Destroy(_root.gameObject);if(_square!=null)Destroy(_square);if(_texture!=null)Destroy(_texture);}
    }
}
