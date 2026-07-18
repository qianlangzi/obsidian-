# InboxDock

![InboxDock icon](assets/inboxdock.png)

InboxDock 是一个常驻 Windows 桌面的 Obsidian 材料桶。Obsidian 没有打开时，也可以先把文件、链接和文字放进材料桶；你确认后，它才会写入 Vault 的 Inbox。

> 当前是首个公开版本，功能仍会继续完善。欢迎通过 GitHub Issues 提交问题和建议。

## 它能做什么

- 展开后 10 秒无操作自动滑入屏幕边缘，只留下一个小箭头
- 从左、右、上、下边缘向屏幕内部展开，不会固定从左上角变大
- 在整个收集页面拖入一个或多个文件，自动生成一张待确认卡片
- 粘贴剪贴板图片、复制的文件或网页链接，自动识别并询问是否收进 Inbox
- 在材料桶底部写文字，按 `Ctrl+Enter` 进入确认
- 给一个或多个文件添加共享备注，再一次收进 Inbox
- 选择“先暂存”后，材料会跨重启保留
- 按完成、学习、问题、灵感追加到今天的 Daily
- 本地运行，无账号、无遥测、无网络上传

## 安装

1. 从 GitHub Releases 下载 `InboxDock-win-x64.zip`。
2. 解压到一个固定目录。
3. 双击运行 `InboxDock.exe`。
4. 点击右上角设置图标，选择包含 `.obsidian` 的 Vault 根目录。

关闭主窗口只会把 InboxDock 隐藏到系统托盘。右键托盘图标并选择“退出”，才会完全结束程序。

## 日常怎么用

### 收集文件

把一个或多个文件拖到“收集”页面的任意区域。拖入时会显示绿色提示层；InboxDock 会先把文件复制到自己的本地暂存区，并马上弹出确认：

- **收进 Inbox**：把暂存副本写入 Vault，然后从材料桶移除卡片。
- **先暂存**：暂时不写入 Vault，卡片留在材料桶里，下次打开仍然存在。

原文件不会被移动或删除。一次拖入多个文件时，它们会组成一张卡片、共享一条可选备注，并通过一次确认一起写入 Inbox。

### 粘贴图片或复制的文件

先在截图工具、浏览器或资源管理器里复制图片或文件，再把光标放到底部文字框并按 `Ctrl+V`。图片会转换为本地 PNG 暂存材料；复制的多个文件会组成一个文件组。所有内容都只在本机处理。

### 收集链接

在材料桶底部的文字框里直接粘贴以 `http://` 或 `https://` 开头的网址。链接会自动变成卡片并弹出确认，不需要再点提交按钮。

### 写文字笔记

在底部白色文字框里写内容：

- `Enter`：正常换行。
- `Ctrl+Enter`：把文字变成卡片并弹出确认。

没有按 `Ctrl+Enter` 的草稿也会自动保存在本机，退出程序不会丢失。

### 处理暂存卡片

点击一张“待处理”或“收集失败”的卡片，可以重新打开确认。垃圾桶按钮需要再次确认，只删除 InboxDock 自己的暂存副本，不会删除原文件。

### 写 Daily

切换到“今日”，选择完成、学习、问题或灵感，写下内容后点击“追加到今天的 Daily”。这部分流程和旧版本一致。

## 自动收边、拖动和吸附

- 拖动标题栏并松开后，窗口会吸附到当前显示器最近的左、右、上或下边缘。
- 鼠标离开窗口并连续 10 秒没有键盘、拖放或其他操作时，窗口会平滑滑入当前边缘。
- 收边后只留下一个指向屏幕内部的小箭头；鼠标滑到箭头上会立即展开。
- 正在输入、拖放、确认材料或选择 Vault 时不会突然收边；操作结束后重新开始完整的 10 秒倒计时。
- 点击右上角收起图标可以立即收边。
- 展开方向取决于吸附边缘，例如贴在右边时向左展开，贴在底部时向上展开。

## 默认保存位置

所选 Vault 内：

- Inbox：`00 Inbox收件箱`
- Daily：`01 Daily日常/YYYY-MM-DD.md`
- Daily 模板：`10 Knowledge Hub/Templates/Daily.md`
- 附件：`05 Resources/Attachments/YYYY-MM-DD`

InboxDock 自己的本地数据：

- 设置：`%LocalAppData%\InboxDock\settings.json`
- 窗口位置：`%LocalAppData%\InboxDock\window-state.json`
- 暂存卡片与文件：`%LocalAppData%\InboxDock\Staging`

## 和 Codex、Obsidian 一起用

InboxDock 负责快速收集，Obsidian 负责查看和编辑，Codex 负责集中整理。InboxDock 不会自动调用 Codex，也不会把内容上传到网络。

一种简单的使用方式：

1. 平时把文件、链接和想法扔进 InboxDock。
2. 需要马上进入知识库的内容选择“收进 Inbox”，暂时不确定的选择“先暂存”。
3. 每天继续在 Daily 里记录。
4. 每隔几天让 Codex 检查 `00 Inbox收件箱`，归类到项目、领域或资料目录，并补充双向链接。

可以直接对 Codex 说：

```text
请整理我的 Obsidian 知识库里的 00 Inbox收件箱。
先阅读现有目录和笔记风格，再把新材料归到合适位置；
不确定的内容不要擅自删除，列出来问我。
```

## 开发

需要 Windows 和 .NET 10 SDK。

```powershell
dotnet restore InboxDock.sln
dotnet build InboxDock.sln
dotnet test InboxDock.sln
dotnet run --project src/InboxDock.App
```

发布便携版：

```powershell
dotnet publish src/InboxDock.App -c Release -r win-x64 --self-contained true -o artifacts/publish
Compress-Archive artifacts/publish/* artifacts/InboxDock-win-x64.zip
```

## 隐私和安全

- 原始拖入文件不会被移动或删除。
- 文件在完整复制到暂存区后，卡片才会出现。
- 目标重名时自动增加编号，不覆盖已有文件。
- 暂存状态与草稿使用本地 JSON 保存。
- Vault 写入失败时保留卡片和错误信息，方便重试。
- Vault 相对路径必须保持在 Vault 根目录内。

## License

MIT
