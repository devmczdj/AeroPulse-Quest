using System;
using UnityEngine;

// Invented, north-up schematic. One session state, never a GPS position.
public sealed class AeroRouteDemo
{
    public readonly struct Stop
    {
        public readonly string Name;
        public readonly Vector2 Position;
        public Stop(string name, float x, float y) { Name = name; Position = new Vector2(x, y); }
    }

    static readonly Stop[] stops =
    {
        new Stop("TRAILHEAD", -168, -70),
        new Stop("RIDGE", -94, 32),
        new Stop("LOOKOUT", -8, -14),
        new Stop("SPRING", 75, 66),
        new Stop("FINISH", 174, 25)
    };
    public int Count => stops.Length;
    public int CurrentIndex { get; private set; }
    public int NextIndex => CurrentIndex + 1 < Count ? CurrentIndex + 1 : -1;
    public Stop Current => stops[CurrentIndex];
    public string NextName => NextIndex >= 0 ? stops[NextIndex].Name : "COMPLETE";
    public event Action Changed;
    public Stop StopAt(int index) => stops[index];

    public void Advance()
    {
        CurrentIndex = (CurrentIndex + 1) % Count;
        Changed?.Invoke();
    }
}
