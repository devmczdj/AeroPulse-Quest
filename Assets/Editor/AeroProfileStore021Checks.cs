using System;
using System.IO;
using UnityEngine;

// Storage checks use an isolated temporary directory, never the application's profiles.
public static class AeroProfileStore021Checks
{
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("ProfileStore021: " + message); }

    static AeroLayoutState State()
    {
        string[] ids = { "main", "heart", "pace", "distance", "next", "timer" };
        var state = new AeroLayoutState { panels = new AeroPanelState[ids.Length] };
        for (int i = 0; i < ids.Length; i++) state.panels[i] = new AeroPanelState
        {
            id = ids[i], visible = i == 0, placed = i == 0, anchor = (int)AeroWindowAnchor.Follow,
            position = new Vector3(i * .05f, 0, .85f), physicalScale = .00075f, userScale = 1,
            followMode = (int)HudSoftFollow.FollowMode.Soft, speed = (int)HudSoftFollow.FollowMode.Soft
        };
        return state;
    }

    static void Reject(AeroLayoutProfileStore store, string file, Action<AeroLayoutState> breakState, string message)
    {
        var invalid = State(); breakState(invalid); string before = File.ReadAllText(file);
        Require(!store.TryReplace(store.Data.activeId, invalid), "Accepted " + message);
        Require(!string.IsNullOrEmpty(store.LastError) && File.ReadAllText(file) == before,
            "Invalid " + message + " changed the saved file");
    }

    public static void Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "aero-layout-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "profiles.json");
            var store = new AeroLayoutProfileStore(file);
            Require(store.Load() && store.Data.profiles.Length == 0, "Missing file should start empty");
            var state = State();
            Require(store.TryAdd(" 日常 ", state, out var first), store.LastError);
            Require(first.name == "日常" && store.Data.activeId == first.id, "First save should trim and activate");
            state.panels[0].position = Vector3.one * 20;
            Require(store.Find(first.id).layout.panels[0].position != state.panels[0].position, "Save retained a caller-owned state alias");

            state = State(); state.panels[1].visible = true; state.panels[1].placed = true;
            state.panels[1].anchor = (int)AeroWindowAnchor.Hand; state.panels[1].position = new Vector3(.02f, .03f, -.1f);
            state.panels[2].anchor = (int)AeroWindowAnchor.World; state.panels[2].pinned = true;
            state.panels[2].followMode = (int)HudSoftFollow.FollowMode.Fixed;
            state.panels[3].anchor = (int)AeroWindowAnchor.View; state.panels[3].userScale = 1.5f;
            state.panels[4].speed = (int)HudSoftFollow.FollowMode.Quick;
            Require(store.TryAdd("运动", state, out var second) && store.Data.activeId == second.id,
                "Save As New did not activate in the same transaction");
            var reload = new AeroLayoutProfileStore(file);
            Require(reload.Load() && JsonUtility.ToJson(reload.Data) == JsonUtility.ToJson(store.Data), "Disk roundtrip lost layout fields");
            var clone = AeroLayoutProfileStore.Clone(store.Data); clone.profiles[0].layout.panels[0].visible = false;
            Require(store.Find(first.id).layout.panels[0].visible, "Collection clone retained nested state aliases");
            Require(store.TryActivate(first.id), store.LastError);
            Require(new AeroLayoutProfileStore(file).Load(), "Activated file was unreadable");
            reload.Load(); Require(reload.Data.activeId == first.id, "Active ID was not persisted");

            string saved = File.ReadAllText(file);
            Require(!store.TryAdd("日常", State(), out _) && !store.TryRename(second.id, "日常"), "Duplicate names accepted");
            Require(!store.TryAdd(" ", State(), out _) && !store.TryAdd("bad\nname", State(), out _), "Blank/control names accepted");
            Require(!store.TryAdd(new string('x', 25), State(), out _), "Oversized name accepted");
            Require(!store.TryActivate("missing") && !store.TryReplace("missing", State()), "Unknown profile ID accepted");
            Require(File.ReadAllText(file) == saved, "Rejected profile operation changed disk");
            Require(store.TryRename(second.id, "训练") && store.Find(second.id).name == "训练", "Rename did not persist");
            Require(store.TryAdd("Sport", State(), out _) && !store.TryAdd("sport", State(), out _), "Name uniqueness must ignore case");

            Reject(store, file, s => s.panels[0].id = "unknown", "unknown panel ID");
            Reject(store, file, s => s.panels[0].id = "heart", "duplicate panel ID");
            Reject(store, file, s => s.panels = new AeroPanelState[0], "missing panels");
            Reject(store, file, s => s.panels[0].anchor = 8, "unknown reference frame");
            Reject(store, file, s => s.panels[0].position = new Vector3(float.NaN, 0, 1), "NaN pose");
            Reject(store, file, s => s.panels[0].position = Vector3.one * 100, "out-of-range pose");
            Reject(store, file, s => s.panels[0].physicalScale = float.PositiveInfinity, "infinite physical scale");
            Reject(store, file, s => s.panels[0].physicalScale = 0, "zero physical scale");
            Reject(store, file, s => s.panels[0].userScale = .2f, "out-of-range user scale");
            Reject(store, file, s => s.panels[0].speed = (int)HudSoftFollow.FollowMode.Fixed, "fixed speed preference");

            while (store.Data.profiles.Length < AeroLayoutProfileStore.MaxProfiles)
                Require(store.TryAdd("布局 " + store.Data.profiles.Length, State(), out _), store.LastError);
            saved = File.ReadAllText(file);
            Require(!store.TryAdd("超出数量", State(), out _) && File.ReadAllText(file) == saved, "Profile cap was not enforced");

            // A blocked staging path makes the write fail before replacing the good file.
            string memoryBefore = JsonUtility.ToJson(store.Data);
            Directory.CreateDirectory(file + ".tmp");
            Require(!store.TryRename(store.Data.activeId, "写入失败"), "Blocked staging write unexpectedly succeeded");
            Require(JsonUtility.ToJson(store.Data) == memoryBefore && File.ReadAllText(file) == saved,
                "Failed write changed memory or the previous good file");
            Directory.Delete(file + ".tmp");

            string corruptFile = Path.Combine(directory, "corrupt.json");
            var corrupt = new AeroLayoutProfileStore(corruptFile);
            Require(corrupt.TryAdd("原布局", State(), out var original), corrupt.LastError);
            Require(corrupt.TryRename(original.id, "第二次保存"), corrupt.LastError);
            const string damaged = "{ definitely not json";
            File.WriteAllText(corruptFile, damaged);
            var recovered = new AeroLayoutProfileStore(corruptFile);
            Require(recovered.Load() && recovered.RecoveredFromBackup && recovered.Find(original.id).name == "原布局",
                "Corrupt primary did not recover the previous valid backup");
            Require(recovered.TryAdd("恢复后", State(), out _), recovered.LastError);
            var evidence = Directory.GetFiles(directory, "corrupt.json.corrupt-*");
            Require(evidence.Length == 1 && File.ReadAllText(evidence[0]) == damaged, "Repair discarded damaged source bytes");
            Require(new AeroLayoutProfileStore(corruptFile).Load(), "Repaired file is unreadable");

            string isolated = Path.Combine(directory, "isolated.json"); File.WriteAllText(isolated, damaged);
            var empty = new AeroLayoutProfileStore(isolated);
            Require(!empty.Load() && empty.Data.profiles.Length == 0 && !string.IsNullOrEmpty(empty.LastError),
                "Unrecoverable corruption should fail with an empty safe collection");
            Require(empty.TryAdd("新布局", State(), out _) && Directory.GetFiles(directory, "isolated.json.corrupt-*").Length == 1,
                "Saving after unrecoverable corruption did not preserve the original");

            string unknown = Path.Combine(directory, "future.json"); var future = AeroLayoutProfileStore.Clone(store.Data);
            future.schemaVersion = 99; File.WriteAllText(unknown, JsonUtility.ToJson(future));
            Require(!new AeroLayoutProfileStore(unknown).Load(), "Unknown schema version was silently loaded");
            string large = Path.Combine(directory, "large.json"); File.WriteAllText(large, new string(' ', 300000));
            Require(!new AeroLayoutProfileStore(large).Load(), "Oversized file accepted");

            File.Copy(corruptFile, corruptFile + ".bak", true); File.Delete(corruptFile);
            var interrupted = new AeroLayoutProfileStore(corruptFile);
            Require(interrupted.Load() && interrupted.RecoveredFromBackup && interrupted.Save(),
                "Missing primary after interrupted replacement did not recover");
            Debug.Log("AeroProfileStore021Checks PASS: roundtrip, activation, names, ownership, validation, capacity, failed writes and backup recovery");
        }
        finally { Directory.Delete(directory, true); }
    }
}
