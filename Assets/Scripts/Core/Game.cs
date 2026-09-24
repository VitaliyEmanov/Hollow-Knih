using System.Collections;
using AshenWick.Art;
using AshenWick.World;
using UnityEngine;

namespace AshenWick
{
    public enum GameState { Boot, Splash, Title, Prologue, Playing, Paused, Dialogue, Panel, Transition, Dead, Ending }

    /// <summary>
    /// Root of "Hollow Knight: Пепел". Creates every subsystem from code, so the game
    /// runs from any (even empty) scene: see <see cref="Boot"/>.
    /// </summary>
    public sealed class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        public GameState State = GameState.Boot;
        public SaveData Save = new SaveData();

        public Room Room { get; private set; }
        public Player Player { get; private set; }
        public CameraRig Cam { get; private set; }
        public Fx Fx { get; private set; }
        public Sound Sound { get; private set; }
        public Hud Hud { get; private set; }

        float hitStop;
        float slowMo = 1f, slowMoTimer;
        bool busy;

        /// <summary>Gameplay is running and the player may act.</summary>
        public bool Live { get { return State == GameState.Playing; } }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
            Time.fixedDeltaTime = 1f / 60f;

            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            DontDestroyOnLoad(camGo);
            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            Cam = camGo.GetComponent<CameraRig>();
            if (Cam == null) Cam = camGo.AddComponent<CameraRig>();

