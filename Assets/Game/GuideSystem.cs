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
    bool hasObjective;
    string objectiveText = "";
    Vector3 objectivePos;
    float hintTimer, cooldownTimer, pathTimer;

    public void Init(MazeGenerator m, PlayerController p)
    {
        maze = m;
        player = p;

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
            if (!player.HasKey(n))
            {
                KeyCard k = FindKey(n);
                if (k != null)
                {
                    objectivePos = k.transform.position;
                    objectiveText = "Find the " + Door.NameOf(n) + " key card";
                    return;
                }
            }

            Door d = FindDoor(n);
            if (d != null && !d.isOpen)
            {
                objectivePos = d.transform.position;
                objectiveText = "Go to the " + Door.NameOf(n) + " door";
                return;
            }
        }

        objectivePos = maze.ExitPosition;
        objectiveText = "Reach the yellow exit pillar!";
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

        // objective arrow + text
        if (hasObjective)
        {
            Vector3 to = objectivePos - player.transform.position;
            to.y = 0f;
            Vector3 fwd = player.transform.forward;
            fwd.y = 0f;
            float angle = Vector3.SignedAngle(fwd, to, Vector3.up);

            if (showArrow)
            {
                Vector2 c = new Vector2(Screen.width / 2f, 48f);
                GUIUtility.RotateAroundPivot(angle, c);
                GUI.color = Color.cyan;
                GUI.DrawTexture(new Rect(c.x - 20f, c.y - 20f, 40f, 40f), arrowTex);
                GUI.matrix = Matrix4x4.identity;
                GUI.color = Color.white;
            }

            Hud.Label(new Rect(0, 74, Screen.width, 28), "> " + objectiveText + "  [" + Mathf.RoundToInt(to.magnitude) + " M]", 20, Hud.Green, TextAnchor.MiddleCenter);

            string hintText = cooldownTimer > 0f ? "ROUTE SCAN RECHARGING " + Mathf.CeilToInt(cooldownTimer) + "S" : "ROUTE SCAN READY [H]";
            Hud.Label(new Rect(0, 100, Screen.width, 22), hintText, 14, new Color(0.4f, 0.8f, 0.9f), TextAnchor.MiddleCenter);
        }

        // map
        float size = bigMap ? Mathf.Min(Screen.height * 0.8f, 600f) : 190f;
        Rect r = bigMap
            ? new Rect((Screen.width - size) / 2f, (Screen.height - size) / 2f, size, size)
            : new Rect(Screen.width - size - 10f, 10f, size, size);

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(r, mapTex);

        // objective marker only while the route hint is active
        if (hintTimer > 0f)
        {
            Vector2 o = WorldToMap(objectivePos, r);
            GUI.color = Color.red;
            GUI.DrawTexture(new Rect(o.x - 4f, o.y - 4f, 8f, 8f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // player arrow
        Vector2 p = WorldToMap(player.transform.position, r);
        GUIUtility.RotateAroundPivot(player.transform.eulerAngles.y, p);
        GUI.color = Color.cyan;
        GUI.DrawTexture(new Rect(p.x - 7f, p.y - 7f, 14f, 14f), arrowTex);
        GUI.matrix = Matrix4x4.identity;
        GUI.color = Color.white;
    }
}
