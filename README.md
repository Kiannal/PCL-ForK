# PCL-ForK

基于 PCL2 二次开发的 Minecraft Java 版启动器（VB.NET / WPF，.NET Framework 4.8）。

## 改动

- Java 版本自动匹配：按实例版本要求检查本机 Java，有对的版本自动切换
- GC 自动调优：内存 4G 以上自动启用 G1GC 参数
- 存档体检：启动前扫描 level.dat 与区域文件头部，损坏提前提示
- 模组直连：Modrinth 搜索走自建反代，国内可访问
- 联机页超时收敛：节点列表请求 4 秒超时，失败快速回退，避免卡死

## 构建

需要 .NET Framework 4.8 与 MSBuild。

```
msbuild src\Plain Craft Launcher 2.vbproj -p:Configuration=Release
```

## 服务端配置

所有后端地址集中在 `src\Modules\ModServerEndpoints.vb`，部署前把 `YOUR_SERVER_HOST`、`YOUR_BUCKET`、`YOUR_SAVE_BUCKET` 换成自己的地址即可，其余代码无需改动。

## 说明

本项目为二次开发，原始 PCL2 版权归原作者所有。
