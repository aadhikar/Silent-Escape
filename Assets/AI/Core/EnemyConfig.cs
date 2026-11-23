using UnityEngine;

// All the numbers for one enemy type live here.
// Right-click in Project window > Create > Stealth Escape > Enemy Config to make your own.
// (If you don't assign one, each enemy builds its own default config in code.)
[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Stealth Escape/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    [Header("Vision")]
    public float viewRange = 15f;      // how far it can see
    public float viewAngle = 90f;      // total angle of the vision cone

    [Header("Hearing and Smell")]
    public float hearingMultiplier = 1f;  // noise loudness x this = how far it hears
    public float smellRange = 0f;         // 0 = cannot smell

    [Header("Speed")]
    public float walkSpeed = 2f;
    public float chaseSpeed = 5f;

    [Header("Dash (dashSpeed 0 = no dash)")]
    public float dashSpeed = 0f;
    public float dashWindup = 0.3f;       // pause before the dash
    public float dashDuration = 0.6f;     // how long the dash lasts
    public float dashCooldown = 4f;
    public float dashTriggerRange = 8f;   // starts dash when player is closer than this

    [Header("Other")]
    public float awarenessSpeed = 1f;     // how fast it notices you (higher = faster)
    public float catchDistance = 1.3f;    // closer than this = you are caught
}
