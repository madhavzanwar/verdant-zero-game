using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Generates a compact 3x3-block city in the open scene using
/// the "POLYGON city pack" package (Asset Store, imported locally).
/// Menu: Verdant Zero > Generate City 3x3. It is idempotent: it deletes the previous "City" object.
/// </summary>
public static class CityGridGenerator
{
    private const string PackRoot = "Assets/POLYGON city pack/Prefabs/";
    private const string MaterialsDir = "Assets/VerdantZero/Materials/Environment/";

    // Layout (meters)
    private const int Blocks = 3;
    private const float BlockSize = 40f;
    private const float RoadWidth = 8f;
    private const float Pitch = BlockSize + RoadWidth;              // 48
    private const float HalfExtent = (Blocks * Pitch + RoadWidth) / 2f; // 76
    private const float SidewalkHeight = 0.2f;
    private const float Setback = 3f;      // sidewalk between street and facade
    private const float LampSpacing = 13f;
    private const float WallHeight = 80f;

    private const int Seed = 1234;

    // Buildings facing -Z (the rest face +Z)
    private static readonly HashSet<string> FacesNegativeZ = new HashSet<string>
    {
        "Shop_A_prefab", "Fire_department_prefab", "Bank_prefab",
    };

    private static readonly string[] TallPool =
    {
        "Building_A_prefab", "Building_B1_prefab", "Building_K_prefab", "Building_J_prefab",
    };

    private static readonly string[] MidPool =
    {
        "Building_H_prefab", "Building_I_1_prefab", "Building_I_2_Prefab", "Building_I_3_prefab",
        "Building_D_prefab", "Building_R_Prefab", "Building_S_prefab", "Bulding_C_prefab 1",
        "Build_G-Left_Prefab", "Build_G-middle_Prefab", "Build_G-right_Prefab", "Shop_A_prefab",
    };

    private static readonly string[] LowPool =
    {
        "Building_N_Prefab", "Building_O_PREFAB", "building_X_prefab", "Building_T_prefab",
        "Building_W_prefab", "Building_Q_prefab", "Building_M_prefab", "Shop_A_prefab",
    };

    private struct Candidate
    {
        public GameObject Prefab;
        public float Width;
        public float Depth;
    }

