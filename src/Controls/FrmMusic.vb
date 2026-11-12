Imports System.Windows.Threading
Imports System.Windows.Shapes

Public Class FrmMusic
    Inherits Window

    Private ReadOnly SearchBox As New TextBox
    Private ReadOnly LabAccount As New TextBlock
    Private ReadOnly ListSongs As New ListBox
    Private ReadOnly LyricsPanel As New StackPanel
    Private ReadOnly LyricsScroll As New ScrollViewer
    Private ReadOnly LabStatus As New TextBlock
    Private ReadOnly BtnPlay As New MyButton
    Private ReadOnly SliderProgress As New Slider
    Private ReadOnly LabTime As New TextBlock
    Private ReadOnly Player As New MediaPlayer
    Private ReadOnly Timer As New DispatcherTimer
    Private ReadOnly CurrentList As New List(Of ModNeteaseMusic.NeteaseSong)
    Private CurrentSong As ModNeteaseMusic.NeteaseSong
    Private CurrentLyric As New List(Of ModNeteaseMusic.NeteaseLyricLine)
    Private CurrentLine As Integer = -1
    Private IsPlaying As Boolean = False

    Private Shared ReadOnly GrayBrush As New SolidColorBrush(Color.FromRgb(120, 126, 134))
    Private Shared ReadOnly AccentBrush As New SolidColorBrush(Color.FromRgb(236, 65, 65))

    Public Sub New()
        Title = "网易云音乐"
        Width = 940
        Height = 640
        MinWidth = 720
        MinHeight = 480
        WindowStartupLocation = WindowStartupLocation.CenterScreen
        Background = New SolidColorBrush(Color.FromRgb(247, 248, 250))
        FontFamily = New FontFamily("Microsoft YaHei UI")

        Dim BackGrid As New Grid
        Content = BackGrid
        Dim BackgroundBrush As Brush = Nothing
        Try
            If FrmMain IsNot Nothing AndAlso FrmMain.ImgBack.Background IsNot Nothing Then
                BackgroundBrush = FrmMain.ImgBack.Background
            End If
        Catch ex As Exception
        End Try
        If BackgroundBrush Is Nothing Then
            Dim BackgroundPath As String = Settings.Get(Of String)("UiBackgroundImage")
            If BackgroundPath <> "" AndAlso FileUtils.Exists(BackgroundPath) Then
                Try
                    BackgroundBrush = New MyBitmap(BackgroundPath)
                Catch ex As Exception
                End Try
            End If
        End If
        If BackgroundBrush IsNot Nothing Then
            Dim BlurRadius As Double = Math.Max(Settings.Get(Of Integer)("UiBackgroundBlur") + 1, 20)
            Dim BackRect As New Shapes.Rectangle With {.Fill = BackgroundBrush, .IsHitTestVisible = False}
            BackRect.Opacity = Settings.Get(Of Integer)("UiBackgroundOpacity") / 1000
            BackRect.Effect = New Effects.BlurEffect With {.Radius = BlurRadius}
            BackRect.Margin = New Thickness(-BlurRadius / 1.8)
            BackGrid.Children.Add(BackRect)
        ElseIf FrmMain IsNot Nothing AndAlso FrmMain.PanForm.Background IsNot Nothing Then
            BackGrid.Children.Add(New Shapes.Rectangle With {.Fill = FrmMain.PanForm.Background})
        Else
            BackGrid.Children.Add(New Shapes.Rectangle With {.Fill = New SolidColorBrush(Color.FromRgb(243, 245, 248))})
        End If
        BackGrid.Children.Add(New Shapes.Rectangle With {
            .Fill = New LinearGradientBrush(Color.FromArgb(178, 255, 255, 255), Color.FromArgb(132, 255, 255, 255), 90),
            .IsHitTestVisible = False})

        Dim Root As New Grid With {.Margin = New Thickness(14)}
        Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Root.RowDefinitions.Add(New RowDefinition)
        Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        BackGrid.Children.Add(Root)

        Dim TopBar As New Grid With {.Margin = New Thickness(0, 0, 0, 10)}
        For i = 0 To 4
            TopBar.ColumnDefinitions.Add(New ColumnDefinition With {.Width = If(i = 0, New GridLength(1, GridUnitType.Star), GridLength.Auto)})
        Next
        SearchBox.Height = 30
        SearchBox.Padding = New Thickness(0)
        SearchBox.FontSize = 13
        SearchBox.Background = Brushes.Transparent
        SearchBox.BorderThickness = New Thickness(0)
        SearchBox.Foreground = New SolidColorBrush(Color.FromRgb(56, 60, 66))
        SearchBox.VerticalContentAlignment = VerticalAlignment.Center
        Dim SearchBoxHint As New TextBlock With {
            .Text = "搜索歌曲、歌手或专辑…", .FontSize = 12.5, .Foreground = New SolidColorBrush(Color.FromRgb(150, 156, 164)),
            .VerticalAlignment = VerticalAlignment.Center, .IsHitTestVisible = False, .Margin = New Thickness(2, 0, 0, 0)}
        Dim SearchIcon As New Path With {
            .Data = Geometry.Parse("M909.6 854.5 776.8 722c58.3-68.4 93.2-157.1 93.2-254 0-216.9-176.1-393-393-393S84 251.1 84 468s176.1 393 393 393c97.5 0 186.9-35.6 255.6-94.5l132.8 132.3c7.9 7.9 20.7 7.9 28.6 0l15.6-15.6c7.9-7.9 7.9-20.7 0-28.7zM477 794c-180.1 0-326-145.9-326-326s145.9-326 326-326 326 145.9 326 326-145.9 326-326 326z"),
            .Fill = New SolidColorBrush(Color.FromRgb(140, 146, 154)), .Width = 14, .Height = 14, .Stretch = Stretch.Uniform,
            .Margin = New Thickness(11, 0, 9, 0), .VerticalAlignment = VerticalAlignment.Center}
        Dim SearchInner As New Grid
        SearchInner.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        SearchInner.ColumnDefinitions.Add(New ColumnDefinition)
        SearchInner.Children.Add(SearchIcon)
        Grid.SetColumn(SearchBox, 1)
        SearchInner.Children.Add(SearchBox)
        Grid.SetColumn(SearchBoxHint, 1)
        SearchInner.Children.Add(SearchBoxHint)
        AddHandler SearchBox.TextChanged, Sub() SearchBoxHint.Visibility = If(SearchBox.Text = "", Visibility.Visible, Visibility.Collapsed)
        Dim SearchBorder As New Border With {
            .Height = 32, .CornerRadius = New CornerRadius(9), .Background = New SolidColorBrush(Color.FromArgb(225, 255, 255, 255)),
            .BorderBrush = New SolidColorBrush(Color.FromArgb(130, 255, 255, 255)), .BorderThickness = New Thickness(1), .Child = SearchInner,
            .VerticalAlignment = VerticalAlignment.Center}
        TopBar.Children.Add(SearchBorder)
        Dim BtnSearch As New MyButton With {.Text = "搜索", .Height = 30, .Padding = New Thickness(16, 0, 16, 0), .Margin = New Thickness(8, 0, 0, 0), .ColorType = MyButton.ColorState.Highlight}
        Grid.SetColumn(BtnSearch, 1)
        TopBar.Children.Add(BtnSearch)
        Dim BtnFav As New MyButton With {.Text = "我的收藏", .Height = 30, .Padding = New Thickness(14, 0, 14, 0), .Margin = New Thickness(8, 0, 0, 0)}
        Grid.SetColumn(BtnFav, 2)
        TopBar.Children.Add(BtnFav)
        Dim BtnLogin As New MyButton With {.Text = "登录", .Height = 30, .Padding = New Thickness(14, 0, 14, 0), .Margin = New Thickness(8, 0, 0, 0)}
        Grid.SetColumn(BtnLogin, 3)
        TopBar.Children.Add(BtnLogin)
        LabAccount.Text = "未登录"
        LabAccount.VerticalAlignment = VerticalAlignment.Center
        LabAccount.Margin = New Thickness(10, 0, 0, 0)
        LabAccount.Foreground = GrayBrush
        LabAccount.FontSize = 12
        Grid.SetColumn(LabAccount, 4)
        TopBar.Children.Add(LabAccount)
        Grid.SetRow(TopBar, 0)
        Root.Children.Add(TopBar)

        Dim ContentGrid As New Grid
        ContentGrid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(330)})
        ContentGrid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(12)})
        ContentGrid.ColumnDefinitions.Add(New ColumnDefinition)
        Grid.SetRow(ContentGrid, 1)
        Root.Children.Add(ContentGrid)
        Dim ItemTemplateXaml As String = "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" &
            "<Grid Margin='10,7,10,7'><Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition/></Grid.ColumnDefinitions>" &
            "<Border Width='32' Height='32' CornerRadius='16' Background='#1F4C8DFF'>" &
            "<TextBlock Text='&#9834;' FontSize='15' Foreground='#5B8DEF' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" &
            "<StackPanel Grid.Column='1' Margin='10,0,0,0' VerticalAlignment='Center'>" &
            "<TextBlock Text='{Binding Name}' FontSize='13' Foreground='#333A42' TextTrimming='CharacterEllipsis'/>" &
            "<TextBlock Text='{Binding Artists}' FontSize='11' Foreground='#98A0A8' TextTrimming='CharacterEllipsis' Margin='0,2,0,0'/>" &
            "</StackPanel></Grid></DataTemplate>"
        Dim ItemStyleXaml As String = "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ListBoxItem}'>" &
            "<Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Margin' Value='4,1,4,1'/>" &
            "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ListBoxItem}'>" &
            "<Border x:Name='Bd' CornerRadius='8' Background='Transparent'><ContentPresenter/></Border>" &
            "<ControlTemplate.Triggers>" &
            "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Bd' Property='Background' Value='#143A3F46'/></Trigger>" &
            "<Trigger Property='IsSelected' Value='True'><Setter TargetName='Bd' Property='Background' Value='#2E4C8DFF'/></Trigger>" &
            "</ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"
        ListSongs.FontSize = 13
        ListSongs.Margin = New Thickness(4)
        ListSongs.Background = Brushes.Transparent
        ListSongs.BorderThickness = New Thickness(0)
        ListSongs.Foreground = New SolidColorBrush(Color.FromRgb(56, 60, 66))
        ListSongs.ItemTemplate = CType(System.Windows.Markup.XamlReader.Parse(ItemTemplateXaml), DataTemplate)
        ListSongs.ItemContainerStyle = CType(System.Windows.Markup.XamlReader.Parse(ItemStyleXaml), Style)
        Dim ListBorder As New Border With {
            .CornerRadius = New CornerRadius(10), .Background = New SolidColorBrush(Color.FromArgb(190, 255, 255, 255)),
            .BorderBrush = New SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), .BorderThickness = New Thickness(1), .Child = ListSongs}
        Grid.SetColumn(ListBorder, 0)
        ContentGrid.Children.Add(ListBorder)
        LyricsScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        LyricsScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        LyricsScroll.Padding = New Thickness(10)
        LyricsScroll.Content = LyricsPanel
        Grid.SetColumn(LyricsScroll, 2)
        ContentGrid.Children.Add(LyricsScroll)
        LyricsPanel.Children.Add(New TextBlock With {
            .Text = "搜索并双击歌曲开始播放", .Foreground = GrayBrush, .FontSize = 14,
            .HorizontalAlignment = HorizontalAlignment.Center, .Margin = New Thickness(0, 30, 0, 0)})

        Dim Bottom As New StackPanel With {.Margin = New Thickness(0, 10, 0, 0)}
        Grid.SetRow(Bottom, 2)
        Root.Children.Add(Bottom)
        Dim Ctrl As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        Bottom.Children.Add(Ctrl)
        Dim BtnLike As New MyButton With {.Text = "收藏", .Height = 30, .Padding = New Thickness(14, 0, 14, 0), .Margin = New Thickness(0, 0, 8, 0)}
        Ctrl.Children.Add(BtnLike)
        Dim BtnPrev As New MyButton With {.Text = "上一首", .Height = 30, .Padding = New Thickness(14, 0, 14, 0), .Margin = New Thickness(0, 0, 8, 0)}
        Ctrl.Children.Add(BtnPrev)
        BtnPlay.Text = "播放"
        BtnPlay.Height = 30
        BtnPlay.Padding = New Thickness(20, 0, 20, 0)
        BtnPlay.Margin = New Thickness(0, 0, 8, 0)
        BtnPlay.ColorType = MyButton.ColorState.Highlight
        Ctrl.Children.Add(BtnPlay)
        Dim BtnNext As New MyButton With {.Text = "下一首", .Height = 30, .Padding = New Thickness(14, 0, 14, 0), .Margin = New Thickness(0, 0, 12, 0)}
        Ctrl.Children.Add(BtnNext)
        SliderProgress.Width = 260
        SliderProgress.VerticalAlignment = VerticalAlignment.Center
        Ctrl.Children.Add(SliderProgress)
        LabTime.Text = "00:00 / 00:00"
        LabTime.VerticalAlignment = VerticalAlignment.Center
        LabTime.Margin = New Thickness(10, 0, 14, 0)
        LabTime.Foreground = GrayBrush
        LabTime.FontSize = 12
        Ctrl.Children.Add(LabTime)
        Dim LabVolume As New TextBlock With {.Text = "音量", .VerticalAlignment = VerticalAlignment.Center, .Foreground = GrayBrush, .FontSize = 12}
        Ctrl.Children.Add(LabVolume)
        Dim SliderVolume As New Slider With {.Width = 90, .Minimum = 0, .Maximum = 100, .Value = 70, .Margin = New Thickness(8, 0, 0, 0), .VerticalAlignment = VerticalAlignment.Center}
        Ctrl.Children.Add(SliderVolume)
        LabStatus.Text = "输入关键词搜索歌曲"
        LabStatus.Margin = New Thickness(0, 8, 0, 0)
        LabStatus.Foreground = GrayBrush
        LabStatus.FontSize = 12
        LabStatus.TextWrapping = TextWrapping.Wrap
        Bottom.Children.Add(LabStatus)

        Player.Volume = 0.7
        AddHandler BtnSearch.Click, Sub(sender, e) DoSearch()
        AddHandler SearchBox.KeyDown, Sub(sender, e)
                                         If e.Key = Key.Enter Then DoSearch()
                                     End Sub
        AddHandler BtnLogin.Click, Sub(sender, e) Login()
        AddHandler BtnFav.Click, Sub(sender, e) LoadFavorites()
        AddHandler BtnLike.Click, Sub(sender, e) LikeCurrent()
        AddHandler BtnPlay.Click, Sub(sender, e) TogglePlay()
        AddHandler BtnPrev.Click, Sub(sender, e) PlayPrev()
        AddHandler BtnNext.Click, Sub(sender, e) PlayNext()
        AddHandler SliderVolume.ValueChanged, Sub() Player.Volume = SliderVolume.Value / 100
        AddHandler SliderProgress.PreviewMouseLeftButtonUp, Sub()
                                                               If Player.NaturalDuration.HasTimeSpan Then Player.Position = TimeSpan.FromSeconds(SliderProgress.Value)
                                                           End Sub
        AddHandler ListSongs.MouseDoubleClick, Sub()
                                                   Dim Song = TryCast(ListSongs.SelectedItem, ModNeteaseMusic.NeteaseSong)
                                                   If Song IsNot Nothing Then PlaySong(Song)
                                               End Sub
        AddHandler Player.MediaOpened, Sub()
                                           If Player.NaturalDuration.HasTimeSpan Then SliderProgress.Maximum = Player.NaturalDuration.TimeSpan.TotalSeconds
                                       End Sub
        AddHandler Player.MediaEnded, Sub() PlayNext()
        AddHandler Player.MediaFailed, Sub(sender, e)
                                           LabStatus.Text = "播放失败：可能是 VIP 歌曲、版权受限或网络问题，可登录后重试"
                                           IsPlaying = False
                                           BtnPlay.Text = "播放"
                                       End Sub
        AddHandler Closed, Sub()
                               Timer.Stop()
                               Player.Close()
                           End Sub
        Timer.Interval = TimeSpan.FromMilliseconds(200)
        AddHandler Timer.Tick, Sub() UpdatePlayback()
        Timer.Start()
        RefreshAccount()
    End Sub

    Private Sub DoSearch()
        Dim Keyword As String = SearchBox.Text.Trim
        If Keyword = "" Then Return
        LabStatus.Text = "搜索中……"
        RunInNewThread(
            Sub()
                Try
                    Dim Result As List(Of ModNeteaseMusic.NeteaseSong) = ModNeteaseMusic.SearchMusic(Keyword)
                    RunInUi(
                        Sub()
                            CurrentList.Clear()
                            CurrentList.AddRange(Result)
                            ListSongs.ItemsSource = Nothing
                            ListSongs.ItemsSource = CurrentList
                            LabStatus.Text = $"找到 {Result.Count} 首歌，双击列表播放"
                        End Sub)
                Catch ex As Exception
                    RunInUi(Sub() LabStatus.Text = "搜索失败：" & ex.Message)
                End Try
            End Sub)
    End Sub

    Private Sub PlaySong(Song As ModNeteaseMusic.NeteaseSong)
        If Song Is Nothing Then Return
        CurrentSong = Song
        CurrentLine = -1
        LabStatus.Text = "正在获取播放地址……"
        RunInNewThread(
            Sub()
                Dim Url As String = ModNeteaseMusic.GetSongUrl(Song.Id)
                Dim Lyric As List(Of ModNeteaseMusic.NeteaseLyricLine) = ModNeteaseMusic.GetLyric(Song.Id)
                RunInUi(
                    Sub()
                        If CurrentSong IsNot Song Then Return
                        If String.IsNullOrEmpty(Url) Then
                            LabStatus.Text = $"无法播放：{Song}（可能需要登录或版权受限）"
                            Return
                        End If
                        BuildLyrics(Lyric)
                        Try
                            Player.Open(New Uri(Url))
                            Player.Play()
                            IsPlaying = True
                            BtnPlay.Text = "暂停"
                            SliderProgress.Value = 0
                            LabStatus.Text = $"正在播放：{Song}"
                        Catch ex As Exception
                            LabStatus.Text = "播放失败：" & ex.Message
                        End Try
                    End Sub)
            End Sub)
    End Sub

    Private Sub TogglePlay()
        Dim Selected = TryCast(ListSongs.SelectedItem, ModNeteaseMusic.NeteaseSong)
        If Selected IsNot Nothing AndAlso Not Object.ReferenceEquals(Selected, CurrentSong) Then
            PlaySong(Selected)
            Return
        End If
        If CurrentSong Is Nothing Then
            If Selected IsNot Nothing Then PlaySong(Selected)
            Return
        End If
        If Player.Source Is Nothing Then
            PlaySong(CurrentSong)
            Return
        End If
        If IsPlaying Then
            Player.Pause()
            IsPlaying = False
            BtnPlay.Text = "播放"
            LabStatus.Text = "已暂停"
        Else
            Player.Play()
            IsPlaying = True
            BtnPlay.Text = "暂停"
            LabStatus.Text = $"正在播放：{CurrentSong}"
        End If
    End Sub

    Private Sub PlayPrev()
        If CurrentList.Count = 0 Then Return
        Dim Index As Integer = CurrentList.IndexOf(CurrentSong)
        Dim NextIndex As Integer = If(Index <= 0, CurrentList.Count - 1, Index - 1)
        PlaySong(CurrentList(NextIndex))
    End Sub

    Private Sub PlayNext()
        If CurrentList.Count = 0 Then Return
        Dim Index As Integer = CurrentList.IndexOf(CurrentSong)
        Dim NextIndex As Integer = If(Index < 0 OrElse Index >= CurrentList.Count - 1, 0, Index + 1)
        PlaySong(CurrentList(NextIndex))
    End Sub

    Private Sub BuildLyrics(Lines As List(Of ModNeteaseMusic.NeteaseLyricLine))
        LyricsPanel.Children.Clear()
        CurrentLyric = If(Lines, New List(Of ModNeteaseMusic.NeteaseLyricLine))
        CurrentLine = -1
        If Not CurrentLyric.Any() Then
            LyricsPanel.Children.Add(New TextBlock With {
                .Text = "暂无歌词", .Foreground = GrayBrush, .FontSize = 14,
                .HorizontalAlignment = HorizontalAlignment.Center, .Margin = New Thickness(0, 30, 0, 0)})
            Return
        End If
        For Each Line As ModNeteaseMusic.NeteaseLyricLine In CurrentLyric
            LyricsPanel.Children.Add(New TextBlock With {
                .Text = Line.Text, .Foreground = GrayBrush, .FontSize = 15,
                .HorizontalAlignment = HorizontalAlignment.Center, .TextAlignment = TextAlignment.Center,
                .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(6)})
        Next
    End Sub

    Private Sub UpdatePlayback()
        If CurrentSong Is Nothing Then Return
        If Player.Source IsNot Nothing Then
            If Not SliderProgress.IsMouseCaptureWithin Then SliderProgress.Value = Player.Position.TotalSeconds
            Dim Total As TimeSpan = If(Player.NaturalDuration.HasTimeSpan, Player.NaturalDuration.TimeSpan, TimeSpan.Zero)
            LabTime.Text = $"{FormatTime(Player.Position)} / {FormatTime(Total)}"
        End If
        If CurrentLyric.Count = 0 Then Return
        Dim Position As Double = Player.Position.TotalSeconds
        Dim Index As Integer = -1
        For i = 0 To CurrentLyric.Count - 1
            If CurrentLyric(i).Time <= Position Then Index = i Else Exit For
        Next
        If Index = CurrentLine Then Return
        CurrentLine = Index
        For i = 0 To LyricsPanel.Children.Count - 1
            Dim Line As TextBlock = TryCast(LyricsPanel.Children(i), TextBlock)
            If Line Is Nothing Then Continue For
            If i = Index Then
                Line.FontWeight = FontWeights.Bold
                Line.Foreground = AccentBrush
                Line.FontSize = 16
            Else
                Line.FontWeight = FontWeights.Normal
                Line.Foreground = GrayBrush
                Line.FontSize = 15
            End If
        Next
        If Index >= 0 AndAlso Index < LyricsPanel.Children.Count Then
            Try
                Dim Line As FrameworkElement = TryCast(LyricsPanel.Children(Index), FrameworkElement)
                If Line Is Nothing Then Return
                Dim PosY As Double = Line.TransformToAncestor(LyricsPanel).Transform(New Point(0, 0)).Y
                LyricsScroll.ScrollToVerticalOffset(PosY - LyricsScroll.ViewportHeight / 2 + Line.ActualHeight / 2)
            Catch ex As Exception
            End Try
        End If
    End Sub

    Private Shared Function FormatTime(Value As TimeSpan) As String
        Return $"{CInt(Value.TotalMinutes):00}:{Value.Seconds:00}"
    End Function

    Private Sub Login()
        Dim InputCookie As String = MyMsgBoxInput("登录网易云",
            "粘贴浏览器 Cookie 中的 MUSIC_U 值（登录 music.163.com 后按 F12 → Application → Cookies → music.163.com → 复制 MUSIC_U 的值）。",
            ModNeteaseMusic.CookieValue, HintText:="MUSIC_U=...")
        If InputCookie Is Nothing Then Return
        Dim Value As String = InputCookie.Trim
        If Value = "" Then Return
        If Value.Contains("MUSIC_U=") Then
            Value = Value.Substring(Value.IndexOf("MUSIC_U=") + 8)
            Value = Value.BeforeFirst(";").Trim
        End If
        ModNeteaseMusic.CookieValue = Value
        RefreshAccount()
    End Sub

    Private Sub RefreshAccount()
        LabAccount.Text = "正在验证登录……"
        RunInNewThread(
            Sub()
                Dim Account = ModNeteaseMusic.GetAccount()
                RunInUi(
                    Sub()
                        If Account Is Nothing OrElse Account("profile") Is Nothing Then
                            LabAccount.Text = If(ModNeteaseMusic.CookieValue = "", "未登录", "登录已失效")
                        Else
                            LabAccount.Text = "已登录：" & Account("profile")("nickname")?.ToString()
                        End If
                    End Sub)
            End Sub)
    End Sub

    Private Sub LoadFavorites()
        If ModNeteaseMusic.CookieValue = "" Then
            Hint("请先登录网易云", HintType.Red)
            Return
        End If
        LabStatus.Text = "正在加载收藏……"
        RunInNewThread(
            Sub()
                Dim Songs As List(Of ModNeteaseMusic.NeteaseSong) = ModNeteaseMusic.GetLikedSongs()
                RunInUi(
                    Sub()
                        CurrentList.Clear()
                        CurrentList.AddRange(Songs)
                        ListSongs.ItemsSource = Nothing
                        ListSongs.ItemsSource = CurrentList
                        LabStatus.Text = $"「我喜欢的音乐」共 {Songs.Count} 首歌"
                    End Sub)
            End Sub)
    End Sub

    Private Sub LikeCurrent()
        If CurrentSong Is Nothing Then
            Hint("请先播放一首歌", HintType.Red)
            Return
        End If
        RunInNewThread(
            Sub()
                Dim Ok As Boolean = ModNeteaseMusic.SetLike(CurrentSong.Id, True)
                RunInUi(Sub() LabStatus.Text = If(Ok, "已收藏到「我喜欢的音乐」", "收藏失败：请先登录网易云"))
            End Sub)
    End Sub
End Class
