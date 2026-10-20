# 2D 创作指南

[插件首页](README.md) · [实现与验收边界](IMPLEMENTATION_STATUS.md)

所有 2D 组件、资产、Inspector、Atlas、Tile Palette 和 Sprite Animation 工作区均由插件提供。安装包内容只读；在 Project 的 `Assets` 中创作，通过 Save 发布当前 draft，Undo/Redo 使用同一套 Editor History。修改不会在保存前改变已导入的运行内容。

## Sprite Renderer

添加 `SpriteRenderer2D` 后，Sprite 字段直接提供 **Texture / Atlas region** 两种来源。Texture 使用标准资产选择器，支持搜索与拖放；Atlas 选择后按区域名称搜索 Sprite。区域 ID 由资产管理，不需要填写。清除是明确操作；Missing source 与 Missing region 保留原引用，直到恢复或主动替换。

图片预览复用插件现有 Sprite Shader、材质绑定与 Render Graph，包含图集裁剪、旋转和 trim。没有 Sprite 时使用 Primitive；有 Sprite 时选择 Simple、Sliced 或 Tiled。Sliced/Tiled 需要图集的 border。

- Size 的零分量采用图片自然尺寸；Pixels Per Unit 为零时采用 Project Settings 中的默认值。
- Pivot 使用归一化坐标；Flip X/Y 改变显示方向。
- Material 默认使用插件材质。显式清除或 Missing 时 Inspector 会提示。
- Sorting Layer 按名称选择，Order in Layer 控制同层 painter order。重命名排序层保留已有身份。
- 开启 Receive Lighting 后显示法线、发光和光照样式；Bounds Padding 等项归入 Advanced。

Sprite Mask、Tilemap Renderer、Sorting Group 和 Particle System 同样使用按名称选择的排序层。Camera、Light、Shadow Caster 继续由各自的 Geometry、Appearance 与 Lighting 字段组成，没有后端句柄或配置表泄漏。

## Sprite Atlas

在 File Browser 选择 **Create → Rendering 2D → Sprites → Sprite Atlas**。

1. Add Texture Source，在 Selected Source 中选择颜色纹理；法线和发光纹理可选。
2. Whole Source 创建整图 Sprite；Slice Grid 按完整单元格切片；Manual Slice 创建指定矩形。网格重新切片时，相同 source/rectangle 保留 ID、名称、pivot、border 和 outline。
3. 在 Source Canvas 中点击区域选择。拖动主体移动，拖动右下角调整尺寸；数值输入也可精确编辑。每次完整手势只有一项 History。
4. 设置名称、pivot、九宫格 border，以及自动轮廓或矩形轮廓。Packing Preview 提供容量诊断；Save 通过原有 importer 生成页面。

重复添加同一个矩形会选择已有 Sprite。删除 source 会同时移除属于它的切片，并保持其他切片身份。Published page 展示已发布产物；未保存的矩形编辑不会提前覆盖运行时图集。

单次网格最多 65,536 个 Sprite，画布最多显示 4,096 个区域命中控件；区域选择器和数值字段仍可访问选中的数据。画布保持在 Inspector 可用宽度内。

## Sprite Animation

创建 **Rendering 2D → Sprites → Sprite Animation**，或者选择 Sprite Animator 的 Animation 后点击 Edit Animation。打开 **Panel → Authoring → Sprite Animation** 可以停靠独立工作区。

片段以名称选择；新片段自动获得独立稳定 ID，改名不影响 Animator。添加帧后在 Inspector 的 Frame Sprite 中选择图片。Duplicate、Move Left/Right 和 Remove 管理顺序，Duration 使用秒；New Frame FPS 只决定新帧默认时长。

时间条按持续时间显示帧；点击选择，水平拖动调整时长。播放头可以拖动，Play/Pause、Stop、Previous、Next 提供预览和逐帧控制。Zoom 与 First Frame 导航长片段。Adjacent Frames 显示相邻帧的独立预览。工作区和 Inspector 共用所选帧、draft、Save/Revert 和 History；预览不修改 Scene，也不派发游戏事件。

