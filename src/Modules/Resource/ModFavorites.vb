Public Module ModFavorites

    Public Class FavoriteEntry
        Public Platform As ResourcePlatforms
        Public Id As String
        Public Name As String
        Public LogoUrl As String
    End Class

    Private ReadOnly Lock As New Object
    Private Cached As List(Of FavoriteEntry) = Nothing

    Public Function GetFilePath() As String
        Return PathPure.Value & "Favorites.json"
    End Function

    Public Function GetAll() As List(Of FavoriteEntry)
        SyncLock Lock
            If Cached IsNot Nothing Then Return Cached
            Dim Result As New List(Of FavoriteEntry)
            Try
                Dim FilePath As String = GetFilePath()
                If FileUtils.Exists(FilePath) Then
                    Dim Json As JArray = CType(FileUtils.ReadAsString(FilePath).DeserializeJson(), JArray)
                    If Json IsNot Nothing Then
                        For Each Token As JToken In Json
                            If Token Is Nothing Then Continue For
                            Dim Entry As New FavoriteEntry With {
                                .Platform = CType(Token("Platform").ToObject(Of Integer), ResourcePlatforms),
                                .Id = Token("Id").ToString,
                                .Name = If(Token("Name") Is Nothing, "", Token("Name").ToString)}
                            If Token("LogoUrl") IsNot Nothing AndAlso Token("LogoUrl").Type <> JTokenType.Null Then Entry.LogoUrl = Token("LogoUrl").ToString
                            Result.Add(Entry)
                        Next
                    End If
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取收藏列表失败")
            End Try
            Cached = Result
            Return Cached
        End SyncLock
    End Function

    Public Function Contains(Project As ResourceProject) As Boolean
        If Project Is Nothing Then Return False
        Return GetAll().Any(Function(Entry) Entry.Platform = Project.Platform AndAlso Entry.Id = Project.Id)
    End Function

    Public Function Toggle(Project As ResourceProject) As Boolean
        If Project Is Nothing Then Return False
        Dim NewList As New List(Of FavoriteEntry)
        Dim IsFavorite As Boolean
        SyncLock Lock
            Dim Current As List(Of FavoriteEntry) = GetAll()
            For Each Entry In Current
                If Entry.Platform = Project.Platform AndAlso Entry.Id = Project.Id Then Continue For
                NewList.Add(Entry)
            Next
            IsFavorite = NewList.Count = Current.Count
            If IsFavorite Then NewList.Add(New FavoriteEntry With {.Platform = Project.Platform, .Id = Project.Id, .Name = Project.RawName, .LogoUrl = Project.LogoUrl})
            Cached = NewList
        End SyncLock
        Save()
        Return IsFavorite
    End Function

    Private Sub Save()
        Try
            DirectoryUtils.Create(PathPure.Value)
            Dim Json As New JArray
            For Each Entry In GetAll()
                Dim Token As New JObject
                Token("Platform") = CInt(Entry.Platform)
                Token("Id") = Entry.Id
                Token("Name") = Entry.Name
                If Not String.IsNullOrEmpty(Entry.LogoUrl) Then Token("LogoUrl") = Entry.LogoUrl
                Json.Add(Token)
            Next
            FileUtils.Write(GetFilePath(), Json.ToString(Newtonsoft.Json.Formatting.Indented))
        Catch ex As Exception
            Logger.Warn(ex, "保存收藏列表失败")
        End Try
    End Sub

    Public Function GetProjects(TypeFilter As ResourceTypes) As List(Of ResourceProject)
        Dim Result As New List(Of ResourceProject)
        For Each Entry In GetAll()
            Try
                Dim Project As ResourceProject = Nothing
                Try
                    Project = ResourceProject.FromProjectId(Entry.Id, Entry.Platform)
                Catch ex As Exception
                    Logger.Warn(ex, $"获取收藏的工程信息失败：{Entry.Id}")
                End Try
                If Project IsNot Nothing AndAlso (TypeFilter = ResourceTypes.Any OrElse Project.Types.HasFlagF(TypeFilter)) Then Result.Add(Project)
            Catch ex As Exception
                Logger.Warn(ex, $"处理收藏的工程信息失败：{Entry.Id}")
            End Try
        Next
        Return Result
    End Function

End Module
