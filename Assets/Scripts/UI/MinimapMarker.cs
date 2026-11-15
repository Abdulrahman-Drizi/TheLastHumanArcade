using UnityEngine;

namespace TheLastHuman
{
    public enum MarkerShape { Dot, Arrow, Diamond, Ring, Door, Frame }

    /// <summary>
    /// A flat shape on the Minimap layer that follows something in the world. For a robot it also
    /// draws the red view triangle: the exact area that robot detects in, turned to where its head
    /// points and cut short wherever a wall or container blocks its sight.
    /// </summary>
    public class MinimapMarker : MonoBehaviour
    {
        public Transform target;
        public MarkerShape shape = MarkerShape.Dot;
        public float size = 0.9f;
        public float height = 20f;            // world height of the marker (above the floor plan)
        public bool turnWithTarget;
        public bool hideWithTarget;           // disappear when the target is switched off (e.g. the mask before it arrives)
        public Material material;

        [Header("Robot view triangle (optional)")]
        public RobotVision vision;
        public Material coneMaterial;
        public Material coneAlertMaterial;    // while this robot can see the player
        const int Rays = 9;                   // sight lines sampled across the triangle

        Transform cone;
        MeshRenderer coneRenderer;
        MeshRenderer shapeRenderer;
        Mesh coneMesh;
        readonly Vector3[] coneVertices = new Vector3[Rays + 1];

        void Start()
        {
            gameObject.layer = Layers.Id(Layers.Minimap);
            shapeRenderer = AddMesh(gameObject, BuildShape(shape, size), material);
            if (vision != null)
            {
                var go = new GameObject("ViewTriangle") { layer = gameObject.layer };
                cone = go.transform;
                cone.SetParent(transform.parent, false);
                coneMesh = new Mesh();
                coneMesh.MarkDynamic();
                var triangles = new int[(Rays - 1) * 3];
                for (int i = 0; i < Rays - 1; i++)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = i + 2;
                }
                coneMesh.vertices = coneVertices;
                coneMesh.triangles = triangles;
                coneRenderer = AddMesh(go, coneMesh, coneMaterial);
            }
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (hideWithTarget) shapeRenderer.enabled = target.gameObject.activeInHierarchy;
            var p = target.position;
            transform.position = new Vector3(p.x, height, p.z);
            if (turnWithTarget) transform.rotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            if (cone != null) UpdateCone(p);
        }

        /// <summary>Rebuilds the triangle from the robot's current sight lines.</summary>
        void UpdateCone(Vector3 robot)
        {
            var forward = vision.Forward;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            cone.SetPositionAndRotation(new Vector3(robot.x, height - 0.2f, robot.z), rotation);
            var eye = vision.EyePosition;
            float half = vision.viewAngle * 0.5f;
            coneVertices[0] = Vector3.zero;
            for (int i = 0; i < Rays; i++)
            {
                float angle = Mathf.Lerp(-half, half, i / (Rays - 1f));
                // the far edge of the triangle is straight, so side rays are longer than the middle one
                float reach = vision.viewDistance / Mathf.Cos(angle * Mathf.Deg2Rad);
                var local = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                if (Physics.Raycast(eye, rotation * local, out var hit, reach, Layers.SightBlockers, QueryTriggerInteraction.Ignore))
                    reach = hit.distance;
                coneVertices[i + 1] = local * reach;
            }
            coneMesh.vertices = coneVertices;
            coneMesh.RecalculateBounds();
            coneRenderer.sharedMaterial = vision.SeesPlayer ? coneAlertMaterial : coneMaterial;
        }

        static MeshRenderer AddMesh(GameObject go, Mesh mesh, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        static Mesh BuildShape(MarkerShape shape, float s)
        {
            switch (shape)
            {
                case MarkerShape.Arrow:
                    return Flat(new[] { new Vector3(0, 0, s), new Vector3(-0.7f * s, 0, -0.8f * s), new Vector3(0, 0, -0.35f * s), new Vector3(0.7f * s, 0, -0.8f * s) },
                                new[] { 0, 1, 2, 0, 2, 3 });
                case MarkerShape.Door:      // hiding closet: a solid block with a notch (the doorway) on its north side
                    return Flat(new[] { new Vector3(-s, 0, -s), new Vector3(s, 0, -s), new Vector3(s, 0, s), new Vector3(0.35f * s, 0, s),
                                        new Vector3(0.35f * s, 0, 0.2f * s), new Vector3(-0.35f * s, 0, 0.2f * s), new Vector3(-0.35f * s, 0, s), new Vector3(-s, 0, s) },
                                new[] { 0, 1, 5, 1, 4, 5, 1, 2, 4, 2, 3, 4, 0, 5, 7, 5, 6, 7 });
                case MarkerShape.Frame:     // hide box: a hollow square
                {
                    float o = s, i2 = s * 0.55f;
                    return Flat(new[] { new Vector3(-o, 0, -o), new Vector3(o, 0, -o), new Vector3(o, 0, o), new Vector3(-o, 0, o),
                                        new Vector3(-i2, 0, -i2), new Vector3(i2, 0, -i2), new Vector3(i2, 0, i2), new Vector3(-i2, 0, i2) },
                                new[] { 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7 });
                }
                case MarkerShape.Diamond:
                    return Flat(new[] { new Vector3(0, 0, s), new Vector3(-0.7f * s, 0, 0), new Vector3(0, 0, -s), new Vector3(0.7f * s, 0, 0) },
                                new[] { 0, 1, 2, 0, 2, 3 });
                default:
                    const int n = 14;
                    bool ring = shape == MarkerShape.Ring;
                    var v = new Vector3[ring ? n * 2 : n + 1];
                    var t = new int[ring ? n * 6 : n * 3];
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * Mathf.PI * 2f / n;
                        var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                        int j = (i + 1) % n;
                        if (ring)
                        {
                            v[i] = dir * s;
                            v[n + i] = dir * s * 0.62f;
                            t[i * 6] = i; t[i * 6 + 1] = n + i; t[i * 6 + 2] = j;
                            t[i * 6 + 3] = j; t[i * 6 + 4] = n + i; t[i * 6 + 5] = n + j;
                        }
                        else
                        {
                            v[i + 1] = dir * s;
                            t[i * 3] = 0; t[i * 3 + 1] = i + 1; t[i * 3 + 2] = j + 1;
                        }
                    }
                    return Flat(v, t);
            }
        }

        static Mesh Flat(Vector3[] vertices, int[] triangles)
        {
            var m = new Mesh { vertices = vertices, triangles = triangles };
            m.RecalculateBounds();
            return m;
        }
    }
}
