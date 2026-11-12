Imports System.Diagnostics
Imports System.Windows.Media.Animation
Imports System.Windows.Threading

Public Class FormStartupAnimation
    Inherits Window

    Private Shared ReadOnly AccentColor As Color = Color.FromRgb(&H3B, &HE8, &HFF)
    Private Const WindowSize As Double = 300
    Private Const CircleRadius As Double = 92
    Private Const DrawDuration As Double = 620
    Private Const HoldDuration As Double = 120
    Private Const DissipateDuration As Double = 520

    Private ReadOnly RootCanvas As New Canvas
    Private ReadOnly RingPath As New Shapes.Path
    Private ReadOnly RingScale As New ScaleTransform(1, 1)
    Private ReadOnly Dots As New List(Of Shapes.Ellipse)
    Private ReadOnly Timer As New DispatcherTimer
    Private ReadOnly Clock As Stopwatch = Stopwatch.StartNew()
    Private ReadOnly CircleCenter As New Point(WindowSize / 2, WindowSize / 2)
    Private ReadOnly ArcSegment As New ArcSegment With {.SweepDirection = SweepDirection.Clockwise, .Size = New Size(CircleRadius, CircleRadius)}
    Private Dissipating As Boolean = False
    Private Finished As Boolean = False

    Public Sub New()
        WindowStyle = WindowStyle.None
        AllowsTransparency = True
        Background = Brushes.Transparent
        ResizeMode = ResizeMode.NoResize
        ShowInTaskbar = False
        ShowActivated = False
        Topmost = True
        WindowStartupLocation = WindowStartupLocation.CenterScreen
        Width = WindowSize
        Height = WindowSize
        Opacity = 0

        RootCanvas.Width = WindowSize
        RootCanvas.Height = WindowSize
        Content = RootCanvas

        '圆环
        Dim Figure As New PathFigure With {.StartPoint = New Point(CircleCenter.X + CircleRadius, CircleCenter.Y)}
        Figure.Segments.Add(ArcSegment)
        Dim Geometry As New PathGeometry
        Geometry.Figures.Add(Figure)
        RingPath.Data = Geometry
        RingPath.Stroke = New SolidColorBrush(AccentColor)
        RingPath.StrokeThickness = 3
        RingPath.StrokeStartLineCap = PenLineCap.Round
        RingPath.StrokeEndLineCap = PenLineCap.Round
        RingPath.StrokeLineJoin = PenLineJoin.Round
        RingPath.RenderTransformOrigin = New Point(0.5, 0.5)
        RingPath.RenderTransform = RingScale
        RingPath.Effect = New Effects.DropShadowEffect With {
            .Color = AccentColor, .BlurRadius = 20, .ShadowDepth = 0, .Opacity = 0.85}
        RootCanvas.Children.Add(RingPath)

        '消散粒子
        Dim DotBrush As New SolidColorBrush(AccentColor)
        For i = 0 To 15
            Dim Dot As New Shapes.Ellipse With {
                .Width = 5, .Height = 5, .Fill = DotBrush, .Opacity = 0,
                .RenderTransform = New TranslateTransform(0, 0)}
            Canvas.SetLeft(Dot, CircleCenter.X - 2.5)
            Canvas.SetTop(Dot, CircleCenter.Y - 2.5)
            RootCanvas.Children.Add(Dot)
            Dots.Add(Dot)
        Next

        AddHandler Loaded, AddressOf OnLoaded
        Timer.Interval = TimeSpan.FromMilliseconds(16)
        AddHandler Timer.Tick, Sub() OnFrame()
    End Sub

    Private Sub OnLoaded(sender As Object, e As RoutedEventArgs)
        BeginAnimation(Window.OpacityProperty, New DoubleAnimation(0, 1, New Duration(TimeSpan.FromMilliseconds(220))))
        Clock.Restart()
        Timer.Start()
    End Sub

    Private Sub OnFrame()
        Dim Elapsed As Double = Clock.Elapsed.TotalMilliseconds
        If Elapsed < DrawDuration Then
            '绘制圆环
            Dim Progress As Double = Elapsed / DrawDuration
            DrawArc(Progress * 360)
        ElseIf Elapsed < DrawDuration + HoldDuration Then
            DrawFullCircle()
        ElseIf Not Dissipating Then
            Dissipating = True
            StartDissipate()
        ElseIf Elapsed > DrawDuration + HoldDuration + DissipateDuration + 80 AndAlso Not Finished Then
            Finished = True
            Timer.Stop()
        End If
    End Sub

    Private Sub DrawArc(Angle As Double)
        If Angle >= 359.5 Then
            DrawFullCircle()
            Return
        End If
        Dim Rad As Double = Angle * Math.PI / 180
        ArcSegment.Point = New Point(CircleCenter.X + CircleRadius * Math.Cos(Rad), CircleCenter.Y + CircleRadius * Math.Sin(Rad))
        ArcSegment.IsLargeArc = Angle > 180
    End Sub

    Private Sub DrawFullCircle()
        If TypeOf RingPath.Data Is EllipseGeometry Then Return
        RingPath.Data = New EllipseGeometry(CircleCenter, CircleRadius, CircleRadius)
    End Sub

    Private Sub StartDissipate()
        '圆环扩散并淡出
        Dim FadeOut As New DoubleAnimation(1, 0, New Duration(TimeSpan.FromMilliseconds(DissipateDuration))) With {
            .EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}}
        RingPath.BeginAnimation(UIElement.OpacityProperty, FadeOut)
        Dim Expand As New DoubleAnimation(1, 1.6, New Duration(TimeSpan.FromMilliseconds(DissipateDuration))) With {
            .EasingFunction = New CubicEase With {.EasingMode = EasingMode.EaseOut}}
        RingScale.BeginAnimation(ScaleTransform.ScaleXProperty, Expand)
        RingScale.BeginAnimation(ScaleTransform.ScaleYProperty, Expand)

        '粒子向外扩散
        For i = 0 To Dots.Count - 1
            Dim Dot = Dots(i)
            Dim Angle As Double = 360.0 / Dots.Count * i
            Dim Rad As Double = Angle * Math.PI / 180
            Dim Distance As Double = CircleRadius * 1.5
            Dim TargetX As Double = Math.Cos(Rad) * Distance
            Dim TargetY As Double = Math.Sin(Rad) * Distance
            Canvas.SetLeft(Dot, CircleCenter.X + Math.Cos(Rad) * CircleRadius - Dot.Width / 2)
            Canvas.SetTop(Dot, CircleCenter.Y + Math.Sin(Rad) * CircleRadius - Dot.Height / 2)
            Dot.Opacity = 1
            Dim Move As New DoubleAnimation(0, 1, New Duration(TimeSpan.FromMilliseconds(DissipateDuration))) With {
                .EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}}
            Dim Transform = CType(Dot.RenderTransform, TranslateTransform)
            Dim TransformX As New DoubleAnimation(0, TargetX * 0.65, New Duration(TimeSpan.FromMilliseconds(DissipateDuration))) With {
                .EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}}
            Dim TransformY As New DoubleAnimation(0, TargetY * 0.65, New Duration(TimeSpan.FromMilliseconds(DissipateDuration))) With {
                .EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}}
            Transform.BeginAnimation(TranslateTransform.XProperty, TransformX)
            Transform.BeginAnimation(TranslateTransform.YProperty, TransformY)
            Dim DotFade As New DoubleAnimation(1, 0, New Duration(TimeSpan.FromMilliseconds(DissipateDuration))) With {
                .EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}}
            Dot.BeginAnimation(UIElement.OpacityProperty, DotFade)
        Next
    End Sub

    Public Overloads Sub Close(FadeDuration As TimeSpan)
        Try
            Timer.Stop()
            If Not IsLoaded Then
                MyBase.Close()
                Return
            End If
            Dim Duration As TimeSpan = If(FadeDuration <= TimeSpan.Zero, TimeSpan.FromMilliseconds(150), FadeDuration)
            Dim FadeOut As New DoubleAnimation(Opacity, 0, New Duration(Duration))
            AddHandler FadeOut.Completed,
                Sub()
                    Dispatcher.Invoke(
                    Sub()
                        MyBase.Close()
                    End Sub)
                End Sub
            BeginAnimation(Window.OpacityProperty, FadeOut)
        Catch ex As Exception
            MyBase.Close()
        End Try
    End Sub

End Class
