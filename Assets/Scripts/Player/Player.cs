using System.Collections.Generic;
using AshenWick.Art;
using UnityEngine;

namespace AshenWick
{
    /// <summary>
    /// Нив — a little living candle ("свечок"). Controls are tuned after Hollow Knight:
    /// instant run, variable jump with coyote time and buffering, 4-way strikes with pogo,
    /// dash, double jump, tap-to-cast "Вспышка" and hold-to-heal "Переплавка".
    /// Health is his flame: the lower it burns, the smaller his light.
    /// </summary>
    public sealed class Player : MonoBehaviour
    {
        enum St { Normal, Dash, Focus, Cast, Hurt, Sit, Dead }

        // tuning
        const float RunSpeed = 8.3f;
        const float JumpSpeed = 18.5f;
        const float DoubleJumpSpeed = 16.5f;
        const float GravityHold = 52f;
        const float GravityFall = 95f;
        const float MaxFall = 22f;
        const float DashSpeed = 22f;
        const float DashTime = 0.2f;
        const float DashCooldown = 0.45f;
        const float Coyote = 0.1f;
        const float JumpBuffer = 0.13f;
        const float AttackCooldown = 0.36f;
        const float AttackActive = 0.13f;
        const int NailDamage = 5;
        const int SpellDamage = 15;
        public const int WaxMax = 99;
        public const int WaxCost = 33;
        const int WaxPerHit = 11;
        const float FocusTime = 0.95f;

        public int Flames { get; private set; }
        public int MaxFlames { get; private set; }
        public int Wax { get; private set; }
        public float Facing { get; private set; }
        public float LookOffset { get; private set; }
        public bool IsDead { get { return state == St.Dead; } }
        public bool Invulnerable { get { return invuln > 0f; } }
        public Rect Hurtbox { get { var r = body.Rect; return new Rect(r.x + 0.06f, r.y, r.width - 0.12f, r.height - 0.08f); } }
        public Vector2 Center { get { return body.Pos; } }
        public Vector2 Velocity { get { return body.Vel; } }
        public bool Grounded { get { return body.Grounded; } }
        public float LightLevel { get; private set; }

        Body body;
        St state = St.Normal;
        bool hasDash, hasSpell, hasDouble;

        float coyote, jumpBuffer, attackBuffer, attackCd, attackTimer, dashTimer, dashCd, invuln, stateTimer;
        float recoilTimer, recoilVx, dropTimer, autoWalk, flameHold, focusTimer, afterimageTimer, lookTimer, safeTimer;
        int autoWalkDir;
        bool jumpCut, canAirDash, canDouble, flameDown, doorArmed, wasGrounded;
        float airVy;
        float pogoGrace;
        int attackDir; // 0 side, 1 up, -1 down
        readonly HashSet<IHittable> hitThisSwing = new HashSet<IHittable>();
        bool pogoDone;
        Vector2 lastSafe;
        Room room;

        // rig
        Transform rig, headT, flameT, cloakT, legL, legR, bladeT;
        SpriteRenderer head, flame, cloak, blade, slash, glowBig, glowSmall, legLR, legRR;
        Flasher flasher = new Flasher();
        float animT, runPhase, blink;
        float slashTimer;

        public void Init(SaveData save)
        {
            MaxFlames = Mathf.Max(5, save.maxFlames);
            Flames = MaxFlames;
            Wax = 0;
            Facing = 1f;
            RefreshAbilities(save);
            body = new Body(Vector2.zero, new Vector2(0.3f, 0.6f));
            BuildRig();
        }

        public void RefreshAbilities(SaveData s)
        {
            hasDash = s.hasDash;
            hasSpell = s.hasSpell;
            hasDouble = s.hasDoubleJump;
        }

        public void AddMaxFlame()
        {
            MaxFlames++;
            Flames = MaxFlames;
        }

        public void PlaceAt(Vector2 pos, Vector2 vel, int walkDir)
        {
            if (body == null) return;
            body.Pos = pos;
            body.Vel = vel;
            state = St.Normal;
            dashTimer = 0; attackTimer = 0; focusTimer = 0; recoilTimer = 0;
            autoWalkDir = walkDir;
            autoWalk = walkDir != 0 ? (vel.y > 0 ? 0.45f : 0.28f) : 0f;
            if (walkDir != 0) Facing = walkDir;
            doorArmed = false;
            lastSafe = pos;
            transform.position = pos;
            room = Game.I.Room;
        }

