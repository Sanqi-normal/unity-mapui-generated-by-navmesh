using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂到门/窗/可破坏墙上。
/// 在地图纹理和图标之间显示覆盖元素。
/// 同时支持小地图和大地图，各自拥有独立实例。
/// </summary>
public class MinimapOverlay : MonoBehaviour
{
    public enum OverlayType { Door, Window, BreachableWall }

    [Header("类型")]
    public OverlayType type;

    [Header("覆盖图层")]
    public RectTransform overlayPrefab;  // 用于盖颜色+缩放的预制体

    [Header("尺寸(世界单位)")]
    public float width = 1.2f;

    [Header("状态Sprite(可选)")]
    public Sprite closedSprite;
    public Sprite openSprite;
    public Sprite destroyedSprite;

    // ---- 运行时 ----
    private RectTransform smallOverlayInstance;    // 小地图实例
    private Image smallSymbolImage;

    private RectTransform largeOverlayInstance;    // 大地图实例
    private Image largeSymbolImage;

    private MinimapUI minimap;

    public enum State { Closed, Open, Destroyed }
    private State currentState = State.Closed;

    void Start()
    {
        minimap = FindObjectOfType<MinimapUI>();
        if (minimap == null || overlayPrefab == null)
        {
            enabled = false;
            return;
        }

        // 小地图实例：挂在 smallRotatingContainer 下（随地图旋转）
        smallOverlayInstance = Instantiate(overlayPrefab, minimap.smallRotatingContainer);
        smallOverlayInstance.SetSiblingIndex(1);
        smallSymbolImage = smallOverlayInstance.GetComponentInChildren<Image>();

        // 大地图实例：挂在 largeIconContainer 的父级下
        // 大地图一般不旋转，直接挂在 largeIconContainer 同级即可
        if (minimap.largeIconContainer != null)
        {
            largeOverlayInstance = Instantiate(overlayPrefab, minimap.largeIconContainer.parent);
            // 确保层级在地图图片之上、图标之下
            largeOverlayInstance.SetSiblingIndex(minimap.largeIconContainer.GetSiblingIndex());
            largeSymbolImage = largeOverlayInstance.GetComponentInChildren<Image>();
            largeOverlayInstance.gameObject.SetActive(false); // 初始隐藏
        }

        UpdateState(State.Closed);
    }

    void LateUpdate()
    {
        if (minimap == null) return;

        bool isLarge = minimap.IsLargeMapActive;

        // ---- 小地图 overlay ----
        if (smallOverlayInstance != null)
        {
            smallOverlayInstance.gameObject.SetActive(!isLarge);
            if (!isLarge)
            {
                UpdateOverlayTransform(
                    smallOverlayInstance,
                    minimap.smallMapImage,
                    minimap.displayRange,
                    rotateWithWorld: true   // 小地图随旋转容器，但 overlay 自身需要对齐建筑朝向
                );
            }
        }

        // ---- 大地图 overlay ----
        if (largeOverlayInstance != null)
        {
            largeOverlayInstance.gameObject.SetActive(isLarge);
            if (isLarge)
            {
                UpdateOverlayTransform(
                    largeOverlayInstance,
                    minimap.largeMapImage,
                    minimap.largeMapDisplayRange,
                    rotateWithWorld: false  // 大地图不旋转，需要额外补偿玩家旋转
                );
            }
        }
    }

    /// <summary>
    /// 更新 overlay 实例的位置、旋转、尺寸。
    /// rotateWithWorld=true 时，父容器已经随地图旋转，只需叠加建筑朝向。
    /// rotateWithWorld=false 时（大地图），父容器不旋转，需要用世界朝向直接设置。
    /// </summary>
    private void UpdateOverlayTransform(RectTransform inst, RawImage mapImg, float range, bool rotateWithWorld)
    {
        if (inst == null || mapImg == null) return;

        // 位置：世界坐标转 UI 偏移
        Vector2 uiPos = minimap.WorldToMapUI(transform.position, range);
        inst.anchoredPosition = uiPos;

        // 旋转
        if (rotateWithWorld)
        {
            // 父容器（smallRotatingContainer）已经有玩家朝向的旋转补偿
            // overlay 自身只叠加建筑的 Y 轴朝向
            float angle = -transform.eulerAngles.y;
            inst.localRotation = Quaternion.Euler(0, 0, angle);
        }
        else
        {
            // 大地图：父容器不旋转，需要用世界绝对朝向
            // 注意：大地图是"北朝上"视角，不随玩家旋转
            float angle = -transform.eulerAngles.y;
            inst.localRotation = Quaternion.Euler(0, 0, angle);
        }

        // 尺寸：按地图缩放比例计算像素宽度
        float mapUISize = mapImg.rectTransform.rect.width;
        float scale = mapUISize / (range * 2f);
        float pixelWidth = width * scale;
        inst.sizeDelta = new Vector2(pixelWidth, pixelWidth * 0.3f);
    }

    /// <summary>外部调用：更新门/窗状态（开/关/破坏）</summary>
    public void UpdateState(State newState)
    {
        currentState = newState;
        ApplyStateSprite(smallSymbolImage, newState);
        ApplyStateSprite(largeSymbolImage, newState);
    }

    private void ApplyStateSprite(Image img, State state)
    {
        if (img == null) return;
        switch (state)
        {
            case State.Closed: img.sprite = closedSprite; break;
            case State.Open: img.sprite = openSprite; break;
            case State.Destroyed: img.sprite = destroyedSprite; break;
        }
    }

    void OnDestroy()
    {
        if (smallOverlayInstance != null) Destroy(smallOverlayInstance.gameObject);
        if (largeOverlayInstance != null) Destroy(largeOverlayInstance.gameObject);
    }
}