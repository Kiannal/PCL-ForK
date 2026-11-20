Imports Newtonsoft.Json.Linq
Imports System.Windows.Threading
Imports System.Collections.ObjectModel
Imports System.Diagnostics
Imports System.Globalization
Imports System.Net
Imports System.Net.NetworkInformation
Imports System.Net.Sockets
Imports System.Reflection
Imports PCL

Public Class PageLinkMain
    Private Const INVITE_CODE_VERSION As Integer = 2

    '===============================
    '  状态机与前端页面
    '===============================

#Region "状态管理"

    Private Shared TerracottaPrompted As Boolean = False

    Private Shared TcUiInited As Boolean = False
    Private TcPort As Integer = 0
    Private TcBusy As Boolean = False
    Private TcState As String = "waiting"
    Private TcRoom As String = ""
    Private ReadOnly TcTimer As New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(700)}

    Private Function TcPlayerName() As String
        Try
            Select Case Settings.Get(Of McLoginType)("LoginType")
                Case McLoginType.Ms
                    Return Settings.Get(Of String)("CacheMsV2Name")
                Case McLoginType.Legacy
                    Return Settings.Get(Of String)("LoginLegacyName").BeforeFirst("¨")
                Case McLoginType.Auth
                    Return Settings.Get(Of String)("CacheAuthName")
            End Select
        Catch ex As Exception
        End Try
        Return ""
    End Function

    Private Sub InitTcUi()
        If TcUiInited Then Return
        TcUiInited = True
        AddHandler BtnTcCreate.Click, Sub(s, e) TcCreateRoom()
        AddHandler BtnTcJoin.Click, Sub(s, e) TcJoinRoom()
        AddHandler BtnTcCopy.Click, Sub(s, e) TcCopyRoom()
        AddHandler BtnTcLeave.Click, Sub(s, e) TcLeaveRoom()
        AddHandler TcTimer.Tick, AddressOf TcTick
    End Sub

    Private Sub NormalizeTcPage()
        BtnTcCreate.IsEnabled = True
        BtnTcJoin.IsEnabled = True
        If TcState = "host-ok" OrElse TcState = "guest-ok" OrElse TcState = "host-scanning" OrElse TcState = "host-starting" OrElse TcState = "guest-connecting" OrElse TcState = "guest-starting" Then Return
        PanTcRoom.Visibility = Visibility.Collapsed
        TcRoom = ""
        TcState = "waiting"
        TcTimer.Stop()
        LabTcHint.Text = "点击左侧「创建房间」或「加入房间」开始联机。"
        If TcPort <> 0 Then
            Dim CurrentPort As Integer = TcPort
            RunInNewThread(Sub()
                               ModTerracotta.ResetState(CurrentPort)
                           End Sub, "Tc Normalize")
        End If
    End Sub

    Private Sub EnsureTcServer(Continuation As Action)
        If TcPort <> 0 Then
            Continuation()
            Return
        End If
        LabTcHint.Text = "正在启动陶瓦联机组件……"
        RunInNewThread(
        Sub()
            Try
                If Not ModTerracotta.IsInstalled() Then
                    Dim Confirm As Integer = -1
                    RunInUiWait(Sub() Confirm = MyMsgBox("陶瓦联机组件尚未下载，是否现在下载？" & vbCrLf & vbCrLf & "组件约 8 MB，会保存到 PCL\Terracotta 文件夹。", "陶瓦联机", "下载组件", "取消"))
                    If Confirm <> 1 Then
                        RunInUi(Sub() LabTcHint.Text = "已取消下载陶瓦联机组件")
                        Return
                    End If
                    RunInUi(Sub() LabTcHint.Text = "正在下载陶瓦联机组件……")
                    ModTerracotta.EnsureExecutable()
                End If
                Dim NewPort As Integer = ModTerracotta.StartServer()
                RunInUi(
                Sub()
                    TcPort = NewPort
                    Continuation()
                End Sub)
            Catch ex As Exception
                RunInUi(Sub() LabTcHint.Text = "启动失败：" & ex.Message)
                Logger.Warn(ex, "启动陶瓦联机组件失败")
            End Try
        End Sub, "Tc Ensure")
    End Sub

    Private Sub TcCreateRoom()
        If TcBusy Then Return
        TcBusy = True
        LabTcHint.Text = "正在创建房间……"
        EnsureTcServer(
        Sub()
            RunInNewThread(
            Sub()
                Dim Err As String = Nothing
                Try
                    ModTerracotta.ResetState(TcPort)
                    ModTerracotta.HttpGet("http://127.0.0.1:" & TcPort & "/state/scanning?player=" & Uri.EscapeDataString(TcPlayerName()))
                Catch ex As Exception
                    Err = ex.Message
                End Try
                RunInUi(
                Sub()
                    TcBusy = False
                    If Err IsNot Nothing Then
                        LabTcHint.Text = "创建房间失败：" & Err
                    Else
                        TcTimer.Start()
                    End If
                End Sub)
            End Sub, "Tc Create")
        End Sub)
    End Sub

    Private Sub TcJoinRoom()
        If TcBusy Then Return
        Dim Code As String = MyMsgBoxInput("加入房间", "输入房主发给你的邀请码。", "")
        If String.IsNullOrWhiteSpace(Code) Then Return
        TcBusy = True
        LabTcHint.Text = "正在加入房间……"
        EnsureTcServer(
        Sub()
            RunInNewThread(
            Sub()
                Dim Err As String = Nothing
                Try
                    ModTerracotta.ResetState(TcPort)
                    ModTerracotta.HttpGet("http://127.0.0.1:" & TcPort & "/state/guesting?room=" & Uri.EscapeDataString(Code.Trim()) & "&player=" & Uri.EscapeDataString(TcPlayerName()))
                Catch ex As Exception
                    Err = ex.Message
                End Try
                RunInUi(
                Sub()
                    TcBusy = False
                    If Err IsNot Nothing Then
                        LabTcHint.Text = "加入房间失败：" & Err
                    Else
                        TcTimer.Start()
                    End If
                End Sub)
            End Sub, "Tc Join")
        End Sub)
    End Sub

    Private Sub TcTick(sender As Object, e As EventArgs)
        If TcPort = 0 OrElse TcBusy Then Return
        TcBusy = True
        Dim CurrentPort As Integer = TcPort
        RunInNewThread(
        Sub()
            Dim Json As JObject = Nothing
            Dim Err As String = Nothing
            Try
                Json = JObject.Parse(ModTerracotta.HttpGet("http://127.0.0.1:" & CurrentPort & "/state"))
            Catch ex As Exception
                Err = ex.Message
            End Try
            RunInUi(
            Sub()
                TcBusy = False
                If Err IsNot Nothing Then
                    LabTcHint.Text = "陶瓦联机连接中断：" & Err
                    TcPort = 0
                    TcState = "exception"
                    PanTcRoom.Visibility = Visibility.Collapsed
                    TcRoom = ""
                    BtnTcCreate.IsEnabled = True
                    BtnTcJoin.IsEnabled = True
                    TcTimer.Stop()
                    Return
                End If
                UpdateTcUi(Json)
            End Sub)
        End Sub, "Tc Poll")
    End Sub

    Private Sub UpdateTcUi(Json As JObject)
        If Json Is Nothing Then Return
        Dim State As String = If(Json("state") Is Nothing, "", Json("state").ToString())
        TcState = State
        Dim Room As String = If(Json("room") Is Nothing, "", Json("room").ToString())
        Dim Profiles As JArray = Nothing
        If Json("profiles") IsNot Nothing Then Profiles = TryCast(Json("profiles"), JArray)
        Dim HostName As String = ""
        Dim Count As Integer = If(Profiles Is Nothing, 0, Profiles.Count)
        If Profiles IsNot Nothing AndAlso Profiles.Count > 0 AndAlso Profiles(0)("name") IsNot Nothing Then HostName = Profiles(0)("name").ToString()
        Dim Url As String = If(Json("url") Is Nothing, "", Json("url").ToString())

        Select Case State
            Case "waiting"
                PanTcRoom.Visibility = Visibility.Collapsed
                TcRoom = ""
                TcTimer.Stop()
                LabTcHint.Text = "点击左侧「创建房间」或「加入房间」开始联机。"
            Case "host-scanning"
                LabTcHint.Text = "正在扫描本机 Minecraft 端口……请先在游戏里进入存档并「对局域网开放」。"
            Case "host-starting"
                LabTcHint.Text = "正在创建房间……"
            Case "host-ok"
                TcRoom = Room
                LabTcHint.Text = "房间已创建，把邀请码发给朋友。"
                ShowRoom(If(HostName = "", "房主", HostName), Count, Room, "房主：本机局域网世界" & vbCrLf & "游戏版本：" & If(McInstanceSelected Is Nothing, "未选择", McInstanceSelected.Name))
            Case "guest-connecting", "guest-starting"
                LabTcHint.Text = "正在加入房间……"
            Case "guest-ok"
                TcRoom = Room
                LabTcHint.Text = "已加入房间，回到游戏多人列表进入「陶瓦联机大厅」。"
                ShowRoom(If(HostName = "", "房主", HostName), Count, Room, "游戏版本：" & If(McInstanceSelected Is Nothing, "未选择", McInstanceSelected.Name) & If(Url = "", "", vbCrLf & "备用直连：" & Url))
            Case "exception"
                Dim Type As Integer = If(Json("type") Is Nothing, -1, Json("type").ToObject(Of Integer)())
                Select Case Type
                    Case 0 : LabTcHint.Text = "无法连接房主（房间已关闭或网络不稳定）。"
                    Case 1 : LabTcHint.Text = "与房主的连接中断。"
                    Case 2 : LabTcHint.Text = "加入方联机组件崩溃。"
                    Case 3 : LabTcHint.Text = "房主联机组件崩溃。"
                    Case 4 : LabTcHint.Text = "房主已退出存档，房间自动关闭。"
                    Case Else : LabTcHint.Text = "联机出现异常（" & Type & "）。"
                End Select
                PanTcRoom.Visibility = Visibility.Collapsed
                TcRoom = ""
                TcTimer.Stop()
            Case Else
                LabTcHint.Text = "状态：" & State
        End Select
    End Sub

    Private Sub ShowRoom(HostName As String, Count As Integer, Room As String, WorldInfo As String)
        PanTcRoom.Visibility = Visibility.Visible
        LabTcHost.Text = HostName
        LabTcAvatar.Text = If(String.IsNullOrWhiteSpace(HostName), "?", HostName.Substring(0, 1).ToUpper())
        LabTcCount.Text = "房间内 " & Math.Max(1, Count) & " 人"
        LabTcCode.Text = "邀请码：" & Room
        LabTcWorld.Text = WorldInfo
        BtnTcCopy.IsEnabled = Room <> ""
    End Sub

    Private Sub TcCopyRoom()
        If TcRoom = "" Then Return
        ClipboardSet(TcRoom)
        Hint("邀请码已复制", HintType.Green)
    End Sub

    Private Sub TcLeaveRoom()
        If TcPort = 0 Then Return
        Dim CurrentPort As Integer = TcPort
        TcTimer.Stop()
        PanTcRoom.Visibility = Visibility.Collapsed
        TcRoom = ""
        LabTcHint.Text = "正在退出房间……"
        RunInNewThread(
        Sub()
            ModTerracotta.ResetState(CurrentPort)
            RunInUi(Sub() LabTcHint.Text = "已退出房间。点击左侧「创建房间」或「加入房间」重新开始。")
        End Sub, "Tc Leave")
    End Sub

    Private Sub PageLinkMain_Enter() Handles Me.PageEnter
        InitTcUi()
        NormalizeTcPage()
        If TerracottaPrompted Then Return
        If ModTerracotta.IsInstalled() Then Return
        TerracottaPrompted = True
        If MyMsgBox("陶瓦联机组件尚未下载，是否现在下载？下载后即可在启动器内创建或加入房间。" & vbCrLf & vbCrLf &
                    "组件约 8 MB，会保存到 PCL\Terracotta 文件夹。", "陶瓦联机", "下载组件", "取消") <> 1 Then Return
        Hint("正在下载陶瓦联机组件……", HintType.Blue)
        RunInNewThread(
            Sub()
                Try
                    ModTerracotta.EnsureExecutable()
                    RunInUi(Sub() Hint("陶瓦联机组件已安装，点击「陶瓦联机」即可使用", HintType.Green))
                Catch ex As Exception
                    RunInUi(Sub() Hint("下载陶瓦联机组件失败：" & ex.Message, HintType.Red))
                    Logger.Warn(ex, "下载陶瓦联机组件失败")
                End Try
            End Sub, "Terracotta Download")
    End Sub

    Public Enum LinkStates
        Waiting
        Loading
        Failed
        Finished
    End Enum
    Public Shared LinkState As LinkStates = LinkStates.Waiting

    ''' <summary>
    ''' 切换到指定的状态。
    ''' </summary>
    Private Sub ChangeState(NewState As LinkStates)
        If LinkState = NewState Then Return
        Dim OldState = LinkState
        LinkState = NewState
        Logger.Info($"主状态由 {OldState} 变更为 {NewState}")
        '触发状态切换
        RunInUi(
        Sub()
            Select Case NewState
                Case LinkStates.Waiting
                    SwitchToWaiting(OldState)
                Case LinkStates.Loading
                    SwitchToLoading(OldState)
                Case LinkStates.Failed
                    SwitchToFailed(OldState)
                Case LinkStates.Finished
                    SwitchToFinished(OldState)
            End Select
        End Sub)
    End Sub

    '让加载器的 成功/失败 事件触发状态切换
    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanContent, PanAlways, LinkLoader, AutoRun:=False)
        AddHandler LinkLoader.OnStateChangedUi,
        Sub(Loader As LoaderBase, NewState As LoadState, OldState As LoadState)
            Select Case NewState
                Case LoadState.Finished
                    ChangeState(LinkStates.Finished)
                Case LoadState.Failed
                    Telemetry("联机失败", "Exception", FilterUserName(Loader.Error.GetDisplay(True), "*"))
                    ChangeState(LinkStates.Failed)
            End Select
        End Sub
    End Sub

    '页面动画兼容
    Private Sub UpdatePanelVisibility() Handles Me.PageEnter
        FrmLinkMain.PanSelect.Visibility = If(LinkState = LinkStates.Waiting, Visibility.Visible, Visibility.Collapsed)
        FrmLinkMain.PanFinish.Visibility = If(LinkState = LinkStates.Finished, Visibility.Visible, Visibility.Collapsed)
    End Sub

#End Region

#Region "初始页面 (Waiting)"

    '由任意状态切换到 Waiting
    Private Sub SwitchToWaiting(OldState As LinkStates)
        '加载器与进程状态
        LinkLoader.Cancel()
        SyncLock LinkLoader.LockState
            LinkLoader.State = LoadState.Waiting
        End SyncLock
        ProcessStop()
        '页面切换
        If OldState <> LinkStates.Finished Then
            '从完成页面退出时，PageOnContentExit 结束会触发 PageEnter 事件，这会调用 UpdatePanelVisibility
            '如果在现在提前调用了，会丢失完成页面的退出动画
            UpdatePanelVisibility()
        End If
        PageOnContentExit()
    End Sub

    '===============================
    '  页面 UI
    '===============================

    '创建
    Private Sub PanSelectTerracotta_MouseLeftButtonUp() Handles PanSelectTerracotta.MouseLeftButtonUp
        If LinkState <> LinkStates.Waiting Then
            Hint("请先退出当前的联机房间！", HintType.Red)
            Return
        End If
        Dim Frm As New FrmTerracotta
        Frm.ShowDialog()
    End Sub

    Private Shared Sub StartTerracotta()
        RunInNewThread(
        Sub()
            Try
                Dim Folder As String = PathPure.Value & "Terracotta\"
                DirectoryUtils.Create(Folder)
                Dim ExePath As String = IO.Path.Combine(Folder, "terracotta.exe")
                If Not IO.File.Exists(ExePath) Then
                    ExePath = IO.Directory.GetFiles(Folder, "*.exe", IO.SearchOption.AllDirectories).FirstOrDefault()
                End If
                If ExePath Is Nothing OrElse Not IO.File.Exists(ExePath) Then
                    RunInUi(Sub() Hint("正在下载陶瓦联机，请稍候……", HintType.Blue))
                    Dim Archive As String = IO.Path.Combine(Folder, "terracotta.tar.gz")
                    Dim Url As String = "https://github.com/burningtnt/Terracotta/releases/download/v0.4.2/terracotta-0.4.2-windows-x86_64-pkg.tar.gz"
                    NetDownloadByLoader(Url, Archive)
                    RunInUi(Sub() Hint("正在解压陶瓦联机……", HintType.Blue))
                    Dim TarProcess As Process = Process.Start(New ProcessStartInfo("tar.exe", "-xzf """ & Archive & """ -C """ & Folder.TrimEnd("\"c) & """") With {.UseShellExecute = False, .CreateNoWindow = True})
                    TarProcess.WaitForExit()
                    Try
                        IO.File.Delete(Archive)
                    Catch ex As Exception
                    End Try
                    Dim Found As String = IO.Directory.GetFiles(Folder, "*.exe", IO.SearchOption.AllDirectories).FirstOrDefault()
                    If Found Is Nothing Then
                        RunInUi(Sub() Hint("陶瓦下载完成，请手动打开文件夹运行：" & Folder, HintType.Red))
                        Return
                    End If
                    ExePath = Found
                End If
                RunInUi(
                Sub()
                    Try
                        Process.Start(New ProcessStartInfo(ExePath) With {.WorkingDirectory = IO.Path.GetDirectoryName(ExePath), .UseShellExecute = True})
                        Hint("已启动陶瓦联机，创建或加入房间后回到游戏多人列表进入", HintType.Green)
                    Catch ex As Exception
                        Hint("启动陶瓦失败：" & ex.Message, HintType.Red)
                    End Try
                End Sub)
            Catch ex As Exception
                RunInUi(Sub() Hint("下载陶瓦联机失败：" & ex.Message, HintType.Red))
                Logger.Warn(ex, "启动陶瓦联机失败")
            End Try
        End Sub, "Terracotta Start")
    End Sub

    Public Shared Function GetIPv6Addresses() As List(Of String)
        Dim Addresses As New List(Of String)
        Try
            For Each Adapter In NetworkInterface.GetAllNetworkInterfaces()
                If Adapter.OperationalStatus <> OperationalStatus.Up Then Continue For
                If Adapter.NetworkInterfaceType = NetworkInterfaceType.Loopback Then Continue For
                Try
                    For Each Uni In Adapter.GetIPProperties().UnicastAddresses
                        Dim Addr = Uni.Address
                        If Addr.AddressFamily <> AddressFamily.InterNetworkV6 Then Continue For
                        If Addr.IsIPv6LinkLocal OrElse Addr.IsIPv6SiteLocal OrElse Addr.IsIPv6Multicast Then Continue For
                        Dim AddrText As String = Addr.ToString().Split("%"c)(0)
                        If AddrText.StartsWithF("fe80", True) OrElse AddrText.StartsWithF("fc", True) OrElse AddrText.StartsWithF("fd", True) Then Continue For
                        If Not Addresses.Contains(AddrText) Then Addresses.Add(AddrText)
                    Next
                Catch ex As Exception
                End Try
            Next
            Addresses.Sort()
        Catch ex As Exception
            Logger.Warn(ex, "获取本机 IPv6 地址失败")
        End Try
        Return Addresses
    End Function

    Public Shared Function GetPublicIPv4() As String
        For Each Api As String In {"https://ipv4.icanhazip.com", "https://api.ipify.org", "https://ifconfig.me/ip"}
            Try
                Dim Text As String = NetRequestByLoader(Api, 8000, False).Trim()
                Dim Result As IPAddress = Nothing
                If IPAddress.TryParse(Text, Result) AndAlso Result.AddressFamily = AddressFamily.InterNetwork Then
                    Return Result.ToString()
                End If
            Catch ex As Exception
            End Try
        Next
        Return Nothing
    End Function

    Private Shared Sub CreateInviteAsync()
        RunInNewThread(
        Sub()
            Try
                Dim V6 As List(Of String) = GetIPv6Addresses()
                Dim V4 As String = GetPublicIPv4()
                If V6.Count = 0 AndAlso V4 Is Nothing Then
                    RunInUi(Sub() MyMsgBox("没有检测到可用的公网 IPv4 或 IPv6 地址。" & vbCrLf & vbCrLf & "IPv4 需要在路由器上做端口转发，IPv6 需要网络支持 IPv6。", "IP 邀请码", "知道了"))
                    Return
                End If
                Dim UseIPv6 As Boolean
                If V6.Count > 0 AndAlso V4 IsNot Nothing Then
                    Dim Choice As Integer = -1
                    RunInUiWait(
                    Sub()
                        Choice = MyMsgBox("选择邀请使用的地址类型：" & vbCrLf & vbCrLf &
                                          "IPv6：通常可以直接连接，无需端口转发。" & vbCrLf &
                                          "IPv4：需要在路由器上把端口转发到本机。", "IP 邀请码", "IPv6（推荐）", "IPv4", "取消")
                    End Sub)
                    If Choice <> 1 AndAlso Choice <> 2 Then Return
                    UseIPv6 = (Choice = 1)
                Else
                    UseIPv6 = (V6.Count > 0)
                End If
                Dim Address As String = If(UseIPv6, V6(0), V4)
                Dim PortText As String = Nothing
                RunInUiWait(Sub() PortText = MyMsgBoxInput("端口", "输入世界或服务器的端口。" & vbCrLf & "局域网世界请填游戏里显示的端口，专用服务器默认 25565。", "25565"))
                If String.IsNullOrWhiteSpace(PortText) Then Return
                Dim Port As Integer
                If Not Integer.TryParse(PortText.Trim(), Port) OrElse Port < 1 OrElse Port > 65535 Then
                    RunInUi(Sub() Hint("端口无效，应为 1-65535 之间的数字", HintType.Red))
                    Return
                End If
                Dim Code As String = BuildInviteCode(Address, Port)
                Dim WarnText As String = If(UseIPv6,
                    "首次使用请确保防火墙已放行端口 " & Port & "。",
                    "需要在路由器上把端口 " & Port & " 转发到本机；如果运营商是 CGNAT，IPv4 直连将不可用。")
                Dim Content As String = "邀请码：" & vbCrLf & vbCrLf & Code & vbCrLf & vbCrLf &
                    "把它发给朋友，让对方在「联机 → IP 直连 → 使用邀请码」中粘贴。" & vbCrLf & WarnText
                Dim Copy As Boolean = False
                RunInUiWait(Sub() Copy = (MyMsgBox(Content, "IP 邀请码", "复制邀请码", "关闭") = 1))
                If Copy Then
                    ClipboardSet(Code)
                    RunInUi(Sub() Hint("邀请码已复制", HintType.Green))
                End If
            Catch ex As Exception
                RunInUi(Sub() Hint("生成邀请码失败：" & ex.Message, HintType.Red))
                Logger.Warn(ex, "生成 IP 邀请码失败")
            End Try
        End Sub, "IP Invite Create")
    End Sub

    Private Shared Sub JoinInvite()
        Try
            Dim Code As String = MyMsgBoxInput("使用邀请码", "粘贴朋友发给你的 IP 邀请码。", "")
            If String.IsNullOrWhiteSpace(Code) Then Return
            Dim Address As String = Nothing
            Dim Port As Integer = 0
            If Not ParseInviteCode(Code, Address, Port) Then
                MyMsgBox("邀请码无效或已损坏，请让朋友重新生成。", "IP 邀请码", "知道了")
                Return
            End If
            Dim IsIPv6 As Boolean = Address.Contains(":")
            Dim Target As String = If(IsIPv6, "[" & Address & "]", Address) & ":" & Port
            Dim Content As String = "连接地址：" & vbCrLf & vbCrLf & Target & vbCrLf & vbCrLf &
                "选择「启动游戏并进入」会自动启动当前版本并进入服务器。" & vbCrLf &
                "如果游戏已经打开，会关闭后重新启动再进入。"
            Select Case MyMsgBox(Content, "IP 邀请码", "启动游戏并进入", "复制地址", "关闭")
                Case 1
                    LaunchIPServer(Address, Port)
                Case 2
                    ClipboardSet(Target)
                    Hint("连接地址已复制", HintType.Green)
            End Select
        Catch ex As Exception
            Hint("使用邀请码失败：" & ex.Message, HintType.Red)
            Logger.Warn(ex, "使用 IP 邀请码失败")
        End Try
    End Sub

    Private Shared Sub LaunchIPServer(Address As String, Port As Integer)
        Try
            If McInstanceSelected Is Nothing Then
                Hint("请先在启动页选择一个 Minecraft 版本", HintType.Red)
                Return
            End If
            If McLaunchLoader.State = LoadState.Loading Then
                Hint("游戏正在启动中，请稍后再试", HintType.Red)
                Return
            End If
            Dim ServerIp As String = If(Address.Contains(":"), "[" & Address & "]", Address) & ":" & Port
            Dim Proc As Process = McLaunchProcess
            If Proc IsNot Nothing AndAlso Not Proc.HasExited Then
                If MyMsgBox("检测到游戏正在运行。自动进入需要关闭当前游戏并重新启动，是否继续？", "IP 直连", "重启并进入", "取消") <> 1 Then Return
                Try
                    Proc.Kill()
                Catch ex As Exception
                    Logger.Warn(ex, "关闭 Minecraft 失败")
                End Try
                RunInNewThread(
                    Sub()
                        Try
                            Proc.WaitForExit(8000)
                        Catch ex As Exception
                        End Try
                        RunInUi(Sub() StartLaunchToServer(ServerIp))
                    End Sub, "IP Restart MC")
                Return
            End If
            StartLaunchToServer(ServerIp)
        Catch ex As Exception
            Hint("自动进入失败：" & ex.Message, HintType.Red)
            Logger.Warn(ex, "IP 自动进入失败")
        End Try
    End Sub

    Private Shared Sub StartLaunchToServer(ServerIp As String)
        Try
            If McLaunchStart(New McLaunchOptions With {.ServerIp = ServerIp}) Then
                Hint("正在启动游戏并进入服务器……", HintType.Blue)
            End If
        Catch ex As Exception
            Hint("启动游戏失败：" & ex.Message, HintType.Red)
            Logger.Warn(ex, "IP 自动进入启动失败")
        End Try
    End Sub

    Public Shared Function BuildInviteCode(Address As String, Port As Integer) As String
        Dim IP As IPAddress = IPAddress.Parse(Address)
        Dim Family As Byte = If(IP.AddressFamily = AddressFamily.InterNetwork, CByte(4), CByte(6))
        Dim Bytes As New List(Of Byte) From {Family}
        Bytes.AddRange(IP.GetAddressBytes())
        Bytes.Add(CByte((Port >> 8) And &HFF))
        Bytes.Add(CByte(Port And &HFF))
        Dim Check As Byte = &H5A
        For Each B As Byte In Bytes
            Check = CByte(Check Xor B)
        Next
        Bytes.Add(Check)
        Dim Encoded As String = Convert.ToBase64String(Bytes.ToArray()).Replace("+"c, "-"c).Replace("/"c, "_"c).TrimEnd("="c)
        Return "PCLIP-" & Encoded
    End Function

    Public Shared Function ParseInviteCode(Code As String, ByRef Address As String, ByRef Port As Integer) As Boolean
        Address = Nothing
        Port = 0
        Try
            Code = If(Code, "").Trim()
            Dim Legacy As Boolean = Code.StartsWithF("PCL6-", True)
            If Not Legacy AndAlso Not Code.StartsWithF("PCLIP-", True) Then Return False
            Dim Encoded As String = Code.Substring(If(Legacy, 5, 6)).Replace("-"c, "+"c).Replace("_"c, "/"c)
            Select Case Encoded.Length Mod 4
                Case 2 : Encoded &= "=="
                Case 3 : Encoded &= "="
            End Select
            Dim Bytes As Byte() = Convert.FromBase64String(Encoded)
            Dim Family As Integer
            Dim AddrStart As Integer
            Dim AddrLength As Integer
            If Legacy Then
                Family = 6
                AddrStart = 0
                AddrLength = 16
                If Bytes.Length <> 19 Then Return False
            Else
                If Bytes.Length < 8 Then Return False
                Family = CInt(Bytes(0))
                AddrStart = 1
                If Family = 4 Then
                    AddrLength = 4
                    If Bytes.Length <> 8 Then Return False
                ElseIf Family = 6 Then
                    AddrLength = 16
                    If Bytes.Length <> 20 Then Return False
                Else
                    Return False
                End If
            End If
            Dim Check As Byte = &H5A
            For i = 0 To Bytes.Length - 2
                Check = CByte(Check Xor Bytes(i))
            Next
            If Check <> Bytes(Bytes.Length - 1) Then Return False
            Dim AddrBytes(AddrLength - 1) As Byte
            Array.Copy(Bytes, AddrStart, AddrBytes, 0, AddrLength)
            Dim IP As New IPAddress(AddrBytes)
            Address = IP.ToString()
            Port = (CInt(Bytes(AddrStart + AddrLength)) << 8) Or CInt(Bytes(AddrStart + AddrLength + 1))
            Return Port > 0 AndAlso Port <= 65535
        Catch ex As Exception
            Return False
        End Try
    End Function

    Private Sub Create_MouseLeftButtonUp() Handles PanSelectCreate.MouseLeftButtonUp
        '输入端口号
        Dim Port As String = MyMsgBoxInput("输入端口", $"在单人游戏的暂停菜单选择 {vbLQ}对局域网开放{vbRQ}，然后输入端口数字。{vbCrLf}甚至可以输入其他游戏的端口……嗯……",
            ValidateRules:=New Collection(Of Validate) From {New ValidateInteger(1024, 65535)},
            HintText:="端口号")
        If Port Is Nothing Then Return
        '基础信息
        IsServerSide = True
        ServerPort = Port
        Dim GenerateRandomCode =
        Function() As String
            Dim Result As String = ""
            For i = 1 To 5
                Result &= RandomInteger(0, 35).ConvertRadix(36)
            Next
            Return Result.Replace("O", "0").Replace("I", "1")
        End Function
        NetworkName = $"P{ServerPort.ConvertRadix(16).PadLeft(4, "0"c)}-{GenerateRandomCode()}"
        NetworkSecret = GenerateRandomCode()
        DiscoverNodeID = -1
        Logger.Info($"尝试创建房间，网络名 {NetworkName}，网络密码 {NetworkSecret}，端口 {ServerPort}")
        '启动
        ChangeState(LinkStates.Loading)
    End Sub

    '加入
    Private Shared LastCode As String = Nothing
    ''' <summary>
    ''' 输入邀请码，切换到联机页并立即加入房间。
    ''' </summary>
    Public Shared Sub Join() Handles PanSelectJoin.MouseLeftButtonUp
        Dim Code As String = MyMsgBoxInput("输入邀请码", "输入房主发给你的邀请码。",
            HintText:=If(String.IsNullOrEmpty(LastCode), "", "使用上一次的邀请码"))
        If Not String.IsNullOrEmpty(LastCode) AndAlso Code IsNot Nothing AndAlso Code = "" Then Code = LastCode
        If Code Is Nothing Then Return
        Join(Code)
    End Sub
    Public Sub JoinInternal(Code As String)
        If Code Is Nothing Then Return
        '基础格式校验
        Dim ValidateResult = ValidateCodeFormat(Code)
        If ValidateResult IsNot Nothing Then
            Hint(ValidateResult, HintType.Red)
            Return
        End If
        Code = FixCodeFormat(Code)
        Logger.Info($"实际使用的邀请码：{Code}")
        '基础信息
        IsServerSide = False
        ServerPort = Code.Substring(1, 4).Upper.ConvertRadix(16, 10)
        NetworkName = Code.Substring(0, 11)
        NetworkSecret = Code.Substring(12, 5)
        If Code.Substring(20, 3) = "000" Then
            DiscoverNodeID = -2
        Else
            DiscoverNodeID = Code.Substring(20, 3).Upper.ConvertRadix(16, 10)
        End If
        Logger.Info($"尝试加入房间，网络名 {NetworkName}，网络密码 {NetworkSecret}，端口 {ServerPort}，发现节点 {DiscoverNodeID}")
        '启动
        LastCode = Code
        ChangeState(LinkStates.Loading)
    End Sub
    Public Shared Function ValidateCodeFormat(Code As String) As String
        If Code Is Nothing Then Return "邀请码为空！"
        Code = FixCodeFormat(Code)
        '判断类型
        If Not (Code.Length >= 14 AndAlso Code(0) = "P"c AndAlso Code(5) = "-"c AndAlso Code(11) = "-"c) Then
            If Code.StartsWithF("U/") Then 'HMCL
                Return "请让房主使用 PCL 创建房间！"
            ElseIf Code.Length = 10 Then 'PCL CE
                Return "请让房主使用非社区版的 PCL 创建房间！"
            Else
                Return "邀请码有误，请让房主使用 PCL 创建房间！"
            End If
        End If
        '校验版本
        If Code.Length >= 23 AndAlso Code(17) = "-"c AndAlso
            Val(Code.Substring(18, 2)) > INVITE_CODE_VERSION Then Return "你的 PCL 版本太老了，请在更新 PCL 之后再联机！"
        Return Nothing
    End Function
    Private Shared Function FixCodeFormat(Code As String) As String
        Code = Code.Between("【", "】").Between("[", "]") '从完整消息中提取
        Code = Code.Upper.Replace("O", "0").Replace("I", "1") '输入修正
        '版本 1 兼容
        If Code.Length >= 17 AndAlso (Code.Length < 23 OrElse (Code.Length >= 18 AndAlso Code(17) <> "-"c)) Then
            Code = Code.Substring(0, 17) & "-0105E"
        End If
        Return Code
    End Function

    '自动加入

    ''' <summary>
    ''' 切换到联机页并立即加入指定房间。
    ''' </summary>
    Public Shared Sub Join(Code As String)
        If LinkState <> LinkStates.Waiting Then
            Hint("你已经在联机房间中了！", HintType.Red)
        ElseIf FrmMain.PageCurrent = FormMain.PageType.Link Then
            FrmLinkMain.JoinInternal(Code)
        Else
            AutoJoinCode = Code
            FrmMain.PageChange(FormMain.PageType.Link, FormMain.PageSubType.LinkMain)
        End If
    End Sub
    '在进入页面时自动尝试加入房间
    Private Shared AutoJoinCode As String = Nothing
    Private Sub PanSelectJoin_Loaded() Handles PanSelectJoin.Loaded
        If AutoJoinCode Is Nothing Then Return
        If LinkState = LinkStates.Waiting Then JoinInternal(AutoJoinCode)
        AutoJoinCode = Nothing
    End Sub

#End Region

#Region "加载页面 (Loading | Failed)"

    '由 Waiting 或 Failed 状态切换到 Loading
    Private Sub SwitchToLoading(OldState As LinkStates)
        '清理变量
        Peers = Nothing
        FailCount = 0
        '加载器与进程状态
        LinkLoader.Start(IsForceRestart:=True)
        '页面切换会由 Loader 调用 MyPageRight 来触发
        'UI 更新
        UpdateProgressBar(0)
        LabLoadTitle.Text = If(IsServerSide, "创建房间中", "加入房间中")
        UpdateLoadingPage("正在初始化……", "准备初始化")
    End Sub

    '由 Loading 状态切换到 Failed
    Private Sub SwitchToFailed(OldState As LinkStates)
        '加载器与进程状态
        LinkLoader.Cancel()
        ProcessStop()
        'UI 更新
        UpdateProgressBar(1)
        '显示错误信息
        LabLoadTitle.Text = FailReason
        Dim Brief As String = LinkLoader.Error.GetDisplay(False)
        Dim InnerEx As Exception = LinkLoader.Error
        If InnerEx.Message.StartsWithF("$") Then Brief = InnerEx.Message
        Do Until InnerEx.InnerException Is Nothing
            InnerEx = InnerEx.InnerException
            If InnerEx.Message.StartsWithF("$") Then Brief = InnerEx.Message
        Loop
        LabLoadDesc.Text = If(Brief.StartsWithF("$"), Brief.TrimStart("$"), LinkLoader.Error.GetDisplay(True))
        Logger.Warn(LinkLoader.Error, LabLoadTitle.Text)
    End Sub

    '===============================
    '  页面 UI
    '===============================

    Private Sub UpdateLoadingPage(Title As String, FailBrief As String)
        If FailReason = FailBrief & "失败" Then Return
        FailReason = FailBrief & "失败"
        Logger.Info($"开始步骤：{Title}")
        RunInUiWait(
        Sub()
            If FrmLinkMain Is Nothing OrElse Not FrmLinkMain.LabLoadDesc.IsLoaded Then Return
            FrmLinkMain.LabLoadDesc.Text = Title
        End Sub)
    End Sub
    Private FailReason As String = "准备初始化"

    '取消加载
    Private Sub BtnLoadCancel_Click() Handles BtnLoadCancel.Click
        ChangeState(LinkStates.Waiting)
    End Sub

    '点击重试
    Private Sub CardLoad_MouseLeftButtonUp() Handles CardLoad.MouseLeftButtonUp
        ChangeState(LinkStates.Loading)
    End Sub

    '进度条
    Private Sub UpdateProgressBar(Optional Value As Double = -1)
        If Value = -1 Then Value = LinkLoader.Progress
        Dim DisplayingProgress As Double = ColumnProgressA.Width.Value
        If Math.Round(Value - DisplayingProgress, 3) = 0 Then Return
        If DisplayingProgress <= Value Then
            Dim NewProgress As Double = If(Value = 1, 1, DisplayingProgress +
                (Value - DisplayingProgress) ^ 2 * 0.5)
            AniStart({
                AaGridLengthWidth(ColumnProgressA, NewProgress - ColumnProgressA.Width.Value, 200),
                AaGridLengthWidth(ColumnProgressB, (1 - NewProgress) - ColumnProgressB.Width.Value, 200)
            }, "Link Progress")
        Else
            ColumnProgressA.Width = New GridLength(Value, GridUnitType.Star)
            ColumnProgressB.Width = New GridLength(1 - Value, GridUnitType.Star)
            AniStop("Link Progress")
        End If
    End Sub
    Private Sub CardLoad_SizeChanged() Handles CardLoad.SizeChanged
        RectProgressClip.Rect = New Rect(0, 0, CardLoad.ActualWidth, 12)
    End Sub

#End Region

#Region "完成页面 (Finished)"

    '由 Loading 状态切换到 Finished
    Private Sub SwitchToFinished(OldState As LinkStates)
        '由于只可能从加载器的完成事件触发，不需要管加载器
        UpdatePanelVisibility() '页面的实际切换会由 Loader 调用 MyPageRight 来触发

        'UI 更新
        If IsServerSide Then
            LabFinishTitle.Text = "已创建房间"
            LabFinishDesc.Text = $"把邀请码发给朋友，让大家加入房间吧！{vbCrLf}邀请码：{GetInviteCode()}"
            BtnFinishExit.Text = "关闭"
            BtnFinishPing.ToolTip = "网络延迟"
            BtnFinishCopy.Visibility = Visibility.Visible
            Copy() '立即复制邀请码
            '下边栏
            BtnFinishPort.Visibility = Visibility.Visible
            LabFinishPort.Visibility = Visibility.Visible
            BtnFinishIp.Visibility = Visibility.Collapsed
            LabFinishIp.Visibility = Visibility.Collapsed
            LabFinishPort.Text = ServerPort
        Else
            LabFinishTitle.Text = "已加入房间"
            LabFinishDesc.Text = $"在多人游戏页面的最下方就能找到联机房间！{vbCrLf}注意：使用离线登录时不要手动输入 IP！"
            BtnFinishExit.Text = "离开"
            BtnFinishPing.ToolTip = "与房主的延迟"
            BtnFinishCopy.Visibility = Visibility.Collapsed
            '下边栏
            BtnFinishPort.Visibility = Visibility.Collapsed
            LabFinishPort.Visibility = Visibility.Collapsed
            LabFinishIp.Text = ClientAddress
            BtnFinishIp.Visibility = Visibility.Visible
            LabFinishIp.Visibility = Visibility.Visible
        End If
        Update() '立即刷新
    End Sub

    '===============================
    '  页面 UI
    '===============================

    '退出
    Private Sub BtnFinishExit_Click() Handles BtnFinishExit.Click
        TryExit(False, False)
    End Sub
    ''' <summary>
    ''' 退出联机。
    ''' 返回是否弹出了警告窗口并且玩家选择了取消。
    ''' </summary>
    Public Function TryExit(Silent As Boolean, Closing As Boolean) As Boolean
        If LinkState = LinkStates.Waiting OrElse LinkState = LinkStates.Failed Then Return False
        If Not Silent Then
            If IsServerSide AndAlso PeopleCount > 1 Then
                If MyMsgBox("你确定要关闭联机房间吗？" & vbCrLf & "所有玩家都需要重新输入邀请码才可加入游戏！", "退出联机", "确定", "取消", IsWarn:=True) = 2 Then Return True
            ElseIf Closing Then
                If MyMsgBox(If(IsServerSide, "你确定要关闭联机房间吗？", "你确定要离开联机房间吗？"), "退出联机", "确定", "取消") = 2 Then Return True
            End If
        End If
        ChangeState(LinkStates.Waiting)
        Return False
    End Function

    '复制邀请码
    Private Sub Copy() Handles BtnFinishCopy.Click
        Dim CodeText As String = $"在 PCL 启动器中输入邀请码【{GetInviteCode()}】，即可加入联机房间！"
        ClipboardSet(CodeText, False)
        Settings.Set("LinkLastAutoJoinInviteCode", CodeText)
        Hint("已复制邀请码！", HintType.Green)
    End Sub
    Private Function GetInviteCode() As String
        Return $"{NetworkName}-{NetworkSecret}-{INVITE_CODE_VERSION.ToString.PadLeft(2, "0"c)}{ _
            If(DiscoverNodeID = -1, 0, DiscoverNodeID).ConvertRadix(16).PadLeft(3, "0"c)}"
    End Function

    '复制 IP
    Private Sub BtnFinishIp_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles BtnFinishIp.MouseLeftButtonUp
        ClipboardSet(ClientAddress, False)
        Hint("已复制服务器地址！", HintType.Green)
    End Sub

#End Region

    '===============================
    '  后端
    '===============================

    ''' <summary>
    ''' EasyTier 所在的文件夹路径，以 \ 结尾。
    ''' </summary>
    Private PathEasyTier As String = Paths.AppDataThenName & "EasyTier\"
    ''' <summary>
    ''' 当前是服务端还是客户端。
    ''' </summary>
    Private IsServerSide As Boolean
    ''' <summary>
    ''' 服务端映射前的端口号。
    ''' </summary>
    Private ServerPort As Integer
    ''' <summary>
    ''' 客户端映射后的端口号。
    ''' </summary>
    Private ClientPort As Integer
    ''' <summary>
    ''' RPC 端口号。
    ''' </summary>
    Private RPCPort As Integer
    ''' <summary>
    ''' 在客户端映射后的地址。
    ''' 除非已作为客户端启动 EasyTier，否则一直为 Nothing。
    ''' </summary>
    Private ClientAddress As String = Nothing
    ''' <summary>
    ''' 发现节点的 ID。
    ''' 若必须设定自定义节点则为 -2；若等待选择则为 -1；选择回退节点则为 0；否则为对应节点的 ID。
    ''' </summary>
    Private DiscoverNodeID As Integer = -1
    ''' <summary>
    ''' 网络信息。
    ''' </summary>
    Private NetworkName As String, NetworkSecret As String

#Region "加载"

    Private WithEvents LinkLoader As New LoaderCombo(Of Integer)("联机", {
        New LoaderTask(Of Integer, Integer)("获取配置", AddressOf InitConfig) With {.ProgressWeight = 8},
        New LoaderTask(Of Integer, List(Of NetFile))("准备下载联机模块", AddressOf InitPrepareDownload) With {.ProgressWeight = 2},
        New LoaderDownload("下载联机模块", New List(Of NetFile)) With {.ProgressWeight = 40},
        New LoaderTask(Of Integer, Integer)("启动联机模块", AddressOf InitLaunch) With {.ProgressWeight = 50}
    })

    '1. 获取服务器配置
    Private Sub InitConfig(Task As LoaderTask(Of Integer, Integer))
        UpdateLoadingPage("正在联网获取配置……", "联网获取配置")
        ServerLoader.WaitForExit(LoaderToSyncProgress:=Task)
        If ServerConfig Is Nothing Then Throw New Exception("无法从服务器获取配置")
        If ServerConfig("Link") Is Nothing Then Throw New Exception("服务器未提供联机配置")
        If CType(ServerConfig("Link"), JObject).ContainsKey("MinVersionCode") AndAlso
           VersionCode < ServerConfig("Link")("MinVersionCode").ToObject(Of Integer) Then
            Throw New Exception("$你的 PCL 版本太老了，请在更新 PCL 之后再联机！")
        End If
    End Sub

    '2. 获取需要下载的文件
    Private ServerVersion As Integer
    Private Sub InitPrepareDownload(Task As LoaderTask(Of Integer, List(Of NetFile)))
        '获取 CPU 架构
        UpdateLoadingPage("正在获取 CPU 架构……", "获取 CPU 架构")
        Dim Architecture As String = GetType(String).Assembly.GetName().ProcessorArchitecture
        Select Case Architecture
            Case ProcessorArchitecture.X86
                Architecture = "i686"
            Case ProcessorArchitecture.Amd64
                Architecture = "x86_64"
            Case ProcessorArchitecture.Arm
                Architecture = "arm64"
            Case Else
                Logger.Warn($"CPU 是不支持的 {Architecture} 架构，这可能会导致联机模块无法启动！")
                Architecture = "arm64"
        End Select
        Logger.Info($"CPU 架构：{Architecture}")
        DirectoryUtils.Create(PathEasyTier)
        Telemetry("联机开始")
        '检查 EasyTier 版本
        UpdateLoadingPage("正在检查联机模块版本……", "检查联机模块版本")
        If Not (FileUtils.Exists(PathEasyTier & "联机模块 CLI.exe") AndAlso FileUtils.Exists(PathEasyTier & "联机模块.exe") AndAlso
                FileUtils.Exists(PathEasyTier & "Packet.dll")) Then
            Settings.Set("LinkEasyTierVersion", -1)
        End If
        Dim LocalVersion As Integer = Settings.Get(Of Integer)("LinkEasyTierVersion")
        ServerVersion = ServerConfig("Link")("EasyTierVersion")
        Dim RequiredFiles As New List(Of NetFile)
        Logger.Info($"EasyTier 本地版本：{LocalVersion}，需求版本：{ServerVersion}")
        If LocalVersion < ServerVersion Then
            RequiredFiles.Add(New NetFile(
                ServerConfig("Link")("Downloads").Select(Function(UrlEntry) UrlEntry.ToString.Replace("{arch}", Architecture)),
                PathEasyTier & "EasyTier.zip",
                New FileChecker With {.MinSize = 1024 * 1024 * 2}))
        End If
        '开始下载
        UpdateLoadingPage("正在下载联机模块……", "下载联机模块")
        Task.Output = RequiredFiles
    End Sub

    '3. 启动联机模块
    Private Sub InitLaunch(Task As LoaderTask(Of Integer, Integer))
        '解压文件
        UpdateLoadingPage("正在解压联机模块……", "解压联机模块")
        If FileUtils.Exists(PathEasyTier & "EasyTier.zip") Then
            '解压
            Dim ExtractPath As String = RequestTaskTempFolder()
            FileUtils.ExtractToDirectory(PathEasyTier & "EasyTier.zip", ExtractPath)
            Dim SourceFolder As String = DirectoryUtils.EnumerateDirectories(ExtractPath, True).FirstOrDefault()
            If String.IsNullOrEmpty(SourceFolder) Then SourceFolder = ExtractPath
            DirectoryUtils.Copy(SourceFolder, PathEasyTier)
            '重命名
            FileUtils.Move(PathEasyTier & "easytier-core.exe", PathEasyTier & "联机模块.exe")
            FileUtils.Move(PathEasyTier & "easytier-cli.exe", PathEasyTier & "联机模块 CLI.exe")
            '清理
            FileUtils.Delete(PathEasyTier & "EasyTier.zip")
            Settings.Set("LinkEasyTierVersion", ServerVersion)
        End If
        Task.Progress = 0.07
        '获取节点列表
        UpdateLoadingPage("正在获取节点列表……", "获取节点列表")
        Dim RawPeers As List(Of String)
        Dim CustomPeers As String = Settings.Get(Of String)("LinkCustomPeer")
        If String.IsNullOrWhiteSpace(CustomPeers) Then
            If DiscoverNodeID = -2 AndAlso Not IsServerSide Then
                Panic("未填写自定义节点设置", $"$你必须在 {vbLQ}自定义节点{vbRQ} 设置中填写与房主相同的内容，{vbCrLf}才能进入该房间！")
                Return
            End If
            RawPeers = GetTargetPeers()
        Else
            If DiscoverNodeID <> -2 AndAlso Not IsServerSide Then Hint("房主可能没有使用自定义节点设置，请确认你们的自定义节点设置是否一致！")
            RawPeers = CustomPeers.Split("，,".ToCharArray).Select(Function(p) p.Trim).Where(Function(p) Not String.IsNullOrEmpty(p)).ToList()
            Logger.Info($"使用自定义节点：{CustomPeers}")
        End If
        Task.Progress = 0.13
        '获取空闲端口
        UpdateLoadingPage("正在启动联机模块……", "启动联机模块")
        Dim FreePorts = FindFreePorts(5, ServerPort)
        ClientPort = FreePorts(0)
        RPCPort = FreePorts(1)
        Dim ListenersPort As Integer = FreePorts(2) '它会占用【当前、当前 +1、当前 +2】共三个端口
        '获取启动参数
        Dim Arguments As String = ServerConfig("Link")("Argument")
        Arguments += $" --network-name={NetworkName} --network-secret={NetworkSecret} --listeners {ListenersPort} --rpc-portal {RPCPort} --private-mode true"
        Dim HostName = If(IsServerSide, "Server-", "Client-") & Identify.GetStableHashCode().ConvertRadix(36).TrimStart("-"c).EnsureLength("0", 6)
        If IsServerSide Then
            Arguments += $" -i 10.114.114.114 --hostname={HostName} --tcp-whitelist={ServerPort} --udp-whitelist={ServerPort}"
        Else
            Arguments += $" -d --hostname={HostName} --tcp-whitelist=0 --udp-whitelist=0"
            '端口转发
            ClientAddress = $"localhost:{ClientPort}"
            Arguments += $" --port-forward tcp://[{IPAddress.IPv6Loopback}]:{ClientPort}/10.114.114.114:{ServerPort}"
            Arguments += $" --port-forward udp://[{IPAddress.IPv6Loopback}]:{ClientPort}/10.114.114.114:{ServerPort}"
            Arguments += $" --port-forward tcp://{IPAddress.Loopback}:{ClientPort}/10.114.114.114:{ServerPort}"
            Arguments += $" --port-forward udp://{IPAddress.Loopback}:{ClientPort}/10.114.114.114:{ServerPort}"
        End If
        For Each Peer As String In RawPeers
            Arguments += $" -p=""{Peer}"""
        Next
        If Settings.Get(Of Integer)("LinkLatencyMode") = 1 Then Arguments += " --latency-first"
        '启动进程
        ProcessStart(Arguments)
        Task.Progress = 0.15
        '等待连接
        Dim TimeoutCounter As Integer = 0
        Do
            Thread.Sleep(500) '为避免还没启动好先跑 CLI 了，先等半秒
            '刷新信息
            CheckCrash()
            RefreshPeerLoader.WaitForExit(IsForceRestart:=True)
            '查找目标节点
            Dim Ping = GetPeerPing()
            If Ping > 0 Then
                Logger.Info($"已与目标建立连接，当前 Ping 为 {Ping:0.0}ms")
                Telemetry("联机成功", "Server", IsServerSide, "NAT", NATType)
                Exit Do '退出循环
            End If
            '更新进度
            Dim LastProgress = Task.Progress
            Dim PeerCount As Integer = If(Peers Is Nothing, -1, Peers.Where(Function(p) p.Ping > 0).Count)
            Select Case PeerCount
                Case -1 'CLI 无返回
                    Task.Progress = (Task.Progress + 0.02).Clamp(0.15, 0.25)
                Case 0 'CLI 有返回，但未连接到任何节点
                    UpdateLoadingPage("正在连接到节点……", "连接节点")
                    Task.Progress = (Task.Progress + 0.02).Clamp(If(IsServerSide, 0.5, 0.3), If(IsServerSide, 0.95, 0.5))
                Case Else '已连接到节点，但未连接到房主
                    UpdateLoadingPage("正在连接到房主……", "连接房主")
                    Task.Progress = (Task.Progress + 0.02).Clamp(Math.Min(0.45 + PeerCount * 0.05, 0.65), 0.95)
            End Select
            '超时判定
            If LastProgress <> Task.Progress Then
                TimeoutCounter = 0
            ElseIf TimeoutCounter < 30 * 2 Then
                TimeoutCounter += 1
            Else '进度停滞超过 30s
                Select Case PeerCount
                    Case -1 'CLI 无返回
                        Panic("无法启动联机模块", "近期日志：" & vbCrLf & LogHistory.Join(vbCrLf))
                    Case 0 'CLI 有返回，但未连接到任何节点
                        Panic("无法连接到节点", "请检查你的网络环境是否良好。" & vbCrLf & vbCrLf &
                              "如果官方中继节点已经停止服务，可以在 设置 → 联机 → 自定义节点 中填入可用的 EasyTier 共享节点（多个节点用 ，， 分隔）。")
                    Case Else '已连接到节点，但未连接到房主
                        Panic("无法连接到房主", $"可能的原因：{vbCrLf}- 你或者房主的网络环境不佳{vbCrLf}- 房主已关闭房间{vbCrLf}- 邀请码输错了")
                End Select
            End If
        Loop Until Task.IsCanceled
        If Task.IsCanceled Then Throw New OperationCanceledException
        '等待连接稳定，最多 5s
        If IsServerSide Then Return
        UpdateLoadingPage("连接优化中……", "优化连接")
        Task.Progress = 0.999
        For i = 1 To 50
            Dim Server = GetTargetPeer()
            If Server IsNot Nothing AndAlso Not Server.Relay AndAlso Server.Ping < 100 Then Return '结束
            If Task.IsCanceled Then Throw New OperationCanceledException
            Thread.Sleep(100)
        Next
    End Sub
    ''' <summary>
    ''' 从在线配置和 API 获取需要连接的节点列表，并更新发现节点 ID。
    ''' 根据发现节点 ID，会有以下行为变化：
    '''   -1：作为房主，根据负载均衡选择一个发现节点。
    '''   0：作为加入者，但邀请码未提供发现节点 ID，使用回退发现节点。
    '''   >0：作为加入者，根据 ID 选择对应的发现节点；如果没有，使用回退发现节点。
    ''' </summary>
    Private Function GetTargetPeers() As List(Of String)
        Dim FinalPeers As New List(Of String), FinalDiscoverID As Integer = -1
        Dim FallbackDiscoverID = ServerConfig("Link")("DiscoverPeerId").ToObject(Of Integer)
        Dim FallbackDiscoverAddress As String = ServerConfig("Link")("DiscoverPeer").ToString()
        Try
            '从 API 获取节点列表
            Dim RawNodes As JObject
            Dim IsFallbackRawList As Boolean = False
            Try
                RawNodes = NetRequestByClient("https://uptime.easytier.cn/api/nodes?page=1&per_page=1000", RequireJson:=True, Timeout:=4000).DeserializeJson()
            Catch exx As Exception
                Logger.Warn(exx, "从源站获取节点列表失败，将使用 CDN 缓存")
                RawNodes = NetRequestByClient("https://easytier.meloong.com/?page=1&per_page=1000", RequireJson:=True, Timeout:=4000).DeserializeJson()
                IsFallbackRawList = True
            End Try
            '分析节点列表
            Dim Nodes As New List(Of JObject) '负载会添加在 load 字段上
            Dim BlackList As List(Of String) = ServerConfig("Link")("PeersBlackList").Select(Function(p) p.ToString).ToList()
            For Each Node As JObject In RawNodes("data")("items")
                '状态检查
                Dim ID = Node("id").ToObject(Of Integer)
                If Not Node("is_active").ToObject(Of Boolean) OrElse Not Node("is_approved").ToObject(Of Boolean) Then Continue For
                If ID = DiscoverNodeID Then GoTo ForcedPass '若为指定的发现节点，忽略后续检查
                If ID = FallbackDiscoverID Then Continue For '不主动选取回退发现节点
                If BlackList.Contains(Node("address").ToString) Then Continue For 'ServerConfig 黑名单
                If Node("usage_percentage").ToObject(Of Double) = 0 AndAlso RandomInteger(1, 100) <> 1 Then Continue For '或许节点有问题才导致是 0 负载，让它只有 1% 概率被选中
                '标签检查
                Dim Tags = Node("tags").Select(Function(t) t.ToString).ToList
                If Not Tags.Contains("国内") OrElse Not Tags.Contains("MC中继") Then Continue For
                '计算负载并加入列表
