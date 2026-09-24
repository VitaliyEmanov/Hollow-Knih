using AshenWick.Art;
using AshenWick.World;
using UnityEngine;

namespace AshenWick
{
    public abstract class Prop : MonoBehaviour, IInteractable
    {
        protected Room room;
        public Vector2 Position { get { return (Vector2)transform.position + new Vector2(0f, 0.6f); } }
        public virtual float Radius { get { return 1.4f; } }
        public abstract string Prompt { get; }
        public virtual bool CanInteract { get { return true; } }
        public abstract void Interact();

        protected virtual void Start()
        {
            room = GetComponentInParent<Room>();
            if (room != null) room.Interactables.Add(this);
        }

        protected virtual void OnDestroy()
        {
            if (room != null) room.Interactables.Remove(this);
        }
    }

    /// <summary>Подсвечник — rest here to heal and save.</summary>
    public sealed class Bench : Prop
    {
        public override string Prompt { get { return "Отдохнуть"; } }

        void Awake()
        {
            var sr = Gfx.Part(transform, "candelabra", Gfx.S("Candelabra", ArtLibrary.Candelabra), Vector2.zero, Layer.Props);
            sr.transform.localScale = Vector3.one * 1.1f;
            // three small living flames
            float[] fx = { -0.69f, 0f, 0.69f };
            float[] fy = { 2.3f, 2.28f, 2.22f };
            for (int i = 0; i < 3; i++)
            {
                var f = Gfx.Part(transform, "flame", Gfx.S("NivFlame", ArtLibrary.NivFlame), new Vector2(fx[i], fy[i]), Layer.Props + 1);
                f.transform.localScale = Vector3.one * 0.32f;
                f.gameObject.AddComponent<FlameFlicker>();
                var g = Gfx.Glow(transform, new Vector2(fx[i], fy[i] + 0.2f), 3f, new Color(1f, 0.7f, 0.35f, 0.3f), Layer.Props - 1);
                g.gameObject.AddComponent<Flicker>().Setup(0.3f, 0.08f, i * 2f);
            }
            var big = Gfx.Glow(transform, new Vector2(0, 1.8f), 9f, new Color(1f, 0.65f, 0.3f, 0.15f), Layer.BackProps);
            big.gameObject.AddComponent<Flicker>().Setup(0.15f, 0.04f, 0f);
        }

        public override void Interact()
        {
            var p = Game.I.Player;
            if (p == null) return;
            p.Rest(transform.position);
            Game.I.Hud.Toast("Пламя восстановлено. Путь сохранён.");
        }
    }

