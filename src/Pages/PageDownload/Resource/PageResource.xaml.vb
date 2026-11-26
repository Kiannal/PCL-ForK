Imports System.Windows.Markup

<ContentProperty("SearchTags")>
Public Class PageResource

    Private PclOnlyMode As Boolean = False
    ''' <summary>
    ''' 强制只读 PCL 资源通道，不发起在线搜索。
    ''' </summary>
    Public Property ForcePclOnly As Boolean = False

#Region "属性"

    ''' <summary>
    ''' 用于 XAML 快速设置的 Tag 下拉框列表。
    ''' </summary>
    Public ReadOnly Property SearchTags As ItemCollection
        Get
            Return ComboSearchTag.Items
        End Get
    End Property

    ''' <summary>
    ''' 英文前后不含空格的可读资源类型名，例如 "Mod"、"整合包"。
    ''' </summary>
    Public Property TypeName As String
        Get
            Return _TypeName
        End Get
        Set(Value As String)
            If _TypeName = Value Then Return
            _TypeName = Value
            Loader.Name = $"社区资源获取：{Value}"
        End Set
    End Property
    Private _TypeName As String = ""

    ''' <summary>
    ''' 英文前后含一个空格的可读资源类型名，例如 " Mod "、"整合包"。
    ''' </summary>
    Public Property TypeNameSpaced As String
        Get
            Return _TypeNameSpaced
        End Get
        Set(Value As String)
            If _TypeNameSpaced = Value Then Return
            _TypeNameSpaced = Value
            PanAlways.Title = $"搜索{Value}"
            Load.Text = $"正在获取{Value}列表"
        End Set
    End Property
    Private _TypeNameSpaced As String = ""

    ''' <summary>
    ''' 该页面对应的资源类型。
    ''' </summary>
    Public Property PageType As ResourceTypes
        Get
            Return _Type
        End Get
        Set(Value As ResourceTypes)
            If _Type = Value Then Return
            _Type = Value
            BtnSearchInstallModPack.Visibility = If(Value = ResourceTypes.ModPack AndAlso Not RestrictMcDownload, Visibility.Visible, Visibility.Collapsed)
        End Set
    End Property
    Private _Type As ResourceTypes = -1
    Private HasLoadedOnce As Boolean = False

#End Region

#Region "加载"

    ''' <summary>
    ''' 在切换到页面时，应自动将筛选项设置为与该目标 MC 版本和加载器相同。
    ''' </summary>
    Public Shared TargetInstance As McInstance = Nothing
    ''' <summary>
    ''' 在切换到该页面时自动设置的搜索框内的内容。
    ''' </summary>
    Public Shared TargetName As String = Nothing

    '在点击 MyResourceItem 时会获取 Loader 的输入，以使资源详情页面可以应用相同的筛选项
    Public Loader As LoaderTask(Of ResourceSearcher.SearchRequest, Integer) = ResourceSearcher.GetLoader(AddressOf LoaderInput)

    Private Sub PageResource_Loaded(sender As Object, e As EventArgs) Handles Me.Loaded
        If ForcePclOnly Then
            ApplyPclOnly()
        End If
        '只有从左侧「PCL资源」子菜单进入时才切成只读 PCL-ForK 模式，普通入口保持原样
        AddHandler ModMain.PclSourceRequested, Sub(Key As String)
            If Key <> PclTypeKey() Then Return
            ModMain.PclSourceType = ""
            ApplyPclOnly()
        End Sub
        If ModMain.PclSourceType.Length > 0 AndAlso ModMain.PclSourceType = PclTypeKey() Then
            ModMain.PclSourceType = ""
            ApplyPclOnly()
        End If
        Static IsFirstLoaded As Boolean = False
        '不知道从 Initialized 改成 Loaded 会不会有问题，但用 Initialized 会导致初始的筛选器修改被覆盖回默认值
        If TargetInstance IsNot Nothing Then
            '设置目标
            ResetFilter() '重置筛选器
            TextSearchVersion.Text = TargetInstance.Version.VanillaName
            Dim GetTargetItemByName =
            Function(Name As String) As MyComboBoxItem
                For Each Item As MyComboBoxItem In ComboSearchLoader.Items
                    If Item.Content = Name Then Return Item
                Next
                Return ComboSearchLoader.Items(0)
            End Function
            If TargetInstance.Version.ModLoaders.HasFlagF(ModLoaders.Forge) Then
                ComboSearchLoader.SelectedItem = GetTargetItemByName("Forge")
            ElseIf TargetInstance.Version.ModLoaders.HasFlagF(ModLoaders.Fabric) Then
                ComboSearchLoader.SelectedItem = GetTargetItemByName("Fabric")
            ElseIf TargetInstance.Version.ModLoaders.HasFlagF(ModLoaders.NeoForge) Then
                ComboSearchLoader.SelectedItem = GetTargetItemByName("NeoForge")
            End If
            TargetInstance = Nothing
            If TargetName IsNot Nothing Then
                TextSearchName.Text = TargetName
                TargetName = Nothing
            End If
            '如果已经完成请求，则重新开始
            If IsFirstLoaded Then StartNewSearch()
            ScrollToHome()
        End If
        '加载器初始化
        If IsFirstLoaded Then Return
        IsFirstLoaded = True
        HasLoadedOnce = True
        If PclOnlyMode Then Return
        '加载器初始化
        CType(Parent, MyPageRight).PageLoaderInit(Load, PanLoad, PanContent, PanAlways, Loader, AddressOf Load_OnFinish, AddressOf LoaderInput)
        '将最高 Drop 加入筛选
        If AllDrops IsNot Nothing AndAlso AllDrops.First > 250 Then
            Dim HighestVersion As String = McVersion.DropToVersion(AllDrops.First)
            If CType(TextSearchVersion.Items(1), MyComboBoxItem).Content.ToString <> HighestVersion Then '0 是全部
                TextSearchVersion.Items.Insert(1, New MyComboBoxItem With {.Content = HighestVersion})
            End If
        End If
    End Sub
    Private Function LoaderInput() As ResourceSearcher.SearchRequest
        Dim Request As New ResourceSearcher.SearchRequest(PageType, Storage, (Page + 1) * PAGE_SIZE)
        Request.Sort = CType(Val(CType(ComboSearchSort.SelectedItem, MyComboBoxItem).Tag), ResourceSearcher.ResourceSort)
        Dim GameVersion As String = If(TextSearchVersion.Text = "全部 (也可自行输入)", Nothing,
            If(TextSearchVersion.Text.Contains(".") OrElse TextSearchVersion.Text.Contains("w"), TextSearchVersion.Text, Nothing))
        With Request
            .SearchText = TextSearchName.Text
            .GameVersion = GameVersion
            .Tag = ComboSearchTag.SelectedItem.Tag
            .ModLoaders = If(PageType = ResourceTypes.Mod, Val(ComboSearchLoader.SelectedItem.Tag), ModLoaders.None)
            .Sources = CType(Val(ComboSearchSource.SelectedItem.Tag), ResourcePlatforms)
        End With
        Return Request
    End Function

#End Region

    Public Storage As New ResourceSearcher.SearchResult
    ''' <summary>
    ''' 每页展示的结果数量。
    ''' </summary>
    Public Const PAGE_SIZE = 40
    Public Page As Integer = 0

    '结果 UI 化
    Private RenderingFromFavorite As Boolean = False
    Private Sub Load_OnFinish()
        If IsFavoriteView AndAlso Not RenderingFromFavorite Then Return
        Try
            Logger.Info($"开始可视化{TypeNameSpaced}列表，已储藏 {Storage.Results.Count} 个结果，当前在第 {Page + 1} 页")
            '列表项
            PanProjects.Children.Clear()
            Dim Index As Integer = Math.Max(0, Math.Min(Page * PAGE_SIZE, Storage.Results.Count - 1))
            Dim CurrentInput As ResourceSearcher.SearchRequest = Loader.Input
            For Each Result In Storage.Results.GetRange(Index, Math.Min(Storage.Results.Count - Index, PAGE_SIZE))
                PanProjects.Children.Add(Result.ToResourceItem(
                    ShowMcVersionDesc:=CurrentInput Is Nothing OrElse CurrentInput.GameVersion Is Nothing,
                    ShowLoaderDesc:=CurrentInput Is Nothing OrElse (CurrentInput.ModLoaders = ModLoaders.None AndAlso (PageType = ResourceTypes.Mod OrElse PageType = ResourceTypes.ModPack))))
            Next
            '页码
            CardPages.Visibility = If(Storage.Results.Count > 40 OrElse
                                      Storage.CurseForgeOffset < Storage.CurseForgeTotal OrElse Storage.ModrinthOffset < Storage.ModrinthTotal,
                                      Visibility.Visible, Visibility.Collapsed)
            LabPage.Text = Page + 1
            BtnPageFirst.IsEnabled = Page > 1
            BtnPageFirst.Opacity = If(Page > 1, 1, 0.2)
            BtnPageLeft.IsEnabled = Page > 0
            BtnPageLeft.Opacity = If(Page > 0, 1, 0.2)
            Dim IsRightEnabled As Boolean = '由于 WPF 的未知 bug，读取到的 IsEnabled 可能是错误的值（#3319）
                Storage.Results.Count > PAGE_SIZE * (Page + 1) OrElse
                Storage.CurseForgeOffset < Storage.CurseForgeTotal OrElse Storage.ModrinthOffset < Storage.ModrinthTotal
            BtnPageRight.IsEnabled = IsRightEnabled
            BtnPageRight.Opacity = If(IsRightEnabled, 1, 0.2)
            '错误信息
            If Storage.ErrorMessage Is Nothing Then
                HintError.Visibility = Visibility.Collapsed
            Else
                HintError.Visibility = Visibility.Visible
                HintError.Text = Storage.ErrorMessage
            End If
            '强制返回顶部
            ScrollToTop()
        Catch ex As Exception
            Logger.Error(ex, $"可视化{TypeNameSpaced}列表出错")
        End Try
    End Sub

    '自动重试
    Private Sub Load_State(sender As Object, state As MyLoading.MyLoadingState, oldState As MyLoading.MyLoadingState) Handles Load.StateChanged
        Select Case Loader.State
            Case LoadState.Failed
                Dim ErrorMessage As String = ""
                If Loader.Error IsNot Nothing Then ErrorMessage = Loader.Error.Message
                If ErrorMessage.Contains("不是有效的 json 文件") Then
                    Logger.Warn($"下载的{TypeNameSpaced}列表 json 文件损坏，已自动重试")
                    CType(Parent, MyPageRight).PageLoaderRestart()
                End If
        End Select
    End Sub

    '切换页码
    Private Sub BtnPageFirst_Click(sender As Object, e As RoutedEventArgs) Handles BtnPageFirst.Click
        ChangePage(0)
    End Sub
    Private Sub BtnPageLeft_Click(sender As Object, e As RoutedEventArgs) Handles BtnPageLeft.Click
        ChangePage(Page - 1)
    End Sub
    Private Sub BtnPageRight_Click(sender As Object, e As RoutedEventArgs) Handles BtnPageRight.Click
        ChangePage(Page + 1)
    End Sub
    Private Sub ChangePage(NewPage As Integer)
        CardPages.IsEnabled = False
        Page = NewPage
        FrmMain.BackToTop()
        Logger.Info($"{TypeName}：切换到第 {Page + 1} 页")
        RunInThread(
        Sub()
            Thread.Sleep(100) '等待向上滚的动画结束
            RunInUi(Sub() CardPages.IsEnabled = True)
            Loader.Start()
        End Sub)
    End Sub

