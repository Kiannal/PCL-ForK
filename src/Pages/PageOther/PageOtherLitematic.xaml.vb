Imports System.Windows.Media.Media3D

Public Class PageOtherLitematic

    Private Info As ModLitematic.LitematicInfo = Nothing
    Private Yaw As Double = 40
    Private Pitch As Double = 28
    Private Distance As Double = 120

    Private IsDragging As Boolean = False
    Private LastMousePoint As Point
    Private IsLoading As Boolean = False

    Private OnlineMode As Boolean = False
    Private IsFetchingOnline As Boolean = False
    Private IsFillingLayer As Boolean = False
    Private SceneLayer As Integer = -1
    Private ReadOnly FilteredOnline As New List(Of OnlineLitematicItem)
    Private Const MaxDisplayedOnline As Integer = 120

    Private Class MeshBuilder
        Public ReadOnly Positions As New List(Of Point3D)
        Public ReadOnly Normals As New List(Of Vector3D)
        Public ReadOnly Indices As New List(Of Integer)
        Public ReadOnly TextureCoordinates As New List(Of Point)
        Public Sub AddFace(X As Integer, Y As Integer, Z As Integer, Direction As Integer, UV As Point())
            Dim Corners As Point3D()
            Dim Normal As Vector3D
            Select Case Direction
                Case 0 '上
                    Corners = {New Point3D(X, Y + 1, Z), New Point3D(X, Y + 1, Z + 1), New Point3D(X + 1, Y + 1, Z + 1), New Point3D(X + 1, Y + 1, Z)}
                    Normal = New Vector3D(0, 1, 0)
                Case 1 '下
                    Corners = {New Point3D(X, Y, Z), New Point3D(X + 1, Y, Z), New Point3D(X + 1, Y, Z + 1), New Point3D(X, Y, Z + 1)}
                    Normal = New Vector3D(0, -1, 0)
                Case 2 '北
                    Corners = {New Point3D(X, Y, Z), New Point3D(X, Y + 1, Z), New Point3D(X + 1, Y + 1, Z), New Point3D(X + 1, Y, Z)}
                    Normal = New Vector3D(0, 0, -1)
                Case 3 '南
                    Corners = {New Point3D(X, Y, Z + 1), New Point3D(X + 1, Y, Z + 1), New Point3D(X + 1, Y + 1, Z + 1), New Point3D(X, Y + 1, Z + 1)}
                    Normal = New Vector3D(0, 0, 1)
                Case 4 '西
                    Corners = {New Point3D(X, Y, Z), New Point3D(X, Y, Z + 1), New Point3D(X, Y + 1, Z + 1), New Point3D(X, Y + 1, Z)}
                    Normal = New Vector3D(-1, 0, 0)
                Case Else '东
                    Corners = {New Point3D(X + 1, Y, Z), New Point3D(X + 1, Y + 1, Z), New Point3D(X + 1, Y + 1, Z + 1), New Point3D(X + 1, Y, Z + 1)}
                    Normal = New Vector3D(1, 0, 0)
            End Select
            Dim BaseIndex As Integer = Positions.Count
            For i = 0 To 3
                Positions.Add(Corners(i))
                Normals.Add(Normal)
                TextureCoordinates.Add(UV(i))
            Next
            Indices.Add(BaseIndex) : Indices.Add(BaseIndex + 1) : Indices.Add(BaseIndex + 2)
            Indices.Add(BaseIndex) : Indices.Add(BaseIndex + 2) : Indices.Add(BaseIndex + 3)
        End Sub
        Public Sub AddBoxFace(PX As Integer, PY As Integer, PZ As Integer, Box As BlockBox, Direction As Integer, UV As Point())
            Dim Corners As Point3D()
            Dim X0 As Double = PX + Box.X0, X1 As Double = PX + Box.X1
            Dim Y0 As Double = PY + Box.Y0, Y1 As Double = PY + Box.Y1
            Dim Z0 As Double = PZ + Box.Z0, Z1 As Double = PZ + Box.Z1
            Dim Normal As Vector3D
            Select Case Direction
                Case 0
                    Corners = {New Point3D(X0, Y1, Z0), New Point3D(X0, Y1, Z1), New Point3D(X1, Y1, Z1), New Point3D(X1, Y1, Z0)}
                    Normal = New Vector3D(0, 1, 0)
                Case 1
                    Corners = {New Point3D(X0, Y0, Z0), New Point3D(X1, Y0, Z0), New Point3D(X1, Y0, Z1), New Point3D(X0, Y0, Z1)}
                    Normal = New Vector3D(0, -1, 0)
                Case 2
                    Corners = {New Point3D(X0, Y0, Z0), New Point3D(X0, Y1, Z0), New Point3D(X1, Y1, Z0), New Point3D(X1, Y0, Z0)}
                    Normal = New Vector3D(0, 0, -1)
                Case 3
                    Corners = {New Point3D(X0, Y0, Z1), New Point3D(X1, Y0, Z1), New Point3D(X1, Y1, Z1), New Point3D(X0, Y1, Z1)}
                    Normal = New Vector3D(0, 0, 1)
                Case 4
                    Corners = {New Point3D(X0, Y0, Z0), New Point3D(X0, Y0, Z1), New Point3D(X0, Y1, Z1), New Point3D(X0, Y1, Z0)}
                    Normal = New Vector3D(-1, 0, 0)
                Case Else
                    Corners = {New Point3D(X1, Y0, Z0), New Point3D(X1, Y1, Z0), New Point3D(X1, Y1, Z1), New Point3D(X1, Y0, Z1)}
                    Normal = New Vector3D(1, 0, 0)
            End Select
            If Box.RotX <> 0 OrElse Box.RotY <> 0 OrElse Box.RotZ <> 0 Then
                Dim OriginPoint As New Point3D(PX + Box.RotOriginX, PY + Box.RotOriginY, PZ + Box.RotOriginZ)
                For i = 0 To 3
                    Corners(i) = RotatePoint(Corners(i), OriginPoint, Box.RotX, Box.RotY, Box.RotZ)
                Next
                Normal = RotateVector(Normal, Box.RotX, Box.RotY, Box.RotZ)
            End If
            Dim BaseIndex As Integer = Positions.Count
            For i = 0 To 3
                Positions.Add(Corners(i))
                Normals.Add(Normal)
                TextureCoordinates.Add(UV(i))
            Next
            Indices.Add(BaseIndex) : Indices.Add(BaseIndex + 1) : Indices.Add(BaseIndex + 2)
            Indices.Add(BaseIndex) : Indices.Add(BaseIndex + 2) : Indices.Add(BaseIndex + 3)
        End Sub
        Private Shared Function RotatePoint(P As Point3D, Origin As Point3D, RX As Double, RY As Double, RZ As Double) As Point3D
            Dim X As Double = P.X - Origin.X, Y As Double = P.Y - Origin.Y, Z As Double = P.Z - Origin.Z
            If RX <> 0 Then
                Dim Rad As Double = RX * Math.PI / 180
                Dim C As Double = Math.Cos(Rad), S As Double = Math.Sin(Rad)
                Dim NewY As Double = Y * C - Z * S
                Dim NewZ As Double = Y * S + Z * C
                Y = NewY : Z = NewZ
            End If
            If RY <> 0 Then
                Dim Rad As Double = RY * Math.PI / 180
                Dim C As Double = Math.Cos(Rad), S As Double = Math.Sin(Rad)
                Dim NewX As Double = X * C + Z * S
                Dim NewZ As Double = -X * S + Z * C
                X = NewX : Z = NewZ
            End If
            If RZ <> 0 Then
                Dim Rad As Double = RZ * Math.PI / 180
                Dim C As Double = Math.Cos(Rad), S As Double = Math.Sin(Rad)
                Dim NewX As Double = X * C - Y * S
                Dim NewY As Double = X * S + Y * C
                X = NewX : Y = NewY
            End If
            Return New Point3D(X + Origin.X, Y + Origin.Y, Z + Origin.Z)
        End Function
        Private Shared Function RotateVector(V As Vector3D, RX As Double, RY As Double, RZ As Double) As Vector3D
            Dim P As Point3D = RotatePoint(New Point3D(V.X, V.Y, V.Z), New Point3D(0, 0, 0), RX, RY, RZ)
            Return New Vector3D(P.X, P.Y, P.Z)
        End Function
    End Class

    Public Sub New()
        InitializeComponent()
        AddHandler BtnChoose.Click, Sub(sender, e) ChooseFile()
        AddHandler BtnReload.Click, Sub(sender, e)
                                        If Info IsNot Nothing Then LoadFile(Info.FilePath)
                                    End Sub
        AddHandler BtnResetView.Click, Sub(sender, e) ResetView()
        AddHandler PanViewport.MouseLeftButtonDown, Sub(sender, e)
                                                        IsDragging = True
                                                        LastMousePoint = e.GetPosition(PanViewport)
                                                        PanViewport.CaptureMouse()
                                                    End Sub
        AddHandler PanViewport.MouseLeftButtonUp, Sub(sender, e)
                                                      IsDragging = False
                                                      PanViewport.ReleaseMouseCapture()
                                                  End Sub
        AddHandler PanViewport.MouseMove, Sub(sender, e)
                                              If Not IsDragging Then Return
                                              Dim CurrentPoint As Point = e.GetPosition(PanViewport)
                                              Yaw -= (CurrentPoint.X - LastMousePoint.X) * 0.4
                                              Pitch += (CurrentPoint.Y - LastMousePoint.Y) * 0.4
                                              If Pitch > 89 Then Pitch = 89
                                              If Pitch < -89 Then Pitch = -89
                                              LastMousePoint = CurrentPoint
                                              UpdateCamera()
                                          End Sub
        AddHandler PanViewport.MouseWheel, Sub(sender, e)
                                               Dim Factor As Double = If(e.Delta > 0, 0.85, 1.18)
                                               Distance *= Factor
                                               If Distance < 8 Then Distance = 8
                                               If Distance > 5000 Then Distance = 5000
                                               UpdateCamera()
                                           End Sub
    End Sub

    Private Sub BtnLibFold_Click(sender As Object, e As EventArgs) Handles BtnLibFold.Click
        If PanLibraryBody.Visibility = Visibility.Visible Then
            PanLibraryBody.Visibility = Visibility.Collapsed
            BtnLibFold.Text = "展开"
        Else
            PanLibraryBody.Visibility = Visibility.Visible
            BtnLibFold.Text = "折叠"
        End If
    End Sub

    Private Sub PageOtherLitematic_Enter() Handles Me.PageEnter
        Try
            If PanLibraryScroller IsNot Nothing Then PanLibraryScroller.ScrollToHome()
        Catch ex As Exception
        End Try
    End Sub

    Private Sub PageOtherLitematic_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Try
            If PanLibrary.Children.Count = 0 Then
                If ModLitematicOnline.HasLoaded Then
                    OnlineMode = True
                    ApplyOnlineFilter()
                Else
                    BuildBuiltinLibrary()
                    StartFetchOnline()
                End If
            End If
            PanLibraryScroller.ScrollToHome()
            If Info Is Nothing AndAlso Viewport.Children.Count = 0 Then ResetView()
        Catch ex As Exception
            Logger.Warn(ex, "初始化投影查看页失败")
        End Try
    End Sub

    Private Sub ChooseFile()
        Try
            Dim Files As List(Of String) = Dialogs.SelectFile("选择投影文件", False, filter:={({"litematic", "litematic.gz"}, "Litematica 投影")})
            If Files Is Nothing OrElse Files.Count = 0 Then Return
            LoadFile(Files(0))
        Catch ex As Exception
            Logger.Warn(ex, "选择投影文件失败")
        End Try
    End Sub

    Private Sub LoadFile(FilePath As String)
        ModMinecraftTextures.ResetTextureCaches()
        ModBlockModel.ResetModelCache()
        If IsLoading Then Return
        If Not FileUtils.Exists(FilePath) Then
            Hint("文件不存在", HintType.Red)
            Return
        End If
        IsLoading = True
        BtnChoose.IsEnabled = False
        LabFile.Text = FilePath
        LabStatus.Text = "正在解析投影文件……"
        LabEmpty.Visibility = Visibility.Collapsed
        RunInNewThread(
            Sub()
                Dim Result As ModLitematic.LitematicInfo = Nothing
                Dim ErrorText As String = Nothing
                Dim TextureReady As Boolean = False
                Try
                    TextureReady = ModMinecraftTextures.Initialize()
                    Result = ModLitematic.Load(FilePath)
                Catch ex As Exception
                    ErrorText = ex.Message
                End Try
                RunInUi(
                    Sub()
                        IsLoading = False
                        BtnChoose.IsEnabled = True
                        If Result Is Nothing Then
                            LabStatus.Text = "解析失败：" & ErrorText
                            Hint("解析投影失败：" & ErrorText, HintType.Red)
                            Return
                        End If
                        Info = Result
                        SceneLayer = -1
                        ShowInfo(Result, TextureReady)
                        Try
                            BuildScene(Result, SceneLayer)
                            LabStatus.Text = $"左键拖拽旋转 · 滚轮缩放 · 采样步长 {Result.SampleStep}" & If(TextureReady, " · 已使用 Minecraft 材质", " · 未找到材质，使用纯色")
                        Catch ex As Exception
                            Logger.Warn(ex, "构建 3D 场景失败")
                            LabStatus.Text = "构建 3D 场景失败：" & ex.Message
                        End Try
                    End Sub)
            End Sub, "LitematicLoad")
    End Sub

    Private Sub ShowInfo(Data As ModLitematic.LitematicInfo, TextureReady As Boolean)
        Dim SizeText As String = $"{Data.EnclosingX}×{Data.EnclosingY}×{Data.EnclosingZ}"
        Dim Text As String = $"名称：{Data.Name}" & vbCrLf &
            $"尺寸：{SizeText}（方块）  结构方块数：{Data.TotalBlocks}  体积：{Data.TotalVolume}  区域数：{Data.RegionCount}" & vbCrLf &
            $"采样：每 {Data.SampleStep} 格取 1 个（预览网格 {Data.GridX}×{Data.GridY}×{Data.GridZ}，方块种类 {Data.PaletteNames.Count - 1}，材质：{If(TextureReady, "Minecraft 原版材质", "纯色（未找到资源文件）")}）"
        If Not String.IsNullOrWhiteSpace(Data.Author) Then Text &= vbCrLf & "作者：" & Data.Author
        If Not String.IsNullOrWhiteSpace(Data.Description) Then Text &= vbCrLf & "描述：" & Data.Description
        LabInfo.Text = Text

        IsFillingLayer = True
        Try
            ComboLayer.Items.Clear()
            ComboLayer.Items.Add(New MyComboBoxItem With {.Content = "全部层级", .Tag = -1, .IsSelected = True})
            For Y = 0 To Data.GridY - 1
                ComboLayer.Items.Add(New MyComboBoxItem With {.Content = $"第 {Y} 层", .Tag = Y})
            Next
        Finally
            IsFillingLayer = False
        End Try
    End Sub

    Private Function GetFaceUV(Direction As Integer) As Point()
        Select Case Direction
            Case 0, 1 '上下
                Return {New Point(0, 0), New Point(0, 1), New Point(1, 1), New Point(1, 0)}
            Case 2, 3 '南北
                Return {New Point(0, 1), New Point(0, 0), New Point(1, 0), New Point(1, 1)}
            Case Else '东西
                Return {New Point(0, 1), New Point(1, 1), New Point(1, 0), New Point(0, 0)}
        End Select
    End Function

    Private Sub BuildScene(Data As ModLitematic.LitematicInfo, LayerFilter As Integer)
        Viewport.Children.Clear()
        Dim Models(Data.PaletteNames.Count - 1) As BlockRenderModel
        Dim Opaque(Data.PaletteNames.Count - 1) As Boolean
        For i = 1 To Data.PaletteNames.Count - 1
            Dim StateText As String = If(i < Data.PaletteStates.Count, Data.PaletteStates(i), "")
            Models(i) = ModBlockModel.GetModel(Data.PaletteNames(i), StateText)
            Opaque(i) = Models(i) IsNot Nothing AndAlso Models(i).IsFullCube AndAlso Not IsTransparentPalette(Data.PaletteNames(i))
        Next
        Try
            Dim ModelReport As New Text.StringBuilder
            For i = 1 To Data.PaletteNames.Count - 1
                Dim M As BlockRenderModel = Models(i)
                Dim FaceTextures As Integer = 0
                If M IsNot Nothing AndAlso M.Boxes IsNot Nothing Then
                    For Each B In M.Boxes
                        For D = 0 To 5
                            If B.FacePresent(D) Then FaceTextures += 1
                        Next
                    Next
                End If
                ModelReport.AppendLine(Data.PaletteNames(i) & " | " & If(i < Data.PaletteStates.Count, Data.PaletteStates(i), "") &
                                       " | boxes=" & If(M Is Nothing OrElse M.Boxes Is Nothing, 0, M.Boxes.Count) &
                                       " | full=" & If(M Is Nothing, False, M.IsFullCube) & " | faces=" & FaceTextures)
            Next
            Logger.Info("投影模型列表：" & vbCrLf & ModelReport.ToString())
        Catch ex As Exception
        End Try
        Dim Builders As New Dictionary(Of String, MeshBuilder)
        Dim GridX As Integer = Data.GridX
        Dim GridY As Integer = Data.GridY
        Dim GridZ As Integer = Data.GridZ
        Dim Voxels As Integer(,,) = Data.Voxels
        Dim OffsetX As Integer = CInt(GridX / 2.0)
        Dim OffsetY As Integer = CInt(GridY / 2.0)
        Dim OffsetZ As Integer = CInt(GridZ / 2.0)
        Dim GetId As Func(Of Integer, Integer, Integer, Integer) =
            Function(VX As Integer, VY As Integer, VZ As Integer) As Integer
                If LayerFilter >= 0 AndAlso VY <> LayerFilter Then Return 0
                If VX < 0 OrElse VY < 0 OrElse VZ < 0 OrElse VX >= GridX OrElse VY >= GridY OrElse VZ >= GridZ Then Return 0
                Return Voxels(VX, VY, VZ)
            End Function
        Dim AddFace As Action(Of Integer, Integer, Integer, String, Integer) =
            Sub(PX As Integer, PY As Integer, PZ As Integer, TexturePath As String, Direction As Integer)
                Dim Key As String
                If Not String.IsNullOrEmpty(TexturePath) Then
                    Key = "tex:" & TexturePath
                Else
                    Key = "color:" & Voxels(PX + OffsetX, PY + OffsetY, PZ + OffsetZ).ToString()
                End If
                Dim Builder As MeshBuilder = Nothing
                If Not Builders.TryGetValue(Key, Builder) Then
                    Builder = New MeshBuilder
                    Builders(Key) = Builder
                End If
                Builder.AddFace(PX, PY, PZ, Direction, GetFaceUV(Direction))
            End Sub
        For X = 0 To GridX - 1
            For Y = 0 To GridY - 1
                If LayerFilter >= 0 AndAlso Y <> LayerFilter Then Continue For
                For Z = 0 To GridZ - 1
                    Dim Id As Integer = Voxels(X, Y, Z)
                    If Id <= 0 Then Continue For
                    Dim Model As BlockRenderModel = If(Id < Models.Length, Models(Id), Nothing)
                    If Model Is Nothing OrElse Model.Boxes Is Nothing OrElse Model.Boxes.Count = 0 Then
                        Model = New BlockRenderModel
                        Model.IsFullCube = True
                    End If
                    Dim PX As Integer = X - OffsetX
                    Dim PY As Integer = Y - OffsetY
                    Dim PZ As Integer = Z - OffsetZ
                    If Model.IsFullCube Then
                        Dim Box As BlockBox = If(Model.Boxes.Count > 0, Model.Boxes(0), Nothing)
                        For Direction = 0 To 5
                            Dim NeighborId As Integer
                            Select Case Direction
                                Case 0 : NeighborId = GetId(X, Y + 1, Z)
                                Case 1 : NeighborId = GetId(X, Y - 1, Z)
                                Case 2 : NeighborId = GetId(X, Y, Z - 1)
                                Case 3 : NeighborId = GetId(X, Y, Z + 1)
                                Case 4 : NeighborId = GetId(X - 1, Y, Z)
                                Case Else : NeighborId = GetId(X + 1, Y, Z)
                            End Select
                            Dim NeighborOpaque As Boolean = NeighborId > 0 AndAlso NeighborId < Opaque.Length AndAlso Opaque(NeighborId)
                            If NeighborOpaque Then Continue For
                            Dim TexturePath As String = If(Box Is Nothing, Nothing, Box.Faces(Direction))
                            AddFace(PX, PY, PZ, TexturePath, Direction)
                        Next
                    Else
                        For Each Box As BlockBox In Model.Boxes
                            For Direction = 0 To 5
                                If Not Box.FacePresent(Direction) Then Continue For
                                Dim Key As String
                                If Not String.IsNullOrEmpty(Box.Faces(Direction)) Then
                                    Key = "tex:" & Box.Faces(Direction)
                                Else
                                    Key = "color:" & Id.ToString()
                                End If
                                Dim Builder As MeshBuilder = Nothing
                                If Not Builders.TryGetValue(Key, Builder) Then
                                    Builder = New MeshBuilder
                                    Builders(Key) = Builder
                                End If
                                Builder.AddBoxFace(PX, PY, PZ, Box, Direction, GetFaceUV(Direction))
                            Next
                        Next
                    End If
                Next
            Next
        Next

        Dim Group As New Model3DGroup
        Group.Children.Add(New AmbientLight(Color.FromRgb(160, 160, 160)))
        Group.Children.Add(New DirectionalLight(Color.FromRgb(235, 235, 235), New Vector3D(-1.0, -1.6, -0.8)))
        For Each Pair In Builders
            Dim Builder As MeshBuilder = Pair.Value
            If Builder.Positions.Count = 0 Then Continue For
            Dim Mesh As New MeshGeometry3D With {
                .Positions = New Point3DCollection(Builder.Positions),
                .Normals = New Vector3DCollection(Builder.Normals),
                .TextureCoordinates = New PointCollection(Builder.TextureCoordinates),
                .TriangleIndices = New Int32Collection(Builder.Indices)}
            Dim Material As Material
            If Pair.Key.StartsWithF("tex:") Then
                Dim Image As BitmapSource = ModMinecraftTextures.GetTextureImage(Pair.Key.Substring(4))
                If Image IsNot Nothing Then
                    Dim Brush As New ImageBrush(Image) With {.Stretch = Stretch.Fill, .TileMode = TileMode.None}
                    RenderOptions.SetBitmapScalingMode(Brush, BitmapScalingMode.NearestNeighbor)
                    Material = New DiffuseMaterial(Brush)
                Else
                    Material = New DiffuseMaterial(New SolidColorBrush(Color.FromRgb(150, 150, 155)))
                End If
            Else
                Dim Id As Integer = Val(Pair.Key.Substring("color:".Length))
                Dim ColorValue As System.Windows.Media.Color = System.Windows.Media.Color.FromRgb(150, 150, 155)
                If Id >= 0 AndAlso Id < Data.PaletteColors.Count Then
                    Dim DrawingColor As System.Drawing.Color = Data.PaletteColors(Id)
                    ColorValue = System.Windows.Media.Color.FromArgb(DrawingColor.A, DrawingColor.R, DrawingColor.G, DrawingColor.B)
                End If
                Material = New DiffuseMaterial(New SolidColorBrush(ColorValue))
            End If
            Group.Children.Add(New GeometryModel3D(Mesh, Material) With {.BackMaterial = Material})
        Next
        RenderOptions.SetBitmapScalingMode(Viewport, BitmapScalingMode.NearestNeighbor)
        Viewport.Children.Add(New ModelVisual3D With {.Content = Group})
        Try
            ModMinecraftTextures.TrimCaches(512, 1024)
            ModMemoryOptimize.TrimIdle()
        Catch ex As Exception
        End Try
        ResetView()
    End Sub

    Private Function IsTransparentPalette(BlockName As String) As Boolean
        Dim Name As String = BlockName
        If Name.Contains(":") Then Name = Name.Substring(Name.IndexOf(":"c) + 1)
        Name = Name.ToLower()
        For Each Word As String In {"glass", "water", "ice", "leaves", "pane", "slab", "stairs", "wall", "fence", "door",
                                    "trapdoor", "carpet", "rail", "torch", "flower", "sapling", "button", "lever", "sign",
                                    "banner", "chest", "hopper", "piston", "repeater", "comparator", "ladder", "vine",
                                    "snow", "scaffolding", "campfire", "lantern", "chain", "bars", "path", "farmland",
                                    "grass", "coral", "kelp", "seagrass", "cactus", "bamboo", "candle", "cake"}
            If Name.Contains(Word) Then Return True
        Next
        Return False
    End Function

    Private Sub ResetView()
        Yaw = 40
        Pitch = 28
        If Info Is Nothing Then
            Distance = 120
        Else
            Distance = Math.Max(30, Math.Max(Math.Max(Info.GridX, Info.GridY), Info.GridZ) * 1.8)
        End If
        UpdateCamera()
    End Sub

    Private Sub UpdateCamera()
        If Info Is Nothing Then Return
        Dim Camera As New PerspectiveCamera With {
            .FieldOfView = 50,
            .NearPlaneDistance = 0.5,
            .FarPlaneDistance = 10000}
        Dim RadYaw As Double = Yaw * Math.PI / 180
        Dim RadPitch As Double = Pitch * Math.PI / 180
        Dim Target As New Point3D(0, 0, 0)
        Dim Position As New Point3D(
            Target.X + Distance * Math.Cos(RadPitch) * Math.Sin(RadYaw),
            Target.Y + Distance * Math.Sin(RadPitch),
            Target.Z + Distance * Math.Cos(RadPitch) * Math.Cos(RadYaw))
        Camera.Position = Position
        Camera.LookDirection = New Vector3D(Target.X - Position.X, Target.Y - Position.Y, Target.Z - Position.Z)
        Camera.UpDirection = New Vector3D(0, 1, 0)
        Viewport.Camera = Camera
    End Sub

