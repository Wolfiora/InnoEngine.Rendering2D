# Rendering2D 创作体验实施与验收

日期：2026-10-21。

## 边界

2D 组件、资产、Inspector、图集、Tile Palette 和帧动画创作属于本插件。优先直接复用现有公开 scripting API；不为本插件修改引擎 2D 接口，不引用具体 backend，不修改 Canvas、BGCS 或第三方源码。只有确实需要的通用动画编辑基础设施允许进入引擎。本轮为时间条补齐 `ImGui.IsMouseButtonDown` 与累计 `GetMouseDragDelta` 两个无指针 UI 查询，并同步该项目 Wiki 与真实 ImGui context 测试；没有把任何 2D 类型或逻辑放入引擎。

持久化继续使用当前原生资产、Stable ID 和引擎序列化。History 继续使用统一协议；不添加平行 Undo、事件总线、资产数据库、旧格式读取或迁移代码。安装内容保持只读。预览不能修改正在运行的 Scene，也不能持有旧代际的对象或回调。

## 实施顺序

1. 记录源码基线，拆分现有大型文档实现，让文档事务、各类资产编辑器、Inspector 与图形控件各有明确 owner。
2. 统一 Sprite 引用编辑，隐藏实现 ID，提供 Texture/Atlas 模式、区域搜索、清除、预览、状态说明；保留 Missing 值直到用户明确替换。
3. 完善全部现有组件 Inspector：常用字段、几何、外观、排序、灯光及高级项分别组织；片段和排序层用当前数据选择，不要求手输 ID。
4. 完善帧动画：片段增删/命名、循环、帧增删/复制/排序、持续时间、事件、播放/停止/逐帧、可视时间刻度及播放头；预览不派发游戏事件。
5. 完善 Tile Set/Tilemap：可编辑 tile 定义、可视调色板、有限可见画布、画笔/擦除/取样/矩形/填充/选择/移动/印章、图层、缩放与导航；复用既有稀疏数据和编辑算法。
6. 完善 Atlas：源纹理编辑、网格/整图/手动切片、区域选择和可视矩形、名称/pivot/border/轮廓及打包诊断；稳定 ID 不随显示名变化。
7. 补齐粒子、后处理及项目设置的一致排版、空状态、数值校验和操作反馈，同步所有当前文档。
8. 通过正式流程编译源码项目及 IDE、导出 `.iplugin`、更新 Samples 安装包；使用 FlappyBird 检查实际 Inspector、Play、画面和退出。

## 文件职责

```text
Assets/Editor/
├── Inspection/
│   ├── SpriteReferencePropertyDrawer.editor.cs
│   ├── Rendering2DComponentInspectionDrawer.editor.cs
│   └── ParticleLifetimePropertyDrawer.editor.cs
├── Assets/
│   ├── Rendering2DAssetDocumentModule.editor.cs
│   ├── Rendering2DAssetDocumentProvider.editor.cs  文档状态与保存事务
│   ├── Rendering2DAssetDraftHistoryHandler.editor.cs
│   ├── Rendering2DAssetInspectionDrawer.editor.cs
│   ├── Rendering2DAssetSourceInspectionDrawer.editor.cs
│   └── Documents/
│       ├── Rendering2DAssetDocumentProvider.Animation.editor.cs
│       ├── Rendering2DAssetDocumentProvider.Atlas.editor.cs
│       ├── Rendering2DAssetDocumentProvider.AtlasCanvas.editor.cs
│       ├── Rendering2DAssetDocumentProvider.TileSet.editor.cs
│       ├── Rendering2DAssetDocumentProvider.Tilemap.editor.cs
│       ├── Rendering2DAssetDocumentProvider.Particle.editor.cs
│       └── Rendering2DAssetDocumentProvider.PostProcess.editor.cs
├── Authoring/
│   ├── SpriteAtlasComposer2D.editor.cs             原有打包实现
│   ├── SpriteAtlasSlicing2D.editor.cs              身份稳定的切片
│   ├── TilemapEditing2D.editor.cs                 原有稀疏编辑算法
│   ├── TilemapHistory2D.editor.cs                 中立变化记录
│   └── Animation/
│       ├── SpriteAnimationEditing2D.editor.cs     确定性帧时间与编辑操作
│       └── SpriteAnimationPanel.editor.cs         共享文档工作区
├── Presentation/
│   ├── Rendering2DEditorFields.editor.cs
│   └── Rendering2DSpritePreviewModule.editor.cs
├── Shaders/
│   ├── SpriteShaderPreview.editor.cs              复用现有管线
│   └── SpritePreviewPipeline.Thumbnail.editor.cs   Sprite 离屏预览
└── Settings/                                      现有项目设置入口
Tests/InnoEngine.Rendering2D.Tests/
├── AnimationEditingTests.cs
├── AnimatorLifecycleTests.cs
├── AssetValidationTests.cs
├── AtlasSlicingTests.cs
├── DocumentLifecycleTests.cs
└── TilemapEditingTests.cs
Tools/Test-Authoring.ps1        正式编译与公开程序集测试入口
Documentation/                 计划、验收命令与实际结果
Assets/Documentation/          随安装包分发的当前创作指南
```

不按文件数量追求拆分；同一功能的 state、操作和 presentation 保持紧邻。所有新源码使用普通 using、英文 XML 注释和项目要求的多参数声明排版。

## 验收关卡

| 关卡 | 必须验证 |
| --- | --- |
| 架构 | 除明确动画基础设施外，引擎源码差异为空；没有具体 backend 或实现 namespace 引用，没有反射穿透或测试后门。 |
| 数据 | Sprite Missing/None 有区别；重命名保留 region/clip 身份；无效或非有限时间拒绝；保存/读取往返一致。 |
| 编辑 | 增删/复制/排序/绘制结果正确；Undo/Redo 和 Revert 恢复；只读来源不变；失败保留原 draft。 |
| UI | 空资产可开始创作；小窗口与不同 zoom 下内容可达、无重叠；控件使用统一比例和有界选择器；图形操作尊重前景与禁用状态。 |
| 编译 | 正式脚本编译、运行/Editor 引用、IDE warn-as-error、测试均通过。 |
| 安装 | 正式导出包内容与源一致；FlappyBird 安装态脚本及 IDE 编译通过。 |
| 产品 | FlappyBird 的 Inspector、Play 往返、图形 API smoke 和退出；Mac 不可用及 Browser 上游 ABI 阻塞单列。 |

每阶段记录实际命令、结果和未执行项；功能存在、编译通过、交互通过和跨平台通过分别报告，不能互相替代。