#Region "搜索"

    Private IsFavoriteView As Boolean = False
    Private Sub BtnFavoriteView_Click(sender As Object, e As RoutedEventArgs) Handles BtnFavoriteView.Click
        If IsFavoriteView Then
            IsFavoriteView = False
            BtnFavoriteView.Text = "收藏夹"
            Page = 0
            Storage = New ResourceSearcher.SearchResult
            CType(Parent, MyPageRight).PageLoaderRestart()
        Else
            IsFavoriteView = True
            BtnFavoriteView.Text = "返回搜索"
            ShowFavorites()
        End If
    End Sub
    Private Sub ShowFavorites()
        Storage.Results.Clear()
        Storage.CurseForgeOffset = 0 : Storage.CurseForgeTotal = 0
        Storage.ModrinthOffset = 0 : Storage.ModrinthTotal = 0
        Storage.ErrorMessage = Nothing
        CardPages.Visibility = Visibility.Collapsed
        PanLoad.Visibility = Visibility.Visible
        Load.Visibility = Visibility.Visible
        RunInNewThread(
            Sub()
                Dim Projects As List(Of ResourceProject) = Nothing
                Try
                    Projects = ModFavorites.GetProjects(PageType)
                Catch ex As Exception
                    Logger.Warn(ex, "获取收藏列表失败")
                End Try
                RunInUi(
                    Sub()
                        Storage.Results.Clear()
                        If Projects IsNot Nothing Then Storage.Results.AddRange(Projects)
                        If Not Storage.Results.Any() Then Storage.ErrorMessage = $"收藏夹中还没有{TypeName}，点击资源条右侧的星标即可收藏。"
                        CardPages.Visibility = Visibility.Collapsed
                        PanLoad.Visibility = Visibility.Collapsed
                        Load.Visibility = Visibility.Collapsed
                        RenderingFromFavorite = True
                        Try
                            Load_OnFinish()
                        Finally
                            RenderingFromFavorite = False
                        End Try
                    End Sub)
            End Sub, "Favorite View", ThreadPriority.BelowNormal)
    End Sub
    '搜索按钮
    Private Sub StartNewSearch() Handles BtnSearchRun.Click
        If PclOnlyMode Then
            LoadPclUploads(True)
            Exit Sub
        End If
        If ComboSearchSource.SelectedItem IsNot Nothing AndAlso Val(ComboSearchSource.SelectedItem.Tag) = 4 Then
            ApplyPclOnly()
        End If
        If IsFavoriteView Then
            IsFavoriteView = False
            BtnFavoriteView.Text = "收藏夹"
            Page = 0
            Storage = New ResourceSearcher.SearchResult
            CType(Parent, MyPageRight).PageLoaderRestart()
            Return
        End If
        Page = 0
        If Loader.ShouldStart(LoaderInput()) Then Storage = New ResourceSearcher.SearchResult '避免连续搜索两次导致 ProjectStorage 引用丢失（#1311）
        Loader.Start()
    End Sub
    Private Sub EnterTrigger(sender As Object, e As KeyEventArgs) Handles TextSearchName.KeyDown, TextSearchVersion.KeyDown
        If e.Key = Key.Enter Then StartNewSearch()
    End Sub

    '改排序立即刷新
    Private Sub ComboSearchSort_SelectionChanged() Handles ComboSearchSort.SelectionChanged
        If HasLoadedOnce AndAlso Not IsFavoriteView Then StartNewSearch()
    End Sub

    '重置按钮
    Private Sub ResetFilter() Handles BtnSearchReset.Click
        Dim WasFavoriteView As Boolean = IsFavoriteView
        If IsFavoriteView Then
            IsFavoriteView = False
            BtnFavoriteView.Text = "收藏夹"
        End If
        TextSearchName.Text = ""
        TextSearchVersion.Text = "全部 (也可自行输入)"
        TextSearchVersion.SelectedIndex = 0
        If PclOnlyMode Then
            PclOnlyMode = False
            PanPclUploads.Visibility = Visibility.Collapsed
            PanContent.Visibility = Visibility.Visible
        End If
        ComboSearchSource.SelectedIndex = 0
        ComboSearchTag.SelectedIndex = 0
        ComboSearchLoader.SelectedIndex = 0
        ComboSearchSort.SelectedIndex = 0
        Loader.LastFinishedTime = 0 '要求强制重新开始
        If WasFavoriteView Then
            Page = 0
            Storage = New ResourceSearcher.SearchResult
            CType(Parent, MyPageRight).PageLoaderRestart()
        End If
    End Sub

    '版本选择
    '#3067：当下拉菜单展开时，程序会被 WPF 挂起，因而无法更新 Grid 布局，所以必须延迟到下拉菜单收起后才能更新
    Private Sub TextSearchVersion_TextChanged() Handles TextSearchVersion.TextChanged
        If Not TextSearchVersion.IsDropDownOpen Then UpdateSearchLoaderVisibility()
    End Sub
    Private Sub UpdateSearchLoaderVisibility() Handles TextSearchVersion.DropDownClosed
        If PageType = ResourceTypes.Mod AndAlso (TextSearchVersion.Text.Contains(".") OrElse TextSearchVersion.Text.Contains("w")) Then
            ComboSearchLoader.Visibility = Visibility.Visible
            Grid.SetColumnSpan(TextSearchVersion, 1)
        Else
            ComboSearchLoader.Visibility = Visibility.Collapsed
            Grid.SetColumnSpan(TextSearchVersion, 2)
            ComboSearchLoader.SelectedIndex = 0
        End If
    End Sub

