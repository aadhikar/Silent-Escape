using UnityEngine;
using UnityEngine.AI;

// Helps the player find the way out:
//  - Objective text + arrow at the top: what to do next and which direction (straight line)
//  - Mini map (top right) that fills in as you explore. Press M for a big map.
//  - Press H to draw the REAL walking route to the objective on the floor (has a cooldown)
public class GuideSystem : MonoBehaviour
{
    public bool showArrow = true;
    public float hintDuration = 8f;
    public float hintCooldown = 15f;

    MazeGenerator maze;
    PlayerController player;
    Texture2D mapTex;
    Texture2D arrowTex;
    bool[,] known;
    LineRenderer line;

    bool bigMap;
    bool showGuide = true;
    Camera cam;
    Color objectiveColor = Color.white;
    string objectiveShort = "";
    public int ObjectiveLevel { get; private set; }   // 1-3 = Red/Blue/Green key or door, 0 = exit
    bool hasObjective;
    string objectiveText = "";
    Vector3 objectivePos;
    float hintTimer, cooldownTimer, pathTimer;

    public void Init(MazeGenerator m, PlayerController p)
    {
        maze = m;
        player = p;
        cam = player.GetComponentInChildren<Camera>();

        int n = maze.TileCount;
        known = new bool[n, n];
        mapTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        mapTex.filterMode = FilterMode.Point;
        mapTex.wrapMode = TextureWrapMode.Clamp;
        mapTex.SetPixels(new Color[n * n]);   // all transparent
        mapTex.Apply();

        arrowTex = Gfx.MakeTexture(64, (x, y) =>
        {
            float half = (56 - y) * 0.45f;
            bool inside = y >= 8 && y <= 56 && Mathf.Abs(x - 32) <= half;
            return inside ? Color.white : new Color(1f, 1f, 1f, 0f);
        });

        GameObject lg = new GameObject("PathLine");
        lg.transform.SetParent(transform, false);
        line = lg.AddComponent<LineRenderer>();
        line.material = Gfx.Mat(new Color(0.2f, 1f, 1f), 2f);
        line.startWidth = 0.18f;
        line.endWidth = 0.18f;
        line.useWorldSpace = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.enabled = false;
    }

