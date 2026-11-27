using UnityEngine;
using UnityEngine.AI;

// A dark grey SHUTTER set into a maze wall.
//  - Closed: it looks like part of the wall (and blocks sight and enemies).
//  - Open:   it is a shortcut between two corridors.
// It rolls up into the ceiling and down again by itself. It never closes while someone
// stands in the doorway. Lamps on the door: GREEN = open, ORANGE blinking = closing soon, RED = closed.
public class RandomDoor : MonoBehaviour
{
    public float closedTime = 6f;     // roughly how long it stays closed
    public float openTime = 12f;      // roughly how long it stays open
    public Renderer[] lamps;
    public Material redMat, greenMat, orangeMat;

    NavMeshObstacle obstacle;
    Collider col;
    AudioSource sound;
    float closedY, openY;
    bool wantClosed;
    float timer;

    void Start()
    {
        col = GetComponent<Collider>();

        obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.zero;
        obstacle.size = Vector3.one;
        obstacle.carving = true;

        if (AudioManager.Instance != null) sound = AudioManager.Instance.Make3D(gameObject);

        closedY = transform.position.y;
        openY = closedY + 3.3f;                 // rolls up into the ceiling
        wantClosed = Random.value < 0.3f;
        timer = Random.Range(2f, 8f);

        Vector3 p = transform.position;
        p.y = wantClosed ? closedY : openY;
        transform.position = p;
        obstacle.enabled = wantClosed;
        col.enabled = wantClosed;
        ShowLamps(wantClosed ? redMat : greenMat);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        timer -= dt;

        if (timer <= 0f)
        {
            if (wantClosed)
            {
                wantClosed = false;
                timer = Random.Range(openTime * 0.6f, openTime * 1.4f);
                obstacle.enabled = false;
                ShowLamps(greenMat);
                MakeSound();
            }
            else if (IsClear())
            {
                wantClosed = true;
                timer = Random.Range(closedTime * 0.6f, closedTime * 1.4f);
                obstacle.enabled = true;
                ShowLamps(redMat);
                MakeSound();
            }
            else
            {
                timer = 1f;   // someone is in the doorway, try again soon
            }
        }

        // warning: lamps blink orange for the last 2 seconds before it closes
        if (!wantClosed && timer > 0f && timer < 2f)
            ShowLamps(Mathf.FloorToInt(Time.time * 5f) % 2 == 0 ? orangeMat : greenMat);

        Vector3 p = transform.position;
        p.y = Mathf.MoveTowards(p.y, wantClosed ? closedY : openY, 4f * dt);
        transform.position = p;

        col.enabled = p.y < closedY + 2f;     // solid until it is rolled up high enough to walk under
    }

    void ShowLamps(Material m)
    {
        if (lamps == null || m == null) return;
        foreach (Renderer r in lamps)
            if (r != null) r.sharedMaterial = m;
    }

    void MakeSound()
    {
        if (sound != null && AudioManager.Instance != null) AudioManager.Instance.DoorSound(sound);
        NoiseSystem.Emit(transform.position, 4f);    // guards nearby may come and look
    }

    bool IsClear()
    {
        PlayerController player = GameManager.Instance.player;
        if (player != null && FlatDistance(player.transform.position) < 2.2f) return false;

        foreach (EnemyBase e in GameManager.Instance.Enemies)
            if (e != null && FlatDistance(e.transform.position) < 2.2f) return false;

        return true;
    }

    float FlatDistance(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }
}
