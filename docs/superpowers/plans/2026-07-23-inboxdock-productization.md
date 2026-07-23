# InboxDock v0.3.0 产品化实施计划

日期：2026-07-23

对应设计：`docs/superpowers/specs/2026-07-23-inboxdock-productization-design.md`

目标：在不丢失现有设置、暂存材料和个人工作流的前提下，将 InboxDock 改造成可适配不同 Obsidian Vault 的 Windows 公开测试版，并补齐快捷键、开机自启动、安装、更新、诊断和发布闭环。

## 实施原则

- 先完成配置迁移、路径安全和写入恢复，再修改主界面。
- 每个任务先增加失败测试，再写最小实现，最后运行相关测试。
- 纯配置、模板、路径、状态和迁移逻辑放在 `InboxDock.Core`。
- WPF 项目只负责窗口、系统 API、对话框和视觉呈现。
- 旧设置、旧暂存 JSON 和旧窗口状态必须兼容。
- 不在 v0.3.0 引入 OCR、语音、AI、桌面宠物或脚本系统。
- 每个阶段形成一个可回退的提交；没有明确要求时不推送远程、不创建 Release。

## 最终验证命令

在仓库根目录 `E:\knowledge\InboxDock` 执行：

```powershell
dotnet restore InboxDock.sln
dotnet build InboxDock.sln -c Release --no-restore
dotnet test InboxDock.sln -c Release --no-build
```

安装与发布任务完成后额外执行：

```powershell
dotnet publish src/InboxDock.App -c Release -r win-x64 --self-contained true -o artifacts/publish
```

## 阶段一：配置模型与安全迁移

### 任务 1：建立 v0.3.0 配置模型

目标：用通用 Vault 配置和收集目标替代固定 Inbox、Daily 和附件路径，同时保持旧代码暂时可编译。

新增文件：

- `src/InboxDock.Core/Configuration/VaultProfile.cs`
- `src/InboxDock.Core/Targets/CaptureTarget.cs`
- `src/InboxDock.Core/Targets/AttachmentPolicy.cs`
- `src/InboxDock.Core/Targets/TargetEnums.cs`
- `tests/InboxDock.Core.Tests/Configuration/VaultProfileTests.cs`
- `tests/InboxDock.Core.Tests/Targets/CaptureTargetTests.cs`

修改文件：

- `src/InboxDock.Core/Configuration/AppSettings.cs`
- `tests/InboxDock.Core.Tests/Configuration/SettingsStoreTests.cs`

实施步骤：

1. 为 `AppSettings` 增加 `SchemaVersion`，当前新版值固定为 `2`。
2. 增加当前 `VaultProfile`，v0.3.0 只保存一个激活配置，不实现多 Vault 列表和切换界面。
3. `VaultProfile` 至少包含：
   - 稳定 `Id`
   - 显示名称
   - Vault 根目录
   - 默认目标 ID
   - 收集目标列表
   - 主题、置顶、自动收回、快捷键、自启动和窗口状态设置
4. 定义四种 `TargetWriteMode`：
   - `AppendToFile`
   - `AppendToPeriodicFile`
   - `CreateNote`
   - `StagingOnly`
5. 定义 `CaptureTarget` 的必要字段，不实现条件规则、脚本和网络行为。
6. 定义附件策略：跟随 Obsidian、固定目录、日期目录、笔记旁和只暂存。
7. 为所有列表提供不可空默认值，防止旧 JSON 缺字段时反序列化为 `null`。
8. 暂时保留旧路径属性，标记为迁移输入；等 `MainViewModel` 完成切换后再决定是否移除。

测试：

- 新配置包含一个 Vault 和至少一个默认目标时通过验证。
- 默认目标 ID 必须指向现有目标。
- 目标 ID 不允许重复。
- 目标名称和必要路径不能为空。
- 中文路径、中文名称和模板可以 JSON 往返保存。

运行：

```powershell
dotnet test tests/InboxDock.Core.Tests/InboxDock.Core.Tests.csproj -c Release --filter "FullyQualifiedName~Configuration|FullyQualifiedName~Targets"
```

建议提交：

```text
feat: model configurable capture targets
```

### 任务 2：实现旧设置迁移和回滚

目标：把当前固定路径配置自动转换成 v2 配置，失败时不覆盖旧文件。

新增文件：

- `src/InboxDock.Core/Configuration/SettingsMigration.cs`
- `src/InboxDock.Core/Configuration/LegacyAppSettings.cs`
- `tests/InboxDock.Core.Tests/Configuration/SettingsMigrationTests.cs`

修改文件：

- `src/InboxDock.Core/Configuration/SettingsStore.cs`
- `tests/InboxDock.Core.Tests/Configuration/SettingsStoreTests.cs`

实施步骤：

