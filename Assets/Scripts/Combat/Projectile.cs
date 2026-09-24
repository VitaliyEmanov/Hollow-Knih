using System.Collections.Generic;
using AshenWick.Art;
using UnityEngine;

namespace AshenWick
{
    /// <summary>Generic enemy projectile (ash globs, feathers, ember orbs, spears, coals).</summary>
    public class Projectile : MonoBehaviour
    {
        public Vector2 Vel;
        public float Gravity;
        public float Radius = 0.3f;
        public int Damage = 1;
        public float Life = 6f;
        public bool Slashable = true;
        public bool DieOnTerrain = true;
        public bool FaceVelocity = true;
        public bool Homing;
        public float HomingTurn = 2f;
        public float Spin;
        public System.Action<Projectile> OnDeath;
        protected SpriteRenderer sr;
        protected Room room;
        float age;
        bool dead;

        public Rect Rect { get { return Combat.RectAt(transform.position, Vector2.one * Radius * 2f); } }

        public static Projectile Spawn(Room room, string spriteKey, System.Func<ArtCanvas> painter, Vector2 pos, Vector2 vel, float radius, float scale = 1f, bool additive = false)
        {
            var go = new GameObject("proj_" + spriteKey);
            go.transform.SetParent(room.transform, false);
            go.transform.position = pos;
            var p = go.AddComponent<Projectile>();
            p.room = room;
            p.Vel = vel;
            p.Radius = radius;
            p.sr = Gfx.Part(go.transform, "vis", Gfx.S(spriteKey, painter), Vector2.zero, Layer.Projectiles, additive);
            p.sr.transform.localScale = Vector3.one * scale;
            Combat.Projectiles.Add(p);
            return p;
        }

        public void AddGlow(Color c, float size)
        {
            var g = Gfx.Glow(transform, Vector2.zero, size, c, Layer.Projectiles - 1);
            g.gameObject.AddComponent<Flicker>().Setup(c.a, c.a * 0.3f, Random.value * 5f);
        }

        protected virtual void Update()
        {
            if (dead) return;
            float dt = Time.deltaTime;
            age += dt;
            if (Homing && Game.I.Player != null)
            {
                Vector2 to = (Game.I.Player.Center - (Vector2)transform.position).normalized;
                float spd = Vel.magnitude;
                Vel = Vector3.RotateTowards(Vel, to * spd, HomingTurn * dt, 0f);
            }
            Vel.y -= Gravity * dt;
            transform.position += (Vector3)(Vel * dt);
            if (FaceVelocity && Vel.sqrMagnitude > 0.01f)
                sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(Vel.y, Vel.x) * Mathf.Rad2Deg);
            else if (Spin != 0f) sr.transform.Rotate(0, 0, Spin * dt);

            if (Combat.HurtPlayerCircle(transform.position, Radius * 0.8f, Damage)) { Kill(true); return; }
            if (age > Life) { Kill(false); return; }
            if (DieOnTerrain && room != null && room.IsSolid(transform.position)) { Kill(true); return; }
        }

        public void Deflect()
        {
            Game.I.Fx.HitBurst(transform.position, -Vel.normalized, 5, 0.6f);
            Kill(false);
        }

        public void Kill(bool impact)
        {
            if (dead) return;
            dead = true;
            Combat.Projectiles.Remove(this);
            if (impact) Game.I.Fx.FireBurst(transform.position, 0.4f, 6);
            if (OnDeath != null) OnDeath(this);
            Destroy(gameObject);
        }

