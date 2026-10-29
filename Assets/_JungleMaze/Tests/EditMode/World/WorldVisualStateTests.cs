using JungleBackend.Protocol.Gameplay;
using JungleMaze.Presentation.World.Phase2;
using JungleMaze.World;
using NUnit.Framework;
using UnityEngine;

namespace JungleMaze.Tests.World
{
    /// <summary>Presentation regression coverage for authoritative world state and extracted frame playback.</summary>
    public sealed class WorldVisualStateTests
    {
        private Texture2D _texture;
        private Sprite _first, _second;
        private GameObject _object;
        private WorldObjectView _view;
        private WorldVisualSet _set;

        [SetUp] public void SetUp()
        {
            _texture=new Texture2D(2,1);
            _first=Sprite.Create(_texture,new Rect(0,0,1,1),Vector2.one*.5f,1);
            _second=Sprite.Create(_texture,new Rect(1,0,1,1),Vector2.one*.5f,1);
            _object=new GameObject("world visual test");_view=_object.AddComponent<WorldObjectView>();
            _set=ScriptableObject.CreateInstance<WorldVisualSet>();_view.Configure(_set);
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(_object);Object.DestroyImmediate(_set);
            Object.DestroyImmediate(_first);Object.DestroyImmediate(_second);Object.DestroyImmediate(_texture);
        }

        [Test] public void RepeatedSnapshotDoesNotRestartAmbientAnimation()
        {
            _set.Entries=new[]{new WorldVisualSet.Entry{Kind=WorldObjectKind.Portal,Sprite=_first,Idle=Frames(true)}};
            var state=State(11);_view.Apply(state,_first);_view.AdvanceVisual(.6f);
            Assert.AreSame(_second,Renderer.sprite);
            _view.Apply(state,_first);Assert.AreSame(_second,Renderer.sprite);
            _view.AdvanceVisual(.5f);Assert.AreSame(_first,Renderer.sprite);
        }

        [Test] public void DamageFlashRequiresHpDecreaseAndDuplicateDoesNotExtendIt()
        {
            var state=State(5);_view.Apply(state,_first);state.Hp=20;_view.Apply(state,_first);
            Assert.AreEqual(WorldVisualState.Damaged,_view.VisualState);
            _view.AdvanceVisual(.1f);_view.Apply(state,_first);_view.AdvanceVisual(.07f);
            Assert.AreEqual(WorldVisualState.Normal,_view.VisualState);
        }

        [Test] public void ConfirmedBrokenPotUsesOnlyExtractedDebrisAndHasNoCollider()
        {
            _set.Entries=new[]{new WorldVisualSet.Entry{Kind=WorldObjectKind.Pot,Sprite=_first,BrokenSprite=_second}};
            var state=State(6);_view.Apply(state,_first);state.Flags=1;state.Hp=0;_view.Apply(state,_first);
            Assert.AreSame(_second,Renderer.sprite);Assert.IsTrue(Renderer.enabled);
            Assert.AreEqual(WorldVisualState.Broken,_view.VisualState);
            Assert.IsNull(_object.GetComponent<Collider2D>());Assert.IsNull(_object.GetComponent<Animator>());
            _view.AdvanceVisual(10);_view.Apply(state,_first);Assert.AreSame(_second,Renderer.sprite);
        }

        [Test] public void MissingBrokenArtworkDoesNotLeaveAnIntactChestVisible()
        {
            var state=State(5);state.Flags=1;state.Hp=0;_view.Apply(state,_first);
            Assert.IsFalse(Renderer.enabled);Assert.AreEqual(WorldVisualState.Broken,_view.VisualState);
        }

        [Test] public void ReusedViewResetsBrokenStateAndCueSequence()
        {
            var state=State(5);_view.Apply(state,_first);Assert.IsTrue(_view.PlayCue(WorldVisualState.Activated,8));
            state.Flags=1;_view.Apply(state,_first);state.Id=2;state.Flags=0;state.Hp=30;_view.Apply(state,_first);
            Assert.IsTrue(Renderer.enabled);Assert.AreEqual(WorldVisualState.Normal,_view.VisualState);
            Assert.IsTrue(_view.PlayCue(WorldVisualState.Teleported,1));
        }

        [Test] public void ConfirmedCueIsIdempotentAndNeverChangesWorldFlagsOrPosition()
        {
            var state=State(11);_view.Apply(state,_first);
            Assert.IsTrue(_view.PlayCue(WorldVisualState.Teleported,10));_view.AdvanceVisual(.2f);
            Assert.IsFalse(_view.PlayCue(WorldVisualState.Teleported,10));Assert.IsFalse(_view.PlayCue(WorldVisualState.Activated,9));
            _view.AdvanceVisual(.06f);Assert.AreEqual(WorldVisualState.Normal,_view.VisualState);
            Assert.AreEqual(new Vector3(state.X,state.Y,0),_view.transform.position);Assert.AreEqual(0,state.Flags);
        }

        [Test] public void DoorFramesFollowServerFlagAndHoldTheirLastFrame()
        {
            _set.Entries=new[]{new WorldVisualSet.Entry{Kind=WorldObjectKind.Door,Sprite=_first,Open=Frames(false),Close=Frames(false)}};
            var state=State(12);_view.Apply(state,_first);_view.AdvanceVisual(2);
            Assert.AreSame(_second,Renderer.sprite);Assert.Less(Renderer.color.a,.5f);
            state.Flags=2;_view.Apply(state,_first);Assert.AreSame(_first,Renderer.sprite);
            _view.AdvanceVisual(2);Assert.AreSame(_second,Renderer.sprite);Assert.AreEqual(1,Renderer.color.a);
        }

        private SpriteRenderer Renderer=>_object.GetComponent<SpriteRenderer>();
        private WorldVisualSet.Frames Frames(bool loop)=>new WorldVisualSet.Frames{Sprites=new[]{_first,_second},Times=new[]{0f,.5f},Duration=1,Loop=loop};
        private static WorldObjectWire State(byte kind)=>new WorldObjectWire{Id=1,Kind=kind,Hp=30,X=3,Y=4,Width=1,Height=1};
    }
}
