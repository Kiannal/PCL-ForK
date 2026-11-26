Imports System.IO
Imports Newtonsoft.Json.Linq

Public Module ModWallpaperDefaults

    Public Const ManifestUrl As String = "https://pcl-update.cn-nb2.rains3.com/mc/wallpapers/list.json"
    Public Const MediaRoot As String = "https://pcl-update.cn-nb2.rains3.com/mc/wallpapers/"

    Public Class WallpaperItem
        Public Property File As String
        Public Property Title As String
        Public Property Size As Long
        Public Property Sha1 As String
    End Class

    Private CachedItems As List(Of WallpaperItem) = Nothing
    Private ManifestLoading As Boolean = False

    ''' <summary>
    ''' 内置视频壁纸的本地缓存目录。
    ''' </summary>
    Public ReadOnly Property WallpaperFolder As String
        Get
            Return PathPure.Value & "Wallpapers\"
        End Get
    End Property

    Private ReadOnly Property ManifestCache As String
        Get
            Return WallpaperFolder & "list.json"
        End Get
    End Property

    ''' <summary>
    ''' 读取壁纸清单。只走本地缓存，不发网络请求，可在 UI 线程安全调用。
    ''' </summary>
    Public Function GetManifest() As List(Of WallpaperItem)
        If CachedItems IsNot Nothing Then Return CachedItems
        Dim Result As New List(Of WallpaperItem)
        Try
            If FileUtils.Exists(ManifestCache) Then
                Dim Json As String = IO.File.ReadAllText(ManifestCache, Global.System.Text.Encoding.UTF8)
                Dim Root As JObject = JObject.Parse(Json)
                For Each Token As JToken In CType(Root("items"), JArray)
                    Result.Add(New WallpaperItem With {
                        .File = Token("file")?.ToString,
                        .Title = If(Token("title")?.ToString, Token("file")?.ToString),
                        .Size = If(Token("size") Is Nothing, 0L, Token("size").ToObject(Of Long)),
                        .Sha1 = Token("sha1")?.ToString
                    })
                Next
            End If
        Catch ex As Exception
            Logger.Warn(ex, "读取壁纸清单缓存失败")
        End Try
        Result = Result.Where(Function(Item) Not String.IsNullOrEmpty(Item.File)).
            OrderBy(Function(Item) If(Item.File.ToLowerInvariant().EndsWith(".webm"), 1, 0)).
            ThenBy(Function(Item) Item.File, StringComparer.OrdinalIgnoreCase).ToList()
        CachedItems = Result
        Return Result
    End Function

    ''' <summary>
    ''' 后台拉取最新清单并写入本地缓存。完成后在 UI 线程回调。
    ''' </summary>
    Public Sub RefreshManifestAsync(Optional OnDone As Action = Nothing)
        If ManifestLoading Then Return
        ManifestLoading = True
        RunInNewThread(
            Sub()
                Try
                    Dim Json As String = NetRequestByClient(ManifestUrl, RequireJson:=True, MakeLog:=False)
                    If Not String.IsNullOrWhiteSpace(Json) Then
                        DirectoryUtils.Create(WallpaperFolder)
                        IO.File.WriteAllText(ManifestCache, Json, Global.System.Text.Encoding.UTF8)
                        CachedItems = Nothing
                        Logger.Info($"壁纸清单已更新，共 {GetManifest().Count} 项")
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, "拉取壁纸清单失败")
                Finally
                    ManifestLoading = False
                    If OnDone IsNot Nothing Then RunInUi(Sub() OnDone())
                End Try
            End Sub, "壁纸清单")
    End Sub

    ''' <summary>
    ''' 扫描本地已下载的壁纸文件。
    ''' </summary>
    Public Function GetLocalVideos() As List(Of String)
        Dim Result As New List(Of String)
        Try
            If Not DirectoryUtils.Exists(WallpaperFolder) Then Return Result
            For Each Path In DirectoryUtils.EnumerateFiles(WallpaperFolder, includeSubDirectories:=False)
                Select Case IO.Path.GetExtension(Path).ToLowerInvariant()
                    Case ".mp4", ".m4v", ".wmv", ".avi", ".webm", ".mov"
                        Result.Add(Path)
                End Select
            Next
            Result.Sort(Function(A, B)
                            Dim Pa As Integer = If(IO.Path.GetExtension(A).ToLowerInvariant() = ".webm", 1, 0)
                            Dim Pb As Integer = If(IO.Path.GetExtension(B).ToLowerInvariant() = ".webm", 1, 0)
                            If Pa <> Pb Then Return Pa.CompareTo(Pb)
                            Return String.Compare(IO.Path.GetFileName(A), IO.Path.GetFileName(B), StringComparison.OrdinalIgnoreCase)
                        End Function)
        Catch ex As Exception
            Logger.Warn(ex, "扫描本地壁纸失败")
        End Try
        Return Result
    End Function

    Public Function GetLocalPath(Item As WallpaperItem) As String
        Return WallpaperFolder & Item.File
    End Function

    ''' <summary>
    ''' 该壁纸是否已在本地且大小相符。
    ''' </summary>
    Public Function IsLocalReady(Item As WallpaperItem) As Boolean
        Try
            Dim Path As String = GetLocalPath(Item)
            If Not FileUtils.Exists(Path) Then Return False
            If Item.Size > 0 Then Return New IO.FileInfo(Path).Length = Item.Size
            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 确保壁纸已在本地，缺失则从对象存储下载。必须在后台线程调用。
    ''' </summary>
    Public Function EnsureLocal(Item As WallpaperItem) As String
        Try
            DirectoryUtils.Create(WallpaperFolder)
            Dim Path As String = GetLocalPath(Item)
            If IsLocalReady(Item) Then Return Path
            Logger.Info($"开始下载内置壁纸：{Item.Title}")
            Dim Checker As FileChecker = Nothing
            If Not String.IsNullOrEmpty(Item.Sha1) Then
                Checker = New FileChecker With {.ActualSize = If(Item.Size > 0, Item.Size, -1), .Hash = Item.Sha1}
            End If
            NetDownloadByLoader(MediaRoot & Item.File, Path, Check:=Checker)
            If Not FileUtils.Exists(Path) Then
                Logger.Warn($"内置壁纸下载后仍不存在：{Item.Title}")
                Return Nothing
            End If
            Logger.Info($"内置壁纸下载完成：{Item.Title}")
            Return Path
        Catch ex As Exception
            Logger.Warn(ex, $"下载内置壁纸失败：{Item.Title}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' 首次启动且尚未设置任何背景时，选用本地第一个视频；本地一个都没有就后台拉清单后下载第一个。
    ''' </summary>
    Public Sub ApplyDefaultIfUnset()
        Try
            If FileUtils.Exists(Settings.Get(Of String)("UiBackgroundVideo")) Then Return
            If FileUtils.Exists(Settings.Get(Of String)("UiBackgroundImage")) Then Return
            Dim Local As List(Of String) = GetLocalVideos()
            If Local.Any() Then
                Settings.Set("UiBackgroundVideo", Local(0))
                Settings.Set("UiBackgroundType", 1)
                Logger.Info($"已启用内置视频壁纸：{IO.Path.GetFileName(Local(0))}")
                Return
            End If
            RefreshManifestAsync(
                Sub()
                    Try
                        Dim Items As List(Of WallpaperItem) = GetManifest()
                        If Not Items.Any() Then Return
                        Dim First As WallpaperItem = Items(0)
                        Settings.Set("UiBackgroundVideo", GetLocalPath(First))
                        Settings.Set("UiBackgroundType", 1)
                        Logger.Info($"已选择内置视频壁纸：{First.Title}，开始下载")
                        RunInNewThread(
                            Sub()
                                If EnsureLocal(First) IsNot Nothing Then
                                    RunInUi(Sub() FrmMain.UpdateBackgroundAndTitleBar())
                                End If
                            End Sub, "壁纸下载")
                    Catch ex As Exception
                        Logger.Warn(ex, "启用内置壁纸失败")
                    End Try
                End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "启用内置视频壁纸失败")
        End Try
    End Sub

    ''' <summary>
    ''' 切到下一个内置壁纸。目标未下载时先下载再切。
    ''' </summary>
    Public Function SwitchToNext() As String
        Dim Items As List(Of WallpaperItem) = GetManifest()
        If Not Items.Any() Then
            RefreshManifestAsync()
            Return Nothing
        End If
        Dim Current As String = Settings.Get(Of String)("UiBackgroundVideo")
        Dim Index As Integer = Items.FindIndex(Function(Item) String.Equals(GetLocalPath(Item), Current, StringComparison.OrdinalIgnoreCase))
        Index = If(Index < 0, 0, (Index + 1) Mod Items.Count)
        Dim Target As WallpaperItem = Items(Index)
        Settings.Set("UiBackgroundImage", "")
        Settings.Set("UiBackgroundVideo", GetLocalPath(Target))
        Settings.Set("UiBackgroundType", 1)
        If IsLocalReady(Target) Then Return Target.Title
        RunInNewThread(
            Sub()
                Dim Path As String = EnsureLocal(Target)
                If Path IsNot Nothing Then
                    RunInUi(Sub()
                                FrmMain.UpdateBackgroundAndTitleBar()
                                Hint($"内置壁纸已下载完成：{Target.Title}", HintType.Green)
                            End Sub)
                Else
                    RunInUi(Sub() Hint($"内置壁纸下载失败：{Target.Title}", HintType.Red))
                End If
            End Sub, "壁纸下载")
        Return Target.Title & "（正在下载）"
    End Function

End Module
