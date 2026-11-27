using UnityEngine;
using UnityEngine.SceneManagement;

// The boss script. Put this on ONE empty GameObject in the scene.
// It also adds MazeGenerator and SpawnManager automatically.
[RequireComponent(typeof(MazeGenerator))]
[RequireComponent(typeof(SpawnManager))]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public PlayerController player;
    public bool gameOver;

    MazeGenerator maze;
    EnemyBase[] enemies = new EnemyBase[0];
    bool isDanger;
    GuideSystem guide;

    public EnemyBase[] Enemies { get { return enemies; } }
    string endMessage = "";
    string hint = "";
    float hintTimer;

    static readonly Color darkColor = new Color(0.03f, 0.04f, 0.06f);

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        if (GetComponent<AudioManager>() == null) gameObject.AddComponent<AudioManager>();
    }

    void Start()
    {
        maze = GetComponent<MazeGenerator>();

        SetupAtmosphere();

        // we make our own camera on the player, so remove the default one
        if (Camera.main != null) Destroy(Camera.main.gameObject);

        maze.Generate();
        CreatePlayer();
        GetComponent<SpawnManager>().SpawnEnemies(maze);
        enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);

        guide = gameObject.AddComponent<GuideSystem>();
        guide.Init(maze, player);
    }

    // dark mood: low ambient light, fog, dim sun
    void SetupAtmosphere()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.22f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.03f;
        RenderSettings.fogColor = darkColor;

        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.intensity = 0.25f;
    }

    void CreatePlayer()
    {
        GameObject p = new GameObject("Player");
        p.tag = "Player";
        p.transform.position = maze.PlayerStart;

        CharacterController cc = p.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);

        p.AddComponent<ScentTrail>();
        player = p.AddComponent<PlayerController>();

        GameObject camObj = new GameObject("PlayerCamera");
        camObj.tag = "MainCamera";
        camObj.transform.SetParent(p.transform, false);
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = darkColor;
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 70f;
        camObj.AddComponent<AudioListener>();

        // flashlight (press F to switch on / off)
        GameObject fl = new GameObject("Flashlight");
        fl.transform.SetParent(camObj.transform, false);
        Light spot = fl.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = 20f;
        spot.spotAngle = 65f;
        spot.intensity = 2.5f;
        spot.color = new Color(1f, 0.97f, 0.88f);
        spot.shadows = LightShadows.None;
        player.flashlight = spot;
    }

    void Update()
    {
        if (hintTimer > 0f) hintTimer -= Time.deltaTime;

        // is something chasing us? (red screen edge + heartbeat)
        isDanger = false;
        foreach (EnemyBase e in enemies)
            if (e != null && (e.state == AIState.Chase || e.state == AIState.Dash)) { isDanger = true; break; }
        if (AudioManager.Instance != null) AudioManager.Instance.SetDanger(isDanger && !gameOver);

        if (!gameOver && player != null)
        {
            // reached the exit?
            Vector3 a = player.transform.position;
            Vector3 b = maze.ExitPosition;
            a.y = 0f;
            b.y = 0f;
            if (Vector3.Distance(a, b) < 1.5f) Win();
        }

        if (gameOver && Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void ShowHint(string text)
    {
        hint = text;
        hintTimer = 2.5f;
    }

    public void Lose()
    {
        if (gameOver) return;
        if (AudioManager.Instance != null) AudioManager.Instance.Caught();
        EndGame("CAUGHT!   Press R to try a new maze");
    }

    void Win()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Win();
        EndGame("YOU ESCAPED!   Press R for a new maze");
    }

    void EndGame(string message)
    {
        gameOver = true;
        endMessage = message;
        if (AudioManager.Instance != null) AudioManager.Instance.SetDanger(false);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // On-screen display in a terminal style
    void OnGUI()
    {
        if (player == null) return;

        bool danger = isDanger;

        // red pulsing screen edge while something is chasing you
        if (danger && !gameOver)
        {
            GUI.color = new Color(1f, 0f, 0f, 0.12f + 0.06f * Mathf.Sin(Time.time * 8f));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // key slots (grey until you pick the key up)
        for (int i = 1; i <= 3; i++)
        {
            bool has = player.HasKey(i);
            Rect slot = new Rect(10 + (i - 1) * 74, 10, 66, 24);
            GUI.color = has ? Door.ColorOf(i) : new Color(0.3f, 0.3f, 0.3f, 0.6f);
            GUI.DrawTexture(slot, Texture2D.whiteTexture);
            GUI.color = Color.white;
            Hud.Label(slot, Door.NameOf(i), 14, has ? Color.white : new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter);

            // the slot you need next pulses with a white frame
            if (guide != null && guide.ObjectiveLevel == i)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Sin(Time.time * 6f));
                GUI.DrawTexture(new Rect(slot.x - 3f, slot.y - 3f, slot.width + 6f, 3f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(slot.x - 3f, slot.yMax, slot.width + 6f, 3f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(slot.x - 3f, slot.y, 3f, slot.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(slot.xMax, slot.y, 3f, slot.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }
        Hud.Label(new Rect(10, 40, 600, 24), "FACILITY SEED " + maze.CurrentSeed, 14, Hud.Green);

        // controls bar at the bottom
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, Screen.height - 36, Screen.width, 36), Texture2D.whiteTexture);
        GUI.color = Color.white;
        Hud.Label(new Rect(0, Screen.height - 34, Screen.width, 30),
            "[SHIFT] RUN    [C] CROUCH    [F] FLASHLIGHT    [M] MAP    [H] ROUTE    [G] GUIDE", 15, Hud.Green, TextAnchor.MiddleCenter);

        Hud.Label(new Rect(0, Screen.height / 2f - 15, Screen.width, 30), "+", 20, Color.white, TextAnchor.MiddleCenter);

        if (danger && !gameOver)
            Hud.Label(new Rect(0, 130, Screen.width, 44), "!! RUN !!", 34, Color.red, TextAnchor.MiddleCenter);

        if (hintTimer > 0f)
            Hud.Label(new Rect(0, Screen.height * 0.75f, Screen.width, 40), "> " + hint, 22, Hud.Green, TextAnchor.MiddleCenter);

        if (gameOver)
        {
            Color c = endMessage.StartsWith("YOU") ? Hud.Green : Color.red;
            Hud.Label(new Rect(0, Screen.height / 2f - 60, Screen.width, 80), endMessage, 40, c, TextAnchor.MiddleCenter);
        }
    }
}