            Fx = new GameObject("Fx").AddComponent<Fx>();
            Fx.transform.SetParent(transform);
            Sound = new GameObject("Sound").AddComponent<Sound>();
            Sound.transform.SetParent(transform);
            Hud = gameObject.AddComponent<Hud>();
            if (Application.platform != RuntimePlatform.WebGLPlayer) Prewarm.Start();
        }

        void Start()
        {
            StartCoroutine(BootFlow());
        }

        IEnumerator BootFlow()
        {
            State = GameState.Splash;
            Hud.FadeTo(1f, 0f);
            yield return null;
            // pre-warm expensive art while the splash is visible
            Hud.FadeTo(0f, 0.8f);
            yield return Hud.Splash();
            Hud.FadeTo(1f, 0.6f);
            yield return new WaitForSecondsRealtime(0.6f);
            ShowTitle();
        }

        public void ShowTitle()
        {
            StopAllCoroutines();
            busy = false;
            Time.timeScale = 1f;
            hitStop = 0; slowMo = 1; slowMoTimer = 0;
            DestroyPlayer();
            try { LoadRoomRaw("outskirts"); }
            catch (System.Exception e) { Debug.LogException(e); }
            Cam.SetImmediate(new Vector2(22f, 11f));
            Cam.Follow = null;
            State = GameState.Title;
            Sound.PlayMusic(-1);
            Hud.FadeTo(0f, 1.2f);
        }

        // ------------------------------------------------------------------
        // Main flow
        // ------------------------------------------------------------------

        public void NewGame()
        {
            if (busy) return;
            StartCoroutine(NewGameFlow());
        }

        IEnumerator NewGameFlow()
        {
            busy = true;
            SaveData.Erase();
            Save = new SaveData();
            Hud.FadeTo(1f, 0.8f);
            yield return new WaitForSecondsRealtime(0.9f);
            State = GameState.Prologue;
            Sound.PlayMusic(-2);
            yield return Hud.Prologue(Lore.Prologue);
            SpawnPlayer();
            LoadRoom("outskirts", '\0', true);
            State = GameState.Playing;
            Hud.FadeTo(0f, 1.5f);
            busy = false;
        }

        public void Continue()
        {
            if (busy) return;
            StartCoroutine(ContinueFlow());
        }

        IEnumerator ContinueFlow()
        {
            busy = true;
            Save = SaveData.Load();
            Hud.FadeTo(1f, 0.6f);
            yield return new WaitForSecondsRealtime(0.7f);
            SpawnPlayer();
            LoadRoom(Save.benchRoom, 'B', true);
            State = GameState.Playing;
            Hud.FadeTo(0f, 1f);
            busy = false;
        }

        void SpawnPlayer()
        {
            DestroyPlayer();
            var go = new GameObject("Niv");
            DontDestroyOnLoad(go);
            Player = go.AddComponent<Player>();
            Player.Init(Save);
        }

        void DestroyPlayer()
        {
            if (Player != null) Destroy(Player.gameObject);
            Player = null;
        }

        /// <summary>Builds a room without touching the player (title screen backdrop).</summary>
        void LoadRoomRaw(string id)
        {
            if (Room != null)
            {
                Room.gameObject.SetActive(false);
                Destroy(Room.gameObject);
            }
            Combat.Clear();
            Fx.ClearAll();
            Hud.HideBossBar();
            var def = RoomDefs.Get(id) ?? RoomDefs.Get("outskirts");
            Room = new GameObject("Room " + def.Id).AddComponent<Room>();
            Room.Build(def, Save);
            Cam.SetBounds(Room.Bounds);
            Sound.PlayMusic(def.Area);
        }

        /// <summary>Loads a room and places the player: at a door, a bench ('B') or the new game spot ('\0').</summary>
        void LoadRoom(string id, char entry, bool snapCamera)
        {
            try { LoadRoomCore(id, entry, snapCamera); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        void LoadRoomCore(string id, char entry, bool snapCamera)
        {
            LoadRoomRaw(id);
            Vector2 pos;
            Vector2 vel = Vector2.zero;
            int walkDir = 0;
            if (entry == 'B') pos = Room.BenchPos + new Vector2(0f, 0.62f);
            else if (entry == '\0') pos = Room.StartPos + new Vector2(0f, 0.62f);
            else Room.DoorArrival(entry, out pos, out vel, out walkDir);
            Player.PlaceAt(pos, vel, walkDir);
            Player.gameObject.SetActive(true);
            Cam.Follow = Player.transform;
            if (snapCamera) Cam.SnapTo(Player.transform.position);
            Room.OnPlayerEntered();
        }

        public void UseDoor(DoorLink link)
        {
            if (busy || State != GameState.Playing) return;
            StartCoroutine(DoorFlow(link));
        }

        IEnumerator DoorFlow(DoorLink link)
        {
            busy = true;
            State = GameState.Transition;
            Hud.FadeTo(1f, 0.22f);
            yield return new WaitForSecondsRealtime(0.24f);
            LoadRoom(link.Room, link.Door, true);
            yield return null;
            State = GameState.Playing;
            Hud.FadeTo(0f, 0.3f);
            busy = false;
        }

        // ------------------------------------------------------------------
        // Death / hazards / rest
        // ------------------------------------------------------------------

        public void OnPlayerDeath()
        {
            if (State == GameState.Dead) return;
            StartCoroutine(DeathFlow());
        }

        IEnumerator DeathFlow()
        {
            busy = true;
            State = GameState.Dead;
            Sound.StopMusic(1.5f);
            SlowMotion(0.25f, 1.2f);
            yield return new WaitForSecondsRealtime(1.6f);
            Hud.FadeTo(1f, 1f);
            yield return new WaitForSecondsRealtime(1.1f);
            Hud.ShowDeathText(true);
            yield return new WaitForSecondsRealtime(2.4f);
            Hud.ShowDeathText(false);
            SpawnPlayer();
            LoadRoom(Save.benchRoom, 'B', true);
            Player.SitOnBench();
            State = GameState.Playing;
            Hud.FadeTo(0f, 1.2f);
            busy = false;
        }

        public void OnHazard(Vector2 safePos)
        {
            if (busy) return;
            StartCoroutine(HazardFlow(safePos));
        }

        IEnumerator HazardFlow(Vector2 safePos)
        {
            busy = true;
            State = GameState.Transition;
            yield return new WaitForSecondsRealtime(0.35f);
            Hud.FadeTo(1f, 0.2f);
            yield return new WaitForSecondsRealtime(0.25f);
            Player.PlaceAt(safePos, Vector2.zero, 0);
            Cam.SnapTo(Player.transform.position);
            yield return new WaitForSecondsRealtime(0.1f);
            State = GameState.Playing;
            Hud.FadeTo(0f, 0.3f);
            busy = false;
        }

        public void RestAtBench()
        {
            Save.benchRoom = Room.Def.Id;
            Save.maxFlames = Player.MaxFlames;
            Save.Write();
        }

        // ------------------------------------------------------------------
        // Pause, dialogue, panels
        // ------------------------------------------------------------------

        void Update()
        {
            // time control (hit-stop and slow motion never override pause/panels)
            if (hitStop > 0f) hitStop -= Time.unscaledDeltaTime;
            if (slowMoTimer > 0f)
            {
                slowMoTimer -= Time.unscaledDeltaTime;
                if (slowMoTimer <= 0f) slowMo = 1f;
            }
            bool frozen = State == GameState.Paused || State == GameState.Panel;
            Time.timeScale = frozen ? 0f : (hitStop > 0f ? 0.02f : slowMo);

            if (Controls.PausePressed)
            {
                if (State == GameState.Playing) { State = GameState.Paused; Sound.Play("ui", 0.5f); }
                else if (State == GameState.Paused) { State = GameState.Playing; Sound.Play("ui", 0.5f); }
            }

            if (State == GameState.Playing || State == GameState.Dialogue || State == GameState.Panel)
                Save.playTime += Time.unscaledDeltaTime;
        }

        public void Resume() { if (State == GameState.Paused) State = GameState.Playing; }

        public void QuitToTitle()
        {
            StartCoroutine(QuitFlow());
        }

        IEnumerator QuitFlow()
        {
            State = GameState.Transition;
            Hud.FadeTo(1f, 0.5f);
            yield return new WaitForSecondsRealtime(0.55f);
            ShowTitle();
        }

        public void HitStop(float seconds)
        {
            hitStop = Mathf.Max(hitStop, seconds);
        }

        public void SlowMotion(float factor, float seconds)
        {
            slowMo = factor;
            slowMoTimer = seconds;
        }

        public void StartDialogue(string name, string[] lines, System.Action onDone = null)
        {
            if (State != GameState.Playing) return;
            State = GameState.Dialogue;
            Hud.ShowDialogue(name, lines, () =>
            {
                if (State == GameState.Dialogue) State = GameState.Playing;
                if (onDone != null) onDone();
            });
        }

        public void ShowPanel(Lore.AbilityInfo info, System.Action onDone = null)
        {
            if (info == null) return;
            var prev = State;
            State = GameState.Panel;
            Sound.Play("pickup", 0.9f);
            Hud.ShowPanel(info, () =>
            {
                State = prev == GameState.Panel ? GameState.Playing : prev;
                if (onDone != null) onDone();
            });
        }

        // ------------------------------------------------------------------
        // Progress
        // ------------------------------------------------------------------

        public void GrantAbility(string id)
        {
            switch (id)
            {
                case "dash": Save.hasDash = true; break;
                case "spell": Save.hasSpell = true; break;
                case "doublejump": Save.hasDoubleJump = true; break;
            }
            Save.maxFlames = Player != null ? Player.MaxFlames : Save.maxFlames;
            Save.Write();
            if (Player != null) Player.RefreshAbilities(Save);
            Fx.Flash(new Color(1f, 0.85f, 0.6f), 0.6f);
            Cam.Shake(0.35f);
            ShowPanel(Lore.Ability(id));
        }

        public void OnBossDefeated(string bossId)
        {
            if (!Save.bosses.Contains(bossId)) Save.bosses.Add(bossId);
            Save.Write();
        }

        public void BeginEnding()
        {
            if (busy) return;
            StartCoroutine(EndingFlow());
        }

        IEnumerator EndingFlow()
        {
            busy = true;
            State = GameState.Ending;
            Save.finished = true;
            Save.Write();
            Sound.PlayMusic(-3);
            Hud.FadeTo(1f, 3f, new Color(1f, 0.93f, 0.8f));
            yield return new WaitForSecondsRealtime(3.2f);
            yield return Hud.Prologue(Lore.Ending);
            yield return Hud.Credits(Lore.Credits);
            busy = false;
            ShowTitle();
        }
    }

    /// <summary>Starts the game automatically in whatever scene is opened.</summary>
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Game.I != null) return;
            new GameObject("Hollow Knight — Пепел").AddComponent<Game>();
        }
    }
}
