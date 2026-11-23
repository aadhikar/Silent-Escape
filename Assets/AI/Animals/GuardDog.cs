using UnityEngine;

// Fast, great nose and ears, short eyes, long dash.
public class GuardDog : EnemyBase
{
    ScentTrail scent;
    float trackTime;          // time-stamp of the crumb we are following
    float lastTrackedTime = -1f;
    Vector3 crumbPos;
    float lastBarkTime = -100f;

    protected override EnemyConfig CreateDefaultConfig()
    {
        EnemyConfig c = ScriptableObject.CreateInstance<EnemyConfig>();
        c.viewRange = 8f;          // short vision
        c.viewAngle = 140f;
        c.hearingMultiplier = 2.5f; // excellent hearing
        c.smellRange = 12f;         // excellent smell
        c.walkSpeed = 3f;
        c.chaseSpeed = 8f;          // fast
        c.dashSpeed = 16f;          // long dash
        c.dashWindup = 0.3f;
        c.dashDuration = 0.6f;
        c.dashCooldown = 4f;
        c.dashTriggerRange = 9f;
        c.awarenessSpeed = 1.3f;
        return c;
    }

    protected override void Awake()
    {
        baseColor = new Color(0.6f, 0.4f, 0.2f); // brown
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        if (playerCtrl != null) scent = playerCtrl.GetComponent<ScentTrail>();
    }

    // Every frame: do we smell a fresh trail nearby?
    protected override void CheckExtraSenses()
    {
        if (scent == null) return;
        bool canSwitch = state == AIState.Patrol || state == AIState.Suspicious ||
                         state == AIState.Investigate || state == AIState.Search;
        if (!canSwitch) return;

        ScentTrail.Crumb crumb;
        if (scent.FindNearest(transform.position, config.smellRange, lastTrackedTime, out crumb))
        {
            trackTime = crumb.time;
            lastTrackedTime = crumb.time;
            crumbPos = crumb.position;
            SetState(AIState.Track);
            agent.SetDestination(crumbPos);
        }
    }

    // Follow the crumbs in the order the player dropped them
    protected override void OnTrack()
    {
        if (stateTimer > 20f) { StartSearch(crumbPos); return; }

        bool reached = !agent.pathPending && agent.remainingDistance < 1.2f;
        if (!reached) return;

        if (NextCrumb()) agent.SetDestination(crumbPos);
        else StartSearch(crumbPos); // trail ended, look around here
    }

    // Move to the next crumb that is not too close (so the dog doesn't stutter)
    bool NextCrumb()
    {
        bool found = false;
        ScentTrail.Crumb next;
        while (scent.FindNext(trackTime, out next))
        {
            trackTime = next.time;
            crumbPos = next.position;
            found = true;
            if (Vector3.Distance(transform.position, crumbPos) > 3f) break;
        }
        lastTrackedTime = trackTime;
        return found;
    }

    // Bark when the chase starts: guards nearby will hear it and come
    protected override void OnEnterState(AIState newState)
    {
        if (newState == AIState.Chase && Time.time - lastBarkTime > 6f)
        {
            lastBarkTime = Time.time;
            NoiseSystem.Emit(transform.position, 14f);
            if (AudioManager.Instance != null) AudioManager.Instance.BarkAt(transform.position);
        }
    }
}