1. `SettingsStore.LoadAsync` 先读取 JSON 文档中的 `schemaVersion`，缺失时视为旧版。
2. 旧版 JSON 反序列化到独立 `LegacyAppSettings`，避免新模型字段默认值掩盖迁移判断。
3. 创建两个目标：
   - “收件箱”：沿用旧 `InboxPath` 和 `AttachmentsPath`。
   - “今日日记”：沿用旧 `DailyPath` 和 `DailyTemplatePath`。
4. 沿用旧 `AlwaysOnTop`、`LaunchAtSignIn`、主题和窗口位置。
5. 首次迁移前复制为带时间或固定 `.v1.bak` 后缀的备份；若备份已存在，不覆盖。
6. 新配置先写临时文件、重新读取验证，再原子替换正式文件。
7. 迁移失败时返回可恢复错误，保持原始设置和备份不变。
8. 迁移成功后再次启动不得重复创建目标或备份。

测试：

- 完整旧设置迁移后路径和开关保持一致。
- 缺少可选旧字段时使用旧版默认值。
- 无效 JSON 不被覆盖。
- 临时文件写入失败时原设置不变。
- 第二次加载迁移后的配置保持幂等。
- 旧版中文路径正确迁移。

运行：

```powershell
dotnet test tests/InboxDock.Core.Tests/InboxDock.Core.Tests.csproj -c Release --filter "FullyQualifiedName~SettingsMigration|FullyQualifiedName~SettingsStore"
```

建议提交：

```text
feat: migrate legacy InboxDock settings safely
```

## 阶段二：模板、路径和通用写入

### 任务 3：实现有限模板渲染

目标：支持设计文档批准的有限变量，不引入脚本执行。

新增文件：

- `src/InboxDock.Core/Templates/TemplateContext.cs`
- `src/InboxDock.Core/Templates/TemplateRenderer.cs`
- `src/InboxDock.Core/Templates/TemplateRenderResult.cs`
- `tests/InboxDock.Core.Tests/Templates/TemplateRendererTests.cs`

实施步骤：

1. 支持 `content`、`title`、`url`、`note`、`files`、`date`、`time`、`timestamp`、`source` 和 `target`。
2. 支持 `{{date:yyyy-MM-dd}}` 和 `{{time:HH:mm}}` 形式的格式。
3. 使用传入的 `DateTimeOffset`，测试不得依赖系统当前时间。
4. 未知变量、非法日期格式和必要值缺失返回结构化错误，不原样静默写入 Vault。
5. 普通正文内容不进行二次模板解析，避免用户输入中的 `{{...}}` 被误处理。
6. 不支持循环、条件、函数、环境变量和文件读取。

测试：

- 中英文正文和文件名变量正确渲染。
- 日期格式使用本地时间但测试可固定时钟。
- 未知变量返回变量名和位置。
- 正文中的模板样式文字不会被再次执行。
- 文件列表生成稳定 Markdown。

运行：

```powershell
dotnet test tests/InboxDock.Core.Tests/InboxDock.Core.Tests.csproj -c Release --filter "FullyQualifiedName~TemplateRenderer"
```

建议提交：

```text
feat: render safe capture templates
```

### 任务 4：实现目标路径解析与安全校验

目标：根据收集目标计算笔记和附件路径，并保证最终路径始终位于 Vault 内。

新增文件：

- `src/InboxDock.Core/Targets/TargetPathResolver.cs`
- `src/InboxDock.Core/Targets/ResolvedTargetPaths.cs`
- `src/InboxDock.Core/Targets/TargetValidationResult.cs`
- `tests/InboxDock.Core.Tests/Targets/TargetPathResolverTests.cs`

修改文件：

- `src/InboxDock.Core/Capture/SafeName.cs`
- `src/InboxDock.Core/Vault/VaultLayout.cs`
- `tests/InboxDock.Core.Tests/Vault/VaultLayoutTests.cs`

实施步骤：

1. 将现有 `VaultLayout.ResolveRelative` 的安全能力提取或复用于新解析器。
2. 固定文件目标解析到一个 Markdown 文件。
3. 周期文件目标先渲染日期路径，再补全 `.md`。
4. 新笔记目标分别渲染目录和文件名。
5. 附件策略独立解析，不允许绝对路径、盘符或 `..` 离开 Vault。
6. 重名时生成 `-2`、`-3` 等稳定名称，不覆盖已有文件。
7. 检查 Windows 保留名称、非法字符、空文件名和最终路径长度。
8. `FollowObsidian` 暂时只接受已经由发现服务解析出的 Vault 相对目录，不在解析器内读取 `.obsidian` 文件。

测试：

- 四种写入方式产生预期路径。
- 中文、空格和 Emoji 路径可用。
- `../`、绝对路径、UNC 和跨盘路径被拒绝。
- 同名笔记和附件不覆盖。
- 日期目录正确创建。
- 跟随笔记附件目录不会离开 Vault。

