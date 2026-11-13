using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

// Keep the tested download route when Unity regenerates the Gradle project.
public class AeroGradleBuildSettings : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 100;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string root = Directory.GetParent(path).FullName;
        string settingsPath = Path.Combine(root, "settings.gradle");
        string settings = File.ReadAllText(settingsPath).Replace("\r\n", "\n");
        const string original = "        gradlePluginPortal()\n        google()\n        mavenCentral()";
        const string preferred = "        google()\n        mavenCentral()\n        gradlePluginPortal()";
        if (!settings.Contains(original) && !settings.Contains(preferred))
            throw new InvalidOperationException("Unexpected plugin repository template; review download ordering before building.");
        File.WriteAllText(settingsPath, settings.Replace(original, preferred));

        string propsPath = Path.Combine(root, "gradle.properties");
        string props = File.ReadAllText(propsPath);
        props = SetProperty(props, "org.gradle.workers.max", "1");
        props = SetProperty(props, "org.gradle.parallel", "false");
        // Only this generated build uses the explicitly requested local proxy.
        string port = Environment.GetEnvironmentVariable("AEROPULSE_GRADLE_PROXY_PORT");
        if (!string.IsNullOrWhiteSpace(port))
        {
            if (!ushort.TryParse(port, out ushort number) || number == 0)
                throw new InvalidOperationException("AEROPULSE_GRADLE_PROXY_PORT must be a valid TCP port.");
            foreach (string scheme in new[] { "http", "https" })
            {
                props = SetProperty(props, "systemProp." + scheme + ".proxyHost", "127.0.0.1");
                props = SetProperty(props, "systemProp." + scheme + ".proxyPort", number.ToString());
            }
        }
        File.WriteAllText(propsPath, props);
        Debug.Log("[AeroPulse Hands] Gradle: official repositories reordered, one worker, local proxy=" + !string.IsNullOrWhiteSpace(port));
    }

    static string SetProperty(string text, string key, string value)
    {
        string pattern = "(?m)^" + Regex.Escape(key) + "=.*$";
        string entry = key + "=" + value;
        return Regex.IsMatch(text, pattern) ? Regex.Replace(text, pattern, entry) : text.TrimEnd() + "\n" + entry + "\n";
    }
}
