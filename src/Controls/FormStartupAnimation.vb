Imports System.Windows.Media.Animation
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading

''' <summary>
''' 启动动画：展示应用图标，弹入后淡出。
''' </summary>
Public Class FormStartupAnimation
    Inherits Window

    Private Const WindowSize As Double = 300
    Private Const IconSize As Double = 160
    Private Const FadeInMs As Double = 260
    Private Const PopMs As Double = 340
    Private Const HoldMs As Double = 520

    Private ReadOnly RootGrid As New Grid
    Private ReadOnly IconView As New Image
    Private ReadOnly IconScale As New ScaleTransform(0.86, 0.86)
    Private ReadOnly IconShift As New TranslateTransform(0, 8)
    Private ReadOnly Timer As New DispatcherTimer
    Private ReadOnly Clock As Stopwatch

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

        RootGrid.Width = WindowSize
        RootGrid.Height = WindowSize
        Content = RootGrid

        IconView.Width = IconSize
        IconView.Height = IconSize
        IconView.Stretch = Stretch.Uniform
        IconView.HorizontalAlignment = HorizontalAlignment.Center
        IconView.VerticalAlignment = VerticalAlignment.Center
        IconView.RenderTransformOrigin = New Point(0.5, 0.5)

        Dim Group As New TransformGroup
        Group.Children.Add(IconScale)
        Group.Children.Add(IconShift)
        IconView.RenderTransform = Group

        Try
            IconView.Source = LoadIconSource()
        Catch ex As Exception
            Logger.Warn(ex, "加载启动动画图标失败")
        End Try

        RootGrid.Children.Add(IconView)

        Clock = Stopwatch.StartNew()
        AddHandler Loaded, AddressOf OnLoaded
        Timer.Interval = TimeSpan.FromMilliseconds(16)
        AddHandler Timer.Tick, Sub() OnFrame()
    End Sub

    ''' <summary>
    ''' 优先取 exe 内嵌的多尺寸 ICO 中最大的一帧，取不到时退回窗口图标。
    ''' </summary>
    Private Shared Function LoadIconSource() As ImageSource
        Try
            Dim Uri As New Uri("pack://application:,,,/Images/icon.ico", UriKind.Absolute)
            Dim Decoder As New IconBitmapDecoder(Uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad)
            Dim Best As BitmapFrame = Nothing
            For Each Frame In Decoder.Frames
                If Best Is Nothing OrElse Frame.PixelWidth > Best.PixelWidth Then Best = Frame
            Next
            If Best IsNot Nothing Then
                Best.Freeze()
                Return Best
            End If
        Catch ex As Exception
            Logger.Warn(ex, "从资源读取 ICO 失败，改用窗口图标")
        End Try
        Try
            Dim ExeIcon As System.Drawing.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Paths.Base & "PCL-ForK.exe")
            If ExeIcon IsNot Nothing Then
                Dim Source As BitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    ExeIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())
                Source.Freeze()
                Return Source
            End If
        Catch ex As Exception
            Logger.Warn(ex, "从 exe 提取图标失败")
        End Try
        Return Nothing
    End Function

    Private Sub OnLoaded(sender As Object, e As RoutedEventArgs)
        BeginAnimation(Window.OpacityProperty, New DoubleAnimation(0, 1, New Duration(TimeSpan.FromMilliseconds(FadeInMs))) With {
            .EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}})

        Dim PopX As New DoubleAnimation(0.86, 1, New Duration(TimeSpan.FromMilliseconds(PopMs))) With {
            .EasingFunction = New BackEase With {.Amplitude = 0.24, .EasingMode = EasingMode.EaseOut}}
        Dim PopY As New DoubleAnimation(0.86, 1, New Duration(TimeSpan.FromMilliseconds(PopMs))) With {
            .EasingFunction = New BackEase With {.Amplitude = 0.24, .EasingMode = EasingMode.EaseOut}}
        IconScale.BeginAnimation(ScaleTransform.ScaleXProperty, PopX)
        IconScale.BeginAnimation(ScaleTransform.ScaleYProperty, PopY)

        Dim Rise As New DoubleAnimation(8, 0, New Duration(TimeSpan.FromMilliseconds(PopMs))) With {
            .EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}}
        IconShift.BeginAnimation(TranslateTransform.YProperty, Rise)

        Clock.Restart()
        Timer.Start()
    End Sub

    Private Sub OnFrame()
        If Clock.Elapsed.TotalMilliseconds < FadeInMs + PopMs + HoldMs Then Return
        Timer.Stop()
        Dim Fade As New DoubleAnimation(1, 0, New Duration(TimeSpan.FromMilliseconds(240))) With {
            .EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseIn}}
        BeginAnimation(Window.OpacityProperty, Fade)
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
