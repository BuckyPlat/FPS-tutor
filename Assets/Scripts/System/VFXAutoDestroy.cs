// File riêng: VFXAutoDestroy.cs
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
        // Khi particle đã dừng phát và không còn particle nào sống
        if (_ps != null && !_ps.IsAlive())
        {
            Destroy(gameObject);
        }
    }
}