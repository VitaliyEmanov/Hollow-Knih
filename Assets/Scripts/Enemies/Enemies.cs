using AshenWick.Art;
using UnityEngine;

namespace AshenWick
{
    /// <summary>Base of every Ashbound creature: health, hit reactions, contact damage.</summary>
    public abstract class Enemy : MonoBehaviour, IHittable
    {
        protected Body body;
        protected Room room;
        protected Transform vis;
        protected readonly Flasher flasher = new Flasher();
        protected int hp;
        protected int contactDamage = 1;
        protected float facing = 1f;
        protected float knockback;
        protected float kbResist = 1f;
        protected float t;
        protected bool flying;
        bool dead;

        public virtual Rect Hurtbox { get { return body.Rect; } }
        public bool Vulnerable { get { return !dead; } }
        protected Player Player { get { return Game.I != null ? Game.I.Player : null; } }

        protected abstract void Build();
        protected abstract void Think(float dt);

        protected virtual void Awake()
        {
            vis = new GameObject("vis").transform;
            vis.SetParent(transform, false);
            body = new Body(transform.position, new Vector2(0.4f, 0.4f));
            Build();
            flasher.AddAll(vis);
        }

        protected virtual void Start()
        {
            room = GetComponentInParent<Room>();
            if (!flying) body.Pos = (Vector2)transform.position + new Vector2(0f, body.Half.y + 0.01f);
            else body.Pos = transform.position;
            Combat.Register(this);
            t = Random.value * 10f;
        }

        protected virtual void OnDestroy() { Combat.Unregister(this); }

        protected virtual void Update()
        {
            var g = Game.I;
            if (g == null || room == null) return;
            if (!(g.Live || g.State == GameState.Dialogue || g.State == GameState.Dead)) return;
            float dt = Time.deltaTime;
            t += dt;
            flasher.Tick(dt);
            if (!dead)
            {
                Think(dt);
                if (contactDamage > 0 && g.Live)
                {
                    var r = Hurtbox;
                    Combat.HurtPlayer(new Rect(r.x + 0.1f, r.y + 0.05f, r.width - 0.2f, r.height - 0.15f), contactDamage, body.Pos);
                }
            }
            transform.position = new Vector3(body.Pos.x, body.Pos.y, 0f);
            vis.localScale = new Vector3(facing * Mathf.Abs(vis.localScale.x), vis.localScale.y, 1f);
        }

        protected void Gravity(float dt, float g = 60f)
        {
            body.Vel.y = Mathf.Max(body.Vel.y - g * dt, -20f);
        }

        protected void ApplyKnockback(float dt)
        {
            if (Mathf.Abs(knockback) > 0.01f)
            {
                body.Vel.x += knockback;
                knockback = Mathf.MoveTowards(knockback, 0f, dt * 60f);
            }
        }

        public virtual bool TakeHit(Hit h)
        {
            if (dead) return false;
            hp -= h.Damage;
            flasher.Flash(Color.white, 1f);
            if (h.Dir.x != 0f) knockback = Mathf.Sign(h.Dir.x) * 7f / kbResist;
            if (flying && h.Dir.y != 0f) body.Vel.y += h.Dir.y * 6f / kbResist;
            OnHurt(h);
            if (hp <= 0) Die(h);
            return true;
        }

        protected virtual void OnHurt(Hit h) { }

        protected virtual void Die(Hit h)
        {
            dead = true;
            Combat.Unregister(this);
            var g = Game.I;
            g.Sound.Play("enemydie", 0.8f);
            g.Fx.HitBurst(body.Pos, h.Dir.sqrMagnitude > 0 ? h.Dir : Vector2.up, 18, 1.2f);
            g.Fx.Smoke(body.Pos, new Color(0.25f, 0.24f, 0.28f, 0.7f), 0.8f, 5);
            g.HitStop(0.06f);
            gameObject.AddComponent<Corpse>().Setup(vis, body.Pos.y - body.Half.y);
            enabled = false;
        }

