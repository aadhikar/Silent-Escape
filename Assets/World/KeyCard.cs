using System.Collections.Generic;
using UnityEngine;

// A floating key card. Walk close to pick it up.
public class KeyCard : MonoBehaviour
{
    public int level = 1;
    public static List<KeyCard> All = new List<KeyCard>();
    float startY;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        startY = transform.position.y;
    }

    void Update()
    {
        // spin and bob so it is easy to spot
        transform.Rotate(0f, 90f * Time.deltaTime, 0f);
        Vector3 p = transform.position;
        p.y = startY + Mathf.Sin(Time.time * 2f) * 0.15f;
        transform.position = p;

        PlayerController player = GameManager.Instance.player;
        if (player == null) return;

        Vector3 a = transform.position;
        Vector3 b = player.transform.position;
        a.y = 0f;
        b.y = 0f;
        if (Vector3.Distance(a, b) < 1.5f)
        {
            player.AddKey(level);
            if (AudioManager.Instance != null) AudioManager.Instance.Key();
            GameManager.Instance.ShowHint("Picked up the " + Door.NameOf(level) + " key card");
            Destroy(gameObject);
        }
    }
}
