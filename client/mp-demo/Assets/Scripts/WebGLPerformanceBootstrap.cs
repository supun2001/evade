using UnityEngine;

public class WebGLPerformanceBootstrap : MonoBehaviour
{
    public const string LowQualityName = "WebGL Low";
    public const string MediumQualityName = "WebGL Medium";
    public const string HighQualityName = "WebGL High";
    private const string ManualGraphicsQualityPlayerPrefsKey = "WebGLManualGraphicsQuality";

    public static bool TryApplyManualGraphicsQuality(string qualityName)
    {
        int qualityIndex = FindQualityIndex(qualityName);
        if (qualityIndex < 0)
        {
            return false;
        }

        QualitySettings.SetQualityLevel(qualityIndex, true);
        PlayerPrefs.SetString(ManualGraphicsQualityPlayerPrefsKey, qualityName);
        PlayerPrefs.Save();
        return true;
    }

    public static string GetSavedGraphicsQualityName()
    {
        return PlayerPrefs.GetString(ManualGraphicsQualityPlayerPrefsKey, string.Empty);
    }

    public static string GetActiveGraphicsQualityName()
    {
        int qualityIndex = QualitySettings.GetQualityLevel();
        string[] qualityNames = QualitySettings.names;
        if (qualityIndex < 0 || qualityIndex >= qualityNames.Length)
        {
            return string.Empty;
        }

        return qualityNames[qualityIndex];
    }

    public static bool IsManualGraphicsQualitySelected()
    {
        return !string.IsNullOrEmpty(GetSavedGraphicsQualityName());
    }

#if UNITY_WEBGL && !UNITY_EDITOR
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
    private bool _manualQualityOverrideEnabled;

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

        _highQualityIndex = FindQualityIndex(HighQualityName);
        _mediumQualityIndex = FindQualityIndex(MediumQualityName);
        _lowQualityIndex = FindQualityIndex(LowQualityName);
        _currentQualityIndex = QualitySettings.GetQualityLevel();

        string savedQualityName = GetSavedGraphicsQualityName();
        int savedQualityIndex = FindQualityIndex(savedQualityName);
        if (savedQualityIndex >= 0)
        {
            _manualQualityOverrideEnabled = true;
            _currentQualityIndex = savedQualityIndex;
            if (QualitySettings.GetQualityLevel() != _currentQualityIndex)
            {
                QualitySettings.SetQualityLevel(_currentQualityIndex, true);
            }
        }
        else if (_currentQualityIndex < 0)
        {
            _currentQualityIndex = _mediumQualityIndex >= 0 ? _mediumQualityIndex : _highQualityIndex;
            if (_currentQualityIndex >= 0 && QualitySettings.GetQualityLevel() != _currentQualityIndex)
            {
                QualitySettings.SetQualityLevel(_currentQualityIndex, true);
            }
        }
    }

    private void Update()
    {
        if (_currentQualityIndex < 0 || _manualQualityOverrideEnabled)
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
#endif

    private static int FindQualityIndex(string qualityName)
    {
        if (string.IsNullOrEmpty(qualityName))
        {
            return -1;
        }

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
}
