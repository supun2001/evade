using UnityEngine;

public class OfflinePlayerIdentity : MonoBehaviour
{
    [SerializeField] private string _sessionId = string.Empty;
    [SerializeField] private string _displayName = string.Empty;
    [SerializeField] private bool _isLocalPlayer;

    public string SessionId => _sessionId;
    public string DisplayName => _displayName;
    public bool IsLocalPlayer => _isLocalPlayer;

    public void Initialize(string sessionId, string displayName, bool isLocalPlayer)
    {
        _sessionId = sessionId ?? string.Empty;
        _displayName = displayName ?? string.Empty;
        _isLocalPlayer = isLocalPlayer;
    }
}