        // ------------------------------------------------------------------
        // Rig
        // ------------------------------------------------------------------

        void BuildRig()
        {
            rig = new GameObject("Rig").transform;
            rig.SetParent(transform, false);

            glowBig = Gfx.Glow(rig, new Vector2(0, 0.6f), 9f, new Color(1f, 0.7f, 0.4f, 0.16f), Layer.Player - 8);
            glowSmall = Gfx.Glow(rig, new Vector2(0, 0.7f), 2.4f, new Color(1f, 0.75f, 0.45f, 0.35f), Layer.Player - 7);

            legLR = Gfx.Part(rig, "legL", Gfx.S("NivLeg", ArtLibrary.NivLeg), new Vector2(-0.1f, -0.33f), Layer.Player - 1);
            legRR = Gfx.Part(rig, "legR", Gfx.S("NivLeg", ArtLibrary.NivLeg), new Vector2(0.1f, -0.33f), Layer.Player - 1);
            legL = legLR.transform; legR = legRR.transform;
            cloak = Gfx.Part(rig, "cloak", Gfx.S("NivCloak", ArtLibrary.NivCloak), new Vector2(0f, 0.02f), Layer.Player);
            cloakT = cloak.transform;
            cloakT.localScale = Vector3.one * 0.78f;
            head = Gfx.Part(rig, "head", Gfx.S("NivHead", ArtLibrary.NivHead), new Vector2(0f, -0.06f), Layer.Player + 1);
            headT = head.transform;
            headT.localScale = Vector3.one * 0.82f;
            flame = Gfx.Part(headT, "flame", Gfx.S("NivFlame", ArtLibrary.NivFlame), new Vector2(0.01f, 0.72f), Layer.Player + 2);
            flameT = flame.transform;
            flameT.localScale = Vector3.one * 0.55f;
            blade = Gfx.Part(rig, "blade", Gfx.S("NivBlade", ArtLibrary.NivBlade), new Vector2(0.15f, 0.05f), Layer.Player + 3);
            bladeT = blade.transform;
            blade.enabled = false;
            slash = Gfx.Part(rig, "slash", Gfx.S("SlashArc", ArtLibrary.SlashArc), Vector2.zero, Layer.Player + 4, true);
            slash.enabled = false;

            flasher.Add(head); flasher.Add(cloak); flasher.Add(legLR); flasher.Add(legRR);
        }

        // ------------------------------------------------------------------
        // Update
        // ------------------------------------------------------------------

        void Update()
        {
            var g = Game.I;
            if (g == null || body == null) return;
            room = g.Room;
            float dt = Time.deltaTime;
            animT += dt;
            flasher.Tick(dt);

            bool live = g.Live;
            bool physics = live || g.State == GameState.Dialogue || g.State == GameState.Dead;

            if (live && state != St.Dead) HandleInput(dt);
            else { flameDown = false; if (state == St.Focus) EndFocus(); }

            if (physics && room != null) Simulate(dt, live);

            if (live && state != St.Dead)
            {
                CheckAttackHits();
                CheckWorld(g);
            }

            transform.position = new Vector3(body.Pos.x, body.Pos.y, 0f);
            Animate(dt);
        }

