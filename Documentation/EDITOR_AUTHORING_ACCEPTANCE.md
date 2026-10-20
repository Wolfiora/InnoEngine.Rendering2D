# Rendering2D 创作工具验收（2026-10-21）

## 交付范围

本轮覆盖 Sprite 选择、组件 Inspector、图集切片、帧动画时间条与独立面板、Tile Set/Tilemap 资产画布、粒子曲线与渐变，以及文档、测试和正式安装包。详细职责和文件树见 [实施计划](EDITOR_AUTHORING_PLAN.md)，操作指南见 [创作指南](../Assets/Documentation/AUTHORING.md)。

2D 实现全部位于本插件。引擎仅新增 `ImGui.IsMouseButtonDown`、`ImGui.GetMouseDragDelta` 两个通用时间条输入查询及其 Wiki、真实 ImGui context 测试。Canvas、BindGen-CS、第三方源码、Bindings/Generated 未修改。BGCS 仍使用原始 NuGet 3.0.1。

没有新增全局/隐式 using、事件总线、资产数据库、Undo 栈、历史格式读取、反射穿透或测试后门。动画事件通过 Core Events 的可释放订阅提供；私有文档草稿只保存当前模型与中立字节。

## 实际修复

- Sprite 编辑使用 Texture/Atlas 区域选择、名称过滤、Missing/None 提示和缩略图；子属性的修改由统一根属性事务提交。
- Animator、Tilemap、Sorting Group、Particle 和 Mask 使用领域名称与分组，减少手输身份标识。
- Animation 使用独立片段名称和 Stable ID；时间边界、无效时长、复制/移动隔离、回调重入和订阅销毁经过测试。
- Atlas 网格保留已有区域身份；增加 source 管理、选择、移动/缩放手势、打包诊断和有限命中控件。
- Tilemap 包含有界调色板与画布、画笔补线、擦除、取样、矩形、填充、选择、移动和印章；限制面积，拒绝坐标溢出，不提交部分结果。
- Tilemap 草稿与保存值只复制业务内容，重命名后不会把原资产路径带入另一个保存目标。
- 文档注册只建立稳定归属；候选切换之后才恢复 typed draft。停止回调只处理冻结快照，不使用旧代际序列化器。
- History 比较和恢复使用原始中立快照，避免重绑定后的引用提示路径变化破坏已存在记录。
- 预览复用引擎的离屏渲染、GPU 资源与生命周期服务，限制可见 owner 数；空闲 Inspector 不再每帧完整序列化草稿。

### 公开入口与职责

| 入口 | 必要性与稳定语义 |
| --- | --- |
| `SpriteAnimator2D.Listen` / `SpriteAnimationFrameEvent2D` | 通过 Core Events 订阅命名帧，只传递片段 ID、帧索引与事件 ID；订阅可释放，组件销毁时统一退休。 |
| `SpriteAnimationClip2D.name` | 可读片段名独立于持久 ID，重命名不改变组件引用。 |
| `SpriteAnimationEditing2D` | 插件创作扩展可复用确定性时间定位、复制与排序；操作返回独立数据，不依赖 UI 或 Scene。 |
| `SpriteAtlasSlicing2D.Grid` | 可替换的创作流程复用有界、保留已有区域身份的切片算法。 |
| `ImGui.IsMouseButtonDown` / `GetMouseDragDelta` | 通用时间条输入基础设施，无 2D 类型、资源或后端依赖。 |

原有 Tilemap 编辑与 History 编解码契约按职责分文件保留。Inspector、文档 provider 和预览 module 通过现有扩展协议发现，不增加中央注册表。

## 环境与证据

Windows x64，.NET SDK 10.0.402 / C# 14；可用 GPU 为 NVIDIA GeForce RTX 3070 Ti Laptop GPU 和 AMD Radeon Graphics。UI 使用 FlappyBird，窗口包含 1069×631 与最大化 1707×912 两种布局。产品日志记录实际初始化的图形 API。

原始命令、stdout/stderr、进程退出码、TRX、API inventory 与项目图位于仓库忽略目录 `artifacts/authoring-2026-10-21/`。实际输出 owner 位于 `D:/InnoEngine-acceptance/rendering2d-authoring-2026-10-21-5c7a1d90`。每个 `*-spec.json` 记录完整可复现命令和环境；`*-result.json` 记录结果及磁盘最低余量。C 盘保护线为 54 GiB，本轮观测保持高于 55 GiB。

| 检查 | 结果与证据 |
| --- | --- |
| Engine Editor、Build 正式 Debug 构建 | 通过，0 warning / 0 error；`engine-editor-build`、`engine-build-host`。 |
| 插件公开契约回归 | 42 通过，0 失败，0 跳过；`tests-18` 和 `authoring.trx`。 |
| 原生 ImGui 布局/输入 | 28 通过，0 失败，0 跳过；`widget-tests-final` / `widgets.trx`。 |
| 插件脚本与 IDE | 正式 scripts 与 warn-as-error 构建通过；`tests-18`、`ide-10`，IDE 为 0 warning / 0 error。 |
| 架构、项目图、XML、Wiki/API 示例 | 通过，233/233 项目归属求值；`architecture-wiki`。非 Windows 产品 API 编译按工具输出明确未执行。 |
| 正式包导出 | 通过；`export-11`。Samples 只替换完整正式包，没有修改包内文件。205 个资产文件逐字节匹配源码，无遗漏、重复 entry 或缺失 sidecar；`static-package-audit.json`。 |
| 最终安装态脚本、IDE、GPU | 见本报告最终校验表，使用同一正式包。 |

