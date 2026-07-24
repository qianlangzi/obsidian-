# InboxDock

![InboxDock icon](assets/inboxdock.png)

InboxDock 是一个常驻 Windows 桌面的 Obsidian 材料桶。Obsidian 没有打开时，也可以先把文件、链接和文字放进材料桶；你确认后，它才会按你配置的目标写入 Vault。

> 当前版本 v0.3.0 公开测试版。欢迎通过 [GitHub Issues](https://github.com/qianlangzi/InboxDock/issues) 提交问题和建议。

## 30 秒上手

1. 从 [Releases](https://github.com/qianlangzi/InboxDock/releases) 下载安装版或便携版。
2. 运行后选择你的 Obsidian Vault。
3. 选择默认保存方式（追加到文件、创建新笔记或只暂存）。
4. 把文件拖进去，或粘贴链接、图片、文字。
5. 按 `Ctrl+Shift+Space` 随时呼出。

## 它能做什么

- **可配置收集目标**：追加到固定文件、追加到周期文件、创建新笔记或只暂存
- **全局快捷键**：`Ctrl+Shift+Space` 系统任意位置呼出
- **自动收边**：5 秒/10 秒/30 秒可配置，图钉置顶时暂停
- **批量处理**：多选卡片批量写入或移除
- **安全写入**：原文件不被移动，失败保留材料，可撤销
- **隐私安全**：本地运行，无账号、无遥测、无网络上传
- **开机自启动**：设置中一键开启
- **更新检查**：非阻塞检查 GitHub 最新发布

## 安装

### 安装版（推荐）

1. 下载 `InboxDock-Setup-<version>.exe`。
2. 运行安装程序。
3. 从开始菜单或桌面启动 InboxDock。

### 便携版

1. 下载 `InboxDock-<version>-portable-win-x64.zip`。
2. 解压到一个固定目录。
3. 双击运行 `InboxDock.exe`。

### 校验下载

下载 `SHA256SUMS.txt`，使用以下命令校验：

```powershell
Get-FileHash InboxDock-Setup-<version>.exe -Algorithm SHA256
# 与 SHA256SUMS.txt 中的值对比
```

## 日常使用

### 收集

- **拖入文件**：拖到收集页面任意区域，自动生成一张待确认卡片
- **粘贴**：`Ctrl+V` 粘贴剪贴板图片、复制的文件或网页链接
- **写文字**：底部文字框写内容，`Ctrl+Enter` 提交

### 确认

- 选择目标后点击"保存到 [目标名称]"
- 首次使用或修改的目标会显示预览
- 失败后可重试或换目标，材料不丢失

### 暂存

- 选择"先暂存"后材料跨重启保留
- 点击卡片可重新打开确认

### 批量

- 点击列表右上角"批量"切换批量模式
- 点击卡片选择，底部操作栏可全选、清空、保存选中或移除选中

### 撤销

- 写入成功后底部撤销按钮可用
- 撤销只移除本次写入标记，用户修改的内容保留

## 设置

点击右上角设置图标打开设置窗口：

- **Vault**：显示当前 Vault 路径
- **收集目标**：新建、编辑、删除、设为默认、排序
- **自动收回**：5 秒 / 10 秒 / 30 秒 / 永不
- **全局快捷键**：捕获真实按键组合
- **开机自启动**：一键开启，便携版移动后提示修复
- **诊断与日志**：打开日志/暂存目录，复制诊断信息

## 数据存储

InboxDock 自己的本地数据：

| 类型 | 位置 |
|------|------|
| 设置 | `%LocalAppData%\InboxDock\settings.json` |
| 窗口位置 | `%LocalAppData%\InboxDock\window-state.json` |
| 暂存材料 | `%LocalAppData%\InboxDock\Staging` |
| 日志 | `%LocalAppData%\InboxDock\Logs` |
| 目标确认 | `%LocalAppData%\InboxDock\confirmations.json` |

卸载时默认保留以上数据。

## 隐私和安全

- 原始拖入文件不被移动或删除
- 文件完整复制到暂存区后卡片才出现
- 目标重名时自动增加编号，不覆盖已有文件
- Vault 相对路径必须保持在 Vault 根目录内
- 写入失败时保留卡片和错误信息，方便重试
- 日志不记录笔记正文、URL 查询内容、文件内容和剪贴板
- 诊断信息路径已遮蔽用户名
- 无账号、无遥测、无网络上传

## 开发

需要 Windows 和 .NET 10 SDK。

```powershell
dotnet restore InboxDock.sln
dotnet build InboxDock.sln
dotnet test InboxDock.sln
dotnet run --project src/InboxDock.App
```

发布全部产物（便携 ZIP + 安装包 + SHA256）：

```powershell
.\scripts\build-release.ps1 -Version 0.3.0
```

仅发布便携版：

```powershell
dotnet publish src/InboxDock.App -c Release -r win-x64 --self-contained true -o artifacts/publish
Compress-Archive artifacts/publish/* artifacts/InboxDock-portable-win-x64.zip
```

## 已知限制

- v0.3.0 不支持 OCR、语音、AI 和脚本系统
- 更新检查只提示，不自动下载或安装
- 仅支持 Windows x64

## License

MIT