        void HandleInput(float dt)
        {
            float mx = Controls.MoveX;
            if (autoWalk > 0f) { autoWalk -= dt; mx = autoWalkDir; }

            // timers
            coyote -= dt; jumpBuffer -= dt; attackBuffer -= dt; attackCd -= dt; dashCd -= dt;
            invuln -= dt; recoilTimer -= dt; dropTimer -= dt; attackTimer -= dt; stateTimer -= dt; pogoGrace -= dt;
            if (dropTimer <= 0f) body.DropThrough = false;

            if (Controls.JumpPressed) jumpBuffer = JumpBuffer;
            if (Controls.AttackPressed) attackBuffer = 0.12f;

            // look up/down (camera) when standing still
            if (state == St.Normal && body.Grounded && mx == 0f && (Controls.Up || Controls.Down))
            {
                lookTimer += dt;
                if (lookTimer > 0.45f) LookOffset = Mathf.MoveTowards(LookOffset, Controls.Up ? 3f : -3.5f, dt * 8f);
            }
            else
            {
                lookTimer = 0f;
                LookOffset = Mathf.MoveTowards(LookOffset, 0f, dt * 10f);
            }

            switch (state)
            {
                case St.Sit:
                    body.Vel = Vector2.zero;
                    if (mx != 0f || Controls.JumpPressed || Controls.DownPressed) { state = St.Normal; Game.I.Sound.Play("land", 0.5f); }
                    return;
                case St.Hurt:
                    if (stateTimer <= 0f) state = St.Normal;
                    return;
                case St.Cast:
                    if (stateTimer <= 0f) state = St.Normal;
                    body.Vel.x = Mathf.MoveTowards(body.Vel.x, 0f, dt * 30f);
                    return;
                case St.Dash:
                    UpdateDash(dt);
                    return;
                case St.Focus:
                    UpdateFocus(dt);
                    return;
            }

            // --- Normal state ---
            if (mx != 0f && attackTimer <= 0f) Facing = Mathf.Sign(mx);
            if (recoilTimer > 0f) body.Vel.x = recoilVx;
            else body.Vel.x = mx * RunSpeed;

            if (body.Grounded)
            {
                coyote = Coyote;
                canAirDash = true;
                canDouble = true;
            }

            // jump / drop through
            if (jumpBuffer > 0f)
            {
                if (body.Grounded && Controls.Down && StandingOnPlatform())
                {
                    body.DropThrough = true;
                    dropTimer = 0.22f;
                    jumpBuffer = 0f;
                }
                else if (coyote > 0f)
                {
                    body.Vel.y = JumpSpeed;
                    jumpCut = false;
                    coyote = 0f; jumpBuffer = 0f;
                    Game.I.Sound.Play("jump", 0.45f);
                    Game.I.Fx.Dust(body.Pos - new Vector2(0, 0.6f), 4, 0.6f);
                }
                else if (hasDouble && canDouble && Controls.JumpPressed)
                {
                    body.Vel.y = DoubleJumpSpeed;
                    jumpCut = false;
                    canDouble = false; jumpBuffer = 0f;
                    Game.I.Sound.Play("wing", 0.7f);
                    var fx = Game.I.Fx;
                    fx.Smoke(body.Pos - new Vector2(0, 0.5f), new Color(0.8f, 0.78f, 0.82f, 0.5f), 0.6f, 5);
                    fx.RingPulse(body.Pos - new Vector2(0, 0.4f), new Color(1f, 0.9f, 0.8f, 0.6f), 0.3f, 1.6f, 0.3f);
                }
            }
            if (!Controls.JumpHeld && body.Vel.y > 0f && pogoGrace <= 0f) jumpCut = true;

            // dash
            if (Controls.DashPressed && hasDash && dashCd <= 0f && (body.Grounded || canAirDash))
            {
                StartDash(mx);
                return;
            }

            // attack
            if (attackBuffer > 0f && attackCd <= 0f)
            {
                attackBuffer = 0f;
                StartAttack();
            }

            // flame key: tap = spell, hold = heal
            if (Controls.FlamePressed) { flameDown = true; flameHold = 0f; }
            if (flameDown)
            {
                if (Controls.FlameHeld)
                {
                    flameHold += dt;
                    if (flameHold >= 0.22f && body.Grounded && Wax >= WaxCost && Flames < MaxFlames)
                    {
                        flameDown = false;
                        StartFocus();
                    }
                    else if (flameHold >= 0.22f && (Wax < WaxCost || Flames >= MaxFlames))
                    {
                        flameDown = false; // held but nothing to heal
                    }
                }
                else
                {
                    flameDown = false;
                    if (flameHold < 0.22f) TryCast();
                }
            }
        }

