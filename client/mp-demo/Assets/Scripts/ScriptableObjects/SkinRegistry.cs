using UnityEngine;

[System.Serializable]
public struct SkinEntry
{
    public string skinName;
    public Texture2D texture;
    public Sprite uiPreview;
    public int price;
    public bool unlockedByDefault;
}

[CreateAssetMenu(fileName = "SkinRegistry", menuName = "Game/SkinRegistry")]
public class SkinRegistry : ScriptableObject
{
    public SkinEntry[] skins;
}