    void Update()
    {
        if (player == null || maze == null) return;

        UpdateObjective();
        RevealMap();

        if (Input.GetKeyDown(KeyCode.M)) bigMap = !bigMap;
        if (Input.GetKeyDown(KeyCode.G)) showGuide = !showGuide;

        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.H) && cooldownTimer <= 0f && hasObjective)
        {
            hintTimer = hintDuration;
            cooldownTimer = hintCooldown;
            pathTimer = 0f;
        }

        if (hintTimer > 0f)
        {
            hintTimer -= Time.deltaTime;
            pathTimer -= Time.deltaTime;
            if (pathTimer <= 0f) { pathTimer = 0.4f; UpdateLine(); }
            if (hintTimer <= 0f) line.enabled = false;
        }
    }

    // ------------------------------------------------------------
    // What should the player do next?  Key 1 > Door 1 > Key 2 > Door 2 > Key 3 > Door 3 > Exit
    // ------------------------------------------------------------
    void UpdateObjective()
    {
        hasObjective = true;
        for (int n = 1; n <= 3; n++)
        {
            string colorName = Door.NameOf(n);

            if (!player.HasKey(n))
            {
                KeyCard k = FindKey(n);
                if (k != null)
                {
                    SetObjective(k.transform.position + Vector3.up * 0.3f, "Find the " + colorName + " key card",
                                 colorName + " key", Door.ColorOf(n), n);
                    return;
                }
            }

            Door d = FindDoor(n);
            if (d != null && !d.isOpen)
            {
                SetObjective(d.transform.position, "Go to the " + colorName + " door",
                             colorName + " door", Door.ColorOf(n), n);
                return;
            }
        }

        SetObjective(maze.ExitPosition + Vector3.up * 1.5f, "Reach the yellow exit pillar", "Exit", Color.yellow, 0);
    }

    void SetObjective(Vector3 pos, string text, string shortName, Color color, int level)
    {
        objectivePos = pos;
        objectiveText = text;
        objectiveShort = shortName;
        objectiveColor = color;
        ObjectiveLevel = level;
    }

    KeyCard FindKey(int level)
    {
        foreach (KeyCard k in KeyCard.All) if (k != null && k.level == level) return k;
        return null;
    }

    Door FindDoor(int level)
    {
        foreach (Door d in Door.All) if (d != null && d.level == level) return d;
        return null;
    }

    // draws the real walking route on the floor
    void UpdateLine()
    {
        Vector3 target = objectivePos;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(objectivePos, out hit, 4f, NavMesh.AllAreas)) target = hit.position;

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(player.transform.position, target, NavMesh.AllAreas, path) || path.corners.Length < 2)
        {
            line.enabled = false;
            return;
        }

        Vector3[] c = path.corners;
        line.positionCount = c.Length;
        for (int i = 0; i < c.Length; i++) line.SetPosition(i, c[i] + Vector3.up * 0.15f);
        line.enabled = true;
    }

    // ------------------------------------------------------------
    // Map (fog of war: tiles appear when you get close)
    // ------------------------------------------------------------
    void RevealMap()
    {
        int n = maze.TileCount;
        int px = Mathf.RoundToInt(player.transform.position.x / maze.tileSize);
        int py = Mathf.RoundToInt(player.transform.position.z / maze.tileSize);
        bool changed = false;

        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                int x = px + dx, y = py + dy;
                if (x < 0 || y < 0 || x >= n || y >= n || known[x, y]) continue;
                known[x, y] = true;
                mapTex.SetPixel(x, y, TileColor(x, y));
                changed = true;
            }
        }
        if (changed) mapTex.Apply();
    }

    Color TileColor(int x, int y)
    {
        if (!maze.OpenTiles[x, y]) return new Color(0.2f, 0.22f, 0.27f, 1f);
        int door = maze.DoorLevels[x, y];
        if (door > 0) return Door.ColorOf(door);
        if (x == maze.ExitTileCoord.x && y == maze.ExitTileCoord.y) return Color.yellow;
        return new Color(0.75f, 0.82f, 0.9f, 1f);
    }

    Vector2 WorldToMap(Vector3 world, Rect r)
    {
        float n = maze.TileCount;
        float u = (world.x / maze.tileSize + 0.5f) / n;
        float v = (world.z / maze.tileSize + 0.5f) / n;
        return new Vector2(r.x + u * r.width, r.y + (1f - v) * r.height);
    }

    // ------------------------------------------------------------
    void OnGUI()
    {
        if (player == null || maze == null || mapTex == null) return;
        if (showGuide && hasObjective)
        {
            DrawArrow();
            DrawWaypoint();
        }
        DrawMap();
    }

    static Color Bright(Color c)
    {
        return Color.Lerp(c, Color.white, 0.35f);   // blue is hard to read on dark, so lighten it
    }

    void DrawDiamond(Vector2 c, float size, Color color)
    {
        GUIUtility.RotateAroundPivot(45f, c);
        GUI.color = color;
        GUI.DrawTexture(new Rect(c.x - size / 2f, c.y - size / 2f, size, size), Texture2D.whiteTexture);
        GUI.matrix = Matrix4x4.identity;
        GUI.color = Color.white;
    }

    // Big arrow at the top. Its colour = the key / door you need next.
    void DrawArrow()
    {
        Color c = Bright(objectiveColor);
        Vector3 to = objectivePos - player.transform.position;
        to.y = 0f;
        Vector3 fwd = player.transform.forward;
        fwd.y = 0f;
        float angle = Vector3.SignedAngle(fwd, to, Vector3.up);

        float cx = Screen.width / 2f;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(cx - 34f, 8f, 68f, 68f), Texture2D.whiteTexture);

        Vector2 center = new Vector2(cx, 42f);
        GUIUtility.RotateAroundPivot(angle, center);
        GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - 26f, center.y - 26f, 52f, 52f), arrowTex);
        GUI.matrix = Matrix4x4.identity;
        GUI.color = Color.white;

        Hud.Label(new Rect(cx - 400f, 82f, 800f, 28f),
            "> " + objectiveText + "  [" + Mathf.RoundToInt(to.magnitude) + " M]", 20, c, TextAnchor.MiddleCenter);

        string hintText = cooldownTimer > 0f ? "ROUTE SCAN RECHARGING " + Mathf.CeilToInt(cooldownTimer) + "S" : "ROUTE SCAN READY [H]";
        Hud.Label(new Rect(cx - 400f, 110f, 800f, 22f), hintText, 14, new Color(0.4f, 0.8f, 0.9f), TextAnchor.MiddleCenter);
    }

    // Marker floating on the screen where the target is (like a waypoint).
    // If the target is off screen or behind you, it sticks to the edge of the screen.
    void DrawWaypoint()
    {
        if (cam == null) return;

        Vector3 sp = cam.WorldToScreenPoint(objectivePos);
        bool behind = sp.z < 0f;
        float x = sp.x;
        float y = sp.y;
        if (behind) { x = Screen.width - x; y = Screen.height - y; }

        float margin = 50f;
        if (behind) x = x < Screen.width / 2f ? margin : Screen.width - margin;
        x = Mathf.Clamp(x, margin, Screen.width - margin);
        float gy = Mathf.Clamp(Screen.height - y, 150f, Screen.height - 90f);

        Color c = Bright(objectiveColor);
        float size = 20f + 3f * Mathf.Sin(Time.time * 5f);
        DrawDiamond(new Vector2(x, gy), size + 6f, new Color(0f, 0f, 0f, 0.7f));
        DrawDiamond(new Vector2(x, gy), size, c);

        int meters = Mathf.RoundToInt(Vector3.Distance(player.transform.position, objectivePos));
        Hud.Label(new Rect(x - 90f, gy + 16f, 180f, 22f), objectiveShort + "  " + meters + " M", 14, c, TextAnchor.MiddleCenter);
    }

    void DrawMap()
    {
        float size = bigMap ? Mathf.Min(Screen.height * 0.8f, 600f) : 190f;
        Rect r = bigMap
            ? new Rect((Screen.width - size) / 2f, (Screen.height - size) / 2f, size, size)
            : new Rect(Screen.width - size - 10f, 10f, size, size);

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(r, mapTex);

        // target marker in the key / door colour (always shown while the guide is on)
        if (showGuide && hasObjective)
        {
            Vector2 o = WorldToMap(objectivePos, r);
            float s = 8f + 2f * Mathf.Sin(Time.time * 6f);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(o.x - s / 2f - 1.5f, o.y - s / 2f - 1.5f, s + 3f, s + 3f), Texture2D.whiteTexture);
            GUI.color = Bright(objectiveColor);
            GUI.DrawTexture(new Rect(o.x - s / 2f, o.y - s / 2f, s, s), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Hud.Label(new Rect(r.x, r.yMax + 2f, r.width, 18f), "TARGET: " + objectiveShort, 12, Bright(objectiveColor), TextAnchor.UpperCenter);
        }

        // player arrow
        Vector2 p = WorldToMap(player.transform.position, r);
        GUIUtility.RotateAroundPivot(player.transform.eulerAngles.y, p);
        GUI.color = Color.cyan;
        GUI.DrawTexture(new Rect(p.x - 7f, p.y - 7f, 14f, 14f), arrowTex);
        GUI.matrix = Matrix4x4.identity;
        GUI.color = Color.white;

        Hud.Label(new Rect(r.x, r.y, r.width, 18f), "N", 12, Hud.Green, TextAnchor.UpperCenter);
    }
}
