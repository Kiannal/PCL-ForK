Imports System.Drawing

Public Module ModLitematic

    Public Class LitematicInfo
        Public Property FilePath As String = ""
        Public Property Name As String = ""
        Public Property Author As String = ""
        Public Property Description As String = ""
        Public Property EnclosingX As Integer
        Public Property EnclosingY As Integer
        Public Property EnclosingZ As Integer
        Public Property TotalBlocks As Long
        Public Property TotalVolume As Long
        Public Property RegionCount As Integer
        Public Property SampleStep As Integer = 1
        Public Property GridX As Integer
        Public Property GridY As Integer
        Public Property GridZ As Integer
        Public Property Voxels As Integer(,,) = Nothing
        Public Property PaletteNames As New List(Of String)
        Public Property PaletteStates As New List(Of String)
        Public Property PaletteColors As New List(Of Color)
    End Class

    Public Function Load(FilePath As String, Optional MaxCells As Integer = 420000) As LitematicInfo
        Dim Info As New LitematicInfo With {.FilePath = FilePath}
        Dim Nbt As ModNbt.NbtFile = ModNbt.ReadFile(FilePath)
        Dim Root As ModNbt.NbtCompound = TryCast(Nbt.Root.Value, ModNbt.NbtCompound)
        If Root Is Nothing Then Throw New Exception("不是有效的投影文件（NBT 解析失败）")
        Info.PaletteNames.Add("minecraft:air")
        Info.PaletteStates.Add("")
        Info.PaletteColors.Add(Color.Transparent)

        Dim Meta As ModNbt.NbtCompound = ModNbt.GetCompoundChild(Root, "Metadata")
        If Meta IsNot Nothing Then
            Info.Name = GetString(Meta, "Name")
            Info.Author = GetString(Meta, "Author")
            Info.Description = GetString(Meta, "Description")
            Info.TotalBlocks = GetLong(Meta, "TotalBlocks")
            Info.TotalVolume = GetLong(Meta, "TotalVolume")
            Dim Enclosing As ModNbt.NbtCompound = ModNbt.GetCompoundChild(Meta, "EnclosingSize")
            If Enclosing IsNot Nothing Then
                Info.EnclosingX = Math.Abs(GetInt(Enclosing, "x"))
                Info.EnclosingY = Math.Abs(GetInt(Enclosing, "y"))
                Info.EnclosingZ = Math.Abs(GetInt(Enclosing, "z"))
            End If
        End If
        If Info.Name = "" Then Info.Name = IO.Path.GetFileNameWithoutExtension(FilePath)

        Dim Regions As ModNbt.NbtCompound = ModNbt.GetCompoundChild(Root, "Regions")
        If Regions Is Nothing Then Throw New Exception("投影文件没有区域数据")
        Info.RegionCount = Regions.Tags.Count

        Dim MinX, MinY, MinZ As Integer
        Dim MaxX, MaxY, MaxZ As Integer
        Dim FirstRegion As Boolean = True
        Dim Parsed As New List(Of RegionData)
        For Each RegionTag As ModNbt.NbtTag In Regions.Tags
            Dim Region As ModNbt.NbtCompound = TryCast(RegionTag.Value, ModNbt.NbtCompound)
            If Region Is Nothing Then Continue For
            Dim Position As ModNbt.NbtCompound = ModNbt.GetCompoundChild(Region, "Position")
            Dim Size As ModNbt.NbtCompound = ModNbt.GetCompoundChild(Region, "Size")
            If Position Is Nothing OrElse Size Is Nothing Then Continue For
            Dim RegionInfo As New RegionData With {
                .Region = Region,
                .PX = GetInt(Position, "x"),
                .PY = GetInt(Position, "y"),
                .PZ = GetInt(Position, "z"),
                .SX = GetInt(Size, "x"),
                .SY = GetInt(Size, "y"),
                .SZ = GetInt(Size, "z")}
            RegionInfo.X0 = RegionInfo.PX + If(RegionInfo.SX < 0, RegionInfo.SX + 1, 0)
            RegionInfo.Y0 = RegionInfo.PY + If(RegionInfo.SY < 0, RegionInfo.SY + 1, 0)
            RegionInfo.Z0 = RegionInfo.PZ + If(RegionInfo.SZ < 0, RegionInfo.SZ + 1, 0)
            RegionInfo.AX = Math.Abs(RegionInfo.SX)
            RegionInfo.AY = Math.Abs(RegionInfo.SY)
            RegionInfo.AZ = Math.Abs(RegionInfo.SZ)
            If FirstRegion Then
                MinX = RegionInfo.X0 : MinY = RegionInfo.Y0 : MinZ = RegionInfo.Z0
                MaxX = RegionInfo.X0 + RegionInfo.AX - 1 : MaxY = RegionInfo.Y0 + RegionInfo.AY - 1 : MaxZ = RegionInfo.Z0 + RegionInfo.AZ - 1
                FirstRegion = False
            Else
                MinX = Math.Min(MinX, RegionInfo.X0) : MinY = Math.Min(MinY, RegionInfo.Y0) : MinZ = Math.Min(MinZ, RegionInfo.Z0)
                MaxX = Math.Max(MaxX, RegionInfo.X0 + RegionInfo.AX - 1) : MaxY = Math.Max(MaxY, RegionInfo.Y0 + RegionInfo.AY - 1) : MaxZ = Math.Max(MaxZ, RegionInfo.Z0 + RegionInfo.AZ - 1)
            End If
            Parsed.Add(RegionInfo)
        Next
        If FirstRegion Then Throw New Exception("投影文件没有可用的区域数据")

        Dim BoundsX As Integer = MaxX - MinX + 1
        Dim BoundsY As Integer = MaxY - MinY + 1
        Dim BoundsZ As Integer = MaxZ - MinZ + 1
        Dim SampleStep As Integer = 1
        Do While (BoundsX \ SampleStep + 1) * (BoundsY \ SampleStep + 1) * (BoundsZ \ SampleStep + 1) > MaxCells
            SampleStep += 1
            If SampleStep > 32 Then Exit Do
        Loop
        Info.SampleStep = SampleStep
        Info.GridX = Math.Max(1, BoundsX \ SampleStep)
        Info.GridY = Math.Max(1, BoundsY \ SampleStep)
        Info.GridZ = Math.Max(1, BoundsZ \ SampleStep)
        Info.Voxels = New Integer(Info.GridX - 1, Info.GridY - 1, Info.GridZ - 1) {}
        If Info.EnclosingX = 0 Then Info.EnclosingX = BoundsX
        If Info.EnclosingY = 0 Then Info.EnclosingY = BoundsY
        If Info.EnclosingZ = 0 Then Info.EnclosingZ = BoundsZ

        Dim NameToId As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For Each RegionInfo In Parsed
            ParseRegion(Info, RegionInfo, MinX, MinY, MinZ, NameToId)
        Next
        Return Info
    End Function

    Private Class RegionData
        Public Region As ModNbt.NbtCompound
        Public PX, PY, PZ As Integer
        Public SX, SY, SZ As Integer
        Public X0, Y0, Z0 As Integer
        Public AX, AY, AZ As Integer
    End Class

    Private Sub ParseRegion(Info As LitematicInfo, RegionInfo As RegionData, MinX As Integer, MinY As Integer, MinZ As Integer,
                            NameToId As Dictionary(Of String, Integer))
        Dim Palette As ModNbt.NbtList = TryCast(RegionInfo.Region.Item("BlockStatePalette")?.Value, ModNbt.NbtList)
        If Palette Is Nothing Then Return
        Dim States As List(Of Long) = TryCast(RegionInfo.Region.Item("BlockStates")?.Value, List(Of Long))
        If States Is Nothing Then Return
        Dim PaletteCount As Integer = Palette.Items.Count
        If PaletteCount = 0 Then Return
        Dim Bits As Integer = Math.Max(2, CInt(Math.Ceiling(Math.Log(PaletteCount, 2))))
        If Bits > 32 Then Return
        Dim EntriesPerLong As Integer = 64 \ Bits
        Dim Mask As ULong = (1UL << Bits) - 1UL
        Dim LocalIds(PaletteCount - 1) As Integer
        For i = 0 To PaletteCount - 1
            Dim Entry As ModNbt.NbtCompound = TryCast(Palette.Items(i), ModNbt.NbtCompound)
            Dim Name As String = "minecraft:air"
            Dim StateText As String = ""
            If Entry IsNot Nothing Then
                Dim NameTag As ModNbt.NbtTag = Entry.Item("Name")
                If NameTag IsNot Nothing AndAlso NameTag.Value IsNot Nothing Then Name = NameTag.Value.ToString()
                Dim PropertiesTag As ModNbt.NbtTag = Entry.Item("Properties")
                If PropertiesTag IsNot Nothing Then
                    Dim Properties As ModNbt.NbtCompound = TryCast(PropertiesTag.Value, ModNbt.NbtCompound)
                    If Properties IsNot Nothing Then
                        StateText = Properties.Tags.OrderBy(Function(tag) tag.Name, StringComparer.OrdinalIgnoreCase).
                            Select(Function(tag) $"{tag.Name}={tag.Value}").Join(",")
                    End If
                End If
            End If
            If String.IsNullOrEmpty(Name) OrElse Name = "minecraft:air" OrElse Name = "air" Then
                LocalIds(i) = 0
            Else
                Dim StateKey As String = Name & "|" & StateText
                Dim GlobalId As Integer
                If Not NameToId.TryGetValue(StateKey, GlobalId) Then
                    GlobalId = Info.PaletteNames.Count
                    NameToId(StateKey) = GlobalId
                    Info.PaletteNames.Add(Name)
                    Info.PaletteStates.Add(StateText)
                    Info.PaletteColors.Add(GetBlockColor(Name))
                End If
                LocalIds(i) = GlobalId
            End If
        Next
        Dim TotalBlocks As Long = CLng(RegionInfo.AX) * RegionInfo.AY * RegionInfo.AZ
        Dim Index As Long = 0
        For Y = 0 To RegionInfo.AY - 1
            For Z = 0 To RegionInfo.AZ - 1
                For X = 0 To RegionInfo.AX - 1
                    Dim StartBit As Long = Index * Bits
                    Dim LongIndex As Integer = CInt(StartBit \ 64)
                    If LongIndex >= States.Count Then Exit For
                    Dim BitOffset As Integer = CInt(StartBit Mod 64)
                    Dim Raw As ULong = BitConverter.ToUInt64(BitConverter.GetBytes(States(LongIndex)), 0) >> BitOffset
                    If BitOffset + Bits > 64 AndAlso LongIndex + 1 < States.Count Then
                        Raw = Raw Or (BitConverter.ToUInt64(BitConverter.GetBytes(States(LongIndex + 1)), 0) << (64 - BitOffset))
                    End If
                    Dim Value As Integer = CInt(Raw And Mask)
                    Dim WorldX As Integer = RegionInfo.X0 + X
                    Dim WorldY As Integer = RegionInfo.Y0 + Y
                    Dim WorldZ As Integer = RegionInfo.Z0 + Z
                    Dim GridX As Integer = (WorldX - MinX) \ Info.SampleStep
                    Dim GridY As Integer = (WorldY - MinY) \ Info.SampleStep
                    Dim GridZ As Integer = (WorldZ - MinZ) \ Info.SampleStep
                    If GridX >= 0 AndAlso GridY >= 0 AndAlso GridZ >= 0 AndAlso
                       GridX < Info.GridX AndAlso GridY < Info.GridY AndAlso GridZ < Info.GridZ Then
                        If Value >= 0 AndAlso Value < LocalIds.Length Then
                            Info.Voxels(GridX, GridY, GridZ) = LocalIds(Value)
                        End If
                    End If
                    Index += 1
                Next
            Next
            If Index >= TotalBlocks Then Exit For
        Next
    End Sub

    Private Function GetInt(Compound As ModNbt.NbtCompound, Key As String) As Integer
        Try
            Dim Value As Object = ModNbt.GetValue(Compound, Key)
            If Value Is Nothing Then Return 0
            Return CInt(Value)
        Catch ex As Exception
            Return 0
        End Try
    End Function

    Private Function GetLong(Compound As ModNbt.NbtCompound, Key As String) As Long
        Try
            Dim Value As Object = ModNbt.GetValue(Compound, Key)
            If Value Is Nothing Then Return 0
            Return CLng(Value)
        Catch ex As Exception
            Return 0
        End Try
    End Function

    Private Function GetString(Compound As ModNbt.NbtCompound, Key As String) As String
        Try
            Dim Value As Object = ModNbt.GetValue(Compound, Key)
            If Value Is Nothing Then Return ""
            Return Value.ToString()
        Catch ex As Exception
            Return ""
        End Try
    End Function

    Public Function GetBlockColor(Name As String) As Color
        Dim N As String = If(Name, "").ToLower().Replace("minecraft:", "")
        Select Case True
            Case N.Contains("water") : Return Color.FromArgb(70, 120, 200)
            Case N.Contains("lava") : Return Color.FromArgb(220, 100, 20)
            Case N.Contains("glass") : Return Color.FromArgb(180, 220, 235)
            Case N.Contains("leaves") OrElse N.Contains("azalea_leaves") : Return Color.FromArgb(60, 130, 50)
            Case N.Contains("grass_block") OrElse N.Contains("moss") : Return Color.FromArgb(90, 150, 70)
            Case N.Contains("dirt") OrElse N.Contains("mud") : Return Color.FromArgb(130, 90, 60)
            Case N.Contains("sand") OrElse N.Contains("sandstone") : Return Color.FromArgb(225, 210, 160)
            Case N.Contains("stone") OrElse N.Contains("cobble") OrElse N.Contains("andesite") OrElse N.Contains("diorite") OrElse N.Contains("granite") : Return Color.FromArgb(130, 130, 130)
            Case N.Contains("deepslate") OrElse N.Contains("blackstone") OrElse N.Contains("obsidian") OrElse N.Contains("basalt") : Return Color.FromArgb(70, 70, 80)
            Case N.Contains("netherrack") OrElse N.Contains("nether_brick") : Return Color.FromArgb(120, 50, 50)
            Case N.Contains("quartz") OrElse N.Contains("white_") : Return Color.FromArgb(235, 235, 230)
            Case N.Contains("oak") OrElse N.Contains("spruce") OrElse N.Contains("wood") OrElse N.Contains("planks") OrElse N.Contains("log") : Return Color.FromArgb(170, 135, 90)
            Case N.Contains("iron") : Return Color.FromArgb(200, 200, 205)
            Case N.Contains("gold") : Return Color.FromArgb(240, 200, 70)
            Case N.Contains("diamond") : Return Color.FromArgb(90, 220, 220)
            Case N.Contains("emerald") : Return Color.FromArgb(60, 200, 120)
            Case N.Contains("redstone") : Return Color.FromArgb(200, 40, 40)
            Case N.Contains("copper") : Return Color.FromArgb(200, 120, 90)
            Case N.Contains("brick") : Return Color.FromArgb(170, 90, 80)
            Case N.Contains("concrete") OrElse N.Contains("terracotta") OrElse N.Contains("wool") OrElse N.Contains("carpet") : Return GetColorBlockColor(N)
            Case N.Contains("torch") : Return Color.FromArgb(240, 220, 150)
            Case N.Contains("snow") OrElse N.Contains("ice") : Return Color.FromArgb(230, 240, 250)
            Case N.Contains("cactus") : Return Color.FromArgb(70, 140, 70)
            Case N.Contains("pumpkin") OrElse N.Contains("melon") : Return Color.FromArgb(220, 160, 60)
            Case N.Contains("wheat") OrElse N.Contains("crop") : Return Color.FromArgb(180, 200, 90)
            Case N.Contains("air") : Return Color.Transparent
            Case Else : Return Color.FromArgb(150, 150, 155)
        End Select
    End Function

    Private Function GetColorBlockColor(Name As String) As Color
        Dim Colors As New Dictionary(Of String, Color) From {
            {"white", Color.FromArgb(235, 235, 235)}, {"orange", Color.FromArgb(230, 130, 40)}, {"magenta", Color.FromArgb(200, 80, 200)},
            {"light_blue", Color.FromArgb(110, 180, 230)}, {"yellow", Color.FromArgb(240, 220, 60)}, {"lime", Color.FromArgb(120, 200, 40)},
            {"pink", Color.FromArgb(240, 160, 190)}, {"gray", Color.FromArgb(90, 95, 100)}, {"light_gray", Color.FromArgb(175, 175, 170)},
            {"cyan", Color.FromArgb(50, 150, 160)}, {"purple", Color.FromArgb(130, 60, 190)}, {"blue", Color.FromArgb(60, 80, 190)},
            {"brown", Color.FromArgb(120, 80, 50)}, {"green", Color.FromArgb(80, 120, 40)}, {"red", Color.FromArgb(180, 50, 50)},
            {"black", Color.FromArgb(35, 35, 40)}}
        For Each Pair In Colors
            If Name.Contains(Pair.Key) Then Return Pair.Value
        Next
        Return Color.FromArgb(160, 160, 160)
    End Function

End Module
