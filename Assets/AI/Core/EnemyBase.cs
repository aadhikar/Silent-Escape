using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum AIState { Patrol, Suspicious, Investigate, Chase, Search, Track, Dash }

// Brain shared by Guard, Dog and Assassin.
// Each enemy only changes its config numbers (and the dog adds scent tracking).
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBase : MonoBehaviour
{
    public EnemyConfig config;
    public List<Vector3> patrolPoints = new List<Vector3>();
    public Color baseColor = Color.gray;
    public AIState state = AIState.Patrol;

    protected NavMeshAgent agent;
    protected Transform player;
    protected PlayerController playerCtrl;
    public Renderer[] indicators;     // glowing eyes that change colour with the state
    public Light indicatorLight;      // flashlight / chest light, changes colour too
    Color lightBaseColor = Color.white;

    protected float awareness;        // 0 = unaware, 1 = fully spotted you
    protected bool seesPlayer;
    protected Vector3 interestPos;    // where something interesting happened
    protected Vector3 lastKnownPos;   // last place we saw the player
    protected Vector3 searchCenter;
    protected float stateTimer;       // time spent in the current state

    int patrolIndex;
    bool waiting;
    float waitTimer;
    float loseTimer;
    float lookTimer;
    float lastDashTime = -100f;
    Vector3 dashDir;

    // Child classes override this to give their own numbers
    protected virtual EnemyConfig CreateDefaultConfig()
    {
        return ScriptableObject.CreateInstance<EnemyConfig>();
    }

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (config == null) config = CreateDefaultConfig();
    }

    protected virtual void Start()
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning(name + " is not on the NavMesh, disabling it.");
            enabled = false;
            return;
        }
        playerCtrl = GameManager.Instance.player;
        player = playerCtrl.transform;
        NoiseSystem.OnNoise += HearNoise;
        if (indicatorLight != null) lightBaseColor = indicatorLight.color;

        agent.acceleration = 14f;
        agent.angularSpeed = 300f;
        if (patrolPoints.Count > 0) patrolIndex = Random.Range(0, patrolPoints.Count);
        SetState(AIState.Patrol);
    }

    void OnDestroy()
    {
        NoiseSystem.OnNoise -= HearNoise;
    }

    // ------------------------------------------------------------
    // MAIN LOOP
    // ------------------------------------------------------------
    protected virtual void Update()
    {
        if (GameManager.Instance.gameOver)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            return;
        }

        stateTimer += Time.deltaTime;
        seesPlayer = CanSeePlayer();
        UpdateAwareness();

        if (seesPlayer)
        {
            lastKnownPos = player.position;
            interestPos = player.position;
        }

        CheckExtraSenses(); // dog uses this for smell

        // Fully noticed the player? Start chasing.
        if (awareness >= 1f && state != AIState.Chase && state != AIState.Dash)
            SetState(AIState.Chase);

        switch (state)
        {
            case AIState.Patrol:      DoPatrol();      break;
            case AIState.Suspicious:  DoSuspicious();  break;
            case AIState.Investigate: DoInvestigate(); break;
            case AIState.Chase:       DoChase();       break;
            case AIState.Search:      DoSearch();      break;
            case AIState.Track:       OnTrack();       break;
            case AIState.Dash:        DoDash();        break;
        }

        // Caught the player?
        bool closeEnough = FlatDistance(player.position) < config.catchDistance;
        if (closeEnough && (seesPlayer || state == AIState.Chase || state == AIState.Dash))
            GameManager.Instance.Lose();
    }

    // ------------------------------------------------------------
    // STATE CHANGES
    // ------------------------------------------------------------
    protected void SetState(AIState newState)
    {
        state = newState;
        stateTimer = 0f;
        agent.isStopped = false;

        switch (newState)
        {
            case AIState.Patrol:
                agent.speed = config.walkSpeed;
                waiting = false;
                if (patrolPoints.Count > 0) agent.SetDestination(patrolPoints[patrolIndex]);
                break;
            case AIState.Suspicious:
                agent.isStopped = true;
                break;
            case AIState.Investigate:
                agent.speed = config.walkSpeed * 1.4f;
                agent.SetDestination(interestPos);
                break;
            case AIState.Chase:
                agent.speed = config.chaseSpeed;
                awareness = 1f;
                loseTimer = 0f;
                break;
            case AIState.Search:
                agent.speed = config.walkSpeed * 1.2f;
                awareness = 0.5f;
                lookTimer = 0f;
                agent.SetDestination(searchCenter);
                break;
            case AIState.Track:
                agent.speed = config.chaseSpeed;
                break;
            case AIState.Dash:
                agent.isStopped = true;
                break;
        }

        ShowState(newState);
        OnEnterState(newState);
    }

    // Child classes can react to state changes (the dog barks, for example)
    protected virtual void OnEnterState(AIState newState) { }

    protected void StartSearch(Vector3 center)
    {
        searchCenter = center;
        SetState(AIState.Search);
    }

    // ------------------------------------------------------------
    // SENSES
    // ------------------------------------------------------------
    protected bool CanSeePlayer()
    {
        bool crouching = playerCtrl.IsCrouching;
        Vector3 eye = transform.position + Vector3.up * 1.5f;
        Vector3 target = player.position + Vector3.up * (crouching ? 0.5f : 1.2f);
        Vector3 dir = target - eye;

        // 1) distance (crouching makes you harder to see)
        float range = config.viewRange * (crouching ? 0.6f : 1f);
        if (dir.magnitude > range) return false;

        // 2) angle
        Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
        if (Vector3.Angle(transform.forward, flatDir) > config.viewAngle * 0.5f) return false;

        // 3) is there a wall in the way?
        RaycastHit hit;
        if (Physics.Raycast(eye, dir.normalized, out hit, range))
            return hit.transform == player;
        return false;
    }

    void UpdateAwareness()
    {
        if (seesPlayer)
        {
            float closeness = 1f - Mathf.Clamp01(FlatDistance(player.position) / config.viewRange);
            awareness += Time.deltaTime * config.awarenessSpeed * (0.5f + closeness * 2f);
        }
        else
        {
            awareness -= Time.deltaTime * 0.15f;
        }
        awareness = Mathf.Clamp01(awareness);
    }

    protected virtual void HearNoise(Vector3 pos, float loudness)
    {
        if (state == AIState.Dash || state == AIState.Track) return;
        if (Vector3.Distance(pos, transform.position) < 0.5f) return; // ignore our own noise

        float hearDistance = loudness * config.hearingMultiplier;

        // walls muffle sound
        RaycastHit hit;
        Vector3 eye = transform.position + Vector3.up;
        if (Physics.Linecast(eye, pos + Vector3.up, out hit) && hit.transform != player)
            hearDistance *= 0.5f;

        if (Vector3.Distance(transform.position, pos) > hearDistance) return;

        interestPos = pos;
        if (state == AIState.Chase) { lastKnownPos = pos; return; }

        awareness = Mathf.Max(awareness, 0.4f);
        SetState(AIState.Suspicious);
    }

    // The dog overrides this to add smell
    protected virtual void CheckExtraSenses() { }

    // ------------------------------------------------------------
    // STATE BEHAVIOURS
    // ------------------------------------------------------------
    void DoPatrol()
    {
        if (awareness > 0.25f) { SetState(AIState.Suspicious); return; }
        if (patrolPoints.Count == 0) return;

        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                patrolIndex = Random.Range(0, patrolPoints.Count);
                agent.SetDestination(patrolPoints[patrolIndex]);
            }
        }
        else if (!agent.pathPending && agent.remainingDistance <= 0.6f)
        {
            waiting = true;
            waitTimer = Random.Range(1f, 3f);
        }
    }

    void DoSuspicious()
    {
        // stop, turn toward what we noticed, wait a moment
        FaceTowards(interestPos, 240f);
        if (stateTimer > 1.5f) SetState(AIState.Investigate);
    }

    void DoInvestigate()
    {
        bool arrived = !agent.pathPending && agent.remainingDistance < 1f;
        if (arrived || stateTimer > 15f) StartSearch(interestPos);
    }

    void DoChase()
    {
        if (seesPlayer)
        {
            loseTimer = 0f;
            awareness = 1f;
            agent.SetDestination(player.position);
            TryStartDash();
        }
        else
        {
            loseTimer += Time.deltaTime;
            agent.SetDestination(lastKnownPos);
            bool reached = !agent.pathPending && agent.remainingDistance < 1f && loseTimer > 0.5f;
            if (loseTimer > 3f || reached) StartSearch(lastKnownPos);
        }
    }

    void DoSearch()
    {
        // give up after 8 seconds and go back to patrol
        if (stateTimer > 8f)
        {
            awareness = 0f;
            SetState(AIState.Patrol);
            return;
        }

        bool arrived = !agent.pathPending && agent.remainingDistance < 0.8f;
        if (arrived)
        {
            // look around, then walk to another spot nearby
            lookTimer += Time.deltaTime;
            transform.Rotate(0f, 120f * Time.deltaTime, 0f);
            if (lookTimer > 1.5f)
            {
                lookTimer = 0f;
                agent.SetDestination(RandomPointNear(searchCenter, 8f));
            }
        }
    }

    // Dog overrides this. Default: just search.
    protected virtual void OnTrack()
    {
        StartSearch(transform.position);
    }

    // ------------------------------------------------------------
    // DASH (used by dog and assassin)
    // ------------------------------------------------------------
    void TryStartDash()
    {
        if (config.dashSpeed <= 0f) return;
        if (Time.time - lastDashTime < config.dashCooldown) return;

        float d = FlatDistance(player.position);
        if (d < 2f || d > config.dashTriggerRange) return;

        SetState(AIState.Dash);
        dashDir = FlatDirection(player.position);
    }

    void DoDash()
    {
        // part 1: wind-up, face the player and aim
        if (stateTimer < config.dashWindup)
        {
            FaceTowards(player.position, 700f);
            dashDir = FlatDirection(player.position);
            return;
        }

        // part 2: run in a straight line (direction is locked)
        if (stateTimer < config.dashWindup + config.dashDuration)
        {
            if (dashDir.sqrMagnitude > 0.01f)
            {
                agent.Move(dashDir * config.dashSpeed * Time.deltaTime);
                transform.rotation = Quaternion.LookRotation(dashDir);
            }
            return;
        }

        // part 3: dash over, go back to chasing
        lastDashTime = Time.time;
        SetState(AIState.Chase);
    }

    // ------------------------------------------------------------
    // SMALL HELPERS
    // ------------------------------------------------------------
    protected float FlatDistance(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    protected Vector3 FlatDirection(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.normalized;
    }

    protected void FaceTowards(Vector3 p, float degreesPerSecond)
    {
        Vector3 d = FlatDirection(p);
        if (d.sqrMagnitude < 0.01f) return;
        Quaternion look = Quaternion.LookRotation(d);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, degreesPerSecond * Time.deltaTime);
    }

    protected Vector3 RandomPointNear(Vector3 center, float radius)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 p = center + Random.insideUnitSphere * radius;
            p.y = center.y;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(p, out hit, 2f, NavMesh.AllAreas)) return hit.position;
        }
        return center;
    }

    // Eyes and light show what the enemy is doing
    void ShowState(AIState s)
    {
        Color c = ColorFor(s);
        if (indicators != null)
        {
            foreach (Renderer r in indicators)
            {
                if (r == null) continue;
                r.material.color = c;
                r.material.SetColor("_EmissionColor", c * 1.5f);
            }
        }
        if (indicatorLight != null)
            indicatorLight.color = (s == AIState.Patrol) ? lightBaseColor : c;
    }

    // Eye colour for each state (great for testing)
    Color ColorFor(AIState s)
    {
        switch (s)
        {
            case AIState.Suspicious:  return Color.yellow;
            case AIState.Investigate: return new Color(1f, 0.5f, 0f);
            case AIState.Chase:       return Color.red;
            case AIState.Search:      return new Color(1f, 0.6f, 0.8f);
            case AIState.Track:       return Color.cyan;
            case AIState.Dash:        return Color.white;
            default:                  return new Color(0.3f, 1f, 0.4f);
        }
    }

    // Draws the vision cone in the Scene view
    void OnDrawGizmos()
    {
        if (config == null) return;
        Gizmos.color = Color.yellow;
        Vector3 p = transform.position + Vector3.up * 1.5f;
        Vector3 left = Quaternion.Euler(0f, -config.viewAngle / 2f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, config.viewAngle / 2f, 0f) * transform.forward;
        Gizmos.DrawRay(p, left * config.viewRange);
        Gizmos.DrawRay(p, right * config.viewRange);
    }
}
