# AGENTS.md — PCL-ForK 项目记忆（给 Codex / 其他编码代理）

## 这是什么

PCL-ForK 是基于 PCL2（VB.NET / WPF，.NET Framework 4.8）二次开发的 Minecraft 启动器分支。
仓库结构：`src/` 源码工程，`dist/PCL-ForK.exe` 构建产物。

## 构建与部署

```powershell
# 构建（Release）
dotnet msbuild "src\Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj" `
  -p:Configuration=Release -p:BuildProjectReferences=false

# 产物
src\Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe
```

- 运行时数据目录：`<启动器所在目录>\PCL\`（Setup.ini、Log1.txt、Cache）
- 皮肤缓存：`%APPDATA%\PCL\Skins`
- 发布：把 bin 下的 exe 复制为 `dist\PCL-ForK.exe`，打 tag 后用 GitHub Release 上传

## 代码约定（必须遵守，违反会编译失败或运行异常）

1. **VB 保留字**：`Name`、`Key`、`Dir`、`Erase`、`Try` 不能作循环变量或标识符，用 `SvcName` / `Kw` / `AceDir` / `EraseIt`。
2. **单行 Try 不合法**：`Sub() Try ... Catch ... End Try` 必须拆多行。
3. **`On Error Resume Next` 与 `Try` 不能共存**：同一方法里二选一，需要 try 就抽成独立函数。
4. **WPF 事件签名**：`MyButton` 用自定义 `Public Event Click(sender, e As MouseButtonEventArgs)`，不是 RoutedEvent；`MyCheckBox` 是 `Check` 不是 `Checked`。
5. **CS 文件必须加入 vbproj**：新增 `Controls\*.vb` 要手动加 `<Compile Include="Controls\Xxx.vb" />`，否则 `未定义类型`。
6. **图片/位图**：`CroppedBitmap` 会冻结源，跨次复用需检测 `IsFrozen` 并重建；`TransformedBitmap` 源必须已冻结。

## 已完成的关键功能与实现位置

| 功能 | 位置 |
| --- | --- |
| 深色模式配色 / 标题栏渐变 | `Modules\ModSecret.vb`（`ThemeRefresh`、`DarkBrush`） |
| 自动汉化（双引擎翻译） | `Modules\Minecraft\ModAutoTranslate.vb` |
| 自动汉化页面 | `Pages\PageInstance\PageInstanceTranslate.xaml(.vb)` |
| AI 助手对话 | `Controls\MyAIChat.xaml.vb` |
| AI 启动器异常诊断 | `Controls\FrmErrorAI.vb` + `Application.xaml.vb` |
| AI 崩溃诊断（Minecraft） | `Controls\FrmCrashFix.vb` + `Modules\Minecraft\ModCrash.vb`（`Output` 的 `Case 3`） |
| 种子地图（cubiomes） | `Pages\PageInstance\PageInstanceSeed.xaml(.vb)` + `Modules\Minecraft\ModSeedMap.vb` + `Resources\SeedMap\gentool.exe` |
| 资源包/光影/数据包管理 | `Pages\PageInstance\PageInstancePack.xaml(.vb)` |
| 百宝箱（ACE 清理、解锁帧率等） | `Pages\PageOther\PageOtherTest.xaml(.vb)` |

## 自动汉化管线（重点）

- 扫描实例 `mods\*.jar` 内 `assets/<modid>/lang/en_us.json`
- 待翻译文本去重，过滤纯符号、已含中文、超过 400 字符的条目
- 每批 200 条：DeepSeek 199 条 + Google 1 条（Google 只发不等）
- DeepSeek 单批超时 50 秒；失败保留原文，不阻塞整批
- 并发 12 批，结果写入 `PCL\Cache\TranslateCache.json`
- 生成 `resourcepacks\PCL自动汉化.zip`，26.x 用 `min_format`/`max_format`，旧版用 `pack_format`
- 写入 `options.txt` 的 `resourcePacks`；文件不存在时自动创建

## 翻译/AI 鉴权

- 用户可填 DeepSeek Key（AES 加密存 Setup.ini 的 `DeepSeekApiKey`）
- 支持 `DeepSeekProxy` 走自建代理，代理脚本 `deepseek-proxy-worker.js`（Cloudflare Worker）
- 仓库内 Key 全部为占位符，禁止提交真实密钥

## 已知未解决问题

- 实验页「皮肤绘制」画布颜色异常（始终偏蓝），可靠替代是页面里的「经典绘制」（`FrmSkinPainter`）
- 崩溃诊断的「自动安装缺失前置」依赖 Modrinth 搜索，个别命名匹配不到