运行：

```powershell
dotnet test tests/InboxDock.Core.Tests/InboxDock.Core.Tests.csproj -c Release --filter "FullyQualifiedName~TargetPathResolver|FullyQualifiedName~VaultLayout|FullyQualifiedName~SafeName"
```

建议提交：

```text
feat: resolve target paths inside the vault
```

### 任务 5：实现无副作用写入预览

目标：在实际写入前展示最终路径、附件位置和 Markdown。

新增文件：

- `src/InboxDock.Core/Targets/CapturePreview.cs`
- `src/InboxDock.Core/Targets/CapturePreviewService.cs`
- `tests/InboxDock.Core.Tests/Targets/CapturePreviewServiceTests.cs`

实施步骤：

1. 预览组合材料、目标、模板和路径解析结果。
2. 预览不得创建目录、复制附件或写入文件。
3. 输出目标显示名称、笔记绝对路径、附件绝对路径列表和 Markdown。
4. 返回是否必须确认及原因：新目标、目标版本变化、冲突或配置异常。
5. 将用户可读错误与内部异常分开，UI 只显示前者。

测试：

- 生成预览后文件系统无变化。
- 新目标和修改后的目标要求确认。
- 未修改且验证过的目标允许快速保存。
- 模板或路径错误阻止写入。

运行：

```powershell
dotnet test tests/InboxDock.Core.Tests/InboxDock.Core.Tests.csproj -c Release --filter "FullyQualifiedName~CapturePreview"
```

建议提交：

```text
feat: preview capture target output
```

### 任务 6：实现通用目标写入服务

目标：用一个安全写入流程覆盖固定文件追加、周期文件追加、新建笔记和只暂存。

新增文件：

- `src/InboxDock.Core/Targets/TargetWriteService.cs`
- `src/InboxDock.Core/Targets/TargetWriteRequest.cs`
- `src/InboxDock.Core/Targets/TargetWriteResult.cs`
- `tests/InboxDock.IntegrationTests/TargetWriteServiceTests.cs`

修改文件：

- `src/InboxDock.Core/IO/AtomicFile.cs`
- `src/InboxDock.Core/History/UndoService.cs`
- `src/InboxDock.Core/Capture/CaptureModels.cs`
- `src/InboxDock.Core/Capture/InboxCaptureService.cs`
- `src/InboxDock.Core/Daily/DailyCaptureService.cs`
- `tests/InboxDock.IntegrationTests/CaptureWorkflowTests.cs`

实施步骤：

1. 使用预览产生的经过验证的路径和 Markdown，写入前再次确认 Vault 状态。
2. 追加目标读取现有内容后通过 `AtomicFile.ReplaceTextAsync` 替换，禁止直接不受控追加。
3. 新建笔记使用唯一文件名并原子创建。
4. 附件先复制到临时名称，全部完成后再提交最终文件名。
5. 任一附件或 Markdown 步骤失败时回滚本次已创建内容；无法自动回滚的文件移入 Recovery。
6. `StagingOnly` 不触碰 Vault，返回明确结果。
7. 在写入内容中保留唯一 InboxDock 标记，支持安全撤销。
8. 扩展 `UndoService`：
   - 追加内容只移除本次标记段。
   - 新建笔记和附件移动到 Recovery，不永久删除。
   - 用户修改过标记区域时拒绝危险撤销。
9. 现有 `InboxCaptureService` 和 `DailyCaptureService` 暂时作为兼容包装器，确保旧测试持续通过；待 UI 完全切换后再清理重复实现。

测试：

- 四种写入模式的集成测试。
- 原文件始终不被移动或删除。
- 多文件中途失败时不留下半完成笔记。
- 追加目标保留已有用户内容。
- 同名附件不覆盖。
- 撤销只影响本次写入。
- 用户修改后危险撤销被拒绝。
- `StagingOnly` 不创建任何 Vault 文件。

运行：

```powershell
dotnet test tests/InboxDock.IntegrationTests/InboxDock.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~TargetWriteService|FullyQualifiedName~CaptureWorkflow"
```

建议提交：

```text
feat: write captures through configurable targets
```

## 阶段三：暂存材料与目标选择

### 任务 7：扩展暂存模型并保持旧 JSON 兼容

目标：让暂存卡片记录首选目标和错误状态，但不保存已解析绝对路径。

修改文件：

- `src/InboxDock.Core/Staging/StagedMaterial.cs`
- `src/InboxDock.Core/Staging/StagingSnapshot.cs`
- `src/InboxDock.Core/Staging/StagingStore.cs`
- `src/InboxDock.Core/Staging/MaterialStagingService.cs`
- `src/InboxDock.Core/Staging/StagedCaptureService.cs`
- `tests/InboxDock.Core.Tests/Staging/StagingStoreTests.cs`
- `tests/InboxDock.IntegrationTests/MaterialStagingServiceTests.cs`
- `tests/InboxDock.IntegrationTests/StagedCaptureServiceTests.cs`

