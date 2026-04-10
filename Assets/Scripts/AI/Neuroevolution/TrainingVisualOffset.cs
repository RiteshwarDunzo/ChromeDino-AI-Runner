using UnityEngine;
using System.Collections.Generic;

namespace Neuroevolution
{
    /// <summary>
    /// Visual-only offset so multiple parallel agents are visible even when overlapping physically.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrainingVisualOffset : MonoBehaviour
    {
        public Vector3 offset = Vector3.zero;

        private struct VisualEntry
        {
            public Transform targetTransform;
            public Vector3 baseLocalPosition;
            public SpriteRenderer sourceRenderer;
            public SpriteRenderer proxyRenderer;
            public bool usesProxy;
        }

        private readonly List<VisualEntry> visuals = new();

        private void Awake()
        {
            RebuildVisualTargets();
        }

        private void OnEnable()
        {
            if (visuals.Count == 0)
            {
                RebuildVisualTargets();
            }

            for (int i = 0; i < visuals.Count; i++)
            {
                var entry = visuals[i];
                if (!entry.usesProxy || entry.sourceRenderer == null) continue;
                entry.sourceRenderer.enabled = false;
                if (entry.proxyRenderer != null) entry.proxyRenderer.enabled = true;
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < visuals.Count; i++)
            {
                var entry = visuals[i];
                if (!entry.usesProxy || entry.sourceRenderer == null) continue;
                entry.sourceRenderer.enabled = true;
                if (entry.proxyRenderer != null) entry.proxyRenderer.enabled = false;
            }
        }

        private void RebuildVisualTargets()
        {
            visuals.Clear();

            // Some dino prefabs can have multiple SpriteRenderers (body parts / child objects).
            // Offset them all so we don't "tear" the sprite hierarchy.
            var srs = GetComponentsInChildren<SpriteRenderer>(true);
            if (srs == null || srs.Length == 0) return;

            foreach (var sr in srs)
            {
                if (sr == null) continue;

                if (sr.transform == transform)
                {
                    var proxyGo = new GameObject($"{sr.gameObject.name}_VisualProxy");
                    proxyGo.transform.SetParent(transform, false);
                    proxyGo.hideFlags = HideFlags.DontSave;

                    var proxy = proxyGo.AddComponent<SpriteRenderer>();
                    proxy.enabled = false;

                    visuals.Add(new VisualEntry
                    {
                        targetTransform = proxy.transform,
                        baseLocalPosition = Vector3.zero,
                        sourceRenderer = sr,
                        proxyRenderer = proxy,
                        usesProxy = true
                    });
                }
                else
                {
                    visuals.Add(new VisualEntry
                    {
                        targetTransform = sr.transform,
                        baseLocalPosition = sr.transform.localPosition,
                        sourceRenderer = sr,
                        proxyRenderer = null,
                        usesProxy = false
                    });
                }
            }
        }

        private void LateUpdate()
        {
            for (int i = 0; i < visuals.Count; i++)
            {
                var entry = visuals[i];
                var t = entry.targetTransform;
                if (t == null) continue;
                t.localPosition = entry.baseLocalPosition + offset;

                if (!entry.usesProxy || entry.sourceRenderer == null || entry.proxyRenderer == null) continue;

                var src = entry.sourceRenderer;
                var proxy = entry.proxyRenderer;
                proxy.enabled = true;
                proxy.sharedMaterial = src.sharedMaterial;
                proxy.sprite = src.sprite;
                proxy.drawMode = src.drawMode;
                proxy.size = src.size;
                proxy.color = src.color;
                proxy.flipX = src.flipX;
                proxy.flipY = src.flipY;
                proxy.sortingLayerID = src.sortingLayerID;
                proxy.sortingOrder = src.sortingOrder;
            }
        }
    }
}
