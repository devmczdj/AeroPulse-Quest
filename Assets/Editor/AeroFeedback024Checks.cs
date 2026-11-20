using System;
using UnityEngine;

// Verifies the persistent confirmation-sound preference without changing the user's final value.
public static class AeroFeedback024Checks
{
    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Feedback024: " + message);
    }

    public static void Run()
    {
        Require(Application.isPlaying, "Play Mode required");
        var owner = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
        Require(owner != null && owner.Feedback != null, "Interaction feedback is missing");
        bool original = owner.FeedbackSoundEnabled;
        bool hadPreference = PlayerPrefs.HasKey(AeroInteractionFeedback.SoundPreferenceKey);
        int stored = PlayerPrefs.GetInt(AeroInteractionFeedback.SoundPreferenceKey, 1);
        try
        {
            Require(hadPreference || original, "Confirmation sound should default on");
            owner.SetFeedbackSound(false);
            Require(!owner.FeedbackSoundEnabled && PlayerPrefs.GetInt(AeroInteractionFeedback.SoundPreferenceKey, -1) == 0,
                "Sound-off preference was not persisted");
            owner.SetFeedbackSound(true);
            Require(owner.FeedbackSoundEnabled && PlayerPrefs.GetInt(AeroInteractionFeedback.SoundPreferenceKey, -1) == 1,
                "Sound-on preference was not persisted");
            Require(owner.Feedback.AudioVolumeForChecks >= .15f, "Confirmation sound volume is too low");
            Debug.Log("[AeroPulse Feedback024] PASS persistent sound toggle and audible default volume.");
        }
        finally
        {
            owner.SetFeedbackSound(original);
            if (hadPreference) PlayerPrefs.SetInt(AeroInteractionFeedback.SoundPreferenceKey, stored);
            else PlayerPrefs.DeleteKey(AeroInteractionFeedback.SoundPreferenceKey);
            PlayerPrefs.Save();
        }
    }
}
