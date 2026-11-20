Public Class PageOtherChat

    Private LastId As Integer = 0
    Private Timer As Threading.Timer
    Private Nick As String = ""


    Private Function SafeGet(Key As String) As String
        Try
            Return If(Settings.Get(Of String)(Key), "")
        Catch
            Return ""
        End Try
    End Function

    Private Function CurrentName() As String
        Dim ms As String = SafeGet("CacheMsV2Name")
        If Not String.IsNullOrWhiteSpace(ms) Then Return ms.Trim()
        Dim auth As String = SafeGet("CacheAuthUsername")
        If Not String.IsNullOrWhiteSpace(auth) Then Return "[第三方账号]" & auth.Trim()
        Dim legacy As String = SafeGet("LoginLegacyName")
        If legacy.Contains("¨") Then legacy = legacy.BeforeFirst("¨")
        legacy = legacy.Replace("¨", "").Trim()
        If String.IsNullOrWhiteSpace(legacy) Then legacy = SafeGet("CacheLegacyName").Trim()
        If String.IsNullOrWhiteSpace(legacy) Then legacy = "玩家"
        Return "[离线账号]" & legacy
    End Function

    Private Function SkinPath() As String
        Try
            Dim id As String = SafeGet("LaunchSkinID")
            If String.IsNullOrWhiteSpace(id) Then Return ""
            Dim dir As String = IO.Path.Combine(PathPure.Value, "Skins")
            If Not IO.Directory.Exists(dir) Then Return ""
            For Each f As String In IO.Directory.GetFiles(dir)
                If IO.Path.GetFileNameWithoutExtension(f) = id Then Return f
            Next
            Dim best As String = ""
            For Each f As String In IO.Directory.GetFiles(dir, "*.png")
                If best = "" OrElse IO.File.GetLastWriteTime(f) > IO.File.GetLastWriteTime(best) Then best = f
            Next
            Return best
        Catch
            Return ""
        End Try
    End Function

    Private Function HeadImage(path As String) As ImageSource
        Try
            If String.IsNullOrWhiteSpace(path) OrElse Not IO.File.Exists(path) Then Return Nothing
            Dim bmp As New BitmapImage()
            bmp.BeginInit()
            bmp.CacheOption = BitmapCacheOption.OnLoad
            bmp.UriSource = New Uri(path)
            bmp.EndInit()
            Dim w As Integer = bmp.PixelWidth
            If w < 64 Then Return bmp
            Dim crop As New CroppedBitmap(bmp, New Int32Rect(8 * w \ 64, 8 * w \ 64, w \ 8, w \ 8))
            Return crop
        Catch
            Return Nothing
        End Try
    End Function

    Private Sub LoadAccount()
        Nick = CurrentName()
        TextName.Text = Nick
        Try
            Dim src As ImageSource = HeadImage(SkinPath())
            If src Is Nothing Then Return
            Dim br As New Border With {.Width = 32, .Height = 32, .CornerRadius = New CornerRadius(6), .Margin = New Thickness(0, 0, 10, 0), .Background = New ImageBrush(src) With {.Stretch = Stretch.UniformToFill}}
            Dim sp As StackPanel = TryCast(TextName.Parent, StackPanel)
            If sp IsNot Nothing Then sp.Children.Insert(0, br)
        Catch
        End Try
    End Sub

    Private Sub PageOtherChat_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        LoadAccount()
        AddHandler BtnSend.Click, AddressOf BtnSend_Click
        AddHandler BtnRefresh.Click, AddressOf BtnRefresh_Click
        AddHandler TextInput.KeyDown, AddressOf TextInput_KeyDown
        Refresh_Async()
        Timer = New Threading.Timer(AddressOf TimerTick, Nothing, 5000, 5000)
    End Sub

    Private Sub TimerTick(state As Object)
        Refresh_Async()
    End Sub

    Private Sub BtnRefresh_Click(sender As Object, e As MouseButtonEventArgs)
        Refresh_Async()
    End Sub

    Private Sub BtnSend_Click(sender As Object, e As MouseButtonEventArgs)
        SendMsg()
    End Sub

    Private Sub TextInput_KeyDown(sender As Object, e As Windows.Input.KeyEventArgs)
        If e.Key = Windows.Input.Key.Enter Then
            e.Handled = True
            SendMsg()
        End If
    End Sub

    Private Sub SendMsg()
        Dim body As String = If(TextInput.Text, "").Trim()
        If body.Length = 0 Then Return
        Dim nick As String = CurrentName()
        If String.IsNullOrWhiteSpace(nick) Then nick = "玩家"
        Nick = nick
        TextInput.Text = ""
        RunInNewThread(
            Sub()
                Try
                    Using wc As New System.Net.WebClient()
                        wc.Encoding = System.Text.Encoding.UTF8
                        wc.Headers.Add("User-Agent", "PCL-ForK")
                        wc.Headers.Add("Content-Type", "application/x-www-form-urlencoded")
                        wc.UploadString(ModServerEndpoints.ChatUrl, "name=" & Uri.EscapeDataString(nick) & "&text=" & Uri.EscapeDataString(body))
                    End Using
                Catch ex As Exception
                    Logger.Warn(ex, "发送聊天消息失败")
                End Try
                Refresh_Async()
            End Sub, "ChatSend")
    End Sub

    Private Sub Refresh_Async()
        RunInNewThread(
            Sub()
                Try
                    Dim raw As String
                    Using wc As New System.Net.WebClient()
                        wc.Encoding = System.Text.Encoding.UTF8
                        wc.Headers.Add("User-Agent", "PCL-ForK")
                        raw = wc.DownloadString(ModServerEndpoints.ChatApiUrl)
                    End Using
                    Dim msgs As New List(Of String)
                    Dim maxId As Integer = LastId
                    Dim mm = System.Text.RegularExpressions.Regex.Matches(raw, """id""\s*:\s*(\d+)\s*,\s*""name""\s*:\s*""((?:[^""\\]|\\.)*)""\s*,\s*""text""\s*:\s*""((?:[^""\\]|\\.)*)""\s*,\s*""time""\s*:\s*""((?:[^""\\]|\\.)*)""")
                    For Each m As System.Text.RegularExpressions.Match In mm
                        Dim id As Integer
                        If Integer.TryParse(m.Groups(1).Value, id) Then
                            If id > maxId Then maxId = id
                            msgs.Add(m.Groups(4).Value & "   " & m.Groups(2).Value & vbCrLf & m.Groups(3).Value.Replace("\n", vbCrLf))
                        End If
                    Next
                    RunInUi(
                        Sub()
                            Try
                                PanMsgs.Children.Clear()
                                If msgs.Count = 0 Then
                                    Dim tip As New TextBlock With {.Text = "还没有人说话，发第一条吧", .Opacity = 0.6, .Margin = New Thickness(6, 6, 0, 0), .TextWrapping = TextWrapping.Wrap}
                                    PanMsgs.Children.Add(tip)
                                Else
                                    For Each s As String In msgs
                                        Dim tb As New TextBlock With {.Text = s, .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(6, 0, 6, 12), .LineHeight = 20}
                                        PanMsgs.Children.Add(tb)
                                    Next
                                End If
                                LabState.Text = "共 " & msgs.Count & " 条消息 · 每 5 秒自动刷新"
                                ScrollBox.ScrollToBottom()
                            Catch
                            End Try
                        End Sub)
                    LastId = maxId
                Catch ex As Exception
                    RunInUi(Sub()
                                LabState.Text = "连接失败：" & ex.Message
                            End Sub)
                End Try
            End Sub, "ChatRefresh")
    End Sub

End Class