ForcedPass:
                Dim Load As Double = Node("usage_percentage").ToObject(Of Double) '负载
                Load *= 110 - Node("health_percentage_24h").ToObject(Of Double) '可用率
                Node("load") = Load
                Nodes.Add(Node)
            Next
            '排序
            If Not IsFallbackRawList Then
                Nodes = Nodes.OrderBy(Function(n) n("load").ToObject(Of Double)).ToList() '按负载从低到高排序
            Else
                Nodes = Nodes.Shuffle().ToList() '回退到 CDN 缓存时，由于负载数据可能过期，直接随机选择
            End If
            '选取发现节点
            Dim SelectedDiscoverNode As JObject = Nothing
            If DiscoverNodeID = -1 Then '-1：作为房主，根据负载均衡选择一个发现节点
                SelectedDiscoverNode = Nodes.FirstOrDefault(Function(n) Not n("allow_relay").ToObject(Of Boolean))
            ElseIf DiscoverNodeID > 0 Then '>0：作为加入者，根据 ID 选择对应的发现节点；如果没有，使用回退发现节点
                SelectedDiscoverNode = Nodes.FirstOrDefault(Function(n) n("id").ToObject(Of Integer) = DiscoverNodeID)
                If SelectedDiscoverNode Is Nothing Then
                    Logger.Warn($"未找到 ID {DiscoverNodeID} 的发现节点")
                    Panic("房间已过期", "请让房主重新创建房间！")
                    Throw New OperationCanceledException
                End If
            End If
            If SelectedDiscoverNode Is Nothing Then '使用回退发现节点
                SelectedDiscoverNode = New JObject From {{"address", FallbackDiscoverAddress}, {"id", FallbackDiscoverID}}
                Logger.Warn("将使用回退发现节点")
            End If
            FinalPeers.Add(SelectedDiscoverNode("address").ToString())
            FinalDiscoverID = SelectedDiscoverNode("id").ToObject(Of Integer)
            Logger.Info($"发现节点：{SelectedDiscoverNode("address")} (ID: {FinalDiscoverID})")
            '选取中继节点
            If ModeDebug OrElse Not IsServerSide Then '房主只连接发现节点，不连接中继节点
                Dim RelayCount As Integer = ServerConfig("Link")("RandomPeer").ToObject(Of Integer)
                Dim RelayNodes = Nodes.Where(Function(n) n("allow_relay").ToObject(Of Boolean)).ToList()
                If RelayNodes.Count < RelayCount Then Throw New Exception($"可用的中继节点数量不足，需要 {RelayCount} 个，实际 {RelayNodes.Count} 个")
                FinalPeers.AddRange(RelayNodes.Take(RelayCount).Select(Function(n) n("address").ToString()))
            End If
        Catch ex As Exception
            ex.ThrowIfCanceled()
            Logger.Error(ex, "获取节点列表失败，联机质量可能受到影响", LogBehavior.Toast)
            FinalPeers.AddRange(ServerConfig("Link")("Peers").Select(Function(p) p.ToString))
            If FinalDiscoverID <= 0 Then
                FinalPeers.Add(FallbackDiscoverAddress)
                FinalDiscoverID = FallbackDiscoverID
            End If
        End Try
        '版本 1 兼容
        If CType(ServerConfig("Link"), JObject).ContainsKey("EnableForcedPeer") Then
            FinalPeers.Add("tcp://mc1.easytier.cn:55558")
            If FinalDiscoverID <= 0 Then FinalDiscoverID = 94
        End If
        '强制添加的节点
        FinalPeers.AddRange(ServerConfig("Link")("MandatoryPeers").Select(Function(p) p.ToString))
        '结束
        DiscoverNodeID = FinalDiscoverID
        Return FinalPeers.Distinct.ToList
    End Function

