using UnityEngine;

// Draws all on-screen text in a "computer terminal" look:
// monospace font, capital letters, green colour, dark shadow.
// To use your own font: put a .ttf file named HudFont in Assets/Resources.
public static class Hud
{
    public static readonly Color Green = new Color(0.45f, 1f, 0.65f);

    static Font font;
    static GUIStyle style;

    public static void Label(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
    {
        if (font == null)
        {
            font = Resources.Load<Font>("HudFont");   // your own font wins if it exists
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont(
                    new string[] { "Consolas", "Lucida Console", "Courier New", "Menlo", "Courier" }, 18);
        }
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.wordWrap = false;
        }

        style.font = font;
        style.fontSize = size;
        style.alignment = anchor;
        text = text.ToUpper();

        style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);                       // shadow
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
        style.normal.textColor = color;
        GUI.Label(r, text, style);
    }
}
