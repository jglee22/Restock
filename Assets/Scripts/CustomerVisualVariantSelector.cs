using System;
using System.Collections.Generic;
using UnityEngine;

// 고객이 생길 때 미리 정한 외형 중 하나를 고른다. 쇼핑이나 계산 상태는 보지 않는다.
public class CustomerVisualVariantSelector : MonoBehaviour
{
    [Serializable]
    public struct PartMaterial
    {
        public string partName;
        public Material material;
    }

    [Serializable]
    public struct Outfit
    {
        public string[] parts;
        public PartMaterial[] materials;
    }

    [SerializeField] string[] toggleParts;
    [SerializeField] Outfit[] outfits;

    void Awake()
    {
        if (outfits == null || outfits.Length == 0)
        {
            return;
        }

        Apply(UnityEngine.Random.Range(0, outfits.Length));
    }

    void Apply(int index)
    {
        if (outfits == null || index < 0 || index >= outfits.Length)
        {
            return;
        }

        Dictionary<string, Renderer> renderers = new Dictionary<string, Renderer>();
        Renderer[] found = GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < found.Length; rendererIndex++)
        {
            Renderer renderer = found[rendererIndex];
            if (!renderers.ContainsKey(renderer.gameObject.name))
            {
                renderers.Add(renderer.gameObject.name, renderer);
            }
        }

        SetActive(renderers, toggleParts, false);
        Outfit outfit = outfits[index];
        SetActive(renderers, outfit.parts, true);
        if (outfit.materials == null)
        {
            return;
        }

        for (int materialIndex = 0; materialIndex < outfit.materials.Length; materialIndex++)
        {
            PartMaterial binding = outfit.materials[materialIndex];
            if (binding.material == null || string.IsNullOrEmpty(binding.partName))
            {
                continue;
            }

            if (!renderers.TryGetValue(binding.partName, out Renderer renderer))
            {
                Debug.LogWarning($"CustomerVisualVariantSelector: '{binding.partName}' 부위를 찾지 못했습니다.", this);
                continue;
            }

            renderer.sharedMaterial = binding.material;
        }
    }

    void SetActive(Dictionary<string, Renderer> renderers, string[] partNames, bool active)
    {
        if (partNames == null)
        {
            return;
        }

        for (int index = 0; index < partNames.Length; index++)
        {
            string partName = partNames[index];
            if (string.IsNullOrEmpty(partName))
            {
                continue;
            }

            if (!renderers.TryGetValue(partName, out Renderer renderer))
            {
                Debug.LogWarning($"CustomerVisualVariantSelector: '{partName}' 부위를 찾지 못했습니다.", this);
                continue;
            }

            renderer.gameObject.SetActive(active);
        }
    }
}