实施步骤：

1. 为材料增加可空 `PreferredTargetId`。
2. 保留现有材料种类、备注、文件和状态字段。
3. 旧 JSON 缺少目标字段时正常加载。
4. 增加更新首选目标的方法，并继续使用内存快照避免每次重载 JSON。
5. 目标不存在时材料仍能打开，UI 要求重新选择目标。
6. 将确认动作改为接收目标写入服务和目标 ID，不再固定调用 Inbox。
7. 增加批量确认的 Core 方法，逐项记录结果；失败项保留，成功项移除。

测试：

- 旧暂存 JSON 加载成功。
- 选择目标后跨重启保留。
- 删除目标不会删除材料。
- 批量操作中部分失败不会影响其他卡片。
- 失败错误可在再次重试后清除。

运行：

```powershell
dotnet test InboxDock.sln -c Release --filter "FullyQualifiedName~Staging|FullyQualifiedName~StagedCapture"
```

建议提交：

```text
feat: route staged materials to capture targets
```

## 阶段四：发现、首次配置和设置界面

### 任务 8：只读发现 Obsidian 配置

目标：读取足够的 Vault 配置用于首次建议，不扫描笔记正文、不修改 `.obsidian`。

新增文件：

- `src/InboxDock.Core/Vault/VaultDiscovery.cs`
- `src/InboxDock.Core/Vault/VaultDiscoveryResult.cs`
- `tests/InboxDock.Core.Tests/Vault/VaultDiscoveryTests.cs`

实施步骤：

1. 验证 Vault 后读取 Obsidian 默认附件设置。
2. 如果存在并可解析，读取 Daily Notes 的目录和文件名格式。
3. 对缺失、禁用、未知格式和损坏配置返回建议缺失，而不是阻止使用。
4. 不搜索整个 Vault 中名字类似 Inbox 的文件。
5. 不写入 `.obsidian` 目录。

测试：

- 默认附件目录识别。
- Daily Notes 配置识别。
- 无配置时返回空建议。
- 损坏 JSON 不抛出未处理异常。
- 调用后配置文件时间和内容不变。

运行：

```powershell
dotnet test tests/InboxDock.Core.Tests/InboxDock.Core.Tests.csproj -c Release --filter "FullyQualifiedName~VaultDiscovery"
```

建议提交：

```text
feat: discover safe Obsidian vault defaults
```

### 任务 9：实现两步首次配置

目标：新用户只需选择 Vault 和默认保存方式。

新增文件：

- `src/InboxDock.App/Views/OnboardingWindow.xaml`
- `src/InboxDock.App/Views/OnboardingWindow.xaml.cs`
- `src/InboxDock.App/ViewModels/OnboardingViewModel.cs`

修改文件：

- `src/InboxDock.App/App.xaml.cs`
- `src/InboxDock.App/ViewModels/MainViewModel.cs`
- `tests/InboxDock.Core.Tests/Configuration/SettingsStoreTests.cs`

实施步骤：

1. 设置不存在时打开首次配置，不直接显示未配置主窗口。
2. 第一步选择并验证 Vault。
3. 第二步选择追加到现有文件、在目录创建新笔记或只暂存。
4. 检测到 Daily Notes 时提供一个默认未强迫的“添加今日日记”选项。
5. 完成前显示将创建的目标摘要。
6. 保存失败时留在当前步骤，不进入空主界面。
7. 用户取消时允许退出程序，不写半完成设置。

验证：

- 全新本地数据目录下完成配置。
- 无 Daily Notes 配置时流程仍为两步。
- 无效 Vault 无法进入下一步。
- 取消后不创建设置文件。

建议提交：

```text
feat: guide first-time vault setup
```

### 任务 10：实现精简设置与目标管理

目标：设置只有 Vault、收集目标、窗口与快捷键、关于与诊断四部分。

新增文件：

- `src/InboxDock.App/Views/SettingsWindow.xaml`
- `src/InboxDock.App/Views/SettingsWindow.xaml.cs`
- `src/InboxDock.App/ViewModels/SettingsViewModel.cs`
- `src/InboxDock.App/ViewModels/CaptureTargetEditorViewModel.cs`

修改文件：

- `src/InboxDock.App/MainWindow.xaml`
- `src/InboxDock.App/MainWindow.xaml.cs`
- `src/InboxDock.App/ViewModels/MainViewModel.cs`

实施步骤：

1. 将当前“选择 Vault”按钮改为打开设置窗口。
2. 实现目标新建、编辑、删除、设为默认和排序。
3. 编辑器先选择四种写入方式，再显示对应的最少字段。
4. 模板、标题插入和附件高级选项默认折叠。
5. 删除默认目标前要求先选择新默认目标。
6. 删除仍被暂存材料引用的目标时明确说明材料会保留但需重新选择。
7. 保存前调用 Core 验证，不把无效目标写入设置。
8. 设置保存成功后通知主 ViewModel 重新加载服务，不要求重启应用。

