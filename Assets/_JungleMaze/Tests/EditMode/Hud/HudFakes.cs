using System;
using JungleMaze.App.Hud.State;
using JungleMaze.Domain.Hud;
using JungleMaze.Presentation.UI.Hud;

namespace JungleMaze.Tests.Hud
{
    /// <summary>Records the last value pushed to each Vitals element; null value text means hidden.</summary>
    public sealed class FakeVitalsView : IVitalsView
    {
        public string HpNumber, HpMax;
        public bool HpLow;
        public float HpFill = -1f, HpTrail = -1f;
        public readonly string[] Labels = new string[3];
        public readonly TextTone[] LabelTones = new TextTone[3];
        public readonly string[] Values = new string[3];
        public readonly TextTone[] ValueTones = new TextTone[3];
        public readonly float[] Fills = { -1f, -1f, -1f };
        public int Writes;

        public void SetHpNumber(char[] text, int length, bool low) { Writes++; HpNumber = new string(text, 0, length); HpLow = low; }
        public void SetHpMax(char[] text, int length) { Writes++; HpMax = new string(text, 0, length); }
        public void SetHpBar(float fill, float trail) { Writes++; HpFill = fill; HpTrail = trail; }
        public void SetRowLabel(VitalsRow row, string label, TextTone tone) { Writes++; Labels[(int)row] = label; LabelTones[(int)row] = tone; }

        public void SetRowValue(VitalsRow row, char[] text, int length, TextTone tone)
        {
            Writes++;
            Values[(int)row] = length > 0 ? new string(text, 0, length) : null;
            ValueTones[(int)row] = tone;
        }

        public void SetRowFill(VitalsRow row, float fill) { Writes++; Fills[(int)row] = fill; }
    }

    public sealed class FakeQuickbarView : IQuickbarView
    {
        public string WeaponName, Magazine, Reserve;
        public bool AmmoVisible, ReloadVisible;
        public readonly string[] SlotIds = new string[8];
        public readonly string[] SlotLabels = new string[8];
        public readonly string[] SlotCounts = new string[8];
        public readonly SlotVisual[] Visuals = new SlotVisual[8];
        public int Writes;

        public void SetWeaponName(string name) { Writes++; WeaponName = name; }

        public void SetAmmo(bool visible, char[] magazine, int magazineLength, char[] reserve, int reserveLength)
        {
            Writes++;
            AmmoVisible = visible;
            Magazine = visible ? new string(magazine, 0, magazineLength) : null;
            Reserve = visible ? new string(reserve, 0, reserveLength) : null;
        }

        public void SetReloadHint(bool visible) { Writes++; ReloadVisible = visible; }

        public void SetSlot(int index, string itemId, string label, char[] count, int countLength, SlotVisual visual)
        {
            Writes++;
            SlotIds[index] = itemId;
            SlotLabels[index] = label;
            SlotCounts[index] = countLength > 0 ? new string(count, 0, countLength) : null;
            Visuals[index] = visual;
        }
    }

    public sealed class FakeMinimapView : IMinimapView
    {
        public bool Visible;
        public float X, Y, Heading;
        public int Writes;
        public float SizePixels => 200f;
        public void SetMarker(bool visible, float x, float y, float headingDegrees) { Writes++; Visible = visible; X = x; Y = y; Heading = headingDegrees; }
    }

    public sealed class FakeMatchStatusView : IMatchStatusView
    {
        public string Zone, Alive;
        public int Writes;
        public void SetZoneCountdown(char[] text, int length) { Writes++; Zone = new string(text, 0, length); }
        public void SetAlive(char[] text, int length) { Writes++; Alive = new string(text, 0, length); }
    }
    /// <summary>Hand-driven IHudState: tests set fields then call Raise; no store, bus or config involved.</summary>
    public sealed class FakeHudState : IHudState
    {
        public event Action<HudStateChange> Changed;
        public int Hp { get; set; } = 100;
        public int MaxHp { get; set; } = 100;
        public GearStatus Armor { get; set; }
        public GearStatus Helmet { get; set; }
        public HeldWeaponStatus HeldWeapon { get; set; } = new HeldWeaponStatus("HAND", "Bare Hands", null);
        public AmmoStatus Ammo { get; set; }
        public MatchStatusInfo MatchStatus { get; set; }
        public int MapSizeCells { get; set; } = 50;
        public bool HasPose { get; set; }
        public HeroPose Pose { get; set; }
        public void Raise(HudStateChange change) => Changed?.Invoke(change);
        public int SubscriberCount => Changed?.GetInvocationList().Length ?? 0;
    }

    public sealed class FakeInventoryState : IInventoryState
    {
        public event Action<InventoryChange> Changed;
        public readonly InventorySlot[] Slots = new InventorySlot[8];
        public int WeaponSlotCount => 4;
        public int ConsumableSlotCount => 4;
        public int ActiveSlot { get; set; }
        public InventorySlot GetSlot(int index) => index >= 0 && index < 8 ? Slots[index] : default;
        public void Raise(InventoryChange change) => Changed?.Invoke(change);
        public int SubscriberCount => Changed?.GetInvocationList().Length ?? 0;
    }

    public sealed class FakeEffectState : IEffectState
    {
        public event Action<EffectChange> Changed;
        public bool HasAdrenaline;
        public ActiveEffect Adrenaline;
        public int Count => HasAdrenaline ? 1 : 0;
        public ActiveEffect GetAt(int index) => index == 0 && HasAdrenaline ? Adrenaline : default;

        public bool TryGet(EffectKind kind, out ActiveEffect effect)
        {
            effect = kind == EffectKind.Adrenaline && HasAdrenaline ? Adrenaline : default;
            return kind == EffectKind.Adrenaline && HasAdrenaline;
        }

        public void Raise(EffectChange change) => Changed?.Invoke(change);
        public int SubscriberCount => Changed?.GetInvocationList().Length ?? 0;
    }
}
