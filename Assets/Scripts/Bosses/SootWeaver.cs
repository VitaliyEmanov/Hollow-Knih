using System.Collections;
using AshenWick.Art;
using UnityEngine;

namespace AshenWick
{
    /// <summary>
    /// МОРРА, Ткачиха Сажи. The moth-widow who once wove veils to shield flames from the wind;
    /// the ash turned her love for light into a hunger to smother it.
    ///
    /// Phase 1: Dive, Feather Fan, Soot Ring, Summon Moths, Thread Drop.
    /// Phase 2 (60%): "Затмение" — she puts out the light. Only Niv's flame and her eyes remain.
    ///                Darkness comes and goes; attacks gain extra volleys.
    /// Phase 3 (25%): frenzy — chained triple dives and double rings.
    /// </summary>
    public sealed class SootWeaver : Boss
    {
        Transform wingL, wingR, bodyT, mask;
        SpriteRenderer eyeL, eyeR, thread, darkness;
        float flapSpeed = 5f, flapAmp = 0.25f, eyeGlow = 0.3f, darkAlpha, darkTarget;
        float eclipseTimer;
        bool eclipse;
        float topY;

        protected override void Build()
        {
            MaxHp = 240;
            musicId = 11;
            phaseAt = new[] { 0.6f, 0.25f };
            body.Half = new Vector2(0.85f, 1.25f);
            body.NoClip = true;
            vis.localScale = Vector3.one * 0.85f;
            wingL = Gfx.Part(vis, "wingL", Gfx.S("WeaverWing", ArtLibrary.WeaverWing), new Vector2(-0.25f, 0.55f), Layer.Boss - 1).transform;
            wingR = Gfx.Part(vis, "wingR", Gfx.S("WeaverWing", ArtLibrary.WeaverWing), new Vector2(0.25f, 0.55f), Layer.Boss - 1).transform;
            bodyT = Gfx.Part(vis, "body", Gfx.S("WeaverBody", ArtLibrary.WeaverBody), Vector2.zero, Layer.Boss).transform;
            mask = Gfx.Part(vis, "mask", Gfx.S("WeaverMask", ArtLibrary.WeaverMask), new Vector2(0f, 1.3f), Layer.Boss + 1).transform;
            eyeL = Gfx.Glow(mask, new Vector2(-0.17f, 0.58f), 1.2f, new Color(1f, 0.35f, 0.15f, 0.3f), Layer.Darkness + 1);
            eyeR = Gfx.Glow(mask, new Vector2(0.17f, 0.58f), 1.2f, new Color(1f, 0.35f, 0.15f, 0.3f), Layer.Darkness + 1);
        }

        protected override void OnSpawned()
        {
            topY = room.H - 1f;
            body.Pos = new Vector2((arenaMin + arenaMax) * 0.5f, topY - 2.2f);
            thread = Gfx.Part(room.transform, "thread", Gfx.S("TelegraphLine", ArtLibrary.TelegraphLine), Vector2.zero, Layer.Boss - 2, false);
            thread.color = new Color(0.8f, 0.8f, 0.85f, 0.7f);
            darkness = Gfx.Part(room.transform, "eclipse", Gfx.S("DarknessHole", ArtLibrary.DarknessHole), Vector2.zero, Layer.Darkness);
            darkness.transform.localScale = Vector3.one * 3.4f;
            darkness.color = new Color(1, 1, 1, 0);
            // folded, asleep
            flapAmp = 0f;
        }

        public override Rect Hurtbox { get { return Combat.RectAt(body.Pos + new Vector2(0f, 0.3f), new Vector2(1.8f, 2.9f)); } }

        float Floor { get { return floorY; } }

