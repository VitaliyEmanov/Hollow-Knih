using System.Collections;
using AshenWick.Art;
using AshenWick.World;
using UnityEngine;

namespace AshenWick
{
    /// <summary>
    /// Base of the three great foes. Bosses are coroutine "brains": each attack is a
    /// readable sequence of telegraph → strike → recovery, and every phase change is a
    /// short staged moment (roar, slow motion, arena changes).
    /// </summary>
    public abstract class Boss : MonoBehaviour, IHittable
    {
        public string Id;
        public int MaxHp = 200;
        public int Hp;
        public int Phase = 1;
        public string DisplayName { get { return Lore.Boss(Id).Name; } }

        protected Room room;
        protected Body body;
        protected Transform vis;
        protected readonly Flasher flasher = new Flasher();
        protected float facing = -1f;
        protected bool fighting, dead;
        protected float t;
        protected float arenaMin, arenaMax, floorY;
        protected int contactDamage = 1;
        protected float speedMul = 1f;
        int pendingPhase;
        string lastAttack;
        protected int musicId = 10;

        public bool Fighting { get { return fighting; } }
        public virtual Rect Hurtbox { get { return body.Rect; } }
        public virtual bool Vulnerable { get { return fighting && !dead; } }
        public float HpFraction { get { return Mathf.Clamp01(Hp / (float)MaxHp); } }

        protected Player Player { get { return Game.I.Player; } }
        protected Vector2 PlayerPos { get { var p = Player; return p != null ? p.Center : body.Pos; } }

        public static Boss Create(string id, Vector2 feet, Room room)
        {
            Boss b;
            switch (id)
            {
                case "gornan": b = room.Spawn<Gornan>(feet); break;
                case "weaver": b = room.Spawn<SootWeaver>(feet); break;
                default: b = room.Spawn<CinderKing>(feet); break;
            }
            b.Id = id;
            b.room = room;
            b.Setup(feet);
            return b;
        }

        void Setup(Vector2 feet)
        {
            vis = new GameObject("vis").transform;
            vis.SetParent(transform, false);
            body = new Body(feet, new Vector2(1f, 1f));
            // arena from gate columns (or the whole room)
            arenaMin = 1.5f; arenaMax = room.W - 1.5f;
            for (int y = 0; y < room.H - 1; y++)
                for (int x = 0; x < room.W; x++)
                {
                    if (room.Def.At(x, y) != 'G' || room.Def.At(x, y + 1) != 'G') continue; // vertical gates only
                    if (x < room.W / 2) arenaMin = Mathf.Max(arenaMin, x + 1.2f);
                    else arenaMax = Mathf.Min(arenaMax, x - 0.2f);
                }
            floorY = room.FloorBelow(feet.x, feet.y + 0.5f, true);
            Build();
            body.Pos = new Vector2(feet.x, floorY + body.Half.y + 0.01f);
            Hp = MaxHp;
            flasher.AddAll(vis);
            transform.position = body.Pos;
            OnSpawned();
        }

        protected abstract void Build();
        protected virtual void OnSpawned() { }
        protected abstract IEnumerator Intro();
        protected abstract IEnumerator Brain();
        protected virtual IEnumerator PhaseChange(int phase) { yield break; }
        protected virtual void Animate(float dt) { }

        public void BeginFight()
        {
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var g = Game.I;
            g.Sound.StopMusic(0.5f);
            yield return Intro();
            var info = Lore.Boss(Id);
            g.Hud.ShowBossTitle(info.Name, info.Title);
            g.Sound.PlayMusic(musicId);
            Combat.Register(this);
            fighting = true;
            g.Hud.ShowBossBar(this);
            while (!dead)
            {
                if (pendingPhase > Phase)
                {
                    Phase = pendingPhase;
                    yield return PhaseChange(Phase);
                    continue;
                }
                yield return Brain();
            }
        }

        protected virtual void Update()
        {
            var g = Game.I;
            if (g == null) return;
            float dt = Time.deltaTime;
            t += dt;
            flasher.Tick(dt);
            if (fighting && !dead && contactDamage > 0 && g.Live)
            {
                var r = Hurtbox;
                Combat.HurtPlayer(new Rect(r.x + 0.25f, r.y + 0.1f, r.width - 0.5f, r.height - 0.3f), contactDamage, body.Pos);
            }
            Animate(dt);
            transform.position = new Vector3(body.Pos.x, body.Pos.y, 0f);
        }

