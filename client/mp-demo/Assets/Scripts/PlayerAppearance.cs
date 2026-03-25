using UnityEngine;
using System.Collections.Generic;
using Colyseus.Schema;
using GameDevWare.Serialization;

public class PlayerAppearance : MonoBehaviour
{
    public SkinRegistry skinRegistry;
    public Renderer[] targetRenderers;
    private Player _playerSchema;

    public void Initialize(Player playerSchema)
    {
        _playerSchema = playerSchema;
        
        SetSkin((int)playerSchema.skinIndex);
    }

    private int _lastSkinIndex = -1;

    private void Update()
    {
        if (_playerSchema != null)
        {
            int currentSkinIndex = Mathf.RoundToInt(_playerSchema.skinIndex);
            if (currentSkinIndex != _lastSkinIndex)
            {
                SetSkin(currentSkinIndex);
                _lastSkinIndex = currentSkinIndex;
            }
        }
    }

    private void SetSkin(int index)
    {
        if (skinRegistry == null || index < 0 || index >= skinRegistry.skins.Length) return;

        if (targetRenderers != null)
        {
            foreach (var renderer in targetRenderers)
            {
                if (renderer != null)
                {
                    // Access the .texture property from the SkinEntry struct
                    renderer.material.mainTexture = skinRegistry.skins[index].texture;
                }
            }
        }
    }

    private void OnDestroy() {
        _playerSchema = null;
    }
}