### 实际 UI 验证

- Sprite/帧纹理选择器、下拉边界、不同 artwork 缩略图、父区域滚动、Save 状态反馈。
- Animation 新建、两帧编辑、Duplicate、Undo/Redo、播放头选择、关闭后重新打开；FlappyBird 的 `~Authoring/BirdFlight.ispriteanimation2d` 保存不同拍翅图片。
- 独立 Animation 面板采用固定根窗口和一个有界 child 滚动 owner；实际滚动能到达底部帧参数、预览和提示，原生测试验证长内容不会再令父窗口产生第二条滚动范围。
- Tile Set 从空状态添加命名 tile，指定纹理并保存；Tilemap 选择它后自动生成调色板和缩略图。
- 单格绘制、Undo、Redo、非空图层删除禁用、重命名后保存。
- 插件候选更新时保留未保存 Tilemap 草稿；更新后一次 Undo 移除第二格，Redo 恢复。最终 `ui-05` 会话正常退出，退出码 0，stdout/stderr 未发现异常、引用丢失或 teardown failure。
- FlappyBird 进入 Play 后管道和角色推进；退出 Play 恢复编辑场景。

早期验收发现的保存/注册/History 问题及失败日志均保留，不能用后续通过覆盖其存在；上述问题已经修复并重新验证。

## 尚未证明与能力边界

- 原生 ImGui 测试验证了累计 drag 与松手状态；computer use 的连续拖动在产品中未获得可靠可见结果。时间条 resize、Atlas move/resize、连续画笔仍需要真实指针交互复核，不能记为产品交互通过。
- Atlas 与粒子工具通过脚本编译和相关算法/数据校验；没有对所有工具、所有 DPI、弹窗遮挡和只读变体逐项完成视觉验收。
- macOS/Metal 无可用设备，本轮未执行。Browser 的 BGCS aggregate ABI 阻塞沿用独立工具链问题，本轮未修改或重新证明。NativeAOT/Player 完整发布矩阵本轮未重跑。
- 通用 Scene 保存的一次操作产生了广泛身份字节变化（`scene-save-diff.json`）。已恢复原始 FlappyBird 场景，本轮不提交该变化；原因尚未在独立最小用例中确定，需要后续 Engine Scene/Identity 专项调查。不能据此宣称全引擎身份保存验收通过。
- 当前动画工具是 Sprite 帧动画，不包含骨骼、电影多轨时间线或 Animator 状态机。Tilemap 画布属于资产文档，Scene viewport 画笔、自由平移图集工作区、实时粒子发射器预览等能力尚未实现；具体状态见 [能力清单](../Assets/Documentation/IMPLEMENTATION_STATUS.md)。
- 本轮没有完成可重复的帧时间/分配基准，不宣称量化性能提升。预算、复制减少和缓存调用次数属于实现事实。

因此，本轮实现可以作为明确可用的创作工具增强交付；不能将其表述为 Unity 功能全部对等或所有平台/UI 已完整验收。

## 最终校验表

| 最终状态检查 | 结果 |
| --- | --- |
| FlappyBird 正式安装态 scripts / IDE 投影 | 通过，`flappy-scripts-final`。 |
| FlappyBird IDE Release / warnings as errors | 通过，0 warning / 0 error；`flappy-ide`。 |
| FlappyBird D3D11 | 3000 帧，正常退出，exit 0；`flappy-d3d11`。 |
| FlappyBird D3D12 | 3000 帧，正常退出，exit 0；`flappy-d3d12`。 |
| FlappyBird Vulkan | 3000 帧，正常退出，exit 0；`flappy-vulkan`。 |
| 原始 FlappyBird Scene | 与本轮开始时相同；只添加独立 `~Authoring` 练习资产。 |
| 静态收口 | Markdown 相对链接、XML、PowerShell/Bash 语法、metadata、包内容和 Git whitespace 检查通过；没有新增 global/implicit using。 |

最终包 SHA-256：`f12ccf984185e722a9ebfedb723eb62c90eff2392ec13c14fb9941b475604498`。

Engine 动画输入基础设施提交为 `76ee62807bd164ad1935a6d93e9f00633670867a`。GPU workflow 默认使用该已验收提交，并允许显式覆盖 Engine revision；不会以默认分支的未来内容替代本次基线。该 CI 配置已更新，远端 workflow 本轮未触发。

所有提交仅保存在各仓库当前 branch，未推送。提示音路径 `/System/Library/Sounds/Glass.aiff` 在此 Windows 环境不可用，未播放。