        protected SpriteRenderer Part(string name, Sprite s, Vector2 pos, int orderOffset = 0, float scale = 1f)
        {
            var sr = Gfx.Part(vis, name, s, pos, Layer.Enemies + orderOffset);
            sr.transform.localScale = Vector3.one * scale;
            return sr;
        }

        protected float DistToPlayer()
        {
            var p = Player;
            return p == null ? 999f : Vector2.Distance(p.Center, body.Pos);
        }
    }

    /// <summary>Dead enemies crumble into ash: fall, darken, fade.</summary>
    public sealed class Corpse : MonoBehaviour
    {
        Transform v;
        float floorY, vy = 4f, life = 1.4f;
        SpriteRenderer[] srs;

        public void Setup(Transform visual, float floor)
        {
            v = visual; floorY = floor;
            srs = v.GetComponentsInChildren<SpriteRenderer>();
            foreach (var s in srs) if (s != null) s.color = new Color(0.35f, 0.33f, 0.36f, s.color.a);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            vy -= 30f * dt;
            var p = transform.position;
            p.y = Mathf.Max(floorY + 0.3f, p.y + vy * dt);
            transform.position = p;
            v.localRotation = Quaternion.Euler(0, 0, Mathf.MoveTowards(v.localRotation.eulerAngles.z, 20f, dt * 60f));
            float a = Mathf.Clamp01(life / 0.8f);
            foreach (var s in srs) if (s != null) s.color = new Color(0.35f, 0.33f, 0.36f, a);
            if (Random.value < 0.2f) Game.I.Fx.AmbientAsh((Vector2)transform.position + Random.insideUnitCircle * 0.5f, 0);
            if (life <= 0f) Destroy(gameObject);
        }
    }

    // ======================================================================
    // Пеплоползень — slow crawler, the first thing that tries to snuff Niv
    // ======================================================================
    public sealed class AshCrawler : Enemy
    {
        Transform[] legs = new Transform[4];

        protected override void Build()
        {
            hp = 10;
            body.Half = new Vector2(0.55f, 0.36f);
            var s = Part("body", Gfx.S("CrawlerBody", ArtLibrary.CrawlerBody), new Vector2(0f, -0.38f), 1);
            for (int i = 0; i < 4; i++)
                legs[i] = Part("leg" + i, Gfx.S("CrawlerLeg", ArtLibrary.CrawlerLeg), new Vector2(-0.4f + i * 0.28f, -0.2f), 0).transform;
            facing = Random.value < 0.5f ? -1f : 1f;
        }

