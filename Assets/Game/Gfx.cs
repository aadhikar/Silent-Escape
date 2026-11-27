using System.Collections.Generic;
using UnityEngine;

// Small helper that makes coloured / glowing / textured materials in code.
// Works with the Built-in and URP pipelines (it copies the default material).
public static class Gfx
{
    static Material template;
    static Dictionary<string, Material> cache = new Dictionary<string, Material>();

    static Material Template()
    {
        if (template == null)
        {
            GameObject t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template = t.GetComponent<Renderer>().sharedMaterial;
            Object.Destroy(t);
        }
        return template;
    }

    // Plain colour. glow > 0 makes it light up (lamps, eyes, keys)
    public static Material Mat(Color color, float glow = 0f)
    {
        string key = ColorUtility.ToHtmlStringRGBA(color) + glow;
        Material m;
        if (cache.TryGetValue(key, out m) && m != null) return m;

        m = new Material(Template());
        m.color = color;
        if (glow > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * glow);
        }
        cache[key] = m;
        return m;
    }

    // Material with a picture on it. tiling = how many times the picture repeats
    public static Material Textured(Texture2D tex, Vector2 tiling)
    {
        Material m = new Material(Template());
        m.color = Color.white;
        m.mainTexture = tex;
        m.mainTextureScale = tiling;
        return m;
    }

    // Draws a texture pixel by pixel using a small function
    public static Texture2D MakeTexture(int size, System.Func<int, int, Color> pixel)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = pixel(x, y);
        t.SetPixels(px);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        t.anisoLevel = 4;
        t.Apply(true);
        return t;
    }

    static Color Shade(Color c, float f)
    {
        return new Color(c.r * f, c.g * f, c.b * f, 1f);
    }

    // Grey lab wall: dark base, yellow hazard stripe, panel seams
    public static Texture2D WallTexture()
    {
        return MakeTexture(128, (x, y) =>
        {
            float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.14f;
            Color c = Shade(new Color(0.62f, 0.65f, 0.7f), 1f - n);
            if (y < 16) c = Shade(new Color(0.22f, 0.24f, 0.28f), 1f - n);
            else if (y >= 18 && y < 26)
                c = ((x / 16) % 2 == 0) ? new Color(0.9f, 0.75f, 0.1f) : new Color(0.1f, 0.1f, 0.1f);
            if (x < 2 || x > 125) c = Shade(c, 0.6f);
            if (y > 125) c = Shade(c, 0.7f);
            return c;
        });
    }

    // Dark tiled floor
    public static Texture2D FloorTexture()
    {
        return MakeTexture(128, (x, y) =>
        {
            float n = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.2f;
            Color c = Shade(new Color(0.24f, 0.26f, 0.28f), 1f - n);
            if (((x / 64) + (y / 64)) % 2 == 0) c = Shade(c, 1.15f);
            if (x < 3 || y < 3) c = Shade(c, 0.45f);
            return c;
        });
    }

    // Ceiling panels
    public static Texture2D CeilingTexture()
    {
        return MakeTexture(64, (x, y) =>
        {
            float n = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.1f;
            Color c = Shade(new Color(0.32f, 0.34f, 0.36f), 1f - n);
            if (x < 2 || y < 2) c = Shade(c, 0.5f);
            return c;
        });
    }

    // Dark grey roll-up shutter: horizontal slats
    public static Texture2D ShutterTexture()
    {
        return MakeTexture(64, (x, y) =>
        {
            float n = Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.08f;
            Color c = Shade(new Color(0.30f, 0.32f, 0.35f), 1f - n);
            int slat = y % 16;                              // 4 slats per picture
            if (slat < 2) c = Shade(c, 0.45f);              // dark gap between slats
            else if (slat < 4) c = Shade(c, 1.2f);          // light edge on top of each slat
            if (x < 2 || x > 61) c = Shade(c, 0.6f);
            return c;
        });
    }
}