    public sealed class FlameFlicker : MonoBehaviour
    {
        Vector3 s0; float ph;
        void Start() { s0 = transform.localScale; ph = Random.value * 10f; }
        void Update()
        {
            float t = Time.time + ph;
            float k = 1f + Mathf.Sin(t * 17f) * 0.07f + Mathf.Sin(t * 31f) * 0.05f;
            transform.localScale = new Vector3(s0.x * (0.94f + Mathf.Sin(t * 23f) * 0.06f), s0.y * k, 1f);
            transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 3f) * 5f);
        }
    }

    /// <summary>Stone tablet with lore text.</summary>
    public sealed class Tablet : Prop
    {
        public string Text = "";
        public override string Prompt { get { return "Прочитать"; } }

        void Awake()
        {
            Gfx.Part(transform, "tablet", Gfx.S("Tablet", ArtLibrary.Tablet), Vector2.zero, Layer.Props);
            var g = Gfx.Glow(transform, new Vector2(0, 0.8f), 2.5f, new Color(1f, 0.8f, 0.5f, 0.12f), Layer.Props - 1);
            g.gameObject.AddComponent<Flicker>().Setup(0.12f, 0.05f, 1f);
        }

        public override void Interact()
        {
            Game.I.Sound.Play("ui", 0.5f);
            Game.I.StartDialogue("", new[] { Text });
        }
    }

    /// <summary>Talking NPC (Ilva the Candlemother, Prakh the Chronicler).</summary>
    public sealed class Npc : Prop
    {
        string id;
        Lore.Speech speech;
        Transform visual;
        float t;

        public override string Prompt { get { return "Говорить"; } }
        public override float Radius { get { return 2.2f; } }

        public void Setup(string npcId)
        {
            id = npcId;
            speech = Lore.Npc(id);
            Sprite s = id == "prakh" ? Gfx.S("Chronicler", ArtLibrary.Chronicler) : Gfx.S("Candlemother", ArtLibrary.Candlemother);
            var sr = Gfx.Part(transform, "npc", s, Vector2.zero, Layer.Props + 2);
            visual = sr.transform;
            visual.localScale = Vector3.one * (id == "prakh" ? 1.05f : 1.15f);
            if (id == "ilva")
            {
                var g = Gfx.Glow(transform, new Vector2(-0.1f, 0.9f), 4f, new Color(1f, 0.6f, 0.3f, 0.3f), Layer.Props + 1);
                g.gameObject.AddComponent<Flicker>().Setup(0.3f, 0.08f, 0.5f);
                var f = Gfx.Glow(transform, new Vector2(-0.02f, 3.3f), 1.6f, new Color(1f, 0.7f, 0.3f, 0.5f), Layer.Props + 3);
                f.gameObject.AddComponent<Flicker>().Setup(0.5f, 0.15f, 0f);
            }
        }

        void Update()
        {
            t += Time.deltaTime;
            if (visual == null) return;
            visual.localScale = new Vector3(visual.localScale.x, visual.localScale.x * (1f + Mathf.Sin(t * 1.6f) * 0.012f), 1f);
            // face Niv
            var p = Game.I != null ? Game.I.Player : null;
            if (p != null)
            {
                float dir = Mathf.Sign(p.transform.position.x - transform.position.x);
                var sc = visual.localScale;
                visual.localScale = new Vector3(Mathf.Abs(sc.x) * (dir < 0 ? -1f : 1f) * (id == "prakh" ? 1f : 1f), sc.y, 1f);
            }
        }

        public override void Interact()
        {
            if (speech == null) return;
            var save = Game.I.Save;
            bool first = !save.talked.Contains(id);
            Game.I.Sound.Play("ui", 0.5f);
            Game.I.StartDialogue(speech.Name, first ? speech.Lines : speech.Repeat, () =>
            {
                if (first)
                {
                    save.talked.Add(id);
                    if (id == "ilva") Game.I.ShowPanel(Lore.Ability("heal"));
                }
            });
        }
    }

    /// <summary>Altar that grants an ability (the Ember Dash in the Scar).</summary>
    public sealed class Altar : Prop
    {
        string ability;
        Transform relic;
        SpriteRenderer relicGlow;
        float t;

        public override string Prompt { get { return "Коснуться"; } }
        public override bool CanInteract { get { return relic != null; } }

        public void Setup(string abilityId)
        {
            ability = abilityId;
            Gfx.Part(transform, "altar", Gfx.S("Altar", ArtLibrary.Altar), Vector2.zero, Layer.Props);
            if (!Owned())
            {
                var sr = Gfx.Part(transform, "relic", Gfx.S("Relic", ArtLibrary.Relic), new Vector2(0, 2.1f), Layer.Props + 2);
                relic = sr.transform;
                relicGlow = Gfx.Glow(relic, Vector2.zero, 5f, new Color(1f, 0.6f, 0.3f, 0.35f), Layer.Props + 1);
            }
        }

        bool Owned()
        {
            var s = Game.I.Save;
            return (ability == "dash" && s.hasDash) || (ability == "spell" && s.hasSpell) || (ability == "doublejump" && s.hasDoubleJump);
        }

        void Update()
        {
            t += Time.deltaTime;
            if (relic != null)
            {
                relic.localPosition = new Vector3(0, 2.1f + Mathf.Sin(t * 2f) * 0.12f, 0);
                if (Random.value < 0.2f) Game.I.Fx.Ember(relic.position);
                if (relicGlow != null) relicGlow.color = new Color(1f, 0.6f, 0.3f, 0.3f + Mathf.Sin(t * 3f) * 0.08f);
            }
        }

        public override void Interact()
        {
            if (relic == null) return;
            Game.I.Fx.FireBurst(relic.position, 1.2f, 24);
            Destroy(relic.gameObject);
            relic = null;
            Game.I.GrantAbility(ability);
        }
    }

    /// <summary>Floating wax shard (+1 max flame). Collected on touch.</summary>
    public sealed class WaxShardPickup : MonoBehaviour
    {
        public string Id;
        Transform vis;
        float t;

        void Awake()
        {
            var sr = Gfx.Part(transform, "shard", Gfx.S("WaxShard", ArtLibrary.WaxShard), Vector2.zero, Layer.Props + 2);
            vis = sr.transform;
            Gfx.Glow(transform, Vector2.zero, 3.5f, new Color(1f, 0.85f, 0.6f, 0.3f), Layer.Props + 1).gameObject.AddComponent<Flicker>().Setup(0.3f, 0.1f, 0);
        }

        void Update()
        {
            t += Time.deltaTime;
            vis.localPosition = new Vector3(0, Mathf.Sin(t * 2.4f) * 0.15f, 0);
            vis.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f) * 8f);
            var g = Game.I;
            if (g == null || g.Player == null || !g.Live) return;
            if (Vector2.Distance(g.Player.Center, transform.position) < 0.9f)
            {
                if (!g.Save.shards.Contains(Id)) g.Save.shards.Add(Id);
                g.Player.AddMaxFlame();
                g.Save.maxFlames = g.Player.MaxFlames;
                g.Save.Write();
                g.Fx.Motes(transform.position, new Color(1f, 0.9f, 0.7f, 1f), 24, 0.8f);
                g.Fx.RingPulse(transform.position, new Color(1f, 0.9f, 0.7f, 1f), 0.3f, 4f, 0.5f);
                g.ShowPanel(Lore.Ability("shard"));
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Relic left by a defeated boss: grants its power on touch.</summary>
    public sealed class AbilityPickup : MonoBehaviour
    {
        public string Ability;
        Transform vis;
        float t;
        Vector2 target;

        public static AbilityPickup Spawn(Room room, Vector2 pos, string ability)
        {
            var p = room.Spawn<AbilityPickup>(pos);
            p.Ability = ability;
            p.target = new Vector2(pos.x, room.FloorBelow(pos.x, pos.y) + 1.4f);
            return p;
        }

        void Awake()
        {
            var sr = Gfx.Part(transform, "relic", Gfx.S("Relic", ArtLibrary.Relic), Vector2.zero, Layer.Props + 3);
            vis = sr.transform;
            vis.localScale = Vector3.one * 1.4f;
            Gfx.Glow(transform, Vector2.zero, 6f, new Color(1f, 0.6f, 0.3f, 0.35f), Layer.Props + 2).gameObject.AddComponent<Flicker>().Setup(0.35f, 0.1f, 0);
        }

        void Update()
        {
            t += Time.deltaTime;
            transform.position = Vector2.Lerp(transform.position, target, 1f - Mathf.Exp(-2f * Time.deltaTime));
            vis.localPosition = new Vector3(0, Mathf.Sin(t * 2f) * 0.15f, 0);
            var g = Game.I;
            if (Random.value < 0.3f) g.Fx.Ember((Vector2)transform.position + Random.insideUnitCircle * 0.4f);
            if (g.Player == null || !g.Live || t < 1.2f) return;
            if (Vector2.Distance(g.Player.Center, transform.position) < 1.1f)
            {
                g.Fx.FireBurst(transform.position, 1.4f, 30);
                g.GrantAbility(Ability);
                Destroy(gameObject);
            }
        }
    }
}
