Imports System.IO.Compression
Imports System.Windows.Media.Imaging

Public Module ModMinecraftTextures

    Private ReadOnly TextureCache As New Dictionary(Of String, BitmapSource)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly BlockFaceCache As New Dictionary(Of String, Dictionary(Of Integer, String))(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly JsonCache As New Dictionary(Of String, JObject)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly JarList As New List(Of ZipArchive)
    Private ReadOnly JarNames As New List(Of String)
    Private ReadOnly MissCache As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Public Property IsReady As Boolean = False
    Public Property StatusText As String = ""

    Public Function Initialize() As Boolean
        Try
            If IsReady Then Return True
            For Each Zip In JarList
                Try
                    Zip.Dispose()
                Catch ex As Exception
                End Try
            Next
            JarList.Clear()
            JarNames.Clear()
            TextureCache.Clear()
            BlockFaceCache.Clear()
            JsonCache.Clear()
            MissCache.Clear()

            Dim Candidates As New List(Of String)
            If Not String.IsNullOrWhiteSpace(McFolderSelected) Then Candidates.Add(McFolderSelected)
            Try
                For Each Folder In McFolderList
                    If Folder IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(Folder.Location) AndAlso Not Candidates.Contains(Folder.Location) Then
                        Candidates.Add(Folder.Location)
                    End If
                Next
            Catch ex As Exception
            End Try
            Dim DefaultFolder As String = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft")
            If Not Candidates.Contains(DefaultFolder) Then Candidates.Add(DefaultFolder)

            Dim JarPaths As New List(Of String)
            If McInstanceSelected IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(McInstanceSelected.PathVersion) Then
                Try
                    For Each Jar In IO.Directory.GetFiles(McInstanceSelected.PathVersion, "*.jar")
                        JarPaths.Add(Jar)
                    Next
                Catch ex As Exception
                End Try
            End If
            For Each Folder In Candidates
                Dim VersionsFolder As String = IO.Path.Combine(Folder, "versions")
                If Not DirectoryUtils.Exists(VersionsFolder) Then Continue For
                Try
                    For Each VersionFolder In IO.Directory.GetDirectories(VersionsFolder)
                        For Each Jar In IO.Directory.GetFiles(VersionFolder, "*.jar", IO.SearchOption.TopDirectoryOnly)
                            If Not JarPaths.Contains(Jar) Then JarPaths.Add(Jar)
                        Next
                    Next
                Catch ex As Exception
                End Try
            Next
            JarPaths = JarPaths.OrderByDescending(Function(f) New IO.FileInfo(f).LastWriteTime).Take(6).ToList()
            For Each JarPath In JarPaths
                Try
                    JarList.Add(ZipFile.OpenRead(JarPath))
                    JarNames.Add(JarPath)
                Catch ex As Exception
                End Try
            Next
            If JarList.Count = 0 Then
                StatusText = "没有找到 Minecraft 客户端 jar，无法读取材质"
                Return False
            End If
            IsReady = True
            StatusText = "已加载 Minecraft 材质"
            Logger.Info($"Minecraft 材质来源：{String.Join("；", JarNames)}")
            Return True
        Catch ex As Exception
            Logger.Warn(ex, "初始化 Minecraft 材质失败")
            Return False
        End Try
    End Function

    Private Function GetAssetBytes(ResourcePath As String) As Byte()
        Try
            If String.IsNullOrWhiteSpace(ResourcePath) Then Return Nothing
            If MissCache.Contains(ResourcePath) Then Return Nothing
            Dim EntryName As String = "assets/" & ResourcePath.Replace("\", "/")
            For Each Zip In JarList
                Try
                    Dim Entry As ZipArchiveEntry = Zip.GetEntry(EntryName)
                    If Entry Is Nothing Then Continue For
                    Using Stream As IO.Stream = Entry.Open()
                        Using Memory As New IO.MemoryStream
                            Stream.CopyTo(Memory)
                            Return Memory.ToArray()
                        End Using
                    End Using
                Catch ex As Exception
                End Try
            Next
            MissCache.Add(ResourcePath)
            Return Nothing
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function GetJsonAsset(ResourcePath As String) As JObject
        Try
            Dim CacheKey As String = "json:" & ResourcePath
            If JsonCache.ContainsKey(CacheKey) Then Return JsonCache(CacheKey)
            Dim Data As Byte() = GetAssetBytes(ResourcePath)
            If Data Is Nothing Then
                JsonCache(CacheKey) = Nothing
                Return Nothing
            End If
            Dim Json As JObject = JObject.Parse(Encoding.UTF8.GetString(Data))
            JsonCache(CacheKey) = Json
            Return Json
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Public Function GetTextureImage(ResourcePath As String) As BitmapSource
        Try
            If String.IsNullOrWhiteSpace(ResourcePath) Then Return Nothing
            If TextureCache.ContainsKey(ResourcePath) Then Return TextureCache(ResourcePath)
            Dim Data As Byte() = GetAssetBytes(ResourcePath)
            If Data Is Nothing Then
                TextureCache(ResourcePath) = Nothing
                Return Nothing
            End If
            Dim Bitmap As New BitmapImage()
            Using Stream As New IO.MemoryStream(Data)
                Bitmap.BeginInit()
                Bitmap.CacheOption = BitmapCacheOption.OnLoad
                Bitmap.StreamSource = Stream
                Bitmap.EndInit()
            End Using
            Bitmap.Freeze()
            TextureCache(ResourcePath) = Bitmap
            Return Bitmap
        Catch ex As Exception
            TextureCache(ResourcePath) = Nothing
            Return Nothing
        End Try
    End Function

    Public Function GetBlockFaceTextures(BlockName As String) As Dictionary(Of Integer, String)
        Try
            Dim Name As String = NormalizeBlockName(BlockName)
            If Name = "" Then Return Nothing
            If BlockFaceCache.ContainsKey(Name) Then Return BlockFaceCache(Name)
            Dim Result As New Dictionary(Of Integer, String)
            Dim StateJson As JObject = GetJsonAsset("minecraft/blockstates/" & Name & ".json")
            If StateJson Is Nothing Then
                BlockFaceCache(Name) = Result
                Return Result
            End If
            Dim ModelRef As String = PickModelRef(StateJson)
            If ModelRef Is Nothing Then
                BlockFaceCache(Name) = Result
                Return Result
            End If

            Dim Merged As JObject = LoadMergedModel(ModelRef, 0)
            If Merged Is Nothing Then
                BlockFaceCache(Name) = Result
                Return Result
            End If
            Dim Textures As JObject = TryCast(Merged("textures"), JObject)

            ' 优先从模型 elements 的 faces 中取每个方向实际使用的材质
            If Merged("elements") IsNot Nothing Then
                Dim BestArea As New Dictionary(Of Integer, Double)
                For Each ElementToken As JToken In CType(Merged("elements"), JArray)
                    Dim Element As JObject = TryCast(ElementToken, JObject)
                    If Element Is Nothing OrElse Element("faces") Is Nothing Then Continue For
                    Dim Faces As JObject = CType(Element("faces"), JObject)
                    For Each FaceName As String In {"up", "down", "north", "south", "east", "west"}
                        Dim FaceToken As JToken = Faces(FaceName)
                        If FaceToken Is Nothing Then Continue For
                        Dim FaceObject As JObject = TryCast(FaceToken, JObject)
                        If FaceObject Is Nothing OrElse FaceObject("texture") Is Nothing Then Continue For
                        Dim Ref As String = FaceObject("texture").ToString()
                        Dim Direction As Integer = FaceDirection(FaceName)
                        Dim Area As Double = FaceArea(Element, FaceName)
                        If Not BestArea.ContainsKey(Direction) OrElse Area > BestArea(Direction) Then
                            BestArea(Direction) = Area
                            Dim TexturePath As String = ResolveTextureRef(Ref, Textures)
                            If TexturePath IsNot Nothing Then Result(Direction) = TexturePath
                        End If
                    Next
                Next
            End If

            ' 补全缺失方向
            Dim TopPath As String = ResolveTexture(Textures, {"top", "all", "particle"})
            Dim BottomPath As String = ResolveTexture(Textures, {"bottom", "all", "particle"})
            Dim SidePath As String = ResolveTexture(Textures, {"side", "all", "particle", "texture"})
            If SidePath Is Nothing Then SidePath = TopPath
            If TopPath Is Nothing Then TopPath = SidePath
            If BottomPath Is Nothing Then BottomPath = SidePath
            If Not Result.ContainsKey(0) Then Result(0) = TopPath
            If Not Result.ContainsKey(1) Then Result(1) = BottomPath
            For Direction As Integer = 2 To 5
                If Not Result.ContainsKey(Direction) Then Result(Direction) = SidePath
            Next
            Result = Result.Where(Function(p) p.Value IsNot Nothing).ToDictionary(Function(p) p.Key, Function(p) p.Value)
            If Result.Count = 0 Then
                Dim Direct As String = GuessBlockTexture(Name)
                If Direct IsNot Nothing Then
                    For Direction As Integer = 0 To 5
                        Result(Direction) = Direct
                    Next
                End If
            End If
            BlockFaceCache(Name) = Result
            Return Result
        Catch ex As Exception
            Logger.Warn(ex, "解析方块材质失败")
            Return Nothing
        End Try
    End Function

    Private Function PickModelRef(StateJson As JObject) As String
        If StateJson("variants") IsNot Nothing Then
            Dim Variants As JObject = CType(StateJson("variants"), JObject)
            Dim EmptyVariant As JToken = Variants("")
            If EmptyVariant IsNot Nothing Then
                Dim Ref As String = ExtractModelRef(EmptyVariant)
                If Ref IsNot Nothing Then Return Ref
            End If
            For Each Pair As JProperty In Variants.Properties()
                Dim Ref As String = ExtractModelRef(Pair.Value)
                If Ref IsNot Nothing Then Return Ref
            Next
        End If
        If StateJson("multipart") IsNot Nothing Then
            For Each Part As JToken In CType(StateJson("multipart"), JArray)
                Dim PartObject As JObject = TryCast(Part, JObject)
                If PartObject IsNot Nothing AndAlso PartObject("apply") IsNot Nothing Then
                    Dim Ref As String = ExtractModelRef(PartObject("apply"))
                    If Ref IsNot Nothing Then Return Ref
                End If
            Next
        End If
        Return Nothing
    End Function

    Private Function ExtractModelRef(Token As JToken) As String
        If Token Is Nothing Then Return Nothing
        If Token.Type = JTokenType.String Then Return Token.ToString()
        If Token.Type = JTokenType.Object Then
            Dim ModelToken As JToken = CType(Token, JObject)("model")
            If ModelToken IsNot Nothing Then Return ModelToken.ToString()
        End If
        If Token.Type = JTokenType.Array AndAlso CType(Token, JArray).Count > 0 Then Return ExtractModelRef(CType(Token, JArray)(0))
        Return Nothing
    End Function

    Private Function LoadMergedModel(ModelRef As String, Depth As Integer) As JObject
        Try
            If Depth > 8 Then Return Nothing
            Dim Path As String = ModelRef
            If Path.Contains(":") Then Path = Path.Substring(Path.IndexOf(":"c) + 1)
            If Not Path.StartsWithF("models/", True) Then Path = "models/" & Path
            Dim Json As JObject = GetJsonAsset("minecraft/" & Path & ".json")
            If Json Is Nothing Then Return Nothing
            Dim Result As New JObject
            If Json("parent") IsNot Nothing Then
                Dim Parent As JObject = LoadMergedModel(Json("parent").ToString(), Depth + 1)
                If Parent IsNot Nothing Then Result = CType(Parent.DeepClone(), JObject)
            End If
            If Json("textures") IsNot Nothing Then
                If Result("textures") Is Nothing OrElse CType(Result("textures"), JObject) Is Nothing Then
                    Result("textures") = New JObject()
                End If
                Dim Target As JObject = CType(Result("textures"), JObject)
                For Each Pair As JProperty In CType(Json("textures"), JObject).Properties()
                    Target(Pair.Name) = Pair.Value.ToString()
                Next
            End If
            If Json("elements") IsNot Nothing Then Result("elements") = Json("elements").DeepClone()
            Return Result
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function ResolveTextureRef(Ref As String, Textures As JObject) As String
        If String.IsNullOrWhiteSpace(Ref) Then Return Nothing
        Dim Value As String = Ref.Trim()
        Dim Guard As Integer = 0
        Do While Value.StartsWith("#") AndAlso Guard < 8
            If Textures Is Nothing Then Return Nothing
            Dim NextToken As JToken = Textures(Value.Substring(1))
            If NextToken Is Nothing Then Return Nothing
            Value = NextToken.ToString()
            Guard += 1
        Loop
        If Value.StartsWith("#") Then Return Nothing
        If Value.Contains(":") Then Value = Value.Substring(Value.IndexOf(":"c) + 1)
        Return "minecraft/textures/" & Value & ".png"
    End Function

    Private Function FaceDirection(FaceName As String) As Integer
        Select Case FaceName
            Case "up" : Return 0
            Case "down" : Return 1
            Case "north" : Return 2
            Case "south" : Return 3
            Case "west" : Return 4
            Case Else : Return 5
        End Select
    End Function

    Private Function FaceArea(Element As JObject, FaceName As String) As Double
        Try
            Dim FromToken As JObject = TryCast(Element("from"), JObject)
            Dim ToToken As JObject = TryCast(Element("to"), JObject)
            Dim X1 As Double = If(FromToken IsNot Nothing AndAlso FromToken("x") IsNot Nothing, FromToken("x").ToObject(Of Double)(), 0)
            Dim Y1 As Double = If(FromToken IsNot Nothing AndAlso FromToken("y") IsNot Nothing, FromToken("y").ToObject(Of Double)(), 0)
            Dim Z1 As Double = If(FromToken IsNot Nothing AndAlso FromToken("z") IsNot Nothing, FromToken("z").ToObject(Of Double)(), 0)
            Dim X2 As Double = If(ToToken IsNot Nothing AndAlso ToToken("x") IsNot Nothing, ToToken("x").ToObject(Of Double)(), 16)
            Dim Y2 As Double = If(ToToken IsNot Nothing AndAlso ToToken("y") IsNot Nothing, ToToken("y").ToObject(Of Double)(), 16)
            Dim Z2 As Double = If(ToToken IsNot Nothing AndAlso ToToken("z") IsNot Nothing, ToToken("z").ToObject(Of Double)(), 16)
            Select Case FaceName
                Case "up", "down" : Return Math.Abs((X2 - X1) * (Z2 - Z1))
                Case "north", "south" : Return Math.Abs((X2 - X1) * (Y2 - Y1))
                Case Else : Return Math.Abs((Z2 - Z1) * (Y2 - Y1))
            End Select
        Catch ex As Exception
            Return 0
        End Try
    End Function

    Private Function ResolveTexture(Textures As JObject, Keys As String()) As String
        If Textures Is Nothing Then Return Nothing
        For Each Key In Keys
            Dim Token As JToken = Textures(Key)
            If Token Is Nothing Then Continue For
            Dim Path As String = ResolveTextureRef(Token.ToString(), Textures)
            If Path IsNot Nothing Then Return Path
        Next
        Return Nothing
    End Function

    Public Function GetJsonAssetPublic(ResourcePath As String) As JObject
        Return GetJsonAsset(ResourcePath)
    End Function

    Public Function LoadMergedModelPublic(ModelRef As String) As JObject
        Return LoadMergedModel(ModelRef, 0)
    End Function

    Public Function ResolveTextureRefPublic(Ref As String, Textures As JObject) As String
        Return ResolveTextureRef(Ref, Textures)
    End Function

    Public Function GuessBlockTexturePublic(BlockName As String) As String
        Return GuessBlockTexture(NormalizeBlockName(BlockName))
    End Function

    Public Sub TrimCaches(MaxTextures As Integer, MaxFaces As Integer)
        Try
            If TextureCache.Count > MaxTextures Then TextureCache.Clear()
            If BlockFaceCache.Count > MaxFaces Then BlockFaceCache.Clear()
        Catch ex As Exception
        End Try
    End Sub

    Public Sub ResetTextureCaches()
        TextureCache.Clear()
        BlockFaceCache.Clear()
    End Sub

    Private Function GuessBlockTexture(Name As String) As String
        Dim Candidates As New List(Of String) From {
            "minecraft/textures/block/" & Name & ".png",
            "minecraft/textures/blocks/" & Name & ".png",
            "minecraft/textures/block/" & Name & "_top.png",
            "minecraft/textures/blocks/" & Name & "_top.png",
            "minecraft/textures/block/" & Name & "_side.png",
            "minecraft/textures/blocks/" & Name & "_side.png",
            "minecraft/textures/block/" & Name & "_front.png",
            "minecraft/textures/block/" & Name & "_still.png",
            "minecraft/textures/blocks/" & Name & "_still.png"}
        If Name.EndsWith("_stairs") Then
            Candidates.Insert(0, "minecraft/textures/block/" & Name.Substring(0, Name.Length - 7) & ".png")
            Candidates.Insert(1, "minecraft/textures/blocks/" & Name.Substring(0, Name.Length - 7) & ".png")
        End If
        If Name.EndsWith("_slab") Then
            Candidates.Insert(0, "minecraft/textures/block/" & Name.Substring(0, Name.Length - 5) & ".png")
            Candidates.Insert(1, "minecraft/textures/blocks/" & Name.Substring(0, Name.Length - 5) & ".png")
        End If
        If Name.Contains("stained_glass") Then
            Dim ColorPart As String = Name.Substring(0, Name.IndexOf("_stained_glass", StringComparison.OrdinalIgnoreCase))
            Candidates.Insert(0, "minecraft/textures/block/" & ColorPart & "_stained_glass.png")
            Candidates.Insert(1, "minecraft/textures/blocks/glass_" & ColorPart & ".png")
            Candidates.Insert(2, "minecraft/textures/block/glass.png")
            Candidates.Insert(3, "minecraft/textures/blocks/glass.png")
        End If
        For Each Candidate In Candidates
            If GetTextureImage(Candidate) IsNot Nothing Then Return Candidate
        Next
        Return Nothing
    End Function

    Private Function NormalizeBlockName(BlockName As String) As String
        Dim Name As String = If(BlockName, "").Trim()
        If Name.Contains(":") Then Name = Name.Substring(Name.IndexOf(":"c) + 1)
        Return Name.ToLower()
    End Function

End Module