验证：

- 创建四类目标。
- 修改后主界面立即更新。
- 删除目标不删除暂存卡片。
- 关闭设置不保存未确认修改。

建议提交：

```text
feat: manage vault capture targets
```

## 阶段五：主收集交互

### 任务 11：在主界面选择目标并接入预览

目标：替换固定“收进 Inbox”，同时保持快速收集流程。

修改文件：

- `src/InboxDock.App/MainWindow.xaml`
- `src/InboxDock.App/MainWindow.xaml.cs`
- `src/InboxDock.App/ViewModels/MainViewModel.cs`
- `src/InboxDock.App/Converters/StagingConverters.cs`

可能新增文件：

- `src/InboxDock.App/Views/CapturePreviewDialog.xaml`
- `src/InboxDock.App/Views/CapturePreviewDialog.xaml.cs`
- `src/InboxDock.App/ViewModels/CapturePreviewViewModel.cs`

实施步骤：

1. 输入区和确认面板显示当前目标选择器。
2. 默认选中材料首选目标，否则使用 Vault 默认目标。
3. 确认按钮文案改为“保存到目标名称”。
4. 第一次使用或修改过的目标显示预览；正常目标直接写入。
5. 写入成功后记录该材料类型最近使用目标，但 v0.3.0 不做自动规则推荐。
6. “只暂存”目标直接关闭确认面板并保留卡片。
7. “在 Obsidian 中打开”使用实际结果路径，不再硬编码 `00 Inbox收件箱`。
8. 保留 Daily 页现有入口到目标系统完成后，再将其映射为“今日日记”目标；迁移期间不得同时写两次。

验证：

- 文字、链接、图片和文件可分别保存到不同目标。
- 目标切换跨暂存确认保持。
- 新目标出现一次预览，确认后正常快速保存。
- 写入失败不关闭窗口、不移除卡片。

建议提交：

```text
feat: select capture targets from the dock
```

### 任务 12：实现基本批量处理和状态挂件

目标：让用户看到待处理数量和失败状态，并能安全批量写入。

修改文件：

- `src/InboxDock.App/MainWindow.xaml`
- `src/InboxDock.App/MainWindow.xaml.cs`
- `src/InboxDock.App/ViewModels/MainViewModel.cs`
- `src/InboxDock.App/Converters/StagingConverters.cs`
- `src/InboxDock.Core/Windowing/WindowDockCalculator.cs`
- `tests/InboxDock.Core.Tests/Windowing/WindowDockCalculatorTests.cs`

实施步骤：

1. 贴边把手显示待处理数量；数量过大时使用 `99+`。
2. 任一材料失败时显示错误状态，但不持续闪烁。
3. 材料列表顶部显示总数和最早收集时间。
4. 增加选择状态和“保存选中项到当前目标”。
5. 批量删除仍使用二次确认，并明确只删除暂存副本。
6. 批量处理中禁止自动收边，完成后总结成功和失败数量。
7. 不增加复杂筛选、搜索或游戏化提醒。

验证：

- 0、1、9、99、100 项数量显示。
- 四边贴靠时徽标不超出屏幕。
- 批量部分失败时失败卡片保留。
- 原文件不受批量删除影响。

建议提交：

```text
feat: show staged counts and batch capture
```

## 阶段六：窗口设置与 Windows 集成

### 任务 13：将自动收回改为可配置并增加图钉状态

目标：默认 5 秒，支持 10 秒、30 秒和永不自动收回。

修改文件：

- `src/InboxDock.Core/Windowing/AutoPeekController.cs`
- `tests/InboxDock.Core.Tests/Windowing/AutoPeekControllerTests.cs`
- `src/InboxDock.App/MainWindow.xaml`
- `src/InboxDock.App/MainWindow.xaml.cs`
- `src/InboxDock.App/ViewModels/SettingsViewModel.cs`

实施步骤：

1. 从当前设置创建或更新 `AutoPeekController`，移除固定 `TimeSpan.FromSeconds(10)`。
2. 默认值设为 5 秒。
3. “永不”时停止空闲收边判断，但保留手动收边。
4. 图钉开启时暂停自动收边，关闭后重新开始完整倒计时。
5. 写入成功后通过独立的短延时触发收边；失败、撤销悬停和批量处理中不触发。
6. 保持现有输入、拖放、确认和设置暂停条件。

测试：

- 5、10、30 秒边界。
- 永不自动收边。
- 固定和解除固定。
- 暂停结束后重新计算完整时间。
- 失败状态不收边。

运行：

```powershell
dotnet test tests/InboxDock.Core.Tests/InboxDock.Core.Tests.csproj -c Release --filter "FullyQualifiedName~AutoPeekController"
```

