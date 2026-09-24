using System.Collections;
using AshenWick.Art;
using UnityEngine;

namespace AshenWick
{
    /// <summary>
    /// ТАЛЛОУ, Король-Огарок. The Tallow King who bound the Great Hearth to his own heart
    /// and burnt down to a stub rather than let the dark come.
    ///
    /// Phase 1 — "Дуэль": Dash Slash, Triple Combo, Rising Plunge, Ember Wave.
    /// Phase 2 (66%) — "Пепельная буря": spear rain, wax pillars, homing embers mixed into the duel.
    /// Phase 3 (33%) — "Угасание": he rises above the arena: radial fire beams, orb bursts,
    ///                  sword rain with gaps, and plunges from the sky.
    /// At zero he falls to his knees. Niv must deliver the last blow himself.
    /// </summary>
    public sealed class CinderKing : Boss
    {
        Transform bodyT, head, sword;
        SpriteRenderer swordSr, heartGlow, arenaGlow;
        float swordAngle = -70f, swordTarget = -70f, swordSpeed = 10f;
        float squash, hover;
        bool floating, kneeling;
        bool swordActive;
        int swordDamage = 1;

        protected override void Build()
        {
            MaxHp = 380;
            musicId = 12;
            phaseAt = new[] { 0.66f, 0.33f };
            body.Half = new Vector2(0.85f, 1.75f);
            vis.localScale = Vector3.one * 0.95f;
            bodyT = Gfx.Part(vis, "body", Gfx.S("KingBody", ArtLibrary.KingBody), new Vector2(0f, -1.84f), Layer.Boss).transform;
            head = Gfx.Part(vis, "head", Gfx.S("KingHead", ArtLibrary.KingHead), new Vector2(0f, 1.15f), Layer.Boss + 1).transform;
            sword = Gfx.Part(vis, "sword", Gfx.S("KingSword", ArtLibrary.KingSword), new Vector2(0.55f, 0.25f), Layer.Boss + 3).transform;
            swordSr = sword.GetComponent<SpriteRenderer>();
            heartGlow = Gfx.Glow(vis, new Vector2(0f, 0.2f), 3f, new Color(1f, 0.4f, 0.15f, 0.4f), Layer.Boss + 2);
            heartGlow.gameObject.AddComponent<Flicker>().Setup(0.4f, 0.12f, 0f);
            // crown flames glow
            Gfx.Glow(head, new Vector2(0f, 1.6f), 3f, new Color(1f, 0.6f, 0.3f, 0.3f), Layer.Boss).gameObject.AddComponent<Flicker>().Setup(0.3f, 0.1f, 1f);
        }

        protected override void OnSpawned()
        {
            facing = 1f; // back turned to the entrance
            arenaGlow = Gfx.Glow(room.transform, new Vector2(room.W * 0.5f, floorY + 6f), 40f, new Color(1f, 0.3f, 0.1f, 0.05f), Layer.BackProps);
        }

        public override Rect Hurtbox { get { return Combat.RectAt(body.Pos + new Vector2(0f, 0.2f), new Vector2(1.7f, 3.4f)); } }

        public override bool Vulnerable { get { return (fighting && !dead) || kneeling; } }

        Vector2 SwordTip { get { return sword.TransformPoint(new Vector3(4.9f, 0f, 0f)); } }

