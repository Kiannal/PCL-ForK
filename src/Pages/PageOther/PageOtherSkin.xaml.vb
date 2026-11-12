Imports Microsoft.Win32
Imports Newtonsoft.Json.Linq
Imports System.Windows.Media.Imaging

Public Class PageOtherSkin

    Private Skins As New List(Of ModSkinLibrary.SkinFile)
    Private SelectedSkin As ModSkinLibrary.SkinFile = Nothing
    Private IsBusy As Boolean = False

    Private OnlineItems As New List(Of ModSkinLibrary.OnlineSkin)
    Private OnlinePage As Integer = 0
    Private OnlineSort As String = "new"
    Private OnlineBusy As Boolean = False

    Public Sub New()
        InitializeComponent()
        AddHandler BtnRefresh.Click, Sub(sender, e) RefreshLibrary()
        AddHandler BtnFetch.Click, Sub(sender, e) FetchSkin()
        AddHandler BtnImport.Click, Sub(sender, e) ImportSkin()
        AddHandler BtnApply.Click, Sub(sender, e) ApplySkin()
        AddHandler BtnReset.Click, Sub(sender, e) ResetSkin()
        AddHandler BtnDelete.Click, Sub(sender, e) DeleteSkin()
        AddHandler BtnOpenFolder.Click, Sub(sender, e) OpenExplorer(ModSkinLibrary.SkinFolder)
        AddHandler BtnLittleSkin.Click, Sub(sender, e)
                                                   Dim Frm As New FrmLittleSkin
                                                   Frm.ShowDialog()
                                               End Sub
        AddHandler BtnOnlineRefresh.Click, Sub(sender, e)
                                               ModSkinLibrary.ClearOnlineCache()
                                               LoadOnline()
                                           End Sub
        AddHandler BtnOnlinePrev.Click, Sub(sender, e)
                                            If OnlinePage > 0 AndAlso Not OnlineBusy Then
                                                OnlinePage -= 1
                                                LoadOnline()
                                            End If
                                        End Sub
        AddHandler BtnOnlineNext.Click, Sub(sender, e)
                                            If Not OnlineBusy Then
                                                OnlinePage += 1
                                                LoadOnline()
                                            End If
                                        End Sub
        AddHandler ComboOnlineSort.SelectionChanged, Sub(sender, e)
                                                         If OnlineBusy Then Return
                                                         OnlineSort = If(CType(ComboOnlineSort.SelectedItem, MyComboBoxItem).Tag.ToString(), "new")
                                                         OnlinePage = 0
                                                         LoadOnline()
                                                     End Sub
    End Sub

    Private Sub PageOtherSkin_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Try
            PanBack.ScrollToHome()
            RefreshLibrary()
            If OnlineItems.Count = 0 Then LoadOnline()
        Catch ex As Exception
            Logger.Warn(ex, "初始化皮肤库失败")
        End Try
    End Sub

    Private Sub BtnSyncOfficial_Click() Handles BtnSyncOfficial.Click
        Try
            If Settings.Get(Of McLoginType)("LoginType") <> McLoginType.Ms Then
                Hint("请先使用正版账号登录", HintType.Red)
                Return
            End If
            Dim Name As String = Settings.Get(Of String)("CacheMsV2Name")
            If String.IsNullOrWhiteSpace(Name) Then
                Hint("还没有正版账号信息，请先登录", HintType.Red)
                Return
            End If
            Hint("正在同步正版皮肤……", HintType.Blue)
            RunInNewThread(
            Sub()
                Dim Err As String = ModSkinLibrary.FetchByName(Name)
                RunInUi(
                Sub()
                    If Err Is Nothing Then
                        Hint("已同步正版皮肤：" & Name, HintType.Green)
                        RefreshLibrary()
                    Else
                        Hint("同步失败：" & Err, HintType.Red)
                    End If
                End Sub)
            End Sub, "Sync Official Skin")
        Catch ex As Exception
            Hint("同步失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Shared ReadOnly OnlineListCache As New Dictionary(Of String, Tuple(Of Date, List(Of ModSkinLibrary.OnlineSkin)))

    Private Sub LoadOnline()
        If OnlineBusy Then Return
        OnlineBusy = True
        BtnOnlinePrev.IsEnabled = False
        BtnOnlineNext.IsEnabled = False
        BtnOnlineRefresh.IsEnabled = False
        LabOnlineStatus.Text = "正在加载在线皮肤库……"
        Dim TargetPage As Integer = OnlinePage
        Dim TargetSort As String = OnlineSort
        Dim CacheKey As String = TargetPage & "|" & TargetSort
        Dim Cached As Tuple(Of Date, List(Of ModSkinLibrary.OnlineSkin)) = Nothing
        If OnlineListCache.TryGetValue(CacheKey, Cached) AndAlso Date.Now - Cached.Item1 < TimeSpan.FromMinutes(3) Then
            OnlineItems = Cached.Item2
            BuildOnlineCards()
            OnlineBusy = False
            BtnOnlineRefresh.IsEnabled = True
            BtnOnlineNext.IsEnabled = True
            BtnOnlinePrev.IsEnabled = OnlinePage > 0
            LabOnlinePage.Text = "第 " & (OnlinePage + 1).ToString() & " 页"
            LabOnlineStatus.Text = $"共 {Cached.Item2.Count} 个皮肤（缓存）"
            Return
        End If
        RunInNewThread(
            Sub()
                Dim Items As List(Of ModSkinLibrary.OnlineSkin) = Nothing
                Dim ErrorText As String = Nothing
                Try
                    Items = ModSkinLibrary.FetchOnlineSkins(TargetPage, TargetSort, 12)
                Catch ex As Exception
                    ErrorText = ex.Message
                End Try
                RunInUi(
                    Sub()
                        OnlineBusy = False
                        BtnOnlineRefresh.IsEnabled = True
                        BtnOnlineNext.IsEnabled = True
                        BtnOnlinePrev.IsEnabled = OnlinePage > 0
                        If Items Is Nothing Then
                            LabOnlineStatus.Text = "加载失败：" & ErrorText
                            Return
                        End If
                        OnlineItems = Items
                        OnlineListCache(CacheKey) = New Tuple(Of Date, List(Of ModSkinLibrary.OnlineSkin))(Date.Now, Items)
                        BuildOnlineCards()
                        LabOnlinePage.Text = "第 " & (OnlinePage + 1).ToString() & " 页"
                        LabOnlineStatus.Text = $"共 {Items.Count} 个皮肤"
                    End Sub)
            End Sub, "SkinLibraryOnline")
    End Sub

    Private Sub BuildOnlineCards()
        PanOnline.Children.Clear()
        For Each Item In OnlineItems
            Dim Card As New Border With {
                .Width = 100, .Height = 122, .Margin = New Thickness(0, 0, 12, 12),
                .CornerRadius = New CornerRadius(8), .BorderThickness = New Thickness(1),
                .Background = Brushes.Transparent, .Cursor = Cursors.Hand,
                .ToolTip = $"{Item.Name}{vbCrLf}作者：{Item.Author}{vbCrLf}点赞：{Item.Likes}{vbCrLf}点击下载到本地皮肤库"}
            Card.SetResourceReference(Border.BorderBrushProperty, "ColorBrushGray6")
            Dim Panel As New StackPanel With {.Margin = New Thickness(6)}
            Dim Head As New Image With {.Width = 58, .Height = 58, .Stretch = Stretch.Uniform}
            RenderOptions.SetBitmapScalingMode(Head, BitmapScalingMode.NearestNeighbor)
            If Not String.IsNullOrEmpty(Item.Hash) Then
                LoadRemoteHead(Item.Hash, Head)
            End If
            Panel.Children.Add(Head)
            Dim NameLabel As New TextBlock With {
                .Text = Item.Name, .FontSize = 11, .Margin = New Thickness(0, 5, 0, 0),
                .TextTrimming = TextTrimming.CharacterEllipsis, .HorizontalAlignment = HorizontalAlignment.Center,
                .MaxWidth = 88}
            Panel.Children.Add(NameLabel)
            Dim InfoLabel As New TextBlock With {
                .Text = $"{If(Item.Model = "alex", "Alex", "Steve")} · ♥{Item.Likes}",
                .FontSize = 10, .Foreground = CType(FindResource("ColorBrushGray3"), Brush),
                .HorizontalAlignment = HorizontalAlignment.Center, .Margin = New Thickness(0, 2, 0, 0)}
            Panel.Children.Add(InfoLabel)
            Card.Child = Panel
            Dim CurrentItem As ModSkinLibrary.OnlineSkin = Item
            AddHandler Card.MouseLeftButtonDown, Sub()
                                                     DownloadOnline(CurrentItem)
                                                 End Sub
            PanOnline.Children.Add(Card)
        Next
    End Sub

    Private Shared ReadOnly RemoteHeadCache As New Dictionary(Of String, BitmapSource)

    Private Sub LoadRemoteHead(Hash As String, Target As Image)
        Try
            If RemoteHeadCache.ContainsKey(Hash) Then
                Target.Source = RemoteHeadCache(Hash)
                Return
            End If
            Dim Bitmap As New BitmapImage()
            Bitmap.BeginInit()
            Bitmap.DecodePixelWidth = 64
            Bitmap.UriSource = New Uri("https://littleskin.cn/textures/" & Hash)
            Bitmap.CacheOption = BitmapCacheOption.OnDemand
            Bitmap.EndInit()
            AddHandler Bitmap.DownloadCompleted, Sub()
                                                     Try
                                                         Dim Result As BitmapSource
                                                         If Bitmap.PixelWidth >= 16 AndAlso Bitmap.PixelHeight >= 16 Then
                                                             Result = New CroppedBitmap(Bitmap, New Int32Rect(8, 8, 8, 8))
                                                         Else
                                                             Result = Bitmap
                                                         End If
                                                         Result.Freeze()
                                                         RemoteHeadCache(Hash) = Result
                                                         Target.Source = Result
                                                     Catch ex As Exception
                                                     End Try
                                                 End Sub
        Catch ex As Exception
        End Try
    End Sub

    Private Sub DownloadOnline(Item As ModSkinLibrary.OnlineSkin)
        If OnlineBusy Then Return
        OnlineBusy = True
        LabOnlineStatus.Text = $"正在下载「{Item.Name}」……"
        RunInNewThread(
            Sub()
                Dim ErrorMessage As String = ModSkinLibrary.DownloadOnlineSkin(Item)
                RunInUi(
                    Sub()
                        OnlineBusy = False
                        If ErrorMessage IsNot Nothing Then
                            LabOnlineStatus.Text = "下载失败：" & ErrorMessage
                            Hint("下载皮肤失败：" & ErrorMessage, HintType.Red)
                            Return
                        End If
                        LabOnlineStatus.Text = "已下载：" & Item.Name
                        RefreshLibrary()
                        Dim Downloaded As ModSkinLibrary.SkinFile =
                            Skins.FirstOrDefault(Function(s) String.Equals(s.Path, ModSkinLibrary.LastDownloadedPath, StringComparison.OrdinalIgnoreCase))
                        If Downloaded IsNot Nothing Then
                            SelectedSkin = Downloaded
                            SelectSkin(Downloaded)
                        End If
                        Hint("皮肤已下载到本地皮肤库", HintType.Green)
                        PanBack.ScrollToVerticalOffset(CardLibrary.TranslatePoint(New Point(0, 0), PanBack).Y)
                    End Sub)
            End Sub, "SkinLibraryDownload")
    End Sub

    Private Sub RefreshLibrary()
        Try
            Skins = ModSkinLibrary.GetSkins()
            PanSkins.Children.Clear()
            LabEmpty.Visibility = If(Skins.Any(), Visibility.Collapsed, Visibility.Visible)
            For Each Skin In Skins
                PanSkins.Children.Add(CreateSkinCard(Skin))
            Next
            Dim Applied As ModSkinLibrary.SkinFile = Skins.FirstOrDefault(Function(s) ModSkinLibrary.IsApplied(s))
            If Applied IsNot Nothing Then
                SelectSkin(Applied)
            ElseIf SelectedSkin IsNot Nothing AndAlso Skins.Contains(SelectedSkin) Then
                SelectSkin(SelectedSkin)
            ElseIf Skins.Any() Then
                SelectSkin(Skins(0))
            Else
                SelectedSkin = Nothing
                ShowPreview(Nothing)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "刷新皮肤库失败")
        End Try
    End Sub

    Private Function CreateSkinCard(Skin As ModSkinLibrary.SkinFile) As UIElement
        Dim Card As New Border With {
            .Width = 82, .Height = 100, .Margin = New Thickness(0, 0, 12, 12),
            .CornerRadius = New CornerRadius(8), .BorderThickness = New Thickness(1),
            .Background = Brushes.Transparent, .Cursor = Cursors.Hand,
            .ToolTip = Skin.Path}
        Card.SetResourceReference(Border.BorderBrushProperty, "ColorBrushGray6")
        Card.SetResourceReference(Border.BackgroundProperty, "ColorBrush7")
        Dim Panel As New StackPanel With {.Margin = New Thickness(6)}
        Dim Head As New Image With {.Width = 56, .Height = 56, .Stretch = Stretch.Uniform}
        RenderOptions.SetBitmapScalingMode(Head, BitmapScalingMode.NearestNeighbor)
        Try
            Head.Source = LoadHead(Skin.Path)
        Catch ex As Exception
        End Try
        Panel.Children.Add(Head)
        Dim Label As New TextBlock With {
            .Text = Skin.Name, .FontSize = 11, .Margin = New Thickness(0, 5, 0, 0),
            .TextTrimming = TextTrimming.CharacterEllipsis, .HorizontalAlignment = HorizontalAlignment.Center}
        Panel.Children.Add(Label)
        Card.Child = Panel
        AddHandler Card.MouseLeftButtonDown, Sub()
                                                 SelectSkin(Skin)
                                             End Sub
        Card.Tag = Skin
        Return Card
    End Function

    Private Function LoadHead(FilePath As String) As BitmapSource
        Dim Image As New BitmapImage()
        Image.BeginInit()
            Image.CacheOption = BitmapCacheOption.OnLoad
        Image.UriSource = New Uri(FilePath)
        Image.EndInit()
        Image.Freeze()
        If Image.PixelWidth >= 16 AndAlso Image.PixelHeight >= 16 Then
            Return New CroppedBitmap(Image, New Int32Rect(8, 8, 8, 8))
        End If
        Return Image
    End Function

    Private Sub SelectSkin(Skin As ModSkinLibrary.SkinFile)
        SelectedSkin = Skin
        For Each Child As UIElement In PanSkins.Children
            Dim Card As Border = TryCast(Child, Border)
            If Card Is Nothing Then Continue For
            If Card.Tag Is Skin Then
                Card.SetResourceReference(Border.BorderBrushProperty, "ColorBrush3")
                Card.BorderThickness = New Thickness(2)
            Else
                Card.SetResourceReference(Border.BorderBrushProperty, "ColorBrushGray6")
                Card.BorderThickness = New Thickness(1)
            End If
        Next
        ShowPreview(Skin)
    End Sub

    Private SkinDragging As Boolean = False
    Private SkinDragX As Double = 0
    Private SkinDragY As Double = 0

    Private Sub ViewSkin_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles ViewSkin.MouseLeftButtonDown
        SkinDragging = True
        SkinDragX = e.GetPosition(ViewSkin).X
        SkinDragY = e.GetPosition(ViewSkin).Y
        ViewSkin.CaptureMouse()
        e.Handled = True
    End Sub

    Private Sub ViewSkin_MouseMove(sender As Object, e As MouseEventArgs) Handles ViewSkin.MouseMove
        If Not SkinDragging Then Return
        Dim NowX As Double = e.GetPosition(ViewSkin).X
        Dim NowY As Double = e.GetPosition(ViewSkin).Y
        ModSkinPreview.RotateBy(NowX - SkinDragX, NowY - SkinDragY)
        SkinDragX = NowX
        SkinDragY = NowY
    End Sub

    Private Sub ViewSkin_MouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles ViewSkin.MouseLeftButtonUp
        SkinDragging = False
        ViewSkin.ReleaseMouseCapture()
    End Sub

    Private Sub ShowPreview(Skin As ModSkinLibrary.SkinFile)
        If Skin Is Nothing Then
            ModSkinPreview.Clear(ViewSkin)
            LabName.Text = "未选择皮肤"
            LabType.Text = "在皮肤库中选择一个皮肤"
            LabPath.Text = ""
            LabApplied.Visibility = Visibility.Collapsed
            Return
        End If
        Try
            Dim Image As New BitmapImage()
            Image.BeginInit()
            Image.CacheOption = BitmapCacheOption.OnLoad
            Image.UriSource = New Uri(Skin.Path)
            Image.EndInit()
            Image.Freeze()
            ModSkinPreview.ShowSkin(Image, Skin.Slim, ViewSkin)
            LabName.Text = Skin.Name
            LabType.Text = $"{If(Skin.Slim, "Alex 模型（细手臂）", "Steve 模型（粗手臂）")} · {Skin.Width}×{Skin.Height}"
            LabPath.Text = Skin.Path
            LabApplied.Visibility = If(ModSkinLibrary.IsApplied(Skin), Visibility.Visible, Visibility.Collapsed)
        Catch ex As Exception
            LabType.Text = "无法读取这个皮肤：" & ex.Message
        End Try
    End Sub

    Private Sub FetchSkin()
        If IsBusy Then Return
        Dim Name As String = MyMsgBoxInput("获取玩家皮肤", "输入正版玩家名（例如 Notch），会从 Mojang 获取该玩家的皮肤：")
        If String.IsNullOrWhiteSpace(Name) Then Return
        IsBusy = True
        RunInNewThread(
            Sub()
                Dim ErrorMessage As String = ModSkinLibrary.FetchByName(Name)
                RunInUi(
                    Sub()
                        IsBusy = False
                        If ErrorMessage Is Nothing Then
                            Hint("已获取皮肤并加入皮肤库", HintType.Green)
                            RefreshLibrary()
                        Else
                            Hint("获取失败：" & ErrorMessage, HintType.Red)
                        End If
                    End Sub)
            End Sub, "SkinLibraryFetch")
    End Sub

    Private Sub ImportSkin()
        Try
            Dim Files As List(Of String) = Dialogs.SelectFile("选择皮肤文件", False, filter:={({"png", "jpg", "jpeg", "webp"}, "皮肤图片")})
            If Files Is Nothing OrElse Files.Count = 0 Then Return
            Dim ErrorMessage As String = ModSkinLibrary.Import(Files(0))
            If ErrorMessage Is Nothing Then
                Hint("皮肤已导入", HintType.Green)
                RefreshLibrary()
            Else
                Hint("导入失败：" & ErrorMessage, HintType.Red)
            End If
        Catch ex As Exception
            Logger.Warn(ex, "导入皮肤失败")
        End Try
    End Sub

    Private Sub ApplySkin()
        If SelectedSkin Is Nothing Then
            Hint("请先在皮肤库中选择一个皮肤", HintType.Blue)
            Return
        End If
        Dim ErrorMessage As String = ModSkinLibrary.Apply(SelectedSkin)
        If ErrorMessage Is Nothing Then
            Hint("已应用为离线皮肤，启动游戏后生效", HintType.Green)
            ShowPreview(SelectedSkin)
        Else
            Hint("应用失败：" & ErrorMessage, HintType.Red)
        End If
    End Sub

    Private Sub ResetSkin()
        If MyMsgBox("确定要恢复默认皮肤吗？", "恢复默认皮肤", "恢复", "取消") <> 1 Then Return
        ModSkinLibrary.ResetSkin()
        Hint("已恢复默认皮肤", HintType.Green)
        ShowPreview(SelectedSkin)
    End Sub

    Private Sub DeleteSkin()
        If SelectedSkin Is Nothing Then
            Hint("请先在皮肤库中选择一个皮肤", HintType.Blue)
            Return
        End If
        If MyMsgBox($"确定要从皮肤库中删除「{SelectedSkin.Name}」吗？", "删除皮肤", "删除", "取消", IsWarn:=True) <> 1 Then Return
        Dim ErrorMessage As String = ModSkinLibrary.Delete(SelectedSkin)
        If ErrorMessage Is Nothing Then
            Hint("已删除", HintType.Green)
            SelectedSkin = Nothing
            RefreshLibrary()
        Else
            Hint("删除失败：" & ErrorMessage, HintType.Red)
        End If
    End Sub








End Class
