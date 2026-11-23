using UnityEngine;

// Human-like guard: good eyes, ok ears, no nose, normal speed.
public class Guard : EnemyBase
{
    protected override EnemyConfig CreateDefaultConfig()
    {
        EnemyConfig c = ScriptableObject.CreateInstance<EnemyConfig>();
        c.viewRange = 16f;
        c.viewAngle = 90f;
        c.hearingMultiplier = 1f;
        c.walkSpeed = 2f;
        c.chaseSpeed = 5f;
        c.awarenessSpeed = 1f;
        return c;
    }

    protected override void Awake()
    {
        baseColor = new Color(0.2f, 0.4f, 1f); // blue
        base.Awake();
    }
}
