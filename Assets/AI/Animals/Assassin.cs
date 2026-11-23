using UnityEngine;

// Excellent eyes and ears, very fast, short dash. Notices you quickly.
public class Assassin : EnemyBase
{
    protected override EnemyConfig CreateDefaultConfig()
    {
        EnemyConfig c = ScriptableObject.CreateInstance<EnemyConfig>();
        c.viewRange = 28f;          // excellent vision
        c.viewAngle = 130f;
        c.hearingMultiplier = 2.5f; // excellent hearing
        c.walkSpeed = 3.2f;
        c.chaseSpeed = 7.2f;        // very fast
        c.dashSpeed = 11f;          // short dash (dog's is longer)
        c.dashWindup = 0.2f;
        c.dashDuration = 0.5f;
        c.dashCooldown = 5f;
        c.dashTriggerRange = 7f;
        c.awarenessSpeed = 2.5f;    // spots you very fast
        return c;
    }

    protected override void Awake()
    {
        baseColor = new Color(0.4f, 0.1f, 0.5f); // dark purple
        base.Awake();
    }
}