#Region "投影库"

    Private Sub StartFetchOnline()
        If IsFetchingOnline Then Return
        IsFetchingOnline = True
        LabLibStatus.Text = "正在从 GitHub 获取在线投影列表……"
        RunInNewThread(
            Sub()
                Dim Success As Boolean = ModLitematicOnline.Fetch()
                RunInUi(
                    Sub()
                        IsFetchingOnline = False
                        If Success Then
                            OnlineMode = True
                            ApplyOnlineFilter()
                        Else
                            OnlineMode = False
                            BuildBuiltinLibrary()
                            LabLibStatus.Text = "在线列表获取失败，显示内置投影：" & ModLitematicOnline.LastError
                        End If
                    End Sub)
            End Sub, "LitematicOnlineFetch")
    End Sub

    Private Sub BuildLibraryRows()
        PanLibrary.Children.Clear()
        If OnlineMode Then ApplyOnlineFilter() Else BuildBuiltinLibrary()
    End Sub

    Private Sub ApplyOnlineFilter()
        If Not OnlineMode Then Return
        Dim Keyword As String = If(TextLibSearch.Text, "").Trim()
        Dim SortTag As String = "name"
        Dim SortItem As MyComboBoxItem = TryCast(ComboLibSort.SelectedItem, MyComboBoxItem)
        If SortItem IsNot Nothing AndAlso SortItem.Tag IsNot Nothing Then SortTag = SortItem.Tag.ToString()
        FilteredOnline.Clear()
        For Each Item In ModLitematicOnline.Items
            If Keyword <> "" AndAlso Not (Item.Name.ContainsIgnoreCase(Keyword) OrElse Item.RepoDisplay.ContainsIgnoreCase(Keyword)) Then Continue For
            FilteredOnline.Add(Item)
        Next
        Select Case SortTag
            Case "size" : FilteredOnline.Sort(Function(a, b) b.Size.CompareTo(a.Size))
            Case "repo" : FilteredOnline.Sort(Function(a, b) String.Compare(a.RepoDisplay & a.Name, b.RepoDisplay & b.Name, StringComparison.OrdinalIgnoreCase))
            Case Else : FilteredOnline.Sort(Function(a, b) String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase))
        End Select
        PanLibrary.Children.Clear()
        Dim ShowCount As Integer = Math.Min(FilteredOnline.Count, MaxDisplayedOnline)
        For i = 0 To ShowCount - 1
            Dim Item = FilteredOnline(i)
            Dim CurrentItem = Item
            PanLibrary.Children.Add(CreateRow(Item.Name,
                $"{Item.RepoDisplay} · {ModLitematicOnline.FormatSize(Item.Size)} · {Item.Path}",
                Sub() DownloadOnlineItem(CurrentItem),
                Sub() OpenOnlineItem(CurrentItem)))
        Next
        LabLibStatus.Text = $"在线投影 {FilteredOnline.Count} 个" & If(FilteredOnline.Count > MaxDisplayedOnline, $"（显示前 {MaxDisplayedOnline} 个）", "") & " · 来自 GitHub"
    End Sub

    Private Sub BuildBuiltinLibrary()
        PanLibrary.Children.Clear()
        For Each Item As LitematicCatalogItem In ModLitematicCatalog.Items
            Dim CurrentItem As LitematicCatalogItem = Item
            PanLibrary.Children.Add(CreateRow(Item.DisplayName,
                $"{Item.Author} · {Item.SizeText} · {Item.Description}",
                Sub() DownloadCatalogItem(CurrentItem),
                Sub() OpenCatalogItem(CurrentItem)))
        Next
        If LabLibStatus IsNot Nothing AndAlso String.IsNullOrEmpty(LabLibStatus.Text) Then LabLibStatus.Text = "内置投影"
    End Sub

    Private Function CreateRow(TitleText As String, SubText As String, DownloadAction As Action, OpenAction As Action) As Border
        Dim Row As New Border With {
            .Height = 58,
            .CornerRadius = New CornerRadius(8),
            .Background = New SolidColorBrush(Color.FromArgb(18, 59, 130, 246)),
            .Margin = New Thickness(0, 0, 0, 6),
            .Padding = New Thickness(12, 6, 10, 6)}
        Dim Grid As New Grid
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Dim InfoPanel As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        Dim Title As New TextBlock With {.Text = TitleText, .FontSize = 13.5, .FontWeight = FontWeights.Bold}
        Title.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Dim SubLine As New TextBlock With {.Text = SubText, .FontSize = 11, .Margin = New Thickness(0, 3, 0, 0), .TextTrimming = TextTrimming.CharacterEllipsis}
        SubLine.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray2")
        InfoPanel.Children.Add(Title)
        InfoPanel.Children.Add(SubLine)
        Grid.Children.Add(InfoPanel)

        Dim BtnBar As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        Dim BtnDownload As New MyButton With {.Text = "下载", .Height = 28, .MinWidth = 64, .Padding = New Thickness(12, 0, 12, 0), .Margin = New Thickness(8, 0, 0, 0)}
        Dim BtnOpen As New MyButton With {.Text = "打开", .Height = 28, .MinWidth = 64, .Padding = New Thickness(12, 0, 12, 0), .Margin = New Thickness(8, 0, 0, 0), .ColorType = MyButton.ColorState.Highlight}
        AddHandler BtnDownload.Click, Sub(sender, e) DownloadAction()
        AddHandler BtnOpen.Click, Sub(sender, e) OpenAction()
        BtnBar.Children.Add(BtnDownload)
        BtnBar.Children.Add(BtnOpen)
        Grid.SetColumn(BtnBar, 1)
        Grid.Children.Add(BtnBar)
        Row.Child = Grid
        Return Row
    End Function

    Private Sub DownloadOnlineItem(Item As OnlineLitematicItem)
        RunInNewThread(
            Sub()
                Try
                    Dim Target As String = ModLitematicOnline.Download(Item)
                    RunInUi(Sub() Hint("已下载：" & Target, HintType.Green))
                Catch ex As Exception
                    RunInUi(Sub() Hint("下载失败：" & ex.Message, HintType.Red))
                    Logger.Warn(ex, "下载在线投影失败")
                End Try
            End Sub, "LitematicOnlineDownload")
    End Sub

    Private Sub OpenOnlineItem(Item As OnlineLitematicItem)
        RunInNewThread(
            Sub()
                Try
                    Dim Target As String = ModLitematicOnline.Download(Item, IO.Path.Combine(PathTemp, "Litematic"))
                    RunInUi(Sub() LoadFile(Target))
                Catch ex As Exception
                    RunInUi(Sub() Hint("下载失败：" & ex.Message, HintType.Red))
                    Logger.Warn(ex, "打开在线投影失败")
                End Try
            End Sub, "LitematicOnlineOpen")
    End Sub

    Private Sub DownloadCatalogItem(Item As LitematicCatalogItem)
        Try
            Dim Target As String = ModLitematicCatalog.Export(Item)
            Hint("已下载：" & Target, HintType.Green)
        Catch ex As Exception
            Hint("下载失败：" & ex.Message, HintType.Red)
            Logger.Warn(ex, "下载内置投影失败")
        End Try
    End Sub

    Private Sub OpenCatalogItem(Item As LitematicCatalogItem)
        Try
            Dim Target As String = ModLitematicCatalog.ExportToTemp(Item)
            LoadFile(Target)
        Catch ex As Exception
            Hint("下载失败：" & ex.Message, HintType.Red)
            Logger.Warn(ex, "打开内置投影失败")
        End Try
    End Sub

    Private Sub BtnLibRefresh_Click(sender As Object, e As EventArgs) Handles BtnLibRefresh.Click
        ModLitematicOnline.HasLoaded = False
        StartFetchOnline()
    End Sub

    Private Sub BtnLibSearch_Click(sender As Object, e As EventArgs) Handles BtnLibSearch.Click
        If OnlineMode Then
            ApplyOnlineFilter()
        Else
            StartFetchOnline()
        End If
    End Sub

    Private Sub TextLibSearch_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextLibSearch.TextChanged
        If OnlineMode Then ApplyOnlineFilter()
    End Sub

    Private Sub ComboLibSort_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboLibSort.SelectionChanged
        If OnlineMode Then ApplyOnlineFilter()
    End Sub

    Private Sub ComboLayer_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboLayer.SelectionChanged
        If IsFillingLayer OrElse Info Is Nothing Then Return
        Dim Item As MyComboBoxItem = TryCast(ComboLayer.SelectedItem, MyComboBoxItem)
        If Item Is Nothing OrElse Item.Tag Is Nothing Then Return
        SceneLayer = CInt(Item.Tag)
        Try
            BuildScene(Info, SceneLayer)
            LabStatus.Text = If(SceneLayer < 0, "显示全部层级", $"仅显示第 {SceneLayer} 层") & " · 左键拖拽旋转 · 滚轮缩放"
        Catch ex As Exception
            Logger.Warn(ex, "切换显示层级失败")
        End Try
    End Sub

#End Region

End Class
