using System.Collections;
using System.Collections.Generic;
using AshenWick.Art;
using AshenWick.World;
using UnityEngine;

namespace AshenWick
{
    /// <summary>
    /// Every screen of the game, drawn with IMGUI so it needs no scene setup:
    /// splash, title menu, prologue, HUD, area and boss title cards, dialogue,
    /// ability panels, pause, death, ending and credits.
    /// Layout is authored for 1080p and scaled to the actual screen.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        // fade / flash
        float fade = 1f, fadeTarget = 1f, fadeSpeed = 1f;
        Color fadeColor = Color.black;

        // textures
        Texture2D white, flameOn, flameOff, vesselFrame, vesselFill, vesselBack, logo, divider, barFrame;

        // title menu
        int menuIndex;
        bool showControls;
        float titleT;

        // cards
        string areaName, areaSub; float areaT = -1f;
        string bossName, bossSub; float bossT = -1f;
        string toast; float toastT = -1f;
        string prompt; Vector2 promptWorld;
        Boss bossBar; float bossBarShown, bossHpShown;
        bool deathText; float deathT;

        // dialogue
        string dlgName; string[] dlgLines; int dlgIndex; float dlgChars; System.Action dlgDone; int dlgFrame;

        // panel
        Lore.AbilityInfo panel; float panelT; System.Action panelDone;

        // cinematic text (prologue / ending) & credits
        string cineText; float cineAlpha;
        string[] credits; float creditsY;
        bool splash; float splashT;

        // pause
        int pauseIndex;

        GUIStyle sText, sTitle, sSub, sCenter, sSmall, sName, sMenu;
        float scale = 1f;
        float VW { get { return Screen.width / scale; } }
        const float VH = 1080f;

        void Awake()
        {
            white = SpriteBank.Tex("WhitePixel", ArtLibrary.WhitePixel);
        }

        void EnsureTextures()
        {
            if (flameOn != null) return;
            flameOn = SpriteBank.Tex("FlameIcon", ArtLibrary.FlameIcon);
            flameOff = SpriteBank.Tex("FlameIconEmpty", ArtLibrary.FlameIconEmpty);
            vesselFrame = SpriteBank.Tex("VesselFrame", ArtLibrary.VesselFrame);
            vesselFill = SpriteBank.Tex("VesselFill", ArtLibrary.VesselFill);
            vesselBack = SpriteBank.Tex("VesselBack", ArtLibrary.VesselBack);
            logo = SpriteBank.Tex("RaspberryLogo", ArtLibrary.RaspberryLogo);
            divider = SpriteBank.Tex("Divider", ArtLibrary.Divider);
            barFrame = SpriteBank.Tex("BossBarFrame", ArtLibrary.BossBarFrame);
        }

        // ------------------------------------------------------------------
        // API
        // ------------------------------------------------------------------

        public void FadeTo(float alpha, float time) { FadeTo(alpha, time, Color.black); }

        public void FadeTo(float alpha, float time, Color color)
        {
            fadeTarget = alpha;
            fadeColor = color;
            if (time <= 0f) { fade = alpha; fadeSpeed = 1000f; }
            else fadeSpeed = Mathf.Abs(alpha - fade) / time;
        }

        public void ShowAreaTitle(string name, string sub) { areaName = name; areaSub = sub; areaT = 0f; }
        public void ShowBossTitle(string name, string sub) { bossName = name; bossSub = sub; bossT = 0f; }
        public void Toast(string s) { toast = s; toastT = 0f; }
        public void SetPrompt(string s, Vector2 world) { prompt = s; promptWorld = world; }
        public void ShowBossBar(Boss b) { bossBar = b; bossHpShown = 1f; }
        public void HideBossBar() { bossBar = null; }
        public void ShowDeathText(bool on) { deathText = on; deathT = 0f; }

        public void ShowDialogue(string name, string[] lines, System.Action done)
        {
            dlgName = name; dlgLines = lines; dlgIndex = 0; dlgChars = 0f; dlgDone = done; dlgFrame = Time.frameCount;
        }

        public void ShowPanel(Lore.AbilityInfo info, System.Action done)
        {
            panel = info; panelT = 0f; panelDone = done;
        }

