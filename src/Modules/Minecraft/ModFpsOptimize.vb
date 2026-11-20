Public Module ModFpsOptimize

    Private ReadOnly FabricMods As String() = {"sodium", "lithium", "ferrite-core", "modernfix", "immediatelyfast", "entityculling", "memoryleakfix"}
    Private ReadOnly ForgeMods As String() = {"embeddium", "ferrite-core", "modernfix", "immediatelyfast", "entityculling", "memoryleakfix", "clumps"}
    Private ReadOnly NeoForgeMods As String() = {"ferrite-core", "modernfix", "immediatelyfast", "entityculling", "clumps"}

    Private Function GetRecommendModSlugs(Instance As McInstance) As List(Of String)
        Dim Loaders = Instance.Version.ModLoaders
        If Loaders.HasFlagF(ModLoaders.Fabric) Then Return FabricMods.ToList()
        If Loaders.HasFlagF(ModLoaders.NeoForge) Then Return NeoForgeMods.ToList()
        If Loaders.HasFlagF(ModLoaders.Forge) Then Return ForgeMods.ToList()
        Return New List(Of String)
    End Function

    Public Sub Run(Instance As McInstance)
        Try
            If Instance Is Nothing Then Return
            Instance.Load()
            Dim Slugs As List(Of String) = GetRecommendModSlugs(Instance)
            Dim ModFolder As String = Instance.PathIndie & "mods\"
            DirectoryUtils.Create(ModFolder)
            Dim ModDescription As String = If(Slugs.Any(), $"将安装以下性能优化 Mod：{vbCrLf}{Slugs.Join("、")}{vbCrLf}{vbCrLf}", "")
            If MyMsgBox($"{ModDescription}并将调整画面设置（快速图形、降低粒子与渲染距离、关闭实体阴影与云），同时启用独显运行。{vbCrLf}{vbCrLf}是否继续？", "帧率优化", "开始优化", "取消") <> 1 Then Return
            Dim ChangedOptions As Integer = OptimizeOptions(Instance)
            Settings.Set("LaunchAdvanceGraphicCard", True)
            If Not Slugs.Any() Then
                Hint($"已调整 {ChangedOptions} 项画面设置（该版本没有 Mod 加载器，无法安装优化 Mod）", HintType.Green)
                Return
            End If
            Dim AnalyzeTask As New LoaderTask(Of Integer, List(Of NetFile))("分析性能优化 Mod", Sub(Task)
                Dim Result As New List(Of NetFile)
                For Each Slug In Slugs
                    Try
                        Dim Versions As List(Of ResourceVersion) = ResourceVersion.FromProjectId(Slug, ResourcePlatforms.Modrinth)
                        Dim File As ResourceVersion = GetBestModVersion(Versions, Instance)
                        If File Is Nothing Then
                            Logger.Info($"没有找到适合当前版本的优化 Mod：{Slug}")
                            Continue For
                        End If
                        If FileUtils.Exists(ModFolder & File.FileName) Then Continue For
                        Result.Add(File.ToNetFile(ModFolder, ResourceVersion.DownloadReason.Dependency, Instance.Version.VanillaName, Instance.Version.ModLoaders))
                        Logger.Info($"准备下载优化 Mod：{File.FileName}")
                    Catch ex As Exception
                        Logger.Warn(ex, $"获取优化 Mod 失败：{Slug}")
                    End Try
                Next
                Task.Output = Result
            End Sub)
            Dim DownloadLoader As New LoaderDownload("下载性能优化 Mod", New List(Of NetFile)) With {.ProgressWeight = 10}
            Dim Loader As New LoaderCombo(Of Integer)("帧率优化", {AnalyzeTask, DownloadLoader}) With {
                .OnStateChanged = Sub(MyLoader)
                    LoaderStateChangedHintOnly(MyLoader)
                    If MyLoader.State = LoadState.Finished Then RunInUi(Sub() Hint($"帧率优化完成：已调整 {ChangedOptions} 项画面设置，重启游戏后生效", HintType.Green))
                End Sub}
            Loader.Start(1)
            LoaderTaskbarAdd(Loader)
        Catch ex As Exception
            Logger.Error(ex, "帧率优化失败")
            Hint("帧率优化失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Function GetBestModVersion(Versions As List(Of ResourceVersion), Instance As McInstance) As ResourceVersion
        If Versions Is Nothing OrElse Not Versions.Any() Then Return Nothing
        Dim GameVersion As String = Instance.Version.VanillaName
        Dim Loaders As ModLoaders = Instance.Version.ModLoaders
        Dim Matched = Versions.
            Where(Function(v) v.DownloadAvailable).
            Where(Function(v) v.GameVersions.Contains(GameVersion)).
            Where(Function(v) v.ModLoaders = ModLoaders.None OrElse v.ModLoaders.Flags.Intersect(Loaders.Flags).Any()).ToList()
        If Not Matched.Any() Then Return Nothing
        Return Matched.OrderBy(Function(v) If(v.ReleaseType = ResourceVersion.ReleaseTypes.Release, 0, 1)).
            ThenByDescending(Function(v) v.ReleaseDate).First()
    End Function

    Private Function OptimizeOptions(Instance As McInstance) As Integer
        Dim OptionsPath As String = Instance.PathIndie & "options.txt"
        Dim Lines As New List(Of String)
        If FileUtils.Exists(OptionsPath) Then Lines.AddRange(FileUtils.ReadAsString(OptionsPath).Replace(vbCrLf, vbLf).Split(vbLf))
        Dim Target As New Dictionary(Of String, String) From {
            {"graphics", "fast"},
            {"fancyGraphics", "false"},
            {"renderDistance", "8"},
            {"simulationDistance", "6"},
            {"entityShadows", "false"},
            {"particles", "1"},
            {"biomeBlendRadius", "0"},
            {"ao", "false"},
            {"enableVsync", "false"},
            {"maxFps", "260"},
            {"renderClouds", "false"},
            {"mipmapLevels", "4"}}
        Dim Changed As Integer = 0
        Dim DoneKeys As New HashSet(Of String)
        For i = 0 To Lines.Count - 1
            Dim Key As String = Lines(i).BeforeFirst(":")
            If Target.ContainsKey(Key) Then
                Dim NewLine As String = Key & ":" & Target(Key)
                If Lines(i) <> NewLine Then
                    Lines(i) = NewLine
                    Changed += 1
                End If
                DoneKeys.Add(Key)
            End If
        Next
        For Each Pair In Target
            If Not DoneKeys.Contains(Pair.Key) Then
                Lines.Add(Pair.Key & ":" & Pair.Value)
                Changed += 1
            End If
        Next
        FileUtils.Write(OptionsPath, Lines.Join(vbCrLf))
        Logger.Info($"已优化画面设置，共修改 {Changed} 项")
        Return Changed
    End Function

End Module
