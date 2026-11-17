using UnityEngine;
public enum AeroEditLayer { Environment, Hand, View }
public enum AeroWindowAnchor { Follow, World, View, Hand }
public class AeroDockZone : MonoBehaviour { public AeroWindowAnchor Anchor; }
