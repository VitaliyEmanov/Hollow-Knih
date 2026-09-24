using System.Collections.Generic;
using UnityEngine;

namespace AshenWick
{
    public struct Hit
    {
        public int Damage;
        public Vector2 Dir;     // direction of the blow (knockback)
        public Vector2 Point;   // where it landed
        public bool Spell;
        public bool DownSlash;
    }

    /// <summary>Anything Niv can strike.</summary>
    public interface IHittable
    {
        Rect Hurtbox { get; }
        bool Vulnerable { get; }
        /// <summary>Returns true if the hit was accepted (counts for wax gain / pogo).</summary>
        bool TakeHit(Hit hit);
    }

    public static class Combat
    {
        public static readonly List<IHittable> Targets = new List<IHittable>();
        public static readonly List<Projectile> Projectiles = new List<Projectile>();

        public static void Register(IHittable h) { if (!Targets.Contains(h)) Targets.Add(h); }
        public static void Unregister(IHittable h) { Targets.Remove(h); }

        public static void Clear()
        {
            Targets.Clear();
            Projectiles.Clear();
        }

        /// <summary>Damages Niv if the area overlaps his hurtbox.</summary>
        public static bool HurtPlayer(Rect area, int damage, Vector2 from)
        {
            var g = Game.I;
            if (g == null || g.Player == null || !g.Live) return false;
            var p = g.Player;
            if (!p.Hurtbox.Overlaps(area)) return false;
            return p.TakeDamage(damage, from);
        }

        public static bool HurtPlayerCircle(Vector2 c, float r, int damage)
        {
            var g = Game.I;
            if (g == null || g.Player == null || !g.Live) return false;
            var p = g.Player;
            if (!CircleRect(c, r, p.Hurtbox)) return false;
            return p.TakeDamage(damage, c);
        }

        public static bool CircleRect(Vector2 c, float r, Rect rect)
        {
            float x = Mathf.Clamp(c.x, rect.xMin, rect.xMax);
            float y = Mathf.Clamp(c.y, rect.yMin, rect.yMax);
            float dx = c.x - x, dy = c.y - y;
            return dx * dx + dy * dy <= r * r;
        }

        public static Rect RectAt(Vector2 center, Vector2 size)
        {
            return new Rect(center - size * 0.5f, size);
        }
    }

    /// <summary>
    /// Axis-aligned kinematic body colliding with the room's tile grid.
    /// Tiles occupy [x, x+1) x [y, y+1) in world units.
    /// </summary>
    public sealed class Body
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public Vector2 Half;
        public bool Grounded, HitLeft, HitRight, HitCeiling;
        public bool DropThrough;
        public bool IgnorePlatforms;
        public bool NoClip;

        const float Eps = 0.001f;

        public Body(Vector2 pos, Vector2 half) { Pos = pos; Half = half; }

        public Rect Rect { get { return new Rect(Pos - Half, Half * 2f); } }
        public float Bottom { get { return Pos.y - Half.y; } }

        public void Move(Room room, float dt)
        {
            Move(room, Vel * dt);
        }

        public void Move(Room room, Vector2 delta)
        {
            Grounded = HitLeft = HitRight = HitCeiling = false;
            if (NoClip || room == null) { Pos += delta; return; }
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) / 0.4f));
            Vector2 step = delta / steps;
            for (int i = 0; i < steps; i++)
            {
                MoveX(room, step.x);
                MoveY(room, step.y);
            }
            // resting contact: detect ground even if not moving down this frame
            if (!Grounded && delta.y <= 0f && TouchingGround(room)) Grounded = true;
        }

        bool SolidSide(Room r, int x, int y) { return r.KindAt(x, y) == Room.Solid; }

        bool SolidDown(Room r, int x, int y)
        {
            int k = r.KindAt(x, y);
            return k == Room.Solid || (k == Room.Platform && !DropThrough && !IgnorePlatforms);
        }

        void MoveX(Room r, float dx)
        {
            if (dx == 0f) return;
            float bottom = Pos.y - Half.y, top = Pos.y + Half.y;
            int r0 = Mathf.FloorToInt(bottom + Eps), r1 = Mathf.CeilToInt(top - Eps) - 1;
            if (dx > 0f)
            {
                float right = Pos.x + Half.x;
                int c0 = Mathf.CeilToInt(right - Eps), c1 = Mathf.FloorToInt(right + dx - Eps);
                for (int c = c0; c <= c1; c++)
                    for (int y = r0; y <= r1; y++)
                        if (SolidSide(r, c, y)) { Pos.x = c - Half.x; HitRight = true; return; }
            }
            else
            {
                float left = Pos.x - Half.x;
                int c0 = Mathf.FloorToInt(left + Eps) - 1, c1 = Mathf.FloorToInt(left + dx + Eps);
                for (int c = c0; c >= c1; c--)
                    for (int y = r0; y <= r1; y++)
                        if (SolidSide(r, c, y)) { Pos.x = c + 1 + Half.x; HitLeft = true; return; }
            }
            Pos.x += dx;
        }

        void MoveY(Room r, float dy)
        {
            if (dy == 0f) return;
            float left = Pos.x - Half.x, right = Pos.x + Half.x;
            int c0 = Mathf.FloorToInt(left + Eps), c1 = Mathf.CeilToInt(right - Eps) - 1;
            if (dy < 0f)
            {
                float bottom = Pos.y - Half.y;
                int y0 = Mathf.FloorToInt(bottom + Eps) - 1, y1 = Mathf.FloorToInt(bottom + dy + Eps);
                for (int y = y0; y >= y1; y--)
                    for (int x = c0; x <= c1; x++)
                        if (SolidDown(r, x, y)) { Pos.y = y + 1 + Half.y; Grounded = true; return; }
            }
            else
            {
                float top = Pos.y + Half.y;
                int y0 = Mathf.CeilToInt(top - Eps), y1 = Mathf.FloorToInt(top + dy - Eps);
                for (int y = y0; y <= y1; y++)
                    for (int x = c0; x <= c1; x++)
                        if (SolidSide(r, x, y)) { Pos.y = y - Half.y; HitCeiling = true; return; }
            }
            Pos.y += dy;
        }

        public bool TouchingGround(Room r)
        {
            float bottom = Pos.y - Half.y;
            if (Mathf.Abs(bottom - Mathf.Round(bottom)) > 0.01f) return false;
            int y = Mathf.RoundToInt(bottom) - 1;
            int c0 = Mathf.FloorToInt(Pos.x - Half.x + Eps), c1 = Mathf.CeilToInt(Pos.x + Half.x - Eps) - 1;
            for (int x = c0; x <= c1; x++) if (SolidDown(r, x, y)) return true;
            return false;
        }

        /// <summary>Is there floor just beyond the front foot? (for patrolling walkers)</summary>
        public bool GroundAhead(Room r, float dir)
        {
            float fx = Pos.x + dir * (Half.x + 0.15f);
            int x = Mathf.FloorToInt(fx);
            int y = Mathf.FloorToInt(Pos.y - Half.y - 0.1f);
            int k = r.KindAt(x, y);
            return k == Room.Solid || k == Room.Platform;
        }

        public bool WallAhead(Room r, float dir)
        {
            float fx = Pos.x + dir * (Half.x + 0.1f);
            int x = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(Pos.y - Half.y + 0.05f), y1 = Mathf.FloorToInt(Pos.y + Half.y - 0.05f);
            for (int y = y0; y <= y1; y++) if (r.KindAt(x, y) == Room.Solid) return true;
            return false;
        }
    }
}
