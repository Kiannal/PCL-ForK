Public Class PageSetupSystem


    Private Sub PageSetupSystem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        Static IsLoaded As Boolean = False
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        SliderLoad()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()
        SettingService.RefreshSettings(Me)
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化启动器页设置")
            Hint("已初始化启动器页设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化启动器页设置失败", LogBehavior.Alert)
        End Try
        Reload()
    End Sub

    '滑动条
    Private Sub SliderLoad()
        SliderDownloadThread.GetHintText = Function(v) v + 1
        SliderDownloadSpeed.GetHintText =
        Function(v)
            Select Case v
                Case Is <= 14
                    Return (v + 1) * 0.1 & " M/s"
                Case Is <= 31
                    Return (v - 11) * 0.5 & " M/s"
                Case Is <= 41
                    Return (v - 21) & " M/s"
                Case Else
                    Return "无限制"
            End Select
        End Function
        SliderDebugAnim.GetHintText = Function(v) If(v > 29, "关闭", (v / 10 + 0.1) & "x")
    End Sub
    Private Sub SliderDownloadThread_PreviewChange(sender As Object, e As RouteEventArgs) Handles SliderDownloadThread.PreviewChange
        If SliderDownloadThread.Value < 100 Then Return
        If Not Settings.Get(Of Boolean)("HintDownloadThread") Then
            Settings.Set("HintDownloadThread", True)
            MyMsgBox("如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。" & vbCrLf &
                     "一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！", "警告", "我知道了", IsWarn:=True)
        End If
    End Sub


    '调试模式
    Private Sub CheckDebugMode_Change() Handles CheckDebugMode.Change
        If AniControlEnabled = 0 Then Hint("部分调试信息将在刷新或启动器重启后切换显示！",, False)
    End Sub

    '自动更新
    Private Sub ComboSystemActivity_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboSystemActivity.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If ComboSystemActivity.SelectedIndex <> 2 Then Return
        If MyMsgBox("若选择此项，即使在将来出现严重问题时，你也无法获取相关通知。" & vbCrLf &
                    "例如，如果发现某个版本游戏存在严重 Bug，你可能就会因为无法得到通知而导致无法预知的后果。" & vbCrLf & vbCrLf &
                    "一般选择 仅在有重要通知时显示公告 就可以让你尽量不受打扰了。" & vbCrLf &
                    "除非你在制作服务器整合包，或时常手动更新启动器，否则极度不推荐选择此项！", "警告", "我知道我在做什么", "取消", IsWarn:=True) = 2 Then
            If e.RemovedItems.Count > 0 Then ComboSystemActivity.SelectedItem = e.RemovedItems(0)
        End If
    End Sub
    Private Sub ComboSystemUpdate_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboSystemUpdate.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If ComboSystemUpdate.SelectedIndex <> 3 Then Return
        If MyMsgBox("若选择此项，即使在启动器将来出现严重问题时，你也无法获取更新并获得修复。" & vbCrLf &
                    "例如，如果官方修改了登录方式，从而导致现有启动器无法登录，你可能就会因为无法更新而无法开始游戏。" & vbCrLf & vbCrLf &
                    "一般选择 仅在有重大漏洞更新时显示提示 就可以让你尽量不受打扰了。" & vbCrLf &
                    "除非你在制作服务器整合包，或时常手动更新启动器，否则极度不推荐选择此项！", "警告", "我知道我在做什么", "取消", IsWarn:=True) = 2 Then
            If e.RemovedItems.Count > 0 Then ComboSystemUpdate.SelectedItem = e.RemovedItems(0)
        End If
    End Sub
    Private Sub BtnSystemUpdate_Click(sender As Object, e As EventArgs) Handles BtnSystemUpdate.Click
        UpdateCheckByButton()
    End Sub
    ''' <summary>
    ''' 启动器是否已经是最新版？
    ''' 若返回 Nothing，则代表无更新缓存文件或出错。
    ''' </summary>
    Public Shared Function IsLauncherNewest() As Boolean?
        Try
            '确认服务器公告是否正常
            Dim ServerContent As String = If(FileUtils.TryReadAsString(PathTemp & "Cache\Notice.cfg"), "")
            If ServerContent.Split("|").Count < 3 Then Return Nothing
            '确认是否为最新
            Return ServerContent.Split("|")(If(BuildType = BuildTypes.Release, 2, 1)) <= VersionCode
        Catch ex As Exception
            Logger.Error(ex, "确认启动器更新失败")
            Return Nothing
        End Try
    End Function

