using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Only layout preferences cross sessions; live measurements and input state never do.
[Serializable]
public sealed class AeroPanelState
{
    public string id;
    public bool visible, placed;
    public int anchor;
    public Vector3 position;
    public float physicalScale, userScale;
    public int followMode, speed;
    public bool pinned;
}

[Serializable]
public sealed class AeroLayoutState
{
    public AeroPanelState[] panels = Array.Empty<AeroPanelState>();
}

[Serializable]
public sealed class AeroLayoutProfile
{
    public string id, name;
    public AeroLayoutState layout;
}

[Serializable]
public sealed class AeroLayoutProfileCollection
{
    public int schemaVersion = 1;
    public string activeId = "";
    public AeroLayoutProfile[] profiles = Array.Empty<AeroLayoutProfile>();
}

public sealed class AeroLayoutProfileStore
{
    public const int MaxProfiles = 12;
    public const int MaxNameLength = 24;
    const int MaxFileBytes = 256 * 1024;
    static readonly HashSet<string> PanelIds = new HashSet<string> { "main", "heart", "pace", "distance", "next", "timer" };
    readonly string path;
    public AeroLayoutProfileCollection Data { get; private set; } = new AeroLayoutProfileCollection();
    public string LastError { get; private set; } = "";
    public bool RecoveredFromBackup { get; private set; }

    public AeroLayoutProfileStore(string filePath) { path = filePath; }

    public bool Load()
    {
        Data = new AeroLayoutProfileCollection(); LastError = ""; RecoveredFromBackup = false;
        if (!File.Exists(path) && !File.Exists(path + ".bak")) return true;
        if (TryRead(path, out var current, out string error)) { Data = current; return true; }
        if (TryRead(path + ".bak", out var backup, out _))
        {
            Data = backup; RecoveredFromBackup = true;
            LastError = "Recovered the previous layout file: " + error; return true;
        }
        LastError = error; return false;
    }

    public AeroLayoutProfile Find(string id)
    {
        foreach (var profile in Data.profiles) if (profile.id == id) return profile;
        return null;
    }

    public bool Save() => Commit(Clone(Data));

    public bool TryAdd(string name, AeroLayoutState state, out AeroLayoutProfile profile)
    {
        profile = null;
        if (!ValidateName(name, Data, null, out name, out string error) || !ValidateState(state, out error))
        { LastError = error; return false; }
        if (Data.profiles.Length >= MaxProfiles) { LastError = "The layout profile limit has been reached."; return false; }
        var next = Clone(Data);
        var added = new AeroLayoutProfile { id = Guid.NewGuid().ToString("N"), name = name, layout = Clone(state) };
        var list = new List<AeroLayoutProfile>(next.profiles) { added }; next.profiles = list.ToArray();
        next.activeId = added.id;
        if (!Commit(next)) return false;
        profile = Find(added.id); return true;
    }

    public bool TryReplace(string id, AeroLayoutState state)
    {
        if (!ValidateState(state, out string error)) { LastError = error; return false; }
        var next = Clone(Data); var profile = Find(next, id);
        if (profile == null) { LastError = "The layout profile no longer exists."; return false; }
        profile.layout = Clone(state); return Commit(next);
    }

    public bool TryRename(string id, string name)
    {
        if (!ValidateName(name, Data, id, out name, out string error)) { LastError = error; return false; }
        var next = Clone(Data); var profile = Find(next, id);
        if (profile == null) { LastError = "The layout profile no longer exists."; return false; }
        profile.name = name; return Commit(next);
    }

    public bool TryActivate(string id)
    {
        if (Find(id) == null) { LastError = "The layout profile no longer exists."; return false; }
        if (Data.activeId == id) { LastError = ""; return true; }
        var next = Clone(Data); next.activeId = id; return Commit(next);
    }

    static AeroLayoutProfile Find(AeroLayoutProfileCollection data, string id)
    { foreach (var profile in data.profiles) if (profile.id == id) return profile; return null; }

    public static AeroLayoutState Clone(AeroLayoutState state)
    {
        if (state == null) return null;
        var copy = new AeroLayoutState { panels = state.panels == null ? null : new AeroPanelState[state.panels.Length] };
        if (copy.panels == null) return copy;
        for (int i = 0; i < state.panels.Length; i++)
        {
            var p = state.panels[i];
            if (p != null) copy.panels[i] = new AeroPanelState { id = p.id, visible = p.visible, placed = p.placed,
                anchor = p.anchor, position = p.position, physicalScale = p.physicalScale, userScale = p.userScale,
                followMode = p.followMode, speed = p.speed, pinned = p.pinned };
        }
        return copy;
    }
    public static AeroLayoutProfileCollection Clone(AeroLayoutProfileCollection data)
    {
        if (data == null) return null;
        var copy = new AeroLayoutProfileCollection { schemaVersion = data.schemaVersion, activeId = data.activeId,
            profiles = data.profiles == null ? null : new AeroLayoutProfile[data.profiles.Length] };
        if (copy.profiles == null) return copy;
        for (int i = 0; i < data.profiles.Length; i++)
        {
            var p = data.profiles[i];
            if (p != null) copy.profiles[i] = new AeroLayoutProfile { id = p.id, name = p.name, layout = Clone(p.layout) };
        }
        return copy;
    }