运行时 `SpriteAnimator2D.Listen(handler, priority)` 返回 Core Events 的可释放订阅。事件携带 `clipId`、`frameIndex` 和 `eventId`，不保留 Scene 对象。监听者应在自身停止/销毁时释放 token；Animator 销毁也会释放全部订阅。

```csharp
using System;
using Inno.Rendering2D;
using InnoEngine.Reflection;
using InnoEngine.Scene;

/// <summary>
/// Counts authored frame events during one scene lifetime.
/// </summary>
[StableTypeId("91c82f7e-34bd-4d73-982f-041e787af123")]
public sealed class FrameCounter : GameBehavior
{
    private IDisposable? m_subscription;

    /// <summary>
    /// Gets the number of named frames entered after subscription.
    /// </summary>
    public int enteredFrames { get; private set; }

    /// <inheritdoc />
    protected override void Start()
    {
        SpriteAnimator2D animator = gameObject.GetComponent<SpriteAnimator2D>();
        m_subscription = animator.Listen(_ => enteredFrames++);
    }

    /// <inheritdoc />
    protected override void OnDestroy() => m_subscription?.Dispose();
}
```

完整片段要求每帧已指定 Sprite、持续时间为正且有限。保存失败会保留 draft 与可读诊断。编辑空片段不要求先构造有效运行时内容。

## Tile Set 和 Tilemap

创建 **Rendering 2D → World → Tile Set**，Add Tile 后设置名称、Sprite 与 Tint。数值身份由工具分配；改名不影响 map。Animation 定义可变时长的视觉帧。Neighbor Rules 提供 3×3 可点击条件矩阵，循环切换 Any/Same/Different/Empty/Occupied；Priority 越高越优先。Rule Result 使用同一个 Sprite 选择器。

创建 **World → Tilemap** 后指定 Tile Set。Tile Palette 按名称过滤并显示缩略图；画布以有限可见窗口编辑稀疏数据，Origin X/Y 和 Zoom 控制导航。

| 工具 | 使用方式 |
| --- | --- |
| Brush / Erase | 拖动绘制/擦除；快速移动的中间单元格会补齐。 |
| Pick | 从已有单元格取得 tile、tint、flip 和旋转。 |
| Box | 从起点到终点填充矩形。 |
| Fill | 在当前可见边界内，对相同连通值填充。 |
| Select / Move | 选择矩形，再拖动整体平移；重叠移动保留精确值。 |
| Stamp | 选择后 Copy Selection，再放置稀疏印章。 |

图层可以命名、调整顺序和可见性；仅空图层允许直接删除。Flip、Quarter Turns 和 Brush Tint 作用于绘制值。Palette 和地图缩略图显示基础 artwork。删除 tile definition 保留 unresolved map cells。完整手势、Undo、Redo 和 Revert 共用文档服务；底层编辑算法提供精确 cell change set，不建立另一套 Undo 栈。

画布最多同时显示 32×24 个单元格和 128 个匹配 tile；基础操作限制单次最多 1,048,576 个单元格，拒绝坐标溢出并保持原 map。这里的画布属于 Tilemap 资产文档，Scene viewport 用于查看运行结果。

## 粒子、后处理和设置

Particle Effect 的 Rendering、Emitter、Lifetime and Motion、Over Lifetime 分组提供可复用参数。Size 曲线和 Color 渐变带采样预览，可按时间选择、添加和移除关键点；时间为 0–1，保持唯一且升序。预览与色彩控件使用线性数据，非法数值在保存前拒绝。

Post Process Profile 分为 Color and Tone、Bloom、Vignette、Pixelation。范围和非有限数值在保存边界检查。Project Settings → Rendering → 2D 管理默认 pipeline、像素单位与容量，以及稳定排序层。

所有选择器受所属窗口尺寸约束。短窗口中使用父区域滚动，或折叠不需要的 section；浮动窗口、Popup、Modal 和只读来源不能触发下层绘制操作。
