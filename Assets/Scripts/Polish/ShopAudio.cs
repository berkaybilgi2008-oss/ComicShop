using System;
using UnityEngine;

public enum ShopCue { Pickup, Place, Release, Bonk, Reject, Click, Complete, Step, Impact }

// Original synthesized paper/wood foley and short cues: no external audio dependency.
// Clips are generated once, voices are pooled, and network cues are emitted by the host only.
public sealed class ShopAudio : MonoBehaviour
{
    static ShopAudio instance;
    readonly AudioSource[] voices = new AudioSource[16];
    readonly float[] lastPlayed = new float[9];
    readonly int[] lastVariant = new int[9];
    readonly AudioClip[,] clips = new AudioClip[9, 4];
    AudioSource roomTone;
    AudioClip ambienceClip;
    System.Random random = new System.Random(9137);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;
    void Awake()
    {
        if (instance != null) { Destroy(this); return; }
        instance = this;
        for (int i = 0; i < voices.Length; i++)
        {
            var go = new GameObject("SFX Voice " + i); go.transform.SetParent(transform, false);
            voices[i] = go.AddComponent<AudioSource>(); voices[i].playOnAwake = false;
            voices[i].dopplerLevel = 0; voices[i].minDistance = 1.5f; voices[i].maxDistance = 18;
            voices[i].rolloffMode = AudioRolloffMode.Linear;
        }
        for (int cue = 0; cue < 9; cue++)
        {
            lastPlayed[cue] = -10;
            for (int variant = 0; variant < 4; variant++) clips[cue, variant] = CreateCue((ShopCue)cue, variant);
        }
        roomTone = gameObject.AddComponent<AudioSource>(); roomTone.playOnAwake = false; roomTone.loop = true;
        ambienceClip = CreateRoomTone(); roomTone.clip = ambienceClip; roomTone.volume = 0; roomTone.Play();
    }
    void Update()
    {
        var settings = ShopSettings.Current;
        foreach (var voice in voices) if (voice != null) voice.volume = settings.master * settings.effects;
        if (roomTone != null) roomTone.volume = ShopRound.State.Active ? settings.master * settings.ambience : 0;
    }
    public static void Play(ShopCue cue, Vector3 position, bool spatial = true)
    {
        if (instance == null || (int)cue < 0 || (int)cue >= 9) return;
        instance.Emit(cue, position, spatial);
    }
    void Emit(ShopCue cue, Vector3 position, bool spatial)
    {
        int index = (int)cue;
        if (Time.unscaledTime - lastPlayed[index] < (cue == ShopCue.Impact ? .1f : .035f)) return;
        lastPlayed[index] = Time.unscaledTime;
        AudioSource source = null;
        foreach (var voice in voices) if (!voice.isPlaying) { source = voice; break; }
        if (source == null)
        {
            if (cue != ShopCue.Complete && cue != ShopCue.Click && cue != ShopCue.Reject) return;
            source = voices[0]; source.Stop();
        }
        int variant = (lastVariant[index] + 1 + random.Next(3)) % 4; lastVariant[index] = variant;
        source.transform.position = position; source.spatialBlend = spatial ? 1 : 0;
        source.pitch = cue == ShopCue.Complete ? 1 : .97f + (float)random.NextDouble() * .06f;
        source.volume = ShopSettings.Current.master * ShopSettings.Current.effects;
        source.clip = clips[index, variant]; source.Play();
    }
    AudioClip CreateCue(ShopCue cue, int variant)
    {
        const int rate = 24000;
        float duration = cue == ShopCue.Complete ? 1.35f : cue == ShopCue.Release ? .24f : .16f;
        var samples = new float[Mathf.CeilToInt(rate * duration)];
        var rng = new System.Random(713 + (int)cue * 47 + variant);
        float low = 0, prior = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2 - 1;
            low += .055f * (noise - low);
            float attack = Mathf.Min(1, t / .003f), end = Mathf.Min(1, (duration - t) / .025f);
            float decay = Mathf.Exp(-t * 25), value;
            float frequency = 120 + variant * 11;
            switch (cue)
            {
                case ShopCue.Pickup: value = (noise - prior) * .12f * Mathf.Exp(-t * 20) + low * .6f * decay; break;
                case ShopCue.Release: value = noise * .12f * Mathf.Sin(Mathf.PI * t / duration); break;
                case ShopCue.Place: value = Mathf.Sin(t * 2 * Mathf.PI * frequency) * .3f * decay + noise * .06f * Mathf.Exp(-t * 50); break;
                case ShopCue.Bonk: value = Mathf.Sin(2 * Mathf.PI * (180 * t - 220 * t * t)) * .4f * Mathf.Exp(-t * 17); break;
                case ShopCue.Reject: value = Mathf.Sin(2 * Mathf.PI * 220 * t) * .13f * Mathf.Exp(-t * 18); break;
                case ShopCue.Click: value = Mathf.Sin(2 * Mathf.PI * 900 * t) * .1f * Mathf.Exp(-t * 65); break;
                case ShopCue.Step: value = low * .8f * decay + Mathf.Sin(2 * Mathf.PI * 85 * t) * .13f * Mathf.Exp(-t * 40); break;
                case ShopCue.Impact: value = low * .8f * decay + noise * .07f * Mathf.Exp(-t * 45); break;
                default:
                    value = 0;
                    for (int note = 0; note < 3; note++)
                    {
                        float time = t - note * .12f;
                        if (time < 0) continue;
                        float hz = note == 0 ? 523.25f : note == 1 ? 659.25f : 783.99f;
                        value += Mathf.Sin(2 * Mathf.PI * hz * time) * .12f * Mathf.Exp(-time * 4) * Mathf.Min(1, time / .005f);
                    }
                    break;
            }
            samples[i] = Mathf.Clamp(value * attack * end, -.8f, .8f); prior = noise;
        }
        var clip = AudioClip.Create("ComicShop " + cue + " " + variant, samples.Length, 1, rate, false);
        clip.SetData(samples, 0); return clip;
    }
    AudioClip CreateRoomTone()
    {
        const int rate = 12000, length = rate * 12;
        var data = new float[length]; float low = 0;
        for (int i = 0; i < length; i++)
        {
            low += .006f * ((float)random.NextDouble() * 2 - 1 - low);
            float fade = Mathf.Min(1, i / 1200f, (length - 1 - i) / 1200f);
            data[i] = low * .2f * fade;
        }
        var clip = AudioClip.Create("ComicShop quiet ventilation", length, 1, rate, false);
        clip.SetData(data, 0); return clip;
    }
    void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        foreach (var clip in clips) if (clip != null) Destroy(clip);
        if (ambienceClip != null) Destroy(ambienceClip);
    }
}
