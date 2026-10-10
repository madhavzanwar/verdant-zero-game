using UnityEngine;

/// <summary>
/// Marks an object (and its children) as a deadly obstacle: if the quadcopter touches it, it crashes.
/// The city generator adds it to every building.
/// </summary>
[DisallowMultipleComponent]
public class CrashHazard : MonoBehaviour
{
}
