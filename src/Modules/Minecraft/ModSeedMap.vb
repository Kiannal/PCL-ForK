Imports System.Diagnostics
Imports System.Text

Public Module ModSeedMap

    Public ReadOnly Property ToolFolder As String
        Get
            Return PathTemp & "SeedMap\"
        End Get
    End Property
    Public ReadOnly Property ToolPath As String
        Get
            Return ToolFolder & "gentool.exe"
        End Get
    End Property

    Public Function EnsureTool() As Boolean
        Try
            DirectoryUtils.Create(ToolFolder)
            Dim Assembly As Reflection.Assembly = Reflection.Assembly.GetExecutingAssembly()
            Dim Stream As IO.Stream = Assembly.GetManifestResourceStream("PCL.SeedMap.gentool.exe")
            If Stream Is Nothing Then Throw New Exception("找不到种子地图生成器")
            Using Stream
                If FileUtils.Exists(ToolPath) AndAlso New IO.FileInfo(ToolPath).Length = Stream.Length Then Return True
                Stream.Position = 0
                FileUtils.Delete(ToolPath)
                Using Target As New IO.FileStream(ToolPath, IO.FileMode.Create, IO.FileAccess.Write)
                    Stream.CopyTo(Target)
                End Using
            End Using
            Return FileUtils.Exists(ToolPath)
        Catch ex As Exception
            Logger.Error(ex, "释放种子地图生成器失败")
            Return False
        End Try
    End Function

    Public Function GetVersionName(Instance As McInstance) As String
        Try
            If Instance IsNot Nothing AndAlso Instance.Version IsNot Nothing Then
                Dim Name As String = Instance.Version.VanillaName
                If Not String.IsNullOrWhiteSpace(Name) Then Return Name
            End If
        Catch ex As Exception
        End Try
        Return "1.20.1"
    End Function

    Public Function Generate(SeedValue As String, VersionName As String, DimId As String, Radius As Integer, Size As Integer, Optional CenterX As Integer = 0, Optional CenterZ As Integer = 0) As JObject
        If Not EnsureTool() Then Throw New Exception("种子地图生成器不可用")
        Dim Args As String = $"--seed {SeedValue} --mc {VersionName} --dim {DimId} --radius {Radius} --size {Size} --x {CenterX} --z {CenterZ}"
        Dim Info As New ProcessStartInfo(ToolPath) With {
            .Arguments = Args, .UseShellExecute = False, .CreateNoWindow = True,
            .RedirectStandardOutput = True, .RedirectStandardError = True,
            .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8}
        Using Proc As New Process With {.StartInfo = Info}
            Proc.Start()
            Dim Output As String = Proc.StandardOutput.ReadToEnd()
            Dim ErrorText As String = Proc.StandardError.ReadToEnd()
            Proc.WaitForExit()
            If String.IsNullOrWhiteSpace(Output) Then Throw New Exception("地图生成器没有返回数据：" & ErrorText)
            Dim Result As JObject = JObject.Parse(Output)
            If Result("ok") Is Nothing OrElse Result("ok").ToString() <> "1" Then Throw New Exception("地图生成失败")
            Return Result
        End Using
    End Function


    Public Sub LaunchCubiomesViewer(Seed As String, VersionName As String)
        Hint("正在准备 Cubiomes Viewer……")
        RunInNewThread(
        Sub()
            Try
                DirectoryUtils.Create(ToolFolder)
                Dim ExePath As String = ToolFolder & "cubiomes-viewer.exe"
                If Not FileUtils.Exists(ExePath) Then
                    Dim Api As String = NetRequestByClient("https://api.github.com/repos/Cubitect/cubiomes-viewer/releases/latest", Timeout:=15000)
                    Dim J As JObject = JObject.Parse(Api)
                    Dim Url As String = ""
                    For Each Asset As JToken In CType(J("assets"), JArray)
                        If Asset("name").ToString() = "cubiomes-viewer-static-w64.exe" Then
                            Url = Asset("browser_download_url").ToString()
                        End If
                    Next
                    If String.IsNullOrWhiteSpace(Url) Then Throw New Exception("没有找到 Windows 版本")
                    NetDownloadByLoader(Url, ExePath)
                End If
                RunInUi(
                Sub()
                    Try
                        If Not String.IsNullOrWhiteSpace(Seed) Then Clipboard.SetText(Seed)
                        Process.Start(New ProcessStartInfo(ExePath) With {.UseShellExecute = True, .WorkingDirectory = ToolFolder})
                        Hint("已启动 Cubiomes Viewer，种子已复制到剪贴板", HintType.Green)
                    Catch ex As Exception
                        Hint("启动失败：" & ex.Message, HintType.Red)
                    End Try
                End Sub)
            Catch ex As Exception
                RunInUi(Sub() Hint("下载 Cubiomes Viewer 失败：" & ex.Message, HintType.Red))
            End Try
        End Sub, "Cubiomes Viewer")
    End Sub

End Module
