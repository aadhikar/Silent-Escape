using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

// Builds the whole level:
//  1. carves a random maze
//  2. finds the longest route (start -> exit) and puts 3 locked doors on it (Red, Blue, Green)
//  3. opens extra ROOMS and SHORTCUTS so there is more than one way around (never around a door!)
//  4. hides each key BEFORE its door, then checks the level can really be finished
//  5. builds floor, walls, ceiling, lamps, then bakes the NavMesh
// Keep this object at position (0,0,0).
public class MazeGenerator : MonoBehaviour
{
    [Header("Maze settings")]
    public int cells = 12;          // maze is cells x cells (use 8 or more)
    public float tileSize = 3f;
    public float wallHeight = 3f;
    public int seed = 0;            // 0 = random every time

    [Header("Extra paths")]
    public int rooms = 5;           // open 3x3 rooms (more space to hide / run around)
    public int shortcuts = 25;      // extra openings that make loops

    public Vector3 PlayerStart { get; private set; }
    public Vector3 ExitPosition { get; private set; }
    public int CurrentSeed { get; private set; }

    // read-only info for the map and guide
    public bool[,] OpenTiles { get { return open; } }
    public int[,] DoorLevels { get { return doorLevel; } }
    public int TileCount { get { return size; } }
    public Vector2Int ExitTileCoord { get { return exitTile; } }

    int size;                       // tiles per side
    bool[,] open;                   // true = floor, false = wall
    int[,] doorLevel;               // 0 = no door, 1..3 = door tier
    int[,] region;                  // how many doors you must pass to reach this tile
    List<Vector2Int> path = new List<Vector2Int>();
    Vector2Int startTile = new Vector2Int(1, 1);
    Vector2Int exitTile;
    Vector2Int[] doorTiles = new Vector2Int[4];   // index 1..3
    Vector2Int[] keyTiles = new Vector2Int[4];    // index 1..3
    Transform worldRoot;

    static readonly Vector2Int[] steps =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    // ------------------------------------------------------------
    public void Generate()
    {
        CurrentSeed = seed != 0 ? seed : Random.Range(1, 999999);
        size = cells * 2 + 1;

        // build the layout on paper first, and check it can be finished
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Random.InitState(CurrentSeed);
            CarveMaze();
            FindPath();
            PlaceDoors();
            FindRegions();
            AddRoomsAndShortcuts();
            ChooseKeyTiles();
            if (LayoutIsValid()) break;
            Debug.LogWarning("Layout " + CurrentSeed + " failed the check, trying the next seed.");
            CurrentSeed++;
        }

