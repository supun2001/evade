using UnityEngine;

[System.Serializable]
public struct SkinEntry
{
    public string skinName;
    public Texture2D texture;
    public Sprite uiPreview;
}

[CreateAssetMenu(fileName = "SkinRegistry", menuName = "Game/SkinRegistry")]
public class SkinRegistry : ScriptableObject
{
    public SkinEntry[] skins;
}
