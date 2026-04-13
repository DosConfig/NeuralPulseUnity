using UnityEngine;

/// <summary>
/// 사운드 + BGM 관리. Code-only AudioSource.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    private AudioSource _bgmSource;
    private AudioSource _sfxSource;

    public float SoundVolume { get; set; } = 1f;
    public float BgmVolume { get; set; } = 0.2f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop = false;
        _sfxSource.playOnAwake = false;
    }

    public void PlaySFX(string resourcePath, float volumeScale = 1f)
    {
        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip != null)
            _sfxSource.PlayOneShot(clip, SoundVolume * volumeScale);
    }

    public void PlayBGM(string resourcePath)
    {
        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null) return;
        _bgmSource.clip = clip;
        _bgmSource.volume = BgmVolume;
        _bgmSource.Play();
    }

    public void StopBGM()
    {
        _bgmSource.Stop();
    }

    public void SetVolume(float volume)
    {
        SoundVolume = Mathf.Clamp01(volume);
        _bgmSource.volume = BgmVolume * SoundVolume;
    }

    // ====== 편의 메서드 (게임 이벤트별) ======

    public void PlayChainSelect(int chainLength)
    {
        // 체인 길이별 피치 상승 (펜타토닉)
        float pitch = 1f + (chainLength - 1) * 0.1f;
        _sfxSource.pitch = Mathf.Min(pitch, 2f);
        PlaySFX("Audio/SFX/chain_select", 0.35f + chainLength * 0.03f);
        _sfxSource.pitch = 1f;
    }

    public void PlayPop(int count)
    {
        float vol = Mathf.Clamp(0.4f + count * 0.05f, 0.4f, 0.7f);
        PlaySFX("Audio/SFX/pop", vol);
    }

    public void PlayGrade(GameManager.ChainGrade grade)
    {
        if (grade == GameManager.ChainGrade.None) return;
        PlaySFX($"Audio/SFX/grade_{grade.ToString().ToLower()}", 0.5f);
    }
}
