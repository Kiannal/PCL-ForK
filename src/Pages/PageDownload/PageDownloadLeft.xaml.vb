Public Class PageDownloadLeft
    Implements IRefreshable

#Region "页面切换"

    ''' <summary>
    ''' 当前页面的编号。
    ''' </summary>
    Public PageID As FormMain.PageSubType = FormMain.PageSubType.DownloadInstall

    '隐藏 MC 下载
    Public Sub New()
        InitializeComponent()
        If Not RestrictMcDownload Then Return
        ItemInstall.Visibility = Visibility.Collapsed
        LabResource.Visibility = Visibility.Collapsed
        PageID = FormMain.PageSubType.DownloadMod
        ItemMod.Checked = True
    End Sub

    ''' <summary>
    ''' 勾选事件改变页面。
    ''' </summary>
    Private Sub PageCheck(sender As FrameworkElement, e As RouteEventArgs) Handles ItemMap.Check, ItemInstall.Check, ItemMod.Check, ItemPack.Check, ItemSkin.Check, ItemResourcePack.Check, ItemShader.Check, ItemDataPack.Check
        '尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
        '若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub

    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.DownloadInstall
                If FrmDownloadInstall Is Nothing Then FrmDownloadInstall = New PageDownloadInstall
                Return FrmDownloadInstall
            Case FormMain.PageSubType.DownloadMod
                If FrmDownloadMod Is Nothing Then FrmDownloadMod = New PageDownloadMod
                Return FrmDownloadMod
            Case FormMain.PageSubType.DownloadPack
                If FrmDownloadPack Is Nothing Then FrmDownloadPack = New PageDownloadPack
                Return FrmDownloadPack
            Case FormMain.PageSubType.DownloadResourcePack
                If FrmDownloadResourcePack Is Nothing Then FrmDownloadResourcePack = New PageDownloadResourcePack
                Return FrmDownloadResourcePack
            Case FormMain.PageSubType.DownloadShader
                If FrmDownloadShader Is Nothing Then FrmDownloadShader = New PageDownloadShader
                Return FrmDownloadShader
            Case FormMain.PageSubType.DownloadDataPack
                If FrmDownloadDataPack Is Nothing Then FrmDownloadDataPack = New PageDownloadDataPack
                Return FrmDownloadDataPack
            Case FormMain.PageSubType.DownloadMap
                ModMain.PclSourceType = "map"
                If FrmDownloadMap Is Nothing Then FrmDownloadMap = New PageDownloadMap
                Return FrmDownloadMap
            Case CType(7, FormMain.PageSubType)
                If FrmOtherSkin Is Nothing Then FrmOtherSkin = New PageOtherSkin
                Return FrmOtherSkin
            Case FormMain.PageSubType.DownloadUpload
                If FrmDownloadUpload Is Nothing Then FrmDownloadUpload = New PageDownloadUpload
                Return FrmDownloadUpload
            Case Else
                Throw New Exception("未知的下载子页面种类：" & ID)
        End Select
    End Function

    ''' <summary>
    ''' 切换现有页面。
    ''' </summary>
    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        AniControlEnabled += 1
        Try
            PageChangeRun(PageGet(ID))
            PageID = ID
        Catch ex As Exception
            Logger.Error(ex, $"切换分页面失败（ID {ID}）")
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub
    Private Shared Sub PageChangeRun(Target As MyPageRight)
        AniStop("FrmMain PageChangeRight") '停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        If Target.Parent IsNot Nothing Then Target.SetValue(ContentPresenter.ContentProperty, Nothing)
        FrmMain.PageRight = Target
        CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnExit()
        AniStart({
            AaCode(
            Sub()
                CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnForceExit()
                FrmMain.PanMainRight.Child = FrmMain.PageRight
                FrmMain.PageRight.Opacity = 0
            End Sub, 130),
            AaCode(
            Sub()
                '延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                FrmMain.PageRight.Opacity = 1
                FrmMain.PageRight.PageOnEnter()
            End Sub, 30, True)
        }, "PageLeft PageChange")
    End Sub

#End Region

    '强制刷新
    Public Sub Refresh_Click(sender As Object, e As EventArgs) '由边栏按钮匿名调用
        Refresh(Val(sender.Tag))
    End Sub
    Public Sub Refresh() Implements IRefreshable.Refresh
        Refresh(FrmMain.PageCurrentSub)
    End Sub
    Public Sub Refresh(SubType As FormMain.PageSubType)
        ResourceProject.Cache.Clear()
        ResourceVersion.ProjectFilesCache.Clear()
        Select Case SubType
            Case FormMain.PageSubType.DownloadInstall
                DlClientListLoader.Start(IsForceRestart:=True)
                DlOptiFineListLoader.Start(IsForceRestart:=True)
                DlForgeListLoader.Start(IsForceRestart:=True)
                DlNeoForgeListLoader.Start(IsForceRestart:=True)
                DlLiteLoaderListLoader.Start(IsForceRestart:=True)
                DlFabricListLoader.Start(IsForceRestart:=True)
                DlFabricApiLoader.Start(IsForceRestart:=True)
                DlOptiFabricLoader.Start(IsForceRestart:=True)
                ItemInstall.Checked = True
            Case FormMain.PageSubType.DownloadMod
                If FrmDownloadMod IsNot Nothing Then
                    FrmDownloadMod.Content.Storage = New ResourceSearcher.SearchResult
                    FrmDownloadMod.Content.Page = 0
                    FrmDownloadMod.PageLoaderRestart()
                End If
                ItemMod.Checked = True
            Case FormMain.PageSubType.DownloadPack
                If FrmDownloadPack IsNot Nothing Then
                    FrmDownloadPack.Content.Storage = New ResourceSearcher.SearchResult
                    FrmDownloadPack.Content.Page = 0
                    FrmDownloadPack.PageLoaderRestart()
                End If
                ItemPack.Checked = True
            Case FormMain.PageSubType.DownloadResourcePack
                If FrmDownloadResourcePack IsNot Nothing Then
                    FrmDownloadResourcePack.Content.Storage = New ResourceSearcher.SearchResult
                    FrmDownloadResourcePack.Content.Page = 0
                    FrmDownloadResourcePack.PageLoaderRestart()
                End If
                ItemResourcePack.Checked = True
            Case FormMain.PageSubType.DownloadShader
                If FrmDownloadShader IsNot Nothing Then
                    FrmDownloadShader.Content.Storage = New ResourceSearcher.SearchResult
                    FrmDownloadShader.Content.Page = 0
                    FrmDownloadShader.PageLoaderRestart()
                End If
                ItemShader.Checked = True
            Case FormMain.PageSubType.DownloadDataPack
                If FrmDownloadDataPack IsNot Nothing Then
                    FrmDownloadDataPack.Content.Storage = New ResourceSearcher.SearchResult
                    FrmDownloadDataPack.Content.Page = 0
                    FrmDownloadDataPack.PageLoaderRestart()
                End If
                ItemDataPack.Checked = True
            Case CType(7, FormMain.PageSubType)
                ItemSkin.Checked = True
        End Select
        Hint("正在刷新……", Log:=False)
    End Sub

    '点击返回
    Private Sub ItemInstall_Click(sender As Object, e As MouseButtonEventArgs) Handles ItemInstall.Click
        If Not ItemInstall.Checked Then Return
        FrmDownloadInstall.ExitSelectPage()
    End Sub

    Private Sub ItemUpload_Init(sender As FrameworkElement, e As RouteEventArgs) Handles ItemUpload.Check
        If Not ItemUpload.Checked Then Return
        PageChange(CType(8, FormMain.PageSubType))
    End Sub

    Private Sub ItemUpload_Click(sender As Object, e As EventArgs)
        Try
            Dim W As New Window()
            W.Title = "上传资源 · PCL-ForK"
            W.Width = 640
            W.Height = 560
            W.Background = New SolidColorBrush(Color.FromRgb(245, 246, 248))
            W.WindowStartupLocation = WindowStartupLocation.CenterOwner
            If Window.GetWindow(Me) IsNot Nothing Then W.Owner = Window.GetWindow(Me)

            Dim Scroll As New ScrollViewer() With {.VerticalScrollBarVisibility = ScrollBarVisibility.Auto}
            Dim Root As New StackPanel() With {.Margin = New Thickness(20)}

            Dim CardType As New MyCard() With {.Title = "选择上传类型", .Margin = New Thickness(0, 0, 0, 12)}
            Dim TypePanel As New WrapPanel() With {.Margin = New Thickness(20, 42, 20, 16)}
            CardType.Children.Add(TypePanel)
            Root.Children.Add(CardType)

            Dim CardInfo As New MyCard() With {.Title = "资源信息", .Margin = New Thickness(0, 0, 0, 12)}
            Dim InfoPanel As New StackPanel() With {.Margin = New Thickness(20, 42, 20, 16)}
            InfoPanel.Children.Add(New TextBlock() With {.Text = "名称（留空则用文件名）", .FontSize = 12, .Opacity = 0.65, .Margin = New Thickness(0, 0, 0, 6)})
            Dim TextName As New MyTextBox() With {.HintText = "例如：Complementary Shaders 重制版", .Height = 32}
            InfoPanel.Children.Add(TextName)
            InfoPanel.Children.Add(New TextBlock() With {.Text = "简介", .FontSize = 12, .Opacity = 0.65, .Margin = New Thickness(0, 12, 0, 6)})
            Dim TextDesc As New MyTextBox() With {.HintText = "写点介绍，别人下载时能看到", .Height = 92, .AcceptsReturn = True, .TextWrapping = TextWrapping.Wrap}
            InfoPanel.Children.Add(TextDesc)
            CardInfo.Children.Add(InfoPanel)
            Root.Children.Add(CardInfo)

            Dim LabState As New TextBlock() With {.Text = "", .FontSize = 12.5, .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(2, 0, 2, 12), .Foreground = New SolidColorBrush(Color.FromRgb(74, 83, 97))}
            Root.Children.Add(LabState)

            Dim CloseBtn As New MyButton() With {.Text = "关闭", .Width = 92, .Height = 34, .HorizontalAlignment = HorizontalAlignment.Right, .Margin = New Thickness(0, 0, 2, 6)}
            Root.Children.Add(CloseBtn)
            AddHandler CloseBtn.Click, Sub(s2, e2) W.Close()

            Dim Types As New List(Of KeyValuePair(Of String, String)) From {
                New KeyValuePair(Of String, String)("mod", "上传 Mod"),
                New KeyValuePair(Of String, String)("pack", "上传整合包"),
                New KeyValuePair(Of String, String)("resourcepack", "上传资源包"),
                New KeyValuePair(Of String, String)("datapack", "上传数据包"),
                New KeyValuePair(Of String, String)("shader", "上传光影包")
            }
            For Each KV As KeyValuePair(Of String, String) In Types
                Dim Btn As New MyButton() With {.Text = KV.Value, .Width = 120, .Height = 36, .Margin = New Thickness(0, 0, 10, 10)}
                Dim TypeKey As String = KV.Key
                Dim TypeName As String = KV.Value.Replace("上传 ", "")
                AddHandler Btn.Click,
                    Sub(s2, e2)
                        Dim Dlg As New Microsoft.Win32.OpenFileDialog()
                        Dlg.Title = "选择要上传的" & TypeName
                        Dlg.Filter = "资源文件|*.jar;*.zip;*.7z;*.rar;*.mrpack;*.mcpack|所有文件|*.*"
                        If Dlg.ShowDialog(W) <> True Then Return
                        Dim Path As String = Dlg.FileName
                        Dim Title As String = GetInnerText(TextName).Trim()
                        If Title.Length = 0 Then Title = System.IO.Path.GetFileNameWithoutExtension(Path)
                        Dim Desc As String = GetInnerText(TextDesc).Trim()
                        LabState.Text = "正在上传 " & System.IO.Path.GetFileName(Path) & " …"
                        Dim Th As New Threading.Thread(
                            Sub()
                                Try
                                    Dim R As String = UploadFile(TypeKey, Title, Desc, Path)
                                    If R.Contains("""ok"":true") Then
                                        W.Dispatcher.Invoke(Sub()
                                                                LabState.Text = "上传成功：" & Title
                                                                MyMsgBox("上传成功，其他人已可在对应下载页看到并下载。" & vbCrLf & Title, "上传" & TypeName)
                                                            End Sub)
                                    Else
                                        W.Dispatcher.Invoke(Sub() LabState.Text = "上传失败：" & R)
                                    End If
                                Catch Ex As Exception
                                    W.Dispatcher.Invoke(Sub() LabState.Text = "上传失败：" & Ex.Message)
                                End Try
                            End Sub)
                        Th.IsBackground = True
                        Th.Start()
                    End Sub
                TypePanel.Children.Add(Btn)
            Next

            Scroll.Content = Root
            W.Content = Scroll
            W.ShowDialog()
        Catch Ex As Exception
            MyMsgBox("打开上传窗口失败：" & Ex.Message, "上传")
        End Try
    End Sub

    Private Function GetInnerText(Root As DependencyObject) As String
        Try
            Dim TB As System.Windows.Controls.TextBox = FindInnerTextbox(Root)
            If TB IsNot Nothing Then Return If(TB.Text, "")
        Catch
        End Try
        Return ""
    End Function

    Private Function FindInnerTextbox(Root As DependencyObject) As System.Windows.Controls.TextBox
        If Root Is Nothing Then Return Nothing
        Dim Self As System.Windows.Controls.TextBox = TryCast(Root, System.Windows.Controls.TextBox)
        If Self IsNot Nothing Then Return Self
        Dim Count As Integer = 0
        Try
            Count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(Root)
        Catch
            Return Nothing
        End Try
        For I As Integer = 0 To Count - 1
            Dim R As System.Windows.Controls.TextBox = FindInnerTextbox(System.Windows.Media.VisualTreeHelper.GetChild(Root, I))
            If R IsNot Nothing Then Return R
        Next
        Return Nothing
    End Function

    Private Function UploadFile(Type As String, Name As String, Desc As String, FilePath As String) As String
        Dim Boundary As String = "----PCLForK" & Guid.NewGuid().ToString("N")
        Dim Enc As Text.Encoding = Text.Encoding.UTF8
        Using MS As New IO.MemoryStream()
            Dim Fields As New List(Of KeyValuePair(Of String, String)) From {
                New KeyValuePair(Of String, String)("type", Type),
                New KeyValuePair(Of String, String)("name", Name),
                New KeyValuePair(Of String, String)("desc", Desc)
            }
            For Each KV As KeyValuePair(Of String, String) In Fields
                Dim Part As String = "--" & Boundary & vbCrLf & "Content-Disposition: form-data; name=""" & KV.Key & """" & vbCrLf & vbCrLf & KV.Value & vbCrLf
                Dim PB As Byte() = Enc.GetBytes(Part)
                MS.Write(PB, 0, PB.Length)
            Next
            Dim FileName As String = IO.Path.GetFileName(FilePath)
            Dim Head As String = "--" & Boundary & vbCrLf & "Content-Disposition: form-data; name=""file""; filename=""" & FileName & """" & vbCrLf & "Content-Type: application/octet-stream" & vbCrLf & vbCrLf
            Dim HB As Byte() = Enc.GetBytes(Head)
            MS.Write(HB, 0, HB.Length)
            Dim FB As Byte() = IO.File.ReadAllBytes(FilePath)
            MS.Write(FB, 0, FB.Length)
            Dim Tail As Byte() = Enc.GetBytes(vbCrLf & "--" & Boundary & "--" & vbCrLf)
            MS.Write(Tail, 0, Tail.Length)

            Dim Req As Net.HttpWebRequest = CType(Net.WebRequest.Create(ModMain.PclUrl("MkXol2Aes9ZiCLLWagSy0W8frtZjC6TfYwizkipd84Y+H+yPKg==")), Net.HttpWebRequest)
            Req.Method = "POST"
            Req.ContentType = "multipart/form-data; boundary=" & Boundary
            Req.ContentLength = MS.Length
            Req.Proxy = Nothing
            Req.UserAgent = "PCL-ForK"
            Req.Timeout = 900000
            Req.ReadWriteTimeout = 900000
            MS.Position = 0
            Using RS As IO.Stream = Req.GetRequestStream()
                MS.CopyTo(RS)
            End Using
            Using Resp As Net.WebResponse = Req.GetResponse()
                Using SR As New IO.StreamReader(Resp.GetResponseStream(), Text.Encoding.UTF8)
                    Return SR.ReadToEnd()
                End Using
            End Using
        End Using
    End Function


    Private PclSubTimer As System.Windows.Threading.DispatcherTimer
    Private PclSubOpened As Boolean = False

    Private Sub ItemPclRes_Init(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If PclSubTimer IsNot Nothing Then Return
        AddHandler ItemPclRes.MouseEnter, Sub() ShowPclSub()
        AddHandler ItemPclRes.MouseLeftButtonUp, Sub()
            If PclSubOpened Then HidePclSub() Else ShowPclSub()
        End Sub
        PclSubTimer = New System.Windows.Threading.DispatcherTimer With {.Interval = New TimeSpan(0, 0, 0, 0, 200)}
        AddHandler PclSubTimer.Tick, Sub()
            If ItemPclRes.IsMouseOver OrElse PanPclSub.IsMouseOver Then Return
            HidePclSub()
        End Sub
        PclSubTimer.Start()
        AddHandler ItemPclMod.Check, Sub(s As FrameworkElement, e2 As RouteEventArgs) PclJump(2)
        AddHandler ItemPclDataPack.Check, Sub(s As FrameworkElement, e2 As RouteEventArgs) PclJump(4)
        AddHandler ItemPclShader.Check, Sub(s As FrameworkElement, e2 As RouteEventArgs) PclJump(6)
        AddHandler ItemPclPack.Check, Sub(s As FrameworkElement, e2 As RouteEventArgs) PclJump(3)
        AddHandler ItemPclResourcePack.Check, Sub(s As FrameworkElement, e2 As RouteEventArgs) PclJump(5)
        AddHandler ItemPclMod.MouseLeftButtonUp, Sub() PclJump(2)
        AddHandler ItemPclDataPack.MouseLeftButtonUp, Sub() PclJump(4)
        AddHandler ItemPclShader.MouseLeftButtonUp, Sub() PclJump(6)
        AddHandler ItemPclPack.MouseLeftButtonUp, Sub() PclJump(3)
        AddHandler ItemPclResourcePack.MouseLeftButtonUp, Sub() PclJump(5)
    End Sub

    Private Sub ShowPclSub()
        If PclSubOpened Then Return
        PclSubOpened = True
        PanPclSub.Visibility = Visibility.Visible
        PanPclSub.MaxHeight = 0
        Dim Dur As New Duration(TimeSpan.FromMilliseconds(190))
        Dim Ease As New System.Windows.Media.Animation.CubicEase With {.EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut}
        PanPclSub.BeginAnimation(FrameworkElement.MaxHeightProperty, New System.Windows.Media.Animation.DoubleAnimation(0, 168, Dur) With {.EasingFunction = Ease})
        PanPclSub.BeginAnimation(UIElement.OpacityProperty, New System.Windows.Media.Animation.DoubleAnimation(0, 1, Dur))
        PanPclSub.RenderTransform = New TranslateTransform(0, -10)
        PanPclSub.BeginAnimation(UIElement.RenderTransformProperty, Nothing)
        Dim Tg As New TranslateTransform(0, -10)
        PanPclSub.RenderTransform = Tg
        Tg.BeginAnimation(TranslateTransform.YProperty, New System.Windows.Media.Animation.DoubleAnimation(-10, 0, Dur) With {.EasingFunction = Ease})
    End Sub

    Private Sub HidePclSub()
        If Not PclSubOpened Then Return
        PclSubOpened = False
        Dim Dur As New Duration(TimeSpan.FromMilliseconds(150))
        Dim A1 As New System.Windows.Media.Animation.DoubleAnimation(PanPclSub.ActualHeight, 0, Dur)
        PanPclSub.BeginAnimation(FrameworkElement.MaxHeightProperty, A1)
        PanPclSub.BeginAnimation(UIElement.OpacityProperty, New System.Windows.Media.Animation.DoubleAnimation(1, 0, Dur))
        AddHandler A1.Completed, Sub()
            If Not PclSubOpened Then PanPclSub.Visibility = Visibility.Collapsed
        End Sub
    End Sub

    Private PclJumpAt As Date = Date.MinValue

    Private Sub PclJump(SubType As Integer)
        If (Date.Now - PclJumpAt).TotalMilliseconds < 400 Then Return
        PclJumpAt = Date.Now
        Dim Key As String = ""
        Select Case SubType
            Case 2 : Key = "mod"
            Case 3 : Key = "pack"
            Case 4 : Key = "datapack"
            Case 5 : Key = "resourcepack"
            Case 6 : Key = "shader"
            Case 11 : Key = "map"
        End Select
        ModMain.RequestPclSource(Key)
        ItemPclRes.Checked = True
        PageChange(CType(SubType, FormMain.PageSubType))
    End Sub

End Class