#Region "导出 / 导入设置"

    Private Sub BtnSystemSettingExp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingExp.Click
        Hint("该功能尚在开发中！")
    End Sub
    Private Sub BtnSystemSettingImp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingImp.Click
        Hint("该功能尚在开发中！")
    End Sub

#End Region

    Private Sub BtnFeedbackOpen_Click(sender As Object, e As MouseButtonEventArgs)
        PanFeedback.Visibility = Visibility.Visible
        TextFeedback.Focus()
    End Sub

    Private Sub BtnFeedbackClose_Click(sender As Object, e As MouseButtonEventArgs)
        PanFeedback.Visibility = Visibility.Collapsed
    End Sub

    Private Function FeedbackNick() As String
        Try
            Dim ms As String = Settings.Get(Of String)("CacheMsV2Name")
            If Not String.IsNullOrWhiteSpace(ms) Then Return ms.Trim()
        Catch
        End Try
        Try
            Dim auth As String = Settings.Get(Of String)("CacheAuthUsername")
            If Not String.IsNullOrWhiteSpace(auth) Then Return "[第三方账号]" & auth.Trim()
        Catch
        End Try
        Try
            Dim legacy As String = Settings.Get(Of String)("LoginLegacyName")
            If legacy.Contains("¨") Then legacy = legacy.BeforeFirst("¨")
            legacy = legacy.Replace("¨", "").Trim()
            If Not String.IsNullOrWhiteSpace(legacy) Then Return "[离线账号]" & legacy
        Catch
        End Try
        Return "匿名"
    End Function


    Private Function FeedbackText() As String
        Try
            Return If(TextFeedback.Text, "")
        Catch
            Return ""
        End Try
    End Function

    Private Sub BtnFeedbackSend_Click(sender As Object, e As MouseButtonEventArgs)
        Dim body As String = FeedbackText().Trim()

        Logger.Info("[Feedback] 取到正文长度：" & body.Length)
        If body.Length = 0 Then
            MyMsgBox("输入框里还没有内容：请先在多行输入框中填写问题描述，再点发送。", "问题反馈", IsWarn:=True)
            Return
        End If
        Dim nick As String = FeedbackNick()
        Dim ver As String = ""
        Try
            ver = Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()
        Catch
        End Try
        BtnFeedbackSend.IsEnabled = False
        RunInNewThread(
            Sub()
                Try
                    Using wc As New System.Net.WebClient()
                        wc.Encoding = System.Text.Encoding.UTF8
                        wc.Proxy = Nothing

                        wc.Headers("Content-Type") = "application/x-www-form-urlencoded"
                        wc.Headers.Add("User-Agent", "PCL-ForK")
                        Dim resp As String = wc.UploadString(ModServerEndpoints.FeedbackUrl, "name=" & Uri.EscapeDataString(nick) & "&version=" & Uri.EscapeDataString(ver) & "&content=" & Uri.EscapeDataString(body))
                        Logger.Info("[Feedback] 服务器返回：" & resp)
                        If resp.Contains("""ok"":false") Then
                            RunInUi(Sub()
                                        BtnFeedbackSend.IsEnabled = True
                                        MyMsgBox("服务器没有收录这条反馈，返回：" & resp, "问题反馈", IsWarn:=True)
                                    End Sub)
                            Return
                        End If
                    End Using
                    RunInUi(Sub()
                                BtnFeedbackSend.IsEnabled = True
                                PanFeedback.Visibility = Visibility.Collapsed
                                TextFeedback.Text = ""
                                MyMsgBox("反馈已发送，感谢你的反馈！", "问题反馈")
                            End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "[Feedback] 发送失败")
                    RunInUi(Sub()
                                BtnFeedbackSend.IsEnabled = True
                                MyMsgBox("发送失败：" & ex.Message, "问题反馈", IsWarn:=True)
                            End Sub)
                End Try
            End Sub, "FeedbackSend")
    End Sub


    Private Sub BtnFeedback_Wire(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        AddHandler BtnFeedbackOpen.Click, AddressOf BtnFeedbackOpen_Click
        AddHandler BtnFeedbackSend.Click, AddressOf BtnFeedbackSend_Click
        AddHandler BtnFeedbackClose.Click, AddressOf BtnFeedbackClose_Click
    End Sub

End Class
