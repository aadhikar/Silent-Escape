using UnityEngine;

// Builds the 3D look of each enemy out of simple shapes (no art files needed).
//   Guard    = human with cap, vest and a flashlight
//   Dog      = four legs, snout, ears, wagging tail
//   Assassin = hovering robot with blades and a glowing red eye
// Every enemy gets "indicators" (glowing eyes) that change colour with its state.
public static class EnemyModels
{
    // ---------- little helpers ----------
    static Transform Part(Transform parent, PrimitiveType shape, Vector3 pos, Vector3 scale, Color color, float glow = 0f)
    {
        GameObject g = GameObject.CreatePrimitive(shape);
        Object.Destroy(g.GetComponent<Collider>());   // enemies don't need physics colliders
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = Gfx.Mat(color, glow);
        return g.transform;
    }

    // A limb that swings from its top (shoulder / hip)
    static Transform Limb(Transform parent, Vector3 pivotPos, Vector3 size, Color color)
    {
        GameObject pivot = new GameObject("LimbPivot");
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = pivotPos;
        Part(pivot.transform, PrimitiveType.Cube, new Vector3(0f, -size.y / 2f, 0f), size, color);
        return pivot.transform;
    }

    static Transform NewModel(GameObject root)
    {
        GameObject m = new GameObject("Model");
        m.transform.SetParent(root.transform, false);
        return m.transform;
    }

    static Light MakeLight(Transform parent, Vector3 pos, LightType type, float range, float intensity, Color color, float angle = 60f)
    {
        GameObject g = new GameObject("EnemyLight");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        Light l = g.AddComponent<Light>();
        l.type = type;
        l.range = range;
        l.intensity = intensity;
        l.color = color;
        l.spotAngle = angle;
        l.shadows = LightShadows.None;
        return l;
    }

    static EnemyAnimator AddAnimator(GameObject root, Transform model, Transform[] legs, Transform[] arms,
                                     Transform tail, float swing, bool hover, float baseHeight)
    {
        EnemyAnimator a = root.AddComponent<EnemyAnimator>();
        a.model = model;
        a.legs = legs;
        a.arms = arms;
        a.tail = tail;
        a.swingAngle = swing;
        a.hover = hover;
        a.baseHeight = baseHeight;
        return a;
    }

    static readonly Color eyeColor = new Color(0.3f, 1f, 0.4f);

    // ---------- GUARD ----------
    public static void BuildGuard(GameObject root, out Renderer[] indicators, out Light light)
    {
        Transform model = NewModel(root);
        Color navy = new Color(0.12f, 0.2f, 0.45f);
        Color dark = new Color(0.1f, 0.1f, 0.14f);
        Color skin = new Color(0.9f, 0.72f, 0.56f);

        Part(model, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(0.62f, 0.75f, 0.34f), navy);        // torso
        Part(model, PrimitiveType.Cube, new Vector3(0f, 0.82f, 0f), new Vector3(0.66f, 0.1f, 0.38f), dark);        // belt
        Part(model, PrimitiveType.Cube, new Vector3(0.14f, 1.32f, 0.18f), new Vector3(0.16f, 0.2f, 0.03f), new Color(0.85f, 0.8f, 0.2f)); // badge
        Part(model, PrimitiveType.Sphere, new Vector3(0f, 1.8f, 0f), Vector3.one * 0.32f, skin);                   // head
        Part(model, PrimitiveType.Cylinder, new Vector3(0f, 1.96f, 0f), new Vector3(0.36f, 0.04f, 0.36f), navy);   // cap
        Part(model, PrimitiveType.Cube, new Vector3(0f, 1.93f, 0.2f), new Vector3(0.28f, 0.03f, 0.16f), navy);     // brim
        Transform visor = Part(model, PrimitiveType.Cube, new Vector3(0f, 1.82f, 0.15f), new Vector3(0.26f, 0.06f, 0.05f), eyeColor, 1.5f);

        Transform legL = Limb(model, new Vector3(-0.16f, 0.85f, 0f), new Vector3(0.24f, 0.85f, 0.26f), dark);
        Transform legR = Limb(model, new Vector3(0.16f, 0.85f, 0f), new Vector3(0.24f, 0.85f, 0.26f), dark);
        Transform armL = Limb(model, new Vector3(-0.42f, 1.52f, 0f), new Vector3(0.17f, 0.7f, 0.2f), navy);
        Transform armR = Limb(model, new Vector3(0.42f, 1.52f, 0f), new Vector3(0.17f, 0.7f, 0.2f), navy);
        Part(armL, PrimitiveType.Sphere, new Vector3(0f, -0.72f, 0f), Vector3.one * 0.18f, skin);                  // hands
        Part(armR, PrimitiveType.Sphere, new Vector3(0f, -0.72f, 0f), Vector3.one * 0.18f, skin);

        // flashlight shows where the guard is looking
        light = MakeLight(model, new Vector3(0f, 1.75f, 0.25f), LightType.Spot, 14f, 3f, new Color(1f, 0.95f, 0.8f), 80f);

        indicators = new Renderer[] { visor.GetComponent<Renderer>() };
        AddAnimator(root, model, new Transform[] { legL, legR }, new Transform[] { armL, armR }, null, 35f, false, 0f);
    }

