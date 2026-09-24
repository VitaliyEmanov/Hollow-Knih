using System.Collections;
using AshenWick.Art;
using UnityEngine;

namespace AshenWick
{
    /// <summary>
    /// ГОРНАН, Угольный Кузнец. A colossal coal beetle who forged the grates of the Great Hearth.
    /// Now he hammers at anything that burns, believing thieves have come for the last fire.
    ///
    /// Phase 1: Hammer Slam (floor wave), Leap Crush, Bellows Charge (wall stun).
    /// Phase 2 (60%): the forge ignites — waves both ways, coal volleys that leave burning puddles,
    ///                ceiling debris after leaps and wall impacts.
    /// Phase 3 (30%): "Горнило" — telegraphed vents erupt across the arena between attacks.
    /// </summary>
    public sealed class Gornan : Boss
    {
        Transform hammer, head, bodyT;
        SpriteRenderer forgeGlow;
        float hammerAngle = -26f, hammerTarget = -26f, hammerSpeed = 8f;
        float squash;
        float walkPhase;
        const float Rest = -26f, Raised = 118f, Slammed = -22f;

        protected override void Build()
        {
            MaxHp = 210;
            musicId = 10;
            body.Half = new Vector2(1.7f, 1.55f);
            bodyT = Gfx.Part(vis, "body", Gfx.S("GornanBody", ArtLibrary.GornanBody), new Vector2(0f, -1.58f), Layer.Boss).transform;
            head = Gfx.Part(vis, "head", Gfx.S("GornanHead", ArtLibrary.GornanHead), new Vector2(1.35f, 0.5f), Layer.Boss + 2).transform;
            hammer = Gfx.Part(vis, "hammer", Gfx.S("GornanHammer", ArtLibrary.GornanHammer), new Vector2(0.4f, 0.1f), Layer.Boss + 3).transform;
            Gfx.Glow(vis, new Vector2(0, 0f), 7f, new Color(1f, 0.4f, 0.15f, 0.18f), Layer.Boss - 1).gameObject.AddComponent<Flicker>().Setup(0.18f, 0.05f, 0);
        }

        protected override void OnSpawned()
        {
            facing = 1f;
            // the forge behind him
            forgeGlow = Gfx.Glow(room.transform, new Vector2(room.W * 0.5f, floorY + 3f), 30f, new Color(1f, 0.35f, 0.1f, 0.06f), Layer.BackProps);
            forgeGlow.gameObject.AddComponent<Flicker>().Setup(0.06f, 0.02f, 0f);
        }

        public override Rect Hurtbox { get { return Combat.RectAt(body.Pos + new Vector2(facing * 0.3f, 0.1f), new Vector2(3.4f, 3.1f)); } }

        Vector2 HammerHead { get { return hammer.TransformPoint(new Vector3(3.22f, 0f, 0f)); } }

