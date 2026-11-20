Imports System.IO.Compression
Imports System.Text

''' <summary>网易中国版（ES Tool）整合模块：内置 ES Tool 运行包，按需释放并启动。</summary>
Public Module ModNeteaseTool

    Private Const ResourceName As String = "PCL.NetEase.ES_Tool.zip"
    Private Const ExeName As String = "ModPcGUI.exe"
    Private Const StampName As String = ".pcl_version"

    ''' <summary>运行目录：PCL\NetEase\</summary>
    Public ReadOnly Property ToolFolder As String
        Get
            Return PathPure.Value & "NetEase\"
        End Get
    End Property

    Public ReadOnly Property ToolPath As String
        Get
            Return ToolFolder & ExeName
        End Get
    End Property

    Public ReadOnly Property IsReady As Boolean
        Get
            Return FileUtils.Exists(ToolPath)
        End Get
    End Property

    ''' <summary>用户数据（账号、配置），释放时必须保留</summary>
    Private Function IsUserData(RelativePath As String) As Boolean
        Dim Normal As String = RelativePath.Replace("/"c, "\"c).ToLowerInvariant()
        Return Normal.StartsWith("user\") OrElse Normal = "config.json" OrElse Normal = "ui.json"
    End Function

    ''' <summary>把内置 ES Tool 释放到 PCL\NetEase\，已是最新则跳过</summary>
    Public Function EnsureTool(Optional Report As Action(Of String) = Nothing) As Boolean
        Try
            DirectoryUtils.Create(ToolFolder)
            Dim Assembly As Reflection.Assembly = Reflection.Assembly.GetExecutingAssembly()
            Using Stream As IO.Stream = Assembly.GetManifestResourceStream(ResourceName)
                If Stream Is Nothing Then Throw New Exception("找不到内置的 ES Tool 资源")
                Dim Stamp As String = $"{Stream.Length}"
                Dim StampPath As String = ToolFolder & StampName
                If FileUtils.Exists(ToolPath) AndAlso FileUtils.Exists(StampPath) Then
                    If FileUtils.ReadAsString(StampPath).Trim() = Stamp Then Return True
                End If
                Report?.Invoke("正在释放网易中国版组件……")
                Stream.Position = 0
                Using Zip As New ZipArchive(Stream, ZipArchiveMode.Read)
                    For Each Entry In Zip.Entries
                        Dim Rel As String = Entry.FullName
                        If String.IsNullOrWhiteSpace(Rel) OrElse Rel.EndsWith("/") Then Continue For
                        If IsUserData(Rel) AndAlso FileUtils.Exists(ToolFolder & Rel.Replace("/"c, "\"c)) Then Continue For
                        Dim Target As String = ToolFolder & Rel.Replace("/"c, "\"c)
                        DirectoryUtils.Create(IO.Path.GetDirectoryName(Target) & "\")
                        Entry.ExtractToFile(Target, True)
                    Next
                End Using
                FileUtils.Write(StampPath, Stamp)
            End Using
            Return FileUtils.Exists(ToolPath)
        Catch ex As Exception
            Logger.Error(ex, "释放网易中国版组件失败")
            Report?.Invoke("释放失败：" & ex.Message)
            Return False
        End Try
    End Function

    ''' <summary>启动 ES Tool（网易中国版）</summary>
    Public Sub Launch(Optional Report As Action(Of String) = Nothing)
        RunInNewThread(
            Sub()
                If Not EnsureTool(Report) Then
                    RunInUi(Sub() Hint("网易中国版组件释放失败，请查看日志", HintType.Red))
                    Return
                End If
                Try
                    Process.Start(New ProcessStartInfo(ToolPath) With {.WorkingDirectory = ToolFolder, .UseShellExecute = True})
                    RunInUi(Sub() Hint("已打开网易中国版", HintType.Green))
                Catch ex As Exception
                    Logger.Error(ex, "启动网易中国版失败")
                    RunInUi(Sub() Hint("启动失败：" & ex.Message, HintType.Red))
                End Try
            End Sub, "NetEase Tool")
    End Sub

    ''' <summary>删掉已释放的组件（保留 user 账号与配置），下次重新解压</summary>
    Public Sub ResetTool()
        Try
            If Not DirectoryUtils.Exists(ToolFolder) Then Return
            For Each SubDir In IO.Directory.GetDirectories(ToolFolder)
                If IO.Path.GetFileName(SubDir).ToLowerInvariant() = "user" Then Continue For
                Try
                    IO.Directory.Delete(SubDir, True)
                Catch ex As Exception
                End Try
            Next
            For Each SubFile In IO.Directory.GetFiles(ToolFolder)
                Dim Name As String = IO.Path.GetFileName(SubFile).ToLowerInvariant()
                If Name = "config.json" OrElse Name = "ui.json" Then Continue For
                Try
                    IO.File.Delete(SubFile)
                Catch ex As Exception
                End Try
            Next
        Catch ex As Exception
            Logger.Warn(ex, "重置网易中国版组件失败")
        End Try
    End Sub

    Public Function GetPackageSizeText() As String
        Try
            Dim Assembly As Reflection.Assembly = Reflection.Assembly.GetExecutingAssembly()
            Using Stream As IO.Stream = Assembly.GetManifestResourceStream(ResourceName)
                If Stream Is Nothing Then Return "-"
                Return Math.Round(Stream.Length / 1024 / 1024, 1).ToString("0.0") & " MB"
            End Using
        Catch ex As Exception
            Return "-"
        End Try
    End Function

End Module
