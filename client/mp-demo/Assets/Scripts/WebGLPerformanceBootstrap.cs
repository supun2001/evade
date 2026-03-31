using UnityEngine;

public class WebGLPerformanceBootstrap : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    private const string WebGlHighQualityName = "WebGL High";
    private const string WebGlMediumQualityName = "WebGL Medium";
    private const string WebGlLowQualityName = "WebGL Low";
    private const int WebGlTargetFrameRate = 60;
    private const float WarmupDuration = 8f;
    private const float SampleDuration = 4f;
    private const float QualityChangeCooldown = 10f;
    private const float MediumToHighFpsThreshold = 58f;
    private const float HighToMediumFpsThreshold = 50f;
    private const float MediumToLowFpsThreshold = 32f;
    private const float LowToMediumFpsThreshold = 50f;

    private static bool _bootstrapped;
    private float _warmupTimer;
    private float _sampleTimer;
    private float _qualityChangeCooldownTimer;
    private int _sampleFrameCount;
    private int _highQualityIndex = -1;
    private int _currentQualityIndex = -1;
    private int _mediumQualityIndex = -1;
    private int _lowQualityIndex = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_bootstrapped)
        {
            return;
        }

        _bootstrapped = true;
        GameObject bootstrapObject = new GameObject(nameof(WebGLPerformanceBootstrap));
        DontDestroyOnLoad(bootstrapObject);
        bootstrapObject.AddComponent<WebGLPerformanceBootstrap>();
    }

    private void Awake()
    {
        Application.targetFrameRate = WebGlTargetFrameRate;
        QualitySettings.vSyncCount = 0;

        _highQualityIndex = FindQualityIndex(WebGlHighQualityName);
        _mediumQualityIndex = FindQualityIndex(WebGlMediumQualityName);
        _lowQualityIndex = FindQualityIndex(WebGlLowQualityName);
        _currentQualityIndex = _mediumQualityIndex >= 0 ? _mediumQualityIndex : _highQualityIndex;

        if (_currentQualityIndex >= 0 && QualitySettings.GetQualityLevel() != _currentQualityIndex)
        {
            QualitySettings.SetQualityLevel(_currentQualityIndex, true);
        }
    }

    private void Update()
    {
        if (_currentQualityIndex < 0)
        {
            return;
        }

        if (_qualityChangeCooldownTimer > 0f)
        {
            _qualityChangeCooldownTimer = Mathf.Max(0f, _qualityChangeCooldownTimer - Time.unscaledDeltaTime);
        }

        _warmupTimer += Time.unscaledDeltaTime;
        if (_warmupTimer < WarmupDuration)
        {
            return;
        }

        _sampleTimer += Time.unscaledDeltaTime;
        _sampleFrameCount++;

        if (_sampleTimer < SampleDuration)
        {
            return;
        }

        float averageFps = _sampleFrameCount / Mathf.Max(_sampleTimer, 0.0001f);
        _sampleTimer = 0f;
        _sampleFrameCount = 0;

        if (_qualityChangeCooldownTimer > 0f)
        {
            return;
        }

        if (_currentQualityIndex == _mediumQualityIndex)
        {
            if (averageFps >= MediumToHighFpsThreshold && _highQualityIndex >= 0)
            {
                SetQualityLevel(_highQualityIndex);
                return;
            }

            if (averageFps < MediumToLowFpsThreshold && _lowQualityIndex >= 0)
            {
                SetQualityLevel(_lowQualityIndex);
                return;
            }
        }

        if (_currentQualityIndex == _highQualityIndex && averageFps < HighToMediumFpsThreshold && _mediumQualityIndex >= 0)
        {
            SetQualityLevel(_mediumQualityIndex);
            return;
        }

        if (_currentQualityIndex == _lowQualityIndex && averageFps >= LowToMediumFpsThreshold && _mediumQualityIndex >= 0)
        {
            SetQualityLevel(_mediumQualityIndex);
        }
    }

    private void SetQualityLevel(int qualityIndex)
    {
        if (qualityIndex < 0 || qualityIndex == _currentQualityIndex)
        {
            return;
        }

        QualitySettings.SetQualityLevel(qualityIndex, true);
        _currentQualityIndex = qualityIndex;
        _sampleTimer = 0f;
        _sampleFrameCount = 0;
        _qualityChangeCooldownTimer = QualityChangeCooldown;
    }

    private static int FindQualityIndex(string qualityName)
    {
        string[] qualityNames = QualitySettings.names;
        for (int i = 0; i < qualityNames.Length; i++)
        {
            if (string.Equals(qualityNames[i], qualityName, System.StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
#endif
}
