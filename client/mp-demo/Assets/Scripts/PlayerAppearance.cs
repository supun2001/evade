using UnityEngine;
using System.Collections.Generic;
using Colyseus.Schema;
using GameDevWare.Serialization;

public class PlayerAppearance : MonoBehaviour
{
    public SkinRegistry skinRegistry;
    public Renderer[] targetRenderers;
    [SerializeField] private float _skinPollInterval = 0.25f;
    private Player _playerSchema;
    private float _nextSkinPollTime;

    public void Initialize(Player playerSchema)
    {
        _playerSchema = playerSchema;
        
        SetSkin((int)playerSchema.skinIndex);
    }

    private int _lastSkinIndex = -1;

    private void Update()
    {
        if (_playerSchema == null || Time.unscaledTime < _nextSkinPollTime)
        {
            return;
        }

        _nextSkinPollTime = Time.unscaledTime + Mathf.Max(0.05f, _skinPollInterval);

        int currentSkinIndex = Mathf.RoundToInt(_playerSchema.skinIndex);
        if (currentSkinIndex != _lastSkinIndex)
        {
            SetSkin(currentSkinIndex);
            _lastSkinIndex = currentSkinIndex;
        }
    }

    private void SetSkin(int index)
    {
        ApplySkinToRenderers(skinRegistry, index, GetTargetRenderers());
    }

    private void OnDestroy() {
        _playerSchema = null;
    }

    public Renderer[] GetTargetRenderers()
    {
        if (targetRenderers != null && targetRenderers.Length > 0)
        {
            List<Renderer> assignedRenderers = new List<Renderer>(targetRenderers.Length);
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null)
                {
                    assignedRenderers.Add(targetRenderers[i]);
                }
            }

            if (assignedRenderers.Count > 0)
            {
                return assignedRenderers.ToArray();
            }
        }

        Renderer[] discoveredRenderers = GetComponentsInChildren<Renderer>(true);
        List<Renderer> validRenderers = new List<Renderer>(discoveredRenderers.Length);
        for (int i = 0; i < discoveredRenderers.Length; i++)
        {
            Renderer renderer = discoveredRenderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            {
                continue;
            }

            validRenderers.Add(renderer);
        }

        targetRenderers = validRenderers.ToArray();
        return targetRenderers;
    }

    public static bool ApplySkinToRenderers(SkinRegistry registry, int index, Renderer[] renderers)
    {
        if (registry == null || registry.skins == null || index < 0 || index >= registry.skins.Length || renderers == null)
        {
            return false;
        }

        Texture texture = registry.skins[index].texture;
        if (texture == null)
        {
            return false;
        }

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", texture);
                }

                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", texture);
                }
            }
        }

        return true;
    }
}