        bool StandingOnPlatform()
        {
            int y = Mathf.RoundToInt(body.Bottom) - 1;
            int x0 = Mathf.FloorToInt(body.Pos.x - body.Half.x + 0.01f), x1 = Mathf.FloorToInt(body.Pos.x + body.Half.x - 0.01f);
            bool any = false;
            for (int x = x0; x <= x1; x++)
            {
                int k = room.KindAt(x, y);
                if (k == Room.Solid) return false;
                if (k == Room.Platform) any = true;
            }
            return any;
        }

        void Simulate(float dt, bool live)
        {
            if (state == St.Dash || state == St.Sit) { }
            else
            {
                float grav = (body.Vel.y > 0f && !jumpCut && live && (Controls.JumpHeld || pogoGrace > 0f)) ? GravityHold : GravityFall;
                if (state == St.Hurt) grav = GravityFall * 0.8f;
                if (state == St.Focus) grav = GravityFall;
                body.Vel.y = Mathf.Max(body.Vel.y - grav * dt, -MaxFall);
            }
            if (!live && state != St.Dead) body.Vel.x = Mathf.MoveTowards(body.Vel.x, 0f, dt * 40f);
            if (state == St.Dead) body.Vel.x = Mathf.MoveTowards(body.Vel.x, 0f, dt * 10f);

            if (state != St.Sit)
            {
                airVy = Mathf.Min(airVy, body.Vel.y);
                body.Move(room, dt);
                if (body.HitCeiling && body.Vel.y > 0f) body.Vel.y = 0f;
                if (body.Grounded && body.Vel.y < 0f) body.Vel.y = 0f;
                if ((body.HitLeft || body.HitRight) && state == St.Dash) EndDash();
            }

            if (body.Grounded && !wasGrounded)
            {
                if (airVy < -10f)
                {
                    Game.I.Fx.Dust(body.Pos - new Vector2(0, 0.6f), airVy < -18f ? 8 : 4);
                    Game.I.Sound.Play("land", 0.45f);
                    squash = 0.25f;
                }
                airVy = 0f;
            }
            if (body.Grounded) airVy = 0f;
            wasGrounded = body.Grounded;
        }

        float squash;

        // ------------------------------------------------------------------
        // Dash
        // ------------------------------------------------------------------

        float dashDir;

        void StartDash(float mx)
        {
            state = St.Dash;
            dashDir = mx != 0f ? Mathf.Sign(mx) : Facing;
            Facing = dashDir;
            dashTimer = DashTime;
            dashCd = DashCooldown;
            if (!body.Grounded) canAirDash = false;
            body.Vel = new Vector2(dashDir * DashSpeed, 0f);
            attackTimer = 0f;
            Game.I.Sound.Play("dash", 0.6f);
            Game.I.Fx.Smoke(body.Pos, new Color(0.9f, 0.6f, 0.3f, 0.4f), 0.5f, 3);
        }

        void UpdateDash(float dt)
        {
            dashTimer -= dt;
            body.Vel = new Vector2(dashDir * DashSpeed, 0f);
            afterimageTimer -= dt;
            if (afterimageTimer <= 0f)
            {
                afterimageTimer = 0.035f;
                Game.I.Fx.Afterimage(cloak, new Color(1f, 0.6f, 0.3f, 0.5f));
                Game.I.Fx.Afterimage(head, new Color(1f, 0.8f, 0.5f, 0.5f));
                Game.I.Fx.Ember(body.Pos + Random.insideUnitCircle * 0.3f);
            }
            if (dashTimer <= 0f) EndDash();
        }

        void EndDash()
        {
            state = St.Normal;
            body.Vel.x = dashDir * RunSpeed * 0.6f;
        }

        // ------------------------------------------------------------------
        // Attack
        // ------------------------------------------------------------------

