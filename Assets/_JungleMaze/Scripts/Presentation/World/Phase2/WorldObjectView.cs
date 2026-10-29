using JungleBackend.Protocol.Gameplay;
using UnityEngine;
namespace JungleMaze.Presentation.World.Phase2
{
    /// <summary>Pure visual child. Never owns damage, cooldown, loot, or transport.</summary>
    public sealed class WorldObjectView : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        public ushort WorldId {get;private set;}
        public void Apply(WorldObjectWire state,Sprite sprite)
        {
            WorldId=state.Id;if(_renderer==null)_renderer=gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite=sprite;_renderer.sortingOrder=state.Kind==100?8:state.Kind>=8&&state.Kind<=10?-1:1;
            transform.position=new Vector3(state.X,state.Y,0);
            if(sprite!=null){var s=sprite.bounds.size;transform.localScale=new Vector3(state.Width/Mathf.Max(.01f,s.x),state.Height/Mathf.Max(.01f,s.y),1);}
            var broken=(state.Flags&1)!=0;
            _renderer.color=ColorFor(state.Kind);
            if(state.Kind==12&&(state.Flags&2)==0)_renderer.color=new Color(.5f,.65f,.65f,.3f);
            if(broken)_renderer.color=new Color(.4f,.35f,.3f,.23f);
        }
        public static Color ColorFor(byte kind)
        {
            switch(kind){case 5:return new Color(.75f,.45f,.16f);case 6:return new Color(.65f,.37f,.3f);case 7:return new Color(.2f,.5f,.22f,.8f);
                case 8:return new Color(.25f,.4f,.25f);case 9:return new Color(1,.3f,.08f);case 10:return new Color(.65f,.22f,.7f);
                case 11:return new Color(.2f,.65f,1);case 100:return new Color(1,.85f,.25f);default:return new Color(.55f,.65f,.6f);}
        }
    }
}