建议提交：

```text
feat: configure automatic edge hiding
```

### 任务 14：实现单实例与自定义全局快捷键

目标：系统任意位置呼出 InboxDock，重复启动复用现有实例。

新增文件：

- `src/InboxDock.App/SystemIntegration/SingleInstanceService.cs`
- `src/InboxDock.App/SystemIntegration/GlobalHotkeyService.cs`
- `src/InboxDock.Core/SystemIntegration/HotkeyGesture.cs`
- `tests/InboxDock.Core.Tests/SystemIntegration/HotkeyGestureTests.cs`

修改文件：

- `src/InboxDock.App/App.xaml.cs`
- `src/InboxDock.App/MainWindow.xaml.cs`
- `src/InboxDock.App/ViewModels/SettingsViewModel.cs`
- `src/InboxDock.Core/Configuration/VaultProfile.cs`

实施步骤：

1. 用命名互斥体保证单实例。
2. 第二实例通过命名管道或 Windows 消息通知第一实例展开并聚焦，然后退出。
3. 默认快捷键为 `Ctrl+Shift+Space`。
4. 设置界面捕获真实按键组合，不使用自由文本框。
5. 禁止只有修饰键、常见系统保留组合和无法注册的按键。
6. 更新快捷键时先尝试注册新组合；成功后再释放旧组合并保存设置。
7. 应用退出时释放快捷键、消息钩子和单实例资源。

测试与验证：

- 快捷键字符串和按键模型往返。
- 无效组合验证。
- 手工确认快捷键在浏览器、资源管理器和 Obsidian 外呼出应用。
- 重复启动只保留一个进程和一个托盘图标。
- 快捷键冲突时旧设置仍可用。

建议提交：

```text
feat: add configurable global hotkey
```

### 任务 15：实现开机自启动开关

目标：让用户在设置中可靠开启、关闭和修复登录启动项。

新增文件：

- `src/InboxDock.App/SystemIntegration/LaunchAtSignInService.cs`
- `src/InboxDock.App/SystemIntegration/LaunchAtSignInStatus.cs`
- `src/InboxDock.Core/SystemIntegration/LaunchAtSignInCommand.cs`
- `tests/InboxDock.Core.Tests/SystemIntegration/LaunchAtSignInCommandTests.cs`

修改文件：

- `src/InboxDock.App/ViewModels/SettingsViewModel.cs`
- `src/InboxDock.App/Views/SettingsWindow.xaml`
- `src/InboxDock.Core/Configuration/VaultProfile.cs`

实施步骤：

1. 使用当前用户级 Windows 登录启动机制，不要求管理员权限。
2. 写入带引号的当前可执行文件绝对路径，处理路径空格。
3. 开关默认关闭。
4. 开启前检查当前运行路径存在且可执行。
5. 注册成功后才保存 `LaunchAtSignIn=true`。
6. 关闭时只移除 InboxDock 创建的启动项。
7. 启动时比较注册路径和当前路径；便携版移动后显示“需要修复”。
8. 安装版卸载脚本移除启动项，但默认保留用户数据。
9. 注册或移除失败时恢复 UI 开关并显示用户可理解错误。

测试与验证：

- 将注册状态计算和命令格式提取为可测试纯逻辑。
- 手工开启后注销或重启 Windows，确认 InboxDock 启动。
- 关闭后再次登录不启动。
- 路径含空格时启动成功。
- 移动便携版后检测旧路径失效并可修复。

建议提交：

```text
feat: manage launch at Windows sign-in
```

## 阶段七：反馈、诊断和更新

### 任务 16：完善成功、失败和撤销反馈

目标：让用户明确知道内容是否安全写入。

修改文件：

- `src/InboxDock.App/MainWindow.xaml`
- `src/InboxDock.App/MainWindow.xaml.cs`
- `src/InboxDock.App/ViewModels/MainViewModel.cs`
- `src/InboxDock.Core/History/UndoService.cs`
- `tests/InboxDock.IntegrationTests/CaptureWorkflowTests.cs`

实施步骤：

1. 成功反馈显示实际目标名称。
2. 提供撤销和打开最终笔记。
3. 失败文案明确“材料仍安全保存在 InboxDock”。
4. 提供重试、更换目标和复制简化错误。
5. 撤销完成后刷新材料和按钮状态。
6. 错误信息不包含笔记正文和附件内容。

验证：

- 成功、失败、撤销三条路径。
- 用户修改目标笔记后危险撤销被拒绝。
- 失败窗口保持展开。

建议提交：

```text
feat: clarify capture success and recovery
```

### 任务 17：实现隐私安全的日志和诊断

目标：用户可以自行定位问题并复制安全诊断信息。

新增文件：

