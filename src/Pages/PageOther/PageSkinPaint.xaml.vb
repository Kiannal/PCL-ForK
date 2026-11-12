Imports System.Windows.Controls.Primitives
Imports System.IO
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Windows.Shapes

Public Class PageSkinPaint
    Inherits MyPageRight

    Private Const SkinSize As Integer = 64
    Private Bitmap As New WriteableBitmap(SkinSize, SkinSize, 96, 96, PixelFormats.Bgra32, Nothing)
    Private ReadOnly Pixels(SkinSize * SkinSize - 1) As Integer
    Private ReadOnly UndoStack As New Stack(Of Integer())
    Private ReadOnly RedoStack As New Stack(Of Integer())
    Private PaintColor As Color = Windows.Media.Color.FromRgb(120, 170, 255)
    Private IsEraser As Boolean
    Private ShowFront As Boolean = True
    Private ReadOnly Parts As New List(Of SkinPart)
    Private ActivePart As SkinPart
    Private ColCurrent As Border
    Private IsDrawing As Boolean

    Private Class SkinPart
        Public Name As String
        Public Ax As Integer, Ay As Integer, Aw As Integer, Ah As Integer
        Public Dx As Double, Dy As Double, Dw As Double, Dh As Double
        Public Element As Rectangle
        Public ImageEl As Image
    End Class

    Public Sub New()
        InitializeComponent()
        FillWhite(False)
        BuildToolbar()
        BuildPalette()
        BuildDoll()
        AddHandler PanDoll.MouseLeftButtonDown, AddressOf PanDoll_MouseDown
        AddHandler PanDoll.MouseRightButtonDown, AddressOf PanDoll_MouseDown
        AddHandler PanDoll.MouseMove, AddressOf PanDoll_MouseMove
        AddHandler PanDoll.MouseLeftButtonUp, AddressOf PanDoll_MouseUp
        AddHandler PanDoll.MouseRightButtonUp, AddressOf PanDoll_MouseUp
        AddHandler Me.PreviewMouseLeftButtonDown, AddressOf PageMouseDown
        AddHandler Me.PreviewMouseRightButtonDown, AddressOf PageMouseDown
        AddHandler Me.PreviewMouseMove, AddressOf PageMouseMove
        AddHandler Me.PreviewMouseLeftButtonUp, AddressOf PageMouseUp
        AddHandler Me.PreviewMouseRightButtonUp, AddressOf PageMouseUp
        Try
            Dim Checker As New DrawingBrush
            Checker.TileMode = TileMode.Tile
            Checker.Viewport = New Rect(0, 0, 16, 16)
            Checker.ViewportUnits = BrushMappingMode.Absolute
            Dim Group As New DrawingGroup
            Group.Children.Add(New GeometryDrawing(New SolidColorBrush(Windows.Media.Color.FromRgb(32, 32, 36)), Nothing, New RectangleGeometry(New Rect(0, 0, 16, 16))))
            Group.Children.Add(New GeometryDrawing(New SolidColorBrush(Windows.Media.Color.FromRgb(40, 40, 45)), Nothing, New RectangleGeometry(New Rect(0, 0, 8, 8))))
            Group.Children.Add(New GeometryDrawing(New SolidColorBrush(Windows.Media.Color.FromRgb(40, 40, 45)), Nothing, New RectangleGeometry(New Rect(8, 8, 8, 8))))
            Checker.Drawing = Group
            PanDoll.Background = Checker
        Catch ex As Exception
        End Try
    End Sub

    Private Sub FillWhite(Record As Boolean)
        If Record Then PushUndo()
        For I = 0 To Pixels.Length - 1
            Pixels(I) = &HFFFFFFFF
        Next
        Render()
    End Sub

    Private Function BuildSource() As BitmapSource
        Using Bmp As New System.Drawing.Bitmap(SkinSize, SkinSize)
            For Y = 0 To SkinSize - 1
                For X = 0 To SkinSize - 1
                    Dim V As Integer = Pixels(Y * SkinSize + X)
                    Bmp.SetPixel(X, Y, System.Drawing.Color.FromArgb((V >> 24) And &HFF, (V >> 16) And &HFF, (V >> 8) And &HFF, V And &HFF))
                Next
            Next
            Dim H As IntPtr = Bmp.GetHbitmap()
            Try
                Dim Src As BitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(H, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())
                Src.Freeze()
                Return Src
            Finally
                DeleteObject(H)
            End Try
        End Using
    End Function

    <Runtime.InteropServices.DllImport("gdi32.dll")>
    Private Shared Function DeleteObject(hObject As IntPtr) As Boolean
    End Function

    Private Sub Render()
        Dim Src As BitmapSource = BuildSource()
        For Each P In Parts
            If P.ImageEl IsNot Nothing Then
                Dim Cropped As New CroppedBitmap(Src, New Int32Rect(P.Ax, P.Ay, P.Aw, P.Ah))
                Cropped.Freeze()
                P.ImageEl.Source = Cropped
            End If
        Next
    End Sub

    Private Sub BuildToolbar()
        PanToolbar.Children.Clear()
        PanToolbar.Children.Add(MakeButton("画笔", Sub()
                                                    IsEraser = False
                                                    If LabTip IsNot Nothing Then LabTip.Text = "画笔"
                                                End Sub))
        PanToolbar.Children.Add(MakeButton("橡皮擦", Sub()
                                                      IsEraser = True
                                                      If LabTip IsNot Nothing Then LabTip.Text = "橡皮擦（擦回白色）"
                                                  End Sub))
        PanToolbar.Children.Add(MakeButton("撤回上一步", Sub() Undo()))
        PanToolbar.Children.Add(MakeButton("重做", Sub() Redo()))
        PanToolbar.Children.Add(MakeButton("清空", Sub() FillWhite(True)))
        PanToolbar.Children.Add(MakeButton("切换正/背面", Sub()
                                                          ShowFront = Not ShowFront
                                                          BuildDoll()
                                                      End Sub))
        PanToolbar.Children.Add(MakeButton("生成皮肤", Sub() Generate(), True))
        PanToolbar.Children.Add(MakeButton("经典绘制", Sub()
                                                      Try
                                                          Dim F As New FrmSkinPainter With {.Owner = FrmMain}
                                                          F.ShowDialog()
                                                      Catch ex As Exception
                                                      End Try
                                                  End Sub))
    End Sub

    Private Function MakeButton(Text As String, Action As Action, Optional Highlight As Boolean = False) As MyButton
        Dim Btn As New MyButton With {
            .Text = Text, .Height = 30, .MinWidth = 92, .Padding = New Thickness(12, 0, 12, 0),
            .Margin = New Thickness(0, 0, 8, 8)
        }
        If Highlight Then Btn.ColorType = MyButton.ColorState.Highlight
        AddHandler Btn.Click, Sub() Action()
        Return Btn
    End Function

    Private Hue As Double = 0.58
    Private Sat As Double = 0.55
    Private Val As Double = 1.0
    Private SvCanvas As Canvas
    Private SvBase As Rectangle
    Private SvCursor As Ellipse
    Private HueCanvas As Canvas
    Private HueCursor As Rectangle
    Private ColorPreview As Border
    Private HexBox As TextBox
    Private UpdatingHex As Boolean

    Private Sub BuildPalette()
        PanPalette.Children.Clear()
        Dim Host As New StackPanel With {.Margin = New Thickness(0, 0, 0, 8)}
        Dim ColorRow As New StackPanel With {.Orientation = Orientation.Horizontal}
        SvCanvas = New Canvas With {.Width = 220, .Height = 140, .Background = Brushes.Transparent}
        SvBase = New Rectangle With {.Width = 220, .Height = 140}
        SvCanvas.Children.Add(SvBase)
        Dim WhiteLayer As New Rectangle With {.Width = 220, .Height = 140}
        Dim WhiteBrush As New LinearGradientBrush With {.StartPoint = New Point(0, 0), .EndPoint = New Point(1, 0)}
        WhiteBrush.GradientStops.Add(New GradientStop(Colors.White, 0))
        WhiteBrush.GradientStops.Add(New GradientStop(Color.FromArgb(0, 255, 255, 255), 1))
        WhiteLayer.Fill = WhiteBrush
        SvCanvas.Children.Add(WhiteLayer)
        Dim BlackLayer As New Rectangle With {.Width = 220, .Height = 140}
        Dim BlackBrush As New LinearGradientBrush With {.StartPoint = New Point(0, 0), .EndPoint = New Point(0, 1)}
        BlackBrush.GradientStops.Add(New GradientStop(Color.FromArgb(0, 0, 0, 0), 0))
        BlackBrush.GradientStops.Add(New GradientStop(Colors.Black, 1))
        BlackLayer.Fill = BlackBrush
        SvCanvas.Children.Add(BlackLayer)
        SvCursor = New Ellipse With {.Width = 12, .Height = 12, .Stroke = Brushes.White, .StrokeThickness = 2, .IsHitTestVisible = False}
        SvCanvas.Children.Add(SvCursor)
        Host.Children.Add(SvCanvas)
        Dim HuePanel As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 8, 0, 0)}
        HueCanvas = New Canvas With {.Width = 220, .Height = 18, .Background = Brushes.Transparent}
        Dim HueBar As New Rectangle With {.Width = 220, .Height = 18, .RadiusX = 6, .RadiusY = 6}
        Dim Hb As New LinearGradientBrush With {.StartPoint = New Point(0, 0.5), .EndPoint = New Point(1, 0.5)}
        For I = 0 To 6
            Hb.GradientStops.Add(New GradientStop(HsvToColor(I / 6.0, 1, 1), I / 6.0))
        Next
        HueBar.Fill = Hb
        HueCanvas.Children.Add(HueBar)
        HueCursor = New Rectangle With {.Width = 4, .Height = 22, .Fill = Brushes.White, .Stroke = New SolidColorBrush(Color.FromRgb(30, 30, 34)), .StrokeThickness = 1, .IsHitTestVisible = False}
        HueCanvas.Children.Add(HueCursor)
        HuePanel.Children.Add(HueCanvas)
        Host.Children.Add(HuePanel)

        Dim Bottom As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 8, 0, 0)}
        ColorPreview = New Border With {.Width = 30, .Height = 30, .CornerRadius = New CornerRadius(6), .BorderThickness = New Thickness(1), .BorderBrush = New SolidColorBrush(Color.FromRgb(90, 94, 102))}
        Bottom.Children.Add(ColorPreview)
        HexBox = New TextBox With {.Text = "#78AAFF", .Width = 100, .Height = 30, .FontSize = 12, .Margin = New Thickness(10, 0, 0, 0), .VerticalContentAlignment = VerticalAlignment.Center}
        HexBox.SetResourceReference(TextBox.BackgroundProperty, "ColorBrushGray7")
        HexBox.SetResourceReference(TextBox.ForegroundProperty, "ColorBrush1")
        HexBox.SetResourceReference(TextBox.BorderBrushProperty, "ColorBrushGray6")
        Bottom.Children.Add(HexBox)
        Host.Children.Add(Bottom)

        AddHandler SvCanvas.MouseLeftButtonDown, AddressOf PickSv
        AddHandler SvCanvas.MouseMove, Sub(s, e) If e.LeftButton = MouseButtonState.Pressed Then PickSv(s, e)
        AddHandler HueCanvas.MouseLeftButtonDown, AddressOf PickHue
        AddHandler HueCanvas.MouseMove, Sub(s, e) If e.LeftButton = MouseButtonState.Pressed Then PickHue(s, e)
        AddHandler HexBox.TextChanged, Sub()
                                          If UpdatingHex Then Return
                                          Try
                                              Dim Tx As String = HexBox.Text.Trim().TrimStart("#"c)
                                              If Tx.Length = 6 Then
                                                  Dim C As Color = Color.FromRgb(Convert.ToByte(Tx.Substring(0, 2), 16), Convert.ToByte(Tx.Substring(2, 2), 16), Convert.ToByte(Tx.Substring(4, 2), 16))
                                                  PaintColor = C
                                                  IsEraser = False
                                                  If ColorPreview IsNot Nothing Then ColorPreview.Background = New SolidColorBrush(C)
                                              End If
                                          Catch
                                          End Try
                                      End Sub

        Dim Presets As New WrapPanel With {.Margin = New Thickness(0, 8, 0, 0)}
        Dim ColorsList As Color() = {
            Color.FromRgb(255, 255, 255), Color.FromRgb(221, 221, 221), Color.FromRgb(136, 136, 136), Color.FromRgb(60, 60, 60),
            Color.FromRgb(255, 120, 120), Color.FromRgb(180, 40, 40), Color.FromRgb(255, 170, 90), Color.FromRgb(255, 220, 90),
            Color.FromRgb(120, 220, 120), Color.FromRgb(40, 150, 60), Color.FromRgb(100, 220, 220), Color.FromRgb(30, 140, 150),
            Color.FromRgb(110, 150, 255), Color.FromRgb(40, 60, 180), Color.FromRgb(220, 130, 255), Color.FromRgb(140, 50, 160),
            Color.FromRgb(240, 190, 160), Color.FromRgb(180, 120, 80), Color.FromRgb(90, 60, 30), Color.FromRgb(240, 220, 190)}
        For Each C As Color In ColorsList
            Dim B As New Border With {.Width = 22, .Height = 22, .Margin = New Thickness(0, 0, 5, 5), .CornerRadius = New CornerRadius(5), .Background = New SolidColorBrush(C), .Cursor = Cursors.Hand, .BorderThickness = New Thickness(1), .BorderBrush = New SolidColorBrush(Color.FromRgb(70, 70, 78))}
            Dim Cap As Color = C
            AddHandler B.MouseLeftButtonDown, Sub()
                                                  PaintColor = Cap
                                                  IsEraser = False
                                                  If ColorPreview IsNot Nothing Then ColorPreview.Background = New SolidColorBrush(Cap)
                                                  If HexBox IsNot Nothing Then
                                                      UpdatingHex = True
                                                      HexBox.Text = "#" & Cap.R.ToString("X2") & Cap.G.ToString("X2") & Cap.B.ToString("X2")
                                                      UpdatingHex = False
                                                  End If
                                              End Sub
            Presets.Children.Add(B)
        Next
        Host.Children.Add(Presets)
        PanPalette.Children.Add(Host)
        UpdatePicker()
    End Sub

    Private Sub PickSv(s As Object, e As MouseEventArgs)
        Dim P As Point = e.GetPosition(SvCanvas)
        Sat = Math.Max(0, Math.Min(1, P.X / 220))
        Val = Math.Max(0, Math.Min(1, 1 - P.Y / 140))
        UpdatePicker()
    End Sub

    Private Sub PickHue(s As Object, e As MouseEventArgs)
        Dim P As Point = e.GetPosition(HueCanvas)
        Hue = Math.Max(0, Math.Min(0.999, P.X / 220))
        UpdatePicker()
    End Sub

    Private Sub UpdatePicker()
        Dim BaseColor As Color = HsvToColor(Hue, 1, 1)
        If SvBase IsNot Nothing Then SvBase.Fill = New SolidColorBrush(BaseColor)
        PaintColor = HsvToColor(Hue, Sat, Val)
        IsEraser = False
        If ColorPreview IsNot Nothing Then ColorPreview.Background = New SolidColorBrush(PaintColor)
        If HexBox IsNot Nothing Then
            UpdatingHex = True
            HexBox.Text = "#" & PaintColor.R.ToString("X2") & PaintColor.G.ToString("X2") & PaintColor.B.ToString("X2")
            UpdatingHex = False
        End If
        If SvCursor IsNot Nothing Then
            Canvas.SetLeft(SvCursor, Sat * 220 - 6)
            Canvas.SetTop(SvCursor, (1 - Val) * 140 - 6)
        End If
        If HueCursor IsNot Nothing Then Canvas.SetLeft(HueCursor, Hue * 220 - 2)
        If LabTip IsNot Nothing Then LabTip.Text = "画笔 · #" & PaintColor.R.ToString("X2") & PaintColor.G.ToString("X2") & PaintColor.B.ToString("X2")
    End Sub

    Private Function HsvToColor(h As Double, s As Double, v As Double) As Color
        Dim I As Integer = CInt(Math.Floor(h * 6)) Mod 6
        Dim F As Double = h * 6 - Math.Floor(h * 6)
        Dim Pv As Double = v * (1 - s)
        Dim Qv As Double = v * (1 - F * s)
        Dim Tv As Double = v * (1 - (1 - F) * s)
        Dim R As Double = 0, G As Double = 0, B As Double = 0
        Select Case I
            Case 0 : R = v : G = Tv : B = Pv
            Case 1 : R = Qv : G = v : B = Pv
            Case 2 : R = Pv : G = v : B = Tv
            Case 3 : R = Pv : G = Qv : B = v
            Case 4 : R = Tv : G = Pv : B = v
            Case Else : R = v : G = Pv : B = Qv
        End Select
        Return Color.FromRgb(CByte(R * 255), CByte(G * 255), CByte(B * 255))
    End Function


    Private Sub BuildDoll()
        PanDoll.Children.Clear()
        Parts.Clear()
        If ShowFront Then
            AddPart("头", 8, 8, 8, 8, 76, 0, 96, 96)
            AddPart("身体", 20, 20, 8, 12, 76, 96, 96, 144)
            AddPart("右臂", 44, 20, 4, 12, 28, 96, 48, 144)
            AddPart("左臂", 36, 52, 4, 12, 172, 96, 48, 144)
            AddPart("右腿", 4, 20, 4, 12, 76, 240, 48, 144)
            AddPart("左腿", 20, 52, 4, 12, 124, 240, 48, 144)
            LabTip.Text = "正面视图"
        Else
            AddPart("后脑", 24, 8, 8, 8, 76, 0, 96, 96)
            AddPart("后背", 32, 20, 8, 12, 76, 96, 96, 144)
            AddPart("右臂后", 52, 20, 4, 12, 28, 96, 48, 144)
            AddPart("左臂后", 44, 52, 4, 12, 172, 96, 48, 144)
            AddPart("右腿后", 12, 20, 4, 12, 76, 240, 48, 144)
            AddPart("左腿后", 28, 52, 4, 12, 124, 240, 48, 144)
            LabTip.Text = "背面视图"
        End If
    End Sub

    Private Sub AddPart(Name As String, Ax As Integer, Ay As Integer, Aw As Integer, Ah As Integer, Dx As Double, Dy As Double, Dw As Double, Dh As Double)
        Dim P As New SkinPart With {.Name = Name, .Ax = Ax, .Ay = Ay, .Aw = Aw, .Ah = Ah, .Dx = Dx, .Dy = Dy, .Dw = Dw, .Dh = Dh}
        Dim ImgEl As New Image With {.Width = Dw, .Height = Dh, .Stretch = Stretch.Fill}
        RenderOptions.SetBitmapScalingMode(ImgEl, BitmapScalingMode.NearestNeighbor)
        Canvas.SetLeft(ImgEl, Dx)
        Canvas.SetTop(ImgEl, Dy)
        P.ImageEl = ImgEl
        Dim GridOverlay As New Rectangle With {.Width = Dw, .Height = Dh, .IsHitTestVisible = False}
        Dim GridBrush As New DrawingBrush With {.TileMode = TileMode.Tile, .Viewport = New Rect(0, 0, 12, 12), .ViewportUnits = BrushMappingMode.Absolute}
        Dim GridDrawing As New DrawingGroup
        GridDrawing.Children.Add(New GeometryDrawing(Nothing, New Pen(New SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 0.6), New RectangleGeometry(New Rect(0, 0, 12, 12))))
        GridBrush.Drawing = GridDrawing
        GridOverlay.Fill = GridBrush
        Canvas.SetLeft(GridOverlay, Dx)
        Canvas.SetTop(GridOverlay, Dy)
        PanDoll.Children.Add(ImgEl)
        PanDoll.Children.Add(GridOverlay)
        P.Element = GridOverlay
        Parts.Add(P)
    End Sub


    Private Function FindParent(Of T As DependencyObject)(Start As DependencyObject) As T
        Dim Current As DependencyObject = Start
        While Current IsNot Nothing
            If TypeOf Current Is T Then Return DirectCast(Current, T)
            Current = VisualTreeHelper.GetParent(Current)
        End While
        Return Nothing
    End Function

    Private Sub PageMouseDown(S As Object, E As MouseButtonEventArgs)
        Dim Hit As DependencyObject = TryCast(Me.InputHitTest(E.GetPosition(Me)), DependencyObject)
        If Hit Is Nothing Then Return
        If SvCanvas IsNot Nothing Then
            Dim SvPos As Point = E.GetPosition(SvCanvas)
            If SvPos.X >= 0 AndAlso SvPos.Y >= 0 AndAlso SvPos.X <= 220 AndAlso SvPos.Y <= 140 Then
                Sat = Math.Max(0, Math.Min(1, SvPos.X / 220))
                Val = Math.Max(0, Math.Min(1, 1 - SvPos.Y / 140))
                UpdatePicker()
                E.Handled = True
                Return
            End If
        End If
        If HueCanvas IsNot Nothing Then
            Dim HuePos As Point = E.GetPosition(HueCanvas)
            If HuePos.X >= 0 AndAlso HuePos.Y >= 0 AndAlso HuePos.X <= 220 AndAlso HuePos.Y <= 18 Then
                Hue = Math.Max(0, Math.Min(0.999, HuePos.X / 220))
                UpdatePicker()
                E.Handled = True
                Return
            End If
        End If
        Dim Btn As ButtonBase = FindParent(Of ButtonBase)(Hit)
        If Btn IsNot Nothing Then Return
        Dim Pos As Point = E.GetPosition(PanDoll)
        If Pos.X >= 0 AndAlso Pos.Y >= 0 AndAlso Pos.X < PanDoll.ActualWidth AndAlso Pos.Y < PanDoll.ActualHeight Then
            PushUndo()
            IsDrawing = True
            PaintCanvasAt(Pos, E.ChangedButton = MouseButton.Right OrElse IsEraser)
            E.Handled = True
        End If
    End Sub

    Private Sub PageMouseMove(S As Object, E As MouseEventArgs)
        If Not IsDrawing Then Return
        PaintCanvasAt(E.GetPosition(PanDoll), E.RightButton = MouseButtonState.Pressed OrElse IsEraser)
    End Sub

    Private Sub PageMouseUp(S As Object, E As MouseButtonEventArgs)
        IsDrawing = False
    End Sub

    Private Sub PanDoll_MouseDown(S As Object, E As MouseButtonEventArgs)
        PushUndo()
        IsDrawing = True
        PaintCanvasAt(E.GetPosition(PanDoll), E.ChangedButton = MouseButton.Right OrElse IsEraser)
        PanDoll.CaptureMouse()
    End Sub

    Private Sub PanDoll_MouseMove(S As Object, E As MouseEventArgs)
        If Not IsDrawing Then Return
        PaintCanvasAt(E.GetPosition(PanDoll), E.RightButton = MouseButtonState.Pressed OrElse IsEraser)
    End Sub

    Private Sub PanDoll_MouseUp(S As Object, E As MouseButtonEventArgs)
        IsDrawing = False
        PanDoll.ReleaseMouseCapture()
    End Sub

    Private Sub PaintCanvasAt(Pos As Point, EraseIt As Boolean)
        For Each P In Parts
            If Pos.X >= P.Dx AndAlso Pos.X < P.Dx + P.Dw AndAlso Pos.Y >= P.Dy AndAlso Pos.Y < P.Dy + P.Dh Then
                Dim Lx As Integer = CInt((Pos.X - P.Dx) / P.Dw * P.Aw)
                Dim Ly As Integer = CInt((Pos.Y - P.Dy) / P.Dh * P.Ah)
                If Lx >= 0 AndAlso Ly >= 0 AndAlso Lx < P.Aw AndAlso Ly < P.Ah Then
                    SetPixel(P.Ax + Lx, P.Ay + Ly, If(EraseIt, Colors.White, ActiveColor()))
                    Render()
                End If
                Return
            End If
        Next
    End Sub

    Private Sub StartDraw(P As SkinPart, E As MouseButtonEventArgs)
        PushUndo()
        ActivePart = P
        IsDrawing = True
        PaintAt(P, E.GetPosition(P.Element), If(E.ChangedButton = MouseButton.Right, True, IsEraser))
        DirectCast(P.Element, UIElement).CaptureMouse()
    End Sub

    Private Sub ContinueDraw(P As SkinPart, E As MouseEventArgs)
        If Not IsDrawing OrElse ActivePart IsNot P Then Return
        PaintAt(P, E.GetPosition(P.Element), If(E.RightButton = MouseButtonState.Pressed, True, IsEraser))
    End Sub

    Private Sub StopDraw()
        IsDrawing = False
        ActivePart = Nothing
    End Sub

    Private Sub PaintAt(P As SkinPart, Pos As Point, EraseIt As Boolean)
        If P.Element.ActualWidth <= 0 OrElse P.Element.ActualHeight <= 0 Then Return
        Dim Lx As Integer = CInt(Pos.X / P.Element.ActualWidth * P.Aw)
        Dim Ly As Integer = CInt(Pos.Y / P.Element.ActualHeight * P.Ah)
        If Lx < 0 OrElse Ly < 0 OrElse Lx >= P.Aw OrElse Ly >= P.Ah Then Return
        Dim X As Integer = P.Ax + Lx
        Dim Y As Integer = P.Ay + Ly
        SetPixel(X, Y, If(EraseIt, Colors.White, ActiveColor()))
        Render()
    End Sub

    Private Function ActiveColor() As Color
        Try
            If ColorPreview IsNot Nothing Then
                Dim B As SolidColorBrush = TryCast(ColorPreview.Background, SolidColorBrush)
                If B IsNot Nothing Then Return B.Color
            End If
        Catch ex As Exception
        End Try
        Return PaintColor
    End Function

    Private Sub SetPixel(X As Integer, Y As Integer, C As Color)
        Pixels(Y * SkinSize + X) = (&HFF << 24) Or (C.R << 16) Or (C.G << 8) Or C.B
    End Sub

    Private Sub PushUndo()
        UndoStack.Push(DirectCast(Pixels.Clone(), Integer()))
        RedoStack.Clear()
    End Sub

    Private Sub Undo()
        If UndoStack.Count = 0 Then Return
        RedoStack.Push(DirectCast(Pixels.Clone(), Integer()))
        CopyPixels(UndoStack.Pop())
    End Sub

    Private Sub Redo()
        If RedoStack.Count = 0 Then Return
        UndoStack.Push(DirectCast(Pixels.Clone(), Integer()))
        CopyPixels(RedoStack.Pop())
    End Sub

    Private Sub CopyPixels(Source As Integer())
        Array.Copy(Source, Pixels, Pixels.Length)
        Render()
    End Sub

    Private Sub Generate()
        Try
            Dim Folder As String = IO.Path.Combine(PathPure.Value, "Skins")
            Directory.CreateDirectory(Folder)
            Dim Path As String = IO.Path.Combine(Folder, "手绘皮肤_" & Date.Now.ToString("yyyyMMdd_HHmmss") & ".png")
            Dim Encoder As New PngBitmapEncoder
            Encoder.Frames.Add(BitmapFrame.Create(BuildSource()))
            Using Fs As New FileStream(Path, FileMode.Create)
                Encoder.Save(Fs)
            End Using
            Hint("已生成皮肤：" & IO.Path.GetFileName(Path) & "，可在皮肤库中应用", HintType.Green)
        Catch ex As Exception
            Hint("生成皮肤失败：" & ex.Message, HintType.Red)
        End Try
    End Sub
End Class