#End Region

#Region "进程与日志"

    ''' <summary>
    ''' EasyTier Core 进程。
    ''' </summary>
    Private Shared ProcessCore As Process
    Private Shared ProcessOutputHandle As AutoResetEvent, ProcessErrorHandle As AutoResetEvent

    ''' <summary>
    ''' 若程序正在运行，则结束程序进程。
    ''' 即使失败也不会抛出异常。
    ''' </summary>
    Public Shared Sub ProcessStop()
        Try
            '关闭未捕获的进程
            For Each ProcessObject In Process.GetProcesses
                If ProcessObject.Id = ProcessCore?.Id Then Continue For
                If ProcessObject.ProcessName <> "联机模块" AndAlso ProcessObject.ProcessName <> "联机模块 CLI" Then Continue For
                Try
                    Logger.Info($"停止残留的联机模块，PID：{ProcessObject.Id}，进程名：{ProcessObject.ProcessName}")
                    ProcessObject.Kill()
                    ProcessObject.Close()
                Catch exx As Exception
                    Logger.Warn(exx, $"结束进程失败（{ProcessObject.ProcessName}，PID {ProcessObject.Id}）")
                End Try
            Next
            '关闭由自身启动的进程
            If ProcessCore IsNot Nothing AndAlso Not ProcessCore.HasExited Then
                Logger.Info($"停止所启动的联机模块，PID：{ProcessCore.Id}")
                ProcessCore.Kill()
                ProcessCore.Close()
                ProcessCore = Nothing
            End If
            '释放资源
            ProcessOutputHandle?.Dispose()
            ProcessErrorHandle?.Dispose()
        Catch ex As Exception
            Logger.Warn(ex, "停止联机模块失败")
        End Try
    End Sub
    ''' <summary>
    ''' 出现意外错误，给出错误信息并结束联机。
    ''' </summary>
    Private Sub Panic(Brief As String, Detail As String)
        '常见原因分析
        If Detail.Contains("failed to listen on ") Then
            Detail = $"监听端口失败。{vbCrLf}请点击重试，如果还是出现此错误，可以重启电脑解决。{vbCrLf}{vbCrLf}{Detail}"
        End If
        '显示信息
        If LinkState = LinkStates.Loading Then
            FailReason = Brief
            LinkLoader.Failed(New Exception(Detail))
        Else
            MyMsgBox(Detail, "联机出错：" & Brief, IsWarn:=True)
            ChangeState(LinkStates.Waiting)
            Telemetry("联机失败", "Exception", FilterUserName(Brief & "：" & Detail, "*"))
        End If
    End Sub

    ''' <summary>
    ''' 启动进程。
    ''' </summary>
    Private Sub ProcessStart(Arguments As String)
        Dim Info = New ProcessStartInfo With {
            .FileName = PathEasyTier & "联机模块.exe",
            .Arguments = Arguments,
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardError = True,
            .RedirectStandardOutput = True,
            .StandardOutputEncoding = Encoding.UTF8,
            .StandardErrorEncoding = Encoding.UTF8
        }
        Logger.Info($"正在启动 EasyTier：{Arguments}")
        ProcessCore = New Process With {.StartInfo = Info}
        Dim LogLineHandler =
        Sub(sender As Object, e As DataReceivedEventArgs, Handle As AutoResetEvent)
            Try
                If e.Data Is Nothing Then
                    Handle.[Set]()
                    Return
                End If
                ProcessLogLine(e.Data)
            Catch unused As ObjectDisposedException
            Catch ex As Exception
                Logger.Warn(ex, "读取 EasyTier 信息失败")
            End Try
        End Sub
        ProcessOutputHandle = New AutoResetEvent(False)
        AddHandler ProcessCore.OutputDataReceived, Sub(sender, e) LogLineHandler(sender, e, ProcessOutputHandle)
        ProcessErrorHandle = New AutoResetEvent(False)
        AddHandler ProcessCore.ErrorDataReceived, Sub(sender, e) LogLineHandler(sender, e, ProcessErrorHandle)
        ProcessCore.Start()
        ProcessCore.BeginOutputReadLine()
        ProcessCore.BeginErrorReadLine()
    End Sub
    Private Sub ProcessLogLine(Line As String)
        '记录日志
        Logger.Info(Line)
        LogHistory.Enqueue(Line)
        If LogHistory.Count >= 10 Then LogHistory.Dequeue()
        '检查日志内容
        If Line.ContainsIgnoreCase("new peer connection added") Then
            Logger.Warn($"已建立连接：{If(Line.RegexSeek("(?<=remote_addr.+?"")[^""}]{3,}"), Line)}")
            Update()
        ElseIf Line.ContainsIgnoreCase("peer connection removed") Then
            Logger.Warn($"已断开连接：{If(Line.RegexSeek("(?<=remote_addr.+?"")[^""}]{3,}"), Line)}")
            Update()
        End If
    End Sub
    Private LogHistory As New Queue(Of String)(10)

#End Region

#Region "CLI 节点信息获取"

    Private Class Peer
        ''' <summary>
        ''' 该节点的类型。
        ''' </summary>
        Public ReadOnly Type As Types
        Public Enum Types
            ''' <summary>
            ''' 客户端。
            ''' </summary>
            Client
            ''' <summary>
            ''' 服务器。
            ''' </summary>
            Server
            ''' <summary>
            ''' 公共节点。
            ''' </summary>
            Misc
            ''' <summary>
            ''' 自己，或者自己的重复（它可能返回多个自己）。
            ''' </summary>
            Self
        End Enum
        ''' <summary>
        ''' 与该节点的延迟 (ms)。
        ''' 若是自己则为 0。
        ''' </summary>
        Public ReadOnly Ping As Double
        ''' <summary>
        ''' 名称。
        ''' </summary>
        Public ReadOnly Name As String
        ''' <summary>
        ''' 是否通过中继连接。
        ''' </summary>
        Public ReadOnly Relay As Boolean
        ''' <summary>
        ''' NAT 类型。
        ''' </summary>
        Public ReadOnly NATType As NATTypes

        ''' <summary>
        ''' 从 CLI 给出的信息分析对应的数据。
        ''' </summary>
        Public Sub New(Info As JObject)
            '类别
            Dim PeerName = Info("hostname").ToString
            If Info("cost").ToString = "Local" Then
                Type = Types.Self
            ElseIf PeerName.StartsWithF("Client") Then
                Type = Types.Client
            ElseIf Info("ipv4") = "10.114.114.114" Then '只允许这个 IP 作为房主，避免网络被多个房主搞炸的情况
                Type = Types.Server
            Else
                Type = Types.Misc
            End If
            '基础信息
            Double.TryParse(Info("lat_ms"), NumberStyles.Any, CultureInfo.InvariantCulture, Ping)
            Name = PeerName
            Relay = Info("cost").ToString.ContainsIgnoreCase("relay")
            NATType = EnumUtils.FromString(Of NATTypes)(Info("nat_type"))
        End Sub
        Public Overrides Function ToString() As String
            Return $"{Type} - {Name} - Ping {Ping:0.0}ms [中继? {Relay}] - NAT {NATType}"
        End Function
    End Class
    Private Enum NATTypes
        'https://github.com/EasyTier/EasyTier/blob/6bb2fd9a15ab2499bdeabdcc3a925e9bd9aebf50/easytier/src/proto/common.proto#L129
        Unknown = 0
        OpenInternet = 1
        NoPAT = 2
        FullCone = 3
        Restricted = 4
        PortRestricted = 5
        Symmetric = 6
        SymUdpFirewall = 7
        SymmetricEasyInc = 8
        SymmetricEasyDec = 9
        ''' <summary>
        ''' 尚未获取。
        ''' </summary>
        Pending = 10
    End Enum

    ''' <summary>
    ''' 当前的节点列表，使用 RefreshPeerLoader 来刷新。
    ''' 若尚未成功获取过则为 Nothing，但保证在加载完成后至少是一个列表。
    ''' </summary>
    Private Peers As List(Of Peer) = Nothing
    ''' <summary>
    ''' 自己的 NAT 类型。
    ''' </summary>
    Private NATType As NATTypes = NATTypes.Pending

    ''' <summary>
    ''' 调用 EasyTier CLI 获取已连接节点信息。
    ''' 这会过滤掉尚未完成连接的其他节点以及自己。
    ''' 若在加载完成后连续获取失败 3 次，则会强制断开连接。
    ''' </summary>
    Private RefreshPeerLoader As New LoaderTask(Of Integer, List(Of Peer))("EasyTier CLI", AddressOf RefreshPeer)
    Private Function GetEasyTierCliOutput(Arguments As String) As String
        Try
            Dim Info As New ProcessStartInfo(PathEasyTier & "联机模块 CLI.exe") With {
                .Arguments = Arguments,
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .StandardOutputEncoding = Encoding.UTF8,
                .StandardErrorEncoding = Encoding.UTF8,
                .WorkingDirectory = PathEasyTier}
            Dim CliProcess As Process = Process.Start(Info)
            Dim Output As String = CliProcess.StandardOutput.ReadToEnd()
            CliProcess.WaitForExit(3000)
            If String.IsNullOrWhiteSpace(Output) Then Output = CliProcess.StandardError.ReadToEnd()
            Return Output
        Catch ex As Exception
            Logger.Warn(ex, "调用联机模块 CLI 失败")
            Return ""
        End Try
    End Function
    Private Sub RefreshPeer()
        Try
            Dim CliResult As String = GetEasyTierCliOutput($"-o json -p 127.0.0.1:{RPCPort} peer")
            '解析
            If Not CliResult.Contains("lat_ms") Then Throw New Exception("CLI 调用失败：" & vbCrLf & CliResult)
            If GetUuid() Mod If(ModeDebug, 23, 103) = 0 Then Logger.Info($"CLI 输出抽样：{vbCrLf}{CliResult}")
            Dim NewPeers As New List(Of Peer)
            For Each Line As JObject In CType(CliResult.DeserializeJson(), JArray)
                Try
                    Dim Peer = New Peer(Line)
                    If Peer.Type = Peer.Types.Self Then
                        NATType = Peer.NATType '记录自己的 NAT
                    Else
                        NewPeers.Add(Peer)
                    End If
                Catch exx As Exception
                    Logger.Warn(exx, $"错误的信息（{Line}）")
                End Try
            Next
            '完成
            Peers = NewPeers
            FailCount = 0
        Catch ex As Exception
            Logger.Warn(ex, "获取节点信息失败")
            If LinkState = LinkStates.Finished Then
                FailCount += 1
                If FailCount >= 4 Then Panic("获取节点信息失败", ex.Message)
            End If
        End Try
    End Sub
    ''' <summary>
    ''' CLI 调用连续失败的次数。
    ''' </summary>
    Private FailCount As Integer = 0

    ''' <summary>
    ''' 根据当前的节点列表获取 Ping 值。
    ''' 服务端会返回所有节点中最低的 Ping，客户端会返回服务端的 Ping。
    ''' 0 代表尚不可用。
    ''' </summary>
    Private Function GetPeerPing() As Double
        Dim Peer = GetTargetPeer()
        If Peer Is Nothing Then Return 0
        Return Peer.Ping
    End Function
    ''' <summary>
    ''' 服务端会返回所有节点中 Ping 大于 0 最低的那一个，客户端会返回服务端。
    ''' 若没有则为 Nothing。
    ''' </summary>
    Private Function GetTargetPeer() As Peer
        If Peers Is Nothing Then Return Nothing
        Dim Targets As IEnumerable(Of Peer)
        If IsServerSide Then
            Targets = Peers.Where(Function(p) p.Ping > 0)
        Else
            Targets = Peers.Where(Function(p) p.Type = Peer.Types.Server AndAlso p.Ping > 0)
        End If
        If Not Targets.Any Then Return Nothing
        '返回 Ping 且最低的那个
        Dim MinPing = Targets.Min(Function(p) p.Ping)
        Return Targets.First(Function(p) p.Ping = MinPing)
    End Function

    ''' <summary>
    ''' 获取当前房间中的人数，包括自己。
    ''' 0 代表不可用。
    ''' </summary>
    Private ReadOnly Property PeopleCount As Integer
        Get
            If Peers Is Nothing Then Return 0
            Return Peers.Where(Function(p) p.Type <> Peer.Types.Misc).Count() + 1 '加上自己
        End Get
    End Property

#End Region

#Region "定时任务"

    '启动
    Private Sub StartTimerThread() Handles Me.Loaded
        Static IsTimerStarted As Boolean = False
        If IsTimerStarted Then Return
        RunInNewThread(
        Sub()
            Dim Counter As Integer = 0
            Do While True
                Try
                    Thread.Sleep(200)
                    Counter += 200
                    '每秒执行一次
                    If Counter >= 1000 Then
                        Counter = 0
                        Update()
                    End If
                    '每 200ms 更新进度条
                    If LinkState = LinkStates.Loading Then RunInUi(AddressOf UpdateProgressBar)
                Catch ex As Exception
                    Logger.Error(ex, "联机模块主时钟出错")
                    Thread.Sleep(10000)
                End Try
            Loop
        End Sub, "Link Timer")
    End Sub

    '每秒或进入页面时触发
    Private Sub Update() Handles Me.Loaded
        If LinkState <> LinkStates.Finished Then Return
        '重新获取信息
        SyncLock RefreshPeerLoader.LockState
            If RefreshPeerLoader.State <> LoadState.Loading Then RefreshPeerLoader.Start(IsForceRestart:=True)
        End SyncLock
        '更新 UI
        If FrmMain.PageCurrent = FormMain.PageType.Link Then
            RunInUi(
            Sub()
                'Logo 旋转动画
                AniStart(AaRotateTransform(ImgFinishLogo, 500, 5000), "Link Logo Rotation")
                'Ping
                Dim Ping As Double = GetPeerPing()
                Dim Connecting As Boolean = Ping Mod 500 = 0 OrElse $"{Ping:0.0}" = "1.0" OrElse FailCount > 0
                If Connecting Then
                    LabFinishPing.Text = "连接优化中"
                Else
                    LabFinishPing.Text = If(Ping >= 10, $"{Ping:0} ms", $"{Ping:0.0} ms")
                End If
                '人数显示
                LabFinishPlayer.Text = PeopleCount & "  人"
                '------------------------
                '  提示条
                '------------------------
                HintFinish.Visibility = Visibility.Collapsed
                If IsServerSide Then Return '服务端没有需要显示的提示
                If Connecting Then Return '连接稳定过程中不显示提示
                '中继提示
                Dim Server As Peer = GetTargetPeer()
                If Server Is Nothing Then Return
                If Server.Relay Then
                    If NATType >= NATTypes.Symmetric AndAlso Server.NATType >= NATTypes.Symmetric Then
                        HintFinish.Text = "你和房主的网络环境都不太好，"
                    ElseIf NATType >= NATTypes.Symmetric Then
                        HintFinish.Text = "你的网络环境不太好，"
                    ElseIf Server.NATType >= NATTypes.Symmetric Then
                        HintFinish.Text = "房主的网络环境不太好，"
                    Else
                        HintFinish.Text = "你或者房主的网络环境不太好，"
                    End If
                    If String.IsNullOrWhiteSpace(Settings.Get(Of String)("LinkCustomPeer")) Then
                        HintFinish.Text &= "正使用社区节点进行中继。"
                    Else
                        HintFinish.Text &= "正通过自定义节点进行中继。"
                    End If
                    HintFinish.Visibility = Visibility.Visible
                    HintFinish.Theme = MyHint.Themes.Yellow
                    Return
                End If
                '环境比房主更好的提示
                If NATType >= NATTypes.PortRestricted Then Return '自身的 NAT 为 2 或更好
                If Server.NATType < NATTypes.PortRestricted Then Return '房主的 NAT 为 3 或更差
                Dim OtherPlayers = Peers.Where(Function(p) p.Type = Peer.Types.Client).ToList()
                If Not OtherPlayers.Any() Then Return '房间里还有其他玩家
                If OtherPlayers.Any(Function(p) p.Relay) Then Return '自己可以与所有玩家打洞
                If OtherPlayers.All(Function(p) p.NATType < NATTypes.PortRestricted) Then Return '任意其他玩家的 NAT 为 3 或更差
                HintFinish.Visibility = Visibility.Visible
                HintFinish.Theme = MyHint.Themes.Blue
                HintFinish.Text = "你的网络环境比房主更好！如果你来当房主，其他玩家或许能更加流畅！"
            End Sub)
        End If
        '检查核心状态
        CheckCrash()
        '检查节点状态
        If Not Peers.Any Then
            Panic("网络连接已断开", "请检查你的网络环境是否正常。")
            Return
        End If
        If Not IsServerSide AndAlso Not Peers.Any(Function(p) p.Type = Peer.Types.Server) Then
            MyMsgBox("房主已离开房间！", "联机结束")
            ChangeState(LinkStates.Waiting)
            Return
        End If
        '广播联机房间端口
        If Not IsServerSide Then
            Try
                Static BroadcastSocket As New Socket(SocketType.Dgram, ProtocolType.Udp)
                BroadcastSocket.SendTo(
                    Encoding.UTF8.GetBytes($"[MOTD]PCL 联机房间[/MOTD][AD]{ClientPort}[/AD]"),
                    SocketFlags.None,
                    New IPEndPoint(IPAddress.Loopback, 4445))
            Catch ex As Exception
                Logger.Warn(ex, "广播联机房间端口失败")
            End Try
        End If
    End Sub
    ''' <summary>
    ''' 检查联机模块是否崩溃。
    ''' </summary>
    Private Sub CheckCrash()
        If ProcessCore IsNot Nothing AndAlso Not ProcessCore.HasExited Then Return
        Panic("联机模块已崩溃", "近期日志：" & vbCrLf & LogHistory.Join(vbCrLf))
    End Sub

#End Region




End Class