        protected override void Animate(float dt)
        {
            vis.localScale = new Vector3(0.95f * facing, 0.95f, 1f);
            swordAngle = Mathf.Lerp(swordAngle, swordTarget, 1f - Mathf.Exp(-swordSpeed * dt));
            sword.localRotation = Quaternion.Euler(0, 0, swordAngle);
            squash = Mathf.MoveTowards(squash, 0f, dt * 2.5f);
            float breathe = Mathf.Sin(t * 1.6f) * 0.015f;
            bodyT.localScale = new Vector3(1f + squash * 0.15f, 1f - squash * 0.2f + breathe, 1f);
            head.localPosition = new Vector3(0f, 1.15f - squash * 0.7f + breathe * 2f + (kneeling ? -0.9f : 0f), 0f);
            head.localRotation = Quaternion.Euler(0, 0, kneeling ? -18f : Mathf.Sin(t * 1.1f) * 2.5f);
            if (floating)
            {
                hover += dt;
                vis.localPosition = new Vector3(0, Mathf.Sin(hover * 2f) * 0.15f, 0);
                if (Random.value < 0.5f) Game.I.Fx.Ember(body.Pos + new Vector2(Random.Range(-1f, 1f), -1.8f));
            }
            // live sword hitbox (a rotated sword approximated with 3 boxes along its length)
            if (swordActive && Game.I.Live)
            {
                for (int i = 1; i <= 3; i++)
                {
                    Vector2 p = sword.TransformPoint(new Vector3(1.4f * i, 0f, 0f));
                    Combat.HurtPlayer(Combat.RectAt(p, new Vector2(1.3f, 1.3f)), swordDamage, body.Pos);
                }
            }
            if (fighting && Random.value < 0.1f * Phase) Game.I.Fx.Ember(SwordTip);
        }

        void SwordOn(int dmg = 1) { swordActive = true; swordDamage = dmg; }
        void SwordOff() { swordActive = false; }

        // ------------------------------------------------------------------

        protected override IEnumerator Intro()
        {
            var g = Game.I;
            g.Cam.Focus(body.Pos + new Vector2(0, 1.5f), 0.6f);
            yield return new WaitForSeconds(0.6f);
            // the crown flames flare
            g.Sound.Play("fire", 0.9f, 0f);
            g.Fx.FireBurst(head.position + new Vector3(0, 1.6f, 0), 1f, 20);
            yield return new WaitForSeconds(0.7f);
            facing = -1f;
            squash = 0.3f;
            g.Sound.Play("gate", 0.7f, 0f);
            yield return new WaitForSeconds(0.5f);
            // he drags his sword up from the ash
            swordSpeed = 3f; swordTarget = 100f;
            for (int i = 0; i < 20; i++) { g.Fx.Ember(SwordTip); yield return new WaitForSeconds(0.04f); }
            Roar(1f);
            g.Fx.Flash(new Color(1f, 0.5f, 0.2f), 0.4f);
            g.Cam.Punch(0.1f);
            yield return new WaitForSeconds(0.8f);
            swordSpeed = 8f; swordTarget = -70f;
            g.Cam.Focus(body.Pos, 0f);
            yield return new WaitForSeconds(0.3f);
        }

        protected override IEnumerator Brain()
        {
            FacePlayer();
            float d = Mathf.Abs(PlayerPos.x - body.Pos.x);
            string a;
            if (Phase == 1) a = d > 7f ? Pick("dash", "plunge", "wave") : Pick("combo", "dash", "plunge", "wave", "combo");
            else if (Phase == 2) a = Pick("dash", "combo", "plunge", "spears", "pillars", "embers", "wave");
            else a = Pick("beams", "orbs", "swordrain", "skyplunge", "beams", "embers");

            switch (a)
            {
                case "dash": yield return DashSlash(); break;
                case "combo": yield return TripleCombo(); break;
                case "plunge": yield return RisingPlunge(); break;
                case "wave": yield return EmberWave(); break;
                case "spears": yield return SpearRain(); break;
                case "pillars": yield return WaxPillars(); break;
                case "embers": yield return HomingEmbers(); break;
                case "beams": yield return Beams(); break;
                case "orbs": yield return OrbBurst(); break;
                case "swordrain": yield return SwordRain(); break;
                case "skyplunge": yield return SkyPlunge(); break;
            }
            yield return Wait(Phase == 1 ? 0.45f : 0.3f);
        }

        // ---------------- Phase 1 ----------------

