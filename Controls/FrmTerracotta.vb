Imports System.Windows.Threading
Imports Newtonsoft.Json.Linq

Public Class FrmTerracotta
    Inherits Window

    Private ReadOnly LabStatus As New TextBlock
    Private ReadOnly LabRoom As New TextBlock
    Private ReadOnly LabMembers As New TextBlock
    Private ReadOnly BtnHost As New MyButton
    Private ReadOnly BtnJoin As New MyButton
    Private ReadOnly BtnCopy As New MyButton
    Private ReadOnly BtnBack As New MyButton
    Private ReadOnly BtnClose As New MyButton
    Private Port As Integer = 0
    Private Ready As Boolean = False
    Private Busy As Boolean = False
    Private CurrentRoom As String = ""
    Private ReadOnly Timer As New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(700)}

    Public Sub New()
        Title = "陶瓦联机"
        Width = 460
        SizeToContent = SizeToContent.Height
        ResizeMode = ResizeMode.NoResize
        WindowStartupLocation = WindowStartupLocation.CenterScreen
        Background = New SolidColorBrush(Color.FromRgb(248, 249, 251))

        Dim Root As New StackPanel With {.Margin = New Thickness(18)}

        Root.Children.Add(New TextBlock With {
            .Text = "陶瓦联机（启动器内）",
            .FontSize = 16,
            .FontWeight = FontWeights.Bold,
            .Margin = New Thickness(0, 0, 0, 4)})
        Root.Children.Add(New TextBlock With {
            .Text = "无需打开陶瓦窗口。房主点击「创建房间」后把房间码发给朋友；朋友点「加入房间」输入房间码，之后在游戏多人列表里进入「陶瓦联机大厅」。",
            .FontSize = 12,
            .TextWrapping = TextWrapping.Wrap,
            .Foreground = New SolidColorBrush(Color.FromRgb(110, 116, 124)),
            .Margin = New Thickness(0, 0, 0, 12)})

        LabStatus.Text = "正在启动陶瓦……"
        LabStatus.FontSize = 12.5
        LabStatus.Foreground = New SolidColorBrush(Color.FromRgb(80, 86, 94))
        LabStatus.TextWrapping = TextWrapping.Wrap
        LabStatus.Margin = New Thickness(0, 0, 0, 8)
        Root.Children.Add(LabStatus)

        Dim ButtonRow As New WrapPanel With {.Margin = New Thickness(0, 0, 0, 8)}
        ConfigureButton(BtnHost, "创建房间", MyButton.ColorState.Highlight)
        ConfigureButton(BtnJoin, "加入房间", MyButton.ColorState.Normal)
        ConfigureButton(BtnBack, "返回待机", MyButton.ColorState.Normal)
        ButtonRow.Children.Add(BtnHost)
        ButtonRow.Children.Add(BtnJoin)
        ButtonRow.Children.Add(BtnBack)
        Root.Children.Add(ButtonRow)

        LabRoom.Text = ""
        LabRoom.FontSize = 14
        LabRoom.FontFamily = New FontFamily("Consolas")
        LabRoom.Margin = New Thickness(0, 0, 0, 6)
        Root.Children.Add(LabRoom)

        BtnCopy.IsEnabled = False
        ConfigureButton(BtnCopy, "复制房间码", MyButton.ColorState.Normal)
        Root.Children.Add(BtnCopy)

        LabMembers.Text = ""
        LabMembers.FontSize = 12
        LabMembers.TextWrapping = TextWrapping.Wrap
        LabMembers.Margin = New Thickness(0, 8, 0, 6)
        LabMembers.Foreground = New SolidColorBrush(Color.FromRgb(110, 116, 124))
        Root.Children.Add(LabMembers)

        BtnClose.HorizontalAlignment = HorizontalAlignment.Right
        ConfigureButton(BtnClose, "关闭", MyButton.ColorState.Normal)
        Root.Children.Add(BtnClose)

        Content = Root

        AddHandler BtnHost.Click, Sub(s, e) HostRoom()
        AddHandler BtnJoin.Click, Sub(s, e) JoinRoom()
        AddHandler BtnBack.Click, Sub(s, e) ResetState()
        AddHandler BtnCopy.Click, Sub(s, e) CopyRoom()
        AddHandler BtnClose.Click, Sub(s, e) CloseDialog()
        AddHandler Timer.Tick, AddressOf OnTick
        AddHandler Me.Loaded, Sub() StartAsync()
        AddHandler Me.Closed, Sub() Timer.Stop()
    End Sub

    Private Sub ConfigureButton(Button As MyButton, Text As String, ColorType As MyButton.ColorState)
        Button.Text = Text
        Button.ColorType = ColorType
        Button.Height = 30
        Button.Padding = New Thickness(14, 0, 14, 0)
        Button.Margin = New Thickness(0, 0, 8, 8)
        Button.MinWidth = 90
    End Sub

    Private Function PlayerName() As String
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

    Private Sub StartAsync()
        RunInNewThread(
        Sub()
            Try
                RunInUi(Sub() LabStatus.Text = "正在准备陶瓦……")
                Port = ModTerracotta.StartServer()
                RunInUi(
                Sub()
                    Ready = True
                    LabStatus.Text = "就绪。房主点「创建房间」，加入方点「加入房间」。"
                    Timer.Start()
                End Sub)
            Catch ex As Exception
                RunInUi(Sub() LabStatus.Text = "启动失败：" & ex.Message)
            End Try
        End Sub, "Terracotta UI")
    End Sub

    Private Sub OnTick(sender As Object, e As EventArgs)
        If Not Ready OrElse Busy Then Return
        Busy = True
        Dim CurrentPort As Integer = Port
        RunInNewThread(
        Sub()
            Dim Json As JObject = Nothing
            Dim ErrorText As String = Nothing
            Try
                Json = JObject.Parse(ModTerracotta.HttpGet("http://127.0.0.1:" & CurrentPort & "/state"))
            Catch ex As Exception
                ErrorText = ex.Message
            End Try
            RunInUi(
            Sub()
                Busy = False
                If ErrorText IsNot Nothing Then
                    LabStatus.Text = "陶瓦连接中断：" & ErrorText
                    Return
                End If
                ApplyState(Json)
            End Sub)
        End Sub, "Terracotta Poll")
    End Sub

    Private Sub ApplyState(Json As JObject)
        If Json Is Nothing Then Return
        Dim State As String = If(Json("state") Is Nothing, "", Json("state").ToString())
        Dim Room As String = If(Json("room") Is Nothing, "", Json("room").ToString())
        If Room <> "" Then CurrentRoom = Room
        LabRoom.Text = If(CurrentRoom = "", "", "房间码：" & CurrentRoom)
        BtnCopy.IsEnabled = CurrentRoom <> ""
        LabMembers.Text = ""
        If Json("profiles") IsNot Nothing Then
            Dim Names As New List(Of String)
            For Each Profile As JToken In CType(Json("profiles"), JArray)
                Dim Name As String = If(Profile("name") Is Nothing, "", Profile("name").ToString())
                Dim Kind As String = If(Profile("kind") Is Nothing, "", Profile("kind").ToString())
                If Name <> "" Then Names.Add(Name & If(Kind = "HOST", "（房主）", ""))
            Next
            If Names.Count > 0 Then LabMembers.Text = "成员：" & String.Join("、", Names)
        End If
        Select Case State
            Case "waiting"
                LabStatus.Text = "待机。房主点「创建房间」，加入方点「加入房间」。"
                BtnHost.IsEnabled = True : BtnJoin.IsEnabled = True
            Case "host-scanning"
                LabStatus.Text = "正在扫描本机 Minecraft 端口……请先在游戏里进入存档并「对局域网开放」。"
                BtnHost.IsEnabled = False : BtnJoin.IsEnabled = False
            Case "host-starting"
                LabStatus.Text = "正在创建房间……"
                BtnHost.IsEnabled = False : BtnJoin.IsEnabled = False
            Case "host-ok"
                LabStatus.Text = "房间已创建。把房间码发给朋友，朋友加入后在多人游戏列表进入「陶瓦联机大厅」。"
                BtnHost.IsEnabled = False : BtnJoin.IsEnabled = False
            Case "guest-connecting", "guest-starting"
                LabStatus.Text = "正在加入房间……"
                BtnHost.IsEnabled = False : BtnJoin.IsEnabled = False
            Case "guest-ok"
                Dim Url As String = If(Json("url") Is Nothing, "", Json("url").ToString())
                LabStatus.Text = "已加入房间。" & If(Url = "", "", "备用直连地址：" & Url)
                BtnHost.IsEnabled = False : BtnJoin.IsEnabled = False
            Case "exception"
                Dim Type As Integer = If(Json("type") Is Nothing, -1, Json("type").ToObject(Of Integer)())
                Select Case Type
                    Case 0 : LabStatus.Text = "无法连接房主（房间已关闭或网络不稳定）。"
                    Case 1 : LabStatus.Text = "与房主的连接中断。"
                    Case 2 : LabStatus.Text = "加入方联机组件崩溃。"
                    Case 3 : LabStatus.Text = "房主联机组件崩溃。"
                    Case 4 : LabStatus.Text = "房主已退出存档，房间自动关闭。"
                    Case Else : LabStatus.Text = "发生异常（" & Type & "）。"
                End Select
                BtnHost.IsEnabled = True : BtnJoin.IsEnabled = True
            Case Else
                LabStatus.Text = "状态：" & State
        End Select
    End Sub

    Private Sub HostRoom()
        If Not Ready OrElse Busy Then Return
        Busy = True
        LabStatus.Text = "正在创建房间……"
        Dim CurrentPort As Integer = Port
        Dim Name As String = PlayerName()
        RunInNewThread(
        Sub()
            Dim ErrorText As String = Nothing
            Try
                ModTerracotta.HttpGet("http://127.0.0.1:" & CurrentPort & "/state/scanning?player=" & Uri.EscapeDataString(Name))
            Catch ex As Exception
                ErrorText = ex.Message
            End Try
            RunInUi(
            Sub()
                Busy = False
                If ErrorText IsNot Nothing Then LabStatus.Text = "创建房间失败：" & ErrorText
            End Sub)
        End Sub, "Terracotta Host")
    End Sub

    Private Sub JoinRoom()
        If Not Ready OrElse Busy Then Return
        Dim Code As String = MyMsgBoxInput("加入陶瓦房间", "输入房主发给你的房间码。", "")
        If String.IsNullOrWhiteSpace(Code) Then Return
        Busy = True
        LabStatus.Text = "正在加入房间……"
        Dim CurrentPort As Integer = Port
        Dim Name As String = PlayerName()
        RunInNewThread(
        Sub()
            Dim ErrorText As String = Nothing
            Try
                ModTerracotta.HttpGet("http://127.0.0.1:" & CurrentPort & "/state/guesting?room=" & Uri.EscapeDataString(Code.Trim()) & "&player=" & Uri.EscapeDataString(Name))
            Catch ex As Exception
                ErrorText = ex.Message
            End Try
            RunInUi(
            Sub()
                Busy = False
                If ErrorText IsNot Nothing Then LabStatus.Text = "加入房间失败：" & ErrorText
            End Sub)
        End Sub, "Terracotta Join")
    End Sub

    Private Sub ResetState()
        If Not Ready Then Return
        Dim CurrentPort As Integer = Port
        LabStatus.Text = "正在返回待机……"
        RunInNewThread(
        Sub()
            ModTerracotta.ResetState(CurrentPort)
            RunInUi(Sub() LabStatus.Text = "待机。房主点「创建房间」，加入方点「加入房间」。")
        End Sub, "Terracotta Reset")
    End Sub

    Private Sub CopyRoom()
        If CurrentRoom <> "" Then
            ClipboardSet(CurrentRoom)
            Hint("房间码已复制", HintType.Green)
        End If
    End Sub

    Private Sub CloseDialog()
        Timer.Stop()
        Dim CurrentPort As Integer = Port
        If CurrentPort <> 0 Then
            RunInNewThread(Sub() ModTerracotta.Shutdown(CurrentPort), "Terracotta Shutdown")
        End If
        Close()
    End Sub

End Class