        void StartAttack()
        {
            attackCd = AttackCooldown;
            attackTimer = AttackActive + 0.1f;
            slashTimer = 0.16f;
            hitThisSwing.Clear();
            pogoDone = false;
            if (Controls.Up) attackDir = 1;
            else if (Controls.Down && !body.Grounded) attackDir = -1;
            else attackDir = 0;
            Game.I.Sound.Play("slash", 0.55f, 0.12f);

            slash.enabled = true;
            blade.enabled = true;
            var st = slash.transform;
            if (attackDir == 0) { st.localPosition = new Vector3(0.35f, 0.05f, 0); st.localRotation = Quaternion.identity; st.localScale = new Vector3(1.05f, 0.85f, 1f); }
            else if (attackDir == 1) { st.localPosition = new Vector3(0f, 0.55f, 0); st.localRotation = Quaternion.Euler(0, 0, 90f); st.localScale = new Vector3(1f, 0.8f, 1f); }
            else { st.localPosition = new Vector3(0f, -0.45f, 0); st.localRotation = Quaternion.Euler(0, 0, -90f); st.localScale = new Vector3(1f, 0.8f, 1f); }
        }

        Rect AttackRect()
        {
            Vector2 c = body.Pos;
            if (attackDir == 0) return Combat.RectAt(c + new Vector2(Facing * 1.05f, 0.05f), new Vector2(1.75f, 1.25f));
            if (attackDir == 1) return Combat.RectAt(c + new Vector2(0f, 1.2f), new Vector2(1.35f, 1.7f));
            return Combat.RectAt(c + new Vector2(0f, -1.15f), new Vector2(1.35f, 1.7f));
        }

        void CheckAttackHits()
        {
            if (attackTimer <= 0.1f) return;
            var g = Game.I;
            var area = AttackRect();
            Vector2 dir = attackDir == 0 ? new Vector2(Facing, 0) : new Vector2(0, attackDir);
            bool landed = false;

            // copy: targets may unregister while being hit
            var targets = new List<IHittable>(Combat.Targets);
            foreach (var t in targets)
            {
                if (t == null || hitThisSwing.Contains(t) || !t.Vulnerable) continue;
                if (!t.Hurtbox.Overlaps(area)) continue;
                hitThisSwing.Add(t);
                var hb = t.Hurtbox;
                Vector2 point = new Vector2(Mathf.Clamp(body.Pos.x + dir.x * 0.9f, hb.xMin, hb.xMax), Mathf.Clamp(body.Pos.y + dir.y * 0.9f, hb.yMin, hb.yMax));
                if (t.TakeHit(new Hit { Damage = NailDamage, Dir = dir, Point = point, DownSlash = attackDir == -1 }))
                {
                    landed = true;
                    Wax = Mathf.Min(WaxMax, Wax + WaxPerHit);
                    g.Fx.HitBurst(point, dir, 9);
                    g.Sound.Play("hit", 0.7f, 0.1f);
                }
            }

            // parry projectiles
            foreach (var p in new List<Projectile>(Combat.Projectiles))
            {
                if (p == null || !p.Slashable) continue;
                if (!area.Overlaps(p.Rect)) continue;
                p.Deflect();
                landed = true;
                g.Sound.Play("gate", 0.35f, 0.2f);
            }

            // pogo off thorns
            if (!landed && attackDir == -1 && room.SpikeAt(area)) landed = true;

            if (landed)
            {
                g.HitStop(0.05f);
                g.Cam.Shake(0.14f);
                if (attackDir == -1 && !pogoDone)
                {
                    pogoDone = true;
                    body.Vel.y = 16f;
                    jumpCut = false;
                    pogoGrace = 0.12f;
                    canAirDash = true;
                    canDouble = true;
                }
                else if (attackDir == 0 && recoilTimer <= 0f)
                {
                    recoilTimer = 0.08f;
                    recoilVx = -Facing * 7f;
                }
            }
        }

        // ------------------------------------------------------------------
        // Flame: spell and focus
        // ------------------------------------------------------------------

        void TryCast()
        {
            if (!hasSpell || Wax < WaxCost) return;
            Wax -= WaxCost;
            state = St.Cast;
            stateTimer = 0.28f;
            body.Vel = new Vector2(-Facing * 5f, body.Grounded ? 0f : Mathf.Max(body.Vel.y, 2f));
            var p = FlareBolt.Spawn(body.Pos + new Vector2(Facing * 0.6f, 0.15f), Facing, SpellDamage);
            p.transform.SetParent(room.transform, true);
            Game.I.Sound.Play("flame", 0.8f);
            Game.I.Cam.Shake(0.2f);
            Game.I.Fx.FireBurst(body.Pos + new Vector2(Facing * 0.6f, 0.15f), 0.6f, 10);
        }

