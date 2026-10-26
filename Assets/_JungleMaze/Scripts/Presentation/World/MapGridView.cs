using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using UnityEngine;

namespace JungleMaze.Presentation.World
{
    /// <summary>
    /// ENV-01: one tiled renderer, 1 world unit/cell, texture PPU from cellSize (48).
    /// Four static solid walls keep the Hero/projectile inside [0,mapSize]. Existing cover prefabs stay editable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapGridView : MonoBehaviour
    {
        [SerializeField] private Sprite groundSprite;
        private IBalanceConfigProvider _balance;
        private Texture2D _texture;
        private Sprite _sprite;
        public int MapSizeCells { get; private set; }
        public int CellSizePixels { get; private set; }

        [Inject]
        public void Construct(IBalanceConfigProvider balance) => _balance = balance;

        private void Start()
        {
            Build(_balance != null ? _balance.Match.MapSizeCells : 50,
                _balance != null ? _balance.Match.CellSizePixels : 48);
        }

        public void Build(int cells, int pixelsPerCell)
        {
            if (MapSizeCells != 0) return; // Build once; no per-frame creation or 2500 individual cell objects.
            if (cells <= 0 || pixelsPerCell < 2) throw new System.ArgumentOutOfRangeException();
            var layer = LayerMask.NameToLayer("HardCover");
            if (layer < 0) throw new System.InvalidOperationException("HardCover layer is required.");
            MapSizeCells = cells;
            CellSizePixels = pixelsPerCell;
            _texture = new Texture2D(pixelsPerCell, pixelsPerCell, TextureFormat.RGBA32, false)
                { name = "Map grid cell", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var colors = new Color32[pixelsPerCell * pixelsPerCell];
            for (var y = 0; y < pixelsPerCell; y++)
            for (var x = 0; x < pixelsPerCell; x++)
                colors[y * pixelsPerCell + x] = x == 0 || y == 0
                    ? new Color32(39, 64, 48, 255) : new Color32(49, 79, 59, 255);
            _texture.SetPixels32(colors);
            _texture.Apply(false, true);
            _sprite = Sprite.Create(_texture, new Rect(0, 0, pixelsPerCell, pixelsPerCell),
                Vector2.one * .5f, pixelsPerCell, 0, SpriteMeshType.FullRect);
            var floor = new GameObject("Grid floor");
            floor.transform.SetParent(transform, false);
            floor.transform.localPosition = new Vector3(cells / 2f, cells / 2f, 0);
            var renderer = floor.AddComponent<SpriteRenderer>();
            renderer.sprite = groundSprite != null ? groundSprite : _sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(cells, cells);
            renderer.sortingOrder = -100;

            // Inner faces exactly at 0 and cells. Radius-aware collision keeps the entire Hero inside.
            CreateWall("West boundary", new Vector2(-.5f, cells / 2f), new Vector2(1, cells + 2), layer);
            CreateWall("East boundary", new Vector2(cells + .5f, cells / 2f), new Vector2(1, cells + 2), layer);
            CreateWall("South boundary", new Vector2(cells / 2f, -.5f), new Vector2(cells, 1), layer);
            CreateWall("North boundary", new Vector2(cells / 2f, cells + .5f), new Vector2(cells, 1), layer);
        }

        private void CreateWall(string wallName, Vector2 position, Vector2 size, int layer)
        {
            var wall = new GameObject(wallName) { layer = layer };
            wall.transform.SetParent(transform, false);
            wall.transform.localPosition = position;
            wall.AddComponent<BoxCollider2D>().size = size;
            var renderer = wall.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = size;
            renderer.color = new Color(.7f, .65f, .5f);
        }

        private void OnDestroy()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }
    }
}
