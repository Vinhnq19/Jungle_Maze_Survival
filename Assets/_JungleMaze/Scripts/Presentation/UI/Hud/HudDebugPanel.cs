#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using JungleMaze.App.Hud.Mocks;
using JungleMaze.Core.DI;
using UnityEngine;

namespace JungleMaze.Presentation.UI.Hud
{
    /// <summary>
    /// Dev-only HUD value injector (G-8, AC16), toggled with F4. A thin IMGUI layer over the HUD mocks: it draws
    /// buttons and fields and calls the mocks, which write through the state ports like a real data owner.
    /// It never touches the HUD or the ports directly. Not compiled into release builds.
    /// </summary>
    public sealed class HudDebugPanel : MonoBehaviour
    {
        private const float StepIntervalSeconds = 0.5f;
        private const float StressRateHz = 30f;
        private const float StressDurationSeconds = 60f;

        private MockHudState _hud;
        private MockInventoryState _inventory;
        private MockEffectState _effects;
        private HudMockScenarios _scenarios;

        private bool _visible;
        private Vector2 _scroll;
        private string _hp = "72", _armorDurability = "48", _helmetDurability = "12";
        private string _adrenalineTotal = "8", _adrenalineRemaining = "8";
        private string _magazine = "4", _reserve = "30", _zone = "84", _alive = "7", _total = "10";
        private string _poseX = "25.5", _poseY = "25.5", _poseRotation = "90";
        private int _armorIndex = -1, _helmetIndex = -1, _weaponIndex;
        private Sequence _sequence;
        private float _sequenceTimer, _stressElapsed;
        private int _stressFrame;

        private enum Sequence { None, HpDown, HealUp, Stress }