        void StartFocus()
        {
            state = St.Focus;
            focusTimer = 0f;
            body.Vel.x = 0f;
            Game.I.Sound.Play("focus", 0.6f, 0f);
        }

        void UpdateFocus(float dt)
        {
            body.Vel.x = 0f;
            if (!Controls.FlameHeld || !body.Grounded || Wax < WaxCost || Flames >= MaxFlames) { EndFocus(); return; }
            focusTimer += dt;
            if (Random.value < 0.6f) Game.I.Fx.Converge(body.Pos + new Vector2(0, 0.4f), new Color(1f, 0.85f, 0.6f, 1f), 1.6f, 1);
            if (focusTimer >= FocusTime)
            {
                focusTimer = 0f;
                Wax -= WaxCost;
                Flames = Mathf.Min(MaxFlames, Flames + 1);
                Game.I.Sound.Play("heal", 0.7f, 0f);
                Game.I.Fx.Motes(body.Pos + new Vector2(0, 0.5f), new Color(1f, 0.85f, 0.5f, 1f), 14, 0.6f);
                Game.I.Fx.RingPulse(body.Pos + new Vector2(0, 0.6f), new Color(1f, 0.9f, 0.7f, 0.8f), 0.4f, 2.2f, 0.4f);
                flasher.Flash(new Color(1f, 0.95f, 0.8f), 0.8f);
                if (Wax >= WaxCost && Flames < MaxFlames) Game.I.Sound.Play("focus", 0.6f, 0f);
            }
        }

        void EndFocus()
        {
            if (state == St.Focus) state = St.Normal;
            focusTimer = 0f;
        }

        // ------------------------------------------------------------------
        // Damage
        // ------------------------------------------------------------------

        public bool TakeDamage(int dmg, Vector2 from)
        {
            if (state == St.Dead || invuln > 0f) return false;
            if (state == St.Sit) state = St.Normal;
            Flames -= dmg;
            invuln = 1.3f;
            var g = Game.I;
            g.Sound.Play("hurt", 0.9f, 0.05f);
            g.Fx.WaxSplash(body.Pos + new Vector2(0, 0.3f));
            g.Cam.Shake(0.55f);
            g.Fx.Flash(new Color(0.05f, 0.03f, 0.03f), 0.35f);
            flasher.Flash(Color.white, 1f);
            if (state == St.Focus) EndFocus();
            if (state == St.Dash) EndDash();
            attackTimer = 0f;

            if (Flames <= 0)
            {
                Flames = 0;
                Die();
                return true;
            }
            g.HitStop(0.14f);
            float dir = Mathf.Sign(body.Pos.x - from.x);
            if (dir == 0f) dir = -Facing;
            body.Vel = new Vector2(dir * 9f, 9f);
            state = St.Hurt;
            stateTimer = 0.22f;
            return true;
        }

        void Die()
        {
            state = St.Dead;
            body.Vel = new Vector2(body.Vel.x * 0.3f, 6f);
            var g = Game.I;
            g.Sound.Play("death", 1f, 0f);
            g.Fx.Smoke(body.Pos + new Vector2(0, 0.8f), new Color(0.2f, 0.2f, 0.22f, 0.8f), 1.2f, 10);
            g.Fx.WaxSplash(body.Pos);
            g.Cam.Shake(0.8f);
            g.HitStop(0.25f);
            g.OnPlayerDeath();
        }

        public void SitOnBench()
        {
            state = St.Sit;
            body.Vel = Vector2.zero;
            Flames = MaxFlames;
        }

        public void Rest(Vector2 benchFeet)
        {
            body.Pos = benchFeet + new Vector2(0f, body.Half.y);
            body.Vel = Vector2.zero;
            state = St.Sit;
            Flames = MaxFlames;
            Game.I.Sound.Play("rest", 0.8f, 0f);
            Game.I.Fx.Motes(body.Pos, new Color(1f, 0.85f, 0.55f, 1f), 18, 1f);
            Game.I.RestAtBench();
        }

