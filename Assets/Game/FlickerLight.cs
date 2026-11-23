using UnityEngine;

// Makes a light flicker now and then (creepy lab feeling)
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    Light lamp;
    float baseIntensity;
    float wait;
    float burst;

    void Start()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
        wait = Random.Range(1f, 6f);
    }

    void Update()
    {
        if (burst > 0f)
        {
            burst -= Time.deltaTime;
            lamp.intensity = Random.value < 0.5f ? baseIntensity * 0.15f : baseIntensity;
            if (burst <= 0f) lamp.intensity = baseIntensity;
        }
        else
        {
            wait -= Time.deltaTime;
            if (wait <= 0f)
            {
                wait = Random.Range(2f, 7f);
                burst = Random.Range(0.1f, 0.4f);
            }
        }
    }
}
