using UnityEngine;

// All sounds are generated in code (no audio files needed).
// Ambient hum, footsteps, key / door sounds, heartbeat when chased, dog bark,
// robot hum, and the caught / win sounds.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    AudioClip ambient, key, doorOpen, locked, caught, win, bark, heartbeat, hum;
    AudioClip[] steps;
    AudioSource sfx, stepSrc, heartSrc;
    float heartTarget;
    const int rate = 44100;

    void Awake()
    {
        Instance = this;
        BuildClips();

        AddSource(ambient, true, 0.35f);
        heartSrc = AddSource(heartbeat, true, 0f);
        heartSrc.pitch = 1.2f;
        sfx = gameObject.AddComponent<AudioSource>();
        stepSrc = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        heartSrc.volume = Mathf.MoveTowards(heartSrc.volume, heartTarget, 2f * Time.unscaledDeltaTime);
    }

    AudioSource AddSource(AudioClip clip, bool loop, float volume)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = loop;
        s.volume = volume;
        s.spatialBlend = 0f;
        s.playOnAwake = false;
        s.Play();
        return s;
    }

    // ------------------------------------------------------------
    // Things other scripts can call
    // ------------------------------------------------------------
    public void PlayerStep(float loudness)           // crouch = quiet, run = loud
    {
        float basePitch = loudness > 8f ? 1.1f : (loudness > 2f ? 0.95f : 0.8f);
        stepSrc.pitch = basePitch + Random.Range(-0.05f, 0.05f);
        stepSrc.PlayOneShot(RandomStep(), Mathf.Clamp(loudness / 10f, 0.1f, 1f));
    }

    public void Key()      { Play(key, 0.7f); }
    public void DoorOpen() { Play(doorOpen, 0.8f); }
    public void Locked()   { Play(locked, 0.6f); }
    public void Caught()   { Play(caught, 0.9f); }
    public void Win()      { Play(win, 0.8f); }

    public void BarkAt(Vector3 position)
    {
        AudioSource.PlayClipAtPoint(bark, position, 1f);
    }

    public void SetDanger(bool danger)
    {
        heartTarget = danger ? 0.8f : 0f;
    }

    // A 3D sound source (you hear it from the right direction and distance)
    public AudioSource Make3D(GameObject go)
    {
        AudioSource s = go.AddComponent<AudioSource>();
        s.spatialBlend = 1f;
        s.minDistance = 2f;
        s.maxDistance = 25f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.playOnAwake = false;
        return s;
    }

    public void StepAt(AudioSource source, float volume)
    {
        source.pitch = Random.Range(0.85f, 1.05f);
        source.PlayOneShot(RandomStep(), volume);
    }

    public void AttachHum(GameObject go)              // the robot hums so you can hear it coming
    {
        AudioSource s = Make3D(go);
        s.clip = hum;
        s.loop = true;
        s.volume = 0.5f;
        s.Play();
    }

    void Play(AudioClip clip, float volume)
    {
        sfx.pitch = 1f;
        sfx.PlayOneShot(clip, volume);
    }

    // ------------------------------------------------------------
    // Sound synthesis
    // ------------------------------------------------------------
    AudioClip Make(string clipName, float seconds, System.Func<float, float> sample)
    {
        int n = Mathf.CeilToInt(seconds * rate);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
            data[i] = Mathf.Clamp(sample(i / (float)rate), -1f, 1f);
        AudioClip clip = AudioClip.Create(clipName, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip RandomStep()
    {
        return steps[Random.Range(0, steps.Length)];
    }

    // One footstep = heel thump + scuff + sharp heel click + toe tap
    AudioClip MakeStep(string clipName, float thumpHz, float toeDelay, float brightness)
    {
        float twoPi = 2f * Mathf.PI;
        float phase = 0f, low = 0f, low2 = 0f;

        return Make(clipName, 0.28f, t =>
        {
            // heel: low thump whose pitch drops quickly
            float f = Mathf.Lerp(thumpHz * 1.6f, thumpHz, Mathf.Clamp01(t / 0.05f));
            phase += twoPi * f / rate;
            float heel = Mathf.Sin(phase) * Mathf.Exp(-t * 24f) * 0.9f;

            // scuff: noise softened with a low-pass filter (brightness = how dull / sharp)
            float n = Noise();
            low += (n - low) * brightness;
            float scuff = low * Mathf.Exp(-t * 30f) * 1.6f;

            // heel click: very short sharp tick (noise minus its low-pass = high-pass)
            low2 += (n - low2) * 0.5f;
            float click = (n - low2) * (t < 0.015f ? Mathf.Exp(-t * 250f) : 0f) * 0.7f;

            // toe tap a moment after the heel
            float tt = t - toeDelay;
            float toe = tt > 0f ? low * Mathf.Exp(-tt * 45f) : 0f;

            return (heel + scuff + click + toe) * 0.8f;
        });
    }

    static float Noise() { return Random.value * 2f - 1f; }
    static float Thump(float x) { return x < 0f ? 0f : Mathf.Sin(2f * Mathf.PI * 55f * x) * Mathf.Exp(-x * 20f); }

    void BuildClips()
    {
        float twoPi = 2f * Mathf.PI;

        // dark ambient drone (8 seconds, loops)
        float low = 0f;
        ambient = Make("ambient", 8f, t =>
        {
            low += (Noise() - low) * 0.02f;
            float lfo = 1f + 0.3f * Mathf.Sin(twoPi * 0.25f * t);
            return (0.25f * Mathf.Sin(twoPi * 55f * t) + 0.18f * Mathf.Sin(twoPi * 82f * t)) * lfo + low * 0.6f;
        });

        // footsteps: four slightly different shoe-on-floor sounds.
        // Want real recordings? Put .wav files in Assets/Resources/Footsteps and they are used instead.
        AudioClip[] custom = Resources.LoadAll<AudioClip>("Footsteps");
        if (custom != null && custom.Length > 0) steps = custom;
        else steps = new AudioClip[]
        {
            MakeStep("step1", 85f, 0.10f, 0.30f),
            MakeStep("step2", 95f, 0.11f, 0.35f),
            MakeStep("step3", 75f, 0.09f, 0.25f),
            MakeStep("step4", 90f, 0.12f, 0.40f)
        };

        // key pickup: two quick chime notes
        key = Make("key", 0.6f, t =>
        {
            float local = t < 0.15f ? t : t - 0.15f;
            float f = t < 0.15f ? 880f : 1320f;
            return Mathf.Sin(twoPi * f * t) * Mathf.Exp(-local * 6f) * 0.5f;
        });

        // door: heavy sliding sweep
        float doorPhase = 0f, doorLow = 0f;
        doorOpen = Make("door", 0.9f, t =>
        {
            doorPhase += twoPi * Mathf.Lerp(220f, 70f, t / 0.9f) / rate;
            doorLow += (Noise() - doorLow) * 0.1f;
            float env = Mathf.Sin(Mathf.PI * t / 0.9f);
            return (Mathf.Sin(doorPhase) * 0.5f + doorLow * 0.8f) * env * 0.7f;
        });

        // locked: two buzzes
        locked = Make("locked", 0.4f, t =>
        {
            bool on = t < 0.15f || (t > 0.22f && t < 0.37f);
            return on ? Mathf.Sign(Mathf.Sin(twoPi * 110f * t)) * 0.25f : 0f;
        });

        // caught: falling harsh tone
        float caughtPhase = 0f;
        caught = Make("caught", 1f, t =>
        {
            caughtPhase += twoPi * Mathf.Lerp(300f, 60f, t) / rate;
            float saw = Mathf.Repeat(caughtPhase / twoPi, 1f) * 2f - 1f;
            return saw * Mathf.Exp(-t * 2.5f) * 0.6f;
        });

        // win: rising arpeggio
        float[] notes = { 523f, 659f, 784f, 1047f };
        win = Make("win", 1.6f, t =>
        {
            float sum = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float local = t - i * 0.25f;
                if (local < 0f) continue;
                sum += Mathf.Sin(twoPi * notes[i] * local) * Mathf.Exp(-local * 3f);
            }
            return sum * 0.3f;
        });

        // dog bark: short falling tone + noise
        float barkPhase = 0f;
        bark = Make("bark", 0.3f, t =>
        {
            barkPhase += twoPi * Mathf.Lerp(420f, 180f, t / 0.3f) / rate;
            float env = t < 0.02f ? t / 0.02f : Mathf.Exp(-(t - 0.02f) * 9f);
            float tone = Mathf.Sin(barkPhase) + 0.5f * Mathf.Sin(2f * barkPhase);
            return (tone * 0.6f + Noise() * 0.4f) * env * 0.7f;
        });

        // heartbeat: two thumps (1 second, loops)
        heartbeat = Make("heartbeat", 1f, t => (Thump(t) + 0.7f * Thump(t - 0.28f)) * 0.9f);

        // robot hum (2 seconds, loops)
        hum = Make("hum", 2f, t =>
            0.2f * Mathf.Sin(twoPi * 140f * t) + 0.2f * Mathf.Sin(twoPi * 143f * t) + 0.1f * Mathf.Sin(twoPi * 280f * t));
    }
}
