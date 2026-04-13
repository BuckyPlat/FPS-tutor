// Separate file: VFXAutoDestroy.cs
using UnityEngine;

public class VFXAutoDestroy : MonoBehaviour
{
    private ParticleSystem _ps;

    private void Start()
    {
        _ps = GetComponent<ParticleSystem>();
    }

    private void Update()
    {
        // When the particle has stopped playing and is no longer alive
        if (_ps != null && !_ps.IsAlive())
        {
            Destroy(gameObject);
        }
    }
}