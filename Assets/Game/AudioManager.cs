using System.Collections.Generic;
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
        BuildVoices();

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
    // ------------------------------------------------------------
    // Voices (guard, dog, robot)
    // Want real recordings? Put .wav files named like the keys below in Assets/Resources/Voices
    // (guard_chase, guard_chase2, guard_suspicious, guard_search, dog_bark, dog_bark2, dog_growl,
    //  dog_sniff, robot_scan, robot_chase, robot_chase2, robot_dash, robot_search). They replace the generated ones.
    // ------------------------------------------------------------
    Dictionary<string, AudioClip> voices = new Dictionary<string, AudioClip>();

    public void Say(AudioSource source, string key, float volume = 1f)
    {
        AudioClip clip;
        if (source == null || !voices.TryGetValue(key, out clip)) return;
        source.pitch = 1f;
        source.PlayOneShot(clip, volume);
    }

    public void DoorSound(AudioSource source)
    {
        source.pitch = Random.Range(0.9f, 1.1f);
        source.PlayOneShot(doorOpen, 0.9f);
    }

    static string[] Ph(params string[] s) { return s; }

    void AddVoice(string key, float[] data)
    {
        AudioClip custom = Resources.Load<AudioClip>("Voices/" + key);
        if (custom != null) { voices[key] = custom; return; }
        AudioClip clip = AudioClip.Create(key, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        voices[key] = clip;
    }

    void BuildVoices()
    {
        // ---- GUARD: "Hello?" / "Hey... stop! stop!" / "Hmm..." ----
        float[] hey = VoiceSynth.Normalize(VoiceSynth.Word(Ph("H", "EY"), 150f, 120f), 0.8f);
        float[] stop1 = VoiceSynth.Normalize(VoiceSynth.Word(Ph("S", "T", "AA", "P"), 135f, 110f), 0.85f);
        float[] stop2 = VoiceSynth.Normalize(VoiceSynth.Word(Ph("S", "T", "AA", "P"), 150f, 120f), 1f);
        AddVoice("guard_chase", VoiceSynth.Join(hey, VoiceSynth.Silence(0.35f), stop1, VoiceSynth.Silence(0.15f), stop2));
        AddVoice("guard_chase2", VoiceSynth.Join(stop1, VoiceSynth.Silence(0.12f), stop2));
        AddVoice("guard_suspicious", VoiceSynth.Normalize(VoiceSynth.Word(Ph("H", "EH", "L", "OW"), 115f, 150f), 0.8f));
        AddVoice("guard_search", VoiceSynth.Normalize(VoiceSynth.Word(Ph("MM"), 110f, 95f, 1f, 0.05f, 0.03f), 0.6f));

        // ---- DOG: bark, double bark, growl, sniffing ----
        float[] bark = VoiceSynth.Normalize(VoiceSynth.Word(Ph("BARK", "H"), 380f, 220f, 1f, 0.35f, 0f), 1f);
        AddVoice("dog_bark", bark);
        AddVoice("dog_bark2", VoiceSynth.Join(bark, VoiceSynth.Silence(0.1f), bark));
        AddVoice("dog_growl", VoiceSynth.Growl(VoiceSynth.Word(Ph("GROWL"), 70f, 58f, 1f, 0.5f, 0.04f)));
        AddVoice("dog_sniff", VoiceSynth.Normalize(VoiceSynth.Word(Ph("SN", "_", "SN", "_", "SN"), 100f, 100f), 0.5f));

        // ---- ROBOT: flat robotic voice ----
        float[] intruder = VoiceSynth.Word(Ph("IH", "N", "T", "R", "UW", "D", "ER"), 95f, 95f, 0.95f, 0.05f, 0f);
        float[] detected = VoiceSynth.Word(Ph("D", "IH", "T", "EH", "K", "T", "IH", "D"), 95f, 95f, 0.95f, 0.05f, 0f);
        float[] target = VoiceSynth.Word(Ph("T", "AA", "R", "G", "IH", "T"), 95f, 95f, 0.95f, 0.05f, 0f);
        float[] located = VoiceSynth.Word(Ph("L", "OW", "K", "EY", "T", "IH", "D"), 95f, 95f, 0.95f, 0.05f, 0f);
        AddVoice("robot_scan", VoiceSynth.Robotize(VoiceSynth.Word(Ph("S", "K", "AE", "N", "IH", "NG"), 95f, 95f, 0.95f, 0.05f, 0f)));
        AddVoice("robot_chase", VoiceSynth.Robotize(VoiceSynth.Join(intruder, VoiceSynth.Silence(0.18f), detected)));
        AddVoice("robot_chase2", VoiceSynth.Robotize(VoiceSynth.Join(target, VoiceSynth.Silence(0.18f), located)));
        AddVoice("robot_dash", VoiceSynth.Robotize(VoiceSynth.Word(Ph("EH", "N", "G", "EY", "T", "SH"), 95f, 95f, 0.95f, 0.05f, 0f)));
        AddVoice("robot_search", VoiceSynth.Robotize(VoiceSynth.Word(Ph("S", "ER", "T", "SH", "IH", "NG"), 95f, 95f, 0.95f, 0.05f, 0f)));
    }

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
