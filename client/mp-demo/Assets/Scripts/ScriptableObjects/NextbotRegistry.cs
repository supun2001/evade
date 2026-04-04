using UnityEngine;

[System.Serializable]
public class NextbotRegistryEntry
{
    public string nextbotId;
    public string displayName;
    public Texture2D iconTexture;
    public AudioClip loopClip;
    public Color tint = Color.white;
    [Min(0f)] public float speed = 0f;
    [Range(0.5f, 1.5f)] public float loopPitch = 1f;
}

[CreateAssetMenu(fileName = "NextbotRegistry", menuName = "Game/Nextbot Registry")]
public class NextbotRegistry : ScriptableObject
{
    public NextbotRegistryEntry[] entries;

    public bool TryGetEntry(string nextbotId, out NextbotRegistryEntry entry)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                NextbotRegistryEntry candidate = entries[i];
                if (candidate != null && string.Equals(candidate.nextbotId, nextbotId, System.StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }
        }

        entry = null;
        return false;
    }
}
