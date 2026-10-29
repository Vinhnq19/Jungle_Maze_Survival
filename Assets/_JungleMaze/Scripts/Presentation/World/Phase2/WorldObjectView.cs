using JungleBackend.Protocol.Gameplay;
using UnityEngine;
namespace JungleMaze.Presentation.World.Phase2
{
    /// <summary>Presentation state only; none of these states changes authoritative world simulation.</summary>
    public enum WorldVisualState { Normal, Damaged, Broken, Activated, Teleported }

    /// <summary>Pure visual child. Never owns damage, cooldown, loot, or transport.</summary>
    public sealed class WorldObjectView : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private WorldVisualSet _set;
        private WorldVisualSet.Entry _entry;
        private WorldVisualSet.Frames _frames;
        private Sprite _fallback;
        private byte _kind;
        private ushort _hp;
        private bool _applied, _broken, _closed, _hasSource;
        private float _elapsed, _flashRemaining;
        private uint _lastCue;
        private Color _baseColor;
        public ushort WorldId {get;private set;}
        public WorldVisualState VisualState { get; private set; }

        /// <summary>Shares an extracted visual catalog. Repeated configuration keeps animation progress.</summary>
        public void Configure(WorldVisualSet set) { _set = set; }

        /// <summary>Applies a replicated state; repeated snapshots never replay damage or restart ambient frames.</summary>
        public void Apply(WorldObjectWire state,Sprite sprite)
        {
            var fresh = !_applied || WorldId != state.Id || _kind != state.Kind;
            var broken = (state.Flags & 1) != 0;
            var closed = (state.Flags & 2) != 0;
            if(_renderer==null)_renderer=gameObject.AddComponent<SpriteRenderer>();
            if (fresh) { _elapsed = _flashRemaining = 0; _lastCue = 0; VisualState = WorldVisualState.Normal; }
            WorldId=state.Id; _kind=state.Kind; _fallback=sprite;
            _hasSource = _set != null && _set.TryFind(state.Kind, out _entry);
            if (!_hasSource) _entry = default;
            var nextFrames = state.Kind == 12 ? (closed ? _entry.Close : _entry.Open) : _entry.Idle;
            if (fresh || _frames != nextFrames || broken != _broken || closed != _closed) _elapsed = 0;
            _frames = nextFrames;
            if (!fresh && !broken && state.Hp < _hp) { _flashRemaining=.16f; VisualState=WorldVisualState.Damaged; }
            if (broken) { _flashRemaining=0; VisualState=WorldVisualState.Broken; }
            else if (fresh || _broken) VisualState=WorldVisualState.Normal;
            _broken=broken;_closed=closed;_hp=state.Hp;_applied=true;
            _renderer.sortingOrder=state.Kind==100?8:state.Kind>=8&&state.Kind<=10?-1:1;
            transform.position=new Vector3(state.X,state.Y,0);
            var reference=_entry.Sprite!=null?_entry.Sprite:sprite;
            if(reference!=null){var s=reference.bounds.size;transform.localScale=new Vector3(state.Width/Mathf.Max(.01f,s.x),state.Height/Mathf.Max(.01f,s.y),1);}
            _baseColor=_hasSource?Color.white:ColorFor(state.Kind);
            if(state.Kind==12&&!closed)_baseColor.a=.25f;
            Render();
        }

        /// <summary>Plays one confirmed presentation cue. Duplicate or older event sequences are ignored.</summary>
        public bool PlayCue(WorldVisualState cue,uint sequence)
        {
            if (!_applied || _broken || sequence==0 || (_lastCue!=0 && unchecked((int)(sequence-_lastCue))<=0) ||
                (cue!=WorldVisualState.Activated && cue!=WorldVisualState.Teleported)) return false;
            _lastCue=sequence;VisualState=cue;_flashRemaining=.25f;Render();return true;
        }

        private void Update() { AdvanceVisual(Time.deltaTime); }

        /// <summary>Advances only visual animation; exposed for deterministic presentation tests.</summary>
        public void AdvanceVisual(float seconds)
        {
            if (!_applied || seconds<0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            _elapsed+=seconds;_flashRemaining=Mathf.Max(0,_flashRemaining-seconds);
            if (_flashRemaining<=0 && !_broken) VisualState=WorldVisualState.Normal;
            Render();
        }

        private void Render()
        {
            var frame=_broken?_entry.BrokenSprite:_frames?.Sample(_elapsed);
            if(frame==null&&!_broken)frame=_entry.Sprite!=null?_entry.Sprite:_fallback;
            _renderer.sprite=frame;
            // A missing broken asset disappears instead of tinting an intact chest as fake debris.
            _renderer.enabled=frame!=null;
            _renderer.color=_flashRemaining>0?(VisualState==WorldVisualState.Teleported?new Color(.5f,.9f,1):new Color(1,.55f,.5f)):_baseColor;
        }
        public static Color ColorFor(byte kind)
        {
            switch(kind){case 5:return new Color(.75f,.45f,.16f);case 6:return new Color(.65f,.37f,.3f);case 7:return new Color(.2f,.5f,.22f,.8f);
                case 8:return new Color(.25f,.4f,.25f);case 9:return new Color(1,.3f,.08f);case 10:return new Color(.65f,.22f,.7f);
                case 11:return new Color(.2f,.65f,1);case 100:return new Color(1,.85f,.25f);default:return new Color(.55f,.65f,.6f);}
        }
    }
}
