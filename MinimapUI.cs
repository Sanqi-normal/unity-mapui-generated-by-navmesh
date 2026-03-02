using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 小地图UI运行时控制
/// </summary>
public class MinimapUI : MonoBehaviour
{
    [Header("引用")]
    public NavMeshMapBaker baker;
    public Transform playerTransform;

    [Header("小地图 UI")]
    public RawImage smallMapImage;
    public RectTransform smallMapContainer;
    public RectTransform smallRotatingContainer;
    public RectTransform smallIconContainer;

    [Header("大地图 UI")]
    public RectTransform largeMapContainer;
    public RawImage largeMapImage;
    public RectTransform largeIconContainer;
    public float largeMapDisplayRange = 50f;

    [Header("显示")]
    public float displayRange = 20f;
    public KeyCode largeMapKey = KeyCode.Q;

    [Header("大地图模式(运行时烘焙NavMesh时选)")]
    public bool runtimeBakeMode = false;

    private int currentFloorIndex = -1;
    private NavMeshMapBaker.FloorData currentFloor;
    private List<MinimapIcon> registeredIcons = new List<MinimapIcon>();

    // 当前是否处于大地图模式，供外部(MinimapOverlay等)查询
    public bool IsLargeMapActive { get; private set; }

    void Start()
    {
        if (runtimeBakeMode)
        {
            baker.BakeRuntime();
        }
        else if (!baker.HasBakedData())
        {
            Debug.LogError("MinimapUI: 没有已烘焙的地图数据。请在编辑器中'烘焙地图数据'或勾选runtimeBakeMode");
            enabled = false;
            return;
        }

        currentFloorIndex = -1;

        if (largeMapContainer != null)
            largeMapContainer.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
        if (playerTransform == null || baker.floors.Count == 0) return;

        UpdateFloor();
        if (currentFloor == null) return;

        IsLargeMapActive = Input.GetKey(largeMapKey);

        if (largeMapImage != null)
        {
            largeMapContainer.gameObject.SetActive(IsLargeMapActive);
            smallMapContainer.gameObject.SetActive(!IsLargeMapActive);
        }

        if (IsLargeMapActive)
        {
            UpdateLargeMap();
            // 大地图显示时隐藏所有小地图图标，避免叠影
            HideAllSmallIcons();
        }
        else
        {
            UpdateSmallMap();
            // 小地图显示时隐藏所有大地图图标
            HideAllLargeIcons();
        }
    }

    void UpdateSmallMap()
    {
        UpdateMapPosition(smallMapImage, displayRange);
        UpdateMapRotation();
        UpdateSmallIcons(displayRange);
    }

    void UpdateLargeMap()
    {
        UpdateMapPosition(largeMapImage, largeMapDisplayRange);
        UpdateLargeIcons(largeMapDisplayRange);
    }

