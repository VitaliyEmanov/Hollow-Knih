using System.Collections.Generic;
using AshenWick.Art;
using AshenWick.World;
using UnityEngine;

namespace AshenWick
{
    /// <summary>Something Niv can use with "Up": benches, tablets, NPCs, altars.</summary>
    public interface IInteractable
    {
        Vector2 Position { get; }
        float Radius { get; }
        string Prompt { get; }
        bool CanInteract { get; }
        void Interact();
    }

    /// <summary>A playable room, built at runtime from a <see cref="RoomDef"/>.</summary>
    public sealed class Room : MonoBehaviour
    {
        public const int Air = 0, Solid = 1, Platform = 2, Spikes = 3, Lava = 4;

        public RoomDef Def { get; private set; }
        public Theme Theme { get; private set; }
        public int W { get; private set; }
        public int H { get; private set; }
        public Rect Bounds { get { return new Rect(0, 0, W, H); } }
        public Vector2 BenchPos { get; private set; }
        public Vector2 StartPos { get; private set; }
        public readonly List<IInteractable> Interactables = new List<IInteractable>();
        public Boss ActiveBoss { get; private set; }
        public bool GatesLocked { get; private set; }

        int[,] kind;
        readonly List<Vector2Int> gateCells = new List<Vector2Int>();
        readonly List<GameObject> gateVisuals = new List<GameObject>();
        readonly Dictionary<char, RectInt> doors = new Dictionary<char, RectInt>();
        readonly List<Vector2> lavaSurface = new List<Vector2>();
        Boss dormantBoss;
        bool bossStarted;
        SaveData save;
        Parallax parallax;
        float ambientTimer;

        public int KindAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return Solid;
            return kind[x, y];
        }