    // ---------- DOG ----------
    public static void BuildDog(GameObject root, out Renderer[] indicators, out Light light)
    {
        Transform model = NewModel(root);
        Color brown = new Color(0.55f, 0.35f, 0.17f);
        Color tan = new Color(0.78f, 0.58f, 0.34f);
        Color dark = new Color(0.2f, 0.12f, 0.07f);

        Part(model, PrimitiveType.Cube, new Vector3(0f, 0.62f, 0f), new Vector3(0.42f, 0.4f, 0.95f), brown);         // body
        Part(model, PrimitiveType.Cube, new Vector3(0f, 0.45f, 0.05f), new Vector3(0.38f, 0.1f, 0.6f), tan);         // belly
        Part(model, PrimitiveType.Cube, new Vector3(0f, 0.88f, 0.58f), new Vector3(0.32f, 0.3f, 0.34f), brown);      // head
        Part(model, PrimitiveType.Cube, new Vector3(0f, 0.8f, 0.84f), new Vector3(0.17f, 0.14f, 0.22f), tan);        // snout
        Part(model, PrimitiveType.Sphere, new Vector3(0f, 0.85f, 0.96f), Vector3.one * 0.08f, Color.black);          // nose
        Part(model, PrimitiveType.Cube, new Vector3(-0.13f, 1.08f, 0.52f), new Vector3(0.08f, 0.2f, 0.1f), dark);    // ears
        Part(model, PrimitiveType.Cube, new Vector3(0.13f, 1.08f, 0.52f), new Vector3(0.08f, 0.2f, 0.1f), dark);
        Part(model, PrimitiveType.Cylinder, new Vector3(0f, 0.78f, 0.35f), new Vector3(0.36f, 0.04f, 0.36f), new Color(0.8f, 0.1f, 0.1f)); // red collar

        Transform eyeL = Part(model, PrimitiveType.Sphere, new Vector3(-0.09f, 0.93f, 0.74f), Vector3.one * 0.07f, eyeColor, 1.5f);
        Transform eyeR = Part(model, PrimitiveType.Sphere, new Vector3(0.09f, 0.93f, 0.74f), Vector3.one * 0.07f, eyeColor, 1.5f);

        // legs: front-left, back-right, front-right, back-left (diagonal pairs swing together)
        Vector3 legSize = new Vector3(0.13f, 0.45f, 0.13f);
        Transform fl = Limb(model, new Vector3(-0.15f, 0.45f, 0.35f), legSize, brown);
        Transform br = Limb(model, new Vector3(0.15f, 0.45f, -0.35f), legSize, brown);
        Transform fr = Limb(model, new Vector3(0.15f, 0.45f, 0.35f), legSize, brown);
        Transform bl = Limb(model, new Vector3(-0.15f, 0.45f, -0.35f), legSize, brown);

        // tail
        GameObject tailPivot = new GameObject("Tail");
        tailPivot.transform.SetParent(model, false);
        tailPivot.transform.localPosition = new Vector3(0f, 0.78f, -0.48f);
        Part(tailPivot.transform, PrimitiveType.Cube, new Vector3(0f, 0f, -0.2f), new Vector3(0.07f, 0.07f, 0.4f), brown);

        light = null;
        indicators = new Renderer[] { eyeL.GetComponent<Renderer>(), eyeR.GetComponent<Renderer>() };
        AddAnimator(root, model, new Transform[] { fl, br, fr, bl }, new Transform[0], tailPivot.transform, 45f, false, 0f);
    }