        // ------------------------------------------------------------------
        // World checks: hazards, doors, safe ground, interactables
        // ------------------------------------------------------------------

        void CheckWorld(Game g)
        {
            var rect = body.Rect;

            // hazards
            if (room.Hazard(new Rect(rect.x + 0.05f, rect.y, rect.width - 0.1f, rect.height - 0.1f)))
            {
                g.Sound.Play("hurt", 0.9f);
                g.Fx.WaxSplash(body.Pos);
                g.Fx.FireBurst(body.Pos, 0.6f, 8);
                invuln = 0f;
                Flames -= 1;
                flasher.Flash(Color.white, 1f);
                g.Cam.Shake(0.5f);
                if (Flames <= 0) { Flames = 0; Die(); return; }
                invuln = 1.2f;
                body.Vel = Vector2.zero;
                state = St.Hurt;
                stateTimer = 0.4f;
                g.OnHazard(lastSafe);
                return;
            }

            // safe ground memory (for hazard respawns)
            if (body.Grounded && state == St.Normal)
            {
                safeTimer += Time.deltaTime;
                if (safeTimer > 0.15f && IsSafeFooting()) lastSafe = body.Pos;
            }
            else safeTimer = 0f;

            // doors
            DoorLinkCheck(g, rect);

            // interactables
            IInteractable best = null;
            float bestD = float.MaxValue;
            foreach (var it in room.Interactables)
            {
                if (it == null || !it.CanInteract) continue;
                float d = Vector2.Distance(it.Position, body.Pos);
                if (d < it.Radius && d < bestD) { bestD = d; best = it; }
            }
            g.Hud.SetPrompt(best != null && state == St.Normal ? best.Prompt : null, best != null ? best.Position : Vector2.zero);
            if (best != null && state == St.Normal && body.Grounded && Controls.InteractPressed)
            {
                LookOffset = 0f;
                best.Interact();
            }
        }

        void DoorLinkCheck(Game g, Rect rect)
        {
            World.DoorLink link;
            bool inDoor = room.DoorAt(rect, out link);
            if (!doorArmed) { if (!inDoor) doorArmed = true; return; }
            if (inDoor) g.UseDoor(link);
        }

