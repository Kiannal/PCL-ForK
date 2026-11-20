Imports System.Runtime.InteropServices

Public Class PageOtherTest

    Private Shared ReadOnly RandomHints As String() = {
        "遇到问题先看日志，PCL 的日志在 PCL\Log1.txt。",
        "启动太慢的话，可以去 设置 → 启动 里关掉启动前检查。",
        "下载慢可以换下载源，设置 → 下载 里可以改。",
        "模组冲突时，先看崩溃分析给出的可疑 Mod 列表。",
        "Java 版本不对是启动失败最常见的原因之一。",
        "显卡驱动太旧也会导致启动崩溃，记得更新。",
        "内存别分配太多，超过物理内存反而会变卡。",
        "整合包解压后记得在 PCL 里重新导入版本。",
        "存档备份：版本文件夹下的 saves 直接复制走就行。",
        "正版登录失败时，可以先试试离线登录进游戏。",
        "启动器卡住时可以按 Ctrl+Shift+Esc 看看进程占用。",
        "遇到奇怪的崩溃，先把日志拖进 PCL 窗口试试。"}

    Private Shared ReadOnly RandomCaves As String() = {
        "今天的风儿甚是喧嚣。",
        "你已经盯着这个页面看了很久了。",
        "其实按一下按钮也不会发生什么。",
        "在 Minecraft 里，最坚固的方块是基岩，其次是你的存档。",
        "苦力怕的拥抱，总是来得猝不及防。",
        "如果苦力怕会说话，它大概只会说 ""嘶——""。",
        "警告：本提示没有任何实际作用。"}

    Private Shared ReadOnly RandomPresetHints As String() = {
        "按住 Shift 点击启动器里的按钮，有时会有小惊喜。",
        "PCL 支持拖拽文件到窗口来进行安装或分析。",
        "多版本共存时，建议开启版本隔离。",
        "自定义背景图片支持视频，设置 → 个性化 里可以选。",
        "在模板里输入 {verpath} 可以指代版本文件夹。"}

    Private Sub BtnCustomDownload_Click() Handles BtnCustomDownload.Click
        StartCustomDownload("", "")
    End Sub

    Private Sub BtnRubbishClear_Click() Handles BtnRubbishClear.Click
        RubbishClear()
    End Sub

    Private Sub BtnDontClick_Click() Handles BtnDontClick.Click
        If MyMsgBox("确定要点吗？点了启动器会上下抽搐，然后自动重启。", "千万别点", "我偏要点", "算了", IsWarn:=True) <> 1 Then Return
        Hint("完了，你点了……", HintType.Blue)
        RunInNewThread(
        Sub()
            Dim NormalHeight As Double = 0, NormalTop As Double = 0
            Dim NormalMinHeight As Double = 0
            RunInUiWait(
            Sub()
                NormalHeight = FrmMain.Height
                NormalTop = FrmMain.Top
                NormalMinHeight = FrmMain.MinHeight
                FrmMain.MinHeight = 100
            End Sub)
            Try
                For i = 1 To 12
                    RunInUi(
                    Sub()
                        FrmMain.Height = Math.Max(160, NormalHeight * 0.35)
                        FrmMain.Top = NormalTop + 40
                    End Sub)
                    Thread.Sleep(90)
                    RunInUi(
                    Sub()
                        FrmMain.Height = NormalHeight
                        FrmMain.Top = NormalTop
                    End Sub)
                    Thread.Sleep(90)
                Next
            Finally
                RunInUi(
                Sub()
                    FrmMain.Height = NormalHeight
                    FrmMain.Top = NormalTop
                    FrmMain.MinHeight = NormalMinHeight
                End Sub)
            End Try
            Try
                Dim Argument As String = "-NoProfile -WindowStyle Hidden -Command ""Start-Sleep -Seconds 2; Start-Process '" & PathExe.Replace("'", "''") & "'"""
                Process.Start(New ProcessStartInfo("powershell.exe", Argument) With {.CreateNoWindow = True, .UseShellExecute = False})
            Catch ex As Exception
                Logger.Warn(ex, "重启启动器失败")
            End Try
            Thread.Sleep(400)
            RunInUi(Sub() FormMain.EndProgramForce())
        End Sub, "Dont Click")
    End Sub

    Private Sub BtnMemoryOptimize_Click() Handles BtnMemoryOptimize.Click
        ModMemoryOptimize.OptimizeOnce(True)
    End Sub

    Private Sub CheckRealtimeMemory_Loaded(sender As Object, e As RoutedEventArgs) Handles CheckRealtimeMemory.Loaded
        CheckRealtimeMemory.Checked = Settings.Get(Of Boolean)("SystemMemoryOptimize")
    End Sub

    Private Sub CheckRealtimeMemory_Change(sender As Object, e As Object) Handles CheckRealtimeMemory.Change
        Settings.Set("SystemMemoryOptimize", CheckRealtimeMemory.Checked)
        If CheckRealtimeMemory.Checked Then ModMemoryOptimize.EnsureStarted()
    End Sub

    Private Sub BtnJrrp_Click() Handles BtnJrrp.Click
        Jrrp()
    End Sub

    Private Sub BtnRandomHint_Click() Handles BtnRandomHint.Click
        Hint(GetRandomHint(), HintType.Blue)
    End Sub

    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Try
            If String.IsNullOrWhiteSpace(Url) Then
                Url = MyMsgBoxInput("自定义下载", "输入要下载文件的直链（需以 http:// 或 https:// 开头）。", HintText:="https://...")
                If String.IsNullOrWhiteSpace(Url) Then Return
            End If
            Url = Url.Trim
            If Not (Url.StartsWithF("http://", True) OrElse Url.StartsWithF("https://", True)) Then
                Hint("请输入正确的 http/https 链接", HintType.Red)
                Return
            End If
            If String.IsNullOrWhiteSpace(FileName) Then
                Dim DefaultName As String = PathUtils.GetLastPart(Url).BeforeFirst("?")
                If DefaultName = "" OrElse Not DefaultName.Contains(".") Then DefaultName = "download.bin"
                FileName = MyMsgBoxInput("自定义下载", "输入保存的文件名。", DefaultName)
                If String.IsNullOrWhiteSpace(FileName) Then Return
            End If
            Dim DefaultFolder As String = If(Folder, Environment.GetFolderPath(Environment.SpecialFolder.Desktop))
            Dim SavePath As String = Dialogs.SaveFile("选择保存位置", FileName, DefaultFolder)
            If String.IsNullOrWhiteSpace(SavePath) Then Return
            Dim Loader As New LoaderDownload("自定义下载：" & PathUtils.GetLastPart(SavePath), {New NetFile({Url}, SavePath, Nothing, True)})
            Loader.OnStateChanged = Sub(L) LoaderStateChangedHintOnly(L)
            Loader.Start()
            Hint("已开始下载：" & PathUtils.GetLastPart(SavePath), HintType.Blue)
        Catch ex As Exception
            Logger.Error(ex, "自定义下载失败")
            Hint("下载失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Public Shared Sub Jrrp()
        Try
            Dim Seed As Integer = Date.Now.Year * 10000 + Date.Now.Month * 100 + Date.Now.Day
            For Each Ch In Environment.UserName
                Seed += AscW(Ch)
            Next
            Dim Rand As New Random(Seed)
            Dim Value As Integer = Rand.Next(0, 101)
            Dim Comment As String
            Select Case Value
                Case 95 To 100
                    Comment = "今天运气爆棚，抽卡必出金！"
                Case 80 To 94
                    Comment = "运气不错，适合开新世界。"
                Case 50 To 79
                    Comment = "平平淡淡才是真。"
                Case 20 To 49
                    Comment = "运气一般，挖矿小心点。"
                Case Else
                    Comment = "今天还是待在家里吧，出门容易遇到苦力怕。"
            End Select
            MyMsgBox($"今日人品：{Value}{vbCrLf}{vbCrLf}{Comment}", "今日人品")
        Catch ex As Exception
            Logger.Warn(ex, "今日人品计算失败")
        End Try
    End Sub

    Public Shared Sub RubbishClear()
        Try
            Dim Targets As New List(Of String) From {PathTemp}
            Dim Sizes As New Dictionary(Of String, Long)
            Dim Total As Long = 0
            For Each Folder In Targets
                If DirectoryUtils.Exists(Folder) Then
                    Dim Size As Long = GetFolderSize(Folder)
                    If Size > 0 Then
                        Sizes(Folder) = Size
                        Total += Size
                    End If
                End If
            Next
            If Total <= 0 Then
                Hint("没有可以清理的缓存", HintType.Blue)
                Return
            End If
            If MyMsgBox($"发现 {StringUtils.FormatByteSize(Total)} 可清理的缓存与临时文件：{vbCrLf}{PathTemp}{vbCrLf}{vbCrLf}清理后部分图片、Mod 信息需要重新加载，确定继续吗？", "清理垃圾", "清理", "取消") <> 1 Then Return
            For Each Pair In Sizes
                Try
                    DirectoryUtils.Delete(Pair.Key)
                Catch ex As Exception
                    Logger.Warn(ex, "清理缓存失败：" & Pair.Key)
                End Try
            Next
            Hint($"清理完成，释放了 {StringUtils.FormatByteSize(Total)}", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "清理垃圾失败")
            Hint("清理失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        MemoryOptimizeInternal(ShowHint)
    End Sub

    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        Try
            Dim Proc As Process = Process.GetCurrentProcess()
            Dim Before As Long = Proc.WorkingSet64
            GC.Collect(2, GCCollectionMode.Forced, True, True)
            GC.WaitForPendingFinalizers()
            GC.Collect(2, GCCollectionMode.Forced, True, True)
            SetProcessWorkingSetSize(Proc.Handle, New IntPtr(-1), New IntPtr(-1))
            Dim After As Long = Process.GetCurrentProcess().WorkingSet64
            Dim Freed As Long = Math.Max(0, Before - After)
            Logger.Info($"内存优化：{StringUtils.FormatByteSize(Before)} → {StringUtils.FormatByteSize(After)}")
            If ShowHint Then Hint($"内存优化完成，释放了 {StringUtils.FormatByteSize(Freed)}", HintType.Green)
        Catch ex As Exception
            Logger.Warn(ex, "内存优化失败")
            If ShowHint Then Hint("内存优化失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Public Shared Function GetRandomCave() As String
        Return RandomOne(RandomCaves)
    End Function

    Public Shared Function GetRandomHint() As String
        Return RandomOne(RandomHints)
    End Function

    Public Shared Function GetRandomPresetHint() As String
        Return RandomOne(RandomPresetHints)
    End Function

    Private Shared Function GetFolderSize(Folder As String) As Long
        Dim Size As Long = 0
        Try
            DirectoryUtils.Create(Folder)
            For Each File In DirectoryUtils.EnumerateFiles(Folder, True)
                Try
                    Size += New IO.FileInfo(File).Length
                Catch ex As Exception
                End Try
            Next
        Catch ex As Exception
        End Try
        Return Size
    End Function

    <DllImport("kernel32.dll")>
    Private Shared Function SetProcessWorkingSetSize(hProcess As IntPtr, dwMinimumWorkingSetSize As IntPtr, dwMaximumWorkingSetSize As IntPtr) As Boolean
    End Function


    Private Sub BtnAceClean_Click() Handles BtnAceClean.Click
        If MyMsgBox("将停止并删除 ACE / SGuard 反作弊组件，可能导致依赖它的游戏无法启动。继续？", "清理反作弊", "继续", "取消", IsWarn:=True) <> 1 Then Return
        RunInNewThread(
        Sub()
            Dim Report As New Text.StringBuilder
            Dim Count As Integer = 0
            For Each SvcName As String In {"ACE-BASE", "ACE-Guard", "ACE-TRACE", "SGuard", "SGuard64", "AntiCheatExpert", "ACE-Guard Client"}
                Try
                    Process.Start(New ProcessStartInfo("sc.exe", "stop " & SvcName) With {.CreateNoWindow = True, .UseShellExecute = False, .RedirectStandardOutput = True, .RedirectStandardError = True}).WaitForExit(4000)
                    Process.Start(New ProcessStartInfo("sc.exe", "delete " & SvcName) With {.CreateNoWindow = True, .UseShellExecute = False, .RedirectStandardOutput = True, .RedirectStandardError = True}).WaitForExit(4000)
                    Count += 1
                Catch ex As Exception
                End Try
            Next
            For Each Proc In Process.GetProcesses()
                Try
                    Dim Pn As String = Proc.ProcessName.ToLower()
                    If Pn.Contains("ace") OrElse Pn.Contains("sguard") OrElse Pn.Contains("anticheat") Then
                        Proc.Kill()
                        Count += 1
                    End If
                Catch ex As Exception
                End Try
            Next
            Dim Dirs As New List(Of String) From {
                IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AntiCheatExpert"),
                IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "AntiCheatExpert"),
                IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AntiCheatExpert"),
                "C:\Program Files\ACE", "C:\Program Files (x86)\ACE"
            }
            For Each AceDir As String In Dirs
                Try
                    If IO.Directory.Exists(AceDir) Then
                        IO.Directory.Delete(AceDir, True)
                        Report.AppendLine("已删除：" & AceDir)
                        Count += 1
                    End If
                Catch ex As Exception
                    Report.AppendLine("删除失败：" & AceDir & "（" & ex.Message & "）")
                End Try
            Next
            RunInUi(Sub()
                        LabAceStatus.Text = $"清理完成，处理 {Count} 项。" & If(Report.Length > 0, vbCrLf & Report.ToString(), "")
                        Hint("ACE 清理完成", HintType.Green)
                    End Sub)
        End Sub, "ACE Clean")
    End Sub

    Private Sub BtnUnlockFps_Click() Handles BtnUnlockFps.Click
        Try
            If McInstanceSelected Is Nothing Then
                Hint("请先选择一个版本", HintType.Red)
                Return
            End If
            Dim OptionsPath As String = McInstanceSelected.PathIndie & "options.txt"
            Dim Lines As New List(Of String)
            If FileUtils.Exists(OptionsPath) Then Lines.AddRange(IO.File.ReadAllLines(OptionsPath, Encoding.UTF8))
            Dim Keys As New Dictionary(Of String, String) From {{"maxFps", "260"}, {"enableVsync", "false"}, {"pauseOnLostFocus", "false"}}
            For Each Key In Keys.Keys.ToList()
                Dim Found As Boolean = False
                For I = 0 To Lines.Count - 1
                    If Lines(I).StartsWith(Key & ":", StringComparison.OrdinalIgnoreCase) Then
                        Lines(I) = Key & ":" & Keys(Key)
                        Found = True
                        Exit For
                    End If
                Next
                If Not Found Then Lines.Add(Key & ":" & Keys(Key))
            Next
            IO.File.WriteAllLines(OptionsPath, Lines, New UTF8Encoding(False))
            LabAceStatus.Text = "已解除帧率限制：maxFps=260，垂直同步关闭。部分显卡驱动仍可能限制上限。"
            Hint("已解除 60 帧限制，启动游戏后生效", HintType.Green)
        Catch ex As Exception
            Hint("解除帧率限制失败：" & ex.Message, HintType.Red)
        End Try
    End Sub


    Private Sub PageOtherTest_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Try
            LabNeteaseStatus.Text = If(ModNeteaseTool.IsReady, "组件已就绪：" & ModNeteaseTool.ToolFolder, "内置组件 " & ModNeteaseTool.GetPackageSizeText() & "，首次打开自动释放。")
        Catch ex As Exception
        End Try
    End Sub

    Private Sub BtnNeteaseTool_Click() Handles BtnNeteaseTool.Click
        LabNeteaseStatus.Text = "正在准备网易中国版……"
        ModNeteaseTool.Launch(Sub(Text As String)
                                   RunInUi(Sub() LabNeteaseStatus.Text = Text)
                               End Sub)
    End Sub

    Private Sub BtnNeteaseReset_Click() Handles BtnNeteaseReset.Click
        If MyMsgBox("将删除已释放的网易中国版组件（PCL\NetEase），账号与配置不受影响。继续？", "重新释放组件", "继续", "取消") <> 1 Then Return
        ModNeteaseTool.ResetTool()
        LabNeteaseStatus.Text = "已清除，下次打开时重新释放。"
    End Sub

End Class
