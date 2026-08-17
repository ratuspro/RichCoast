using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Data
{
    /// <summary>
    /// Designer-editable ball ladder. Seeded from <see cref="DefaultTierLadder"/> on creation, so
    /// an untouched asset is exactly the authored table; edits in the Inspector override it.
    ///
    /// <see cref="ToTable"/> hands the pure Core math a plain <see cref="TierTable"/> — Core never
    /// touches the asset, which is what keeps its tests asset-free.
    /// </summary>
    [CreateAssetMenu(menuName = "Rich Coast/Ball Tier Table", fileName = "BallTierTable")]
    public sealed class BallTierTableSO : ScriptableObject
    {
        [Tooltip("Base radii; index = tier-1. Tiers past this table grow geometrically by Radius Growth.")]
        [SerializeField] private float[] radii = (float[])Tuning.Radii.Clone();

        [Tooltip("Per-tier radius multiplier beyond the base table.")]
        [SerializeField] private float radiusGrowth = Tuning.RadiusGrowth;

        [Tooltip("The material ladder. Tiers past the end wrap around it, one ring per cycle.")]
        [SerializeField] private MaterialDef[] materials = DefaultTierLadder.CreateMaterials();

        private TierTable _cached;

        /// <summary>Plain-struct view for Core. Built once and reused; the asset is read-only at runtime.</summary>
        public TierTable ToTable() => _cached ??= new TierTable(radii, radiusGrowth, materials);

        private void OnValidate()
        {
            _cached = null; // pick up Inspector edits without a domain reload
            if (radiusGrowth < 1f) radiusGrowth = 1f;
        }

        private void Reset()
        {
            radii = (float[])Tuning.Radii.Clone();
            radiusGrowth = Tuning.RadiusGrowth;
            materials = DefaultTierLadder.CreateMaterials();
            _cached = null;
        }
    }
}