        bool IsSafeFooting()
        {
            int y = Mathf.RoundToInt(body.Bottom) - 1;
            int cx = Mathf.FloorToInt(body.Pos.x);
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                int k = room.KindAt(x, y);
                if (k != Room.Solid && k != Room.Platform) return false;
                int above = room.KindAt(x, y + 1);
                if (above == Room.Spikes || above == Room.Lava) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Animation
        // ------------------------------------------------------------------

        void Animate(float dt)
        {
            if (rig == null) return;
            var v = body.Vel;
            bool grounded = body.Grounded;
            rig.localScale = new Vector3(Facing, 1f, 1f);

            float bob = 0f, lean = 0f, sx = 1f, sy = 1f;
            float legA = 0f, legB = 0f;

            if (state == St.Sit)
            {
                bob = -0.18f;
                legA = 70f; legB = 60f;
                sy = 0.95f;
            }
            else if (state == St.Dead)
            {
                sy = 0.9f;
                lean = -20f;
            }
            else if (state == St.Dash)
            {
                sx = 1.25f; sy = 0.82f; lean = -12f;
                legA = 60f; legB = 40f;
            }
            else if (!grounded)
            {
                float k = Mathf.Clamp(v.y / 18f, -1f, 1f);
                sy = 1f + k * 0.07f; sx = 1f - k * 0.05f;
                legA = 25f + k * 15f; legB = -10f + k * 10f;
                lean = -k * 4f;
            }
            else if (Mathf.Abs(v.x) > 0.5f)
            {
                runPhase += dt * 15f;
                legA = Mathf.Sin(runPhase) * 38f;
                legB = -legA;
                bob = Mathf.Abs(Mathf.Sin(runPhase)) * 0.05f;
                lean = -6f;
            }
            else
            {
                bob = Mathf.Sin(animT * 2.2f) * 0.015f;
                sy = 1f + Mathf.Sin(animT * 2.2f) * 0.012f;
            }

            if (state == St.Focus)
            {
                sy = 0.94f + Mathf.Sin(animT * 30f) * 0.01f;
                bob -= 0.05f;
            }
            if (state == St.Cast) { lean = 10f; sx = 0.9f; }
            if (state == St.Hurt) { lean = 14f; }

            squash = Mathf.Max(0f, squash - dt * 2.5f);
            sy -= squash * 0.5f; sx += squash * 0.4f;

            cloakT.localPosition = new Vector3(0f, 0.02f + bob, 0f);
            cloakT.localScale = new Vector3(0.78f * sx, 0.78f * sy, 1f);
            cloakT.localRotation = Quaternion.Euler(0, 0, lean);
            headT.localPosition = new Vector3(lean * -0.004f, -0.06f + bob * 1.2f + (sy - 1f) * 0.5f, 0f);
            headT.localRotation = Quaternion.Euler(0, 0, lean * 0.6f + (attackTimer > 0f ? (attackDir == 1 ? 8f : attackDir == -1 ? -8f : -4f) : 0f));
            legL.localRotation = Quaternion.Euler(0, 0, legA);
            legR.localRotation = Quaternion.Euler(0, 0, legB);
            legL.localPosition = new Vector3(-0.1f, -0.3f + bob, 0);
            legR.localPosition = new Vector3(0.1f, -0.3f + bob, 0);

            // flame: flicker, leans against motion, dims with health, flares with wax
            float hp = MaxFlames > 0 ? Flames / (float)MaxFlames : 0f;
            float flick = 1f + Mathf.Sin(animT * 17f) * 0.05f + Mathf.Sin(animT * 29f + 1.3f) * 0.04f;
            float size = state == St.Dead ? Mathf.MoveTowards(flameT.localScale.y, 0f, dt * 1.5f) : (0.3f + 0.3f * hp + (state == St.Focus ? 0.15f : 0f)) * flick;
            flameT.localScale = new Vector3(size * (0.95f + Mathf.Sin(animT * 23f) * 0.05f), size, 1f);
            float flameLean = Mathf.Clamp(-v.x * Facing * 1.8f, -30f, 30f) + Mathf.Clamp(-v.y * 0.4f, -10f, 10f);
            flameT.localRotation = Quaternion.Slerp(flameT.localRotation, Quaternion.Euler(0, 0, flameLean), 1f - Mathf.Exp(-10f * dt));

            LightLevel = state == St.Dead ? 0f : 0.35f + 0.65f * hp;
            glowBig.transform.localScale = Vector3.one * (5f + 5f * LightLevel) * flick;
            glowBig.color = new Color(1f, 0.7f, 0.4f, 0.12f + 0.06f * LightLevel);
            glowSmall.transform.localScale = Vector3.one * (1.8f + 1f * LightLevel) * flick;
            glowBig.transform.localPosition = headT.localPosition + new Vector3(0, 0.65f, 0);
            glowSmall.transform.localPosition = headT.localPosition + new Vector3(0, 0.7f, 0);

            // blade & slash
            slashTimer -= dt;
            if (slashTimer > 0f)
            {
                float t = 1f - slashTimer / 0.16f;
                slash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1.2f - t * 1.2f));
                float swing = Mathf.Lerp(70f, -70f, Mathf.SmoothStep(0f, 1f, t * 1.4f));
                if (attackDir == 1) swing = Mathf.Lerp(160f, 20f, Mathf.SmoothStep(0f, 1f, t * 1.4f));
                if (attackDir == -1) swing = Mathf.Lerp(-20f, -160f, Mathf.SmoothStep(0f, 1f, t * 1.4f));
                bladeT.localRotation = Quaternion.Euler(0, 0, swing);
                bladeT.localPosition = new Vector3(0.1f, 0.05f, 0);
            }
            else
            {
                slash.enabled = false;
                blade.enabled = false;
            }

            // invulnerability blink
            blink += dt;
            bool visible = invuln <= 0f || state == St.Dead || Mathf.Repeat(blink, 0.12f) < 0.08f;
            head.enabled = cloak.enabled = legLR.enabled = legRR.enabled = visible;
        }
    }
}