    [MenuItem("Verdant Zero/Generate City 3x3")]
    public static void Generate()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + "Floor/Street 2 Prefab.prefab") == null)
        {
            EditorUtility.DisplayDialog("City generator",
                "'POLYGON city pack' was not found. Import it from the Asset Store (it is in .gitignore).", "OK");
            return;
        }

        Random.State oldState = Random.state;
        Random.InitState(Seed);

        GameObject existing = GameObject.Find("City");
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        var city = new GameObject("City");
        Undo.RegisterCreatedObjectUndo(city, "Generate City");
        Transform roads = NewChild(city.transform, "Roads");
        Transform blocks = NewChild(city.transform, "Blocks");
        Transform buildings = NewChild(city.transform, "Buildings");
        Transform props = NewChild(city.transform, "Props");
        Transform bounds = NewChild(city.transform, "Bounds");

        BuildRoads(roads);
        Material sidewalk = GetSidewalkMaterial();

        for (int bx = 0; bx < Blocks; bx++)
        {
            for (int bz = 0; bz < Blocks; bz++)
            {
                Vector3 center = BlockCenter(bx, bz);
                BuildBlockBase(blocks, center, sidewalk, $"Block_{bx}_{bz}");

                bool isCenter = bx == 1 && bz == 1;
                bool isCorner = (bx != 1) && (bz != 1);
                string[] pool = isCenter ? TallPool : isCorner ? LowPool : MidPool;
                bool rowsAlongX = (bx + bz) % 2 == 0;

                Transform blockRoot = NewChild(buildings, $"Block_{bx}_{bz}");
                if (rowsAlongX)
                {
                    BuildRow(blockRoot, pool, center, Vector3.forward);
                    BuildRow(blockRoot, pool, center, Vector3.back);
                }
                else
                {
                    BuildRow(blockRoot, pool, center, Vector3.right);
                    BuildRow(blockRoot, pool, center, Vector3.left);
                }

                BuildSidewalkProps(props, center);
            }
        }

        BuildBoundaryWalls(bounds);
        ConfigureGround();
        PlacePlayer();
        ConfigureAtmosphere();

        Random.state = oldState;
        EditorSceneManager.MarkSceneDirty(city.scene);
        Selection.activeGameObject = city;
        Debug.Log("City 3x3 generada.");
    }

    #region Calles

    private static void BuildRoads(Transform parent)
    {
        GameObject lane = LoadPrefab("Floor/Street 2 Prefab");       // 4x4, yellow line on +Z
        GameObject crosswalk = LoadPrefab("Floor/Street 8 Prefab");  // 4x4, crosswalk
        GameObject crossing = LoadPrefab("Floor/Street 4 8M prefab"); // 8x8, plain

        for (int i = 0; i <= Blocks; i++)
        {
            float roadCenter = -HalfExtent + RoadWidth / 2f + i * Pitch;

            // Intersections
            for (int j = 0; j <= Blocks; j++)
            {
                float other = -HalfExtent + RoadWidth / 2f + j * Pitch;
                Place(crossing, parent, new Vector3(roadCenter, 0f, other), 0f);
            }

            // Segments between intersections
            for (int s = 0; s < Blocks; s++)
            {
                float start = -HalfExtent + RoadWidth + s * Pitch;
                int tiles = Mathf.RoundToInt(BlockSize / 4f);
                for (int t = 0; t < tiles; t++)
                {
                    float along = start + 2f + t * 4f;
                    bool isEnd = t == 0 || t == tiles - 1;

                    // Street along X (centered at z = roadCenter)
                    if (isEnd)
                    {
                        Place(crosswalk, parent, new Vector3(along, 0f, roadCenter - 2f), 90f);
                        Place(crosswalk, parent, new Vector3(along, 0f, roadCenter + 2f), 90f);
                    }
                    else
                    {
                        Place(lane, parent, new Vector3(along, 0f, roadCenter - 2f), 0f);
                        Place(lane, parent, new Vector3(along, 0f, roadCenter + 2f), 180f);
                    }

                    // Street along Z (centered at x = roadCenter)
                    if (isEnd)
                    {
                        Place(crosswalk, parent, new Vector3(roadCenter - 2f, 0f, along), 0f);
                        Place(crosswalk, parent, new Vector3(roadCenter + 2f, 0f, along), 0f);
                    }
                    else
                    {
                        Place(lane, parent, new Vector3(roadCenter - 2f, 0f, along), 90f);
                        Place(lane, parent, new Vector3(roadCenter + 2f, 0f, along), 270f);
                    }
                }
            }
        }

        foreach (Transform child in parent)
        {
            StripColliders(child.gameObject);
            MakeStatic(child.gameObject, occluder: false);
            DisableShadowCasting(child.gameObject);
        }
    }

    #endregion

    #region Manzanas

    private static Vector3 BlockCenter(int bx, int bz)
    {
        float start = -HalfExtent + RoadWidth + BlockSize / 2f;
        return new Vector3(start + bx * Pitch, 0f, start + bz * Pitch);
    }

    private static void BuildBlockBase(Transform parent, Vector3 center, Material material, string name)
    {
        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = name;
        slab.transform.SetParent(parent, false);
        slab.transform.position = center + Vector3.up * (SidewalkHeight / 2f);
        slab.transform.localScale = new Vector3(BlockSize, SidewalkHeight, BlockSize);
        slab.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(slab.GetComponent<Collider>());
        MakeStatic(slab, occluder: false);
        DisableShadowCasting(slab);
    }

    /// <summary>Fills one side of the block with buildings whose facade faces <paramref name="facing"/>.</summary>
    private static void BuildRow(Transform parent, string[] pool, Vector3 blockCenter, Vector3 facing)
    {
        float rowLength = BlockSize - 2f * Setback + 2f; // allows a 1 m margin at the corners
        float maxDepth = BlockSize / 2f - Setback + 1f;

        var candidates = new List<Candidate>();
        foreach (string name in pool)
        {
            GameObject prefab = LoadPrefab("Buildings/" + name);
            if (prefab == null) continue;
            Bounds b = PrefabBounds(prefab);
            // Width along the row = local X, depth = local Z (facade on local ±Z)
            if (b.size.z > maxDepth) continue;
            candidates.Add(new Candidate { Prefab = prefab, Width = b.size.x, Depth = b.size.z });
        }

        var chosen = new List<Candidate>();
        float used = 0f;
        for (int guard = 0; guard < 20; guard++)
        {
            float remaining = rowLength - used;
            List<Candidate> fits = candidates.FindAll(c => c.Width <= remaining);
            if (fits.Count == 0) break;
            Candidate pick = fits[Random.Range(0, fits.Count)];
            chosen.Add(pick);
            used += pick.Width;
        }
        if (chosen.Count == 0) return;

        float gap = (rowLength - used) / (chosen.Count + 1);
        Vector3 along = Vector3.Cross(Vector3.up, facing); // row axis
        Vector3 frontEdge = blockCenter + facing * (BlockSize / 2f - Setback);
        float cursor = -rowLength / 2f + gap;

        foreach (Candidate c in chosen)
        {
            float yaw = Quaternion.LookRotation(facing).eulerAngles.y;
            if (FacesNegativeZ.Contains(c.Prefab.name)) yaw += 180f;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(c.Prefab, parent);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = Vector3.zero;

            Bounds world = RendererBounds(go);
            Vector3 target = frontEdge + along * (cursor + c.Width / 2f) - facing * (c.Depth / 2f);
            Vector3 offset = new Vector3(target.x - world.center.x, SidewalkHeight - world.min.y, target.z - world.center.z);
            go.transform.position += offset;

            ReplaceWithBoxCollider(go);
            go.AddComponent<CrashHazard>(); // hitting a building = defeat
            MakeStatic(go, occluder: true);
            cursor += c.Width + gap;
        }
    }

    private static void BuildSidewalkProps(Transform parent, Vector3 center)
    {
        GameObject lamp = LoadPrefab("Lamps/street_lamp 1 prefab"); // arm toward local -Z
        GameObject tree = LoadPrefab("Props/Tree prefab");
        GameObject bench = LoadPrefab("Props/bench prefab");
        GameObject hydrant = LoadPrefab("Props/Hydrant prefab");
        GameObject bin = LoadPrefab("Props/trashcan prefab");

        Vector3[] sides = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        foreach (Vector3 facing in sides)
        {
            Vector3 along = Vector3.Cross(Vector3.up, facing);
            Vector3 edge = center + facing * (BlockSize / 2f - 1f) + Vector3.up * SidewalkHeight;
            float facingYaw = Quaternion.LookRotation(facing).eulerAngles.y;

            int count = Mathf.FloorToInt((BlockSize - 6f) / LampSpacing);
            for (int i = 0; i <= count; i++)
            {
                float t = -((count * LampSpacing) / 2f) + i * LampSpacing;
                // Lamp arm pointing at the street
                Place(lamp, parent, edge + along * t, facingYaw + 180f);

                if (i < count)
                {
                    Vector3 mid = edge + along * (t + LampSpacing / 2f);
                    float roll = Random.value;
                    if (roll < 0.55f) Place(tree, parent, mid, Random.Range(0f, 360f));
                    else if (roll < 0.75f) Place(bench, parent, mid - facing * 0.5f, facingYaw + 180f);
                    else if (roll < 0.88f) Place(bin, parent, mid, facingYaw);
                    else Place(hydrant, parent, mid, facingYaw);
                }
            }
        }

        foreach (Transform child in parent)
        {
            StripColliders(child.gameObject);
            MakeStatic(child.gameObject, occluder: false);
            // Only trees cast shadows; the other props are too small
            if (!child.name.StartsWith("Tree")) DisableShadowCasting(child.gameObject);
        }
    }

    #endregion

    #region Bounds, ground, player and ambience

    private static void BuildBoundaryWalls(Transform parent)
    {
        float edge = HalfExtent + 2f;
        float length = edge * 2f;
        CreateWall(parent, "Wall_N", new Vector3(0f, WallHeight / 2f, edge), new Vector3(length, WallHeight, 1f));
        CreateWall(parent, "Wall_S", new Vector3(0f, WallHeight / 2f, -edge), new Vector3(length, WallHeight, 1f));
        CreateWall(parent, "Wall_E", new Vector3(edge, WallHeight / 2f, 0f), new Vector3(1f, WallHeight, length));
        CreateWall(parent, "Wall_W", new Vector3(-edge, WallHeight / 2f, 0f), new Vector3(1f, WallHeight, length));
        // Invisible ceiling above the drone's maximum altitude
        CreateWall(parent, "Ceiling", new Vector3(0f, WallHeight, 0f), new Vector3(length, 1f, length));
    }

    private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 size)
    {
        var wall = new GameObject(name);
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        var box = wall.AddComponent<BoxCollider>();
        box.size = size;
        GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic);
    }

    private static void ConfigureGround()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null) return;

        Undo.RecordObject(ground.transform, "Generate City");
        ground.transform.position = Vector3.zero;
        // 1 x 1 km plane: reaches past the camera's far plane, where the fog fully covers it
        ground.transform.localScale = new Vector3(100f, 1f, 100f);

        Material asphalt = GetOrCreateMaterial("City_Ground", new Color(0.18f, 0.18f, 0.17f), null, Vector2.one);
        var renderer = ground.GetComponent<MeshRenderer>();
        Undo.RecordObject(renderer, "Generate City");
        renderer.sharedMaterial = asphalt;
        MakeStatic(ground, occluder: false);
    }

    private static void PlacePlayer()
    {
        GameObject player = GameObject.Find("PlayerBuster1");
        if (player == null) return;

        // Southeast intersection of the central block, looking north along the avenue.
        // The camera sits behind, over the street, with no lamps or trees blocking it.
        float road = -HalfExtent + RoadWidth / 2f + 2f * Pitch;
        float roadZ = -HalfExtent + RoadWidth / 2f + Pitch;
        Undo.RecordObject(player.transform, "Generate City");
        player.transform.SetPositionAndRotation(new Vector3(road, 2f, roadZ), Quaternion.identity);
    }

    private static void ConfigureAtmosphere()
    {
        // Smog fog: adds atmosphere and hides distant things (mobile performance).
        // Its color matches Mat_Sky's horizon (tint computed for that), so there is no seam.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.76f, 0.72f, 0.62f);
        RenderSettings.fogDensity = 0.009f;

        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/VerdantZero/Materials/Mat_Sky.mat");
        if (sky == null) sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/POLYGON city pack/Materials/Day.mat");
        if (sky != null) RenderSettings.skybox = sky;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.657f, 0.633f, 0.559f);
        RenderSettings.ambientEquatorColor = new Color(0.608f, 0.576f, 0.496f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.22f, 0.19f);

        Light sun = Object.FindAnyObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            Undo.RecordObject(sun, "Generate City");
            Undo.RecordObject(sun.transform, "Generate City");
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            // Overcast sky: slightly softer light and less harsh shadows
            sun.color = new Color(1f, 0.92f, 0.8f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam, "Generate City");
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;
        }
    }

    #endregion

    #region Utilidades

    private static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject LoadPrefab(string relativePath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + relativePath + ".prefab");
        if (prefab == null) Debug.LogWarning($"Prefab no encontrado: {relativePath}");
        return prefab;
    }

    private static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw)
    {
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return go;
    }

    private static Bounds PrefabBounds(GameObject prefab)
    {
        var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Bounds b = RendererBounds(temp);
        Object.DestroyImmediate(temp);
        return b;
    }

    private static Bounds RendererBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    private static void StripColliders(GameObject go)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(c);
        }
    }

    /// <summary>Package MeshColliders -> one BoxCollider per building (cheaper for the drone).</summary>
    private static void ReplaceWithBoxCollider(GameObject go)
    {
        StripColliders(go);
        Bounds world = RendererBounds(go);
        var box = go.AddComponent<BoxCollider>();
        box.center = go.transform.InverseTransformPoint(world.center);
        Vector3 size = go.transform.InverseTransformVector(world.size);
        box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
    }

    private static void MakeStatic(GameObject go, bool occluder)
    {
        StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic
                                  | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic;
        if (occluder) flags |= StaticEditorFlags.OccluderStatic;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }
    }

    private static void DisableShadowCasting(GameObject go)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private static Material GetSidewalkMaterial()
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/POLYGON city pack/Materials/Material_meshs/Sideway 2x2m.mat");
        Texture tex = source != null ? source.GetTexture("_BaseMap") : null;
        Color color = source != null ? source.GetColor("_BaseColor") : new Color(0.7f, 0.7f, 0.7f);
        // 40 m cube with 2 m tiles -> tiling 20
        return GetOrCreateMaterial("City_Sidewalk", color, tex, new Vector2(BlockSize / 2f, BlockSize / 2f));
    }

    private static Material GetOrCreateMaterial(string name, Color color, Texture texture, Vector2 tiling)
    {
        string path = MaterialsDir + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetTexture("_BaseMap", texture);
        mat.SetTextureScale("_BaseMap", tiling);
        mat.SetFloat("_Smoothness", 0.1f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    #endregion
}