#End Region

    '安装已有整合包按钮
    Private Sub BtnSearchInstallModPack_Click(sender As Object, e As EventArgs) Handles BtnSearchInstallModPack.Click
        ModpackInstall()
    End Sub


    Private Sub LoadPclUploads(Optional Force As Boolean = False)
        PanPclUploads.Title = "PCL-ForK 上传 · " & PclTypeName()
        If Force Then PanPclUploads.Visibility = Visibility.Visible
        Dim Key As String = PclTypeKey()
        If Key.Length = 0 Then Return
        Dim Url As String = ModMain.PclUrl("MkXol2Aes9ZiCLLWagSy0W8frtZjC6TfYwizkipd84Y+H+yPKg7onipUoQ==") & Key
        RunInNewThread(
            Sub()
                Dim Raw As String
                Try
                    Using Wc As New Net.WebClient()
                        Wc.Encoding = Text.Encoding.UTF8
                        Wc.Proxy = Nothing
                        Wc.Headers.Add("User-Agent", "PCL-ForK")
                        Raw = Wc.DownloadString(Url)
                    End Using
                Catch ex As Exception
                    Logger.Warn(ex, "[PCL-ForK] 加载上传列表失败")
                    RunInUi(
                        Sub()
                            PanPclUploads.Visibility = Visibility.Visible
                            PanPclList.Children.Clear()
                            PanPclList.Children.Add(New TextBlock() With {.Text = "读取失败：" & ex.Message, .FontSize = 12, .TextWrapping = TextWrapping.Wrap, .Opacity = 0.7})
                        End Sub)
                    Return
                End Try
                RunInUi(Sub() RenderPclList(Raw, Key, Force))
            End Sub, "加载 PCL 资源")
    End Sub

    Private Sub RenderPclList(Raw As String, Key As String, Force As Boolean)
        Try
            Dim Items As System.Text.RegularExpressions.MatchCollection =
                System.Text.RegularExpressions.Regex.Matches(Raw, "\{[^{}]*?""source"":""PCL-ForK""[^{}]*?\}")
            Logger.Info("[PclUpload] " & Key & " items=" & Items.Count)
            PanPclList.Children.Clear()
            If Items.Count = 0 Then
                PanPclList.Children.Add(New TextBlock() With {.Text = "还没有人上传过这类资源，去左侧「上传」页发第一个吧。", .FontSize = 12.5, .Opacity = 0.6})
                PanPclUploads.Visibility = Visibility.Visible
                Return
            End If
            For Each M As System.Text.RegularExpressions.Match In Items
                Dim S As String = M.Value
                If PclJsonField(S, "type") <> Key Then Continue For
                Dim ItemName As String = PclJsonField(S, "name")
                Dim ItemDesc As String = PclJsonField(S, "desc")
                Dim ItemUrl As String = PclJsonField(S, "url").Replace("\/", "/")
                Dim ItemSize As String = PclJsonField(S, "size")
                Dim InfoText As String = "来源 PCL-ForK"
                Dim SizeNum As Long = 0
                If Long.TryParse(ItemSize, SizeNum) AndAlso SizeNum > 0 Then InfoText &= " · " & (SizeNum / 1024.0 / 1024.0).ToString("0.0") & " MB"
                If ItemDesc.Length > 0 Then InfoText &= " · " & ItemDesc
                Dim TagList As New List(Of String)
                If ItemDesc.Length > 0 Then
                    For Each Seg In ItemDesc.Split("·"c)
                        Dim Tg As String = Seg.Trim()
                        If Tg.Length > 0 AndAlso TagList.Count < 3 Then TagList.Add(Tg)
                    Next
                End If
                Dim Row As New MyResourceItem With {
                    .Title = ItemName,
                    .SubTitle = "",
                    .Description = ItemDesc,
                    .Tags = TagList,
                    .Height = 76,
                    .Margin = New Thickness(0, 0, 0, 1),
                    .SkipDefaultClick = True
                }
                Row.PathLogo.Visibility = Visibility.Collapsed
                Dim LogoHost = TryCast(Row.PathLogo.Parent, FrameworkElement)
                If LogoHost IsNot Nothing Then LogoHost.Visibility = Visibility.Collapsed
                If Row.ColumnDefinitions.Count >= 3 Then
                    Row.ColumnDefinitions(0).Width = New GridLength(0)
                    Row.ColumnDefinitions(1).Width = New GridLength(0)
                    Row.ColumnDefinitions(2).Width = New GridLength(0)
                End If
                Row.LabDownload.Text = If(SizeNum > 0, (SizeNum / 1048576.0).ToString("0.0") & " MB", "未知")
                Row.LabTime.Text = ""
                Row.LabSource.Text = "pixelmap"
                Row.LabFavorite.Visibility = Visibility.Collapsed
                Dim CUrl As String = PclJsonField(S, "cover")
                Dim IExt As Boolean = S.Contains("""external"":true") OrElse S.Contains("""external"": true")
                Dim INm As String = ItemName
                Dim IU As String = ItemUrl
                Dim IDesc As String = ItemDesc
                AddHandler Row.Click, Sub(Sender2, E2)
                                         Try
                                             Dim Frm As New FrmMapPreview(INm, CUrl, "来源 PCL-ForK · " & IDesc, IU, Key, IExt, Sub() DownloadPclItem(IU, Key, INm))
                                             If FrmMain IsNot Nothing Then Frm.Owner = FrmMain
                                             Frm.ShowDialog()
                                         Catch ex As Exception
                                             Logger.Warn(ex, "打开预览失败，直接下载")
                                             DownloadPclItem(IU, Key, INm)
                                         End Try
                                     End Sub
                PanPclList.Children.Add(Row)
            Next
            PanPclUploads.Visibility = Visibility.Visible
            WarmupPreviewWindow()
        Catch Ex As Exception
            Logger.Warn(Ex, "[PCL-ForK] 渲染上传列表失败")
            PanPclUploads.Visibility = Visibility.Visible
            PanPclList.Children.Clear()
            PanPclList.Children.Add(New TextBlock() With {.Text = "读取失败：" & Ex.Message, .FontSize = 12, .TextWrapping = TextWrapping.Wrap, .Opacity = 0.7})
        End Try
    End Sub

    ''' <summary>
    ''' 预热预览窗，避免第一次点开地图时因窗口模板首次加载而卡顿。
    ''' </summary>
    Private Sub WarmupPreviewWindow()
        If _PreviewWarmed Then Return
        _PreviewWarmed = True
        RunInNewThread(
            Sub()
                Threading.Thread.Sleep(600)
                RunInUi(
                    Sub()
                        Try
                            Dim Frm As New FrmMapPreview("", "", "", "", "", False)
                            Frm.ShowInTaskbar = False
                            Frm.Opacity = 0
                            Frm.WindowStartupLocation = WindowStartupLocation.Manual
                            Frm.Left = -20000
                            Frm.Top = -20000
                            Frm.Show()
                            Frm.Close()
                        Catch ex As Exception
                            Logger.Warn(ex, "预热预览窗失败")
                        End Try
                    End Sub)
            End Sub, "Preview Warmup")
    End Sub
    Private _PreviewWarmed As Boolean = False

    Private Function PclJsonField(Text As String, Field As String) As String
        Dim M = System.Text.RegularExpressions.Regex.Match(Text, """" & Field & """\s*:\s*""([^""]*)""")
        Return If(M.Success, M.Groups(1).Value, "")
    End Function

    Private Function PclGameRoot() As String
        Try
            Dim Sel As String = McFolderSelected
            If Sel IsNot Nothing AndAlso Sel.Trim().Length > 0 Then Return Sel.TrimEnd("\"c)
        Catch
        End Try
        Try
            If McFolderList IsNot Nothing AndAlso McFolderList.Count > 0 Then
                Dim Loc As String = McFolderList(0).Location
                If Loc IsNot Nothing AndAlso Loc.Length > 0 Then Return Loc.TrimEnd("\"c)
            End If
        Catch
        End Try
        Return IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft")
    End Function

    Private Function PclSubFolder(Key As String) As String
        Select Case Key
            Case "mod" : Return "mods"
            Case "resourcepack" : Return "resourcepacks"
            Case "shader" : Return "shaderpacks"
            Case "datapack" : Return "datapacks"
          Case "map" : Return "modpacks"
            Case "pack" : Return "modpacks"
            Case Else : Return "PCL"
        End Select
    End Function

    Public Sub DownloadPclItem(Url As String, Key As String, Name As String)
                '非直链资源：打开原站页面
        Dim LowUrl As String = Url.ToLower()
        Dim IsFile As Boolean = LowUrl.EndsWith(".zip") OrElse LowUrl.EndsWith(".rar") OrElse LowUrl.EndsWith(".7z") OrElse LowUrl.EndsWith(".mcworld") OrElse LowUrl.EndsWith(".mcpack") OrElse LowUrl.EndsWith(".jar")
        If Url.Length > 0 AndAlso Not IsFile Then
            Try
                Process.Start(New ProcessStartInfo(Url) With {.UseShellExecute = True})
                Hint("已在浏览器打开资源页面，请从原站下载", HintType.Blue)
            Catch ex As Exception
                Hint("打开页面失败：" & ex.Message, HintType.Red)
            End Try
            Return
        End If
If Url.Length = 0 Then
            Hint("这条资源没有下载地址", HintType.Red)
            Return
        End If
        Dim Folder As String = IO.Path.Combine(PclGameRoot(), PclSubFolder(Key))
        Dim FileName As String = ""
        Try
            FileName = IO.Path.GetFileName(New Uri(Url).AbsolutePath)
        Catch
        End Try
        If FileName.Length = 0 Then FileName = Name & ".jar"
        Dim Target As String = IO.Path.Combine(Folder, FileName)
        Hint("开始下载 " & Name, HintType.Blue)
        RunInNewThread(
            Sub()
                Try
                    IO.Directory.CreateDirectory(Folder)
                    Net.ServicePointManager.SecurityProtocol = Net.SecurityProtocolType.Tls12 Or Net.SecurityProtocolType.Tls11 Or Net.SecurityProtocolType.Tls
                    Net.ServicePointManager.SecurityProtocol = Net.SecurityProtocolType.Tls12 Or Net.SecurityProtocolType.Tls11 Or Net.SecurityProtocolType.Tls
                    '交给 PCL 多线程下载引擎，进度显示在右下角任务管理器
                    '交给 PCL 多线程下载引擎，任务挂到右下角任务管理器
                    Dim DlTask As New LoaderDownload("地图下载 " & Name, New List(Of NetFile) From {New NetFile({Url}, Target)})
                    Dim DlCombo As New LoaderCombo(Of Integer)("地图下载 " & Name, {DlTask})
                    LoaderTaskbarAdd(DlCombo)
                    FrmMain.BtnExtraDownload.ShowRefresh()
                    DlCombo.Start(0)
                    DlCombo.WaitForExit()
                    LoaderTaskbar.Remove(DlCombo)
                    FrmMain.BtnExtraDownload.ShowRefresh()
                    Logger.Info("[PclUpload] 已下载 " & Target)
                    RunInUi(Sub() Hint("已下载到 " & Target, HintType.Green))
                Catch Ex As Exception
                    Logger.Warn(Ex, "[PCL-ForK] 下载资源失败")
                    RunInUi(Sub() Hint("下载失败：" & Ex.Message, HintType.Red))
                End Try
            End Sub, "下载资源")
    End Sub

    Private Sub OpenPclUrl(Url As String)
        Try
            Process.Start(New ProcessStartInfo(Url) With {.UseShellExecute = True})
        Catch
        End Try
    End Sub

    Private Sub ApplyPclOnly()
        PclOnlyMode = True
        PanContent.Visibility = Visibility.Collapsed
        PanContent.MaxHeight = 0
        PanContent.Height = 0
        PanContent.Opacity = 0
        PanLoad.Visibility = Visibility.Collapsed
        PanLoad.Opacity = 0
        If ComboSearchSource.Items.Count > 3 Then ComboSearchSource.SelectedIndex = 3
        LoadPclUploads(True)
    End Sub


    Private Function PclTypeKey() As String
        Select Case PageType.ToString()
            Case "Shader"
                Return "shader"
            Case "ResourcePack"
                Return "resourcepack"
            Case "DataPack"
                Return "datapack"
            Case "Mod"
                Return "mod"
            Case "Map"
                Return "map"
            Case "ModPack", "Pack"
                Return "pack"
            Case Else
                Return ""
        End Select
    End Function


    Private Function PclTypeName() As String
        Select Case PclTypeKey()
            Case "shader"
                Return "光影包"
            Case "resourcepack"
                Return "资源包"
            Case "datapack"
                Return "数据包"
            Case "mod"
                Return "Mod"
            Case "pack"
                Return "整合包"
            Case "map"
                Return "地图"
            Case Else
                Return "资源"
        End Select
    End Function

End Class