        IEnumerator DashSlash()
        {
            if (floating) yield break;
            FacePlayer();
            swordSpeed = 9f; swordTarget = 160f;
            squash = 0.3f;
            yield return Wait(0.25f);
            Telegraph(SwordTip, new Color(1f, 0.8f, 0.5f, 1f));
            yield return Wait(Phase == 1 ? 0.35f : 0.25f);
            swordSpeed = 30f; swordTarget = -5f;
            Game.I.Sound.Play("dash", 1f);
            Game.I.Sound.Play("slash", 1f);
            SwordOn(2);
            float time = 0f;
            while (time < 0.42f)
            {
                float dt = Time.deltaTime;
                time += dt;
                MoveGround(dt, facing * 25f * speedMul);
                if (Time.frameCount % 2 == 0)
                {
                    Game.I.Fx.Afterimage(bodyT.GetComponent<SpriteRenderer>(), new Color(1f, 0.5f, 0.2f, 0.4f), 0.25f);
                    Game.I.Fx.Afterimage(swordSr, new Color(1f, 0.7f, 0.3f, 0.5f), 0.25f);
                }
                if (body.HitLeft || body.HitRight || body.Pos.x <= arenaMin + body.Half.x + 0.05f || body.Pos.x >= arenaMax - body.Half.x - 0.05f) break;
                yield return null;
            }
            SwordOff();
            body.Vel.x = 0f;
            squash = 0.3f;
            yield return Wait(0.4f);
            swordSpeed = 6f; swordTarget = -70f;
        }

        IEnumerator TripleCombo()
        {
            if (floating) yield break;
            FacePlayer();
            if (Mathf.Abs(PlayerPos.x - body.Pos.x) > 3.2f)
                yield return WalkTo(PlayerPos.x - facing * 2.6f, 6f, 1f, 0.4f);
            float[] up = { 130f, -120f, 150f };
            float[] down = { -40f, 60f, -30f };
            for (int i = 0; i < 3; i++)
            {
                FacePlayer();
                swordSpeed = 12f; swordTarget = up[i];
                Game.I.Fx.Converge(SwordTip, new Color(1f, 0.7f, 0.4f, 1f), 1.2f, 3);
                yield return Wait(Phase == 1 ? 0.3f : 0.22f);
                Game.I.Sound.Play("slash", 1f, 0.1f);
                swordSpeed = 35f; swordTarget = down[i];
                SwordOn(1);
                float time = 0f;
                while (time < 0.14f)
                {
                    float dt = Time.deltaTime;
                    time += dt;
                    MoveGround(dt, facing * 9f);
                    yield return null;
                }
                body.Vel.x = 0f;
                SwordOff();
                if (i == 2)
                {
                    Game.I.Cam.Shake(0.4f);
                    Shockwaves(new Vector2(SwordTip.x, floorY + 0.5f), false, 13f);
                }
                yield return Wait(0.12f);
            }
            yield return Wait(0.45f);
            swordSpeed = 6f; swordTarget = -70f;
        }

