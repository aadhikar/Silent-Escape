using System.Collections.Generic;
using UnityEngine;

// Put on the player. Drops invisible "scent crumbs" as you walk.
// Crumbs fade away after a while. The dog follows them.
public class ScentTrail : MonoBehaviour
{
    public struct Crumb
    {
        public Vector3 position;
        public float time;
    }

    public float dropDistance = 0.4f;   // drop a crumb every time we moved this far
    public float lifeTime = 25f;        // seconds before a crumb disappears

    public List<Crumb> crumbs = new List<Crumb>();
    Vector3 lastDropPos;

    void Start()
    {
        lastDropPos = transform.position;
    }

    void Update()
    {
        if (Vector3.Distance(transform.position, lastDropPos) > dropDistance)
        {
            Crumb c = new Crumb();
            c.position = transform.position;
            c.time = Time.time;
            crumbs.Add(c);
            lastDropPos = transform.position;
        }

        // remove old crumbs (the oldest are at the start of the list)
        while (crumbs.Count > 0 && Time.time - crumbs[0].time > lifeTime)
            crumbs.RemoveAt(0);
    }

    // Closest crumb to a position, only crumbs newer than minTime
    public bool FindNearest(Vector3 from, float range, float minTime, out Crumb result)
    {
        result = default(Crumb);
        bool found = false;
        float best = range;
        foreach (Crumb c in crumbs)
        {
            if (c.time <= minTime) continue;
            float d = Vector3.Distance(from, c.position);
            if (d < best) { best = d; result = c; found = true; }
        }
        return found;
    }

    // The next crumb dropped after a given time (the way the player walked)
    public bool FindNext(float afterTime, out Crumb result)
    {
        foreach (Crumb c in crumbs)
        {
            if (c.time > afterTime) { result = c; return true; }
        }
        result = default(Crumb);
        return false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        foreach (Crumb c in crumbs)
            Gizmos.DrawSphere(c.position + Vector3.up * 0.1f, 0.1f);
    }
}
