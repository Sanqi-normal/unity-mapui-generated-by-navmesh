using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 从 NavMesh 生成每层楼的地图纹理
/// 支持编辑器中预烘焙保存纹理
/// </summary>
public class NavMeshMapBaker : MonoBehaviour
{
    [Header("纹理设置")]
    public int textureSize = 1024;
    public float worldSize = 80f;

    [Header("楼层聚类")]
    public float floorClusterThreshold = 1.5f;

    [Header("边缘简化")]
    public float simplifyTolerance = 0.3f;
    public float angleSnapDegrees = 10f;

    [Header("过滤")]
    public float minIslandArea = 0.5f;

    [Header("绘制颜色")]
    public Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);
    public Color floorColor = new Color(0.2f, 0.22f, 0.28f, 1f);
    public Color wallColor = new Color(0.7f, 0.7f, 0.75f, 1f);
    public Color wallLineColor = new Color(0.7f, 0.7f, 0.75f, 1f);
    public Color stairColor = new Color(0.5f, 0.45f, 0.55f, 1f);
    public Color stairLineColor = new Color(0.65f, 0.6f, 0.7f, 1f);

    [Header("墙线")]
    public int wallThickness = 2;

    [Header("楼梯线")]
    public float stairLineSpacing = 0.3f;

    [Header("保存路径(相对Assets)")]
    public string saveFolderPath = "MinimapTextures";

    [Header("已烘焙数据(运行时使用)")]
    public List<FloorData> floors = new List<FloorData>();
    public Vector3 mapOrigin;

    [System.Serializable]
    public class FloorData
    {
        public int index;
        public float baseY;
        public float minY, maxY;
        public Texture2D texture; // 序列化引用,编辑器中保存为资产
    }

    // 内部用
    private int layerAreaIndex;
    private int stairAreaIndex;

    struct TriData
    {
        public Vector3 v0, v1, v2;
        public int area;
        public float centroidY;
    }

    /// <summary>
    /// 运行时检查: 有已烘焙纹理就直接用
    /// </summary>
    public bool HasBakedData()
    {
        return floors != null && floors.Count > 0 && floors[0].texture != null;
    }

    /// <summary>
    /// 运行时按需烘焙(程序化生成地图时用)
    /// </summary>
    public void BakeRuntime()
    {
        BakeInternal(false);
    }

    /// <summary>
    /// 烘焙核心逻辑
    /// </summary>
    private void BakeInternal(bool saveAsAsset)
    {
        layerAreaIndex = NavMesh.GetAreaFromName("layer");
        stairAreaIndex = NavMesh.GetAreaFromName("stair");

        if (layerAreaIndex == -1 || stairAreaIndex == -1)
        {
            Debug.LogError("NavMesh Area 'layer' 或 'stair' 未定义");
            return;
        }

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.vertices.Length == 0)
        {
            Debug.LogError("NavMesh 无数据，请先烘焙 NavMesh");
            return;
        }

        List<TriData> allTris = new List<TriData>();
        for (int i = 0; i < tri.indices.Length; i += 3)
        {
            Vector3 v0 = tri.vertices[tri.indices[i]];
            Vector3 v1 = tri.vertices[tri.indices[i + 1]];
            Vector3 v2 = tri.vertices[tri.indices[i + 2]];

            allTris.Add(new TriData
            {
                v0 = v0,
                v1 = v1,
                v2 = v2,
                area = tri.areas[i / 3],
                centroidY = (v0.y + v1.y + v2.y) / 3f
            });
        }

        // 地图原点
        Vector3 center = Vector3.zero;
        foreach (var v in tri.vertices) center += v;
        center /= tri.vertices.Length;
        mapOrigin = new Vector3(center.x - worldSize * 0.5f, 0, center.z - worldSize * 0.5f);

        // 分离
        List<TriData> layerTris = allTris.Where(t => t.area == layerAreaIndex).ToList();
        List<TriData> stairTris = allTris.Where(t => t.area == stairAreaIndex).ToList();
        List<List<TriData>> floorClusters = ClusterByY(layerTris);

        // 清理旧数据
        floors.Clear();

        for (int i = 0; i < floorClusters.Count; i++)
        {
            var cluster = floorClusters[i];
            float minY = cluster.Min(t => Mathf.Min(t.v0.y, Mathf.Min(t.v1.y, t.v2.y)));
            float maxY = cluster.Max(t => Mathf.Max(t.v0.y, Mathf.Max(t.v1.y, t.v2.y)));
            float baseY = cluster.Average(t => t.centroidY);

            List<TriData> floorStairs = stairTris.Where(s =>
            {
                float sMinY = Mathf.Min(s.v0.y, Mathf.Min(s.v1.y, s.v2.y));
                float sMaxY = Mathf.Max(s.v0.y, Mathf.Max(s.v1.y, s.v2.y));
                return sMaxY >= minY - floorClusterThreshold &&
                       sMinY <= maxY + floorClusterThreshold;
            }).ToList();

            Texture2D tex = GenerateFloorTexture(cluster, floorStairs);
            tex.name = $"Minimap_Floor{i}";

#if UNITY_EDITOR
            if (saveAsAsset)
            {
                tex = SaveTextureAsAsset(tex, i);
            }
#endif

            floors.Add(new FloorData
            {
                index = i,
                baseY = baseY,
                minY = minY - floorClusterThreshold * 0.5f,
                maxY = maxY + floorClusterThreshold * 0.5f,
                texture = tex
            });
        }

        floors.Sort((a, b) => a.baseY.CompareTo(b.baseY));
        for (int i = 0; i < floors.Count; i++) floors[i].index = i;

