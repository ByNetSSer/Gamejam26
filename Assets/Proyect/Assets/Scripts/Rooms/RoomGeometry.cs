using System.Collections.Generic;
using UnityEngine;

// Geometric references are authored per room; they do not determine connectivity.
[DisallowMultipleComponent]
public sealed class RoomGeometry : MonoBehaviour
{
    [Tooltip("Objetos que contienen las paredes; pueden agrupar varias piezas.")]
    [SerializeField] GameObject[] walls = new GameObject[0];
    [Tooltip("Objetos que contienen el suelo; pueden agrupar varias piezas.")]
    [SerializeField] GameObject[] floors = new GameObject[0];
    [Tooltip("Objetos que contienen el techo; pueden agrupar varias piezas.")]
    [SerializeField] GameObject[] ceilings = new GameObject[0];

    public IReadOnlyList<GameObject> Walls => walls;
    public IReadOnlyList<GameObject> Floors => floors;
    public IReadOnlyList<GameObject> Ceilings => ceilings;
}
