Imports System.Net.Http

Public Class OnlineLitematicItem
    Public Property Name As String
    Public Property FileName As String
    Public Property Repo As String
    Public Property RepoDisplay As String
    Public Property Path As String
    Public Property Size As Long
    Public Property DownloadUrl As String
End Class

Public Module ModLitematicOnline

    Public ReadOnly Items As New List(Of OnlineLitematicItem)
    Public Property LastError As String = ""
    Public Property HasLoaded As Boolean = False

    Private ReadOnly Sources As Object(,) = {
        {"Mindstormman06/litematics", "main"},
        {"Arcadi4/LitematicaPreview", "main"},
        {"SmylerMC/litemapy", "master"},
        {"SandroHc/schematic4j", "master"}
    }

    Public Function Fetch() As Boolean
        Try
            SyncLock Items
                Items.Clear()
            End SyncLock
            LastError = ""
            For Index As Integer = 0 To Sources.GetLength(0) - 1
                Dim Repo As String = CStr(Sources(Index, 0))
                Dim Branch As String = CStr(Sources(Index, 1))
                Try
                    Dim Json As String = NetRequestByLoader($"https://api.github.com/repos/{Repo}/git/trees/{Branch}?recursive=1", 40000, True)
                    Dim Tree As JObject = JObject.Parse(Json)
                    If Tree("tree") Is Nothing Then Continue For
                    Dim RepoDisplay As String = Repo.Split("/"c).Last()
                    For Each Node As JToken In CType(Tree("tree"), JArray)
                        Dim NodePath As String = Node("path").ToString()
                        If Not NodePath.EndsWithF(".litematic", True) Then Continue For
                        If Node("type") IsNot Nothing AndAlso Node("type").ToString() <> "blob" Then Continue For
                        Dim FileName As String = NodePath.Split("/"c).Last()
                        Dim DisplayName As String = FileName.Substring(0, FileName.Length - ".litematic".Length)
                        Dim Item As New OnlineLitematicItem With {
                            .Name = DisplayName,
                            .FileName = FileName,
                            .Repo = Repo,
                            .RepoDisplay = RepoDisplay,
                            .Path = NodePath,
                            .Size = If(Node("size") Is Nothing, 0L, Node("size").ToObject(Of Long)),
                            .DownloadUrl = $"https://raw.githubusercontent.com/{Repo}/{Branch}/{NodePath.Replace(" ", "%20")}"}
                        SyncLock Items
                            Items.Add(Item)
                        End SyncLock
                    Next
                Catch ex As Exception
                    LastError = ex.Message
                    Logger.Warn(ex, "获取在线投影列表失败：" & Repo)
                End Try
            Next
            HasLoaded = Items.Count > 0
            Return HasLoaded
        Catch ex As Exception
            LastError = ex.Message
            Logger.Warn(ex, "获取在线投影库失败")
            Return False
        End Try
    End Function

    Public Function Download(Item As OnlineLitematicItem, Optional Folder As String = Nothing) As String
        If String.IsNullOrWhiteSpace(Folder) Then Folder = IO.Path.Combine(Paths.Base, "PCL\Litematic")
        DirectoryUtils.Create(Folder)
        Using Client As New HttpClient()
            Client.Timeout = TimeSpan.FromSeconds(90)
            Client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-ForK")
            Dim Bytes As Byte() = Client.GetByteArrayAsync(Item.DownloadUrl).GetAwaiter().GetResult()
            If Bytes Is Nothing OrElse Bytes.Length = 0 Then Throw New Exception("下载内容为空")
            If Bytes.Length < 2 OrElse Bytes(0) <> &H1F OrElse Bytes(1) <> &H8B Then Throw New Exception("下载到的不是有效的 litematic 文件")
            Dim Target As String = IO.Path.Combine(Folder, Item.FileName)
            IO.File.WriteAllBytes(Target, Bytes)
            Return Target
        End Using
    End Function

    Public Function FormatSize(Size As Long) As String
        If Size < 1024 Then Return Size & " B"
        If Size < 1024 * 1024 Then Return (Size / 1024).ToString("0.#") & " KB"
        Return (Size / 1024 / 1024).ToString("0.##") & " MB"
    End Function

End Module
