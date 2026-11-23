using System;
using UnityEngine;

// A simple "noise board". Anyone can shout a noise, every enemy listens.
public static class NoiseSystem
{
    public static event Action<Vector3, float> OnNoise;

    public static void Emit(Vector3 position, float loudness)
    {
        if (OnNoise != null) OnNoise(position, loudness);
    }
}