#if UNITY_EDITOR
        if (saveAsAsset)
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
#endif

        Debug.Log($"NavMeshMapBaker: 生成了 {floors.Count} 层地图纹理");
    }

    // ============================================================
    //  编辑器保存纹理为资产
    // ============================================================

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器中调用: 烘焙并保存纹理到项目
    /// </summary>
    public void BakeAndSave()
    {
        BakeInternal(true);
    }

    Texture2D SaveTextureAsAsset(Texture2D tex, int floorIndex)
    {
        string folderPath = $"Assets/{saveFolderPath}";

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            // 逐级创建文件夹
            string[] parts = saveFolderPath.Split('/');
            string current = "Assets";
            foreach (string part in parts)
            {
                string next = $"{current}/{part}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }

        string sceneName = gameObject.scene.name;
        string assetPath = $"{folderPath}/{sceneName}_Floor{floorIndex}.png";

        // 写入PNG
        byte[] pngData = tex.EncodeToPNG();
        System.IO.File.WriteAllBytes(assetPath, pngData);
        AssetDatabase.ImportAsset(assetPath);

        // 设置导入设置
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // 返回资产引用(而非内存中的临时纹理)
        Texture2D saved = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        return saved;
    }
#endif

    // ============================================================
    //  以下为原有的生成逻辑(不变)
    // ============================================================

    List<List<TriData>> ClusterByY(List<TriData> tris)
    {
        if (tris.Count == 0) return new List<List<TriData>>();
        tris.Sort((a, b) => a.centroidY.CompareTo(b.centroidY));

        var clusters = new List<List<TriData>>();
        var current = new List<TriData> { tris[0] };
        float clusterY = tris[0].centroidY;

        for (int i = 1; i < tris.Count; i++)
        {
            if (Mathf.Abs(tris[i].centroidY - clusterY) < floorClusterThreshold)
            {
                current.Add(tris[i]);
                clusterY = current.Average(t => t.centroidY);
            }
            else
            {
                clusters.Add(current);
                current = new List<TriData> { tris[i] };
                clusterY = tris[i].centroidY;
            }
        }
        clusters.Add(current);
        return clusters;
    }

    Texture2D GenerateFloorTexture(List<TriData> floorTris, List<TriData> stairTris)
    {
        var tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[textureSize * textureSize];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = backgroundColor;

        floorTris = FilterSmallIslands(floorTris);

        foreach (var t in floorTris)
            FillTriangle(pixels, WorldToPixel(t.v0), WorldToPixel(t.v1), WorldToPixel(t.v2), floorColor);

        foreach (var t in stairTris)
            FillTriangle(pixels, WorldToPixel(t.v0), WorldToPixel(t.v1), WorldToPixel(t.v2), stairColor);

        //DrawStairLines(pixels, stairTris);

        var floorEdges = ExtractOuterEdges(floorTris);
        var stairEdges = ExtractOuterEdges(stairTris);
        floorEdges = SimplifyEdges(floorEdges);
        stairEdges = SimplifyEdges(stairEdges);

        foreach (var edge in floorEdges)
            DrawLine(pixels, edge.a, edge.b, wallLineColor, wallThickness);
        foreach (var edge in stairEdges)
            DrawLine(pixels, edge.a, edge.b, wallColor, Mathf.Max(1, wallThickness - 1));

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    List<TriData> FilterSmallIslands(List<TriData> tris)
    {
        var vertToTris = new Dictionary<long, List<int>>();
        for (int i = 0; i < tris.Count; i++)
        {
            foreach (var v in new[] { tris[i].v0, tris[i].v1, tris[i].v2 })
            {
                long key = HashVertex(v);
                if (!vertToTris.ContainsKey(key))
                    vertToTris[key] = new List<int>();
                vertToTris[key].Add(i);
            }
        }

        bool[] visited = new bool[tris.Count];
        var result = new List<TriData>();

        for (int i = 0; i < tris.Count; i++)
        {
            if (visited[i]) continue;

            var component = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(i);
            visited[i] = true;

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                component.Add(idx);
                foreach (var v in new[] { tris[idx].v0, tris[idx].v1, tris[idx].v2 })
                {
                    long key = HashVertex(v);
                    if (!vertToTris.ContainsKey(key)) continue;
                    foreach (int neighbor in vertToTris[key])
                    {
                        if (!visited[neighbor])
                        {
                            visited[neighbor] = true;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            float area = 0;
            foreach (int idx in component)
                area += TriangleArea(tris[idx]);

            if (area >= minIslandArea)
            {
                foreach (int idx in component)
                    result.Add(tris[idx]);
            }
        }
        return result;
    }

    float TriangleArea(TriData t)
    {
        return Vector3.Cross(t.v1 - t.v0, t.v2 - t.v0).magnitude * 0.5f;
    }

    long HashVertex(Vector3 v)
    {
        int x = Mathf.RoundToInt(v.x * 100);
        int y = Mathf.RoundToInt(v.y * 100);
        int z = Mathf.RoundToInt(v.z * 100);
        return ((long)x * 73856093L) ^ ((long)y * 19349663L) ^ ((long)z * 83492791L);
    }

    // ---- 边缘 ----

    struct Edge2D
    {
        public Vector2Int a, b;
        public Vector3 worldA, worldB;
    }

    List<Edge2D> ExtractOuterEdges(List<TriData> tris)
    {
        var edgeCount = new Dictionary<long, (int count, Vector3 va, Vector3 vb)>();
        foreach (var t in tris)
        {
            AddEdge(edgeCount, t.v0, t.v1);
            AddEdge(edgeCount, t.v1, t.v2);
            AddEdge(edgeCount, t.v2, t.v0);
        }

        var outerEdges = new List<Edge2D>();
        foreach (var kvp in edgeCount)
        {
            if (kvp.Value.count == 1)
            {
                outerEdges.Add(new Edge2D
                {
                    a = WorldToPixel(kvp.Value.va),
                    b = WorldToPixel(kvp.Value.vb),
                    worldA = kvp.Value.va,
                    worldB = kvp.Value.vb
                });
            }
        }
        return outerEdges;
    }

    void AddEdge(Dictionary<long, (int, Vector3, Vector3)> dict, Vector3 va, Vector3 vb)
    {
        long ha = HashVertex(va);
        long hb = HashVertex(vb);
        long key = ha < hb ? ha * 1000003L + hb : hb * 1000003L + ha;

        if (dict.TryGetValue(key, out var val))
            dict[key] = (val.Item1 + 1, val.Item2, val.Item3);
        else
            dict[key] = (1, va, vb);
    }

    List<Edge2D> SimplifyEdges(List<Edge2D> edges)
    {
        var chains = BuildEdgeChains(edges);
        var result = new List<Edge2D>();

        foreach (var chain in chains)
        {
            var simplified = DouglasPeucker(chain, simplifyTolerance);
            simplified = SnapAngles(simplified);

            for (int i = 0; i < simplified.Count - 1; i++)
            {
                result.Add(new Edge2D
                {
                    a = WorldToPixel(simplified[i]),
                    b = WorldToPixel(simplified[i + 1]),
                    worldA = simplified[i],
                    worldB = simplified[i + 1]
                });
            }
        }
        return result;
    }

    List<List<Vector3>> BuildEdgeChains(List<Edge2D> edges)
    {
        var hashToPos = new Dictionary<long, Vector3>();
        var adj = new Dictionary<long, List<long>>();

        foreach (var e in edges)
        {
            long ha = HashVertex(e.worldA);
            long hb = HashVertex(e.worldB);
            hashToPos[ha] = e.worldA;
            hashToPos[hb] = e.worldB;

            if (!adj.ContainsKey(ha)) adj[ha] = new List<long>();
            if (!adj.ContainsKey(hb)) adj[hb] = new List<long>();
            adj[ha].Add(hb);
            adj[hb].Add(ha);
        }

        var visited = new HashSet<long>();
        var chains = new List<List<Vector3>>();

        foreach (var startKey in adj.Keys)
        {
            if (visited.Contains(startKey)) continue;

            var chain = new List<Vector3>();
            long current = startKey;

            while (!visited.Contains(current))
            {
                visited.Add(current);
                chain.Add(hashToPos[current]);

                bool found = false;
                foreach (long next in adj[current])
                {
                    if (!visited.Contains(next))
                    {
                        current = next;
                        found = true;
                        break;
                    }
                }
                if (!found) break;
            }

            if (chain.Count >= 2)
                chains.Add(chain);
        }
        return chains;
    }

    List<Vector3> DouglasPeucker(List<Vector3> points, float tolerance)
    {
        if (points.Count <= 2) return new List<Vector3>(points);

        float maxDist = 0;
        int maxIdx = 0;

        for (int i = 1; i < points.Count - 1; i++)
        {
            float dist = DistToLine2D(points[i], points[0], points[points.Count - 1]);
            if (dist > maxDist) { maxDist = dist; maxIdx = i; }
        }

        if (maxDist > tolerance)
        {
            var left = DouglasPeucker(points.GetRange(0, maxIdx + 1), tolerance);
            var right = DouglasPeucker(points.GetRange(maxIdx, points.Count - maxIdx), tolerance);
            left.RemoveAt(left.Count - 1);
            left.AddRange(right);
            return left;
        }

        return new List<Vector3> { points[0], points[points.Count - 1] };
    }

    float DistToLine2D(Vector3 point, Vector3 lineA, Vector3 lineB)
    {
        Vector2 p = new Vector2(point.x, point.z);
        Vector2 a = new Vector2(lineA.x, lineA.z);
        Vector2 b = new Vector2(lineB.x, lineB.z);
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }

    List<Vector3> SnapAngles(List<Vector3> points)
    {
        if (points.Count <= 2) return points;
        var result = new List<Vector3> { points[0] };

        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector2 dirIn = new Vector2(points[i].x - points[i - 1].x, points[i].z - points[i - 1].z);
            Vector2 dirOut = new Vector2(points[i + 1].x - points[i].x, points[i + 1].z - points[i].z);

            float aIn = SnapAngle(Mathf.Atan2(dirIn.y, dirIn.x) * Mathf.Rad2Deg);
            float aOut = SnapAngle(Mathf.Atan2(dirOut.y, dirOut.x) * Mathf.Rad2Deg);

            if (Mathf.Abs(Mathf.DeltaAngle(aIn, aOut)) >= 1f)
                result.Add(points[i]);
        }

        result.Add(points[points.Count - 1]);
        return result;
    }

    float SnapAngle(float deg)
    {
        float[] snaps = { 0, 45, 90, 135, 180, -180, -135, -90, -45 };
        float best = deg;
        float bestDiff = float.MaxValue;
        foreach (float s in snaps)
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(deg, s));
            if (diff < bestDiff && diff < angleSnapDegrees)
            { bestDiff = diff; best = s; }
        }
        return best;
    }

    // ---- 楼梯线 ----
    // 表现不好，已注释掉它的活动
    void DrawStairLines(Color[] pixels, List<TriData> stairTris)
    {
        if (stairTris.Count == 0) return;

        Vector3 lowest = stairTris[0].v0, highest = stairTris[0].v0;
        foreach (var t in stairTris)
            foreach (var v in new[] { t.v0, t.v1, t.v2 })
            {
                if (v.y < lowest.y) lowest = v;
                if (v.y > highest.y) highest = v;
            }

        Vector2 stairDir = new Vector2(highest.x - lowest.x, highest.z - lowest.z);
        if (stairDir.sqrMagnitude < 0.01f) return;
        stairDir.Normalize();
        Vector2 lineDir = new Vector2(-stairDir.y, stairDir.x);

        float projMin = float.MaxValue, projMax = float.MinValue;
        foreach (var t in stairTris)
            foreach (var v in new[] { t.v0, t.v1, t.v2 })
            {
                float proj = v.x * stairDir.x + v.z * stairDir.y;
                projMin = Mathf.Min(projMin, proj);
                projMax = Mathf.Max(projMax, proj);
            }

        for (float d = projMin; d <= projMax; d += stairLineSpacing)
        {
            Vector2 center = new Vector2(lowest.x, lowest.z) + stairDir * (d - projMin);
            float halfLen = 0.5f;
            Vector3 lineA = new Vector3(center.x - lineDir.x * halfLen, 0, center.y - lineDir.y * halfLen);
            Vector3 lineB = new Vector3(center.x + lineDir.x * halfLen, 0, center.y + lineDir.y * halfLen);
            DrawLine(pixels, WorldToPixel(lineA), WorldToPixel(lineB), stairLineColor, 1);
        }
    }

    // ---- 绘制工具 ----

    Vector2Int WorldToPixel(Vector3 world)
    {
        float nx = (world.x - mapOrigin.x) / worldSize;
        float ny = (world.z - mapOrigin.z) / worldSize;
        return new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(nx * textureSize), 0, textureSize - 1),
            Mathf.Clamp(Mathf.RoundToInt(ny * textureSize), 0, textureSize - 1)
        );
    }

    public Vector3 PixelToWorld(Vector2Int pixel, float y = 0)
    {
        float x = ((float)pixel.x / textureSize) * worldSize + mapOrigin.x;
        float z = ((float)pixel.y / textureSize) * worldSize + mapOrigin.z;
        return new Vector3(x, y, z);
    }

    void FillTriangle(Color[] pixels, Vector2Int a, Vector2Int b, Vector2Int c, Color color)
    {
        if (a.y > b.y) { var t = a; a = b; b = t; }
        if (a.y > c.y) { var t = a; a = c; c = t; }
        if (b.y > c.y) { var t = b; b = c; c = t; }

        for (int y = Mathf.Max(a.y, 0); y <= Mathf.Min(c.y, textureSize - 1); y++)
        {
            bool secondHalf = y >= b.y;
            int segH = secondHalf ? (c.y - b.y) : (b.y - a.y);
            if (segH == 0) segH = 1;
            int totalH = c.y - a.y;
            if (totalH == 0) totalH = 1;

            float alpha = (float)(y - a.y) / totalH;
            float beta = (float)(y - (secondHalf ? b.y : a.y)) / segH;

            int xA = Mathf.RoundToInt(a.x + (c.x - a.x) * alpha);
            int xB = secondHalf
                ? Mathf.RoundToInt(b.x + (c.x - b.x) * beta)
                : Mathf.RoundToInt(a.x + (b.x - a.x) * beta);

            if (xA > xB) { int t = xA; xA = xB; xB = t; }
            xA = Mathf.Max(xA, 0);
            xB = Mathf.Min(xB, textureSize - 1);

            for (int x = xA; x <= xB; x++)
                pixels[y * textureSize + x] = color;
        }
    }

    void DrawLine(Color[] pixels, Vector2Int from, Vector2Int to, Color color, int thickness)
    {
        int dx = Mathf.Abs(to.x - from.x);
        int dy = Mathf.Abs(to.y - from.y);
        int sx = from.x < to.x ? 1 : -1;
        int sy = from.y < to.y ? 1 : -1;
        int err = dx - dy;
        int x = from.x, y = from.y;
        int half = thickness / 2;

        while (true)
        {
            for (int tx = -half; tx <= half; tx++)
                for (int ty = -half; ty <= half; ty++)
                {
                    int px = x + tx, py = y + ty;
                    if (px >= 0 && px < textureSize && py >= 0 && py < textureSize)
                        pixels[py * textureSize + px] = color;
                }
            if (x == to.x && y == to.y) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }
}