        void OnDestroy() { Combat.Projectiles.Remove(this); }
    }

    /// <summary>Niv's spell "Вспышка": a rolling ball of candle fire that pierces enemies.</summary>
    public sealed class FlareBolt : MonoBehaviour
    {
        float dir, life = 1.1f;
        int damage;
        readonly HashSet<IHittable> hit = new HashSet<IHittable>();
        SpriteRenderer sr;
        float trail;

        public static FlareBolt Spawn(Vector2 pos, float dir, int dmg)
        {
            var go = new GameObject("Flare");
            go.transform.position = pos;
            var f = go.AddComponent<FlareBolt>();
            f.dir = dir; f.damage = dmg;
            f.sr = Gfx.Part(go.transform, "vis", Gfx.S("FlareBolt", ArtLibrary.FlareBolt), Vector2.zero, Layer.Projectiles, true);
            f.sr.transform.localScale = new Vector3(dir * 1.2f, 1.2f, 1f);
            Gfx.Glow(go.transform, Vector2.zero, 5f, new Color(1f, 0.6f, 0.2f, 0.5f), Layer.Projectiles - 1);
            return f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            transform.position += new Vector3(dir * 21f * dt, 0, 0);
            var room = Game.I.Room;
            trail -= dt;
            if (trail <= 0f) { trail = 0.02f; Game.I.Fx.Ember((Vector2)transform.position + Random.insideUnitCircle * 0.3f); }
            var area = Combat.RectAt(transform.position, new Vector2(1.4f, 1.1f));
            foreach (var t in new List<IHittable>(Combat.Targets))
            {
                if (t == null || hit.Contains(t) || !t.Vulnerable || !t.Hurtbox.Overlaps(area)) continue;
                hit.Add(t);
                if (t.TakeHit(new Hit { Damage = damage, Dir = new Vector2(dir, 0), Point = transform.position, Spell = true }))
                {
                    Game.I.Fx.FireBurst(transform.position, 0.8f, 10);
                    Game.I.Sound.Play("hit", 0.7f);
                    Game.I.HitStop(0.04f);
                }
            }
            foreach (var p in new List<Projectile>(Combat.Projectiles))
                if (p != null && p.Slashable && area.Overlaps(p.Rect)) p.Deflect();

            if (life <= 0f || (room != null && room.IsSolid((Vector2)transform.position + new Vector2(dir * 0.4f, 0))))
            {
                Game.I.Fx.FireBurst(transform.position, 1f, 14);
                Game.I.Sound.Play("fire", 0.5f);
                Destroy(gameObject);
            }
        }
    }

    // ======================================================================
    // Hazards spawned by bosses
    // ======================================================================

    /// <summary>A flame wave that runs along the floor until it hits a wall.</summary>
    public sealed class Shockwave : MonoBehaviour
    {
        float dir, speed, life;
        SpriteRenderer sr;
        float t;

        public static Shockwave Spawn(Room room, Vector2 floorPos, float dir, float speed = 12f, float scale = 1.2f)
        {
            var s = room.Spawn<Shockwave>(floorPos);
            s.dir = dir; s.speed = speed; s.life = 4f;
            s.sr = Gfx.Part(s.transform, "vis", Gfx.S("Shockwave", ArtLibrary.Shockwave), Vector2.zero, Layer.Projectiles, true);
            s.sr.transform.localScale = new Vector3(scale, scale, 1f);
            Gfx.Glow(s.transform, new Vector2(0, 0.4f), 3f, new Color(1f, 0.5f, 0.2f, 0.4f), Layer.Projectiles - 1);
            return s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt; life -= dt;
            transform.position += new Vector3(dir * speed * dt, 0, 0);
            sr.transform.localScale = new Vector3(sr.transform.localScale.x, 1.2f + Mathf.Sin(t * 30f) * 0.12f, 1f);
            if (Random.value < 0.5f) Game.I.Fx.Ember((Vector2)transform.position + new Vector2(Random.Range(-0.3f, 0.3f), 0.2f));
            Combat.HurtPlayer(Combat.RectAt((Vector2)transform.position + new Vector2(0, 0.55f), new Vector2(0.8f, 1.1f)), 1, transform.position);
            var room = Game.I.Room;
            bool wall = room != null && room.IsSolid((Vector2)transform.position + new Vector2(dir * 0.5f, 0.3f));
            bool noFloor = room != null && !room.IsGroundAt((Vector2)transform.position + new Vector2(dir * 0.5f, -0.2f));
            if (life <= 0f || wall || noFloor)
            {
                Game.I.Fx.FireBurst((Vector2)transform.position + new Vector2(0, 0.4f), 0.5f, 6);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Telegraphed column of fire bursting from the floor (vents, wax pillars).</summary>
    public sealed class FireColumn : MonoBehaviour
    {
        float warn, active, height, width, t;
        SpriteRenderer pillar, marker;
        bool fired;

        public static FireColumn Spawn(Room room, Vector2 floorPos, float warnTime = 0.8f, float activeTime = 0.7f, float height = 7f, float width = 1.3f)
        {
            var f = room.Spawn<FireColumn>(floorPos);
            f.warn = warnTime; f.active = activeTime; f.height = height; f.width = width;
            f.marker = Gfx.Glow(f.transform, new Vector2(0, 0.1f), 2.2f, new Color(1f, 0.5f, 0.2f, 0f), Layer.Projectiles);
            f.marker.transform.localScale = new Vector3(width * 2.2f, 1f, 1f);
            f.pillar = Gfx.Part(f.transform, "pillar", Gfx.S("FirePillar", ArtLibrary.FirePillar), Vector2.zero, Layer.Projectiles, true);
            f.pillar.enabled = false;
            return f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            var fx = Game.I.Fx;
            if (t < warn)
            {
                float k = t / warn;
                marker.color = new Color(1f, 0.5f, 0.2f, 0.25f + 0.5f * k + Mathf.Sin(t * 40f) * 0.1f);
                if (Random.value < 0.4f) fx.Ember((Vector2)transform.position + new Vector2(Random.Range(-width * 0.5f, width * 0.5f), 0.1f));
                return;
            }
            if (!fired)
            {
                fired = true;
                pillar.enabled = true;
                Game.I.Sound.Play("fire", 0.6f);
                Game.I.Cam.Shake(0.2f);
                fx.FireBurst((Vector2)transform.position + new Vector2(0, 0.3f), 0.8f, 10);
            }
            float at = t - warn;
            float grow = Mathf.Clamp01(at / 0.08f) * Mathf.Clamp01((active - at) / 0.15f);
            pillar.transform.localScale = new Vector3(width / 1f * (0.9f + Mathf.Sin(t * 50f) * 0.1f), height / 4f * grow, 1f);
            marker.color = new Color(1f, 0.6f, 0.3f, 0.6f * grow);
            if (grow > 0.3f) Combat.HurtPlayer(new Rect(transform.position.x - width * 0.4f, transform.position.y, width * 0.8f, height * grow), 1, (Vector2)transform.position);
            if (at >= active) Destroy(gameObject);
        }
    }

    /// <summary>Burning puddle left by coals.</summary>
    public sealed class FirePuddle : MonoBehaviour
    {
        float life;
        SpriteRenderer sr;

        public static FirePuddle Spawn(Room room, Vector2 floorPos, float time = 2.4f)
        {
            var f = room.Spawn<FirePuddle>(floorPos);
            f.life = time;
            f.sr = Gfx.Part(f.transform, "vis", Gfx.S("FirePuddle", ArtLibrary.FirePuddle), Vector2.zero, Layer.Projectiles, true);
            f.sr.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            return f;
        }

        void Update()
        {
            life -= Time.deltaTime;
            float a = Mathf.Clamp01(life / 0.4f);
            sr.color = new Color(1f, 1f, 1f, a);
            sr.transform.localScale = new Vector3(1.4f, 1.4f * (1f + Mathf.Sin(Time.time * 20f) * 0.1f), 1f);
            if (Random.value < 0.3f) Game.I.Fx.Ember((Vector2)transform.position + new Vector2(Random.Range(-1f, 1f), 0.2f));
            if (a > 0.5f) Combat.HurtPlayer(new Rect(transform.position.x - 1.1f, transform.position.y, 2.2f, 0.5f), 1, transform.position);
            if (life <= 0f) Destroy(gameObject);
        }
    }

    /// <summary>Falling debris/spear with a shadow telegraph on the floor.</summary>
    public sealed class FallingHazard : MonoBehaviour
    {
        float delay, speed = 22f;
        Vector2 floor;
        SpriteRenderer vis, shadow;
        bool falling;
        int damage;
        float radius;

        public static FallingHazard Spawn(Room room, float x, float delay, bool spear)
        {
            float top = room.CeilingAbove(x, room.H * 0.5f) - 0.5f;
            float fl = room.FloorBelow(x, top - 1f);
            var f = room.Spawn<FallingHazard>(new Vector2(x, top));
            f.delay = delay;
            f.floor = new Vector2(x, fl);
            f.damage = 1;
            f.radius = spear ? 0.3f : 0.45f;
            f.vis = spear
                ? Gfx.Part(f.transform, "spear", Gfx.S("AshSpear", ArtLibrary.AshSpear), Vector2.zero, Layer.Projectiles)
                : Gfx.Part(f.transform, "rock", Gfx.S("Debris", ArtLibrary.Debris), Vector2.zero, Layer.Projectiles);
            if (spear) f.vis.transform.localScale = Vector3.one * 1.1f;
            f.vis.transform.localRotation = spear ? Quaternion.Euler(0, 0, 0f) : Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            f.shadow = Gfx.Glow(room.transform, f.floor + new Vector2(0, 0.05f), 1.6f, spear ? new Color(1f, 0.4f, 0.1f, 0f) : new Color(0f, 0f, 0f, 0f), Layer.Projectiles - 2);
            f.shadow.sharedMaterial = spear ? SpriteBank.AdditiveMaterial : SpriteBank.SpriteMaterial;
            f.shadow.transform.localScale = new Vector3(1.6f, 0.5f, 1f);
            return f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!falling)
            {
                delay -= dt;
                var c = shadow.color; c.a = Mathf.Min(0.7f, c.a + dt * 1.5f); shadow.color = c;
                transform.position += new Vector3(Mathf.Sin(Time.time * 60f) * 0.01f, 0, 0);
                if (delay <= 0f) falling = true;
                return;
            }
            transform.position += new Vector3(0, -speed * dt, 0);
            if (!vis.name.StartsWith("spear")) vis.transform.Rotate(0, 0, 360f * dt);
            Combat.HurtPlayerCircle((Vector2)transform.position + new Vector2(0, -0.3f), radius, damage);
            if (transform.position.y <= floor.y + 0.2f)
            {
                Game.I.Fx.HitBurst(floor + new Vector2(0, 0.2f), Vector2.up, 6, 0.7f);
                Game.I.Fx.Dust(floor, 5);
                Game.I.Sound.Play("land", 0.5f);
                if (shadow != null) Destroy(shadow.gameObject);
                Destroy(gameObject);
            }
        }

        void OnDestroy() { if (shadow != null) Destroy(shadow.gameObject); }
    }

    /// <summary>Rotating beam of fire with a thin telegraph line first.</summary>
    public sealed class SweepBeam : MonoBehaviour
    {
        float warn, active, a0, a1, length, t;
        SpriteRenderer line, beam;

        public static SweepBeam Spawn(Room room, Vector2 origin, float fromDeg, float toDeg, float warnTime, float activeTime, float length = 40f)
        {
            var b = room.Spawn<SweepBeam>(origin);
            b.warn = warnTime; b.active = activeTime; b.a0 = fromDeg; b.a1 = toDeg; b.length = length;
            b.line = Gfx.Part(b.transform, "line", Gfx.S("TelegraphLine", ArtLibrary.TelegraphLine), Vector2.zero, Layer.Projectiles, true);
            b.line.color = new Color(1f, 0.4f, 0.2f, 0.6f);
            b.line.transform.localScale = new Vector3(length, 0.5f, 1f);
            b.beam = Gfx.Part(b.transform, "beam", Gfx.S("Beam", ArtLibrary.Beam), Vector2.zero, Layer.Projectiles + 1, true);
            b.beam.transform.localScale = new Vector3(length / 4f, 1.2f, 1f);
            b.beam.enabled = false;
            b.transform.rotation = Quaternion.Euler(0, 0, fromDeg);
            return b;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            if (t < warn)
            {
                line.color = new Color(1f, 0.4f, 0.2f, 0.3f + 0.5f * Mathf.Abs(Mathf.Sin(t * 20f)));
                return;
            }
            if (!beam.enabled)
            {
                beam.enabled = true;
                line.enabled = false;
                Game.I.Sound.Play("fire", 0.8f);
                Game.I.Cam.Shake(0.4f);
            }
            float k = Mathf.Clamp01((t - warn) / active);
            float ang = Mathf.Lerp(a0, a1, Mathf.SmoothStep(0, 1, k));
            transform.rotation = Quaternion.Euler(0, 0, ang);
            beam.transform.localScale = new Vector3(length / 4f, 1.2f * (0.85f + Mathf.Sin(t * 60f) * 0.15f), 1f);

            // damage along the beam
            var p = Game.I.Player;
            if (p != null)
            {
                Vector2 o = transform.position;
                Vector2 d = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                Vector2 to = p.Center - o;
                float along = Vector2.Dot(to, d);
                float perp = Mathf.Abs(to.x * d.y - to.y * d.x);
                if (along > 0f && along < length && perp < 0.7f) p.TakeDamage(1, p.Center - d);
            }
            if (Random.value < 0.8f) Game.I.Fx.Ember((Vector2)transform.position + new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * Random.Range(1f, 15f));
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
