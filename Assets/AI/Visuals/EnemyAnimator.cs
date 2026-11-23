using UnityEngine;

// Moves the body parts of an enemy: legs and arms swing when walking,
// the body leans forward when chasing, the robot hovers, the dog wags its tail.
public class EnemyAnimator : MonoBehaviour
{
    public Transform model;           // the whole body
    public Transform[] legs;          // even index swings one way, odd index the other way
    public Transform[] arms;
    public Transform tail;
    public float swingAngle = 35f;
    public float leanChase = 10f;
    public float leanDash = 25f;
    public bool hover;                // robot floats above the floor
    public float baseHeight;

    EnemyBase enemy;
    Vector3 lastPos;
    float phase;
    float speed;
    AudioSource stepSource;
    int lastStep;

    void Start()
    {
        enemy = GetComponent<EnemyBase>();
        lastPos = transform.position;
        if (AudioManager.Instance != null) stepSource = AudioManager.Instance.Make3D(gameObject);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // how fast are we really moving? (smoothed so it doesn't jitter)
        float rawSpeed = Vector3.Distance(transform.position, lastPos) / dt;
        lastPos = transform.position;
        speed = Mathf.Lerp(speed, rawSpeed, 10f * dt);

        float moving = Mathf.Clamp01(speed / 2.5f);
        phase += speed * dt * 2.2f;

        // footstep sound each time a foot lands (you can hear enemies coming)
        int stepIndex = Mathf.FloorToInt(phase / Mathf.PI);
        if (stepIndex != lastStep)
        {
            lastStep = stepIndex;
            if (moving > 0.3f && !hover && stepSource != null)
                AudioManager.Instance.StepAt(stepSource, 0.4f + 0.4f * moving);
        }
        float swing = Mathf.Sin(phase) * swingAngle * moving;

        for (int i = 0; i < legs.Length; i++)
            legs[i].localRotation = Quaternion.Euler(i % 2 == 0 ? swing : -swing, 0f, 0f);

        for (int i = 0; i < arms.Length; i++)
            arms[i].localRotation = Quaternion.Euler(i % 2 == 0 ? -swing * 0.8f : swing * 0.8f, 0f, 0f);

        if (tail != null)
            tail.localRotation = Quaternion.Euler(40f, Mathf.Sin(Time.time * 10f) * 25f, 0f);

        // lean forward while chasing or dashing
        float lean = 0f;
        if (enemy != null)
        {
            if (enemy.state == AIState.Dash) lean = leanDash;
            else if (enemy.state == AIState.Chase || enemy.state == AIState.Track) lean = leanChase;
        }
        model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(lean, 0f, 0f), 8f * dt);

        // little bounce when walking, floating when hovering
        float y = baseHeight;
        if (hover) y += 0.1f + Mathf.Sin(Time.time * 3f) * 0.06f;
        else y += Mathf.Abs(Mathf.Sin(phase)) * 0.05f * moving;
        model.localPosition = new Vector3(0f, y, 0f);
    }
}
