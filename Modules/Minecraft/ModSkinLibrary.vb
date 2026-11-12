Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Net.Http
Imports System.Text

Public Module ModSkinLibrary

    Public Const SkinUserAgent As String = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36"
    Public Const LittleSkinListApi As String = "https://littleskin.cn/skinlib/list"
    Public Const LittleSkinInfoApi As String = "https://littleskin.cn/skinlib/info/"
    Public Const LittleSkinTextureApi As String = "https://littleskin.cn/textures/"

    Public Class SkinFile
        Public Property Name As String = ""
        Public Property Path As String = ""
        Public Property Slim As Boolean = False
        Public Property Width As Integer = 64
        Public Property Height As Integer = 64
        Public Overrides Function ToString() As String
            Return Name
        End Function
    End Class

    Public Class OnlineSkin
        Public Property Tid As Integer
        Public Property Name As String = ""
        Public Property Author As String = ""
        Public Property Model As String = ""
        Public Property Likes As Integer = 0
        Public Property Hash As String = ""
    End Class

    Public Property LastDownloadedPath As String = ""
    Private ReadOnly Http As HttpClient = CreateHttpClient()
    Private ReadOnly OnlineCache As New Dictionary(Of String, List(Of OnlineSkin))

    Private Function CreateHttpClient() As HttpClient
        Dim Client As New HttpClient
        Client.Timeout = TimeSpan.FromSeconds(20)
        Return Client
    End Function

    Public Sub ClearOnlineCache()
        SyncLock OnlineCache
            OnlineCache.Clear()
        End SyncLock
    End Sub

    Public ReadOnly Property SkinFolder As String
        Get
            Return Paths.AppDataThenName & "Skins\"
        End Get
    End Property

    Public ReadOnly Property CustomSkinPath As String
        Get
            Return Paths.AppDataThenName & "CustomSkin.png"
        End Get
    End Property

    Public Function GetSkins() As List(Of SkinFile)
        Dim Result As New List(Of SkinFile)
        Try
            DirectoryUtils.Create(SkinFolder)
            For Each FilePath As String In DirectoryUtils.EnumerateFiles(SkinFolder).
                Where(Function(Item) Item.ToLower().EndsWith(".png"))
                Dim Entry As New SkinFile With {
                    .Path = FilePath,
                    .Name = IO.Path.GetFileNameWithoutExtension(FilePath)}
                Try
                    Using Image As New Bitmap(FilePath)
                        Entry.Width = Image.Width
                        Entry.Height = Image.Height
                        If Image.Width = 64 AndAlso Image.Height = 64 Then Entry.Slim = Image.GetPixel(54, 20).A = 0
                    End Using
                Catch ex As Exception
                    Entry.Name &= "（无法读取）"
                End Try
                Result.Add(Entry)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取皮肤库失败")
        End Try
        Return Result.OrderBy(Function(Item) Item.Name).ToList()
    End Function

    Public Function FetchOnlineSkins(Page As Integer, Sort As String, PerPage As Integer) As List(Of OnlineSkin)
        Try
            Dim CacheKey As String = $"{Sort}|{Page}|{PerPage}"
            SyncLock OnlineCache
                Dim Cached As List(Of OnlineSkin) = Nothing
                If OnlineCache.TryGetValue(CacheKey, Cached) Then Return Cached
            End SyncLock
            Dim Url As String = $"{LittleSkinListApi}?page={Page + 1}&per_page={PerPage}"
            If Sort = "likes" Then Url &= "&sort=likes"
            Dim Json As JObject = GetJson(Url)
            Dim Result As New List(Of OnlineSkin)
            If Json("data") Is Nothing Then Return Result
            For Each Item As JObject In Json("data")
                Result.Add(New OnlineSkin With {
                    .Tid = CInt(Item("tid")),
                    .Name = If(Item("name") Is Nothing, "", Item("name").ToString()),
                    .Author = If(Item("nickname") Is Nothing, "", Item("nickname").ToString()),
                    .Model = If(Item("type") Is Nothing, "steve", Item("type").ToString()),
                    .Likes = If(Item("likes") Is Nothing, 0, CInt(Item("likes")))})
            Next
            Parallel.ForEach(Result, New Threading.Tasks.ParallelOptions With {.MaxDegreeOfParallelism = 8},
                Sub(Item)
                    Try
                        Dim Info As JObject = GetJson(LittleSkinInfoApi & Item.Tid.ToString())
                        Item.Hash = If(Info("hash") Is Nothing, "", Info("hash").ToString())
                    Catch ex As Exception
                    End Try
                End Sub)
            SyncLock OnlineCache
                OnlineCache(CacheKey) = Result
            End SyncLock
            Return Result
        Catch ex As Exception
            Logger.Warn(ex, "获取在线皮肤列表失败")
            Throw
        End Try
    End Function

    Public Function DownloadOnlineSkin(Item As OnlineSkin) As String
        Try
            If Item Is Nothing Then Return "没有选择皮肤"
            If String.IsNullOrEmpty(Item.Hash) Then
                Dim Info As JObject = GetJson(LittleSkinInfoApi & Item.Tid.ToString())
                Item.Hash = If(Info("hash") Is Nothing, "", Info("hash").ToString())
            End If
            If String.IsNullOrEmpty(Item.Hash) Then Return "无法获取皮肤文件地址"
            Dim Data As Byte() = DownloadBytes(LittleSkinTextureApi & Item.Hash)
            If Data Is Nothing OrElse Data.Length = 0 Then Return "下载皮肤失败"
            DirectoryUtils.Create(SkinFolder)
            Dim BaseName As String = SanitizeName(Item.Name)
            If BaseName = "skin" Then BaseName = "Skin_" & Item.Tid.ToString()
            Dim Target As String = SkinFolder & BaseName & ".png"
            If FileUtils.Exists(Target) Then Target = SkinFolder & BaseName & "_" & Item.Tid.ToString() & ".png"
            IO.File.WriteAllBytes(Target, Data)
            Try
                Using Image As New Bitmap(Target)
                    If Image.Width > 64 Then
                        Dim NewBitmap As New Bitmap(64, Math.Max(32, CInt(Image.Height * 64.0 / Image.Width)))
                        Using Graphics As Graphics = Graphics.FromImage(NewBitmap)
                            Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor
                            Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half
                            Graphics.DrawImage(Image, 0, 0, NewBitmap.Width, NewBitmap.Height)
                        End Using
                        Dim SavePath As String = Target
                        Image.Dispose()
                        NewBitmap.Save(SavePath, ImageFormat.Png)
                        NewBitmap.Dispose()
                    End If
                End Using
            Catch ex As Exception
            End Try
            Logger.Info($"已下载在线皮肤：{Item.Name}（作者 {Item.Author}）→ {Target}")
            LastDownloadedPath = Target
            Return Nothing
        Catch ex As Exception
            Logger.Warn(ex, "下载在线皮肤失败")
            Return ex.Message
        End Try
    End Function

    Private Function GetJson(Url As String) As JObject
        Using Request As New HttpRequestMessage(HttpMethod.Get, Url)
            Request.Headers.TryAddWithoutValidation("User-Agent", SkinUserAgent)
            Using Response As HttpResponseMessage = Http.SendAsync(Request).Result
                If Not Response.IsSuccessStatusCode Then Throw New Exception("HTTP " & CInt(Response.StatusCode))
                Return JObject.Parse(Response.Content.ReadAsStringAsync().Result)
            End Using
        End Using
    End Function

    Public Function Import(SourcePath As String) As String
        Try
            Using Image As New Bitmap(SourcePath)
                If Image.Width <> 64 OrElse (Image.Height <> 32 AndAlso Image.Height <> 64) Then
                    Return "皮肤图片必须是 64×32 或 64×64 像素"
                End If
            End Using
            DirectoryUtils.Create(SkinFolder)
            Dim BaseName As String = SanitizeName(IO.Path.GetFileNameWithoutExtension(SourcePath))
            Dim Target As String = SkinFolder & BaseName & ".png"
            Dim Index As Integer = 1
            Do While FileUtils.Exists(Target)
                Target = SkinFolder & BaseName & "_" & Index.ToString() & ".png"
                Index += 1
            Loop
            FileUtils.Copy(SourcePath, Target)
            Logger.Info($"已导入皮肤：{Target}")
            Return Nothing
        Catch ex As Exception
            Logger.Warn(ex, "导入皮肤失败")
            Return ex.Message
        End Try
    End Function

    Public Function FetchByName(PlayerName As String) As String
        Try
            Dim Name As String = PlayerName.Trim()
            If Name = "" Then Return "玩家名不能为空"
            Hint($"正在获取玩家 {Name} 的皮肤……", HintType.Blue)
            Dim Uuid As String = McLoginMojangUuid(Name, True)
            If String.IsNullOrWhiteSpace(Uuid) Then Return "找不到这个正版玩家"
            Dim Url As String = McSkinGetAddress(Uuid, "Mojang")
            If String.IsNullOrWhiteSpace(Url) Then Return "这个玩家没有设置皮肤"
            Dim Data As Byte() = DownloadBytes(Url)
            If Data Is Nothing OrElse Data.Length = 0 Then Return "下载皮肤失败"
            DirectoryUtils.Create(SkinFolder)
            Dim Target As String = SkinFolder & SanitizeName(Name) & ".png"
            IO.File.WriteAllBytes(Target, Data)
            Logger.Info($"已获取玩家皮肤：{Name} → {Target}")
            Return Nothing
        Catch ex As Exception
            Logger.Warn(ex, "获取玩家皮肤失败")
            Return ex.Message
        End Try
    End Function

    Private Function DownloadBytes(Url As String) As Byte()
        Using Request As New HttpRequestMessage(HttpMethod.Get, Url)
            Request.Headers.TryAddWithoutValidation("User-Agent", SkinUserAgent)
            Using Response As HttpResponseMessage = Http.SendAsync(Request).Result
                If Not Response.IsSuccessStatusCode Then Return Nothing
                Return Response.Content.ReadAsByteArrayAsync().Result
            End Using
        End Using
    End Function

    Public Function Apply(Skin As SkinFile) As String
        Try
            If Skin Is Nothing OrElse Not FileUtils.Exists(Skin.Path) Then Return "皮肤文件不存在"
            FileUtils.Copy(Skin.Path, CustomSkinPath)
            Using Source As New Bitmap(CustomSkinPath)
                If Source.Width = 64 AndAlso Source.Height = 32 Then
                    Using Expanded As New Bitmap(64, 64)
                        Using Graphics As Graphics = Graphics.FromImage(Expanded)
                            Graphics.DrawImageUnscaled(Source, 0, 0)
                        End Using
                        Source.Dispose()
                        Expanded.Save(CustomSkinPath, ImageFormat.Png)
                    End Using
                End If
            End Using
            Settings.Set("LaunchSkinSlim", Skin.Slim)
            Settings.Set("LaunchSkinType", 4)
            PageLaunchLeft.SkinLegacy.Start(IsForceRestart:=True)
            Logger.Info($"已应用离线皮肤：{Skin.Name}（Alex={Skin.Slim}）")
            Return Nothing
        Catch ex As Exception
            Logger.Warn(ex, "应用离线皮肤失败")
            Return ex.Message
        End Try
    End Function

    Public Sub ResetSkin()
        Try
            FileUtils.Delete(CustomSkinPath)
            Settings.Set("LaunchSkinType", 0)
            PageLaunchLeft.SkinLegacy.Start(IsForceRestart:=True)
        Catch ex As Exception
            Logger.Warn(ex, "重置离线皮肤失败")
        End Try
    End Sub

    Public Function Delete(Skin As SkinFile) As String
        Try
            If Skin Is Nothing Then Return "没有选择皮肤"
            FileUtils.Delete(Skin.Path)
            Return Nothing
        Catch ex As Exception
            Return ex.Message
        End Try
    End Function

    Public Function IsApplied(Skin As SkinFile) As Boolean
        Try
            If Skin Is Nothing OrElse Not FileUtils.Exists(Skin.Path) OrElse Not FileUtils.Exists(CustomSkinPath) Then Return False
            Return IO.File.ReadAllBytes(Skin.Path).SequenceEqual(IO.File.ReadAllBytes(CustomSkinPath))
        Catch ex As Exception
            Return False
        End Try
    End Function

    Private Function SanitizeName(Name As String) As String
        Dim Result As New StringBuilder
        For Each Ch As Char In If(Name, "")
            If Char.IsLetterOrDigit(Ch) OrElse Ch = "-"c OrElse Ch = "_"c Then Result.Append(Ch)
        Next
        Dim Value As String = Result.ToString()
        If Value = "" Then Value = "skin"
        Return Value
    End Function

End Module