- `src/InboxDock.Core/Diagnostics/DiagnosticSnapshot.cs`
- `src/InboxDock.Core/Diagnostics/DiagnosticRedactor.cs`
- `src/InboxDock.App/Diagnostics/AppLog.cs`
- `tests/InboxDock.Core.Tests/Diagnostics/DiagnosticRedactorTests.cs`

修改文件：

- `src/InboxDock.App/App.xaml.cs`
- `src/InboxDock.App/ViewModels/SettingsViewModel.cs`
- `src/InboxDock.App/Views/SettingsWindow.xaml`

实施步骤：

1. 替换当前只写 `startup.log` 的方式，增加简单滚动日志或限制日志文件大小。
2. 日志记录版本、时间、操作类别和错误类型，不记录正文、URL 查询内容、文件内容和剪贴板。
3. 诊断信息包含版本、Windows、架构、Vault 是否存在和可写、暂存数量、最近错误类型。
4. 完整私人路径进行遮蔽。
5. 设置页提供打开日志目录、打开暂存目录和复制诊断信息。
6. 上次异常退出时提示暂存已恢复，不强制弹出堆栈。

测试：

- 路径遮蔽。
- 诊断输出不包含测试正文和文件名秘密标记。
- 超长错误消息截断。

建议提交：

```text
feat: add privacy-safe diagnostics
```

### 任务 18：实现非阻塞 GitHub 更新检查

目标：每天最多检查一次，不影响离线收集。

新增文件：

- `src/InboxDock.Core/Updates/VersionInfo.cs`
- `src/InboxDock.Core/Updates/VersionComparer.cs`
- `src/InboxDock.App/Updates/UpdateCheckService.cs`
- `tests/InboxDock.Core.Tests/Updates/VersionComparerTests.cs`

修改文件：

- `src/InboxDock.App/App.xaml.cs`
- `src/InboxDock.App/ViewModels/SettingsViewModel.cs`
- `src/InboxDock.App/Views/SettingsWindow.xaml`
- `src/InboxDock.Core/Configuration/VaultProfile.cs`

实施步骤：

1. 从程序集元数据读取当前版本。
2. 查询 GitHub 最新 Release，只读取版本、名称和 URL。
3. 使用短超时和可取消请求，失败不弹阻塞错误。
4. 记录上次检查时间，每天最多自动检查一次。
5. 用户可关闭自动检查，也可手动检查。
6. 有新版时提示并打开 Release 页面，不自动下载或安装。
7. 对预发布版本默认不提醒，除非当前版本本身是预发布。

测试：

- 语义版本比较。
- 当前版本、较新版本、较旧版本和预发布版本。
- 网络失败由服务返回无更新状态，不抛到主线程。

建议提交：

```text
feat: check GitHub releases for updates
```

## 阶段八：安装、CI 和公开发布

### 任务 19：增加 Windows 安装包

目标：同时提供安装版和便携版，卸载默认保留数据。

新增文件：

- `installer/InboxDock.iss`
- `installer/LICENSE.txt` 或指向仓库 `LICENSE` 的安装配置
- `scripts/build-release.ps1`

修改文件：

- `src/InboxDock.App/InboxDock.App.csproj`
- `.gitignore`
- `README.md`

实施步骤：

1. 使用 Inno Setup 6 生成 `InboxDock-Setup-<version>.exe`。
2. 安装到当前用户可写的标准程序目录，第一版不要求管理员权限。
3. 创建开始菜单入口，桌面快捷方式为可选任务。
4. 卸载时移除应用文件、开始菜单、桌面快捷方式和 InboxDock 创建的启动项。
5. 默认保留 `%LocalAppData%\InboxDock`；只有用户明确选择时才删除设置和暂存材料。
6. 同一脚本同时产出便携 ZIP。
7. 生成 `SHA256SUMS.txt`。
8. 从项目版本生成文件名，避免手工写多个不一致版本号。

验证：

- 干净环境安装、启动和卸载。
- 安装路径含空格。
- 开机自启动开启后卸载清理。
- 卸载后用户数据仍存在。
- 便携版无需安装即可运行。

建议提交：

```text
build: package InboxDock installer and portable app
```

### 任务 20：升级 CI 和 Release 工作流

目标：`v*` 标签自动测试、构建、打包并发布所有产物。

修改文件：

- `.github/workflows/ci.yml`
- `.github/workflows/release.yml`
- `Directory.Build.props`
- `README.md`
- 新增 `CHANGELOG.md`

实施步骤：

1. 在 `Directory.Build.props` 维护单一版本来源或允许标签注入版本。
2. CI 保持 Windows Release restore、build 和 test。
3. Release 工作流安装 Inno Setup，运行统一发布脚本。
4. 上传 Setup、Portable 和 SHA256 文件。
5. 发布前验证标签格式与程序集版本一致。
6. Release Notes 使用变更日志内容，不再只写模糊一句话。
7. README 更新仓库定位、截图、30 秒上手、隐私、安装选择和已知限制。
8. 如果重命名 GitHub 仓库，先确认远程 URL、Actions、Release 链接和更新检查地址全部更新。

