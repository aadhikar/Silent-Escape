using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// A locked door. Opens by itself when you walk up with the right key card.
// While it is closed it also blocks enemies (the NavMeshObstacle).
public class Door : MonoBehaviour
{
    public int level = 1;           // 1 = Red, 2 = Blue, 3 = Green
    public float openRange = 3f;
    public bool isOpen;
    public static List<Door> All = new List<Door>();
    float lockedSoundTime;

    NavMeshObstacle obstacle;
    Collider col;
    Vector3 openPos;

    public static string NameOf(int level)
    {
        if (level == 1) return "Red";
        if (level == 2) return "Blue";
        return "Green";
    }

    public static Color ColorOf(int level)
    {
        if (level == 1) return new Color(0.9f, 0.1f, 0.1f);
        if (level == 2) return new Color(0.1f, 0.3f, 0.95f);
        return new Color(0.1f, 0.8f, 0.2f);
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Awake()
    {
        col = GetComponent<Collider>();

        obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.zero;
        obstacle.size = Vector3.one;
        obstacle.carving = true;      // cuts a hole in the NavMesh while closed

        openPos = transform.position + Vector3.down * 4f;   // door sinks into the floor
    }

    void Update()
    {
        if (isOpen)
        {
            transform.position = Vector3.MoveTowards(transform.position, openPos, 3f * Time.deltaTime);
            return;
        }

        PlayerController player = GameManager.Instance.player;
        if (player == null) return;

        Vector3 a = transform.position;
        Vector3 b = player.transform.position;
        a.y = 0f;
        b.y = 0f;
        if (Vector3.Distance(a, b) > openRange) return;

        if (player.HasKey(level))
        {
            isOpen = true;
            obstacle.enabled = false;
            col.enabled = false;
            GameManager.Instance.ShowHint(NameOf(level) + " door opened!");
            if (AudioManager.Instance != null) AudioManager.Instance.DoorOpen();
        }
        else
        {
            GameManager.Instance.ShowHint("Locked! You need the " + NameOf(level) + " key card.");
            if (Time.time > lockedSoundTime && AudioManager.Instance != null)
            {
                lockedSoundTime = Time.time + 1.5f;
                AudioManager.Instance.Locked();
            }
        }
    }
}
