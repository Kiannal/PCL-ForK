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
        Public Property IsNewFormat As Boolean = False
        Public Overrides Function ToString() As String
            Return Name
        End Function
    End Class

    ''' <summary>
    ''' 26.1 起的难度以字符串标识符存储，转回 0-3 的数值。
    ''' </summary>
    Private Function DifficultyFromName(Name As String) As Integer
        Select Case If(Name, "").Trim().ToLowerInvariant()
            Case "peaceful" : Return 0
            Case "easy" : Return 1
            Case "normal" : Return 2
            Case "hard" : Return 3
            Case Else : Return 2
        End Select
    End Function

    ''' <summary>
    ''' 数值难度转回 26.1 使用的字符串标识符。
    ''' </summary>
    Private Function DifficultyToName(Value As Integer) As String
        Select Case Value
            Case 0 : Return "peaceful"
            Case 1 : Return "easy"
            Case 3 : Return "hard"
            Case Else : Return "normal"
        End Select
    End Function

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

    ''' <summary>
    ''' 26.1 起种子搬到了 data/minecraft/world_gen_settings.dat，且根节点包在 data 复合标签里。
    ''' </summary>
    Private Function ReadSeedFromSeparateFile(Folder As String) As String
        Try
            Dim Path As String = IO.Path.Combine(Folder, "data", "minecraft", "world_gen_settings.dat")
            If Not FileUtils.Exists(Path) Then Return ""
            Dim Nbt As ModNbt.NbtFile = ModNbt.ReadFile(Path)
            If Nbt Is Nothing OrElse Nbt.Root Is Nothing Then Return ""
            Dim Root As ModNbt.NbtCompound = TryCast(Nbt.Root.Value, ModNbt.NbtCompound)
            If Root Is Nothing Then Return ""
            Dim Wrap As ModNbt.NbtCompound = ModNbt.GetCompoundChild(Root, "data")
            Dim Holder As ModNbt.NbtCompound = If(Wrap, Root)
            Dim SeedValue As Object = ModNbt.GetValue(Holder, "seed")
            If SeedValue Is Nothing Then Return ""
            Return SeedValue.ToString()
        Catch ex As Exception
            Logger.Warn(ex, "读取 world_gen_settings.dat 失败")
            Return ""
        End Try
    End Function

    Private Sub ReadWorldInfo(Info As WorldInfo)
        Dim Nbt As ModNbt.NbtFile = ModNbt.ReadFile(Info.LevelPath)
        Dim Data As ModNbt.NbtCompound = ModNbt.GetCompound(Nbt.Root, "Data")
        If Data Is Nothing Then Return
        Dim LevelName As Object = ModNbt.GetValue(Data, "LevelName")
        If LevelName IsNot Nothing Then Info.Name = LevelName.ToString()
        '26.1 起的世界设置容器
        Dim DiffSettings As ModNbt.NbtCompound = ModNbt.GetCompoundChild(Data, "difficulty_settings")
        Info.IsNewFormat = DiffSettings IsNot Nothing
        '种子：先找 level.dat，再找新版独立文件
        Dim SeedValue As Object = ModNbt.GetValue(Data, "RandomSeed")
        If SeedValue Is Nothing Then
            Dim WorldGen As ModNbt.NbtCompound = ModNbt.GetCompoundChild(Data, "WorldGenSettings")
            If WorldGen IsNot Nothing Then SeedValue = ModNbt.GetValue(WorldGen, "seed")
        End If
        If SeedValue IsNot Nothing Then
            Info.Seed = SeedValue.ToString()
        Else
            Info.Seed = ReadSeedFromSeparateFile(Info.Folder)
        End If
        Dim GameType As Object = ModNbt.GetValue(Data, "GameType")
        If GameType IsNot Nothing Then Info.GameMode = CInt(GameType)
        '难度：新版读子复合标签里的字符串，旧版读字节
        If DiffSettings IsNot Nothing Then
            Dim DiffName As Object = ModNbt.GetValue(DiffSettings, "difficulty")
            If DiffName IsNot Nothing Then
                Info.Difficulty = DifficultyFromName(DiffName.ToString())
            Else
                Info.Difficulty = 2
            End If
        Else
            Dim DifficultyValue As Object = ModNbt.GetValue(Data, "Difficulty")
            If DifficultyValue IsNot Nothing Then Info.Difficulty = CInt(DifficultyValue)
        End If
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
            '难度按存档格式分流写入
            Dim DiffSettings As ModNbt.NbtCompound = ModNbt.GetCompoundChild(Data, "difficulty_settings")
            If DiffSettings IsNot Nothing Then
                ModNbt.SetValue(DiffSettings, "difficulty", 8, DifficultyToName(Difficulty))
            Else
                ModNbt.SetValue(Data, "Difficulty", 1, CByte(Difficulty))
            End If
            ModNbt.SetValue(Data, "allowCommands", 1, CByte(If(AllowCommands, 1, 0)))
            ModNbt.WriteFile(Info.LevelPath, Nbt)
            Info.GameMode = GameMode
            Info.Difficulty = Difficulty
            Info.AllowCommands = AllowCommands
            Logger.Info($"已修改存档设置：{Info.Name}，模式 {GameMode}，难度 {Difficulty}，允许作弊 {AllowCommands}，新格式 {Info.IsNewFormat}")
            Return Nothing
        Catch ex As Exception
            Logger.Error(ex, "修改存档设置失败")
            Return ex.Message
        End Try
    End Function

End Module
