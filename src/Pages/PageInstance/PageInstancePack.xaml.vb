Imports System.IO.Compression
Public Class PageInstancePack

    Public Shared Mode As Integer = 0

    Private Sub PageEnterHandler() Handles Me.PageEnter
        ReloadList()
    End Sub

    Private Sub FilterChanged(sender As Object, e As RoutedEventArgs)
        ReloadList()
    End Sub

    Private Sub LoadedHandler(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        RemoveHandler BtnDownload.Click, AddressOf OpenDownload
        AddHandler BtnDownload.Click, AddressOf OpenDownload
        AddHandler BtnFilterAll.Check, Sub(s, ev) ReloadList()
        AddHandler BtnFilterEnabled.Check, Sub(s, ev) ReloadList()
        AddHandler BtnFilterDisabled.Check, Sub(s, ev) ReloadList()
        ReloadList()
    End Sub

    Private ReadOnly Property FolderName As String
        Get
            Select Case Mode
                Case 1 : Return "shaderpacks"
                Case Else : Return "resourcepacks"
            End Select
        End Get
    End Property

    Private Sub ReloadList()
        Try
            If SearchBox IsNot Nothing Then
                RemoveHandler SearchBox.TextChanged, AddressOf SearchTextChanged
                AddHandler SearchBox.TextChanged, AddressOf SearchTextChanged
            End If
            PanPacks.Children.Clear()
            Select Case Mode
                Case 0
                    CardMain.Title = "资源包"
                    BtnDownload.Text = "下载新资源包"
                    BtnInstall.Visibility = Visibility.Visible
                    LabHint.Text = "资源包放在 resourcepacks 文件夹中，文件名后面加 .disabled 可禁用。"
                Case 1
                    CardMain.Title = "光影包"
                    BtnDownload.Text = "下载新光影包"
                    BtnInstall.Visibility = Visibility.Visible
                    LabHint.Text = "光影包放在 shaderpacks 文件夹中，需要安装光影加载器（如 Iris、OptiFine）才能使用。"
                Case 2
                    CardMain.Title = "数据包"
                    BtnDownload.Text = "下载新数据包"
                    BtnInstall.Visibility = Visibility.Collapsed
                    LabHint.Text = "数据包需要放在对应世界存档的 datapacks 文件夹中，下方按世界列出。"
            End Select
            If Mode = 2 Then
                ListDataPacks()
            Else
                ListPackItems()
            End If
            If PanPacks.Children.Count = 0 Then
                PanPacks.Children.Add(New TextBlock With {
                    .Text = If(Mode = 2, "没有找到数据包。请先在游戏中创建世界，数据包需放入世界存档的 datapacks 文件夹。", "还没有安装任何" & If(Mode = 1, "光影包", "资源包") & "。"),
                    .FontSize = 12.5,
                    .Margin = New Thickness(2, 6, 0, 0),
                    .TextWrapping = TextWrapping.Wrap,
                    .Foreground = New SolidColorBrush(Color.FromRgb(130, 136, 144))})
            End If
        Catch ex As Exception
            Logger.Warn(ex, "加载资源包列表失败")
        End Try
    End Sub

    Private Function GetPackFolders() As List(Of String)
        Dim Result As New List(Of String)
        Dim Candidates As New List(Of String)
        Try
            If PageInstanceLeft.Instance IsNot Nothing Then Candidates.Add(PageInstanceLeft.Instance.PathIndie & FolderName)
        Catch ex As Exception
        End Try
        Candidates.Add(McFolderSelected & FolderName)
        For Each PackFolder In Candidates
            If Not String.IsNullOrWhiteSpace(PackFolder) AndAlso Not Result.Contains(PackFolder) Then Result.Add(PackFolder)
        Next
        Return Result
    End Function

    Private Sub ListPackItems()
        For Each PackFolder In GetPackFolders()
            If Not IO.Directory.Exists(PackFolder) Then Continue For
            For Each Entry In IO.Directory.GetFileSystemEntries(PackFolder)
                Dim Name As String = IO.Path.GetFileName(Entry)
                If Name.StartsWithF(".") OrElse Name.EndsWithF(".json") OrElse Name.EndsWithF(".txt") Then Continue For
                AddPackRow(Entry, Name, "")
            Next
        Next
    End Sub

    Private Sub ListDataPacks()
        Dim WorldRoots As New List(Of String)
        Try
            If PageInstanceLeft.Instance IsNot Nothing Then WorldRoots.Add(PageInstanceLeft.Instance.PathIndie & "saves")
        Catch ex As Exception
        End Try
        WorldRoots.Add(McFolderSelected & "saves")
        For Each Root In WorldRoots
            If Not IO.Directory.Exists(Root) Then Continue For
            For Each WorldDir In IO.Directory.GetDirectories(Root)
                Dim DatapackDir As String = IO.Path.Combine(WorldDir, "datapacks")
                If Not IO.Directory.Exists(DatapackDir) Then Continue For
                Dim WorldName As String = IO.Path.GetFileName(WorldDir)
                For Each Entry In IO.Directory.GetFileSystemEntries(DatapackDir)
                    AddPackRow(Entry, IO.Path.GetFileName(Entry), WorldName)
                Next
            Next
        Next
    End Sub

    Private Sub AddPackRow(FullPath As String, RawName As String, WorldName As String)
        Dim Disabled As Boolean = RawName.EndsWithF(".disabled")
        Dim SearchText As String = If(SearchBox Is Nothing, "", SearchBox.Text.Trim())
        If SearchText <> "" AndAlso Not RawName.ToLower().Contains(SearchText.ToLower()) Then Return
        If BtnFilterEnabled IsNot Nothing AndAlso BtnFilterEnabled.Checked AndAlso Disabled Then Return
        If BtnFilterDisabled IsNot Nothing AndAlso BtnFilterDisabled.Checked AndAlso Not Disabled Then Return
        Dim DisplayName As String = If(Disabled, RawName.Substring(0, RawName.Length - 9), RawName)
        Dim Info As String = If(IO.Directory.Exists(FullPath), "文件夹", GetFileSizeText(FullPath))
        If WorldName <> "" Then Info = "世界：" & WorldName & " · " & Info

        Dim Row As New Grid With {.Height = 44, .Margin = New Thickness(0, 0, 0, 2)}
        Row.SetResourceReference(Grid.BackgroundProperty, "ColorBrushSemiTransparent")
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(6)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(34)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(7)})
        Row.ColumnDefinitions.Add(New ColumnDefinition)
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(6)})
        Dim IconBox As New Border With {.Width = 34, .Height = 34, .CornerRadius = New CornerRadius(6), .VerticalAlignment = VerticalAlignment.Center, .Background = New SolidColorBrush(Color.FromRgb(38, 40, 45))}
        Grid.SetColumn(IconBox, 1)
        Row.Children.Add(IconBox)
        Dim PackIcon As ImageSource = LoadPackIcon(FullPath)
        If PackIcon IsNot Nothing Then
            IconBox.Clip = New RectangleGeometry(New Rect(0, 0, 34, 34), 6, 6)
            IconBox.Child = New Image With {.Source = PackIcon, .Stretch = Stretch.UniformToFill, .Width = 34, .Height = 34}
        Else
            IconBox.Visibility = Visibility.Collapsed
            Row.ColumnDefinitions(1).Width = New GridLength(0)
        End If

        Dim TextPanel As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        TextPanel.Children.Add(New TextBlock With {.Text = DisplayName, .FontSize = 14, .TextTrimming = TextTrimming.CharacterEllipsis})
        TextPanel.Children.Add(New TextBlock With {
            .Text = Info & If(Disabled, " · 已禁用", ""),
            .FontSize = 12,
            .Foreground = New SolidColorBrush(Color.FromRgb(122, 130, 142))})
        Grid.SetColumn(TextPanel, 3)
        Row.Children.Add(TextPanel)

        Dim ToggleButton As New MyButton With {
            .Text = If(Disabled, "启用", "禁用"),
            .Height = 28,
            .MinWidth = 64,
            .Padding = New Thickness(12, 0, 12, 0),
            .Margin = New Thickness(8, 0, 8, 0),
            .VerticalAlignment = VerticalAlignment.Center}
        AddHandler ToggleButton.Click, Sub()
                                           TogglePack(FullPath, Disabled)
                                       End Sub
        Grid.SetColumn(ToggleButton, 4)
        Row.Children.Add(ToggleButton)

        Dim DeleteButton As New MyButton With {
            .Text = "删除",
            .Height = 28,
            .MinWidth = 64,
            .Padding = New Thickness(12, 0, 12, 0),
            .VerticalAlignment = VerticalAlignment.Center,
            .ColorType = MyButton.ColorState.Red}
        AddHandler DeleteButton.Click, Sub()
                                           DeletePack(FullPath, DisplayName)
                                       End Sub
        Grid.SetColumn(DeleteButton, 5)
        Row.Children.Add(DeleteButton)

        PanPacks.Children.Add(Row)
    End Sub

    Private Function GetFileSizeText(Path As String) As String
        Try
            Dim Size As Long = New IO.FileInfo(Path).Length
            If Size >= 1024 * 1024 Then Return (Size / 1024 / 1024).ToString("0.0") & " MB"
            Return Math.Max(1, Size \ 1024) & " KB"
        Catch ex As Exception
            Return ""
        End Try
    End Function

    Private Sub TogglePack(FullPath As String, Disabled As Boolean)
        Try
            Dim Target As String
            If Disabled Then
                Target = FullPath.Substring(0, FullPath.Length - 9)
            Else
                Target = FullPath & ".disabled"
            End If
            If IO.Directory.Exists(FullPath) Then
                IO.Directory.Move(FullPath, Target)
            Else
                IO.File.Move(FullPath, Target)
            End If
            ReloadList()
        Catch ex As Exception
            Hint("切换启用状态失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub DeletePack(FullPath As String, DisplayName As String)
        Try
            If MyMsgBox("确定要删除 " & DisplayName & " 吗？此操作不可撤销。", "删除确认", "删除", "取消", IsWarn:=True) <> 1 Then Return
            If IO.Directory.Exists(FullPath) Then
                IO.Directory.Delete(FullPath, True)
            Else
                IO.File.Delete(FullPath)
            End If
            ReloadList()
        Catch ex As Exception
            Hint("删除失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnRefreshList_Click(sender As Object, e As EventArgs) Handles BtnRefreshList.Click
        ReloadList()
    End Sub

    Private Sub BtnOpenFolder_Click(sender As Object, e As EventArgs) Handles BtnOpenFolder.Click
        Try
            Dim Folder As String
            If Mode = 2 Then
                Folder = McFolderSelected & "saves"
            Else
                Folder = McFolderSelected & FolderName
            End If
            DirectoryUtils.Create(Folder)
            OpenExplorer(Folder)
        Catch ex As Exception
            Hint("打开文件夹失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnInstall_Click(sender As Object, e As EventArgs) Handles BtnInstall.Click
        Try
            Dim Dialog As New Microsoft.Win32.OpenFileDialog With {
                .Filter = "资源包/光影包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                .Title = "选择要安装的" & If(Mode = 1, "光影包", "资源包")}
            If Dialog.ShowDialog() <> True Then Return
            Dim TargetDir As String = McFolderSelected & FolderName
            DirectoryUtils.Create(TargetDir)
            IO.File.Copy(Dialog.FileName, IO.Path.Combine(TargetDir, IO.Path.GetFileName(Dialog.FileName)), True)
            ReloadList()
        Catch ex As Exception
            Hint("安装失败：" & ex.Message, HintType.Red)
        End Try
    End Sub


    Private Sub SearchTextChanged(sender As Object, e As EventArgs)
        ReloadList()
    End Sub


    Private Sub OpenDownload(sender As Object, e As EventArgs)
        Dim SubType As FormMain.PageSubType = FormMain.PageSubType.DownloadResourcePack
        If Mode = 1 Then SubType = FormMain.PageSubType.DownloadShader
        If Mode = 2 Then SubType = FormMain.PageSubType.DownloadDataPack
        FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Download, .Additional = SubType})
    End Sub

    Private Shared ReadOnly IconCache As New Dictionary(Of String, ImageSource)

    Private Function LoadPackIcon(FullPath As String) As ImageSource
        Try
            If IconCache.ContainsKey(FullPath) Then Return IconCache(FullPath)
            Dim Data As Byte() = Nothing
            If IO.Directory.Exists(FullPath) Then
                For Each Candidate As String In {"pack.png", "icon.png", IO.Path.Combine("assets", "pack.png")}
                    Dim Png As String = IO.Path.Combine(FullPath, Candidate)
                    If IO.File.Exists(Png) Then
                        Data = IO.File.ReadAllBytes(Png)
                        Exit For
                    End If
                Next
            Else
                Using Zip As ZipArchive = ZipFile.OpenRead(FullPath)
                    Dim Entry As ZipArchiveEntry = Zip.GetEntry("pack.png")
                    If Entry Is Nothing Then Entry = Zip.GetEntry("icon.png")
                    If Entry Is Nothing Then
                        Entry = Zip.Entries.FirstOrDefault(Function(E2) E2.FullName.EndsWith("/pack.png", StringComparison.OrdinalIgnoreCase) OrElse E2.FullName.EndsWith("/icon.png", StringComparison.OrdinalIgnoreCase))
                    End If
                    If Entry IsNot Nothing Then
                        Using S As IO.Stream = Entry.Open()
                            Using Ms As New IO.MemoryStream
                                S.CopyTo(Ms)
                                Data = Ms.ToArray()
                            End Using
                        End Using
                    End If
                End Using
            End If
            If Data Is Nothing OrElse Data.Length = 0 Then Return Nothing
            Dim Bit As New BitmapImage
            Bit.BeginInit()
            Bit.CacheOption = BitmapCacheOption.OnLoad
            Bit.CreateOptions = BitmapCreateOptions.IgnoreImageCache
            Bit.DecodePixelWidth = 34
            Bit.StreamSource = New IO.MemoryStream(Data)
            Bit.EndInit()
            Bit.Freeze()
            IconCache(FullPath) = Bit
            Return Bit
        Catch ex As Exception
            Logger.Warn(ex, "读取包图标失败：" & FullPath)
            Return Nothing
        End Try
    End Function

End Class
