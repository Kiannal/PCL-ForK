Imports System.IO.Compression

Public Module ModServerPack

    Public Enum ModEnvironments
        Unknown = 0
        ClientOnly = 1
        ServerOnly = 2
        Both = 3
    End Enum

    Public Function GetModEnvironment(FilePath As String) As ModEnvironments
        Try
            If PathUtils.GetExtension(FilePath).Lower = "litemod" Then Return ModEnvironments.ClientOnly
            If PathUtils.GetFileNameWithoutExtension(FilePath).ContainsIgnoreCase("optifine") Then Return ModEnvironments.ClientOnly
            Using Zip As ZipArchive = FileUtils.OpenZip(FilePath)
                Dim FabricEntry = Zip.GetEntry("fabric.mod.json")
                If FabricEntry IsNot Nothing Then
                    Using Reader As New StreamReader(FabricEntry.Open, Encoding.UTF8)
                        Dim Json As JObject = Reader.ReadToEnd().DeserializeJson()
                        Select Case Json("environment")?.ToString.Lower
                            Case "client"
                                Return ModEnvironments.ClientOnly
                            Case "server"
                                Return ModEnvironments.ServerOnly
                            Case Else
                                Return ModEnvironments.Both
                        End Select
                    End Using
                End If
                For Each TomlName In {"META-INF/mods.toml", "META-INF/neoforge.mods.toml"}
                    Dim TomlEntry = Zip.GetEntry(TomlName)
                    If TomlEntry Is Nothing Then Continue For
                    Using Reader As New StreamReader(TomlEntry.Open, Encoding.UTF8)
                        Dim Content As String = Reader.ReadToEnd()
                        If Content.RegexCheck("(?im)^\s*clientSideOnly\s*=\s*true") Then Return ModEnvironments.ClientOnly
                        If Content.RegexCheck("(?im)^\s*serverSideOnly\s*=\s*true") Then Return ModEnvironments.ServerOnly
                        Return ModEnvironments.Both
                    End Using
                Next
                Dim OldEntry = Zip.GetEntry("mcmod.info")
                If OldEntry IsNot Nothing Then
                    Using Reader As New StreamReader(OldEntry.Open, Encoding.UTF8)
                        Dim Content As String = Reader.ReadToEnd()
                        If Content.RegexCheck("""clientSideOnly""\s*:\s*true") Then Return ModEnvironments.ClientOnly
                        If Content.RegexCheck("""serverSideOnly""\s*:\s*true") Then Return ModEnvironments.ServerOnly
                        Return ModEnvironments.Both
                    End Using
                End If
            End Using
        Catch ex As Exception
            Logger.Warn(ex, $"识别 Mod 运行环境失败：{FilePath}")
        End Try
        Return ModEnvironments.Unknown
    End Function

    Public Sub ExportServerMods(Instance As McInstance)
        Try
            If Instance Is Nothing Then Return
            Dim ModFolder As String = Instance.PathIndie & "mods\"
            If Not DirectoryUtils.Exists(ModFolder) Then
                Hint("该版本没有 mods 文件夹", HintType.Red)
                Return
            End If
            Dim ModFiles = DirectoryUtils.EnumerateFiles(ModFolder).
                Where(Function(f) {"jar", "litemod"}.Contains(PathUtils.GetExtension(f).Lower)).ToList()
            If Not ModFiles.Any() Then
                Hint("该版本没有安装 Mod", HintType.Red)
                Return
            End If
            Dim SavePath As String = Dialogs.SaveFile("保存服务端 Mod 压缩包", $"{Instance.Name} 服务端 Mod.zip", Instance.PathIndie, {("zip", "压缩包")})
            If String.IsNullOrEmpty(SavePath) Then Return
            Dim PackTask As New LoaderTask(Of (Instance As McInstance, SavePath As String), Integer)("打包服务端 Mod", AddressOf PackServerMods)
            Dim Loader As New LoaderCombo(Of (Instance As McInstance, SavePath As String))("打包服务端 Mod", {PackTask}) With {
                .OnStateChanged = Sub(MyLoader) LoaderStateChangedHintOnly(MyLoader)}
            Loader.Start((Instance, SavePath))
            LoaderTaskbarAdd(Loader)
        Catch ex As Exception
            Logger.Error(ex, "导出服务端 Mod 失败")
            Hint("导出服务端 Mod 失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub PackServerMods(Task As LoaderTask(Of (Instance As McInstance, SavePath As String), Integer))
        Dim Instance As McInstance = Task.Input.Instance
        Dim SavePath As String = Task.Input.SavePath
        Dim ModFiles = DirectoryUtils.EnumerateFiles(Instance.PathIndie & "mods\").
            Where(Function(f) {"jar", "litemod"}.Contains(PathUtils.GetExtension(f).Lower)).ToList()
        Dim ServerMods As New List(Of (Path As String, Name As String))
        Dim ClientMods As New List(Of String)
        For i = 0 To ModFiles.Count - 1
            Task.Progress = i / ModFiles.Count * 0.6
            Dim Name As String = PathUtils.GetLastPart(ModFiles(i))
            If GetModEnvironment(ModFiles(i)) = ModEnvironments.ClientOnly Then
                ClientMods.Add(Name)
            Else
                ServerMods.Add((ModFiles(i), Name))
            End If
        Next
        Task.Progress = 0.65
        DirectoryUtils.Create(PathUtils.RemoveLastPart(SavePath))
        Using Zip As New ZipArchive(File.Open(SavePath, FileMode.Create), ZipArchiveMode.Create)
            For i = 0 To ServerMods.Count - 1
                Task.Progress = 0.65 + (i + 1) / Math.Max(ServerMods.Count, 1) * 0.3
                Dim Entry = Zip.CreateEntry("mods/" & ServerMods(i).Name)
                Using EntryStream = Entry.Open()
                    Using SourceStream = File.Open(ServerMods(i).Path, FileMode.Open, FileAccess.Read, FileShare.Read)
                        SourceStream.CopyTo(EntryStream)
                    End Using
                End Using
            Next
            Dim Note As New StringBuilder
            Note.AppendLine("服务端 Mod 打包结果")
            Note.AppendLine($"版本：{Instance.Name}")
            Note.AppendLine($"已包含 Mod（{ServerMods.Count} 个）：")
            For Each ModItem In ServerMods
                Note.AppendLine("  " & ModItem.Name)
            Next
            Note.AppendLine($"仅客户端、无需在服务器安装的 Mod（{ClientMods.Count} 个，未包含）：")
            For Each ModItem In ClientMods
                Note.AppendLine("  " & ModItem)
            Next
            Dim NoteEntry = Zip.CreateEntry("服务端Mod说明.txt")
            Using Writer As New StreamWriter(NoteEntry.Open(), Encoding.UTF8)
                Writer.Write(Note.ToString())
            End Using
        End Using
        Task.Progress = 1
        Task.Output = ServerMods.Count
        Logger.Info($"已导出服务端 Mod：{ServerMods.Count} 个（忽略仅客户端 Mod {ClientMods.Count} 个）")
        RunInUi(Sub() Hint($"已导出 {ServerMods.Count} 个服务端 Mod，忽略 {ClientMods.Count} 个仅客户端 Mod", HintType.Green))
    End Sub

End Module
