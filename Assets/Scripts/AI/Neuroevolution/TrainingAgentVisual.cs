using UnityEngine;

namespace Neuroevolution
{
    /// <summary>
    /// Visual tweaks for parallel agents: alpha + sorting so stacks look clean.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrainingAgentVisual : MonoBehaviour
    {
        [Range(0f, 1f)] public float opacity = 0.25f;
        public int sortingOrderOffset = 0;

        private SpriteRenderer[] srs;
        private Color[] baseColors;
        private int[] baseOrders;

        private void Awake()
        {
            srs = GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[srs.Length];
            baseOrders = new int[srs.Length];
            for (int i = 0; i < srs.Length; i++)
            {
                baseColors[i] = srs[i] != null ? srs[i].color : Color.white;
                baseOrders[i] = srs[i] != null ? srs[i].sortingOrder : 0;
            }
        }

        public void Configure(float alpha, int orderOffset)
        {
            opacity = Mathf.Clamp01(alpha);
            sortingOrderOffset = orderOffset;
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Apply()
        {
            if (srs == null) return;
            for (int i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                if (sr == null) continue;
                var c = baseColors != null && i < baseColors.Length ? baseColors[i] : sr.color;
                sr.color = new Color(c.r, c.g, c.b, opacity);
                sr.sortingOrder = (baseOrders != null && i < baseOrders.Length ? baseOrders[i] : sr.sortingOrder) + sortingOrderOffset;
            }
        }
    }
}

