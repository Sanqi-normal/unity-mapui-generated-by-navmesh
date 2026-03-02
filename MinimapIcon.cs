using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂到场景中任何要在小地图上显示图标的对象。
/// 图标 Sprite 由 iconSprite 字段直接赋值或预制体预设。
/// 同时支持小地图和大地图双实例，各自挂在对应的 iconContainer 下。
/// </summary>
public class MinimapIcon : MonoBehaviour
{
    [Header("图标")]
    public Sprite iconSprite;
    public Vector2 iconSize = new Vector2(24f, 24f);
    public Color iconColor = Color.white;

    [Header("行为")]
    public bool showOnEdge = false;
    public float edgeClampDistance = 50f;
    public bool showDirection = false;

    [Header("跟踪目标(可选，不填则跟踪自身 Transform)")]
    [SerializeField] private Transform trackedTransform;

    public Transform TrackedTransform => trackedTransform ? trackedTransform : transform;
    public bool visable = true;

    // ---- 运行时 ----
    private RectTransform smallIconInstance;   // 小地图图标实例
    private Image smallIconImage;

    private RectTransform largeIconInstance;   // 大地图图标实例
    private Image largeIconImage;

    private MinimapUI minimap;
    private bool prevVisible = true;
    private Vector2 scalexy;
    void Start()
    {
        minimap = FindObjectOfType<MinimapUI>();
        if (minimap == null)
        {
            Debug.LogWarning("MinimapIcon: 场景中没有 MinimapUI");
            return;
        }

        // 为小地图创建实例
        smallIconInstance = CreateInstance(minimap.smallIconContainer, out smallIconImage);

        // 为大地图创建实例（如果大地图容器存在）
        if (minimap.largeIconContainer != null)
            largeIconInstance = CreateInstance(minimap.largeIconContainer, out largeIconImage);

        minimap.RegisterIcon(this);
        scalexy = new Vector2(minimap.largeMapImage.transform.localScale.x, minimap.largeMapImage.transform.localScale.y);
    }

    private RectTransform CreateInstance(RectTransform parent, out Image img)
    {
        GameObject go = new GameObject($"MinimapIcon_{gameObject.name}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = iconSize;
        rt.anchoredPosition = Vector2.zero;

        img = go.GetComponent<Image>();
        img.sprite = iconSprite;
        img.color = iconColor;
        img.raycastTarget = false;

        go.SetActive(true);
        return rt;
    }

    void OnDestroy()
    {
        if (minimap != null) minimap.UnregisterIcon(this);
        if (smallIconInstance != null) Destroy(smallIconInstance.gameObject);
        if (largeIconInstance != null) Destroy(largeIconInstance.gameObject);
    }

    void LateUpdate()
    {
        if (smallIconInstance == null) {
            Start();
        }
        ;
        if (smallIconInstance == null) return;

        // 可见性同步
        if (visable != prevVisible)
        {
            // 实际可见性由 MinimapUI.UpdateIcons 控制，此处只做外部强制隐藏
            prevVisible = visable;
        }

        // ---- 小地图图标旋转 ----
        UpdateIconRotation(smallIconInstance, minimap.smallIconContainer.eulerAngles.z);

        // ---- 大地图图标旋转（大地图一般不旋转，eulerAngles.z 为 0）----
        if (largeIconInstance != null)
            UpdateIconRotation(largeIconInstance, minimap.largeIconContainer.eulerAngles.z);
    }

    private void UpdateIconRotation(RectTransform inst, float mapRotationZ)
    {
        if (showDirection)
        {
            float yaw = TrackedTransform.eulerAngles.y;
            inst.rotation = Quaternion.Euler(0f, 0f, - yaw);
        }
        else
        {
            // 不需要朝向时，保持世界空间旋转为零即可
            inst.rotation = Quaternion.identity;
        }
    }

    // ---- 由 MinimapUI 调用 ----

    /// <summary>设置小地图上的 UI 位置</summary>
    public void SetSmallUIPosition(Vector2 pos)
    {
        if (smallIconInstance != null)
            smallIconInstance.anchoredPosition = pos;
    }

    /// <summary>设置大地图上的 UI 位置</summary>
    public void SetLargeUIPosition(Vector2 pos)
    {
        pos.x *= scalexy.x;
        pos.y *= scalexy.y;
        if (largeIconInstance != null)
            largeIconInstance.anchoredPosition = pos;
    }

    /// <summary>兼容旧接口，默认设置小地图位置（MinimapUI 会直接调用分离接口）</summary>
    public void SetUIPosition(Vector2 pos) => SetSmallUIPosition(pos);

    public void SetSmallVisible(bool visible)
    {
        if (smallIconInstance != null)
            smallIconInstance.gameObject.SetActive(visible && visable);
    }

    public void SetLargeVisible(bool visible)
    {
        if (largeIconInstance != null)
            largeIconInstance.gameObject.SetActive(visible && visable);
    }

    /// <summary>兼容旧接口</summary>
    public void SetVisible(bool visible) => SetSmallVisible(visible);

    public void SetSmallEdgeMode(bool edge)
    {
        if (smallIconInstance != null)
            smallIconInstance.localScale = Vector3.one * (edge ? 0.6f : 1f);
    }

    public void SetLargeEdgeMode(bool edge)
    {
        if (largeIconInstance != null)
            largeIconInstance.localScale = Vector3.one * (edge ? 0.6f : 1f);
    }

    /// <summary>兼容旧接口</summary>
    public void SetEdgeMode(bool edge) => SetSmallEdgeMode(edge);

    // ---- 运行时动态修改 ----

    public void SetSprite(Sprite sprite)
    {
        iconSprite = sprite;
        if (smallIconImage != null) smallIconImage.sprite = sprite;
        if (largeIconImage != null) largeIconImage.sprite = sprite;
    }

    public void SetColor(Color color)
    {
        iconColor = color;
        if (smallIconImage != null) smallIconImage.color = color;
        if (largeIconImage != null) largeIconImage.color = color;
    }

    public void SetAlpha(float alpha)
    {
        iconColor.a = Mathf.Clamp01(alpha);
        if (smallIconImage != null) smallIconImage.color = iconColor;
        if (largeIconImage != null) largeIconImage.color = iconColor;
    }
}