    // ---------- ASSASSIN (robot) ----------
    public static void BuildRobot(GameObject root, out Renderer[] indicators, out Light light)
    {
        Transform model = NewModel(root);
        Color metal = new Color(0.14f, 0.14f, 0.17f);
        Color steel = new Color(0.5f, 0.52f, 0.58f);
        Color red = new Color(1f, 0.15f, 0.1f);

        Part(model, PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.5f, 0.7f, 0.3f), metal);            // torso
        Transform core = Part(model, PrimitiveType.Sphere, new Vector3(0f, 1.22f, 0.16f), Vector3.one * 0.15f, eyeColor, 2f); // chest core
        Part(model, PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f), new Vector3(0.34f, 0.2f, 0.24f), steel);          // pelvis
        Part(model, PrimitiveType.Cube, new Vector3(0f, 1.68f, 0f), new Vector3(0.3f, 0.26f, 0.3f), metal);           // head
        Transform visor = Part(model, PrimitiveType.Cube, new Vector3(0f, 1.69f, 0.16f), new Vector3(0.26f, 0.06f, 0.04f), eyeColor, 2f);
        Part(model, PrimitiveType.Cylinder, new Vector3(0.1f, 1.9f, 0f), new Vector3(0.03f, 0.12f, 0.03f), steel);    // antenna
        Part(model, PrimitiveType.Sphere, new Vector3(-0.36f, 1.42f, 0f), Vector3.one * 0.24f, steel);                // shoulders
        Part(model, PrimitiveType.Sphere, new Vector3(0.36f, 1.42f, 0f), Vector3.one * 0.24f, steel);

        Transform armL = Limb(model, new Vector3(-0.36f, 1.42f, 0f), new Vector3(0.12f, 0.65f, 0.12f), metal);
        Transform armR = Limb(model, new Vector3(0.36f, 1.42f, 0f), new Vector3(0.12f, 0.65f, 0.12f), metal);
        Part(armL, PrimitiveType.Cube, new Vector3(0f, -0.85f, 0.1f), new Vector3(0.04f, 0.5f, 0.1f), new Color(0.85f, 0.88f, 0.95f), 0.4f); // blades
        Part(armR, PrimitiveType.Cube, new Vector3(0f, -0.85f, 0.1f), new Vector3(0.04f, 0.5f, 0.1f), new Color(0.85f, 0.88f, 0.95f), 0.4f);

        Transform legL = Limb(model, new Vector3(-0.12f, 0.72f, 0f), new Vector3(0.14f, 0.65f, 0.14f), metal);
        Transform legR = Limb(model, new Vector3(0.12f, 0.72f, 0f), new Vector3(0.14f, 0.65f, 0.14f), metal);
        Part(legL, PrimitiveType.Cube, new Vector3(0f, -0.67f, 0.05f), new Vector3(0.16f, 0.06f, 0.26f), steel);      // feet
        Part(legR, PrimitiveType.Cube, new Vector3(0f, -0.67f, 0.05f), new Vector3(0.16f, 0.06f, 0.26f), steel);

        // glowing hover pad on the floor (stays on the ground while the body bobs)
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.8f, 0.01f, 0.8f), red, 1.2f);

        light = MakeLight(model, new Vector3(0f, 1.2f, 0.3f), LightType.Point, 5f, 1.5f, red);
        indicators = new Renderer[] { visor.GetComponent<Renderer>(), core.GetComponent<Renderer>() };
        AddAnimator(root, model, new Transform[] { legL, legR }, new Transform[] { armL, armR }, null, 25f, true, 0.2f);
    }
}