    void UpdateFloor()
    {
        float playerY = playerTransform.position.y;
        int bestFloor = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < baker.floors.Count; i++)
        {
            float dist = Mathf.Abs(playerY - baker.floors[i].baseY);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestFloor = i;
            }
        }

        if (bestFloor != currentFloorIndex)
        {
            currentFloorIndex = bestFloor;
            currentFloor = baker.floors[currentFloorIndex];
            if (smallMapImage != null) smallMapImage.texture = currentFloor.texture;
            if (largeMapImage != null) largeMapImage.texture = currentFloor.texture;
        }
    }

    /// <summary>根据世界坐标Y值计算所在楼层索引</summary>
    int GetIconFloor(Vector3 iconPos)
    {
        float iconY = iconPos.y;
        int bestFloor = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < baker.floors.Count; i++)
        {
            float dist = Mathf.Abs(iconY - baker.floors[i].baseY);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestFloor = i;
            }
        }
        return bestFloor;
    }

    void UpdateMapPosition(RawImage mapImg, float range)
    {
        Vector3 pos = playerTransform.position;
        float u = (pos.x - baker.mapOrigin.x) / baker.worldSize;
        float v = (pos.z - baker.mapOrigin.z) / baker.worldSize;
        float rangeRatio = (range * 2f) / baker.worldSize;

        mapImg.uvRect = new Rect(
            u - rangeRatio * 0.5f,
            v - rangeRatio * 0.5f,
            rangeRatio,
            rangeRatio
        );
    }

    void UpdateMapRotation()
    {
        float yaw = playerTransform.eulerAngles.y;
        smallRotatingContainer.localRotation = Quaternion.Euler(0, 0, yaw);
    }

    // ---- Icon管理 ----

    public void RegisterIcon(MinimapIcon icon)
    {
        if (!registeredIcons.Contains(icon))
            registeredIcons.Add(icon);
    }

    public void UnregisterIcon(MinimapIcon icon)
    {
        registeredIcons.Remove(icon);
    }

    /// <summary>更新小地图图标位置与可见性</summary>
    void UpdateSmallIcons(float range)
    {
        Vector3 playerPos = playerTransform.position;
        float mapUISize = smallMapImage.rectTransform.rect.width;
        float scale = mapUISize / (range * 2f);
        float mapRadius = mapUISize * 0.5f;

        for (int i = registeredIcons.Count - 1; i >= 0; i--)
        {
            var icon = registeredIcons[i];
            if (icon == null || icon.TrackedTransform == null)
            {
                registeredIcons.RemoveAt(i);
                continue;
            }

            Vector3 targetPos = icon.TrackedTransform.position;

            int iconFloor = GetIconFloor(targetPos);
            if (iconFloor != currentFloorIndex)
            {
                icon.SetSmallVisible(false);
                continue;
            }

            Vector2 offset = new Vector2(targetPos.x - playerPos.x, targetPos.z - playerPos.z);
            float distance = offset.magnitude;
            Vector2 uiPos = offset * scale;

            if (distance <= range)
            {
                icon.SetSmallUIPosition(uiPos);
                icon.SetSmallVisible(true);
                icon.SetSmallEdgeMode(false);
            }
            else if (icon.showOnEdge && distance <= icon.edgeClampDistance)
            {
                Vector2 clamped = uiPos.normalized * mapRadius * 0.9f;
                icon.SetSmallUIPosition(clamped);
                icon.SetSmallVisible(true);
                icon.SetSmallEdgeMode(true);
            }
            else
            {
                icon.SetSmallVisible(false);
            }
        }
    }

    /// <summary>更新大地图图标位置与可见性</summary>
    void UpdateLargeIcons(float range)
    {
        if (largeIconContainer == null) return;

        Vector3 playerPos = playerTransform.position;
        // 大地图 UI 尺寸以 largeMapImage 为准（可能与小地图不同）
        float mapUISize = largeMapImage != null
            ? largeMapImage.rectTransform.rect.width
            : smallMapImage.rectTransform.rect.width;
        float scale = mapUISize / (range * 2f);
        float mapRadius = mapUISize * 0.5f;

        for (int i = registeredIcons.Count - 1; i >= 0; i--)
        {
            var icon = registeredIcons[i];
            if (icon == null || icon.TrackedTransform == null)
            {
                registeredIcons.RemoveAt(i);
                continue;
            }

            Vector3 targetPos = icon.TrackedTransform.position;

            int iconFloor = GetIconFloor(targetPos);
            if (iconFloor != currentFloorIndex)
            {
                icon.SetLargeVisible(false);
                continue;
            }

            Vector2 offset = new Vector2(targetPos.x - playerPos.x, targetPos.z - playerPos.z);
            float distance = offset.magnitude;
            Vector2 uiPos = offset * scale;

            if (distance <= range)
            {
                icon.SetLargeUIPosition(uiPos);
                icon.SetLargeVisible(true);
                icon.SetLargeEdgeMode(false);
            }
            else if (icon.showOnEdge && distance <= icon.edgeClampDistance)
            {
                Vector2 clamped = uiPos.normalized * mapRadius * 0.9f;
                icon.SetLargeUIPosition(clamped);
                icon.SetLargeVisible(true);
                icon.SetLargeEdgeMode(true);
            }
            else
            {
                icon.SetLargeVisible(false);
            }
        }
    }

    void HideAllSmallIcons()
    {
        foreach (var icon in registeredIcons)
            if (icon != null) icon.SetSmallVisible(false);
    }

    void HideAllLargeIcons()
    {
        foreach (var icon in registeredIcons)
            if (icon != null) icon.SetLargeVisible(false);
    }

    // ---- 世界坐标转 UI 坐标 ----

    /// <summary>转换为小地图 UI 坐标（供 MinimapOverlay 使用）</summary>
    public Vector2 WorldToMapUI(Vector3 worldPos)
    {
        return WorldToMapUI(worldPos, displayRange, smallMapImage);
    }

    /// <summary>根据当前显示状态自动选择合适的坐标和 range</summary>
    public Vector2 WorldToMapUICurrent(Vector3 worldPos)
    {
        if (IsLargeMapActive && largeMapImage != null)
            return WorldToMapUI(worldPos, largeMapDisplayRange, largeMapImage);
        return WorldToMapUI(worldPos, displayRange, smallMapImage);
    }

    public Vector2 WorldToMapUI(Vector3 worldPos, float range)
    {
        return WorldToMapUI(worldPos, range, smallMapImage);
    }

    private Vector2 WorldToMapUI(Vector3 worldPos, float range, RawImage mapImg)
    {
        Vector3 playerPos = playerTransform.position;
        Vector2 offset = new Vector2(worldPos.x - playerPos.x, worldPos.z - playerPos.z);
        float mapUISize = mapImg.rectTransform.rect.width;
        float scale = mapUISize / (range * 2f);
        return offset * scale;
    }
}