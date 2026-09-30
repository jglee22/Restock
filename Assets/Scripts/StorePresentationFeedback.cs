using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 배치, 계산, 패널에 짧은 시각 반응만 담당한다.
// 게임 상태와 세이브 데이터는 갖지 않는다.
public class StorePresentationFeedback : MonoBehaviour
{
    const float PanelFadeSeconds = 0.14f;
    const float ResultFadeSeconds = 0.24f;
    const float BurstLifetimeSeconds = 0.4f;
    const int BurstCount = 12;

    [SerializeField] GameObject burstPrefab;
    readonly Dictionary<int, int> versions = new Dictionary<int, int>();
    readonly Dictionary<int, Vector3> baseScales = new Dictionary<int, Vector3>();
    readonly HashSet<int> hiding = new HashSet<int>();
    Material burstMaterial;

    void Awake()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader != null)
        {
            Color burstColor = new Color(1f, 0.93f, 0.78f, 0.9f);
            burstMaterial = new Material(shader);
            burstMaterial.color = burstColor;
            if (burstMaterial.HasProperty("_BaseColor"))
            {
                burstMaterial.SetColor("_BaseColor", burstColor);
            }
        }
    }

    void OnDestroy()
    {
        if (burstMaterial != null)
        {
            Destroy(burstMaterial);
        }
    }

    public void PlayBurst(Vector3 position)
    {
        if (burstMaterial == null)
        {
            return;
        }

        if (burstPrefab == null)
        {
            return;
        }

        GameObject burstObject = Instantiate(burstPrefab, position + Vector3.up * 0.45f, Quaternion.identity);
        burstObject.name = "PresentationBurst";
        burstObject.SetActive(true);
        ParticleSystem particles = burstObject.GetComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = burstObject.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && burstMaterial != null)
        {
            renderer.sharedMaterial = burstMaterial;
        }

        if (particles != null)
        {
            particles.Play();
        }
    }

    public void ClearBursts()
    {
        ParticleSystem[] systems = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include);
        for (int index = 0; index < systems.Length; index++)
        {
            if (systems[index] != null && systems[index].name == "PresentationBurst")
            {
                Destroy(systems[index].gameObject);
            }
        }
    }

    public bool IsOpen(GameObject panel)
    {
        return panel != null && panel.activeSelf && !hiding.Contains(panel.GetInstanceID());
    }

    public void Show(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        int id = panel.GetInstanceID();
        hiding.Remove(id);
        CanvasGroup group = Group(panel);
        RectTransform rect = panel.transform as RectTransform;
        Vector3 scale = BaseScale(id, rect);
        panel.SetActive(true);
        group.alpha = 0f;
        group.interactable = true;
        group.blocksRaycasts = true;
        if (rect != null)
        {
            rect.localScale = scale * 0.98f;
        }

        StartCoroutine(Fade(panel, group, rect, 0f, 1f, scale, PanelFadeSeconds, Next(id), false));
    }

    public void Hide(GameObject panel)
    {
        if (panel == null || !panel.activeSelf || hiding.Contains(panel.GetInstanceID()))
        {
            return;
        }

        int id = panel.GetInstanceID();
        hiding.Add(id);
        CanvasGroup group = Group(panel);
        group.interactable = false;
        group.blocksRaycasts = true;
        RectTransform rect = panel.transform as RectTransform;
        StartCoroutine(Fade(panel, group, rect, group.alpha, 0f, BaseScale(id, rect), PanelFadeSeconds, Next(id), true));
    }

    public void PresentResult(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        int id = panel.GetInstanceID();
        hiding.Remove(id);
        CanvasGroup group = Group(panel);
        RectTransform rect = panel.transform as RectTransform;
        Vector3 scale = BaseScale(id, rect);
        panel.SetActive(true);
        group.alpha = 0f;
        group.interactable = true;
        group.blocksRaycasts = true;
        if (rect != null)
        {
            rect.localScale = scale * 0.98f;
        }

        StartCoroutine(Fade(panel, group, rect, 0f, 1f, scale, ResultFadeSeconds, Next(id), false));
    }

    public void ResetPanels()
    {
        hiding.Clear();
        versions.Clear();
        StopAllCoroutines();
        ClearBursts();
    }

    IEnumerator Fade(
        GameObject panel,
        CanvasGroup group,
        RectTransform rect,
        float from,
        float to,
        Vector3 scale,
        float duration,
        int version,
        bool deactivate)
    {
        int id = panel.GetInstanceID();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!versions.TryGetValue(id, out int current) || current != version)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(from, to, t);
            if (rect != null)
            {
                rect.localScale = Vector3.Lerp(scale * 0.98f, scale, t);
            }

            yield return null;
        }

        if (!versions.TryGetValue(id, out int latest) || latest != version)
        {
            yield break;
        }

        group.alpha = to;
        if (rect != null)
        {
            rect.localScale = scale;
        }

        if (deactivate)
        {
            hiding.Remove(id);
            group.blocksRaycasts = false;
            panel.SetActive(false);
        }
    }

    int Next(int id)
    {
        versions.TryGetValue(id, out int version);
        version += 1;
        versions[id] = version;
        return version;
    }

    Vector3 BaseScale(int id, RectTransform rect)
    {
        if (baseScales.TryGetValue(id, out Vector3 scale))
        {
            return scale;
        }

        scale = rect != null ? rect.localScale : Vector3.one;
        baseScales[id] = scale;
        return scale;
    }

    static CanvasGroup Group(GameObject panel)
    {
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = panel.AddComponent<CanvasGroup>();
        }

        return group;
    }
}