        protected override void Think(float dt)
        {
            body.Vel.x = facing * 1.8f;
            ApplyKnockback(dt);
            Gravity(dt);
            body.Move(room, dt);
            if (body.Grounded && knockback == 0f && (body.WallAhead(room, facing) || !body.GroundAhead(room, facing))) facing = -facing;
            for (int i = 0; i < 4; i++) legs[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 12f + i * 1.7f) * 25f);
            vis.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(t * 6f)) * 0.03f, 0);
        }
    }

    // ======================================================================
    // Сажевый мотылёк — soot moth drawn to Niv's light
    // ======================================================================
    public sealed class SootMoth : Enemy
    {
        Transform wingL, wingR;
        Vector2 home;
        bool aggro;

        protected override void Build()
        {
            hp = 10;
            flying = true;
            kbResist = 0.7f;
            body.Half = new Vector2(0.38f, 0.38f);
            body.IgnorePlatforms = true;
            wingL = Part("wingL", Gfx.S("MothWing", ArtLibrary.MothWing), new Vector2(-0.1f, 0.1f), -1).transform;
            wingL.localScale = new Vector3(-1f, 1f, 1f);
            wingR = Part("wingR", Gfx.S("MothWing", ArtLibrary.MothWing), new Vector2(0.1f, 0.1f), -1).transform;
            Part("body", Gfx.S("MothBody", ArtLibrary.MothBody), Vector2.zero, 1);
        }

        protected override void Start()
        {
            base.Start();
            home = body.Pos;
        }

        protected override void Think(float dt)
        {
            var p = Player;
            float d = DistToPlayer();
            if (!aggro && d < 8f) { aggro = true; Game.I.Sound.Play("wing", 0.4f); }
            if (aggro && d > 16f) aggro = false;
            Vector2 target = aggro && p != null ? p.Center + new Vector2(0, 0.2f) : home + new Vector2(Mathf.Sin(t * 0.7f) * 1.5f, Mathf.Sin(t * 1.3f) * 0.6f);
            Vector2 to = target - body.Pos;
            float maxSpeed = aggro ? 5.2f : 1.6f;
            body.Vel = Vector2.MoveTowards(body.Vel, to.normalized * maxSpeed, (aggro ? 9f : 4f) * dt);
            ApplyKnockback(dt);
            knockback *= 0.9f;
            body.Move(room, dt);
            if (body.HitLeft || body.HitRight) body.Vel.x *= -0.5f;
            if (body.HitCeiling || body.Grounded) body.Vel.y *= -0.5f;
            if (Mathf.Abs(to.x) > 0.3f) facing = Mathf.Sign(to.x);
            float flap = Mathf.Abs(Mathf.Sin(t * (aggro ? 22f : 12f)));
            wingL.localScale = new Vector3(-1f, 0.3f + flap * 0.8f, 1f);
            wingR.localScale = new Vector3(1f, 0.3f + flap * 0.8f, 1f);
            vis.localPosition = new Vector3(0, Mathf.Sin(t * 5f) * 0.05f, 0);
        }
    }

    // ======================================================================
    // Тлеющий страж — cinder husk: patrols, sees Niv, lowers lance and charges
    // ======================================================================
    public sealed class CinderHusk : Enemy
    {
        enum S { Patrol, Windup, Charge, Recover }
        S s = S.Patrol;
        float timer;
        Transform lance, bodyT;
        SpriteRenderer tipGlow;

        protected override void Build()
        {
            hp = 30;
            kbResist = 1.6f;
            body.Half = new Vector2(0.45f, 0.92f);
            bodyT = Part("body", Gfx.S("HuskBody", ArtLibrary.HuskBody), new Vector2(0f, -0.93f), 1).transform;
            lance = Part("lance", Gfx.S("HuskLance", ArtLibrary.HuskLance), new Vector2(0.15f, -0.1f), 2).transform;
            tipGlow = Gfx.Glow(lance, new Vector2(1.45f, 0f), 1.4f, new Color(1f, 0.5f, 0.2f, 0f), Layer.Enemies + 3);
            flasher.Add(tipGlow);
        }

        protected override void Think(float dt)
        {
            timer -= dt;
            var p = Player;
            switch (s)
            {
                case S.Patrol:
                    body.Vel.x = facing * 1.4f;
                    if (body.Grounded && (body.WallAhead(room, facing) || !body.GroundAhead(room, facing))) facing = -facing;
                    if (p != null)
                    {
                        Vector2 to = p.Center - body.Pos;
                        if (Mathf.Abs(to.y) < 2.5f && Mathf.Abs(to.x) < 9f && Mathf.Sign(to.x) == facing)
                        {
                            s = S.Windup; timer = 0.55f;
                            Game.I.Sound.Play("telegraph", 0.5f);
                        }
                    }
                    lance.localRotation = Quaternion.Euler(0, 0, 50f + Mathf.Sin(t * 3f) * 4f);
                    break;
                case S.Windup:
                    body.Vel.x = -facing * 0.6f;
                    lance.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(lance.localRotation.eulerAngles.z > 180 ? lance.localRotation.eulerAngles.z - 360 : lance.localRotation.eulerAngles.z, 0f, dt * 12f));
                    tipGlow.color = new Color(1f, 0.5f, 0.2f, 0.6f + Mathf.Sin(t * 40f) * 0.3f);
                    vis.localPosition = new Vector3(Mathf.Sin(t * 70f) * 0.04f, 0, 0);
                    if (timer <= 0f) { s = S.Charge; timer = 1.4f; Game.I.Sound.Play("dash", 0.6f); }
                    break;
                case S.Charge:
                    body.Vel.x = facing * 12f;
                    lance.localRotation = Quaternion.identity;
                    if (Random.value < 0.5f) Game.I.Fx.Ember(body.Pos + new Vector2(-facing * 0.3f, -0.6f));
                    if (Random.value < 0.3f) Game.I.Fx.Dust(body.Pos - new Vector2(0, 0.9f), 1);
                    Combat.HurtPlayer(Combat.RectAt(body.Pos + new Vector2(facing * 1.2f, -0.1f), new Vector2(1.2f, 0.5f)), 1, body.Pos);
                    if (timer <= 0f || body.WallAhead(room, facing) || !body.GroundAhead(room, facing))
                    {
                        s = S.Recover; timer = 0.8f;
                        if (body.WallAhead(room, facing)) { Game.I.Cam.Shake(0.2f); Game.I.Sound.Play("slam", 0.4f); }
                    }
                    break;
                case S.Recover:
                    body.Vel.x = Mathf.MoveTowards(body.Vel.x, 0f, dt * 40f);
                    tipGlow.color = new Color(1f, 0.5f, 0.2f, Mathf.MoveTowards(tipGlow.color.a, 0f, dt * 2f));
                    vis.localPosition = Vector3.zero;
                    if (timer <= 0f) { s = S.Patrol; facing = p != null ? Mathf.Sign(p.Center.x - body.Pos.x) : -facing; }
                    break;
            }
            ApplyKnockback(dt);
            Gravity(dt);
            body.Move(room, dt);
            bodyT.localPosition = new Vector3(0, -0.93f + (s == S.Patrol ? Mathf.Abs(Mathf.Sin(t * 5f)) * 0.03f : 0f), 0);
        }
    }

    // ======================================================================
    // Золоплюй — ash spitter: lobs burning globs in arcs
    // ======================================================================
    public sealed class AshSpitter : Enemy
    {
        float cd = 1.5f, windup;
        Transform bodyT;

        protected override void Build()
        {
            hp = 20;
            kbResist = 99f;
            body.Half = new Vector2(0.6f, 0.55f);
            bodyT = Part("body", Gfx.S("SpitterBody", ArtLibrary.SpitterBody), new Vector2(0f, -0.56f), 1).transform;
            Gfx.Glow(vis, new Vector2(-0.05f, 0.1f), 2.2f, new Color(1f, 0.45f, 0.15f, 0.25f), Layer.Enemies - 1).gameObject.AddComponent<Flicker>().Setup(0.25f, 0.08f, 0f);
        }

        protected override void Think(float dt)
        {
            Gravity(dt);
            body.Vel.x = 0f;
            body.Move(room, dt);
            var p = Player;
            if (p != null) facing = Mathf.Sign(p.Center.x - body.Pos.x);
            float d = DistToPlayer();
            if (windup > 0f)
            {
                windup -= dt;
                float k = 1f - windup / 0.55f;
                bodyT.localScale = new Vector3(1f + k * 0.15f + Mathf.Sin(t * 50f) * 0.03f, 1f + k * 0.2f, 1f);
                if (windup <= 0f) Spit();
                return;
            }
            bodyT.localScale = new Vector3(1f + Mathf.Sin(t * 3f) * 0.03f, 1f - Mathf.Sin(t * 3f) * 0.03f, 1f);
            cd -= dt;
            if (cd <= 0f && d < 13f)
            {
                windup = 0.55f;
                cd = 2.4f;
            }
        }

        void Spit()
        {
            var p = Player;
            if (p == null) return;
            Vector2 from = body.Pos + new Vector2(0f, 0.6f);
            Vector2 to = p.Center;
            float T = Mathf.Clamp(Mathf.Abs(to.x - from.x) / 8f, 0.7f, 1.3f);
            float g = 18f;
            Vector2 v = new Vector2((to.x - from.x) / T, (to.y - from.y) / T + 0.5f * g * T);
            var pr = Projectile.Spawn(room, "AshGlob", ArtLibrary.AshGlob, from, v, 0.28f, 1.2f);
            pr.Gravity = g;
            pr.FaceVelocity = false;
            pr.Spin = 360f;
            pr.AddGlow(new Color(1f, 0.5f, 0.2f, 0.35f), 1.6f);
            Game.I.Sound.Play("shoot", 0.6f);
            Game.I.Fx.Smoke(from, new Color(0.3f, 0.28f, 0.3f, 0.6f), 0.5f, 3);
            bodyT.localScale = new Vector3(0.85f, 0.9f, 1f);
        }
    }

    // ======================================================================
    // Искра — ember wisp: drifts to Niv and bursts
    // ======================================================================
    public sealed class EmberWisp : Enemy
    {
        float fuse = -1f;
        Transform core;
        SpriteRenderer glow;

        protected override void Build()
        {
            hp = 5;
            flying = true;
            contactDamage = 0;
            body.Half = new Vector2(0.3f, 0.3f);
            body.IgnorePlatforms = true;
            core = Part("wisp", Gfx.S("WispBody", ArtLibrary.WispBody), Vector2.zero, 1).transform;
            glow = Gfx.Glow(vis, Vector2.zero, 2.6f, new Color(1f, 0.55f, 0.2f, 0.4f), Layer.Enemies - 1);
        }

        protected override void Think(float dt)
        {
            var p = Player;
            float d = DistToPlayer();
            if (fuse >= 0f)
            {
                fuse += dt;
                body.Vel = Vector2.MoveTowards(body.Vel, Vector2.zero, dt * 20f);
                body.Move(room, dt);
                float blink = Mathf.Repeat(fuse * (6f + fuse * 20f), 1f) < 0.5f ? 1f : 0f;
                flasher.Flash(Color.white, blink);
                core.localScale = Vector3.one * (1f + fuse * 0.6f);
                glow.transform.localScale = Vector3.one * (2.6f + fuse * 3f);
                if (fuse >= 0.75f) Explode();
                return;
            }
            Vector2 target = body.Pos;
            if (p != null && d < 10f) target = p.Center;
            else target += new Vector2(Mathf.Sin(t), Mathf.Cos(t * 1.3f));
            body.Vel = Vector2.MoveTowards(body.Vel, (target - body.Pos).normalized * 3.2f, dt * 6f);
            ApplyKnockback(dt);
            knockback *= 0.9f;
            body.Move(room, dt);
            core.localPosition = new Vector3(0, Mathf.Sin(t * 4f) * 0.08f, 0);
            if (Random.value < 0.3f) Game.I.Fx.Ember(body.Pos);
            if (p != null && d < 1.7f) { fuse = 0f; Game.I.Sound.Play("telegraph", 0.6f, 0.2f); }
        }

        void Explode()
        {
            var g = Game.I;
            g.Fx.FireBurst(body.Pos, 1.6f, 26);
            g.Sound.Play("boom", 0.6f);
            g.Cam.Shake(0.35f);
            Combat.HurtPlayerCircle(body.Pos, 1.9f, 1);
            Combat.Unregister(this);
            Destroy(gameObject);
        }

        protected override void Die(Hit h)
        {
            Game.I.Fx.FireBurst(body.Pos, 0.6f, 10);
            base.Die(h);
        }
    }
}