    public static bool ValidateState(AeroLayoutState state, out string error)
    {
        error = "Invalid layout data.";
        if (state == null || state.panels == null || state.panels.Length != PanelIds.Count) return false;
        var seen = new HashSet<string>();
        foreach (var panel in state.panels)
        {
            if (panel == null || !PanelIds.Contains(panel.id) || !seen.Add(panel.id)) return false;
            if (panel.anchor < 0 || panel.anchor > (int)AeroWindowAnchor.Hand || panel.followMode < 0 || panel.followMode > (int)HudSoftFollow.FollowMode.Fixed || panel.speed < 0 || panel.speed > (int)HudSoftFollow.FollowMode.Quick) return false;
            if (!Finite(panel.position.x) || !Finite(panel.position.y) || !Finite(panel.position.z) || panel.position.sqrMagnitude > 10000f) return false;
            if (!Finite(panel.physicalScale) || panel.physicalScale < .00005f || panel.physicalScale > .01f) return false;
            if (!Finite(panel.userScale) || panel.userScale < HudSoftFollow.MinScale || panel.userScale > HudSoftFollow.MaxScale) return false;
        }
        error = ""; return true;
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    static bool ValidateName(string value, AeroLayoutProfileCollection data, string ownId, out string name, out string error)
    {
        name = value == null ? "" : value.Trim(); error = "Use a unique name containing 1–24 characters.";
        if (name.Length == 0 || name.Length > MaxNameLength) return false;
        foreach (char letter in name) if (char.IsControl(letter)) return false;
        foreach (var profile in data.profiles)
            if (profile != null && profile.id != ownId && string.Equals(profile.name, name, StringComparison.OrdinalIgnoreCase)) return false;
        error = ""; return true;
    }

    static bool Validate(AeroLayoutProfileCollection data, out string error)
    {
        error = "Unsupported or invalid layout file.";
        if (data == null || data.schemaVersion != 1 || data.profiles == null || data.profiles.Length > MaxProfiles) return false;
        var ids = new HashSet<string>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in data.profiles)
        {
            if (profile == null || !Guid.TryParseExact(profile.id, "N", out _) || !ids.Add(profile.id)) return false;
            if (!ValidateName(profile.name, new AeroLayoutProfileCollection(), null, out string name, out _) || name != profile.name || !names.Add(name)) return false;
            if (!ValidateState(profile.layout, out _)) return false;
        }
        if (data.profiles.Length == 0 ? !string.IsNullOrEmpty(data.activeId) : !ids.Contains(data.activeId)) return false;
        error = ""; return true;
    }

    static bool TryRead(string file, out AeroLayoutProfileCollection data, out string error)
    {
        data = null; error = "No saved layout file.";
        try
        {
            if (!File.Exists(file)) return false;
            var info = new FileInfo(file);
            if (info.Length == 0 || info.Length > MaxFileBytes) { error = "Invalid layout file size."; return false; }
            data = JsonUtility.FromJson<AeroLayoutProfileCollection>(File.ReadAllText(file, Encoding.UTF8));
            if (!Validate(data, out error)) { data = null; return false; }
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
        { error = exception.Message; return false; }
    }

    bool Commit(AeroLayoutProfileCollection next)
    {
        if (!Validate(next, out string error)) { LastError = error; return false; }
        string temporary = path + ".tmp";
        try
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(directory);
            bool exists = File.Exists(path), validPrevious = exists && TryRead(path, out _, out _);
            // Preserve damaged bytes for recovery instead of silently replacing the only copy.
            if (exists && !validPrevious) File.Copy(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(next, true));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (exists)
            {
                try { File.Replace(temporary, path, validPrevious ? path + ".bak" : null); }
                catch (PlatformNotSupportedException) { ReplaceWithBackup(temporary, validPrevious); }
            }
            else File.Move(temporary, path);
            Data = next; LastError = ""; RecoveredFromBackup = false; return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
        { LastError = exception.Message; return false; }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    void ReplaceWithBackup(string temporary, bool validPrevious)
    {
        string rollback = path + ".rollback-" + Guid.NewGuid().ToString("N");
        // On runtimes without replace, a crash between renames can recover the previous .bak.
        if (validPrevious) File.Copy(path, path + ".bak", true);
        File.Move(path, rollback);
        try { File.Move(temporary, path); }
        catch { File.Move(rollback, path); throw; }
        try { File.Delete(rollback); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
