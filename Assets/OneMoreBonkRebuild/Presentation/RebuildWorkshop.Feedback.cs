using System;
using UnityEngine;

namespace OneMoreBonk.Rebuild.Presentation
{
    public sealed partial class RebuildWorkshop
    {
        const string SoundPreference = "omb.rebuild.feedback.v1.sound";
        const string HapticsPreference = "omb.rebuild.feedback.v1.haptics";
        const int FeedbackSampleRate = 22050;
        enum StrikeCue { Hold, Success, Great, Damage, Protected, Broken }
        AudioSource feedbackSource;
        AudioClip[] feedbackClips;
        bool feedbackInitialized, feedbackSound = true, feedbackHaptics = true, feedbackVibrating;
#if UNITY_ANDROID && !UNITY_EDITOR
        float feedbackNextHaptic;
#endif

        public bool SoundEnabled
        {
            get => feedbackSound;
            set
            {
                feedbackSound = value;
                SaveFeedbackPreference(SoundPreference, value);
                if (!value && feedbackSource != null) feedbackSource.Stop();
            }
        }
        public bool HapticsEnabled
        {
            get => feedbackHaptics;
            set
            {
                feedbackHaptics = value;
                SaveFeedbackPreference(HapticsPreference, value);
                if (!value) CancelFeedbackHaptics();
            }
        }
        public void ToggleSound() => SoundEnabled = !SoundEnabled;
        public void ToggleHaptics() => HapticsEnabled = !HapticsEnabled;

        void InitializeFeedback()
        {
            if (feedbackInitialized) return;
            feedbackInitialized = true;
            try
            {
                feedbackSound = PlayerPrefs.GetInt(SoundPreference, 1) != 0;
                feedbackHaptics = PlayerPrefs.GetInt(HapticsPreference, 1) != 0;
            }
            catch { /* A preference failure must not block the workshop. */ }
            try
            {
                feedbackSource = gameObject.AddComponent<AudioSource>();
                feedbackSource.playOnAwake = false;
                feedbackSource.loop = false;
                feedbackSource.spatialBlend = 0;
                feedbackSource.dopplerLevel = 0;
                feedbackSource.volume = .32f;
                feedbackSource.ignoreListenerPause = false;
                feedbackClips = new AudioClip[6];
                foreach (StrikeCue cue in Enum.GetValues(typeof(StrikeCue)))
                {
                    float[] samples = SynthesizeStrikeCue(cue);
                    var clip = AudioClip.Create("Workshop " + cue, samples.Length, 1, FeedbackSampleRate, false);
                    feedbackClips[(int)cue] = clip;
                    if (!clip.SetData(samples, 0)) throw new InvalidOperationException("Audio sample upload failed.");
                }
            }
            catch { ReleaseFeedbackAudio(); }
        }

        // Called only after Session.TryStrike committed. This layer never writes gameplay state.
        void PlayStrikeFeedback(RiskStrikeResult result)
        {
            if (result == null || !result.Accepted || FeedbackSuspended) return;
            if (!feedbackInitialized) InitializeFeedback();
            StrikeCue cue = StrikeFeedbackCue(result);
            if (feedbackSound && feedbackSource != null && feedbackClips != null)
            {
                try
                {
                    // One dedicated voice bounds rapid-hammer volume and stops stale result tails.
                    feedbackSource.Stop();
                    feedbackSource.clip = feedbackClips[(int)cue];
                    feedbackSource.Play();
                }
                catch { /* Audio availability never changes an accepted strike. */ }
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (feedbackHaptics && Time.unscaledTime >= feedbackNextHaptic &&
                (cue == StrikeCue.Protected || cue == StrikeCue.Broken))
            {
                try
                {
                    Handheld.Vibrate();
                    feedbackVibrating = true;
                    feedbackNextHaptic = Time.unscaledTime + 1.5f;
                }
                catch { /* Devices without a vibrator continue silently. */ }
            }
#endif
        }

        bool FeedbackSuspended => !focused || appPaused || adVisible || !isActiveAndEnabled || view?.PendingReward != null;

        // Existing focus/pause/ad hooks can call this immediately; LateUpdate is a fallback.
        public void SuspendFeedback()
        {
            if (feedbackSource != null) feedbackSource.Stop();
            CancelFeedbackHaptics();
        }
        void LateUpdate()
        {
            if (FeedbackSuspended) SuspendFeedback();
        }
        void CancelFeedbackHaptics()
        {
            if (!feedbackVibrating) return;
            feedbackVibrating = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                    vibrator?.Call("cancel");
            }
            catch { /* Suspension remains safe when the Activity is being torn down. */ }
#endif
        }
        void DisposeFeedback()
        {
            CancelFeedbackHaptics();
            ReleaseFeedbackAudio();
            feedbackInitialized = false;
        }
        void ReleaseFeedbackAudio()
        {
            if (feedbackSource != null)
            {
                feedbackSource.Stop();
                feedbackSource.clip = null;
                Destroy(feedbackSource);
                feedbackSource = null;
            }
            if (feedbackClips != null)
                foreach (var clip in feedbackClips) if (clip != null) Destroy(clip);
            feedbackClips = null;
        }
        static void SaveFeedbackPreference(string key, bool enabled)
        {
            try { PlayerPrefs.SetInt(key, enabled ? 1 : 0); PlayerPrefs.Save(); }
            catch { /* Settings stay effective in memory if local preference storage is unavailable. */ }
        }
        static StrikeCue StrikeFeedbackCue(RiskStrikeResult result)
        {
            if (result.Fatal) return StrikeCue.Broken;
            if (result.RolledOutcome == ForgeOutcome.Break && !string.IsNullOrEmpty(result.ProtectionId))
                return StrikeCue.Protected;
            switch (result.Outcome)
            {
                case ForgeOutcome.Great: return StrikeCue.Great;
                case ForgeOutcome.Success: return StrikeCue.Success;
                case ForgeOutcome.Damage: return StrikeCue.Damage;
                case ForgeOutcome.Break: return StrikeCue.Broken;
                default: return StrikeCue.Hold;
            }
        }