        // ------------------------------------------------------------------
        // Damage & death
        // ------------------------------------------------------------------

        protected float[] phaseAt = { 0.6f, 0.3f };

        public virtual bool TakeHit(Hit h)
        {
            if (!Vulnerable) return false;
            Hp -= h.Damage;
            flasher.Flash(Color.white, 0.9f);
            Game.I.Cam.Shake(h.Spell ? 0.25f : 0.1f);
            for (int i = 0; i < phaseAt.Length; i++)
                if (HpFraction <= phaseAt[i] && pendingPhase < i + 2) pendingPhase = i + 2;
            if (Hp <= 0)
            {
                Hp = 0;
                OnDefeated();
            }
            return true;
        }

        protected virtual void OnDefeated()
        {
            dead = true;
            fighting = false;
            StopAllCoroutines();
            Combat.Unregister(this);
            StartCoroutine(DeathSequence());
        }

        protected virtual IEnumerator DeathSequence()
        {
            var g = Game.I;
            g.Hud.HideBossBar();
            g.Sound.StopMusic(0.3f);
            g.Sound.Play("roar", 1f, 0f);
            g.SlowMotion(0.2f, 1.6f);
            g.Fx.Flash(Color.white, 0.9f);
            g.Cam.Shake(1f);
            g.Cam.Punch(0.12f);
            ClearHazards();
            for (int i = 0; i < 10; i++)
            {
                flasher.Flash(i % 2 == 0 ? Color.white : new Color(1f, 0.5f, 0.2f), 1f);
                g.Fx.HitBurst(body.Pos + Random.insideUnitCircle * 1.5f, Random.insideUnitCircle.normalized, 8, 1.3f);
                g.Fx.FireBurst(body.Pos + Random.insideUnitCircle * 1.5f, 0.8f, 8);
                g.Sound.Play("hit", 0.8f, 0.2f);
                vis.localPosition = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f), 0);
                yield return new WaitForSecondsRealtime(0.12f);
            }
            g.Sound.Play("boom", 1f, 0f);
            g.Sound.Play("shatter", 0.8f, 0f);
            g.Fx.BossExplosion(body.Pos);
            g.Fx.Flash(new Color(1f, 0.95f, 0.85f), 1f);
            g.Cam.Shake(1f);
            yield return OnDeathVisual();
            g.OnBossDefeated(Id);
            room.OnBossDefeated(this);
            yield return new WaitForSecondsRealtime(1.2f);
            g.Sound.PlayMusic(room.Def.Area);
            Reward();
        }

        /// <summary>By default the body crumbles to ash.</summary>
        protected virtual IEnumerator OnDeathVisual()
        {
            foreach (var sr in vis.GetComponentsInChildren<SpriteRenderer>()) sr.color = new Color(0.25f, 0.24f, 0.27f, 1f);
            float a = 1f;
            while (a > 0f)
            {
                a -= Time.unscaledDeltaTime * 0.8f;
                foreach (var sr in vis.GetComponentsInChildren<SpriteRenderer>()) sr.color = new Color(0.25f, 0.24f, 0.27f, a);
                vis.localPosition += new Vector3(0, -Time.unscaledDeltaTime * 0.6f, 0);
                if (Random.value < 0.8f) Game.I.Fx.AmbientAsh(body.Pos + Random.insideUnitCircle * 2f, 0);
                yield return null;
            }
            vis.gameObject.SetActive(false);
        }

        protected virtual void Reward() { }

        protected void ClearHazards()
        {
            foreach (var p in FindObjectsOfTypeCompat<Projectile>()) if (p != null) p.Kill(false);
            foreach (var h in FindObjectsOfTypeCompat<FireColumn>()) if (h != null) Destroy(h.gameObject);
            foreach (var h in FindObjectsOfTypeCompat<FallingHazard>()) if (h != null) Destroy(h.gameObject);
            foreach (var h in FindObjectsOfTypeCompat<SweepBeam>()) if (h != null) Destroy(h.gameObject);
            foreach (var h in FindObjectsOfTypeCompat<Shockwave>()) if (h != null) Destroy(h.gameObject);
        }

        protected T[] FindObjectsOfTypeCompat<T>() where T : Component
        {
            return room.GetComponentsInChildren<T>();
        }

        // ------------------------------------------------------------------
        // Helpers for attack scripts
        // ------------------------------------------------------------------

        protected WaitForSeconds Wait(float s) { return new WaitForSeconds(s / speedMul); }

        protected void FacePlayer()
        {
            float d = PlayerPos.x - body.Pos.x;
            if (Mathf.Abs(d) > 0.2f) facing = Mathf.Sign(d);
        }

        protected string Pick(params string[] options)
        {
            string c = options[Random.Range(0, options.Length)];
            if (c == lastAttack && options.Length > 1) c = options[Random.Range(0, options.Length)];
            if (c == lastAttack && options.Length > 1) c = options[(System.Array.IndexOf(options, c) + 1) % options.Length];
            lastAttack = c;
            return c;
        }

        protected void Telegraph(Vector2 at, Color c, float size = 1.6f)
        {
            Game.I.Sound.Play("telegraph", 0.7f, 0.05f);
            Game.I.Fx.RingPulse(at, c, size * 1.8f, 0.2f, 0.35f);
            Game.I.Fx.HitBurst(at, Vector2.up, 0, 0.6f);
        }

        protected void Roar(float shake = 0.8f)
        {
            Game.I.Sound.Play("roar", 1f, 0.05f);
            Game.I.Cam.Shake(shake);
            Game.I.Fx.RingPulse(body.Pos + new Vector2(0, body.Half.y * 0.6f), new Color(1f, 0.8f, 0.6f, 0.7f), 1f, 14f, 0.8f);
        }

        protected float ClampX(float x, float margin) { return Mathf.Clamp(x, arenaMin + margin, arenaMax - margin); }

        protected void MoveGround(float dt, float vx, float gravity = 60f)
        {
            body.Vel.x = vx;
            body.Vel.y = Mathf.Max(body.Vel.y - gravity * dt, -25f);
            body.Move(room, dt);
            body.Pos.x = ClampX(body.Pos.x, body.Half.x);
            if (body.Grounded && body.Vel.y < 0f) body.Vel.y = 0f;
        }

        /// <summary>Walks toward x with gravity until close or timeout.</summary>
        protected IEnumerator WalkTo(float x, float speed, float maxTime, float stopDist = 0.4f)
        {
            float time = 0f;
            while (time < maxTime && Mathf.Abs(x - body.Pos.x) > stopDist)
            {
                float dt = Time.deltaTime;
                time += dt;
                facing = Mathf.Sign(x - body.Pos.x);
                MoveGround(dt, facing * speed * speedMul);
                OnWalkStep(dt);
                yield return null;
            }
            body.Vel.x = 0f;
        }

        protected virtual void OnWalkStep(float dt) { }

        /// <summary>Ballistic jump to x, landing on the floor. Calls onAir each frame.</summary>
        protected IEnumerator JumpTo(float x, float flightTime, float gravity, System.Action<float> onAir = null)
        {
            x = ClampX(x, body.Half.x);
            float T = flightTime / speedMul;
            float vx = (x - body.Pos.x) / T;
            body.Vel = new Vector2(vx, 0.5f * gravity * T);
            float time = 0f;
            yield return null;
            while (true)
            {
                float dt = Time.deltaTime;
                time += dt;
                body.Vel.y = Mathf.Max(body.Vel.y - gravity * dt, -35f);
                body.Vel.x = vx;
                body.Move(room, dt);
                body.Pos.x = ClampX(body.Pos.x, body.Half.x);
                if (onAir != null) onAir(time / T);
                if ((body.Grounded && time > 0.1f) || time > T * 2.5f) break;
                yield return null;
            }
            body.Vel = Vector2.zero;
        }

        /// <summary>Free flight to a point (flying bosses, NoClip).</summary>
        protected IEnumerator FlyTo(Vector2 target, float speed, float maxTime = 3f)
        {
            float time = 0f;
            while (time < maxTime)
            {
                float dt = Time.deltaTime;
                time += dt;
                Vector2 d = target - body.Pos;
                if (d.magnitude < 0.2f) break;
                float s = Mathf.Min(speed * speedMul, d.magnitude * 5f);
                body.Pos += d.normalized * s * dt;
                yield return null;
            }
        }

        protected void Shockwaves(Vector2 at, bool both, float speed = 12f)
        {
            float fy = room.FloorBelow(at.x, at.y + 0.5f);
            Shockwave.Spawn(room, new Vector2(at.x + facing * 0.6f, fy), facing, speed);
            if (both) Shockwave.Spawn(room, new Vector2(at.x - facing * 0.6f, fy), -facing, speed);
        }
    }
}