        protected override void Animate(float dt)
        {
            vis.localScale = new Vector3(0.85f * facing, 0.85f, 1f);
            float f = Mathf.Sin(t * flapSpeed);
            wingL.localScale = new Vector3(-1f, 0.55f + flapAmp * (0.5f + 0.5f * f), 1f);
            wingR.localScale = new Vector3(1f, 0.55f + flapAmp * (0.5f + 0.5f * f), 1f);
            wingL.localRotation = Quaternion.Euler(0, 0, -f * 12f * flapAmp);
            wingR.localRotation = Quaternion.Euler(0, 0, f * 12f * flapAmp);
            bodyT.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.4f) * 4f);
            mask.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.1f + 1f) * 5f);
            float e = eyeGlow * (0.85f + Mathf.Sin(t * 9f) * 0.15f);
            eyeL.color = eyeR.color = new Color(1f, 0.35f, 0.15f, e);
            eyeL.transform.localScale = eyeR.transform.localScale = Vector3.one * (0.8f + eyeGlow * 1.2f);

            // thread from the ceiling
            if (thread.enabled)
            {
                float top = topY;
                Vector2 anchor = body.Pos + new Vector2(0, 1.8f);
                thread.transform.position = new Vector3(anchor.x, anchor.y, 0);
                thread.transform.rotation = Quaternion.Euler(0, 0, 90f);
                thread.transform.localScale = new Vector3(Mathf.Max(0.1f, top - anchor.y + 1f), 0.35f, 1f);
            }

            // eclipse cycle
            if (fighting && Phase >= 2)
            {
                eclipseTimer -= dt;
                if (eclipseTimer <= 0f)
                {
                    eclipse = !eclipse;
                    eclipseTimer = eclipse ? (Phase == 3 ? 11f : 9f) : 5f;
                    darkTarget = eclipse ? 0.96f : 0f;
                    Game.I.Sound.Play(eclipse ? "wing" : "rest", 0.5f, 0f);
                }
            }
            darkAlpha = Mathf.MoveTowards(darkAlpha, darkTarget, dt * 0.8f);
            darkness.color = new Color(1, 1, 1, darkAlpha);
            var p = Player;
            if (p != null) darkness.transform.position = new Vector3(p.Center.x, p.Center.y + 0.5f, 0f);
            if (dead) darkTarget = 0f;
            if (fighting && Random.value < 0.15f) Game.I.Fx.AmbientAsh(body.Pos + Random.insideUnitCircle * 1.5f, 0);
        }

        void Hover(float time)
        {
            body.Pos += new Vector2(Mathf.Sin(t * 1.3f) * 0.6f, Mathf.Sin(t * 2.1f) * 0.8f) * Time.deltaTime;
        }

        IEnumerator HoverFor(float time)
        {
            float e = 0f;
            while (e < time) { e += Time.deltaTime; Hover(time); yield return null; }
        }

        // ------------------------------------------------------------------

        protected override IEnumerator Intro()
        {
            var g = Game.I;
            thread.enabled = true;
            g.Cam.Focus(body.Pos, 0.5f);
            // she descends on her thread, wrapped in her own wings
            Vector2 target = new Vector2(body.Pos.x, Floor + 7.5f);
            while (body.Pos.y > target.y + 0.05f)
            {
                body.Pos = Vector2.MoveTowards(body.Pos, target, Time.deltaTime * 2.2f);
                if (Random.value < 0.3f) g.Fx.AmbientAsh(body.Pos + Random.insideUnitCircle * 2f, 0);
                yield return null;
            }
            yield return new WaitForSeconds(0.4f);
            eyeGlow = 1f;
            g.Sound.Play("telegraph", 0.8f, 0f);
            yield return new WaitForSeconds(0.6f);
            // wings unfurl
            flapAmp = 1.4f; flapSpeed = 10f;
            thread.enabled = false;
            Roar(0.9f);
            g.Sound.Play("wing", 1f, 0f);
            for (int i = 0; i < 18; i++)
            {
                g.Fx.Smoke(body.Pos + Random.insideUnitCircle * 2.5f, new Color(0.15f, 0.14f, 0.18f, 0.7f), 1.2f, 1);
                yield return new WaitForSeconds(0.04f);
            }
            g.Cam.Focus(body.Pos, 0f);
            flapAmp = 0.8f; flapSpeed = 6f; eyeGlow = 0.35f;
        }

        protected override IEnumerator Brain()
        {
            FacePlayer();
            string a;
            if (Phase == 1) a = Pick("dive", "fan", "ring", "thread", "dive", "summon");
            else if (Phase == 2) a = Pick("dive", "fan", "ring", "thread", "summon", "fan");
            else a = Pick("dive3", "fan", "ring", "thread", "dive3");

            switch (a)
            {
                case "dive": yield return Dive(Phase == 2 ? 2 : 1); break;
                case "dive3": yield return Dive(3); break;
                case "fan": yield return FeatherFan(); break;
                case "ring": yield return SootRing(); break;
                case "summon": yield return Summon(); break;
                case "thread": yield return ThreadDrop(); break;
            }
            yield return HoverFor(Phase == 1 ? 0.7f : 0.4f);
        }

        IEnumerator Dive(int count)
        {
            for (int c = 0; c < count; c++)
            {
                float side = PlayerPos.x < (arenaMin + arenaMax) * 0.5f ? 1f : -1f;
                if (c % 2 == 1) side = -side;
                var start = new Vector2(ClampX(PlayerPos.x + side * 6.5f, 1.5f), topY - 2.4f);
                flapSpeed = 12f;
                yield return FlyTo(start, 16f, 1.1f);
                FacePlayer();
                // telegraph: the eyes flare, a thread of light marks the path
                Vector2 target = PlayerPos;
                Vector2 dir = (target - body.Pos).normalized;
                if (dir.y > -0.3f) dir = new Vector2(dir.x, -0.3f).normalized;
                var line = Gfx.Part(room.transform, "diveLine", Gfx.S("TelegraphLine", ArtLibrary.TelegraphLine), body.Pos, Layer.Darkness + 2, true);
                line.color = new Color(1f, 0.35f, 0.15f, 0.7f);
                line.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                line.transform.localScale = new Vector3(30f, 0.6f, 1f);
                eyeGlow = 1.2f;
                Game.I.Sound.Play("telegraph", 0.8f);
                float warn = Phase == 3 ? 0.32f : 0.45f;
                float e = 0f;
                while (e < warn / speedMul)
                {
                    e += Time.deltaTime;
                    line.color = new Color(1f, 0.35f, 0.15f, 0.3f + 0.5f * Mathf.Abs(Mathf.Sin(e * 25f)));
                    yield return null;
                }
                Destroy(line.gameObject);
                eyeGlow = 0.6f;
                flapAmp = 0.2f;
                Game.I.Sound.Play("dash", 0.9f);
                // dive
                float speed = 27f * speedMul;
                float time = 0f;
                while (time < 1.5f)
                {
                    float dt = Time.deltaTime;
                    time += dt;
                    body.Pos += dir * speed * dt;
                    if (Time.frameCount % 2 == 0) Game.I.Fx.Afterimage(bodyT.GetComponent<SpriteRenderer>(), new Color(0.6f, 0.6f, 0.7f, 0.35f), 0.2f);
                    if (body.Pos.y <= Floor + 1.5f || body.Pos.x < arenaMin + 1f || body.Pos.x > arenaMax - 1f) break;
                    yield return null;
                }
                body.Pos = new Vector2(ClampX(body.Pos.x, 1f), Mathf.Max(body.Pos.y, Floor + 1.5f));
                var g = Game.I;
                g.Cam.Shake(0.5f);
                g.Sound.Play("slam", 0.7f);
                g.Fx.Dust(new Vector2(body.Pos.x, Floor), 12, 2f);
                if (Phase >= 2)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        float ang = Mathf.Lerp(20f, 160f, i / 6f) * Mathf.Deg2Rad;
                        SpawnFeather(body.Pos + new Vector2(0, -0.6f), new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 9f);
                    }
                }
                flapAmp = 1f;
                yield return Wait(0.35f);
            }
            flapSpeed = 6f; flapAmp = 0.8f;
        }

        void SpawnFeather(Vector2 from, Vector2 v)
        {
            var p = Projectile.Spawn(room, "Feather", ArtLibrary.Feather, from, v, 0.25f, 1.1f);
            p.Life = 5f;
            p.DieOnTerrain = true;
            if (eclipse || Phase >= 2) p.GetComponentInChildren<SpriteRenderer>().sortingOrder = Layer.Darkness + 1;
        }

        IEnumerator FeatherFan()
        {
            float side = PlayerPos.x > (arenaMin + arenaMax) * 0.5f ? -1f : 1f;
            var pos = new Vector2(side < 0 ? arenaMin + 3f : arenaMax - 3f, topY - 3f);
            flapSpeed = 10f;
            yield return FlyTo(pos, 12f, 1.3f);
            FacePlayer();
            int volleys = Phase >= 2 ? 2 : 1;
            for (int v = 0; v < volleys; v++)
            {
                flapAmp = 1.8f; eyeGlow = 1f;
                Telegraph(body.Pos + new Vector2(0, 0.6f), new Color(0.9f, 0.9f, 1f, 0.8f), 2f);
                yield return Wait(0.5f);
                Game.I.Sound.Play("wing", 1f);
                Game.I.Sound.Play("shoot", 0.7f);
                Vector2 to = (PlayerPos - body.Pos).normalized;
                float baseAng = Mathf.Atan2(to.y, to.x);
                int n = Phase >= 2 ? 7 : 5;
                float spread = 55f * Mathf.Deg2Rad;
                float offset = v == 1 ? spread / (n - 1) * 0.5f : 0f;
                for (int i = 0; i < n; i++)
                {
                    float a = baseAng - spread * 0.5f + spread * i / (n - 1) + offset;
                    SpawnFeather(body.Pos + new Vector2(0, 0.3f), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 11.5f);
                }
                flapAmp = 0.6f; eyeGlow = 0.4f;
                yield return Wait(0.45f);
            }
            flapSpeed = 6f; flapAmp = 0.8f;
        }

        IEnumerator SootRing()
        {
            var center = new Vector2((arenaMin + arenaMax) * 0.5f, Floor + 6.5f);
            yield return FlyTo(center, 12f, 1.5f);
            eyeGlow = 1f;
            flapAmp = 0.3f;
            Game.I.Sound.Play("focus", 0.8f, 0f);
            float e = 0f;
            while (e < 0.7f / speedMul)
            {
                e += Time.deltaTime;
                Game.I.Fx.Converge(body.Pos, new Color(0.4f, 0.4f, 0.45f, 1f), 3f, 2);
                yield return null;
            }
            int rings = Phase == 3 ? 2 : 1;
            for (int r = 0; r < rings; r++)
            {
                int n = 14;
                float rot = Random.value * 360f + r * 180f / n;
                Game.I.Sound.Play("boom", 0.5f);
                Game.I.Cam.Shake(0.3f);
                for (int i = 0; i < n; i++)
                {
                    float a = (rot + i * 360f / n) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var p = Projectile.Spawn(room, "AshGlob", ArtLibrary.AshGlob, body.Pos + dir * 0.8f, dir * 5.8f, 0.3f, 1.3f);
                    p.FaceVelocity = false;
                    p.Spin = 200f;
                    p.Life = 7f;
                    p.DieOnTerrain = true;
                    p.AddGlow(new Color(1f, 0.4f, 0.15f, 0.3f), 1.4f);
                    if (eclipse) p.GetComponentInChildren<SpriteRenderer>().sortingOrder = Layer.Darkness + 1;
                }
                Game.I.Fx.RingPulse(body.Pos, new Color(0.7f, 0.7f, 0.8f, 0.8f), 0.5f, 6f, 0.5f);
                yield return Wait(0.6f);
            }
            eyeGlow = 0.35f; flapAmp = 0.8f;
        }

        IEnumerator Summon()
        {
            int alive = room.GetComponentsInChildren<SootMoth>().Length;
            if (alive >= 3) { yield return FeatherFan(); yield break; }
            flapAmp = 1.8f; flapSpeed = 14f;
            Roar(0.4f);
            yield return Wait(0.5f);
            for (int i = 0; i < 2; i++)
            {
                var pos = body.Pos + new Vector2(i == 0 ? -1.8f : 1.8f, 0.6f);
                room.Spawn<SootMoth>(pos);
                Game.I.Fx.Smoke(pos, new Color(0.15f, 0.14f, 0.18f, 0.8f), 1f, 6);
            }
            yield return Wait(0.4f);
            flapAmp = 0.8f; flapSpeed = 6f;
        }

        IEnumerator ThreadDrop()
        {
            var above = new Vector2(ClampX(PlayerPos.x, 1.5f), topY - 1.6f);
            yield return FlyTo(above, 18f, 1f);
            thread.enabled = true;
            flapAmp = 0.1f;
            eyeGlow = 1.2f;
            Telegraph(body.Pos + new Vector2(0, -1f), new Color(1f, 0.4f, 0.2f, 0.9f), 1.5f);
            float e = 0f;
            float track = Phase == 3 ? 0.35f : 0.5f;
            while (e < track / speedMul)
            {
                e += Time.deltaTime;
                body.Pos.x = Mathf.MoveTowards(body.Pos.x, ClampX(PlayerPos.x, 1.5f), Time.deltaTime * 5f);
                yield return null;
            }
            yield return Wait(0.15f);
            Game.I.Sound.Play("dash", 0.8f);
            while (body.Pos.y > Floor + 1.5f)
            {
                body.Pos.y -= 26f * speedMul * Time.deltaTime;
                yield return null;
            }
            body.Pos.y = Floor + 1.5f;
            var g = Game.I;
            g.Cam.Shake(0.6f);
            g.Sound.Play("slam", 0.8f);
            g.Fx.Dust(new Vector2(body.Pos.x, Floor), 14, 2.2f);
            // wing sweep
            flapAmp = 2f; flapSpeed = 20f;
            g.Sound.Play("wing", 1f);
            Combat.HurtPlayer(Combat.RectAt(body.Pos + new Vector2(0, -0.3f), new Vector2(6.5f, 2.2f)), 1, body.Pos);
            Shockwaves(new Vector2(body.Pos.x, Floor + 0.5f), true, 9f);
            yield return Wait(0.5f);
            flapAmp = 0.8f; flapSpeed = 8f; eyeGlow = 0.35f;
            while (body.Pos.y < topY - 3f)
            {
                body.Pos.y += 11f * Time.deltaTime;
                yield return null;
            }
            thread.enabled = false;
        }

        protected override IEnumerator PhaseChange(int phase)
        {
            var g = Game.I;
            var center = new Vector2((arenaMin + arenaMax) * 0.5f, Floor + 7f);
            yield return FlyTo(center, 14f, 1.2f);
            flapAmp = 0.05f;
            eyeGlow = 1.3f;
            g.SlowMotion(0.4f, 0.8f);
            Roar(0.9f);
            g.Cam.Punch(0.1f);
            if (phase == 2)
            {
                g.Hud.Toast("Морра гасит свет…");
                eclipse = true;
                eclipseTimer = 9f;
                darkTarget = 0.96f;
                speedMul = 1.1f;
            }
            else
            {
                g.Hud.Toast("Её крылья рвутся от ярости");
                speedMul = 1.3f;
                eclipse = true;
                eclipseTimer = 11f;
                darkTarget = 0.96f;
            }
            for (int i = 0; i < 16; i++)
            {
                g.Fx.Smoke(body.Pos + Random.insideUnitCircle * 3f, new Color(0.1f, 0.1f, 0.13f, 0.8f), 1.6f, 1);
                yield return new WaitForSeconds(0.05f);
            }
            flapAmp = 1.2f; flapSpeed = 10f; eyeGlow = 0.5f;
            yield return Wait(0.5f);
        }

        protected override void OnDefeated()
        {
            darkTarget = 0f;
            thread.enabled = false;
            base.OnDefeated();
        }

        protected override IEnumerator OnDeathVisual()
        {
            // she falls, wings tearing into ash
            float vy = 0f;
            while (body.Pos.y > Floor + 0.8f)
            {
                vy -= 20f * Time.unscaledDeltaTime;
                body.Pos.y += vy * Time.unscaledDeltaTime;
                Game.I.Fx.AmbientAsh(body.Pos + Random.insideUnitCircle * 2f, 0);
                transform.position = body.Pos;
                yield return null;
            }
            Game.I.Cam.Shake(0.6f);
            Game.I.Fx.Dust(new Vector2(body.Pos.x, Floor), 20, 3f);
            yield return base.OnDeathVisual();
        }

        protected override void Reward()
        {
            AbilityPickup.Spawn(room, body.Pos + new Vector2(0, 1.2f), "doublejump");
            Game.I.Hud.Toast("Пепел больше не тянет вниз…");
        }
    }
}
