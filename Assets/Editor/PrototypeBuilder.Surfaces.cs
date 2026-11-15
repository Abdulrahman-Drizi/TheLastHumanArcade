// Surface detail. The game's colours all come from its own palette; what the big flat surfaces
// lacked was grain. Three CC0 normal maps from ambientCG (see Assets/Art/Textures/CC0/README.txt)
// are laid over them: plated metal on the Monolith's floors, asphalt on the roads, cast concrete
// on pavements, platforms and the workplace yard. Only the normal maps are used, so no colour
// changes: the surfaces simply catch the light.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class PrototypeBuilder
{
    const string SurfaceTexDir = "Assets/Art/Textures/CC0";

    /// <summary>Called by each level builder just before its scene is saved.</summary>
    static void ApplySurfaceDetail()
    {
        int boxes = SurfaceDetail("MetalPlates016A", 4f, 0.55f, cHallFloor, cForecourt)
                  + SurfaceDetail("Asphalt031", 5f, 0.6f, cRoad)
                  + SurfaceDetail("Concrete034", 6f, 0.8f, cWalk, cSlab, mFloorA, mFloorB);
        log.Add("surface detail (CC0 normal maps) on " + boxes + " boxes");
    }

    /// <summary>
    /// Every large box in the open scene that wears one of 'materials' gets a copy of that material
    /// carrying the normal map, tiled so one repeat covers 'metres' whatever the box's size.
    /// (Boxes are scaled cubes, so each size needs its own tiling; copies are shared between equal sizes.)
    /// Returns how many boxes were changed.
    /// </summary>
    static int SurfaceDetail(string texture, float metres, float strength, params Material[] materials)
    {
        string path = SurfaceTexDir + "/" + texture + "_NormalGL.jpg";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { log.Add("surface texture missing: " + path); return 0; }
        if (importer.textureType != TextureImporterType.NormalMap || importer.maxTextureSize != 1024)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var wanted = new HashSet<Material>();
        foreach (var m in materials) if (m != null) wanted.Add(m);
        Directory.CreateDirectory(MatDir + "/Surfaces");

        int changed = 0;
        var made = new Dictionary<string, Material>();
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var source = renderer.sharedMaterial;
            if (source == null || !wanted.Contains(source)) continue;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.name != "Cube") continue;
            Vector3 size = renderer.transform.lossyScale;
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            // the face that matters is the big one: across the box's thinnest side
            int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            int a = (thin + 1) % 3, b = (thin + 2) % 3;
            if (Mathf.Min(size[a], size[b]) < 1.5f) continue;                // trims and strips keep the plain material
            if (!FaceAxes(filter.sharedMesh, thin, out int uAxis, out int vAxis)) continue;
            var tiling = new Vector2(Mathf.Round(size[uAxis] / metres * 20f) / 20f, Mathf.Round(size[vAxis] / metres * 20f) / 20f);
            string key = source.name + "_" + texture + "_" + tiling.x.ToString("0.00") + "x" + tiling.y.ToString("0.00");
            if (!made.TryGetValue(key, out var variant))
            {
                string variantPath = MatDir + "/Surfaces/" + key + ".mat";
                variant = AssetDatabase.LoadAssetAtPath<Material>(variantPath);
                if (variant == null)
                {
                    variant = new Material(source);
                    AssetDatabase.CreateAsset(variant, variantPath);
                }
                variant.CopyPropertiesFromMaterial(source);
                variant.SetTexture("_BumpMap", normal);
                variant.SetFloat("_BumpScale", strength);
                variant.EnableKeyword("_NORMALMAP");
                variant.mainTextureScale = tiling;                           // (URP Lit samples every map with the base map's tiling)
                EditorUtility.SetDirty(variant);
                made[key] = variant;
            }
            renderer.sharedMaterial = variant;
            changed++;
        }
        return changed;
    }

    /// <summary>
    /// On the face of the cube mesh that looks along axis 'normalAxis', which of the box's axes
    /// does the texture's u run along, and which its v? (Read from the mesh itself, not assumed.)
    /// </summary>
    static bool FaceAxes(Mesh cube, int normalAxis, out int uAxis, out int vAxis)
    {
        uAxis = vAxis = -1;
        var vertices = cube.vertices;
        var normals = cube.normals;
        var uvs = cube.uv;
        int first = -1;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (normals[i][normalAxis] < 0.9f) continue;                     // only the corners of the face on the positive side
            if (first < 0) { first = i; continue; }
            Vector3 dp = vertices[i] - vertices[first];
            Vector2 duv = uvs[i] - uvs[first];
            int axis = -1;
            for (int k = 0; k < 3; k++) if (k != normalAxis && Mathf.Abs(dp[k]) > 0.5f) axis = axis < 0 ? k : -2;
            if (axis < 0) continue;                                          // the diagonal corner (or the same one): tells nothing
            if (Mathf.Abs(duv.x) > 0.5f && Mathf.Abs(duv.y) < 0.5f) uAxis = axis;
            else if (Mathf.Abs(duv.y) > 0.5f && Mathf.Abs(duv.x) < 0.5f) vAxis = axis;
        }
        return uAxis >= 0 && vAxis >= 0;
    }
}
