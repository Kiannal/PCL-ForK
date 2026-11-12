Public Class PageExperiment
    Inherits MyPageRight

    Public Sub New()
        InitializeComponent()
        AddHandler Me.IsVisibleChanged,
            Sub()
                If Me.IsVisible Then RefreshScreenshots()
            End Sub
    End Sub

    Private Sub PageExperiment_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        RefreshScreenshots()
    End Sub

    Private Sub BtnRefreshShots_Click() Handles BtnRefreshShots.Click
        RefreshScreenshots()
    End Sub

    Private Sub AddShotDirs(Dirs As HashSet(Of String), Root As String)
        Try
            Dirs.Add(IO.Path.Combine(Root, "screenshots"))
            Dim Vers As String = IO.Path.Combine(Root, "versions")
            If IO.Directory.Exists(Vers) Then
                For Each VerDir In IO.Directory.GetDirectories(Vers)
                    Dirs.Add(IO.Path.Combine(VerDir, "screenshots"))
                Next
            End If
        Catch ex As Exception
        End Try
    End Sub

    Private Sub RefreshScreenshots()
        PanShots.Children.Clear()
        Dim Dirs As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim SettingFolder As String = Settings.Get(Of String)("LaunchFolderSelect")
        If SettingFolder <> "" Then AddShotDirs(Dirs, SettingFolder)
        Dim AppDataMc As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.minecraft"
        AddShotDirs(Dirs, AppDataMc)
        Try
            For Each Folder In McFolderList
                If Folder.Location <> "" Then AddShotDirs(Dirs, Folder.Location)
            Next
        Catch ex As Exception
        End Try
        For Each Root In {"D:\Minecraft", "C:\Minecraft"}
            Try
                If IO.Directory.Exists(Root) Then AddShotDirs(Dirs, Root)
            Catch ex As Exception
            End Try
        Next
        Dim Files As New List(Of String)
        For Each ShotDir In Dirs
            Try
                If IO.Directory.Exists(ShotDir) Then Files.AddRange(IO.Directory.GetFiles(ShotDir, "*.png"))
            Catch ex As Exception
            End Try
        Next
        Dim SortedFiles As List(Of String) = Files.Distinct().OrderByDescending(Function(f) IO.File.GetLastWriteTime(f)).ToList()
        Files = SortedFiles
        LabShotCount.Text = "共 " & Files.Count & " 张"
        LabEmpty.Visibility = If(Files.Count = 0, Visibility.Visible, Visibility.Collapsed)
        For Each ShotFile In Files.Take(120)
            PanShots.Children.Add(MakeShotCard(ShotFile))
        Next
    End Sub

    Private Function LoadImage(FilePath As String) As BitmapImage
        Dim Bit As New BitmapImage
        Bit.BeginInit()
        Bit.CacheOption = BitmapCacheOption.OnLoad
        Bit.DecodePixelWidth = 1600
        Bit.UriSource = New Uri(FilePath)
        Bit.EndInit()
        Bit.Freeze()
        Return Bit
    End Function

    Private Function MakeShotCard(FilePath As String) As Border
        Dim Card As New Border With {
            .Width = 220, .Height = 160, .Margin = New Thickness(0, 0, 12, 12),
            .CornerRadius = New CornerRadius(10),
            .Background = New SolidColorBrush(Color.FromRgb(30, 30, 34)),
            .ClipToBounds = True,
            .Cursor = Cursors.Hand}
        Dim Img As New Image With {.Stretch = Stretch.UniformToFill}
        Try
            Img.Source = LoadImage(FilePath)
        Catch ex As Exception
        End Try
        Card.Clip = New RectangleGeometry(New Rect(0, 0, 220, 160), 10, 10)
        Card.Child = Img
        AddHandler Card.MouseLeftButtonUp, Sub()
                                               Try
                                                   Dim Frm As New FrmScreenshotView(FilePath)
                                                   Frm.Show()
                                               Catch ex As Exception
                                               End Try
                                           End Sub
        Return Card
    End Function

End Class
