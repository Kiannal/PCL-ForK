Imports System.Windows.Media.Imaging

Public Class PageInstanceSeed

    Private Class StructureKind
        Public Key As String = ""
        Public Name As String = ""
        Public Color As Color
        Public Check As MyCheckBox = Nothing
    End Class

    Public Class StructureItem
        Public Property Key As String = ""
        Public Property Name As String = ""
        Public Property BlockX As Integer
        Public Property BlockZ As Integer
        Public Property ColorBrush As Brush = Brushes.Gray
        Public ReadOnly Property Display As String
            Get
                Return $"{Name}  X {BlockX}  Z {BlockZ}"
            End Get
        End Property
    End Class

    Private ReadOnly Kinds As New List(Of StructureKind)
    Private ReadOnly Items As New List(Of StructureItem)

    Private CurrentData As JObject = Nothing
    Private MapSize As Integer = 0
    Private MapRadius As Integer = 0
    Private MapX0 As Integer = 0
    Private MapZ0 As Integer = 0
    Private MapVersion As String = ""
    Private CurrentSeed As String = ""
    Private GeneratedKey As String = ""
    Private BiomePixels As Integer(,) = Nothing
    Private MarkerDrawn As Integer = 0
    Private BiomeNames As New Dictionary(Of Integer, String)
    Private MapScaleValue As Integer = 4

    Private IsDragging As Boolean = False
    Private DragStartPoint As Point
    Private DragStartOffset As Point
    Private IsGenerating As Boolean = False
    Private ShowGrid As Boolean = True
    Private Fitted As Boolean = False
    Private KeepView As Boolean = False
    Private LastExpandAt As Date = Date.MinValue
    Private ViewCenterX As Double = 0
    Private ViewCenterZ As Double = 0

    Public Sub New()
        InitializeComponent()
        InitKinds()
        AddHandler BtnLoad.Click, Sub(sender, e) Generate(False)
        AddHandler BtnReadSave.Click, Sub(sender, e) ReadSaveSeed()
        AddHandler BtnCubiomes.Click, Sub(sender, e)
                                          Try
                                              ModSeedMap.LaunchCubiomesViewer(TextSeed.Text.Trim(), GetVersionValue())
                                          Catch ex As Exception
                                              Hint("启动无限地图失败：" & ex.Message, HintType.Red)
                                          End Try
                                      End Sub
        AddHandler BtnZoomIn.Click, Sub(sender, e) ZoomBy(1.25, New Point(PanMap.ActualWidth / 2, PanMap.ActualHeight / 2))
        AddHandler BtnZoomOut.Click, Sub(sender, e) ZoomBy(0.8, New Point(PanMap.ActualWidth / 2, PanMap.ActualHeight / 2))
        AddHandler BtnReset.Click, Sub(sender, e) ResetView()
        AddHandler PanMap.MouseWheel, Sub(sender, e)
                                         ZoomBy(If(e.Delta > 0, 1.2, 0.8333), e.GetPosition(PanMap))
                                         e.Handled = True
                                     End Sub
        AddHandler PanMap.MouseLeftButtonDown, Sub(sender, e)
                                                   IsDragging = True
                                                   DragStartPoint = e.GetPosition(PanMap)
                                                   DragStartOffset = New Point(MapOffset.X, MapOffset.Y)
                                                   PanMap.CaptureMouse()
                                               End Sub
        AddHandler PanMap.MouseLeftButtonUp, Sub(sender, e)
                                                 IsDragging = False
                                                 PanMap.ReleaseMouseCapture()
                                                 CheckExpand()
                                             End Sub
        AddHandler PanMap.MouseMove, Sub(sender, e)
                                         If IsDragging Then
                                             Dim CurrentPoint As Point = e.GetPosition(PanMap)
                                             MapOffset.X = DragStartOffset.X + (CurrentPoint.X - DragStartPoint.X)
                                             MapOffset.Y = DragStartOffset.Y + (CurrentPoint.Y - DragStartPoint.Y)
                                         End If
                                         UpdateCoordinates(e.GetPosition(MapContent))
                                     End Sub
        AddHandler ListStructures.SelectionChanged, Sub(sender, e) CenterSelected()
        AddHandler BtnCopyTp.Click, Sub(sender, e)
                                        Try
                                            Dim Item As StructureItem = TryCast(ListStructures.SelectedItem, StructureItem)
                                            If Item Is Nothing Then
                                                Hint("请先在右侧列表选择一个结构", HintType.Blue)
                                                Return
                                            End If
                                            Clipboard.SetText($"/tp @s {Item.BlockX} ~ {Item.BlockZ}")
                                            Hint("已复制传送指令", HintType.Green)
                                        Catch ex As Exception
                                        End Try
                                    End Sub
        AddHandler PanMap.SizeChanged, Sub(sender, e)
                                             If Not Fitted AndAlso MapSize > 0 Then ResetView()
                                         End Sub
        AddHandler BtnCenterLoad.Click, Sub(sender, e) Generate(False)
        AddHandler PanMap.MouseRightButtonDown, Sub(sender, e)
                                                    If MapSize <= 0 Then Return
                                                    Dim PointInContent As Point = e.GetPosition(MapContent)
                                                    Dim BlockX As Integer = CInt(MapX0 + PointInContent.X / MapSize * (2.0 * MapRadius))
                                                    Dim BlockZ As Integer = CInt(MapZ0 + PointInContent.Y / MapSize * (2.0 * MapRadius))
                                                    TextCenterX.Text = BlockX.ToString()
                                                    TextCenterZ.Text = BlockZ.ToString()
                                                    Generate(False)
                                                End Sub
    End Sub

    Private Sub PageInstanceSeed_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Try
            InitVersionCombo()
            InitFilters()
            ComboDimension.SelectedIndex = 0
            If ComboRange.SelectedIndex < 0 Then ComboRange.SelectedIndex = ComboRange.Items.Count - 1
            If TextSeed.Text.Trim() = "" Then
                Try
                    Dim Worlds As List(Of ModWorldSettings.WorldInfo) = ModWorldSettings.GetWorlds(PageInstanceLeft.Instance)
                    Dim WithSeed As ModWorldSettings.WorldInfo = Worlds.FirstOrDefault(Function(w) Not String.IsNullOrWhiteSpace(w.Seed))
                    If WithSeed IsNot Nothing Then TextSeed.Text = WithSeed.Seed
                Catch ex As Exception
                End Try
            End If
            If TextSeed.Text.Trim() <> "" AndAlso CurrentData Is Nothing Then Generate(True)
        Catch ex As Exception
            Logger.Warn(ex, "初始化种子地图页失败")
        End Try
    End Sub

    Private Sub InitVersionCombo()
        If ComboVersion.Items.Count > 0 Then Return
        For Each Version In {"1.21", "1.20.6", "1.20.1", "1.19.4", "1.18.2", "1.17.1", "1.16.5", "1.15.2", "1.14.4", "1.13.2", "1.12.2"}
            ComboVersion.Items.Add(New MyComboBoxItem With {.Content = Version, .Tag = Version})
        Next
        Dim GameVersion As String = ModSeedMap.GetVersionName(PageInstanceLeft.Instance)
        Dim Best As Integer = 0
        For i = 0 To ComboVersion.Items.Count - 1
            Dim Item As MyComboBoxItem = CType(ComboVersion.Items(i), MyComboBoxItem)
            If GameVersion.StartsWithF(Item.Tag.ToString(), True) Then
                Best = i
                Exit For
            End If
        Next
        ComboVersion.SelectedIndex = Best
    End Sub

    Private Sub InitKinds()
        Kinds.Clear()
        AddKind("village", "村庄", Color.FromRgb(242, 193, 78))
        AddKind("desert_pyramid", "沙漠神殿", Color.FromRgb(232, 163, 61))
        AddKind("jungle_temple", "丛林神庙", Color.FromRgb(76, 140, 43))
        AddKind("swamp_hut", "沼泽小屋", Color.FromRgb(60, 110, 71))
        AddKind("igloo", "雪屋", Color.FromRgb(189, 227, 245))
        AddKind("monument", "海底神殿", Color.FromRgb(46, 158, 158))
        AddKind("mansion", "林地府邸", Color.FromRgb(107, 79, 42))
        AddKind("outpost", "前哨站", Color.FromRgb(139, 90, 43))
        AddKind("desert_well", "沙漠水井", Color.FromRgb(205, 170, 90))
        AddKind("ruined_portal", "废弃传送门", Color.FromRgb(142, 107, 191))
        AddKind("ancient_city", "远古城市", Color.FromRgb(47, 59, 82))
        AddKind("treasure", "埋藏的宝藏", Color.FromRgb(201, 162, 39))
        AddKind("trail_ruins", "古迹废墟", Color.FromRgb(122, 140, 58))
        AddKind("trial_chambers", "试炼密室", Color.FromRgb(199, 91, 57))
        AddKind("fortress", "下界要塞", Color.FromRgb(179, 58, 58))
        AddKind("bastion", "堡垒遗迹", Color.FromRgb(91, 75, 138))
        AddKind("end_city", "末地城", Color.FromRgb(179, 157, 219))
        AddKind("stronghold", "要塞", Color.FromRgb(229, 57, 53))
        AddKind("spawn", "世界出生点", Color.FromRgb(76, 175, 80))
    End Sub

    Private Sub AddKind(Key As String, Name As String, ColorValue As Color)
        Kinds.Add(New StructureKind With {.Key = Key, .Name = Name, .Color = ColorValue})
    End Sub

    Private Sub InitFilters()
        If PanFilters.Children.Count > 0 Then Return
        For Each Kind In Kinds
            Dim Check As New MyCheckBox With {
                .Text = Kind.Name, .Checked = False, .Margin = New Thickness(0, 0, 14, 8), .Height = 24}
            Dim CurrentKey As String = Kind.Key
            AddHandler Check.Change, Sub(sender As Object, user As Boolean)
                                         Dim Visible As Boolean = Check.Checked
                                         For Each Child As UIElement In PanMarkers.Children
                                             If TryCast(Child, FrameworkElement)?.Tag?.ToString() = CurrentKey Then
                                                 Child.Visibility = If(Visible, Visibility.Visible, Visibility.Collapsed)
                                             End If
                                         Next
                                     End Sub
            Kind.Check = Check
            PanFilters.Children.Add(Check)
        Next
        Dim GridCheck As New MyCheckBox With {
            .Text = "显示网格", .Checked = True, .Margin = New Thickness(0, 0, 14, 8), .Height = 24}
        AddHandler GridCheck.Change, Sub(sender As Object, user As Boolean)
                                         ShowGrid = GridCheck.Checked
                                         For Each Child As UIElement In PanMarkers.Children
                                             If TryCast(Child, FrameworkElement)?.Tag?.ToString() = "grid" Then
                                                 Child.Visibility = If(ShowGrid, Visibility.Visible, Visibility.Collapsed)
                                             End If
                                         Next
                                     End Sub
        PanFilters.Children.Add(GridCheck)
    End Sub

    Private Sub ReadSaveSeed()
        Try
            Dim Worlds As List(Of ModWorldSettings.WorldInfo) = ModWorldSettings.GetWorlds(PageInstanceLeft.Instance)
            If Not Worlds.Any() Then
                Hint("这个版本还没有存档", HintType.Blue)
                Return
            End If
            Dim Selected As ModWorldSettings.WorldInfo = Worlds.FirstOrDefault(Function(w) Not String.IsNullOrWhiteSpace(w.Seed))
            If Selected Is Nothing Then
                Hint("没有从存档里读到种子", HintType.Red)
                Return
            End If
            TextSeed.Text = Selected.Seed
            Hint("已读取种子：" & Selected.Seed, HintType.Green)
            Generate(False)
        Catch ex As Exception
            Logger.Warn(ex, "读取存档种子失败")
            Hint("读取存档种子失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Function GetVersionValue() As String
        If ComboVersion.SelectedItem IsNot Nothing Then
            Return CType(ComboVersion.SelectedItem, MyComboBoxItem).Tag.ToString()
        End If
        Return "1.20.1"
    End Function

    Private Function GetTagInt(Combo As MyComboBox, DefaultValue As Integer) As Integer
        If Combo.SelectedItem IsNot Nothing Then
            Return Val(CType(Combo.SelectedItem, MyComboBoxItem).Tag)
        End If
        Return DefaultValue
    End Function

    Private Sub Generate(Auto As Boolean)
        If IsGenerating Then Return
        Dim SeedValue As String = TextSeed.Text.Trim()
        If SeedValue = "" Then
            Hint("请先输入世界种子", HintType.Blue)
            Return
        End If
        Dim SeedNumber As Long
        If Not Long.TryParse(SeedValue, SeedNumber) Then
            Hint("种子必须是数字（支持负数）", HintType.Red)
            Return
        End If
        Dim Version As String = GetVersionValue()
        Dim Dimension As Integer = GetTagInt(ComboDimension, 0)
        Dim Range As Integer = GetTagInt(ComboRange, 2048)
        Dim CenterX As Integer = 0, CenterZ As Integer = 0
        Integer.TryParse(TextCenterX.Text.Trim(), CenterX)
        Integer.TryParse(TextCenterZ.Text.Trim(), CenterZ)
        Dim Key As String = $"{SeedValue}|{Version}|{Dimension}|{Range}|{CenterX}|{CenterZ}"
        If Key = GeneratedKey AndAlso CurrentData IsNot Nothing Then Return
        IsGenerating = True
        BtnLoad.IsEnabled = False
        LabStatus.Text = "正在生成地图……（范围越大越慢）"
        GeneratedKey = Key
        RunInNewThread(
            Sub()
                Dim Result As JObject = Nothing
                Dim ErrorText As String = Nothing
                Dim Watch As New Diagnostics.Stopwatch
                Watch.Start()
                Try
                    Dim SizeArg As Integer = If(Range >= 8192, 256, If(Range >= 2048, 512, 1024))
                        Result = ModSeedMap.Generate(SeedValue, Version, Dimension.ToString(), Range, SizeArg, CenterX, CenterZ)
                Catch ex As Exception
                    ErrorText = ex.Message
                End Try
                Watch.Stop()
                RunInUi(
                    Sub()
                        IsGenerating = False
                        BtnLoad.IsEnabled = True
                        If Result Is Nothing Then
                            LabStatus.Text = "生成失败：" & ErrorText
                            GeneratedKey = ""
                            If Not Auto Then Hint("生成地图失败：" & ErrorText, HintType.Red)
                            Return
                        End If
                        Try
                            RenderMap(Result, Watch.Elapsed.TotalSeconds)
                        Catch ex As Exception
                            Logger.Warn(ex, "绘制种子地图失败")
                            LabStatus.Text = "绘制地图失败：" & ex.Message
                        End Try
                    End Sub)
            End Sub, "SeedMapGenerate")
    End Sub


    Private Shared Function ToBgra(ColorValue As Color) As Integer
        Return (CInt(ColorValue.B) << 24) Or (CInt(ColorValue.G) << 16) Or (CInt(ColorValue.R) << 8) Or CInt(ColorValue.A)
    End Function

    Private Sub RenderMap(Data As JObject, Elapsed As Double)
        CurrentData = Data
        MapSize = CInt(Data("size"))
        MapRadius = CInt(Data("radius"))
        MapX0 = CInt(Data("x0"))
        MapZ0 = CInt(Data("z0"))
        MapVersion = ComboVersion.SelectedItem?.ToString()
        CurrentSeed = TextSeed.Text.Trim()
        MapScaleValue = If(Data("scale") Is Nothing, 4, CInt(Data("scale")))
        BiomeNames.Clear()
        TextCenterX.Text = (MapX0 + MapRadius).ToString()
        TextCenterZ.Text = (MapZ0 + MapRadius).ToString()

        Dim Palette As New Dictionary(Of Integer, Color)
        For Each Item As JObject In CType(Data("palette"), JArray)
            Dim BiomeId As Integer = CInt(Item("id"))
            Palette(BiomeId) = GetBiomeColor(Item("name").ToString())
            BiomeNames(BiomeId) = Item("name").ToString()
        Next

        Dim PaletteBgra As New Dictionary(Of Integer, Integer)
        For Each Item As JObject In CType(Data("palette"), JArray)
            PaletteBgra(CInt(Item("id"))) = ToBgra(GetBiomeColor(Item("name").ToString()))
        Next
        Dim PaletteNames As New Dictionary(Of Integer, String)
        For Each Item As JObject In CType(Data("palette"), JArray)
            PaletteNames(CInt(Item("id"))) = Item("name").ToString()
        Next
        Dim DefaultBgra As Integer = ToBgra(Color.FromRgb(150, 156, 164))
        Dim PixelData(MapSize * MapSize - 1) As Integer
        Dim BiomeRows As JArray = CType(Data("biomes"), JArray)
        ReDim BiomePixels(MapSize - 1, MapSize - 1)
        Dim RowCount As Integer = Math.Min(BiomeRows.Count, MapSize)
        For RowIndex = 0 To RowCount - 1
            Dim Ids As Integer() = Nothing
            Try
                Ids = BiomeRows(RowIndex).ToObject(Of Integer())()
            Catch
                Ids = Nothing
            End Try
            If Ids Is Nothing Then Continue For
            Dim ColCount As Integer = Math.Min(Ids.Length, MapSize)
            Dim Base As Integer = RowIndex * MapSize
            For ColumnIndex = 0 To ColCount - 1
                Dim BiomeId As Integer = Ids(ColumnIndex)
                BiomePixels(ColumnIndex, RowIndex) = BiomeId
                Dim ColorValue As Integer
                If Not PaletteBgra.TryGetValue(BiomeId, ColorValue) Then ColorValue = DefaultBgra
                PixelData(Base + ColumnIndex) = ColorValue
            Next
        Next
        Dim Bitmap As BitmapSource = BitmapSource.Create(MapSize, MapSize, 96, 96, PixelFormats.Bgra32, Nothing, PixelData, MapSize * 4)
        ImgMap.Source = Bitmap
        ImgMap.Width = MapSize
        ImgMap.Height = MapSize
        PanMarkers.Width = MapSize
        PanMarkers.Height = MapSize
        MapContent.Width = MapSize
        MapContent.Height = MapSize
        PanMarkers.Children.Clear()
        Items.Clear()

        AddMarkers(Data, "structures")
        AddMarkers(Data, "strongholds")
        AddSpawn(Data)
        DrawGrid()
        ListStructures.ItemsSource = Nothing
        ListStructures.ItemsSource = Items
        Fitted = False
        If KeepView Then
            KeepView = False
            Dispatcher.BeginInvoke(New Action(Sub()
                                                  Try
                                                      Dim VW As Double = If(PanMap.ActualWidth > 0, PanMap.ActualWidth, 600)
                                                      Dim VH As Double = If(PanMap.ActualHeight > 0, PanMap.ActualHeight, 400)
                                                      Dim SV As Double = If(MapScale.ScaleX > 0.05, MapScale.ScaleX, 0.5)
                                                      MapScale.ScaleX = SV
                                                      MapScale.ScaleY = SV
                                                      Dim Px As Double = (ViewCenterX - MapX0) / (2.0 * MapRadius) * MapSize
                                                      Dim Pz As Double = (ViewCenterZ - MapZ0) / (2.0 * MapRadius) * MapSize
                                                      MapOffset.X = VW / 2 - Px * SV
                                                      MapOffset.Y = VH / 2 - Pz * SV
                                                  Catch ex As Exception
                                                  End Try
                                              End Sub), Windows.Threading.DispatcherPriority.Loaded)
        Else
            Dispatcher.BeginInvoke(New Action(Sub() ResetView()), Windows.Threading.DispatcherPriority.Loaded)
        End If
        LabStatus.Text = $"种子 {CurrentSeed} · {GetVersionValue()} · 1 像素 = {MapScaleValue} 格 · 用时 {Elapsed:0.0} 秒 · 结构 {Items.Count} 个"
    End Sub

    Private Function AddMarkers(Data As JObject, PropertyName As String) As Integer
        Try
            Dim ArrayValue As JArray = TryCast(Data(PropertyName), JArray)
            If ArrayValue Is Nothing Then Return 0
            For Each Token As JObject In ArrayValue
                Dim Key As String = Token("t").ToString()
                Dim Kind As StructureKind = Kinds.FirstOrDefault(Function(k) k.Key = Key)
                If Kind Is Nothing Then Continue For
                AddMapMarker(Kind, CInt(Token("x")), CInt(Token("z")))
            Next
        Catch ex As Exception
            Logger.Warn(ex, "绘制结构标记失败")
        End Try
        Return 0
    End Function

    Private Sub AddSpawn(Data As JObject)
        Try
            Dim SpawnObject As JObject = TryCast(Data("spawn"), JObject)
            If SpawnObject Is Nothing Then Return
            Dim Kind As StructureKind = Kinds.FirstOrDefault(Function(k) k.Key = "spawn")
            AddMapMarker(Kind, CInt(SpawnObject("x")), CInt(SpawnObject("z")))
        Catch ex As Exception
            Logger.Warn(ex, "绘制出生点失败")
        End Try
    End Sub

    Private Sub AddMapMarker(Kind As StructureKind, BlockX As Integer, BlockZ As Integer)
        If MarkerDrawn >= 600 Then Return
        MarkerDrawn += 1
        If Kind Is Nothing Then Return
        Dim PixelX As Double = (BlockX - MapX0) / (2.0 * MapRadius) * MapSize
        Dim PixelZ As Double = (BlockZ - MapZ0) / (2.0 * MapRadius) * MapSize
        If PixelX < -20 OrElse PixelZ < -20 OrElse PixelX > MapSize + 20 OrElse PixelZ > MapSize + 20 Then Return
        Dim MarkerSize As Double = 9
        Dim Marker As New Shapes.Ellipse With {
            .Width = MarkerSize, .Height = MarkerSize,
            .Fill = New SolidColorBrush(Kind.Color),
            .Stroke = Brushes.White, .StrokeThickness = 1,
            .Tag = Kind.Key,
            .ToolTip = $"{Kind.Name}{vbCrLf}X: {BlockX}  Z: {BlockZ}",
            .Cursor = Cursors.Hand}
        Canvas.SetLeft(Marker, PixelX - MarkerSize / 2)
        Canvas.SetTop(Marker, PixelZ - MarkerSize / 2)
        Marker.Visibility = If(Kind.Check IsNot Nothing AndAlso Not Kind.Check.Checked, Visibility.Collapsed, Visibility.Visible)
        PanMarkers.Children.Add(Marker)

        Dim Item As New StructureItem With {
            .Key = Kind.Key, .Name = Kind.Name, .BlockX = BlockX, .BlockZ = BlockZ,
            .ColorBrush = New SolidColorBrush(Kind.Color)}
        Items.Add(Item)
    End Sub

    Private Sub DrawGrid()
        Dim Interval As Integer = Math.Max(64, MapRadius / 4)
        Interval = CInt(10 ^ Math.Round(Math.Log10(Interval)))
        Dim GridBrush As New SolidColorBrush(Color.FromArgb(70, 40, 44, 52))
        Dim LabelBrush As New SolidColorBrush(Color.FromArgb(160, 40, 44, 52))
        For BlockX = MapX0 To MapX0 + MapRadius * 2 Step Interval
            Dim PixelX As Double = (BlockX - MapX0) / (2.0 * MapRadius) * MapSize
            Dim Line As New Shapes.Line With {
                .X1 = PixelX, .Y1 = 0, .X2 = PixelX, .Y2 = MapSize,
                .Stroke = GridBrush, .StrokeThickness = 1, .Tag = "grid"}
            PanMarkers.Children.Add(Line)
            Dim Label As New TextBlock With {
                .Text = BlockX.ToString(), .FontSize = 10, .Foreground = LabelBrush, .Tag = "grid"}
            Canvas.SetLeft(Label, PixelX + 2)
            Canvas.SetTop(Label, 2)
            PanMarkers.Children.Add(Label)
        Next
        For BlockZ = MapZ0 To MapZ0 + MapRadius * 2 Step Interval
            Dim PixelZ As Double = (BlockZ - MapZ0) / (2.0 * MapRadius) * MapSize
            Dim Line As New Shapes.Line With {
                .X1 = 0, .Y1 = PixelZ, .X2 = MapSize, .Y2 = PixelZ,
                .Stroke = GridBrush, .StrokeThickness = 1, .Tag = "grid"}
            PanMarkers.Children.Add(Line)
            Dim Label As New TextBlock With {
                .Text = BlockZ.ToString(), .FontSize = 10, .Foreground = LabelBrush, .Tag = "grid"}
            Canvas.SetLeft(Label, 2)
            Canvas.SetTop(Label, PixelZ + 1)
            PanMarkers.Children.Add(Label)
        Next
    End Sub

    Private Sub CenterSelected()
        Dim Item As StructureItem = TryCast(ListStructures.SelectedItem, StructureItem)
        If Item Is Nothing OrElse MapSize <= 0 Then Return
        Dim PixelX As Double = (Item.BlockX - MapX0) / (2.0 * MapRadius) * MapSize
        Dim PixelZ As Double = (Item.BlockZ - MapZ0) / (2.0 * MapRadius) * MapSize
        MapOffset.X = PanMap.ActualWidth / 2 - PixelX * MapScale.ScaleX
        MapOffset.Y = PanMap.ActualHeight / 2 - PixelZ * MapScale.ScaleY
        LabStatus.Text = $"{Item.Name}  X {Item.BlockX}  Z {Item.BlockZ}"
    End Sub

    Private Sub ResetView()
        If MapSize <= 0 Then Return
        Dim ViewWidth As Double = PanMap.ActualWidth
        Dim ViewHeight As Double = PanMap.ActualHeight
        If ViewWidth <= 0 OrElse ViewHeight <= 0 Then ViewWidth = 600 : ViewHeight = 400
        Dim ScaleValue As Double = Math.Min(ViewWidth / MapSize, ViewHeight / MapSize) * 0.98
        If ScaleValue <= 0 Then ScaleValue = 0.5
        MapScale.ScaleX = ScaleValue
        MapScale.ScaleY = ScaleValue
        MapOffset.X = (ViewWidth - MapSize * ScaleValue) / 2
        MapOffset.Y = (ViewHeight - MapSize * ScaleValue) / 2
        Fitted = True
    End Sub

    Private Sub ZoomBy(Factor As Double, ScreenPoint As Point)
        If MapSize <= 0 Then Return
        Dim NewScale As Double = MapScale.ScaleX * Factor
        If NewScale < 0.1 Then NewScale = 0.1
        If NewScale > 40 Then NewScale = 40
        Factor = NewScale / MapScale.ScaleX
        Dim ContentPoint As Point = New Point((ScreenPoint.X - MapOffset.X) / MapScale.ScaleX, (ScreenPoint.Y - MapOffset.Y) / MapScale.ScaleY)
        MapScale.ScaleX = NewScale
        MapScale.ScaleY = NewScale
        MapOffset.X = ScreenPoint.X - ContentPoint.X * NewScale
        MapOffset.Y = ScreenPoint.Y - ContentPoint.Y * NewScale
        CheckExpand()
    End Sub

    Private Sub UpdateCoordinates(ContentPoint As Point)
        If MapSize <= 0 Then Return
        Dim PixelX As Integer = CInt(ContentPoint.X)
        Dim PixelZ As Integer = CInt(ContentPoint.Y)
        Dim BlockX As Integer = CInt(MapX0 + ContentPoint.X / MapSize * (2.0 * MapRadius))
        Dim BlockZ As Integer = CInt(MapZ0 + ContentPoint.Y / MapSize * (2.0 * MapRadius))
        Dim BiomeText As String = ""
        If BiomePixels IsNot Nothing AndAlso PixelX >= 0 AndAlso PixelZ >= 0 AndAlso PixelX < MapSize AndAlso PixelZ < MapSize Then
            Dim BiomeId As Integer = BiomePixels(PixelX, PixelZ)
            Dim Name As String = ""
            If BiomeNames.TryGetValue(BiomeId, Name) Then BiomeText = " · 生物群系：" & TranslateBiome(Name)
        End If
        LabStatus.Text = $"X {BlockX}  Z {BlockZ}{BiomeText}"
    End Sub

    Private Function TranslateBiome(Name As String) As String
        Dim Map As New Dictionary(Of String, String) From {
            {"ocean", "海洋"}, {"deep_ocean", "深海"}, {"warm_ocean", "暖水海洋"}, {"lukewarm_ocean", "温水海洋"},
            {"cold_ocean", "冷水海洋"}, {"deep_cold_ocean", "冷水深海"}, {"frozen_ocean", "冻洋"},
            {"river", "河流"}, {"frozen_river", "冻河"}, {"beach", "沙滩"}, {"snowy_beach", "雪沙滩"},
            {"stony_shore", "石岸"}, {"plains", "平原"}, {"sunflower_plains", "向日葵平原"}, {"snowy_plains", "雪原"},
            {"desert", "沙漠"}, {"forest", "森林"}, {"flower_forest", "繁花森林"}, {"birch_forest", "桦木森林"},
            {"old_growth_birch_forest", "原始桦木森林"}, {"dark_forest", "黑森林"}, {"taiga", "针叶林"},
            {"snowy_taiga", "积雪针叶林"}, {"old_growth_pine_taiga", "原始松木针叶林"}, {"old_growth_spruce_taiga", "原始云杉针叶林"},
            {"jungle", "丛林"}, {"sparse_jungle", "稀疏丛林"}, {"bamboo_jungle", "竹林"}, {"swamp", "沼泽"},
            {"mangrove_swamp", "红树林沼泽"}, {"savanna", "热带草原"}, {"savanna_plateau", "热带高原"},
            {"windswept_savanna", "风袭热带草原"}, {"badlands", "恶地"}, {"eroded_badlands", "风蚀恶地"},
            {"wooded_badlands", "繁茂恶地"}, {"meadow", "草甸"}, {"cherry_grove", "樱花树林"}, {"grove", "雪林"},
            {"snowy_slopes", "积雪山坡"}, {"jagged_peaks", "尖峭山峰"}, {"frozen_peaks", "冰封山峰"},
            {"stony_peaks", "裸岩山峰"}, {"windswept_hills", "风袭丘陵"}, {"windswept_gravelly_hills", "风袭砂砾丘陵"},
            {"windswept_forest", "风袭森林"}, {"mushroom_fields", "蘑菇岛"}, {"ice_spikes", "冰刺之地"},
            {"lush_caves", "繁茂洞穴"}, {"dripstone_caves", "溶洞"}, {"deep_dark", "深暗之域"},
            {"nether_wastes", "下界荒地"}, {"soul_sand_valley", "灵魂沙峡谷"}, {"crimson_forest", "绯红森林"},
            {"warped_forest", "诡异森林"}, {"basalt_deltas", "玄武岩三角洲"}, {"the_end", "末地"},
            {"small_end_islands", "末地小型岛屿"}, {"end_midlands", "末地内陆"}, {"end_highlands", "末地高地"},
            {"end_barrens", "末地荒地"}, {"the_void", "虚空"}, {"pale_garden", "苍白之园"}}
        Dim Chinese As String = ""
        If Map.TryGetValue(Name, Chinese) Then Return Chinese
        Return Name
    End Function

    Private Function GetBiomeColor(Name As String) As Color
        If Name Is Nothing Then Return Color.FromRgb(150, 156, 164)
        Dim N As String = Name.ToLower()
        If N.Contains("deep_dark") Then Return Color.FromRgb(25, 30, 45)
        If N.Contains("lush_caves") Then Return Color.FromRgb(80, 140, 60)
        If N.Contains("dripstone") Then Return Color.FromRgb(160, 130, 100)
        If N.Contains("cherry") Then Return Color.FromRgb(245, 190, 210)
        If N.Contains("pale_garden") Then Return Color.FromRgb(150, 160, 152)
        If N.Contains("mangrove") Then Return Color.FromRgb(60, 110, 90)
        If N.Contains("nether_wastes") Then Return Color.FromRgb(150, 50, 50)
        If N.Contains("soul_sand") Then Return Color.FromRgb(110, 80, 70)
        If N.Contains("crimson") Then Return Color.FromRgb(120, 40, 60)
        If N.Contains("warped") Then Return Color.FromRgb(60, 110, 110)
        If N.Contains("basalt") Then Return Color.FromRgb(80, 80, 80)
        If N.Contains("the_end") OrElse N.Contains("end_") Then Return Color.FromRgb(230, 220, 170)
        If N.Contains("frozen") OrElse N.Contains("snowy") OrElse N.Contains("ice_spikes") OrElse N.Contains("snowy_slopes") Then
            Return Color.FromRgb(178, 205, 225)
        End If
        If N.Contains("peak") OrElse N.Contains("windswept") OrElse N.Contains("jagged") OrElse N.Contains("stony_peaks") OrElse N.Contains("mountain") OrElse N.Contains("gravelly") Then
            Return Color.FromRgb(140, 140, 140)
        End If
        If N.Contains("badlands") OrElse N.Contains("mesa") OrElse N.Contains("desert") Then Return Color.FromRgb(219, 145, 70)
        If N.Contains("savanna") Then Return Color.FromRgb(190, 180, 90)
        If N.Contains("jungle") Then Return Color.FromRgb(70, 130, 40)
        If N.Contains("swamp") Then Return Color.FromRgb(80, 120, 90)
        If N.Contains("taiga") OrElse N.Contains("forest") OrElse N.Contains("birch") OrElse N.Contains("grove") OrElse N.Contains("meadow") Then Return Color.FromRgb(90, 150, 80)
        If N.Contains("plains") OrElse N.Contains("sunflower") Then Return Color.FromRgb(140, 190, 110)
        If N.Contains("ocean") OrElse N.Contains("river") Then Return Color.FromRgb(50, 100, 180)
        If N.Contains("beach") OrElse N.Contains("shore") Then Return Color.FromRgb(222, 208, 160)
        Return Color.FromRgb(150, 156, 164)
    End Function


    Private Sub CheckExpand()
        Try
            If IsGenerating OrElse MapSize <= 0 OrElse MapRadius <= 0 Then Return
            If (Date.Now - LastExpandAt).TotalMilliseconds < 2500 Then Return
            Dim VW As Double = PanMap.ActualWidth
            Dim VH As Double = PanMap.ActualHeight
            If VW <= 0 OrElse VH <= 0 Then Return
            Dim PxLeft As Double = (0 - MapOffset.X) / MapScale.ScaleX
            Dim PxTop As Double = (0 - MapOffset.Y) / MapScale.ScaleY
            Dim PxRight As Double = (VW - MapOffset.X) / MapScale.ScaleX
            Dim PxBottom As Double = (VH - MapOffset.Y) / MapScale.ScaleY
            Dim BlockLeft As Double = MapX0 + PxLeft / MapSize * 2.0 * MapRadius
            Dim BlockTop As Double = MapZ0 + PxTop / MapSize * 2.0 * MapRadius
            Dim BlockRight As Double = MapX0 + PxRight / MapSize * 2.0 * MapRadius
            Dim BlockBottom As Double = MapZ0 + PxBottom / MapSize * 2.0 * MapRadius
            Dim MarginBlock As Double = -MapRadius * 0.05
            Dim OutLeft As Boolean = BlockLeft < MapX0 + MarginBlock
            Dim OutTop As Boolean = BlockTop < MapZ0 + MarginBlock
            Dim OutRight As Boolean = BlockRight > MapX0 + 2.0 * MapRadius - MarginBlock
            Dim OutBottom As Boolean = BlockBottom > MapZ0 + 2.0 * MapRadius - MarginBlock
            If Not (OutLeft OrElse OutTop OrElse OutRight OrElse OutBottom) Then Return
            Dim CenterBlockX As Integer = CInt((BlockLeft + BlockRight) / 2)
            Dim CenterBlockZ As Integer = CInt((BlockTop + BlockBottom) / 2)
            Dim NewRadius As Integer = Math.Min(32768, Math.Max(MapRadius * 2, 2048))
            Dim Found As Boolean = False
            For Each Item As MyComboBoxItem In ComboRange.Items
                If CInt(Item.Tag) >= NewRadius Then
                    ComboRange.SelectedItem = Item
                    Found = True
                    Exit For
                End If
            Next
            If Not Found Then ComboRange.SelectedIndex = ComboRange.Items.Count - 1
            TextCenterX.Text = CenterBlockX.ToString()
            TextCenterZ.Text = CenterBlockZ.ToString()
            LastExpandAt = Date.Now
            ViewCenterX = (BlockLeft + BlockRight) / 2
            ViewCenterZ = (BlockTop + BlockBottom) / 2
            KeepView = True
            Generate(False)
        Catch ex As Exception
        End Try
    End Sub

End Class
