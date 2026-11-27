using UnityEngine;

public enum VoiceKind { Guard, Dog, Robot }

// Makes an enemy talk when its state changes, so the player is warned:
//   Guard: "Hello?"  ->  "Hey... stop! stop!"
//   Dog:   sniffing, growling while it follows your scent, barking when it chases
//   Robot: "SCANNING"  ->  "INTRUDER DETECTED"  ->  "ENGAGE"
public class EnemyVoice : MonoBehaviour
{
    public VoiceKind kind;

    EnemyBase enemy;
    AudioSource source;
    AIState last = AIState.Patrol;
    float nextAllowed;
    float nextChaseCall;

    void Start()
    {
        enemy = GetComponent<EnemyBase>();
        if (AudioManager.Instance != null)
        {
            source = AudioManager.Instance.Make3D(gameObject);
            source.maxDistance = 35f;       // voices carry further than footsteps
        }
    }

    void Update()
    {
        if (enemy == null || source == null) return;

        if (enemy.state != last)
        {
            AIState previous = last;
            last = enemy.state;
            OnStateChanged(previous, last);
        }

        // keep shouting / barking while chasing
        if (enemy.state == AIState.Chase && Time.time > nextChaseCall)
        {
            nextChaseCall = Time.time + ChaseRepeatTime();
            Say(ChaseRepeatLine());
        }
    }

    void OnStateChanged(AIState previous, AIState now)
    {
        if (now == AIState.Chase && previous == AIState.Dash) return;   // don't repeat after a dash

        bool urgent = now == AIState.Chase || now == AIState.Dash;
        if (!urgent && Time.time < nextAllowed) return;

        string key = null;
        switch (kind)
        {
            case VoiceKind.Guard:
                if (now == AIState.Suspicious) key = "guard_suspicious";
                else if (now == AIState.Chase) key = "guard_chase";
                else if (now == AIState.Search) key = "guard_search";
                break;
            case VoiceKind.Dog:
                if (now == AIState.Suspicious || now == AIState.Search) key = "dog_sniff";
                else if (now == AIState.Track) key = "dog_growl";
                else if (now == AIState.Chase) key = "dog_bark2";
                else if (now == AIState.Dash) key = "dog_bark";
                break;
            case VoiceKind.Robot:
                if (now == AIState.Suspicious) key = "robot_scan";
                else if (now == AIState.Chase) key = "robot_chase";
                else if (now == AIState.Dash) key = "robot_dash";
                else if (now == AIState.Search) key = "robot_search";
                break;
        }
        if (key == null) return;

        nextAllowed = Time.time + 1.5f;
        if (now == AIState.Chase) nextChaseCall = Time.time + ChaseRepeatTime();
        Say(key);
    }

    float ChaseRepeatTime()
    {
        if (kind == VoiceKind.Dog) return 1.4f;
        if (kind == VoiceKind.Guard) return 4.5f;
        return 7f;
    }

    string ChaseRepeatLine()
    {
        if (kind == VoiceKind.Dog) return "dog_bark";
        if (kind == VoiceKind.Guard) return "guard_chase2";
        return "robot_chase2";
    }

    void Say(string key)
    {
        AudioManager.Instance.Say(source, key, 1f);
    }
}