        protected override void Animate(float dt)
        {
            vis.localScale = new Vector3(facing, 1f, 1f);
            hammerAngle = Mathf.Lerp(hammerAngle, hammerTarget, 1f - Mathf.Exp(-hammerSpeed * dt));
            hammer.localRotation = Quaternion.Euler(0, 0, hammerAngle);
            squash = Mathf.MoveTowards(squash, 0f, dt * 2f);
            float breathe = Mathf.Sin(t * 1.8f) * 0.02f;
            bodyT.localScale = new Vector3(1f + squash * 0.25f, 1f - squash * 0.3f + breathe, 1f);
            head.localPosition = new Vector3(1.35f, 0.5f - squash * 0.9f + breathe * 2f, 0);
            head.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f) * 3f);
            if (fighting && Random.value < 0.08f * Phase) Game.I.Fx.Ember(body.Pos + new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(0f, 1.4f)));
        }

        protected override void OnWalkStep(float dt)
        {
            walkPhase += dt * 6f;
            if (Mathf.Sin(walkPhase) > 0.98f)
            {
                Game.I.Cam.Shake(0.12f);
                Game.I.Fx.Dust(new Vector2(body.Pos.x, floorY), 3);
            }
            vis.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(walkPhase)) * 0.08f, 0);
        }

        // ------------------------------------------------------------------

        protected override IEnumerator Intro()
        {
            var g = Game.I;
            g.Cam.Focus(body.Pos + new Vector2(-2f, 1f), 0.55f);
            // hammering the empty anvil, back turned
            for (int i = 0; i < 3; i++)
            {
                hammerSpeed = 10f; hammerTarget = Raised;
                yield return new WaitForSeconds(0.4f);
                hammerSpeed = 30f; hammerTarget = Slammed;
                yield return new WaitForSeconds(0.08f);
                g.Sound.Play("gate", 0.8f);
                g.Fx.HitBurst(HammerHead, Vector2.up, 6, 0.8f);
                g.Fx.FireBurst(HammerHead, 0.4f, 6);
                g.Cam.Shake(0.25f);
                yield return new WaitForSeconds(0.35f);
            }
            yield return new WaitForSeconds(0.4f);
            facing = -1f;
            squash = 0.4f;
            yield return new WaitForSeconds(0.35f);
            hammerSpeed = 6f; hammerTarget = 60f;
            Roar(1f);
            g.Cam.Punch(0.08f);
            for (int i = 0; i < 20; i++) { g.Fx.Smoke(body.Pos + new Vector2(0, 1f), new Color(0.2f, 0.2f, 0.22f, 0.6f), 1.4f, 1); yield return new WaitForSeconds(0.05f); }
            g.Cam.Focus(body.Pos, 0f);
            hammerTarget = Rest;
            yield return new WaitForSeconds(0.3f);
        }

        protected override IEnumerator Brain()
        {
            FacePlayer();
            float d = Mathf.Abs(PlayerPos.x - body.Pos.x);
            string a;
            if (Phase == 1) a = d > 8f ? Pick("leap", "charge", "slam") : Pick("slam", "slam", "leap", "charge");
            else if (Phase == 2) a = Pick("slam", "leap", "charge", "coal");
            else a = Pick("slam", "leap", "coal", "vents", "charge", "vents");

            switch (a)
            {
                case "slam": yield return Slam(); break;
                case "leap": yield return Leap(); break;
                case "charge": yield return Charge(); break;
                case "coal": yield return CoalVolley(); break;
                case "vents": yield return Vents(); break;
            }
            yield return Wait(Phase == 1 ? 0.5f : 0.3f);
        }

        IEnumerator Slam()
        {
            FacePlayer();
            if (Mathf.Abs(PlayerPos.x - body.Pos.x) > 5.5f)
                yield return WalkTo(PlayerPos.x - facing * 3.8f, 4.5f, 1.3f, 0.5f);
            FacePlayer();
            // wind-up
            hammerSpeed = 7f; hammerTarget = Raised;
            squash = 0.2f;
            yield return Wait(0.3f);
            Telegraph(HammerHead, new Color(1f, 0.7f, 0.3f, 0.9f));
            yield return Wait(Phase == 3 ? 0.2f : 0.3f);
            // strike
            hammerSpeed = 40f; hammerTarget = Slammed;
            yield return new WaitForSeconds(0.07f);
            SlamImpact(2);
            Shockwaves(HammerHead, Phase >= 2, 12f + Phase);
            yield return Wait(0.65f);
            hammerSpeed = 6f; hammerTarget = Rest;
            yield return Wait(0.25f);
        }

        void SlamImpact(int dmg)
        {
            var g = Game.I;
            Vector2 hp = HammerHead;
            var floorHit = new Vector2(hp.x, room.FloorBelow(hp.x, hp.y + 1f));
            g.Sound.Play("slam", 1f, 0.05f);
            g.Cam.Shake(0.6f);
            g.Cam.Punch(0.03f);
            g.Fx.Dust(floorHit, 10, 1.5f);
            g.Fx.FireBurst(floorHit + new Vector2(0, 0.3f), 0.9f, 12);
            g.Fx.HitBurst(floorHit, Vector2.up, 10, 1f);
            Combat.HurtPlayer(Combat.RectAt(hp, new Vector2(2.2f, 2.2f)), dmg, hp);
        }

        IEnumerator Leap()
        {
            FacePlayer();
            squash = 0.5f;
            hammerSpeed = 8f; hammerTarget = 80f;
            Game.I.Sound.Play("roar", 0.4f, 0.2f);
            yield return Wait(0.4f);
            float tx = PlayerPos.x;
            Game.I.Sound.Play("jump", 1f, 0f);
            Game.I.Fx.Dust(new Vector2(body.Pos.x, floorY), 8, 1.5f);
            hammerTarget = Raised;
            yield return JumpTo(tx, 0.95f, 55f, k =>
            {
                FacePlayer();
                if (k > 0.75f) { hammerSpeed = 25f; hammerTarget = Slammed; }
            });
            // landing
            squash = 0.6f;
            var g = Game.I;
            g.Sound.Play("slam", 1f, 0f);
            g.Cam.Shake(0.8f);
            g.Fx.Dust(new Vector2(body.Pos.x, floorY), 14, 2f);
            Combat.HurtPlayer(Combat.RectAt(new Vector2(body.Pos.x, floorY + 1f), new Vector2(4.5f, 2f)), 2, body.Pos);
            Shockwaves(new Vector2(body.Pos.x, floorY + 0.5f), true, 11f + Phase);
            if (Phase >= 2) DebrisRain(Phase == 3 ? 6 : 4);
            yield return Wait(0.85f);
            hammerSpeed = 6f; hammerTarget = Rest;
        }

        IEnumerator Charge()
        {
            FacePlayer();
            // bellows: stomps and steam from the nostrils
            for (int i = 0; i < 2; i++)
            {
                squash = 0.3f;
                Game.I.Sound.Play("land", 0.9f, 0f);
                Game.I.Cam.Shake(0.2f);
                Game.I.Fx.Smoke(body.Pos + new Vector2(facing * 2f, 0.6f), new Color(0.9f, 0.9f, 0.95f, 0.5f), 0.7f, 4);
                yield return Wait(0.28f);
            }
            Telegraph(body.Pos + new Vector2(facing * 2.2f, 0.6f), new Color(1f, 0.6f, 0.3f, 0.9f));
            hammerSpeed = 8f; hammerTarget = -60f;
            yield return Wait(0.2f);
            Game.I.Sound.Play("roar", 0.6f, 0.1f);
            float time = 0f;
            contactDamage = 2;
            while (time < 2.5f)
            {
                float dt = Time.deltaTime;
                time += dt;
                MoveGround(dt, facing * 14f * speedMul);
                if (Random.value < 0.6f) Game.I.Fx.Ember(HammerHead);
                if (Random.value < 0.3f) Game.I.Fx.Dust(new Vector2(body.Pos.x - facing * 1.2f, floorY), 1);
                if (Time.frameCount % 3 == 0) Game.I.Fx.Afterimage(bodyT.GetComponent<SpriteRenderer>(), new Color(1f, 0.4f, 0.1f, 0.35f), 0.2f);
                if (body.HitLeft || body.HitRight || body.Pos.x <= arenaMin + body.Half.x + 0.05f || body.Pos.x >= arenaMax - body.Half.x - 0.05f) break;
                yield return null;
            }
            contactDamage = 1;
            // hits the wall: stunned
            var g = Game.I;
            g.Sound.Play("slam", 1f, 0f);
            g.Sound.Play("gate", 0.6f, 0f);
            g.Cam.Shake(0.9f);
            g.Fx.HitBurst(body.Pos + new Vector2(facing * 1.8f, 0.5f), new Vector2(-facing, 0.3f), 14, 1.2f);
            if (Phase >= 2) DebrisRain(Phase == 3 ? 7 : 5);
            squash = 0.6f;
            head.localRotation = Quaternion.Euler(0, 0, 20f);
            float stun = 1.2f;
            while (stun > 0f)
            {
                stun -= Time.deltaTime;
                if (Random.value < 0.1f) g.Fx.Smoke(body.Pos + new Vector2(0, 1.6f), new Color(0.8f, 0.8f, 0.85f, 0.4f), 0.4f, 1);
                vis.localPosition = new Vector3(Mathf.Sin(t * 40f) * 0.03f, 0, 0);
                yield return null;
            }
            vis.localPosition = Vector3.zero;
            hammerSpeed = 6f; hammerTarget = Rest;
        }

        IEnumerator CoalVolley()
        {
            FacePlayer();
            hammerSpeed = 8f; hammerTarget = -80f;
            Telegraph(HammerHead, new Color(1f, 0.5f, 0.2f, 0.9f));
            yield return Wait(0.45f);
            int count = Phase == 3 ? 5 : 3;
            hammerSpeed = 30f; hammerTarget = 150f;
            yield return new WaitForSeconds(0.08f);
            Game.I.Sound.Play("fire", 0.9f);
            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * 3.2f;
                float tx = ClampX(PlayerPos.x + offset, 0.5f);
                Vector2 from = HammerHead;
                float T = 1.05f + i * 0.06f;
                float grav = 25f;
                Vector2 v = new Vector2((tx - from.x) / T, (floorY - from.y) / T + 0.5f * grav * T);
                var p = Projectile.Spawn(room, "Coal", ArtLibrary.Coal, from, v, 0.4f, 1.1f);
                p.Gravity = grav;
                p.Slashable = false;
                p.FaceVelocity = false;
                p.Spin = 300f;
                p.Life = 5f;
                p.AddGlow(new Color(1f, 0.45f, 0.15f, 0.45f), 2.4f);
                p.OnDeath = pr =>
                {
                    var pos = pr.transform.position;
                    FirePuddle.Spawn(room, new Vector2(pos.x, room.FloorBelow(pos.x, pos.y + 0.5f)), 2.6f);
                    Game.I.Sound.Play("fire", 0.5f);
                };
            }
            yield return Wait(0.7f);
            hammerSpeed = 6f; hammerTarget = Rest;
        }

        IEnumerator Vents()
        {
            // "Горнило": the floor itself erupts
            hammerSpeed = 5f; hammerTarget = 100f;
            Roar(0.5f);
            squash = 0.2f;
            yield return Wait(0.4f);
            float px = PlayerPos.x;
            int n = 6;
            for (int i = 0; i < n; i++)
            {
                float x = ClampX(px + (i % 2 == 0 ? 1 : -1) * (i / 2) * 3.4f, 0.8f);
                FireColumn.Spawn(room, new Vector2(x, floorY), 0.85f, 0.65f, 8f, 1.4f);
                yield return Wait(0.12f);
            }
            yield return Wait(0.9f);
            // and a second, offset row
            px = PlayerPos.x;
            for (int i = 0; i < 5; i++)
            {
                float x = ClampX(arenaMin + 1.7f + i * (arenaMax - arenaMin - 3.4f) / 4f + 1.6f, 0.8f);
                FireColumn.Spawn(room, new Vector2(x, floorY), 0.75f, 0.6f, 8f, 1.4f);
            }
            yield return Wait(1.2f);
            hammerSpeed = 6f; hammerTarget = Rest;
        }

        void DebrisRain(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = i == 0 ? PlayerPos.x : Random.Range(arenaMin + 0.5f, arenaMax - 0.5f);
                FallingHazard.Spawn(room, x, 0.7f + i * 0.18f, false);
            }
        }

        protected override IEnumerator PhaseChange(int phase)
        {
            var g = Game.I;
            hammerSpeed = 5f; hammerTarget = Rest;
            squash = 0.5f;
            yield return Wait(0.2f);
            g.SlowMotion(0.35f, 0.7f);
            Roar(1f);
            g.Fx.Flash(new Color(1f, 0.5f, 0.2f), 0.5f);
            g.Cam.Punch(0.1f);
            for (int i = 0; i < 12; i++)
            {
                g.Fx.FireBurst(body.Pos + Random.insideUnitCircle * 2f, 0.7f, 6);
                yield return new WaitForSeconds(0.06f);
            }
            forgeGlow.GetComponent<Flicker>().Setup(phase == 2 ? 0.14f : 0.22f, 0.05f, 0f);
            forgeGlow.color = new Color(1f, 0.35f, 0.1f, phase == 2 ? 0.14f : 0.22f);
            speedMul = phase == 2 ? 1.15f : 1.3f;
            if (phase == 3)
            {
                // the whole floor glows before the vents
                for (float x = arenaMin + 1f; x < arenaMax - 1f; x += 4f)
                    FireColumn.Spawn(room, new Vector2(x, floorY), 1f, 0.5f, 5f, 1.1f);
            }
            yield return Wait(0.6f);
        }

        protected override void Reward()
        {
            AbilityPickup.Spawn(room, body.Pos + new Vector2(0, 1f), "spell");
            Game.I.Hud.Toast("Угли Горнана ещё тлеют…");
        }
    }
}
