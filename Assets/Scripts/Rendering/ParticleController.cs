using UnityEngine;

/// <summary>
/// 게임 이펙트 파티클 관리. Code-only ParticleSystem.
/// </summary>
public class ParticleController : MonoBehaviour
{
    private ParticleSystem _popPS;
    private ParticleSystem _cascadePS;
    private ParticleSystem _sparkPS;

    public void Initialize()
    {
        _popPS = GameRenderer.CreateParticleSystem(transform, "PopEffect",
            Color.white, 0.3f, 0.6f, 100, 0.8f);

        _cascadePS = GameRenderer.CreateParticleSystem(transform, "CascadeEffect",
            Color.yellow, 0.2f, 0.8f, 60, 0.5f);

        _sparkPS = GameRenderer.CreateParticleSystem(transform, "SparkEffect",
            Color.white, 0.15f, 1.0f, 40, -0.2f);
    }

    public void PlayPop(Vector3 position, Color color, int count = 8)
    {
        GameRenderer.EmitBurst(_popPS, position, count, color);
    }

    public void PlayCascade(Vector3 position, Color color)
    {
        GameRenderer.EmitBurst(_cascadePS, position, 12, color);
    }

    public void PlaySpark(Vector3 position)
    {
        GameRenderer.EmitBurst(_sparkPS, position, 6, Color.yellow);
    }
}
