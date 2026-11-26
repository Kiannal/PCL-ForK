# PCL-ForK

基于 [PCL2](https://github.com/Meloong-Git/PCL) 二次开发的 Minecraft 启动器分支，VB.NET / WPF。

## 功能

- 液态玻璃界面、动态背景、深色模式
- Mod / 资源包 / 光影 / 数据包管理（卡片行、图标、筛选、动态下载按钮）
- 自动汉化：读取模组 `en_us.json`，DeepSeek 与 Google 双通道并行翻译，结果打包为资源包并写入 `options.txt`
- AI 助手、AI 崩溃诊断与自动修复（安装缺失前置、禁用最近 Mod）
- 内置 cubiomes 种子地图、结构筛选、要塞定位
- 皮肤库、皮肤绘制、Litematic 3D 查看器
- 网易云音乐、EasyTier 联机、启动视频、版本文件编辑器
- 百宝箱：ACE 反作弊清理、解除 60 帧限制、内存优化

## 给编码代理

项目记忆、构建命令、代码约定与踩坑记录见 `AGENTS.md`（Codex 等代理会自动读取）。

## 目录

```
src/    VB.NET / WPF 源码（Plain Craft Launcher 2.vbproj）
dist/   构建产物 PCL-ForK.exe
```

## 构建

进入 `src` 后用 Visual Studio 打开 `Plain Craft Launcher 2.sln`，或：

```
dotnet msbuild "Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj" -p:Configuration=Release
```

## 下载

`dist/PCL-ForK.exe`

## 说明

- 本仓库为 Fork，原项目版权归 PCL2 作者所有，遵循仓库根目录的《PCL 分发有限许可》与《PCL 存储库合理使用指南》
- 仓库内的密钥、用户名、路径均已替换为占位符，使用前请填写自己的 DeepSeek Key
- `dist/PCL-ForK.exe` 为构建产物，未做代码签名

## 协议

《PCL 分发有限许可》与《PCL 存储库合理使用指南》，全文见 [LICENSE](LICENSE)。