        public IEnumerator Splash()
        {
            splash = true; splashT = 0f;
            Game.I.Sound.Play("rest", 0.6f, 0f);
            while (splashT < 3.4f)
            {
                if (splashT > 0.6f && Input.anyKeyDown) break;
                yield return null;
            }
            splash = false;
        }

        public IEnumerator Prologue(string[] lines)
        {
            FadeTo(1f, 0.01f, fadeColor);
            foreach (var line in lines)
            {
                cineText = line;
                cineAlpha = 0f;
                float t = 0f;
                while (cineAlpha < 1f) { cineAlpha += Time.unscaledDeltaTime * 1.2f; yield return null; }
                while (t < 5.5f)
                {
                    t += Time.unscaledDeltaTime;
                    if (t > 0.4f && (Controls.ConfirmPressed || Input.GetKeyDown(KeyCode.Escape))) break;
                    yield return null;
                }
                while (cineAlpha > 0f) { cineAlpha -= Time.unscaledDeltaTime * 2f; yield return null; }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            cineText = null;
        }

        public IEnumerator Credits(string[] lines)
        {
            credits = lines;
            creditsY = VH + 40f;
            float endY = -lines.Length * 64f - 200f;
            while (creditsY > endY)
            {
                creditsY -= Time.unscaledDeltaTime * (Controls.JumpHeld || Controls.ConfirmPressed ? 260f : 70f);
                yield return null;
            }
            credits = null;
        }

        // ------------------------------------------------------------------
        // Update: timers and input for menus
        // ------------------------------------------------------------------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * dt);
            if (areaT >= 0f) { areaT += dt; if (areaT > 5f) areaT = -1f; }
            if (bossT >= 0f) { bossT += dt; if (bossT > 4.5f) bossT = -1f; }
            if (toastT >= 0f) { toastT += dt; if (toastT > 3.5f) toastT = -1f; }
            if (splash) splashT += dt;
            if (deathText) deathT += dt;
            titleT += dt;
            if (bossBar != null)
            {
                bossBarShown = Mathf.MoveTowards(bossBarShown, 1f, dt * 2f);
                bossHpShown = Mathf.MoveTowards(bossHpShown, bossBar.HpFraction, dt * 0.6f);
            }
            else bossBarShown = Mathf.MoveTowards(bossBarShown, 0f, dt * 2f);

            var g = Game.I;
            if (g == null) return;

            // dialogue
            if (dlgLines != null && g.State == GameState.Dialogue)
            {
                dlgChars += dt * 55f;
                if (Time.frameCount > dlgFrame && (Controls.ConfirmPressed || Controls.InteractPressed))
                {
                    if (dlgChars < dlgLines[dlgIndex].Length) dlgChars = 9999f;
                    else
                    {
                        dlgIndex++;
                        dlgChars = 0f;
                        g.Sound.Play("ui", 0.35f);
                        if (dlgIndex >= dlgLines.Length)
                        {
                            dlgLines = null;
                            var done = dlgDone; dlgDone = null;
                            if (done != null) done();
                        }
                    }
                }
            }

            // ability panel
            if (panel != null)
            {
                panelT += dt;
                if (panelT > 1f && (Controls.ConfirmPressed || Controls.InteractPressed || Input.GetKeyDown(KeyCode.Escape)))
                {
                    panel = null;
                    g.Sound.Play("ui", 0.5f);
                    var done = panelDone; panelDone = null;
                    if (done != null) done();
                }
            }

            if (g.State == GameState.Title) TitleInput(g);
            if (g.State == GameState.Paused) PauseInput(g);
            if (g.State != GameState.Playing) prompt = null;
        }

        void TitleInput(Game g)
        {
            if (fade > 0.5f) return;
            if (showControls)
            {
                if (Controls.ConfirmPressed || Controls.BackPressed) { showControls = false; g.Sound.Play("ui", 0.5f); }
                return;
            }
            var items = TitleItems();
            if (Controls.MenuUp) { menuIndex = (menuIndex + items.Count - 1) % items.Count; g.Sound.Play("ui", 0.4f); }
            if (Controls.MenuDown) { menuIndex = (menuIndex + 1) % items.Count; g.Sound.Play("ui", 0.4f); }
            menuIndex = Mathf.Clamp(menuIndex, 0, items.Count - 1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                g.Sound.Play("pickup", 0.5f);
                switch (items[menuIndex])
                {
                    case "Продолжить": g.Continue(); break;
                    case "Новая игра": g.NewGame(); break;
                    case "Управление": showControls = true; break;
                    case "Выход": Application.Quit(); break;
                }
            }
        }

