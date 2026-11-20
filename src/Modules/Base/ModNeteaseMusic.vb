Imports System.Net.Http

Public Module ModNeteaseMusic

    Public Const UserAgent As String = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36"

    Public Property CookieValue As String
        Get
            Return Settings.Get(Of String)("NeteaseCookie")
        End Get
        Set(value As String)
            Settings.Set("NeteaseCookie", If(value, "").Trim)
        End Set
    End Property

    Public Class NeteaseSong
        Public Property Id As Long
        Public Property Name As String
        Public Property Artists As String
        Public Property Album As String
        Public Overrides Function ToString() As String
            Return $"{Name} - {Artists}"
        End Function
    End Class

    Public Class NeteaseLyricLine
        Public Property Time As Double
        Public Property Text As String
    End Class

    Private Function Request(Url As String) As String
        Dim Handler As New HttpClientHandler With {.AllowAutoRedirect = False, .UseCookies = False}
        Using Client As New HttpClient(Handler)
            Client.Timeout = TimeSpan.FromSeconds(20)
            Using Req As New HttpRequestMessage(HttpMethod.Get, Url)
                Req.Headers.TryAddWithoutValidation("User-Agent", UserAgent)
                Req.Headers.TryAddWithoutValidation("Referer", "https://music.163.com/")
                If CookieValue <> "" Then
                    Req.Headers.TryAddWithoutValidation("Cookie", $"MUSIC_U={CookieValue}")
                End If
                Using Resp = Client.SendAsync(Req).Result
                    Using Reader As New StreamReader(Resp.Content.ReadAsStreamAsync().Result, Encoding.UTF8)
                        Return Reader.ReadToEnd()
                    End Using
                End Using
            End Using
        End Using
    End Function

    Public Function SearchMusic(Keyword As String) As List(Of NeteaseSong)
        Dim Result As New List(Of NeteaseSong)
        Dim Json As String = Request($"https://music.163.com/api/search/get/web?csrf_token=&s={Uri.EscapeDataString(Keyword)}&type=1&offset=0&total=true&limit=50")
        Dim Obj As JObject = Json.DeserializeJson()
        If Obj("result") Is Nothing OrElse Obj("result")("songs") Is Nothing Then Return Result
        For Each Item As JObject In Obj("result")("songs")
            Result.Add(New NeteaseSong With {
                .Id = Item("id").ToObject(Of Long),
                .Name = Item("name").ToString,
                .Artists = String.Join("/", Item("artists").Select(Function(a) a("name").ToString)),
                .Album = Item("album")("name").ToString})
        Next
        Return Result
    End Function

    Public Function GetSongUrl(SongId As Long) As String
        Try
            Dim Json As String = Request($"https://music.163.com/api/song/enhance/player/url?ids=%5B{SongId}%5D&br=320000")
            Dim Obj As JObject = Json.DeserializeJson()
            Dim Url As String = Obj("data")?.First?("url")?.ToString()
            If Not String.IsNullOrEmpty(Url) Then Return Url
        Catch ex As Exception
            Logger.Warn(ex, "获取网易云歌曲播放地址失败")
        End Try
        Try
            Dim Handler As New HttpClientHandler With {.AllowAutoRedirect = False, .UseCookies = False}
            Using Client As New HttpClient(Handler)
                Using Req As New HttpRequestMessage(HttpMethod.Get, $"https://music.163.com/song/media/outer/url?id={SongId}.mp3")
                    Req.Headers.TryAddWithoutValidation("User-Agent", UserAgent)
                    Req.Headers.TryAddWithoutValidation("Referer", "https://music.163.com/")
                    Using Resp = Client.SendAsync(Req).Result
                        Dim Loc As String = If(Resp.Headers.Location?.ToString, "")
                        If Loc <> "" AndAlso Not Loc.Contains("/404") Then Return Loc
                    End Using
                End Using
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "获取网易云歌曲外链失败")
        End Try
        Return ""
    End Function

    Public Function GetLyric(SongId As Long) As List(Of NeteaseLyricLine)
        Dim Result As New List(Of NeteaseLyricLine)
        Try
            Dim Json As String = Request($"https://music.163.com/api/song/lyric?id={SongId}&lv=1&kv=1&tv=-1")
            Dim Lrc As String = Json.DeserializeJson()("lrc")?("lyric")?.ToString()
            If String.IsNullOrEmpty(Lrc) Then Return Result
            For Each Line As String In Lrc.Replace(vbCrLf, vbLf).Split(vbLf)
                Dim Match As RegularExpressions.Match = RegularExpressions.Regex.Match(Line, "^\[(\d+):(\d+(?:\.\d+)?)\](.*)$")
                If Not Match.Success Then Continue For
                Dim TimeSec As Double = Integer.Parse(Match.Groups(1).Value) * 60 + Double.Parse(Match.Groups(2).Value, Globalization.CultureInfo.InvariantCulture)
                Dim Text As String = Match.Groups(3).Value.Trim
                If Text <> "" Then Result.Add(New NeteaseLyricLine With {.Time = TimeSec, .Text = Text})
            Next
        Catch ex As Exception
            Logger.Warn(ex, "获取网易云歌词失败")
        End Try
        Return Result
    End Function

    Public Function GetAccount() As JObject
        Try
            If CookieValue = "" Then Return Nothing
            Dim Obj As JObject = Request("https://music.163.com/api/nuser/account/get").DeserializeJson()
            If Obj("code")?.ToObject(Of Integer) = 200 AndAlso Obj("account") IsNot Nothing Then Return Obj
        Catch ex As Exception
            Logger.Warn(ex, "获取网易云账号信息失败")
        End Try
        Return Nothing
    End Function

    Public Function GetLikedSongs() As List(Of NeteaseSong)
        Dim Result As New List(Of NeteaseSong)
        Try
            Dim Account = GetAccount()
            If Account Is Nothing Then Return Result
            Dim Uid As String = Account("account")("id").ToString
            Dim PlaylistObj As JObject = Request($"https://music.163.com/api/user/playlist?uid={Uid}&limit=1&offset=0").DeserializeJson()
            Dim LikedId As String = PlaylistObj("playlist")(0)("id").ToString
            Dim Detail As JObject = Request($"https://music.163.com/api/v1/playlist/detail?id={LikedId}").DeserializeJson()
            For Each Item As JObject In Detail("playlist")("tracks")
                Result.Add(New NeteaseSong With {
                    .Id = Item("id").ToObject(Of Long),
                    .Name = Item("name").ToString,
                    .Artists = String.Join("/", Item("ar").Select(Function(a) a("name").ToString)),
                    .Album = Item("al")("name").ToString})
            Next
        Catch ex As Exception
            Logger.Warn(ex, "获取网易云收藏列表失败")
        End Try
        Return Result
    End Function

    Public Function SetLike(SongId As Long, IsLike As Boolean) As Boolean
        Try
            If CookieValue = "" Then Return False
            Dim Obj As JObject = Request($"https://music.163.com/api/song/like?trackId={SongId}&like={If(IsLike, "true", "false")}&csrf_token=").DeserializeJson()
            Return Obj("code")?.ToObject(Of Integer) = 200
        Catch ex As Exception
            Logger.Warn(ex, "网易云收藏操作失败")
        End Try
        Return False
    End Function

End Module