        [Inject]
        public void Construct(MockHudState hud, MockInventoryState inventory, MockEffectState effects, HudMockScenarios scenarios)
        {
            _hud = hud;
            _inventory = inventory;
            _effects = effects;
            _scenarios = scenarios;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F4)) _visible = !_visible;
            if (_scenarios == null || _sequence == Sequence.None) return;
            if (_sequence == Sequence.Stress)
            {
                _stressElapsed += Time.deltaTime;
                while (_stressFrame < _stressElapsed * StressRateHz && _stressFrame < StressDurationSeconds * StressRateHz)
                    _scenarios.StressStep(_stressFrame++);
                if (_stressFrame >= StressDurationSeconds * StressRateHz) _sequence = Sequence.None;
                return;
            }
            _sequenceTimer -= Time.deltaTime;
            if (_sequenceTimer > 0f) return;
            _sequenceTimer = StepIntervalSeconds;
            var more = _sequence == Sequence.HpDown ? _scenarios.HpStepDown(10) : _scenarios.HealStepUp(10);
            if (!more) _sequence = Sequence.None;
        }

        private void OnGUI()
        {
            if (!_visible || _scenarios == null) return;
            // Right side, under Match status and above the quick bar, so the HUD blocks stay visible while testing.
            var scale = Screen.height / 720f;
            var top = 320f * scale;
            var rect = new Rect(Screen.width - 380, top, 360, Mathf.Max(200f, Screen.height - top - 180f * scale));
            GUILayout.BeginArea(rect, GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("HUD mock (F4) — dev only");

            GUILayout.Label("Preset");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("AC14 = mockup v2")) _scenarios.MockupV2();
            if (GUILayout.Button("Reset all")) ResetAll();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("HP 100→0 /10")) { _hud.SetHp(_hud.MaxHp); BeginSequence(Sequence.HpDown); }
            if (GUILayout.Button("Heal 0→100 /10")) { _hud.SetHp(0); BeginSequence(Sequence.HealUp); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_sequence == Sequence.Stress ? $"Stress {_stressFrame}/1800" : "Stress 30 Hz / 60 s")) StartStress();
            if (GUILayout.Button("Stop")) _sequence = Sequence.None;
            GUILayout.EndHorizontal();

            GUILayout.Label($"HP (max {_hud.MaxHp})");
            GUILayout.BeginHorizontal();
            _hp = GUILayout.TextField(_hp, GUILayout.Width(60));
            if (GUILayout.Button("Set")) _hud.SetHp(ParseInt(_hp, _hud.Hp));
            if (GUILayout.Button("-10")) _hud.Damage(10);
            if (GUILayout.Button("+10")) _hud.Heal(10);
            GUILayout.EndHorizontal();

            GearRow("Armor", _hud.ArmorIds, ref _armorIndex, ref _armorDurability, true);
            GearRow("Helmet", _hud.HelmetIds, ref _helmetIndex, ref _helmetDurability, false);

            GUILayout.Label("Adrenaline (total / remaining s; 8 s is a test value)");
            GUILayout.BeginHorizontal();
            _adrenalineTotal = GUILayout.TextField(_adrenalineTotal, GUILayout.Width(50));
            _adrenalineRemaining = GUILayout.TextField(_adrenalineRemaining, GUILayout.Width(50));
            if (GUILayout.Button("Apply")) _effects.Adrenaline(ParseFloat(_adrenalineTotal, 8f), ParseFloat(_adrenalineRemaining, 8f));
            if (GUILayout.Button("Clear")) _effects.Clear();
            GUILayout.EndHorizontal();

            var weaponCount = _hud.WeaponIds.Count;
            var weaponId = weaponCount > 0 ? _hud.WeaponIds[_weaponIndex % weaponCount] : null;
            var nextWeaponId = weaponCount > 0 ? _hud.WeaponIds[(_weaponIndex + 1) % weaponCount] : null;
            GUILayout.Label("Held weapon + ammo");
            GUILayout.BeginHorizontal();
            // The button names the weapon it will hold; nothing happens when weapons.json is empty.
            if (GUILayout.Button(nextWeaponId == null ? "-" : "Hold " + nextWeaponId) && nextWeaponId != null)
            {
                _weaponIndex = (_weaponIndex + 1) % weaponCount;
                _hud.Hold(nextWeaponId);
            }
            if (GUILayout.Button("Full mag " + (weaponId ?? "-")) && weaponId != null) _hud.FullMagazine(weaponId);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _magazine = GUILayout.TextField(_magazine, GUILayout.Width(50));
            _reserve = GUILayout.TextField(_reserve, GUILayout.Width(50));
            if (GUILayout.Button("Set ammo")) _hud.SetAmmo(ParseInt(_magazine, 0), ParseInt(_reserve, 0));
            GUILayout.EndHorizontal();

            GUILayout.Label("Slots (click = next item, Sel = select, +/- = count)");
            for (var i = 0; i < MockInventoryState.SlotCount; i++)
            {
                GUILayout.BeginHorizontal();
                var id = _inventory.GetId(i) ?? "(empty)";
                var label = i < 4 ? $"{i + 1}: {id}" : $"{i + 1}: {id} ×{_inventory.GetCount(i)}";
                if (GUILayout.Button(label, GUILayout.Width(210))) _inventory.CycleItem(i);
                if (GUILayout.Button(_inventory.ActiveSlot == i ? "[Sel]" : "Sel")) _inventory.Select(i);
                if (i >= 4 && GUILayout.Button("-")) _inventory.AddCount(i, -1);
                if (i >= 4 && GUILayout.Button("+")) _inventory.AddCount(i, 1);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("Match status: zone s / alive / total at start");
            GUILayout.BeginHorizontal();
            _zone = GUILayout.TextField(_zone, GUILayout.Width(50));
            if (GUILayout.Button("Zone")) _hud.Zone(ParseFloat(_zone, 0f));
            _alive = GUILayout.TextField(_alive, GUILayout.Width(35));
            _total = GUILayout.TextField(_total, GUILayout.Width(35));
            if (GUILayout.Button("Alive")) _hud.Alive(ParseInt(_alive, 0), ParseInt(_total, 0));
            GUILayout.EndHorizontal();

            GUILayout.Label("Pose x / y / rotation (only without a Hero)");
            GUILayout.BeginHorizontal();
            _poseX = GUILayout.TextField(_poseX, GUILayout.Width(50));
            _poseY = GUILayout.TextField(_poseY, GUILayout.Width(50));
            _poseRotation = GUILayout.TextField(_poseRotation, GUILayout.Width(50));
            if (GUILayout.Button("Set pose")) _hud.Pose(ParseFloat(_poseX, 0f), ParseFloat(_poseY, 0f), ParseFloat(_poseRotation, 0f));
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void GearRow(string title, System.Collections.Generic.IReadOnlyList<string> ids, ref int index, ref string durability, bool armor)
        {
            var id = index >= 0 && index < ids.Count ? ids[index] : null;
            GUILayout.Label(title);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(id ?? "(none)", GUILayout.Width(140)))
            {
                index = index + 1 >= ids.Count ? -1 : index + 1;
                id = index >= 0 ? ids[index] : null;
                durability = _hud.MaxDurability(id).ToString(CultureInfo.InvariantCulture);
                Equip(armor, id, ParseFloat(durability, 0f));
            }
            durability = GUILayout.TextField(durability, GUILayout.Width(50));
            if (GUILayout.Button("Set")) Equip(armor, id, ParseFloat(durability, 0f));
            GUILayout.EndHorizontal();
        }

        private void Equip(bool armor, string id, float durability)
        {
            if (armor) _hud.EquipArmor(id, durability);
            else _hud.EquipHelmet(id, durability);
        }

        private void BeginSequence(Sequence sequence)
        {
            _sequence = sequence;
            _sequenceTimer = StepIntervalSeconds;
        }

        private void StartStress()
        {
            _sequence = Sequence.Stress;
            _stressElapsed = 0f;
            _stressFrame = 0;
        }

        private void ResetAll()
        {
            _sequence = Sequence.None;
            _hud.Reset();
            _inventory.Reset();
            _effects.Clear();
            _armorIndex = _helmetIndex = -1;
        }

        private static int ParseInt(string text, int fallback) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

        private static float ParseFloat(string text, float fallback) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }
}
#endif
