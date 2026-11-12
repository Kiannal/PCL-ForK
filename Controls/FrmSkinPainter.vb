Imports System.IO
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Media.Imaging

Public Class FrmSkinPainter
    Inherits Window

    Private Const SkinSize As Integer = 64
    Private ReadOnly Bitmap As New WriteableBitmap(SkinSize, SkinSize, 96, 96, PixelFormats.Bgra32, Nothing)
    Private ReadOnly Pixels(SkinSize * SkinSize - 1) As Integer
    Private PaintColor As Color = Colors.White
    Private IsDrawing As Boolean
    Private ReadOnly UndoStack As New Stack(Of Integer())
    Private ReadOnly RedoStack As New Stack(Of Integer())
    Private ReadOnly ImgCanvas As New Image With {.Width = 512, .Height = 512, .Stretch = Stretch.Fill, .SnapsToDevicePixels = True}
    Private ReadOnly ImgPreview As New Image With {.Width = 128, .Height = 128, .Stretch = Stretch.Uniform}

    Public Sub New()
        Title = "皮肤绘制"
        Width = 900
        Height = 700
        WindowStartupLocation = WindowStartupLocation.CenterOwner
        Background = New SolidColorBrush(Windows.Media.Color.FromRgb(28, 28, 33))
        RenderOptions.SetBitmapScalingMode(ImgCanvas, BitmapScalingMode.NearestNeighbor)
        RenderOptions.SetBitmapScalingMode(ImgPreview, BitmapScalingMode.NearestNeighbor)
        ImgCanvas.Source = Bitmap
        ImgPreview.Source = Bitmap
        Content = BuildUi()
        ClearCanvas(False)
    End Sub

    Private Function BuildUi() As UIElement
        Dim Root As New Grid
        Root.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(220)})
        Root.ColumnDefinitions.Add(New ColumnDefinition)
        Dim Left As New StackPanel With {.Margin = New Thickness(16)}
        Dim TitleText As New TextBlock With {.Text = "调色板", .Foreground = Brushes.White, .FontSize = 15, .Margin = New Thickness(0, 0, 0, 8)}
        Left.Children.Add(TitleText)
        Dim Palette As New WrapPanel With {.Width = 188}
        Dim ColorsList As Color() = {
            Colors.White, Windows.Media.Color.FromRgb(170, 170, 170), Windows.Media.Color.FromRgb(85, 85, 85), Colors.Black,
            Windows.Media.Color.FromRgb(255, 85, 85), Windows.Media.Color.FromRgb(170, 0, 0), Windows.Media.Color.FromRgb(255, 170, 85), Windows.Media.Color.FromRgb(255, 255, 85),
            Windows.Media.Color.FromRgb(85, 255, 85), Windows.Media.Color.FromRgb(0, 170, 0), Windows.Media.Color.FromRgb(85, 255, 255), Windows.Media.Color.FromRgb(0, 170, 170),
            Windows.Media.Color.FromRgb(85, 85, 255), Windows.Media.Color.FromRgb(0, 0, 170), Windows.Media.Color.FromRgb(255, 85, 255), Windows.Media.Color.FromRgb(170, 0, 170),
            Windows.Media.Color.FromRgb(255, 170, 170), Windows.Media.Color.FromRgb(120, 70, 40), Windows.Media.Color.FromRgb(255, 220, 180), Windows.Media.Color.FromRgb(200, 160, 110),
            Windows.Media.Color.FromRgb(90, 60, 30), Windows.Media.Color.FromRgb(40, 90, 40), Windows.Media.Color.FromRgb(150, 200, 90), Windows.Media.Color.FromRgb(230, 230, 190)}
        For Each C As Color In ColorsList
            Dim Btn As New Border With {.Width = 22, .Height = 22, .Margin = New Thickness(0, 0, 2, 2), .Background = New SolidColorBrush(C), .Cursor = Cursors.Hand, .BorderThickness = New Thickness(1), .BorderBrush = New SolidColorBrush(Windows.Media.Color.FromRgb(60, 60, 66))}
            Dim Captured As Color = C
            AddHandler Btn.MouseLeftButtonDown, Sub() PaintColor = Captured
            Palette.Children.Add(Btn)
        Next
        Left.Children.Add(Palette)
        Dim HexBox As New TextBox With {.Text = "#FFFFFF", .Margin = New Thickness(0, 8, 0, 8), .Height = 26, .FontSize = 12}
        AddHandler HexBox.TextChanged,
            Sub()
                Try
                    Dim T As String = HexBox.Text.Trim().TrimStart("#"c)
                    If T.Length = 6 Then
                        PaintColor = Windows.Media.Color.FromRgb(Convert.ToByte(T.Substring(0, 2), 16), Convert.ToByte(T.Substring(2, 2), 16), Convert.ToByte(T.Substring(4, 2), 16))
                    End If
                Catch
                End Try
            End Sub
        Left.Children.Add(HexBox)
        Dim ToolRow As New WrapPanel
        ToolRow.Children.Add(MakeButton("撤销", Sub() Undo()))
        ToolRow.Children.Add(MakeButton("重做", Sub() Redo()))
        ToolRow.Children.Add(MakeButton("清空", Sub() ClearCanvas(True)))
        Left.Children.Add(ToolRow)
        Dim FileRow As New WrapPanel With {.Margin = New Thickness(0, 12, 0, 0)}
        FileRow.Children.Add(MakeButton("载入当前皮肤", Sub() LoadCurrentSkin()))
        FileRow.Children.Add(MakeButton("保存到皮肤库", Sub() SaveSkin()))
        Left.Children.Add(FileRow)
        Dim TipText As New TextBlock With {.Text = "左键绘制，右键擦除。" & vbCrLf & "画布为 64×64 标准皮肤。", .Foreground = New SolidColorBrush(Windows.Media.Color.FromRgb(170, 172, 180)), .FontSize = 12, .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 16, 0, 0)}
        Left.Children.Add(TipText)
        Dim PreviewTitle As New TextBlock With {.Text = "预览", .Foreground = Brushes.White, .FontSize = 13, .Margin = New Thickness(0, 16, 0, 6)}
        Left.Children.Add(PreviewTitle)
        Left.Children.Add(ImgPreview)
        Grid.SetColumn(Left, 0)
        Root.Children.Add(Left)

        Dim CanvasHost As New Border With {.Margin = New Thickness(0, 16, 16, 16), .CornerRadius = New CornerRadius(8), .Background = New SolidColorBrush(Windows.Media.Color.FromRgb(45, 45, 52)), .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center}
        CanvasHost.Child = ImgCanvas
        AddHandler ImgCanvas.MouseLeftButtonDown, Sub(S, E) StartDraw(E, PaintColor)
        AddHandler ImgCanvas.MouseRightButtonDown, Sub(S, E) StartDraw(E, Colors.Transparent)
        AddHandler ImgCanvas.MouseMove, Sub(S, E) ContinueDraw(E)
        AddHandler ImgCanvas.MouseLeftButtonUp, Sub() IsDrawing = False
        AddHandler ImgCanvas.MouseRightButtonUp, Sub() IsDrawing = False
        Grid.SetColumn(CanvasHost, 1)
        Root.Children.Add(CanvasHost)
        Return Root
    End Function

    Private Function MakeButton(Text As String, Action As Action) As Button
        Dim Btn As New Button With {.Content = Text, .Height = 28, .Margin = New Thickness(0, 0, 6, 6), .Padding = New Thickness(10, 0, 10, 0), .Background = New SolidColorBrush(Windows.Media.Color.FromRgb(50, 50, 58)), .Foreground = Brushes.White, .BorderBrush = New SolidColorBrush(Windows.Media.Color.FromRgb(80, 80, 90))}
        AddHandler Btn.Click, Sub() Action()
        Return Btn
    End Function

    Private Sub StartDraw(E As MouseButtonEventArgs, C As Color)
        PushUndo()
        IsDrawing = True
        PaintPixel(E, C)
        ImgCanvas.CaptureMouse()
    End Sub

    Private Sub ContinueDraw(E As MouseEventArgs)
        If Not IsDrawing Then Return
        PaintPixel(E, If(E.RightButton = MouseButtonState.Pressed, Colors.Transparent, PaintColor))
    End Sub

    Private Sub PaintPixel(E As MouseEventArgs, C As Color)
        Dim P As Point = E.GetPosition(ImgCanvas)
        If ImgCanvas.ActualWidth <= 0 OrElse ImgCanvas.ActualHeight <= 0 Then Return
        Dim X As Integer = CInt(P.X / ImgCanvas.ActualWidth * SkinSize)
        Dim Y As Integer = CInt(P.Y / ImgCanvas.ActualHeight * SkinSize)
        If X < 0 OrElse Y < 0 OrElse X >= SkinSize OrElse Y >= SkinSize Then Return
        SetPixel(X, Y, C)
        Render()
    End Sub

    Private Sub SetPixel(X As Integer, Y As Integer, C As Color)
        Dim A As Byte = If(C = Colors.Transparent, CByte(0), C.A)
        Dim V As Integer = (A << 24) Or (C.R << 16) Or (C.G << 8) Or C.B
        Pixels(Y * SkinSize + X) = V
    End Sub

    Private Sub Render()
        Bitmap.WritePixels(New Int32Rect(0, 0, SkinSize, SkinSize), Pixels, SkinSize * 4, 0)
    End Sub

    Private Sub ClearCanvas(Record As Boolean)
        If Record Then PushUndo()
        Array.Clear(Pixels, 0, Pixels.Length)
        Render()
    End Sub

    Private Sub PushUndo()
        UndoStack.Push(DirectCast(Pixels.Clone(), Integer()))
        RedoStack.Clear()
    End Sub

    Private Sub Undo()
        If UndoStack.Count = 0 Then Return
        RedoStack.Push(DirectCast(Pixels.Clone(), Integer()))
        Array.Copy(UndoStack.Pop(), Pixels, Pixels.Length)
        Render()
    End Sub

    Private Sub Redo()
        If RedoStack.Count = 0 Then Return
        UndoStack.Push(DirectCast(Pixels.Clone(), Integer()))
        Array.Copy(RedoStack.Pop(), Pixels, Pixels.Length)
        Render()
    End Sub

    Private Sub LoadCurrentSkin()
        Try
            Dim Path As String = IO.Path.Combine(PathPure.Value, "CustomSkin.png")
            If Not File.Exists(Path) Then
                Hint("没有找到当前离线皮肤", HintType.Red)
                Return
            End If
            Dim Decoder As New PngBitmapDecoder(New Uri(Path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad)
            Dim Frame As BitmapSource = Decoder.Frames(0)
            Dim Converted As New FormatConvertedBitmap(Frame, PixelFormats.Bgra32, Nothing, 0)
            Dim Buffer(SkinSize * SkinSize * 4 - 1) As Byte
            Dim Scale As Integer = SkinSize
            Dim Visual As New DrawingVisual
            Using Dc = Visual.RenderOpen()
                Dc.DrawImage(Frame, New Rect(0, 0, Scale, Scale))
            End Using
            Dim Rtb As New RenderTargetBitmap(Scale, Scale, 96, 96, PixelFormats.Pbgra32)
            Rtb.Render(Visual)
            Rtb.CopyPixels(Buffer, Scale * 4, 0)
            PushUndo()
            For I = 0 To SkinSize * SkinSize - 1
                Dim B As Byte = Buffer(I * 4)
                Dim G As Byte = Buffer(I * 4 + 1)
                Dim R As Byte = Buffer(I * 4 + 2)
                Dim A As Byte = Buffer(I * 4 + 3)
                Pixels(I) = (A << 24) Or (R << 16) Or (G << 8) Or B
            Next
            Render()
        Catch ex As Exception
            Hint("载入皮肤失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub SaveSkin()
        Try
            Dim Folder As String = IO.Path.Combine(PathPure.Value, "Skins")
            Directory.CreateDirectory(Folder)
            Dim Path As String = IO.Path.Combine(Folder, "手绘_" & Date.Now.ToString("yyyyMMdd_HHmmss") & ".png")
            Dim Encoder As New PngBitmapEncoder
            Encoder.Frames.Add(BitmapFrame.Create(Bitmap))
            Using Fs As New FileStream(Path, FileMode.Create)
                Encoder.Save(Fs)
            End Using
            Hint("已保存到皮肤库：" & IO.Path.GetFileName(Path), HintType.Green)
        Catch ex As Exception
            Hint("保存失败：" & ex.Message, HintType.Red)
        End Try
    End Sub
End Class
