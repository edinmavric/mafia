using UnityEngine;

namespace MafiaGame.Presentation.Match
{
    /// <summary>
    /// Placeholder 3D environment for the match scene: a floor, a round table and one marker per
    /// possible seat. It is built from primitives at runtime rather than authored in the scene so the
    /// scene file stays trivial and nothing here can break a serialized reference.
    ///
    /// This is deliberately not the real environment (Milestone 6). It exists so the match visibly
    /// happens somewhere other than the lobby, and so the scene flow can be tested for real.
    ///
    /// The scene carries no camera and no light of its own: it is loaded additively over the lobby
    /// scene, which already has both, and a second camera or directional light would fight with them.
    /// </summary>
    public sealed class MatchEnvironment : MonoBehaviour
    {
        /// <summary>Markers are built for the largest lobby, so the ring never changes size mid-match.</summary>
        private const int SeatMarkerCount = 10;

        private const float TableRadius = 2.5f;
        private const float TableHeight = 0.75f;
        private const float SeatRingRadius = 3.6f;

        private static readonly Color FloorColor = new Color(0.16f, 0.17f, 0.20f);
        private static readonly Color TableColor = new Color(0.35f, 0.24f, 0.18f);
        private static readonly Color SeatColor = new Color(0.55f, 0.57f, 0.62f);

        private void Awake()
        {
            BuildFloor();
            BuildTable();
            BuildSeatMarkers();
        }

        private void BuildFloor()
        {
            GameObject floor = CreatePart(PrimitiveType.Plane, "Floor", FloorColor);
            floor.transform.localPosition = Vector3.zero;
            floor.transform.localScale = new Vector3(3f, 1f, 3f); // Unity's plane is 10x10 per unit scale.
        }

        private void BuildTable()
        {
            GameObject table = CreatePart(PrimitiveType.Cylinder, "Table", TableColor);
            table.transform.localPosition = new Vector3(0f, TableHeight * 0.5f, 0f);

            // A Unity cylinder is 2 units tall and 1 unit wide at scale 1, hence the halved height.
            table.transform.localScale = new Vector3(TableRadius * 2f, TableHeight * 0.5f, TableRadius * 2f);
        }

        private void BuildSeatMarkers()
        {
            for (int seat = 0; seat < SeatMarkerCount; seat++)
            {
                float angle = seat * Mathf.PI * 2f / SeatMarkerCount;
                GameObject marker = CreatePart(PrimitiveType.Cube, $"Seat {seat + 1}", SeatColor);
                marker.transform.localPosition = new Vector3(
                    Mathf.Sin(angle) * SeatRingRadius, 0.45f, Mathf.Cos(angle) * SeatRingRadius);
                marker.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                marker.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            }
        }

        /// <summary>
        /// The material every part is drawn with. It must be a real asset referenced from here:
        /// primitives are created with the built-in pipeline's default material, which URP cannot
        /// render, and in a build there is nothing to fall back on — URP's own
        /// <c>RenderPipelineAsset.defaultMaterial</c> is Editor-only and returns null in a player, so
        /// everything came out magenta. A serialized reference also keeps the shader in the build,
        /// which <see cref="Shader.Find"/> cannot promise. Assigned by MafiaGame → Create Game scene.
        /// </summary>
        [SerializeField] private Material _partMaterial;

        private Material PartMaterial()
        {
            if (_partMaterial != null)
            {
                return _partMaterial;
            }

            // Last resort. Works in the Editor; in a build it only works if something else already
            // pulled the shader in, hence the warning rather than a silent fallback.
            Shader lit = Shader.Find(LitShaderName);
            if (lit == null)
            {
                Debug.LogWarning(
                    "[MatchEnvironment] No material assigned and the URP Lit shader is not in this " +
                    "build; the placeholder environment will render magenta. Run " +
                    "MafiaGame → Create Game scene, then rebuild.");
                return null;
            }

            _partMaterial = new Material(lit);
            return _partMaterial;
        }

        private const string LitShaderName = "Universal Render Pipeline/Lit";

        /// <summary>
        /// Creates one primitive under this object. The colour is applied through a property block so
        /// no material asset is touched — writing to the shared default material would recolour every
        /// primitive in the project, including in the Editor.
        /// </summary>
        private GameObject CreatePart(PrimitiveType type, string name, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(transform, false);

            // Nothing moves or is clicked in the scene, so the colliders are pure overhead.
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = PartMaterial();
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }

                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColorProperty, color);
                block.SetColor(LegacyColorProperty, color);
                renderer.SetPropertyBlock(block);
            }

            return part;
        }

        // URP's Lit shader uses _BaseColor; _Color is kept as a harmless fallback for other shaders.
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorProperty = Shader.PropertyToID("_Color");
    }
}