        BuildWorld();
        BakeNavMesh();
        BuildDoorObjects();   // doors are made AFTER the bake so the NavMesh has no hole
        SpawnKeys();
        BuildLights();
    }

    // Positions of all floor tiles in a region (used to spawn enemies)
    public List<Vector3> GetRegionPositions(int level)
    {
        List<Vector3> list = new List<Vector3>();
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                if (open[x, y] && doorLevel[x, y] == 0 && region[x, y] == level)
                    list.Add(TileToWorld(new Vector2Int(x, y)));
        return list;
    }

    // ------------------------------------------------------------
    // 1) MAZE (random depth-first walk, a "perfect maze" with no loops yet)
    // ------------------------------------------------------------
    void CarveMaze()
    {
        open = new bool[size, size];
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        open[startTile.x, startTile.y] = true;
        stack.Push(startTile);

        Vector2Int[] jumps =
        {
            new Vector2Int(0, 2), new Vector2Int(0, -2), new Vector2Int(2, 0), new Vector2Int(-2, 0)
        };

        while (stack.Count > 0)
        {
            Vector2Int cur = stack.Peek();
            List<Vector2Int> options = new List<Vector2Int>();
            foreach (Vector2Int j in jumps)
            {
                Vector2Int n = cur + j;
                bool inside = n.x > 0 && n.y > 0 && n.x < size - 1 && n.y < size - 1;
                if (inside && !open[n.x, n.y]) options.Add(j);
            }

            if (options.Count == 0) { stack.Pop(); continue; }

            Vector2Int pick = options[Random.Range(0, options.Count)];
            open[cur.x + pick.x / 2, cur.y + pick.y / 2] = true;   // knock down the wall between
            open[cur.x + pick.x, cur.y + pick.y] = true;
            stack.Push(cur + pick);
        }
    }

    // ------------------------------------------------------------
    // 2) LONGEST ROUTE: the exit is the tile farthest from the start
    // ------------------------------------------------------------
    void FindPath()
    {
        int[,] dist = new int[size, size];
        Vector2Int[,] parent = new Vector2Int[size, size];
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                dist[x, y] = -1;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        dist[startTile.x, startTile.y] = 0;
        queue.Enqueue(startTile);
        Vector2Int farthest = startTile;

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            if (dist[cur.x, cur.y] > dist[farthest.x, farthest.y]) farthest = cur;
            foreach (Vector2Int s in steps)
            {
                Vector2Int n = cur + s;
                if (open[n.x, n.y] && dist[n.x, n.y] < 0)
                {
                    dist[n.x, n.y] = dist[cur.x, cur.y] + 1;
                    parent[n.x, n.y] = cur;
                    queue.Enqueue(n);
                }
            }
        }

        exitTile = farthest;
        path.Clear();
        for (Vector2Int t = exitTile; t != startTile; t = parent[t.x, t.y]) path.Add(t);
        path.Add(startTile);
        path.Reverse();   // path[0] = start, last = exit
    }

    // ------------------------------------------------------------
    // 3) DOORS on the route. While the maze has no loops, every tile
    //    on the route is a real chokepoint: you MUST pass through it.
    // ------------------------------------------------------------
    void PlaceDoors()
    {
        doorLevel = new int[size, size];
        int last = path.Count;
        int lowest = 2;

        for (int n = 1; n <= 3; n++)
        {
            int target = (n == 1) ? last / 4 : (n == 2) ? last / 2 : last - 2;
            int highest = last - 2;
            int chosen = Mathf.Clamp(target, lowest, highest);

            // look for a straight corridor tile close to the target (looks nicer)
            for (int k = 0; k < last; k++)
            {
                int a = target - k;
                int b = target + k;
                if (a >= lowest && a <= highest && IsStraight(path[a])) { chosen = a; break; }
                if (n < 3 && b >= lowest && b <= highest && IsStraight(path[b])) { chosen = b; break; }
            }

            doorTiles[n] = path[chosen];
            doorLevel[doorTiles[n].x, doorTiles[n].y] = n;
            lowest = chosen + 2;
        }
    }

    bool IsStraight(Vector2Int t)
    {
        bool horizontal = open[t.x - 1, t.y] && open[t.x + 1, t.y];
        bool vertical = open[t.x, t.y - 1] && open[t.x, t.y + 1];
        return OpenNeighbours(t) == 2 && (horizontal || vertical);
    }

    int OpenNeighbours(Vector2Int t)
    {
        int count = 0;
        foreach (Vector2Int s in steps)
            if (open[t.x + s.x, t.y + s.y]) count++;
        return count;
    }

    // Region = how many doors you need to pass to get there
    void FindRegions()
    {
        region = new int[size, size];
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                region[x, y] = -1;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        region[startTile.x, startTile.y] = 0;
        queue.Enqueue(startTile);

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            foreach (Vector2Int s in steps)
            {
                Vector2Int n = cur + s;
                if (open[n.x, n.y] && region[n.x, n.y] < 0)
                {
                    // standing on a door tile and stepping forward = one more door passed
                    region[n.x, n.y] = region[cur.x, cur.y] + (doorLevel[cur.x, cur.y] > 0 ? 1 : 0);
                    queue.Enqueue(n);
                }
            }
        }
    }

    // ------------------------------------------------------------
    // NEW PATHS: rooms and shortcuts.
    // Only tiles from the SAME region are joined, and never next to a door,
    // so nobody can walk around a locked door.
    // ------------------------------------------------------------
    void AddRoomsAndShortcuts()
    {
        // rooms: a 3x3 block that merges four maze cells into one open space
        int made = 0;
        for (int tries = 0; tries < 300 && made < rooms; tries++)
        {
            int x = Random.Range(0, cells - 1) * 2 + 1;
            int y = Random.Range(0, cells - 1) * 2 + 1;
            if (!CanOpenRoom(x, y)) continue;

            int r = region[x, y];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    open[x + i, y + j] = true;
                    region[x + i, y + j] = r;
                }
            made++;
        }

        // shortcuts: knock down a wall between two corridors of the same region
        int opened = 0;
        for (int tries = 0; tries < 600 && opened < shortcuts; tries++)
        {
            int x = Random.Range(1, size - 1);
            int y = Random.Range(1, size - 1);
            if (open[x, y]) continue;

            Vector2Int a, b;
            if (x % 2 == 0 && y % 2 == 1) { a = new Vector2Int(x - 1, y); b = new Vector2Int(x + 1, y); }
            else if (x % 2 == 1 && y % 2 == 0) { a = new Vector2Int(x, y - 1); b = new Vector2Int(x, y + 1); }
            else continue;

            if (!open[a.x, a.y] || !open[b.x, b.y]) continue;
            if (doorLevel[a.x, a.y] > 0 || doorLevel[b.x, b.y] > 0) continue;
            if (region[a.x, a.y] != region[b.x, b.y]) continue;
            if (NearDoor(x, y)) continue;

            open[x, y] = true;
            region[x, y] = region[a.x, a.y];
            opened++;
        }
    }

    bool CanOpenRoom(int x, int y)
    {
        int r = region[x, y];
        if (r < 0) return false;

        for (int i = -1; i <= 3; i++)
        {
            for (int j = -1; j <= 3; j++)
            {
                int tx = x + i, ty = y + j;
                bool insideRoom = i >= 0 && i <= 2 && j >= 0 && j <= 2;

                if (doorLevel[tx, ty] > 0) return false;                       // no door in or next to the room
                if (!insideRoom) continue;

                bool isCell = (i % 2 == 0) && (j % 2 == 0);
                if (isCell && (!open[tx, ty] || region[tx, ty] != r)) return false;   // all 4 cells: same region
                if (open[tx, ty] && region[tx, ty] != r) return false;
                if (tx == startTile.x && ty == startTile.y) return false;
                if (tx == exitTile.x && ty == exitTile.y) return false;
            }
        }
        return true;
    }

    bool NearDoor(int x, int y)
    {
        if (doorLevel[x, y] > 0) return true;
        foreach (Vector2Int s in steps)
            if (doorLevel[x + s.x, y + s.y] > 0) return true;
        return false;
    }

    // ------------------------------------------------------------
    // 4) KEYS: key N hides in region N-1 (before door N). Prefer dead ends.
    // ------------------------------------------------------------
    void ChooseKeyTiles()
    {
        HashSet<Vector2Int> used = new HashSet<Vector2Int>();

        for (int n = 1; n <= 3; n++)
        {
            keyTiles[n] = new Vector2Int(-1, -1);
            List<Vector2Int> options = new List<Vector2Int>();
            List<Vector2Int> deadEnds = new List<Vector2Int>();

            for (int x = 1; x < size - 1; x++)
            {
                for (int y = 1; y < size - 1; y++)
                {
                    if (!open[x, y] || doorLevel[x, y] > 0 || region[x, y] != n - 1) continue;
                    Vector2Int t = new Vector2Int(x, y);
                    if (t == startTile || t == exitTile || used.Contains(t)) continue;
                    options.Add(t);
                    if (OpenNeighbours(t) == 1) deadEnds.Add(t);
                }
            }

            List<Vector2Int> pool = deadEnds.Count > 0 ? deadEnds : options;
            if (pool.Count == 0) continue;

            keyTiles[n] = pool[Random.Range(0, pool.Count)];
            used.Add(keyTiles[n]);
        }
    }

    // Final safety check: with doors 1..n-1 open and n..3 locked, key n must be
    // reachable and the exit must NOT be. With everything open the exit must be reachable.
    bool LayoutIsValid()
    {
        for (int n = 1; n <= 3; n++)
        {
            if (keyTiles[n].x < 0) return false;
            bool[,] seen = Flood(n);
            if (!seen[keyTiles[n].x, keyTiles[n].y]) return false;
            if (seen[exitTile.x, exitTile.y]) return false;
        }
        bool[,] everything = Flood(4);
        return everything[exitTile.x, exitTile.y];
    }

    // Which tiles can we reach from the start if doors with level >= lockedFrom are locked?
    bool[,] Flood(int lockedFrom)
    {
        bool[,] seen = new bool[size, size];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        seen[startTile.x, startTile.y] = true;
        queue.Enqueue(startTile);

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            foreach (Vector2Int s in steps)
            {
                Vector2Int n = cur + s;
                if (!open[n.x, n.y] || seen[n.x, n.y]) continue;
                int d = doorLevel[n.x, n.y];
                if (d > 0 && d >= lockedFrom) continue;
                seen[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }
        return seen;
    }

    // ------------------------------------------------------------
    // BUILD THE WORLD (floor, walls, ceiling, exit marker)
    // ------------------------------------------------------------
    void BuildWorld()
    {
        worldRoot = new GameObject("MazeWorld").transform;
        worldRoot.SetParent(transform, false);

        Material wallMat = Gfx.Textured(Gfx.WallTexture(), Vector2.one);
        Material floorMat = Gfx.Textured(Gfx.FloorTexture(), new Vector2(size, size));
        Material ceilMat = Gfx.Textured(Gfx.CeilingTexture(), new Vector2(size, size));

        float mid = (size - 1) / 2f * tileSize;

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(worldRoot, false);
        floor.transform.position = new Vector3(mid, -0.5f, mid);
        floor.transform.localScale = new Vector3(size * tileSize, 1f, size * tileSize);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (open[x, y]) continue;
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall";
                wall.transform.SetParent(worldRoot, false);
                wall.transform.position = TileToWorld(new Vector2Int(x, y)) + Vector3.up * wallHeight / 2f;
                wall.transform.localScale = new Vector3(tileSize, wallHeight, tileSize);
                wall.GetComponent<Renderer>().sharedMaterial = wallMat;
            }
        }

        // ceiling (NOT under worldRoot, so it is not part of the NavMesh bake)
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.SetParent(transform, false);
        ceiling.transform.position = new Vector3(mid, wallHeight + 0.5f, mid);
        ceiling.transform.localScale = new Vector3(size * tileSize, 1f, size * tileSize);
        ceiling.GetComponent<Renderer>().sharedMaterial = ceilMat;
        Destroy(ceiling.GetComponent<Collider>());

        PlayerStart = TileToWorld(startTile) + Vector3.up * 0.1f;
        ExitPosition = TileToWorld(exitTile);

        // glowing yellow pillar + light marks the exit
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillar.name = "ExitMarker";
        pillar.transform.SetParent(transform, false);
        pillar.transform.position = ExitPosition + Vector3.up * (wallHeight * 0.45f);
        pillar.transform.localScale = new Vector3(0.8f, wallHeight * 0.9f, 0.8f);
        pillar.GetComponent<Renderer>().sharedMaterial = Gfx.Mat(Color.yellow, 1.5f);
        Destroy(pillar.GetComponent<Collider>());
        AddPointLight(pillar.transform, new Vector3(0f, 0f, 0f), Color.yellow, 12f, 3f);
    }

    void BakeNavMesh()
    {
        NavMeshSurface surface = worldRoot.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;   // only floor + walls
        surface.BuildNavMesh();
    }

    // ------------------------------------------------------------
    // Doors, keys, lamps
    // ------------------------------------------------------------
    void BuildDoorObjects()
    {
        Transform doorRoot = new GameObject("Doors").transform;
        doorRoot.SetParent(transform, false);

        for (int n = 1; n <= 3; n++)
        {
            Vector2Int t = doorTiles[n];
            bool alongX = open[t.x - 1, t.y] && open[t.x + 1, t.y];
            bool alongY = open[t.x, t.y - 1] && open[t.x, t.y + 1];

            Vector3 scale = new Vector3(tileSize * 0.98f, wallHeight, tileSize * 0.98f);
            if (alongX && !alongY) scale = new Vector3(0.5f, wallHeight, tileSize);
            else if (alongY && !alongX) scale = new Vector3(tileSize, wallHeight, 0.5f);

            GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cube);
            d.name = Door.NameOf(n) + " Door";
            d.transform.SetParent(doorRoot, false);
            d.transform.position = TileToWorld(t) + Vector3.up * wallHeight / 2f;
            d.transform.localScale = scale;
            d.GetComponent<Renderer>().sharedMaterial = Gfx.Mat(Door.ColorOf(n), 0.6f);
            d.AddComponent<Door>().level = n;
            AddPointLight(d.transform, new Vector3(0f, 0f, 0f), Door.ColorOf(n), 7f, 1.5f);
        }
    }

    void SpawnKeys()
    {
        for (int n = 1; n <= 3; n++)
        {
            if (keyTiles[n].x < 0) continue;

            GameObject key = GameObject.CreatePrimitive(PrimitiveType.Cube);
            key.name = Door.NameOf(n) + " Key Card";
            key.transform.SetParent(transform, false);
            key.transform.position = TileToWorld(keyTiles[n]) + Vector3.up * 1f;
            key.transform.localScale = new Vector3(0.5f, 0.8f, 0.08f);
            key.GetComponent<Renderer>().sharedMaterial = Gfx.Mat(Door.ColorOf(n), 1.5f);
            Destroy(key.GetComponent<Collider>());
            key.AddComponent<KeyCard>().level = n;
            AddPointLight(key.transform, Vector3.zero, Door.ColorOf(n), 5f, 2f);
        }
    }

    // Ceiling lamps: a lamp in every other cell, a real light in every fourth
    void BuildLights()
    {
        Transform lightRoot = new GameObject("Lamps").transform;
        lightRoot.SetParent(transform, false);
        Material lampMat = Gfx.Mat(new Color(1f, 0.95f, 0.8f), 2f);

        for (int x = 1; x < size - 1; x += 2)
        {
            for (int y = 1; y < size - 1; y += 2)
            {
                if (!open[x, y] || doorLevel[x, y] > 0) continue;
                int cx = x / 2, cy = y / 2;
                if ((cx + cy) % 2 != 0) continue;

                GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lamp.name = "Lamp";
                lamp.transform.SetParent(lightRoot, false);
                lamp.transform.position = TileToWorld(new Vector2Int(x, y)) + Vector3.up * (wallHeight - 0.05f);
                lamp.transform.localScale = new Vector3(1f, 0.08f, 1f);
                lamp.GetComponent<Renderer>().sharedMaterial = lampMat;
                Destroy(lamp.GetComponent<Collider>());

                if ((cx + cy) % 4 == 0)
                {
                    Light l = AddPointLight(lamp.transform, new Vector3(0f, -4f, 0f), new Color(1f, 0.93f, 0.8f), tileSize * 3.5f, 1.6f);
                    if (Random.value < 0.2f) l.gameObject.AddComponent<FlickerLight>();
                }
            }
        }
    }

    // ------------------------------------------------------------
    Light AddPointLight(Transform parent, Vector3 localPos, Color color, float range, float intensity)
    {
        GameObject g = new GameObject("Light");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return l;
    }

    Vector3 TileToWorld(Vector2Int t)
    {
        return new Vector3(t.x * tileSize, 0f, t.y * tileSize);
    }
}