        // Fixed mathematical waves and sample hashes; no Unity or gameplay random stream is used.
        static float[] SynthesizeStrikeCue(StrikeCue cue)
        {
            double duration = cue == StrikeCue.Hold ? .09 : cue == StrikeCue.Success ? .18 :
                cue == StrikeCue.Damage ? .21 : cue == StrikeCue.Protected ? .29 : .34;
            var samples = new float[(int)(FeedbackSampleRate * duration)];
            double peak = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / FeedbackSampleRate;
                double impact = .28 * Ring(t, 170, 55) + .16 * FeedbackNoise(i, 1) * Math.Exp(-90 * t);
                double wave;
                switch (cue)
                {
                    case StrikeCue.Success:
                        wave = impact + .36 * Ring(t, 1120, 22) + .18 * Ring(t, 1710, 28); break;
                    case StrikeCue.Great:
                        wave = impact + .26 * Ring(t, 980, 14) + .30 * Ring(t - .055, 1470, 17) + .32 * Ring(t - .11, 1960, 19); break;
                    case StrikeCue.Damage:
                        wave = impact + .30 * Math.Sin(2 * Math.PI * (310 * t - 360 * t * t)) * Math.Exp(-20 * t) +
                            .25 * FeedbackNoise(i, 2) * Math.Exp(-35 * t); break;
                    case StrikeCue.Protected:
                        wave = impact + .32 * Ring(t, 820, 10) + .22 * Ring(t, 1230, 14) +
                            .20 * Math.Sin(2 * Math.PI * (900 * t + 1800 * t * t)) * Math.Exp(-15 * t); break;
                    case StrikeCue.Broken:
                        wave = impact + .48 * FeedbackNoise(i, 3) * Math.Exp(-17 * t) + .25 * Ring(t, 95, 12) +
                            .16 * Ring(t - .055, 2040, 50) + .14 * Ring(t - .13, 1370, 45); break;
                    default:
                        wave = impact + .23 * Ring(t, 360, 55); break;
                }
                double edge = Math.Min(1, t / .002) * Math.Min(1, (duration - t) / .012);
                samples[i] = (float)(wave * edge);
                peak = Math.Max(peak, Math.Abs(samples[i]));
            }
            if (peak > .8)
                for (int i = 0; i < samples.Length; i++) samples[i] *= (float)(.8 / peak);
            samples[0] = samples[samples.Length - 1] = 0;
            return samples;
        }
        static double Ring(double t, double frequency, double decay) =>
            t < 0 ? 0 : Math.Sin(2 * Math.PI * frequency * t) * Math.Exp(-decay * t);
        static double FeedbackNoise(int sample, uint salt)
        {
            uint value = unchecked((uint)sample * 747796405u + salt * 2891336453u);
            value = unchecked((value ^ (value >> 16)) * 2246822519u);
            return ((value >> 8) & 65535) / 32767.5 - 1;
        }
    }
}
