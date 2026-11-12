Imports System.Diagnostics

Public Class FrmSakuraFrp
    Inherits Window

    Private ReadOnly TextArgument As New TextBox
    Private ReadOnly TextAddress As New TextBox
    Private ReadOnly TextLog As New TextBox
    Private ReadOnly LabStatus As New TextBlock
    Private ReadOnly BtnStart As New Button
    Private ReadOnly BtnStop As New Button
    Private ReadOnly PathFolder As String = PathPure.Value & "SakuraFrp\"
    Private ReadOnly PathExe As String = PathFolder & "frpc.exe"
    Private SakuraProcess As Process = Nothing
    Private Shared ReadOnly AddressRegex As New RegularExpressions.Regex("([a-zA-Z0-9.\-]+\.natfrp\.cloud):(\d{1,5})", RegularExpressions.RegexOptions.IgnoreCase)

    Public Sub New()
        Title = "SakuraFrp 隧道"
        Width = 640
        Height = 560
        MinWidth = 480
        MinHeight = 420
        WindowStartupLocation = WindowStartupLocation.CenterScreen
        Background = New SolidColorBrush(Color.FromRgb(248, 249, 251))

        Dim Root As New StackPanel With {.Margin = New Thickness(18)}
        Content = Root

        Root.Children.Add(New TextBlock With {
            .Text = "在 SakuraFrp 面板创建 TCP 隧道：本地 IP 填 127.0.0.1，本地端口填 Minecraft「对局域网开放」显示的端口；然后在下方填入面板复制的启动参数（格式：访问密钥:隧道ID），点「开始隧道」即可。",
            .TextWrapping = TextWrapping.Wrap, .Foreground = New SolidColorBrush(Color.FromRgb(90, 96, 104)),
            .FontSize = 12, .Margin = New Thickness(0, 0, 0, 12)})

        Dim ArgRow As New Grid With {.Margin = New Thickness(0, 0, 0, 8)}
        ArgRow.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(80)})
        ArgRow.ColumnDefinitions.Add(New ColumnDefinition)
        ArgRow.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        ArgRow.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        ArgRow.Children.Add(New TextBlock With {.Text = "启动参数", .VerticalAlignment = VerticalAlignment.Center, .FontSize = 12})
        TextArgument.FontFamily = New FontFamily("Consolas")
        TextArgument.FontSize = 12
        TextArgument.Margin = New Thickness(0, 0, 8, 0)
        Grid.SetColumn(TextArgument, 1)
        ArgRow.Children.Add(TextArgument)
        Dim BtnFolder As New Button With {.Content = "打开文件夹", .Padding = New Thickness(10, 3, 10, 3), .FontSize = 12, .Margin = New Thickness(0, 0, 8, 0)}
        Grid.SetColumn(BtnFolder, 2)
        ArgRow.Children.Add(BtnFolder)
        Dim BtnPanel As New Button With {.Content = "打开樱花面板", .Padding = New Thickness(10, 3, 10, 3), .FontSize = 12}
        Grid.SetColumn(BtnPanel, 3)
        ArgRow.Children.Add(BtnPanel)
        Root.Children.Add(ArgRow)

        Dim AddrRow As New Grid With {.Margin = New Thickness(0, 0, 0, 8)}
        AddrRow.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(80)})
        AddrRow.ColumnDefinitions.Add(New ColumnDefinition)
        AddrRow.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        AddrRow.Children.Add(New TextBlock With {.Text = "连接地址", .VerticalAlignment = VerticalAlignment.Center, .FontSize = 12})
        TextAddress.IsReadOnly = True
        TextAddress.FontFamily = New FontFamily("Consolas")
        TextAddress.FontSize = 12
        TextAddress.Margin = New Thickness(0, 0, 8, 0)
        Grid.SetColumn(TextAddress, 1)
        AddrRow.Children.Add(TextAddress)
        Dim BtnCopy As New Button With {.Content = "复制地址", .Padding = New Thickness(10, 3, 10, 3), .FontSize = 12}
        Grid.SetColumn(BtnCopy, 2)
        AddrRow.Children.Add(BtnCopy)
        Root.Children.Add(AddrRow)

        Dim BtnRow As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 0, 0, 8)}
        BtnStart.Content = "开始隧道"
        BtnStart.Padding = New Thickness(18, 5, 18, 5)
        BtnStart.FontSize = 13
        BtnStart.Margin = New Thickness(0, 0, 10, 0)
        BtnRow.Children.Add(BtnStart)
        BtnStop.Content = "停止隧道"
        BtnStop.Padding = New Thickness(18, 5, 18, 5)
        BtnStop.FontSize = 13
        BtnStop.IsEnabled = False
        BtnRow.Children.Add(BtnStop)
        Root.Children.Add(BtnRow)

        LabStatus.Text = "尚未启动"
        LabStatus.FontSize = 12
        LabStatus.TextWrapping = TextWrapping.Wrap
        LabStatus.Foreground = New SolidColorBrush(Color.FromRgb(90, 96, 104))
        LabStatus.Margin = New Thickness(0, 0, 0, 8)
        Root.Children.Add(LabStatus)

        TextLog.IsReadOnly = True
        TextLog.AcceptsReturn = True
        TextLog.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        TextLog.FontFamily = New FontFamily("Consolas")
        TextLog.FontSize = 11
        TextLog.Background = New SolidColorBrush(Color.FromRgb(250, 250, 252))
        TextLog.Height = 200
        Root.Children.Add(TextLog)

        AddHandler BtnStart.Click, AddressOf StartTunnel
        AddHandler BtnStop.Click, AddressOf StopTunnel
        AddHandler BtnCopy.Click, Sub() ClipboardSet(TextAddress.Text)
        AddHandler BtnFolder.Click, Sub()
                                        DirectoryUtils.Create(PathFolder)
                                        OpenExplorer(PathFolder)
                                    End Sub
        AddHandler BtnPanel.Click, Sub() OpenWebsite("https://www.natfrp.com/tunnel/")
        AddHandler Closing, Sub(sender, e) StopTunnel()
        TextArgument.Text = Settings.Get(Of String)("SakuraFrpArgument")
    End Sub

    Private Sub AppendLog(Line As String)
        Dim Clean As String = RegularExpressions.Regex.Replace(Line, "\x1B\[[0-9;]*m", "")
        TextLog.Dispatcher.Invoke(
            Sub()
                TextLog.AppendText(Clean & vbCrLf)
                TextLog.ScrollToEnd()
                '解析连接地址
                Dim Match = AddressRegex.Match(Clean)
                If Match.Success Then
                    TextAddress.Text = Match.Value
                    LabStatus.Text = "隧道已启动，把连接地址发给朋友，在多人游戏里直接连接即可。"
                ElseIf Clean.Contains("隧道启动成功") OrElse Clean.Contains("start proxy success") Then
                    LabStatus.Text = "隧道已启动，请从上方日志中复制连接地址。"
                End If
            End Sub)
    End Sub

    Private Sub StartTunnel()
        If SakuraProcess IsNot Nothing AndAlso Not SakuraProcess.HasExited Then
            Hint("隧道已经在运行了", HintType.Blue)
            Return
        End If
        Dim Argument As String = TextArgument.Text.Trim
        If Argument = "" Then
            Hint("请先填写启动参数（访问密钥:隧道ID）", HintType.Red)
            Return
        End If
        If Not Argument.Contains(":") Then
            Hint("启动参数格式应为：访问密钥:隧道ID", HintType.Red)
            Return
        End If
        Settings.Set("SakuraFrpArgument", Argument)
        DirectoryUtils.Create(PathFolder)
        If Not FileUtils.Exists(PathExe) Then
            MyMsgBox($"还没有找到 frpc.exe。{vbCrLf}{vbCrLf}请到 SakuraFrp 面板的「软件下载」页面下载 frpc（Windows，amd64），重命名为 frpc.exe 后放到：{vbCrLf}{PathFolder}{vbCrLf}{vbCrLf}放好后再次点击「开始隧道」。" & vbCrLf &
                     "也可以点击「打开文件夹」直接打开该目录。", "需要 frpc.exe", "打开文件夹", "知道了")
            OpenExplorer(PathFolder)
            Return
        End If
        Try
            TextLog.Clear()
            TextAddress.Clear()
            Dim Info As New ProcessStartInfo(PathExe) With {
                .Arguments = $"-f ""{Argument}""",
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .StandardOutputEncoding = Encoding.UTF8,
                .StandardErrorEncoding = Encoding.UTF8,
                .WorkingDirectory = PathFolder}
            SakuraProcess = New Process With {.StartInfo = Info, .EnableRaisingEvents = True}
            AddHandler SakuraProcess.OutputDataReceived, Sub(sender, e) If e.Data IsNot Nothing Then AppendLog(e.Data)
            AddHandler SakuraProcess.ErrorDataReceived, Sub(sender, e) If e.Data IsNot Nothing Then AppendLog(e.Data)
            AddHandler SakuraProcess.Exited, Sub()
                                                 Dispatcher.Invoke(
                                                 Sub()
                                                     LabStatus.Text = $"隧道已停止（退出码 {SakuraProcess.ExitCode}）"
                                                     BtnStart.IsEnabled = True
                                                     BtnStop.IsEnabled = False
                                                 End Sub)
                                             End Sub
            SakuraProcess.Start()
            SakuraProcess.BeginOutputReadLine()
            SakuraProcess.BeginErrorReadLine()
            LabStatus.Text = "正在启动隧道……"
            BtnStart.IsEnabled = False
            BtnStop.IsEnabled = True
            Logger.Info($"SakuraFrp：启动 frpc，参数 {Argument}")
        Catch ex As Exception
            Logger.Error(ex, "启动 SakuraFrp 隧道失败")
            LabStatus.Text = "启动失败：" & ex.Message
            Hint("启动隧道失败：" & ex.Message, HintType.Red)
            BtnStart.IsEnabled = True
            BtnStop.IsEnabled = False
        End Try
    End Sub

    Private Sub StopTunnel()
        Try
            If SakuraProcess IsNot Nothing AndAlso Not SakuraProcess.HasExited Then
                SakuraProcess.Kill()
                Logger.Info("SakuraFrp：已停止 frpc")
            End If
        Catch ex As Exception
            Logger.Warn(ex, "停止 SakuraFrp 隧道失败")
        End Try
        BtnStart.IsEnabled = True
        BtnStop.IsEnabled = False
        LabStatus.Text = "隧道已停止"
    End Sub
End Class