        public bool IsSolid(Vector2 p) { return KindAt(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)) == Solid; }

        public bool IsGroundAt(Vector2 p)
        {
            int k = KindAt(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
            return k == Solid || k == Platform;
        }

        /// <summary>Finds the floor surface height below a point (for spawning hazards on the ground).</summary>
        public float FloorBelow(float x, float fromY, bool solidOnly = false)
        {
            int cx = Mathf.FloorToInt(x);
            for (int y = Mathf.Min(H - 1, Mathf.FloorToInt(fromY)); y >= 0; y--)
            {
                int k = KindAt(cx, y);
                if (k == Solid || (k == Platform && !solidOnly)) return y + 1;
            }
            return 0;
        }

        public float CeilingAbove(float x, float fromY)
        {
            int cx = Mathf.FloorToInt(x);
            for (int y = Mathf.Max(0, Mathf.FloorToInt(fromY)); y < H; y++)
                if (KindAt(cx, y) == Solid) return y;
            return H;
        }

        // ------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------

        public void Build(RoomDef def, SaveData saveData)
        {
            Def = def;
            save = saveData;
            W = def.W; H = def.H;
            Theme = Theme.For(def.Area);
            kind = new int[W, H];
            int tabletIndex = 0;
            int shardIndex = 0;
            var doorMin = new Dictionary<char, Vector2Int>();
            var doorMax = new Dictionary<char, Vector2Int>();

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char ch = def.At(x, y);
                    int k = Air;
                    switch (ch)
                    {
                        case '#': k = Solid; break;
                        case '=': k = Platform; break;
                        case '^': k = Spikes; break;
                        case '~': k = Lava; break;
                        case 'G': gateCells.Add(new Vector2Int(x, y)); break;
                    }
                    kind[x, y] = k;
                    if (ch >= '1' && ch <= '9')
                    {
                        if (!doorMin.ContainsKey(ch)) { doorMin[ch] = new Vector2Int(x, y); doorMax[ch] = new Vector2Int(x, y); }
                        doorMin[ch] = Vector2Int.Min(doorMin[ch], new Vector2Int(x, y));
                        doorMax[ch] = Vector2Int.Max(doorMax[ch], new Vector2Int(x, y));
                    }
                }
            foreach (var kv in doorMin)
            {
                var mx = doorMax[kv.Key];
                doors[kv.Key] = new RectInt(kv.Value.x, kv.Value.y, mx.x - kv.Value.x + 1, mx.y - kv.Value.y + 1);
            }

            BuildTerrain();
            parallax = gameObject.AddComponent<Parallax>();
            parallax.Build(def.Area, this);
            BuildDecor();

            // spawn markers
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char ch = def.At(x, y);
                    var feet = new Vector2(x + 0.5f, y); // standing on the bottom of the cell
                    switch (ch)
                    {
                        case 'P': StartPos = feet; break;
                        case 'B': BenchPos = feet; Spawn<Bench>(feet); break;
                        case 'T':
                            {
                                var t = Spawn<Tablet>(feet);
                                t.Text = tabletIndex < def.Tablets.Length ? def.Tablets[tabletIndex] : "…";
                                tabletIndex++;
                                break;
                            }
                        case 'N': Spawn<Npc>(feet).Setup(def.NpcId); break;
                        case 'A': Spawn<Altar>(feet).Setup(def.Ability); break;
                        case 'S':
                            {
                                string id = def.Id + "#" + shardIndex++;
                                if (!save.HasShard(id)) Spawn<WaxShardPickup>(feet + new Vector2(0, 0.6f)).Id = id;
                                break;
                            }
                        case 'c': Spawn<AshCrawler>(feet); break;
                        case 'm': Spawn<SootMoth>(feet + new Vector2(0, 0.5f)); break;
                        case 'h': Spawn<CinderHusk>(feet); break;
                        case 's': Spawn<AshSpitter>(feet); break;
                        case 'w': Spawn<EmberWisp>(feet + new Vector2(0, 0.5f)); break;
                        case 'K':
                            if (!string.IsNullOrEmpty(def.Boss) && !save.BossDead(def.Boss))
                            {
                                dormantBoss = Boss.Create(def.Boss, feet, this);
                            }
                            break;
                    }
                }

            // gates start open
            SetGates(false);
        }

        /// <summary>Tile kind encoded by a map character (gates are drawn as open ground).</summary>
        public static int KindOf(char ch)
        {
            switch (ch)
            {
                case '#': return Solid;
                case '=': return Platform;
                case '^': return Spikes;
                case '~': return Lava;
            }
            return Air;
        }

        /// <summary>Paints a room's terrain (pure, safe to call from a worker thread).</summary>
        public static ArtCanvas TerrainFor(RoomDef def)
        {
            return ArtLibrary.Terrain(def.W, def.H, (x, y) => KindOf(def.At(x, y)), Theme.For(def.Area));
        }

        public T Spawn<T>(Vector2 pos) where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            return go.AddComponent<T>();
        }

        void BuildTerrain()
        {
            var def = Def;
            var canvas = ArtCache.Get("Terrain_" + def.Id, () => TerrainFor(def));
            var sprite = SpriteBank.ToSprite(canvas);
            var sr = Gfx.Part(transform, "Terrain", sprite, Vector2.zero, Layer.Terrain);

            // lava surface glow + list for ember particles
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                    if (kind[x, y] == Lava && (y + 1 >= H || kind[x, y + 1] != Lava))
                    {
                        lavaSurface.Add(new Vector2(x + 0.5f, y + 0.8f));
                        var g = Gfx.Glow(transform, new Vector2(x + 0.5f, y + 1f), 2.2f, new Color(1f, 0.45f, 0.15f, 0.35f), Layer.Terrain + 1);
                        g.gameObject.AddComponent<Flicker>().Setup(0.35f, 0.12f, x * 1.7f);
                    }
        }

        void BuildDecor()
        {
            // hanging chains in front of the scene, and dim pillars of light
            var rng = new System.Random(Def.Id.GetHashCode());
            int chains = W / 18;
            for (int i = 0; i < chains; i++)
            {
                float x = 4 + (float)rng.NextDouble() * (W - 8);
                float top = CeilingAbove(x, H * 0.5f);
                if (top >= H) continue;
                var sr = Gfx.Part(transform, "chain", Gfx.S("Chain", ArtLibrary.Chain), new Vector2(x, top), Layer.Foreground);
                sr.color = new Color(0.04f, 0.04f, 0.06f, 1f);
                float len = 0.3f + (float)rng.NextDouble() * 0.4f;
                sr.transform.localScale = new Vector3(1f, len, 1f);
                sr.gameObject.AddComponent<Sway>().Setup(2f + (float)rng.NextDouble() * 3f, 0.4f + (float)rng.NextDouble());
            }

            // light shafts in the cathedral and fields
            if (Def.Area == 0 || Def.Area == 2)
            {
                for (int i = 0; i < W / 22; i++)
                {
                    float x = 6 + (float)rng.NextDouble() * (W - 12);
                    var sr = Gfx.Part(transform, "shaft", Gfx.S("FirePillar", ArtLibrary.FirePillar), new Vector2(x, 0), Layer.BackProps, true);
                    sr.color = Def.Area == 2 ? new Color(0.45f, 0.5f, 0.9f, 0.07f) : new Color(1f, 0.85f, 0.7f, 0.05f);
                    sr.transform.localScale = new Vector3(2.4f, H / 4f * 1.1f, 1f);
                    sr.transform.rotation = Quaternion.Euler(0, 0, -12f);
                    sr.transform.position = new Vector3(x, H, 0f);
                    sr.transform.localScale = new Vector3(2.4f, -H / 4f * 1.2f, 1f);
                }
            }

            // gate visuals
            foreach (var c in gateCells)
            {
                var sr = Gfx.Part(transform, "gate", Gfx.S("GateBar", ArtLibrary.GateBar), new Vector2(c.x + 0.5f, c.y), Layer.Props);
                sr.transform.localScale = new Vector3(1.2f, 0.25f, 1f);
                gateVisuals.Add(sr.gameObject);
            }
        }

        // ------------------------------------------------------------------
        // Runtime
        // ------------------------------------------------------------------

        public void SetGates(bool locked)
        {
            GatesLocked = locked;
            foreach (var c in gateCells) kind[c.x, c.y] = locked ? Solid : Air;
            foreach (var g in gateVisuals) if (g != null) g.SetActive(locked);
        }

        public void OnPlayerEntered()
        {
            var g = Game.I;
            string areaKey = "area" + Def.Area;
            if (!g.Save.seenAreas.Contains(areaKey))
            {
                g.Save.seenAreas.Add(areaKey);
                g.Hud.ShowAreaTitle(Areas.Names[Def.Area], Areas.Subtitles[Def.Area]);
            }
        }

        public bool DoorArrival(char door, out Vector2 pos, out Vector2 vel, out int walkDir)
        {
            vel = Vector2.zero;
            walkDir = 0;
            RectInt d;
            if (!doors.TryGetValue(door, out d))
            {
                pos = StartPos + new Vector2(0, 0.62f);
                return false;
            }
            if (d.xMin == 0)
            {
                pos = new Vector2(1.9f, d.yMin + 0.62f);
                walkDir = 1;
            }
            else if (d.xMax >= W)
            {
                pos = new Vector2(W - 1.9f, d.yMin + 0.62f);
                walkDir = -1;
            }
            else if (d.yMin == 0)
            {
                float cx = d.xMin + d.width * 0.5f;
                pos = new Vector2(cx, 1.4f);
                vel = new Vector2(0f, 21f);
                walkDir = cx < W * 0.5f ? 1 : -1;
            }
            else
            {
                pos = new Vector2(d.xMin + d.width * 0.5f, H - 1.8f);
                vel = new Vector2(0f, -2f);
            }
            return true;
        }

        /// <summary>Returns the door the rect overlaps, if any.</summary>
        public bool DoorAt(Rect r, out DoorLink link)
        {
            foreach (var kv in doors)
            {
                var d = kv.Value;
                var dr = new Rect(d.xMin, d.yMin, d.width, d.height);
                // doors on the vertical edges trigger when the body touches the outermost column
                if (dr.Overlaps(r) && Def.Doors.TryGetValue(kv.Key, out link)) return true;
            }
            link = default(DoorLink);
            return false;
        }

        /// <summary>Is the rect touching thorns or molten wax?</summary>
        public bool Hazard(Rect r)
        {
            int x0 = Mathf.FloorToInt(r.xMin), x1 = Mathf.FloorToInt(r.xMax);
            int y0 = Mathf.FloorToInt(r.yMin), y1 = Mathf.FloorToInt(r.yMax);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int k = KindAt(x, y);
                    if (k == Spikes && new Rect(x + 0.12f, y, 0.76f, 0.55f).Overlaps(r)) return true;
                    if (k == Lava && new Rect(x, y, 1f, 0.7f).Overlaps(r)) return true;
                }
            return false;
        }

        /// <summary>Is the point on spikes (for pogo off thorns)?</summary>
        public bool SpikeAt(Rect r)
        {
            int x0 = Mathf.FloorToInt(r.xMin), x1 = Mathf.FloorToInt(r.xMax);
            int y0 = Mathf.FloorToInt(r.yMin), y1 = Mathf.FloorToInt(r.yMax);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (KindAt(x, y) == Spikes) return true;
            return false;
        }

        void Update()
        {
            var g = Game.I;
            if (g == null) return;
            var p = g.Player;

            // boss trigger
            if (dormantBoss != null && !bossStarted && p != null && g.Live && p.transform.position.x > Def.BossTriggerX)
            {
                bossStarted = true;
                ActiveBoss = dormantBoss;
                SetGates(true);
                g.Sound.Play("gate", 0.9f);
                g.Cam.Shake(0.3f);
                ActiveBoss.BeginFight();
            }

            // ambient particles: falling ash everywhere, rising embers over molten wax
            ambientTimer -= Time.deltaTime;
            if (ambientTimer <= 0f)
            {
                ambientTimer = 0.05f;
                var cam = g.Cam.View;
                g.Fx.AmbientAsh(new Vector2(Random.Range(cam.xMin - 2, cam.xMax + 2), cam.yMax + 1f), Def.Area);
                if (lavaSurface.Count > 0 && Random.value < 0.6f)
                {
                    var lp = lavaSurface[Random.Range(0, lavaSurface.Count)];
                    if (cam.Contains(lp)) g.Fx.Ember(lp);
                }
                if (Def.Area == 3 && Random.value < 0.5f)
                    g.Fx.Ember(new Vector2(Random.Range(cam.xMin, cam.xMax), cam.yMin - 0.5f));
            }
        }

        public void OnBossDefeated(Boss boss)
        {
            ActiveBoss = null;
            dormantBoss = null;
            if (boss.Id != "king") SetGates(false);
        }
    }

    /// <summary>Parallax background: sky + three silhouette layers per area.</summary>
    public sealed class Parallax : MonoBehaviour
    {
        struct LayerSet { public Transform[] Copies; public float Factor; public float Width; public float YOffset; }

        readonly List<LayerSet> layers = new List<LayerSet>();
        SpriteRenderer sky;
        Room room;

        public void Build(int area, Room r)
        {
            room = r;
            var root = new GameObject("Parallax").transform;
            root.SetParent(transform, false);
            sky = Gfx.Part(root, "sky", Gfx.S("Sky" + area, () => ArtLibrary.Sky(area)), Vector2.zero, Layer.Sky);

            float[] factors = { 0.88f, 0.7f, 0.45f };
            int[] orders = { Layer.Far, Layer.Mid, Layer.Near };
            float[] yOff = { 2f, 0f, -1f };
            for (int d = 0; d < 3; d++)
            {
                int depth = d;
                var sprite = Gfx.S("Backdrop" + area + "_" + d, () => ArtLibrary.Backdrop(area, depth));
                var set = new LayerSet { Copies = new Transform[3], Factor = factors[d], Width = sprite.bounds.size.x, YOffset = yOff[d] };
                for (int k = 0; k < 3; k++)
                {
                    var sr = Gfx.Part(root, "bg" + d + "_" + k, sprite, Vector2.zero, orders[d]);
                    set.Copies[k] = sr.transform;
                }
                layers.Add(set);
            }
        }

        void LateUpdate()
        {
            var g = Game.I;
            if (g == null || g.Cam == null) return;
            var cam = g.Cam.transform.position;
            var view = g.Cam.View;
            // sky fills the view
            sky.transform.position = new Vector3(cam.x, cam.y, 0f);
            var sb = sky.sprite.bounds.size;
            sky.transform.localScale = new Vector3(view.width / sb.x * 1.05f, view.height / sb.y * 1.05f, 1f);

            foreach (var l in layers)
            {
                float ox = cam.x * l.Factor;
                float bottomAnchor = 0f;
                float y = Mathf.Lerp(bottomAnchor, view.yMin, l.Factor) + l.YOffset - (1f - l.Factor) * 2f;
                float baseX = ox + l.Width * Mathf.Round((cam.x - ox) / l.Width);
                for (int k = 0; k < 3; k++)
                    l.Copies[k].position = new Vector3(baseX + (k - 1) * l.Width, y, 0f);
            }
        }
    }

    /// <summary>Gentle alpha flicker (fire glows).</summary>
    public sealed class Flicker : MonoBehaviour
    {
        SpriteRenderer sr;
        float baseA, amp, phase;
        Vector3 baseScale;

        public void Setup(float alpha, float amplitude, float ph)
        {
            sr = GetComponent<SpriteRenderer>();
            baseA = alpha; amp = amplitude; phase = ph;
            baseScale = transform.localScale;
        }

        void Update()
        {
            if (sr == null) return;
            float t = Time.time * 3f + phase;
            float n = Mathf.Sin(t) * 0.5f + Mathf.Sin(t * 2.7f + 1.3f) * 0.3f + Mathf.Sin(t * 5.1f) * 0.2f;
            var c = sr.color; c.a = baseA + n * amp; sr.color = c;
            transform.localScale = baseScale * (1f + n * 0.06f);
        }
    }

    /// <summary>Slow pendulum sway for hanging decor.</summary>
    public sealed class Sway : MonoBehaviour
    {
        float amp, speed, phase;
        public void Setup(float degrees, float spd) { amp = degrees; speed = spd; phase = Random.value * 10f; }
        void Update() { transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * speed + phase) * amp); }
    }
}
