# NavMesh 生成小地图插件

本插件基于 Unity 的 NavMesh 数据自动生成多层地图纹理，并提供了完整的小地图 / 大地图 UI 控制、图标和覆盖物系统。支持运行时动态切换楼层、图标方向指示、边缘显示、门 / 窗 / 墙状态变化等功能。

## 目录

- [特性](#特性)
- [安装](#安装)
- [快速开始](#快速开始)
- [组件详解](#组件详解)
  - [NavMeshMapBaker](#navmeshmapbaker)
  - [MinimapUI](#minimapui)
  - [MinimapIcon](#minimapicon)
  - [MinimapOverlay](#minimapoverlay)
  - [PlayerIconRotator](#playericonrotator)
- [UI 层级结构](#ui-层级结构)
- [使用方法](#使用方法)
  - [1. 准备 NavMesh](#1-准备-navmesh)
  - [2. 烘焙地图纹理](#2-烘焙地图纹理)
  - [3. 搭建 UI](#3-搭建-ui)
  - [4. 配置 MinimapUI](#4-配置-minimapui)
  - [5. 添加图标](#5-添加图标)
  - [6. 添加覆盖物（门 / 窗 / 墙）](#6-添加覆盖物门--窗--墙)
  - [7. 运行测试](#7-运行测试)
- [常见问题](#常见问题)
- [扩展与自定义](#扩展与自定义)

---

## 特性

- 自动从 NavMesh 提取楼层信息，按高度聚类生成多层纹理
- 支持编辑器中预烘焙并保存为 PNG 纹理资产，运行时直接加载
- 小地图（随玩家旋转）与大地图（固定北朝上）双模式，一键切换
- 图标系统：任意物体可挂载 `MinimapIcon`，自动创建 UI 图标，支持方向箭头、边缘缩略显示
- 覆盖物系统：门 / 窗 / 可破坏墙可挂载 `MinimapOverlay`，显示状态（开 / 关 / 破坏）并自适应位置与旋转
- 玩家图标自动旋转指示朝向
- 支持多层建筑，根据玩家 Y 坐标自动切换当前楼层

---

## 安装

### 直接将unitypackage下载后拖到unity窗口中

文件位置：release/minimap.unitypackage

此包包含了一个极简易的demo场景用于快速了解功能

---


---

## 组件详解

### NavMeshMapBaker

**作用**：从当前场景的 NavMesh 数据中提取所有三角形，按高度聚类为不同楼层，并生成对应的纹理。

**Inspector 属性**：

| 属性 | 说明 |
|------|------|
| `textureSize` | 生成纹理的分辨率（如 1024x1024）。 |
| `worldSize` | 地图覆盖的世界范围（单位：米）。 |
| `floorClusterThreshold` | 楼层聚类阈值，Y 坐标相差小于该值的三角形视为同一楼层。 |
| `simplifyTolerance` | 边缘简化容差（世界单位），用于减少轮廓顶点数量。 |
| `angleSnapDegrees` | 边缘角度吸附（如 10°），使轮廓更规整。 |
| `minIslandArea` | 最小岛屿面积（平方米），小于该值的孤立区域将被过滤。 |
| 绘制颜色 | 背景色、楼层填充色、墙线颜色、楼梯颜色等。 |
| `wallThickness` | 墙线绘制的像素厚度。 |
| `stairLineSpacing` | 楼梯内部横线间距（世界单位），目前未启用。 |
| `saveFolderPath` | 保存纹理的路径（相对 Assets）。 |
| `floors` | 烘焙后生成的楼层数据列表（运行时只读）。 |
| `mapOrigin` | 地图原点（世界坐标最小角）。 |

**编辑器按钮**：

- **烘焙地图纹理并保存**：执行烘焙，并将每层纹理保存为 PNG 到指定文件夹，同时将引用存入 `floors` 列表。

**注意**：NavMesh 中必须定义两个 Area：**layer**（可走区域）和 **stair**（楼梯区域），并在烘焙时正确赋值。

---

### MinimapUI

**作用**：运行时控制小地图与大地图的显示、楼层切换、图标位置更新。

**Inspector 属性**：

| 属性 | 说明 |
|------|------|
| `baker` | 引用场景中的 `NavMeshMapBaker`，用于获取楼层纹理。 |
| `playerTransform` | 玩家 Transform，用于计算地图偏移和旋转。 |
| **小地图 UI** | |
| `smallMapImage` | 小地图的 RawImage，显示当前楼层纹理。 |
| `smallMapContainer` | 小地图根容器（用于整体显示/隐藏）。 |
| `smallRotatingContainer` | 小地图中随玩家旋转的容器（内含地图图片和图标）。 |
| `smallIconContainer` | 小地图图标父物体（应位于 rotatingContainer 内）。 |
| **大地图 UI** | |
| `largeMapContainer` | 大地图根容器。 |
| `largeMapImage` | 大地图的 RawImage。 |
| `largeIconContainer` | 大地图图标父物体。 |
| `largeMapDisplayRange` | 大地图显示范围（世界单位）。 |
| **显示** | |
| `displayRange` | 小地图显示范围（世界单位）。 |
| `largeMapKey` | 切换大地图的按键（默认 Q）。 |
| `runtimeBakeMode` | 若勾选，则运行时调用 `baker.BakeRuntime()` 实时生成纹理（不保存）。 |

**核心逻辑**：

- 每帧根据玩家位置更新地图 UV 偏移。
- 小地图模式下，`smallRotatingContainer` 旋转以匹配玩家朝向（北朝上）。
- 自动根据玩家 Y 坐标切换最接近的楼层纹理。
- 管理所有注册的 `MinimapIcon`，计算其在 UI 上的位置并控制显隐。

---

### MinimapIcon

**作用**：挂载到任何需要在小地图上显示的物体上（玩家、敌人、道具等）。运行时自动创建对应的 UI 图标实例。

**Inspector 属性**：

| 属性 | 说明 |
|------|------|
| `iconSprite` | 图标图片。 |
| `iconSize` | 图标尺寸（像素）。 |
| `iconColor` | 图标颜色。 |
| `showOnEdge` | 是否在超出显示范围时在边缘显示缩略图标。 |
| `edgeClampDistance` | 边缘显示的额外范围，超出此距离则完全隐藏。 |
| `showDirection` | 是否显示方向（图标旋转跟随物体 Y 轴旋转）。 |
| `trackedTransform` | 可选，指定要跟踪的 Transform，默认跟踪自身。 |
| `visable` | 外部控制可见性（与 UI 显隐结合）。 |

**方法**（可运行时调用）：

- `SetSprite(Sprite sprite)`：动态更换图标。
- `SetColor(Color color)`：修改颜色。
- `SetAlpha(float alpha)`：修改透明度。

**注意**：图标实例会自动创建在 `MinimapUI` 指定的容器下，无需手动添加。

---

### MinimapOverlay

**作用**：用于门、窗、可破坏墙等，在地图纹理上方显示覆盖元素，并根据状态更换 Sprite。

**Inspector 属性**：

| 属性 | 说明 |
|------|------|
| `type` | 类型（Door / Window / BreachableWall），仅用于分类。 |
| `overlayPrefab` | 覆盖物预制体，需包含 RectTransform 和 Image 子物体。 |
| `width` | 覆盖物在世界空间中的宽度（米），用于计算 UI 上的尺寸。 |
| `closedSprite` | 关闭状态时的 Sprite。 |
| `openSprite` | 开启状态时的 Sprite。 |
| `destroyedSprite` | 破坏状态时的 Sprite。 |

**运行时状态枚举**：

- `State.Closed`
- `State.Open`
- `State.Destroyed`

调用 `UpdateState(State newState)` 更新显示。

**位置计算**：根据物体世界坐标转换为 UI 坐标，并自动适应小地图 / 大地图的旋转方式。

---

### PlayerIconRotator

**作用**：简单辅助脚本，用于让玩家图标始终指向玩家朝向（通常用于玩家自己的箭头图标）。

**属性**：

- `playerTransform`：玩家 Transform，若不指定则查找 Tag 为 "Player" 的物体。
- `iconRectTransform`：需要旋转的 UI 图标 RectTransform。

在 `LateUpdate` 中设置图标的局部旋转为 `-playerTransform.eulerAngles.y`。

---

## UI 层级结构

为了正确实现小地图旋转和大地图固定，UI 层级必须按以下结构组织（以 Canvas 为根）：

```
Canvas (Screen Space - Overlay 或 Camera)
├── SmallMapContainer (RectTransform)   // 小地图整体容器
│   └── SmallRotatingContainer (RectTransform)   // 随玩家旋转
│       ├── SmallMapImage (RawImage)   // 地图纹理
│       └── SmallIconContainer (RectTransform)   // 所有小地图图标的父物体
│
└── LargeMapContainer (RectTransform)   // 大地图整体容器
    ├── LargeMapImage (RawImage)        // 大地图纹理
    └── LargeIconContainer (RectTransform)   // 所有大地图图标的父物体
```

**说明**：

- `SmallRotatingContainer` 的旋转由 `MinimapUI` 控制，使地图始终“北朝上”跟随玩家旋转。
- `LargeMapContainer` 不旋转，大地图永远固定方向（北朝上）。
- 图标和覆盖物实例会自动创建在对应的容器下，无需手动放置。

---

## 使用方法

### 1. 准备 NavMesh

- 打开 **Window → AI → Navigation** 面板。
- 在 Areas 标签中确保存在 **layer** 和 **stair** 两个 Area（名称必须完全一致），并为其分配适当的代价。
- 为场景中的地面、楼梯等物体设置 Navigation Static，并分别指定正确的 Area（地面设为 layer，楼梯设为 stair）。
- 烘焙 NavMesh（Bake 标签页）。

### 2. 烘焙地图纹理

- 在场景中新建空物体，命名为 `NavMeshMapBaker`，挂载 `NavMeshMapBaker` 脚本。
- 设置纹理大小、世界尺寸等参数（通常 worldSize 应与 NavMesh 实际覆盖范围匹配）。
- 点击 Inspector 下方的 **烘焙地图纹理并保存** 按钮。
- 等待烘焙完成，生成的纹理将保存在 `Assets/你设置的文件夹` 下，并自动引用到 `floors` 列表中。

### 3. 搭建 UI

- 在 Canvas 下按照 [UI 层级结构](#ui-层级结构) 创建所有必要的 UI 元素。
- 为 `SmallMapImage` 和 `LargeMapImage` 设置合适的宽高（例如 300x300）。
- 可选：为地图图片添加边框、背景等装饰。

### 4. 配置 MinimapUI

- 在 Canvas 上挂载 `MinimapUI` 脚本。
- 将 `NavMeshMapBaker` 物体拖入 `baker` 字段。
- 将玩家 Transform 拖入 `playerTransform` 字段（也可在代码中自动查找，但建议手动指定）。
- 将所有 UI 元素引用拖入对应字段。
- 调整 `displayRange` 和 `largeMapDisplayRange` 以匹配你的游戏需求。
- （可选）修改大地图切换按键。

### 5. 添加图标

- 为玩家物体添加 `MinimapIcon`，设置图标 Sprite 和颜色，勾选 `showDirection` 可使箭头指向玩家朝向。
- 为玩家物体添加 `PlayerIconRotator`，并将玩家图标实例的 RectTransform 拖入 `iconRectTransform`（运行时 `MinimapIcon` 会创建实例，所以需要在脚本执行后获取引用，此处建议通过代码获取，或简单起见不适用该脚本，而依赖 `MinimapIcon` 的 `showDirection` 功能——实际上 `MinimapIcon` 的 `showDirection` 已经会使图标旋转，无需额外脚本。`PlayerIconRotator` 可能已过时，或用于其他用途）。
- 为敌人、物品等需要显示的物体也添加 `MinimapIcon`。

### 6. 添加覆盖物（门 / 窗 / 墙）

- 创建一个覆盖物预制体：一个空 GameObject，挂载 RectTransform（通常宽高 100x30）并包含一个 Image 子物体作为符号。
- 将此预制体拖入 `MinimapOverlay` 的 `overlayPrefab` 字段。
- 为门 / 窗物体添加 `MinimapOverlay`，设置类型、宽度、状态 Sprite。
- 在游戏逻辑中，当门打开/关闭/破坏时，调用 `overlay.UpdateState(新状态)`。

### 7. 运行测试

- 运行场景，小地图应显示当前楼层纹理，玩家图标位于中心。
- 移动玩家，地图滚动，图标位置更新。
- 按切换键（默认 Q）切换至大地图，大地图应显示更大范围且不旋转。
- 走近门 / 窗，应能看到覆盖物正确显示。

---

## 常见问题

**Q：烘焙时提示“NavMesh Area 'layer' 或 'stair' 未定义”**  
A：请在 Navigation 窗口的 Areas 标签中添加这两个 Area，名称必须完全一致（区分大小写）。

**Q：生成的纹理全是背景色，没有楼层填充**  
A：可能原因：NavMesh 未正确烘焙；三角形未设置正确的 Area（应为 layer）；worldSize 设置过小导致所有顶点超出范围。

**Q：图标不显示或位置错误**  
A：检查 `MinimapUI` 中的 IconContainer 引用是否正确；检查图标的 `visable` 是否为 true；确认图标的物体与玩家在同一楼层（Y 坐标接近）。

**Q：大地图不显示纹理**  
A：确保 `largeMapImage.texture` 已被赋值（运行时会自动赋值）；检查 `largeMapContainer` 是否被正确激活。

**Q：覆盖物位置偏移**  
A：检查 `width` 设置是否正确；确认 `overlayPrefab` 的 pivot 设置合理（通常居中下）。

---

## 扩展与自定义

- **多楼层切换**：`MinimapUI` 已自动处理楼层切换，无需额外代码。
- **自定义图标生成逻辑**：可继承 `MinimapIcon` 重写 `UpdateIconRotation` 等方法。
- **覆盖物状态同步**：可从你的游戏逻辑中调用 `UpdateState`。
- **运行时动态生成地图**：启用 `runtimeBakeMode`，并确保在需要时调用 `baker.BakeRuntime()`。

---

如有任何问题或建议，欢迎反馈。
