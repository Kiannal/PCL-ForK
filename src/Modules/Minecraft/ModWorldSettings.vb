Public Module ModWorldSettings

    Public Class WorldInfo
        Public Property Name As String = ""
        Public Property Folder As String = ""
        Public Property LevelPath As String = ""
        Public Property LastPlayed As Date = Date.MinValue
        Public Property Seed As String = ""
        Public Property GameMode As Integer = 0
        Public Property Difficulty As Integer = 2
        Public Property AllowCommands As Boolean = False
        Public Overrides Function ToString() As String
            Return Name
        End Function
    End Class

    Public Function GetWorlds(Instance As McInstance) As List(Of WorldInfo)
        Dim Result As New List(Of WorldInfo)
        Try
            If Instance Is Nothing Then Return Result
            Dim SavesFolder As String = Instance.PathIndie & "saves\"
            If Not DirectoryUtils.Exists(SavesFolder) Then Return Result
            For Each Folder As String In IO.Directory.GetDirectories(SavesFolder)
                Dim LevelPath As String = IO.Path.Combine(Folder, "level.dat")
                If Not FileUtils.Exists(LevelPath) Then Continue For
                Dim Info As New WorldInfo With {
                    .Folder = Folder,
                    .LevelPath = LevelPath,
                    .Name = IO.Path.GetFileName(Folder),
                    .LastPlayed = New IO.FileInfo(LevelPath).LastWriteTime}
                Try
                    ReadWorldInfo(Info)
                Catch ex As Exception
                    Logger.Warn(ex, "读取存档信息失败")
                End Try
                Result.Add(Info)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "扫描存档失败")
        End Try
        Return Result.OrderByDescending(Function(w) w.LastPlayed).ToList()
    End Function

    Private Sub ReadWorldInfo(Info As WorldInfo)
        Dim Nbt As ModNbt.NbtFile = ModNbt.ReadFile(Info.LevelPath)
        Dim Data As ModNbt.NbtCompound = ModNbt.GetCompound(Nbt.Root, "Data")
        If Data Is Nothing Then Return
        Dim LevelName As Object = ModNbt.GetValue(Data, "LevelName")
        If LevelName IsNot Nothing Then Info.Name = LevelName.ToString()
        Dim SeedValue As Object = ModNbt.GetValue(Data, "RandomSeed")
        If SeedValue Is Nothing Then
            Dim WorldGen As ModNbt.NbtCompound = ModNbt.GetCompoundChild(Data, "WorldGenSettings")
            If WorldGen IsNot Nothing Then SeedValue = ModNbt.GetValue(WorldGen, "seed")
        End If
        If SeedValue IsNot Nothing Then Info.Seed = SeedValue.ToString()
        Dim GameType As Object = ModNbt.GetValue(Data, "GameType")
        If GameType IsNot Nothing Then Info.GameMode = CInt(GameType)
        Dim DifficultyValue As Object = ModNbt.GetValue(Data, "Difficulty")
        If DifficultyValue IsNot Nothing Then Info.Difficulty = CInt(DifficultyValue)
        Dim Allow As Object = ModNbt.GetValue(Data, "allowCommands")
        If Allow IsNot Nothing Then Info.AllowCommands = CByte(Allow) <> 0
    End Sub

    Public Function ApplyWorldSettings(Info As WorldInfo, GameMode As Integer, Difficulty As Integer, AllowCommands As Boolean) As String
        Try
            If Info Is Nothing Then Return "没有选择存档"
            Dim Nbt As ModNbt.NbtFile = ModNbt.ReadFile(Info.LevelPath)
            Dim Data As ModNbt.NbtCompound = ModNbt.GetCompound(Nbt.Root, "Data")
            If Data Is Nothing Then Return "level.dat 中找不到 Data 数据"
            ModNbt.SetValue(Data, "GameType", 3, GameMode)
            ModNbt.SetValue(Data, "Difficulty", 1, CByte(Difficulty))
            ModNbt.SetValue(Data, "allowCommands", 1, CByte(If(AllowCommands, 1, 0)))
            ModNbt.WriteFile(Info.LevelPath, Nbt)
            Info.GameMode = GameMode
            Info.Difficulty = Difficulty
            Info.AllowCommands = AllowCommands
            Logger.Info($"已修改存档设置：{Info.Name}，模式 {GameMode}，难度 {Difficulty}，允许作弊 {AllowCommands}")
            Return Nothing
        Catch ex As Exception
            Logger.Error(ex, "修改存档设置失败")
            Return ex.Message
        End Try
    End Function

End Module