验证：

- 在测试标签或本地等价脚本中验证产物名称。
- 解压 Portable 并启动。
- 安装 Setup 并启动。
- SHA-256 与文件匹配。
- GitHub Actions 使用最小必要权限。

建议提交：

```text
ci: publish versioned InboxDock releases
```

## 阶段九：完整回归与公开测试

### 任务 21：完成自动化回归

目标：确保产品化改造没有破坏当前收集、暂存、Daily、窗口和安全行为。

实施步骤：

1. 运行完整 Core 与 Integration 测试。
2. 修复所有警告，Release 构建要求 0 warnings、0 errors。
3. 检查旧测试是否仍在验证真实行为，而不是被无意义放宽。
4. 新增至少一个从旧设置和旧暂存开始的端到端升级测试。
5. 检查 `git status`，不提交用户本地数据、日志、发布目录或测试 Vault。

命令：

```powershell
dotnet restore InboxDock.sln
dotnet build InboxDock.sln -c Release --no-restore
dotnet test InboxDock.sln -c Release --no-build
```

建议提交：

```text
test: cover InboxDock v0.3 upgrade workflow
```

### 任务 22：执行 Windows 人工验收

目标：验证自动测试无法覆盖的系统集成与视觉行为。

验收清单：

- 全新安装。
- 从当前公开旧版升级。
- 旧设置和暂存材料迁移。
- 安装版卸载和重装。
- 便携版移动目录后的自启动修复。
- Obsidian 完全关闭时收集。
- 中文、空格、Emoji 和较长路径。
- 文字、链接、图片、单文件和多文件。
- 四种收集目标。
- 5、10、30 秒和永不收回。
- 图钉固定。
- 自定义快捷键和冲突提示。
- 第二次启动复用现有实例。
- 开机自启动开启、关闭和卸载清理。
- 四边贴靠和多显示器。
- 100%、125%、150% 缩放。
- Vault 移动、只读、文件占用和无网络。
- 写入失败后重启恢复材料。
- 成功撤销及用户修改后的安全拒绝。
- 安装包、便携包和 SHA-256。

将结果写入：

- `docs/testing/2026-07-23-v0.3.0-release-validation.md`

每项记录“通过、失败、环境阻塞、未验证”，不能把环境阻塞写成通过。

### 任务 23：小范围公开测试

目标：在正式 v1.0 前验证真实用户能否理解和信任产品。

步骤：

1. 发布 `v0.3.0-beta.1` 或明确标注的 v0.3.0 公开测试版。
2. 邀请 3 至 5 位 Windows Obsidian 用户，尽量覆盖不同 Vault 结构。
3. 只收集以下反馈：
   - 是否在两分钟内完成配置。
   - 是否理解“暂存”和“写入 Vault”的区别。
   - 是否成功创建自定义目标。
   - 是否信任文件不会丢失。
   - 快捷键和 5 秒收回是否舒服。
   - 安装和卸载是否清楚。
4. 不加入遥测；通过 Issue 或用户主动反馈收集结果。
5. 将阻止日常使用的问题修复为 v0.3.1，不把新点子全部塞回 v0.3.0。

## 推荐执行批次

为降低连续大改风险，建议按以下批次执行并在每批结束后完整测试：

### 批次 A：数据基础

任务 1 至 7。完成后应具备迁移、模板、路径、预览、通用写入和暂存目标，但 UI 可以暂时仍使用兼容入口。

### 批次 B：用户配置

任务 8 至 12。完成后新用户可配置 Vault 和目标，主界面可以真正使用通用目标。

### 批次 C：Windows 产品体验

任务 13 至 18。完成后具备 5 秒可配置收回、图钉、快捷键、单实例、自启动、反馈、诊断和更新检查。

### 批次 D：发布闭环

任务 19 至 23。完成安装、CI、人工验收和小范围公开测试。

## 完成定义

v0.3.0 只有在以下条件全部满足时才算完成：

- 主界面不再依赖固定 Inbox、Daily 或附件路径。
- 当前用户设置和暂存材料自动迁移且有备份。
- Obsidian 关闭时所有本地收集能力正常。
- 默认 5 秒收回，可选择 10 秒、30 秒或永不。
- 全局快捷键可由用户配置并处理冲突。
- 开机自启动可开启、关闭、检测失效和卸载清理。
- 写入失败不丢材料、不自动收边。
- 成功结果可撤销并打开实际目标笔记。
- 安装版、便携版和校验文件可重复构建。
- Release 构建 0 warnings、0 errors，完整测试通过。
- 人工验收结果有书面记录。
- 真实试用中的阻塞问题已解决或明确列为已知限制。