        List<string> TitleItems()
        {
            var l = new List<string>();
            if (SaveData.Exists()) l.Add("Продолжить");
            l.Add("Новая игра");
            l.Add("Управление");
            l.Add("Выход");
            return l;
        }

        void PauseInput(Game g)
        {
            if (showControls)
            {
                if (Controls.ConfirmPressed || Input.GetKeyDown(KeyCode.Backspace)) showControls = false;
                return;
            }
            if (Controls.MenuUp) { pauseIndex = (pauseIndex + 2) % 3; g.Sound.Play("ui", 0.4f); }
            if (Controls.MenuDown) { pauseIndex = (pauseIndex + 1) % 3; g.Sound.Play("ui", 0.4f); }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                g.Sound.Play("ui", 0.5f);
                if (pauseIndex == 0) g.Resume();
                else if (pauseIndex == 1) showControls = true;
                else g.QuitToTitle();
            }
        }

        // ------------------------------------------------------------------
        // Drawing
        // ------------------------------------------------------------------

        void Styles()
        {
            if (sText != null) return;
            sText = new GUIStyle(GUI.skin.label) { fontSize = 30, wordWrap = true, alignment = TextAnchor.UpperLeft, richText = true };
            sText.normal.textColor = new Color(0.93f, 0.91f, 0.88f);
            sCenter = new GUIStyle(sText) { alignment = TextAnchor.MiddleCenter, fontSize = 34 };
            sTitle = new GUIStyle(sText) { fontSize = 96, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            sSub = new GUIStyle(sText) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
            sSmall = new GUIStyle(sText) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            sName = new GUIStyle(sText) { fontSize = 30, fontStyle = FontStyle.Bold };
            sName.normal.textColor = new Color(1f, 0.8f, 0.55f);
            sMenu = new GUIStyle(sText) { fontSize = 40, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        }

        void OnGUI()
        {
            var g = Game.I;
            if (g == null) return;
            Styles();
            EnsureTextures();
            scale = Screen.height / VH;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            GUI.depth = 0;

            bool gameplay = g.State == GameState.Playing || g.State == GameState.Dialogue || g.State == GameState.Panel || g.State == GameState.Paused || g.State == GameState.Transition;
            if (gameplay && g.Player != null) DrawHud(g);
            if (g.State == GameState.Playing && !string.IsNullOrEmpty(prompt)) DrawPrompt(g);
            if (bossBarShown > 0.01f && bossBar != null) DrawBossBar();
            if (areaT >= 0f) DrawAreaTitle();
            if (bossT >= 0f) DrawBossTitle();
            if (toastT >= 0f) DrawToast();
            if (g.State == GameState.Dialogue && dlgLines != null) DrawDialogue();

            // flash from Fx
            if (g.Fx != null && g.Fx.FlashAlpha > 0f) Rect(new Rect(0, 0, VW, VH), g.Fx.FlashColor.WithAlpha(g.Fx.FlashAlpha * 0.6f));

            if (g.State == GameState.Title) DrawTitle();
            if (panel != null) DrawPanel();
            if (g.State == GameState.Paused) DrawPause();

            // fade overlay
            if (fade > 0.001f) Rect(new Rect(0, 0, VW, VH), fadeColor.WithAlpha(fade));

            // on top of fade
            if (splash) DrawSplash();
            if (cineText != null) DrawCine();
            if (credits != null) DrawCredits();
            if (deathText) DrawDeath();
            if (showControls) DrawControls();
        }

        void Rect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void Tex(Rect r, Texture t, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        void Label(Rect r, string s, GUIStyle st, Color c, float shadow = 3f)
        {
            var old = st.normal.textColor;
            st.normal.textColor = new Color(0, 0, 0, c.a * 0.8f);
            GUI.Label(new Rect(r.x + shadow, r.y + shadow, r.width, r.height), s, st);
            st.normal.textColor = c;
            GUI.Label(r, s, st);
            st.normal.textColor = old;
        }

        void DrawHud(Game g)
        {
            var p = g.Player;
            float x = 40f, y = 30f;
            // wax vessel
            float f = p.Wax / (float)Player.WaxMax;
            float vs = 150f;
            Tex(new Rect(x, y, vs, vs), vesselBack, Color.white);
            if (f > 0f)
            {
                var old = GUI.color;
                float glow = p.Wax >= Player.WaxCost ? 1f : 0.75f;
                GUI.color = new Color(glow, glow, glow, 1f);
                GUI.DrawTextureWithTexCoords(new Rect(x, y + vs * (1f - f), vs, vs * f), vesselFill, new Rect(0, 0, 1, f));
                GUI.color = old;
            }
            Tex(new Rect(x, y, vs, vs), vesselFrame, Color.white);

            // flames
            float fx = x + vs + 10f, fy = y + 30f;
            for (int i = 0; i < p.MaxFlames; i++)
            {
                bool on = i < p.Flames;
                float bob = on ? Mathf.Sin(Time.unscaledTime * 3f + i) * 2f : 0f;
                Tex(new Rect(fx + i * 46f, fy + bob, 40f, 64f), on ? flameOn : flameOff, Color.white);
            }

            // ability reminders (subtle)
            var s = g.Save;
            string abil = (s.hasDash ? "Рывок  " : "") + (s.hasSpell ? "Вспышка  " : "") + (s.hasDoubleJump ? "Крылья" : "");
            if (abil.Length > 0) Label(new Rect(fx, fy + 70f, 600f, 34f), abil, sSmallLeft, new Color(1f, 0.9f, 0.8f, 0.45f), 2f);
        }

        GUIStyle smallLeft;
        GUIStyle sSmallLeft { get { if (smallLeft == null) smallLeft = new GUIStyle(sSmall) { alignment = TextAnchor.UpperLeft }; return smallLeft; } }

        void DrawPrompt(Game g)
        {
            var cam = g.Cam.Cam;
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(promptWorld.x, promptWorld.y + 1.7f, 0f));
            float gx = sp.x / scale, gy = (Screen.height - sp.y) / scale;
            var r = new Rect(gx - 150f, gy - 30f, 300f, 50f);
            float a = 0.75f + Mathf.Sin(Time.unscaledTime * 3f) * 0.15f;
            Label(r, "▲  " + prompt, sSmall, new Color(1f, 0.95f, 0.85f, a), 2f);
        }

        void DrawAreaTitle()
        {
            float a = Mathf.Clamp01(areaT / 1f) * Mathf.Clamp01((5f - areaT) / 1.2f);
            float cy = VH * 0.3f;
            Label(new Rect(0, cy - 60f, VW, 110f), areaName.ToUpperInvariant(), sTitle, new Color(1f, 1f, 1f, a));
            Tex(new Rect(VW * 0.5f - 300f, cy + 50f, 600f, 56f), divider, new Color(1f, 1f, 1f, a * 0.85f));
            Label(new Rect(0, cy + 100f, VW, 50f), areaSub, sSub, new Color(0.9f, 0.88f, 0.85f, a * 0.9f));
        }

        void DrawBossTitle()
        {
            float a = Mathf.Clamp01(bossT / 0.6f) * Mathf.Clamp01((4.5f - bossT) / 1f);
            float cy = VH * 0.72f;
            Label(new Rect(0, cy - 40f, VW, 50f), bossSub, sSub, new Color(1f, 0.85f, 0.7f, a));
            Label(new Rect(0, cy, VW, 120f), bossName, sTitle, new Color(1f, 1f, 1f, a));
            Tex(new Rect(VW * 0.5f - 260f, cy + 110f, 520f, 48f), divider, new Color(1f, 0.8f, 0.6f, a * 0.8f));
        }

        void DrawToast()
        {
            float a = Mathf.Clamp01(toastT / 0.4f) * Mathf.Clamp01((3.5f - toastT) / 0.8f);
            Label(new Rect(0, VH * 0.16f, VW, 60f), toast, sSub, new Color(1f, 0.92f, 0.82f, a));
        }

        void DrawBossBar()
        {
            float a = bossBarShown;
            float w = 900f, h = 64f;
            float x = VW * 0.5f - w * 0.5f, y = VH - 110f;
            float f = bossBar.HpFraction;
            // inner bar region inside the frame (240*2 of 512 px wide -> ~94%)
            float ix = x + w * 0.035f, iw = w * 0.93f, iy = y + h * 0.36f, ih = h * 0.28f;
            Rect(new Rect(ix, iy, iw, ih), new Color(0.05f, 0.04f, 0.05f, a));
            Rect(new Rect(ix, iy, iw * bossHpShown, ih), new Color(1f, 0.85f, 0.7f, a * 0.6f));
            Rect(new Rect(ix, iy, iw * f, ih), new Color(0.92f, 0.36f, 0.14f, a));
            Tex(new Rect(x, y, w, h), barFrame, new Color(1f, 1f, 1f, a));
            Label(new Rect(0, y - 40f, VW, 40f), bossBar.DisplayName, sSmall, new Color(1f, 0.95f, 0.9f, a * 0.9f), 2f);
        }

        void DrawDialogue()
        {
            float w = Mathf.Min(1300f, VW - 120f), h = 250f;
            var r = new Rect(VW * 0.5f - w * 0.5f, VH - h - 60f, w, h);
            Rect(r, new Color(0.02f, 0.02f, 0.03f, 0.82f));
            Rect(new Rect(r.x, r.y, r.width, 2f), new Color(1f, 0.95f, 0.85f, 0.6f));
            Rect(new Rect(r.x, r.yMax - 2f, r.width, 2f), new Color(1f, 0.95f, 0.85f, 0.6f));
            float ty = r.y + 26f;
            if (!string.IsNullOrEmpty(dlgName))
            {
                Label(new Rect(r.x + 40f, ty, r.width - 80f, 40f), dlgName, sName, sName.normal.textColor, 2f);
                ty += 48f;
            }
            string line = dlgLines[dlgIndex];
            int n = Mathf.Min(line.Length, (int)dlgChars);
            Label(new Rect(r.x + 40f, ty, r.width - 80f, r.yMax - ty - 20f), line.Substring(0, n), sText, sText.normal.textColor, 2f);
            if (n >= line.Length)
            {
                float a = 0.5f + Mathf.Sin(Time.unscaledTime * 5f) * 0.4f;
                Label(new Rect(r.xMax - 80f, r.yMax - 50f, 40f, 40f), "▼", sSmall, new Color(1f, 0.9f, 0.7f, a), 1f);
            }
        }

        void DrawPanel()
        {
            float a = Mathf.Clamp01(panelT / 0.5f);
            Rect(new Rect(0, 0, VW, VH), new Color(0, 0, 0, 0.6f * a));
            float cy = VH * 0.36f;
            Label(new Rect(0, cy - 60f, VW, 110f), panel.Title, sTitle, new Color(1f, 0.92f, 0.8f, a));
            Tex(new Rect(VW * 0.5f - 280f, cy + 50f, 560f, 52f), divider, new Color(1f, 0.85f, 0.65f, a));
            Label(new Rect(VW * 0.5f - 600f, cy + 120f, 1200f, 60f), panel.Description, sSub, new Color(0.95f, 0.93f, 0.9f, a));
            Label(new Rect(VW * 0.5f - 600f, cy + 200f, 1200f, 60f), panel.Hint, sCenter, new Color(1f, 0.8f, 0.55f, a));
            if (panelT > 1f) Label(new Rect(0, VH - 140f, VW, 40f), "Нажмите Z / Пробел, чтобы продолжить", sSmall, new Color(1f, 1f, 1f, 0.5f + Mathf.Sin(Time.unscaledTime * 3f) * 0.2f), 2f);
        }

        void DrawTitle()
        {
            float a = Mathf.Clamp01(titleT / 2f);
            // darken the backdrop at the top & bottom
            Rect(new Rect(0, 0, VW, VH), new Color(0, 0, 0, 0.35f));
            float cy = VH * 0.26f;
            Label(new Rect(0, cy - 110f, VW, 60f), Lore.Series, sSubSpaced, new Color(0.9f, 0.9f, 0.92f, a * 0.9f), 2f);
            var big = new GUIStyle(sTitle) { fontSize = 190 };
            Label(new Rect(0, cy - 60f, VW, 220f), Lore.GameTitle, big, new Color(0.97f, 0.95f, 0.92f, a), 5f);
            Tex(new Rect(VW * 0.5f - 360f, cy + 150f, 720f, 66f), divider, new Color(1f, 0.85f, 0.65f, a));
            Label(new Rect(0, cy + 210f, VW, 50f), Lore.Subtitle, sSub, new Color(1f, 0.85f, 0.65f, a), 2f);

            if (!showControls)
            {
                var items = TitleItems();
                float my = VH * 0.62f;
                for (int i = 0; i < items.Count; i++)
                {
                    bool sel = i == menuIndex;
                    var c = sel ? new Color(1f, 0.95f, 0.85f, a) : new Color(0.75f, 0.72f, 0.7f, a * 0.8f);
                    var r = new Rect(0, my + i * 62f, VW, 56f);
                    Label(r, items[i], sMenu, c, 2f);
                    if (sel)
                    {
                        float w = 170f + items[i].Length * 11f;
                        float pulse = Mathf.Sin(Time.unscaledTime * 4f) * 4f;
                        Tex(new Rect(VW * 0.5f - w - 36f - pulse, r.y + 6f, 30f, 44f), flameOn, new Color(1, 1, 1, a));
                        Tex(new Rect(VW * 0.5f + w + 6f + pulse, r.y + 6f, 30f, 44f), flameOn, new Color(1, 1, 1, a));
                    }
                }
            }
            Label(new Rect(40f, VH - 60f, 600f, 40f), "© " + Lore.Studio, sSmallLeft, new Color(1f, 0.6f, 0.7f, a * 0.8f), 2f);
            Label(new Rect(VW - 640f, VH - 60f, 600f, 40f), "↑↓ выбор   Z / Enter — подтвердить", smallRight, new Color(1f, 1f, 1f, a * 0.5f), 2f);
        }

        GUIStyle subSpaced, smallRightStyle;
        GUIStyle sSubSpaced { get { if (subSpaced == null) subSpaced = new GUIStyle(sSub) { fontStyle = FontStyle.Normal, fontSize = 34 }; return subSpaced; } }
        GUIStyle smallRight { get { if (smallRightStyle == null) smallRightStyle = new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight }; return smallRightStyle; } }

        void DrawPause()
        {
            Rect(new Rect(0, 0, VW, VH), new Color(0, 0, 0, 0.6f));
            Label(new Rect(0, VH * 0.25f, VW, 110f), "ПАУЗА", sTitle, Color.white);
            Tex(new Rect(VW * 0.5f - 240f, VH * 0.25f + 100f, 480f, 44f), divider, new Color(1f, 0.85f, 0.65f, 0.9f));
            string[] items = { "Продолжить", "Управление", "Выйти в меню" };
            for (int i = 0; i < items.Length; i++)
            {
                bool sel = i == pauseIndex;
                Label(new Rect(0, VH * 0.45f + i * 64f, VW, 56f), (sel ? "—  " : "") + items[i] + (sel ? "  —" : ""), sMenu, sel ? new Color(1f, 0.95f, 0.85f) : new Color(0.7f, 0.68f, 0.66f), 2f);
            }
            var s = Game.I.Save;
            int minutes = (int)(s.playTime / 60f);
            Label(new Rect(0, VH - 90f, VW, 40f), "Время в пути: " + minutes / 60 + " ч " + minutes % 60 + " мин   ·   Осколков воска: " + s.shards.Count + "/3", sSmall, new Color(1, 1, 1, 0.5f), 2f);
        }

        void DrawControls()
        {
            Rect(new Rect(0, 0, VW, VH), new Color(0.02f, 0.02f, 0.03f, 0.95f));
            Label(new Rect(0, 100f, VW, 110f), "УПРАВЛЕНИЕ", sTitle, Color.white);
            string[,] rows =
            {
                { "Движение", "← → / A D  (стик)" },
                { "Прыжок (держать — выше)", "Z / Пробел / K  (A)" },
                { "Удар (↑ вверх, ↓ вниз в воздухе)", "X / J  (X)" },
                { "Рывок", "C / Shift / L  (RB)" },
                { "Вспышка — нажать", "F / V / I  (B)" },
                { "Переплавка (лечение) — держать", "F / V / I  (B)" },
                { "Прочитать / говорить / отдохнуть", "↑ / W" },
                { "Спрыгнуть с уступа", "↓ + прыжок" },
                { "Пауза", "Esc  (Start)" },
            };
            float y = 270f;
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                Label(new Rect(VW * 0.5f - 620f, y, 640f, 50f), rows[i, 0], sText, new Color(0.9f, 0.88f, 0.85f), 2f);
                Label(new Rect(VW * 0.5f + 60f, y, 600f, 50f), rows[i, 1], sName, sName.normal.textColor, 2f);
                y += 60f;
            }
            Label(new Rect(0, VH - 120f, VW, 40f), "Удары по врагам наполняют сосуд воском. Воск — это и заклинание, и лечение.", sSmall, new Color(1f, 0.9f, 0.8f, 0.7f), 2f);
            Label(new Rect(0, VH - 70f, VW, 40f), "Z / Enter — назад", sSmall, new Color(1, 1, 1, 0.5f), 2f);
        }

        void DrawSplash()
        {
            float a = Mathf.Clamp01(splashT / 0.8f) * Mathf.Clamp01((3.4f - splashT) / 0.8f);
            Rect(new Rect(0, 0, VW, VH), new Color(0.03f, 0.02f, 0.03f, 1f));
            float s = 260f;
            float bob = Mathf.Sin(splashT * 2f) * 4f;
            Tex(new Rect(VW * 0.5f - s * 0.5f, VH * 0.3f - s * 0.5f + bob, s, s), logo, new Color(1, 1, 1, a));
            var big = new GUIStyle(sTitle) { fontSize = 84 };
            Label(new Rect(0, VH * 0.3f + 150f, VW, 110f), Lore.Studio, big, new Color(1f, 0.55f, 0.65f, a), 3f);
            Label(new Rect(0, VH * 0.3f + 250f, VW, 50f), "представляет", sSub, new Color(0.9f, 0.88f, 0.9f, a * 0.8f), 2f);
        }

        void DrawCine()
        {
            var st = new GUIStyle(sCenter) { fontSize = 40 };
            bool warm = fadeColor.r > 0.5f;
            Color c = warm ? new Color(0.25f, 0.15f, 0.08f, cineAlpha) : new Color(0.92f, 0.9f, 0.86f, cineAlpha);
            Label(new Rect(VW * 0.5f - 700f, VH * 0.5f - 200f, 1400f, 400f), cineText, st, c, warm ? 0f : 2f);
        }

        void DrawCredits()
        {
            Rect(new Rect(0, 0, VW, VH), new Color(0.03f, 0.02f, 0.03f, 1f));
            var st = new GUIStyle(sCenter) { fontSize = 38 };
            var head = new GUIStyle(sTitle) { fontSize = 64 };
            for (int i = 0; i < credits.Length; i++)
            {
                float y = creditsY + i * 64f;
                if (y < -100f || y > VH + 100f) continue;
                bool big = credits[i] == Lore.Studio || i == 0;
                Label(new Rect(0, y, VW, 70f), credits[i], big ? head : st, big ? new Color(1f, 0.6f, 0.7f) : new Color(0.92f, 0.9f, 0.86f), 2f);
            }
            if (logo != null) Tex(new Rect(VW * 0.5f - 90f, creditsY + credits.Length * 64f + 60f, 180f, 180f), logo, Color.white);
        }

        void DrawDeath()
        {
            float a = Mathf.Clamp01(deathT / 0.8f);
            Label(new Rect(0, VH * 0.4f, VW, 110f), "Пламя угасло", sTitle, new Color(0.9f, 0.88f, 0.85f, a));
            Tex(new Rect(VW * 0.5f - 260f, VH * 0.4f + 110f, 520f, 48f), divider, new Color(0.9f, 0.6f, 0.4f, a * 0.8f));
            Label(new Rect(0, VH * 0.4f + 170f, VW, 50f), "…но Свечная Мать ещё помнит твой фитиль", sSub, new Color(0.85f, 0.82f, 0.8f, a * 0.8f));
        }
    }
}
