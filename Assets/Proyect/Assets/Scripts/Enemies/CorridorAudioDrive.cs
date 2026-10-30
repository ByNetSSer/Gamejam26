using UnityEngine;

// Per-source soft saturation before the AudioSource's SFX volume and 3D attenuation.
public sealed class CorridorAudioDrive : MonoBehaviour
{
    public float Drive { get; set; } = 3f;
    void OnAudioFilterRead(float[] data, int channels)
    {
        float drive = Drive;
        for (int i = 0; i < data.Length; i++)
        {
            float sample = data[i] * drive;
            data[i] = sample / (1f + System.Math.Abs(sample));
        }
    }
}