        IEnumerator RisingPlunge()
        {
            if (floating) yield break;
            FacePlayer();
            squash = 0.5f;
            swordSpeed = 8f; swordTarget = 60f;
            yield return Wait(0.3f);
            Game.I.Sound.Play("jump", 1f, 0f);
            Game.I.Fx.Dust(new Vector2(body.Pos.x, floorY), 8, 1.5f);
            // rise toward the player
            float tx = ClampX(PlayerPos.x, 1f);
            float riseTime = 0.45f;
            Vector2 start = body.Pos;
            Vector2 apex = new Vector2(Mathf.Lerp(body.Pos.x, tx, 0.9f), floorY + 8.5f);
            float e = 0f;
            SwordOn(1);
            while (e < riseTime)
            {
                e += Time.deltaTime;
                float k = e / riseTime;
                body.Pos = Vector2.Lerp(start, apex, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            SwordOff();
            // hang: the sword turns downward and blazes
            swordSpeed = 14f; swordTarget = -90f;
            Telegraph(SwordTip, new Color(1f, 0.6f, 0.2f, 1f), 2f);
            e = 0f;
            while (e < 0.35f / speedMul)
            {
                e += Time.deltaTime;
                body.Pos.x = Mathf.MoveTowards(body.Pos.x, ClampX(PlayerPos.x, 1f), Time.deltaTime * 3f);
                yield return null;
            }
            // plunge
            SwordOn(2);
            Game.I.Sound.Play("dash", 1f);
            while (body.Pos.y > floorY + body.Half.y + 0.01f)
            {
                body.Pos.y = Mathf.Max(floorY + body.Half.y + 0.01f, body.Pos.y - 32f * Time.deltaTime);
                if (Time.frameCount % 2 == 0) Game.I.Fx.Afterimage(bodyT.GetComponent<SpriteRenderer>(), new Color(1f, 0.5f, 0.2f, 0.35f), 0.2f);
                yield return null;
            }
            SwordOff();
            body.Vel = Vector2.zero;
            squash = 0.6f;
            var g = Game.I;
            g.Sound.Play("slam", 1f);
            g.Cam.Shake(0.8f);
            g.Cam.Punch(0.04f);
            g.Fx.FireBurst(new Vector2(body.Pos.x, floorY + 0.3f), 1.4f, 20);
            g.Fx.Dust(new Vector2(body.Pos.x, floorY), 14, 2f);
            Combat.HurtPlayer(Combat.RectAt(new Vector2(body.Pos.x, floorY + 0.8f), new Vector2(3.5f, 1.6f)), 2, body.Pos);
            Shockwaves(new Vector2(body.Pos.x, floorY + 0.5f), true, 13f);
            yield return Wait(0.7f);
            swordSpeed = 6f; swordTarget = -70f;
        }

        IEnumerator EmberWave()
        {
            if (floating) yield break;
            FacePlayer();
            swordSpeed = 8f; swordTarget = 120f;
            Telegraph(SwordTip, new Color(1f, 0.6f, 0.3f, 1f));
            yield return Wait(0.45f);
            int waves = Phase >= 2 ? 3 : 2;
            for (int i = 0; i < waves; i++)
            {
                swordSpeed = 35f; swordTarget = -60f;
                yield return new WaitForSeconds(0.08f);
                Game.I.Sound.Play("slam", 0.8f);
                Game.I.Cam.Shake(0.4f);
                Shockwaves(new Vector2(body.Pos.x + facing * 1.5f, floorY + 0.5f), true, 11f + i * 2f);
                if (i < waves - 1)
                {
                    swordSpeed = 12f; swordTarget = 110f;
                    yield return Wait(0.4f);
                }
            }
            yield return Wait(0.5f);
            swordSpeed = 6f; swordTarget = -70f;
        }

        // ---------------- Phase 2 ----------------

        IEnumerator SpearRain()
        {
            swordSpeed = 6f; swordTarget = 90f;
            Roar(0.4f);
            yield return Wait(0.4f);
            bool leftToRight = PlayerPos.x > (arenaMin + arenaMax) * 0.5f;
            int n = 12;
            int gap = Random.Range(3, n - 3);
            for (int i = 0; i < n; i++)
            {
                if (i == gap || i == gap + 1) continue;
                float k = i / (float)(n - 1);
                float x = Mathf.Lerp(arenaMin + 0.8f, arenaMax - 0.8f, leftToRight ? k : 1f - k);
                FallingHazard.Spawn(room, x, 0.55f + i * 0.09f, true);
            }
            yield return Wait(1.6f);
            swordSpeed = 6f; swordTarget = -70f;
        }

        IEnumerator WaxPillars()
        {
            swordSpeed = 10f; swordTarget = -90f;
            squash = 0.3f;
            Game.I.Sound.Play("slam", 0.7f);
            yield return Wait(0.3f);
            for (int i = 0; i < 4; i++)
            {
                FireColumn.Spawn(room, new Vector2(ClampX(PlayerPos.x, 0.8f), floorY), 0.7f, 0.6f, 9f, 1.5f);
                yield return Wait(0.45f);
            }
            yield return Wait(0.6f);
            swordSpeed = 6f; swordTarget = -70f;
        }

        IEnumerator HomingEmbers()
        {
            swordSpeed = 8f; swordTarget = 70f;
            heartGlow.transform.localScale = Vector3.one * 5f;
            Telegraph(body.Pos + new Vector2(0, 0.2f), new Color(1f, 0.4f, 0.1f, 1f), 2f);
            yield return Wait(0.5f);
            int n = Phase == 3 ? 4 : 3;
            for (int i = 0; i < n; i++)
            {
                float a = (90f + (i - (n - 1) * 0.5f) * 40f) * Mathf.Deg2Rad;
                var p = Projectile.Spawn(room, "EmberOrb", ArtLibrary.EmberOrb, body.Pos + new Vector2(0, 0.3f), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f, 0.35f, 1.2f, true);
                p.Homing = true;
                p.HomingTurn = 1.6f;
                p.FaceVelocity = false;
                p.Life = 5.5f;
                p.DieOnTerrain = false;
                p.AddGlow(new Color(1f, 0.4f, 0.1f, 0.4f), 2.4f);
                Game.I.Sound.Play("shoot", 0.7f);
                yield return Wait(0.15f);
            }
            heartGlow.transform.localScale = Vector3.one * 3f;
            yield return Wait(0.8f);
            swordSpeed = 6f; swordTarget = -70f;
        }

        // ---------------- Phase 3 ----------------

        IEnumerator Beams()
        {
            yield return FlyTo(new Vector2((arenaMin + arenaMax) * 0.5f, floorY + 9f), 10f, 1.5f);
            swordSpeed = 6f; swordTarget = 90f;
            heartGlow.transform.localScale = Vector3.one * 6f;
            Game.I.Sound.Play("focus", 0.9f, 0f);
            // two volleys of static radial beams; the second is offset to the gaps of the first
            for (int v = 0; v < 2; v++)
            {
                int n = 5;
                float offset = v == 0 ? Random.Range(-8f, 8f) : 12f;
                for (int i = 0; i < n; i++)
                {
                    float ang = -90f + (i - (n - 1) * 0.5f) * 24f + offset;
                    SweepBeam.Spawn(room, body.Pos, ang, ang, 0.85f / speedMul, 0.5f, 30f);
                }
                yield return Wait(1.45f);
            }
            // then one slow sweeping beam across the floor from one side — jump over its tip!
            float from = PlayerPos.x < body.Pos.x ? -170f : -10f;
            float to = from < -90f ? -95f : -85f;
            SweepBeam.Spawn(room, body.Pos + new Vector2(0, 0.3f), from, to, 0.7f, 1.2f, 30f);
            yield return Wait(2f);
            heartGlow.transform.localScale = Vector3.one * 3f;
        }

        IEnumerator OrbBurst()
        {
            yield return FlyTo(new Vector2(ClampX(PlayerPos.x, 3f), floorY + 8f), 10f, 1.2f);
            Game.I.Sound.Play("focus", 0.8f, 0f);
            float e = 0f;
            while (e < 0.6f)
            {
                e += Time.deltaTime;
                Game.I.Fx.Converge(body.Pos, new Color(1f, 0.5f, 0.2f, 1f), 3f, 2);
                yield return null;
            }
            for (int r = 0; r < 3; r++)
            {
                int n = 12;
                float rot = r * 15f;
                for (int i = 0; i < n; i++)
                {
                    float a = (rot + i * 360f / n) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var p = Projectile.Spawn(room, "EmberOrb", ArtLibrary.EmberOrb, body.Pos + dir, dir * 6.5f, 0.3f, 0.9f, true);
                    p.FaceVelocity = false;
                    p.Life = 6f;
                    p.DieOnTerrain = true;
                }
                Game.I.Sound.Play("boom", 0.5f);
                Game.I.Fx.RingPulse(body.Pos, new Color(1f, 0.6f, 0.3f, 0.8f), 0.5f, 5f, 0.4f);
                yield return Wait(0.55f);
            }
            yield return Wait(0.5f);
        }

        IEnumerator SwordRain()
        {
            yield return FlyTo(new Vector2((arenaMin + arenaMax) * 0.5f, floorY + 9.5f), 10f, 1.2f);
            swordSpeed = 5f; swordTarget = 90f;
            Roar(0.5f);
            for (int w = 0; w < 2; w++)
            {
                int n = 16;
                int gapA = Random.Range(1, n / 2 - 1), gapB = Random.Range(n / 2 + 1, n - 2);
                for (int i = 0; i < n; i++)
                {
                    if (Mathf.Abs(i - gapA) <= 0 || Mathf.Abs(i - gapB) <= 0) continue;
                    float x = Mathf.Lerp(arenaMin + 0.7f, arenaMax - 0.7f, i / (float)(n - 1));
                    FallingHazard.Spawn(room, x, 0.9f, true);
                }
                yield return Wait(1.6f);
            }
            swordSpeed = 6f; swordTarget = -70f;
        }

        IEnumerator SkyPlunge()
        {
            // vanish in a flare, reappear above Niv, plunge
            var g = Game.I;
            g.Fx.FireBurst(body.Pos, 1.5f, 20);
            g.Sound.Play("fire", 0.8f);
            vis.gameObject.SetActive(false);
            yield return Wait(0.4f);
            body.Pos = new Vector2(ClampX(PlayerPos.x, 1f), floorY + 10f);
            vis.gameObject.SetActive(true);
            g.Fx.FireBurst(body.Pos, 1.2f, 16);
            swordSpeed = 20f; swordTarget = -90f;
            Telegraph(SwordTip, new Color(1f, 0.6f, 0.2f, 1f), 2f);
            float e = 0f;
            while (e < 0.45f / speedMul)
            {
                e += Time.deltaTime;
                body.Pos.x = Mathf.MoveTowards(body.Pos.x, ClampX(PlayerPos.x, 1f), Time.deltaTime * 4f);
                yield return null;
            }
            SwordOn(2);
            while (body.Pos.y > floorY + body.Half.y + 0.01f)
            {
                body.Pos.y = Mathf.Max(floorY + body.Half.y + 0.01f, body.Pos.y - 34f * Time.deltaTime);
                yield return null;
            }
            SwordOff();
            squash = 0.6f;
            g.Sound.Play("slam", 1f);
            g.Cam.Shake(0.9f);
            g.Fx.FireBurst(new Vector2(body.Pos.x, floorY + 0.3f), 1.6f, 24);
            Combat.HurtPlayer(Combat.RectAt(new Vector2(body.Pos.x, floorY + 0.8f), new Vector2(3.5f, 1.6f)), 2, body.Pos);
            Shockwaves(new Vector2(body.Pos.x, floorY + 0.5f), true, 14f);
            yield return Wait(0.9f);
            // back to the sky
            yield return FlyTo(new Vector2(body.Pos.x, floorY + 8f), 9f, 1.5f);
        }

        // ---------------- Phase changes ----------------

        protected override IEnumerator PhaseChange(int phase)
        {
            var g = Game.I;
            SwordOff();
            swordSpeed = 5f; swordTarget = -90f;
            squash = 0.5f;
            yield return Wait(0.3f);
            g.SlowMotion(0.3f, 1f);
            Roar(1f);
            g.Fx.Flash(new Color(1f, 0.4f, 0.15f), 0.6f);
            g.Cam.Punch(0.12f);
            if (phase == 2)
            {
                g.Hud.Toast("«Горн не угаснет. Я не позволю».");
                arenaGlow.color = new Color(1f, 0.3f, 0.1f, 0.12f);
                speedMul = 1.12f;
                for (int i = 0; i < 20; i++) { g.Fx.FireBurst(body.Pos + Random.insideUnitCircle * 2.5f, 0.6f, 4); yield return new WaitForSeconds(0.04f); }
            }
            else
            {
                g.Hud.Toast("«Если нужно — я сожгу и пепел».");
                arenaGlow.color = new Color(1f, 0.25f, 0.08f, 0.22f);
                speedMul = 1.25f;
                floating = true;
                body.NoClip = true;
                // rises into the air, the floor erupts below
                for (float x = arenaMin + 1f; x < arenaMax - 1f; x += 3.2f)
                    FireColumn.Spawn(room, new Vector2(x, floorY), 1.1f, 0.5f, 6f, 1.2f);
                yield return FlyTo(new Vector2((arenaMin + arenaMax) * 0.5f, floorY + 8.5f), 6f, 2f);
            }
            yield return Wait(0.5f);
        }

        // ---------------- The end ----------------

        protected override void OnDefeated()
        {
            // no explosion: he falls to his knees and waits for the last blow
            dead = true;
            fighting = false;
            StopAllCoroutines();
            SwordOff();
            ClearHazards();
            StartCoroutine(Collapse());
        }

        IEnumerator Collapse()
        {
            var g = Game.I;
            g.Hud.HideBossBar();
            g.Sound.StopMusic(2f);
            g.SlowMotion(0.25f, 1.5f);
            g.Fx.Flash(Color.white, 0.8f);
            g.Cam.Shake(1f);
            g.Sound.Play("roar", 0.8f, 0f);
            floating = false;
            vis.localPosition = Vector3.zero;
            vis.gameObject.SetActive(true);
            // fall to the floor
            while (body.Pos.y > floorY + body.Half.y + 0.01f)
            {
                body.Pos.y = Mathf.Max(floorY + body.Half.y + 0.01f, body.Pos.y - 12f * Time.unscaledDeltaTime);
                yield return null;
            }
            g.Sound.Play("slam", 1f, 0f);
            g.Fx.Dust(new Vector2(body.Pos.x, floorY), 20, 2.5f);
            squash = 0.8f;
            swordSpeed = 3f; swordTarget = -95f;
            kneeling = true;
            bodyT.localPosition = new Vector3(0f, -2.3f, 0f);
            heartGlow.color = new Color(1f, 0.3f, 0.1f, 0.25f);
            yield return new WaitForSecondsRealtime(1.2f);
            g.Hud.Toast("Нанесите последний удар");
            Combat.Register(this);
            while (kneeling)
            {
                if (Random.value < 0.1f) g.Fx.Smoke(head.position + new Vector3(0, 1.5f, 0), new Color(0.2f, 0.2f, 0.22f, 0.5f), 0.6f, 1);
                yield return null;
            }
        }

        public override bool TakeHit(Hit h)
        {
            if (kneeling)
            {
                kneeling = false;
                Combat.Unregister(this);
                StartCoroutine(FinalBlow());
                return true;
            }
            return base.TakeHit(h);
        }

        IEnumerator FinalBlow()
        {
            var g = Game.I;
            g.HitStop(0.4f);
            g.Fx.Flash(Color.white, 1f);
            g.Cam.Shake(1f);
            g.Cam.Punch(0.15f);
            g.Sound.Play("shatter", 1f, 0f);
            g.Sound.Play("boom", 1f, 0f);
            yield return new WaitForSecondsRealtime(0.5f);
            g.Fx.BossExplosion(body.Pos);
            // the last coal falls from his chest
            yield return base.OnDeathVisual();
            g.OnBossDefeated(Id);
            yield return new WaitForSecondsRealtime(1.5f);
            g.BeginEnding();
        }
    }
}
