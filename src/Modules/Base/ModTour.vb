Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Media.Animation
Imports System.Windows.Shapes
Imports System.Windows.Input
Imports System.Windows.Threading

Public Module ModTour

    Private Class TourStep
        Public Title As String
        Public ContentText As String
        Public Navigate As Action
        Public GetTarget As Func(Of FrameworkElement)
    End Class

    Private Overlay As Grid
    Private MaskPath As Path
    Private HoleStroke As Border
    Private Card As Border
    Private LabTitle As TextBlock
    Private LabContent As TextBlock
    Private LabStep As TextBlock
    Private BtnPrev As MyButton
    Private BtnNext As MyButton
    Private Steps As New List(Of TourStep)
    Private Index As Integer
    Private IsActive As Boolean
    Private MoveTimer As DispatcherTimer

    Public Sub Start()
        If IsActive Then Return
        BuildSteps()
        Index = 0
        BuildOverlay()
        IsActive = True
        AddHandler FrmMain.PreviewKeyDown, AddressOf OnKeyDown
        AddHandler FrmMain.PanBack.SizeChanged, AddressOf OnSizeChanged
        ShowStep()
    End Sub

    Public Sub Close()
        If Not IsActive Then Return
        IsActive = False
        RemoveHandler FrmMain.PreviewKeyDown, AddressOf OnKeyDown
        RemoveHandler FrmMain.PanBack.SizeChanged, AddressOf OnSizeChanged
        If MoveTimer IsNot Nothing Then MoveTimer.Stop()
        If Overlay IsNot Nothing AndAlso FrmMain.PanBack.Children.Contains(Overlay) Then FrmMain.PanBack.Children.Remove(Overlay)
        Overlay = Nothing
    End Sub

#Region "步骤"

    Private Sub BuildSteps()
        Steps.Clear()
        Steps.Add(New TourStep With {
            .Title = "欢迎使用 PCL-ForK",
            .ContentText = "这是一款基于 PCL2 深度定制的 Minecraft Java 版启动器，在原有功能上增强了界面、下载、联机与一批实用工具。" & vbCrLf & vbCrLf &
                           "接下来用几步快速带你了解主要功能：随时按 Esc 可以退出向导。",
            .Navigate = Nothing,
            .GetTarget = Nothing})

        Steps.Add(New TourStep With {
            .Title = "启动页",
            .ContentText = "左边是账号与启动区域：选择正版或离线账号，点「启动游戏」即可开始。" & vbCrLf & vbCrLf &
                           "下方三个按钮分别是 版本选择、版本设置 和 网易云音乐。",
            .Navigate = Sub() Navigate(FormMain.PageType.Launch),
            .GetTarget = Function() TryCast(IIf(FrmLaunchLeft IsNot Nothing, CObj(FrmLaunchLeft), FrmMain.PanMainLeft.Child), FrameworkElement)})

        Steps.Add(New TourStep With {
            .Title = "顶部导航",
            .ContentText = "所有功能都从顶部这五个标签进入：启动、下载、联机、设置、工具。" & vbCrLf & vbCrLf &
                           "「联机」可以跨启动器联机，「工具」里有更多进阶功能。",
            .Navigate = Nothing,
            .GetTarget = Function() FrmMain.PanTitleSelect})

        Steps.Add(New TourStep With {
            .Title = "下载页",
            .ContentText = "这里可以安装游戏本体（原版 / Forge / Fabric / NeoForge），也可以下载 Mod、整合包、资源包、光影和数据包。" & vbCrLf & vbCrLf &
                           "下载支持多线程加速、排序选择，以及自动下载所需前置 Mod。",
            .Navigate = Sub() Navigate(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall),
            .GetTarget = Function() FrmDownloadLeft})

        Steps.Add(New TourStep With {
            .Title = "联机页",
            .ContentText = "基于 EasyTier 组网：建房、用邀请码加入，支持自定义节点和节点测速；还可以使用 SakuraFrp 隧道。" & vbCrLf & vbCrLf &
                           "不需要公网 IP，也不需要所有人都用同一个启动器。",
            .Navigate = Sub() Navigate(FormMain.PageType.Link),
            .GetTarget = Function() FrmLinkMain})

        Steps.Add(New TourStep With {
            .Title = "设置页",
            .ContentText = "启动设置：Java、内存、启动前优化；个性化：液态玻璃、配色、背景；还有其他设置。" & vbCrLf & vbCrLf &
                           "下载加速、下载源、线程数等也都在这里调整。",
            .Navigate = Sub() Navigate(FormMain.PageType.Setup, FormMain.PageSubType.SetupLaunch),
            .GetTarget = Function() FrmSetupLeft})

        Steps.Add(New TourStep With {
            .Title = "工具页",
            .ContentText = "工具页里有：关于与鸣谢、百宝箱（帧率优化、清理、内存优化等）、皮肤库（在线皮肤一键应用）、投影查看（Litematica 3D 预览）、Mod 开发（一键生成 Forge / NeoForge / Fabric 项目）。",
            .Navigate = Sub() Navigate(FormMain.PageType.Other, FormMain.PageSubType.OtherAbout),
            .GetTarget = Function() FrmOtherLeft})

        Steps.Add(New TourStep With {
            .Title = "进阶工具",
            .ContentText = "值得试的还有：种子地图（真实生物群系与结构查询）、自动 Mod 汉化、世界设置、版本文件编辑器。" & vbCrLf & vbCrLf &
                           "这些入口分布在「版本设置」和「工具」里。",
            .Navigate = Sub() Navigate(FormMain.PageType.Other, FormMain.PageSubType.OtherLitematic),
            .GetTarget = Function() FrmOtherLeft})

        Steps.Add(New TourStep With {
            .Title = "向导结束",
            .ContentText = "这就是 PCL-ForK 的主要功能。之后可以随时在 工具 → 关于与鸣谢 中点击「开始应用向导」重新观看。" & vbCrLf & vbCrLf &
                           "按 Esc 退出向导，开始使用吧。",
            .Navigate = Sub() Navigate(FormMain.PageType.Launch),
            .GetTarget = Nothing})
    End Sub

    Private Sub Navigate(Page As FormMain.PageType, Optional SubType As FormMain.PageSubType = FormMain.PageSubType.Default)
        Try
            FrmMain.PageChange(New FormMain.PageStackData With {.Page = Page}, SubType)
        Catch ex As Exception
            Logger.Warn(ex, "向导切换页面失败")
        End Try
    End Sub

#End Region

#Region "界面"

    Private Function GetBrush(Name As String, Fallback As Color) As Brush
        Dim Result As Brush = Nothing
        Try
            Result = TryCast(System.Windows.Application.Current.TryFindResource(Name), Brush)
        Catch ex As Exception
        End Try
        Return If(Result, New SolidColorBrush(Fallback))
    End Function

    Private Sub BuildOverlay()
        Overlay = New Grid With {.Background = Brushes.Transparent, .ClipToBounds = True}

        ' 拦截点击的透明层
        Overlay.Children.Add(New Rectangle With {.Fill = Brushes.Transparent})

        ' 暗色遮罩（挖出目标区域）
        MaskPath = New Path With {.Fill = New SolidColorBrush(Color.FromArgb(170, 8, 12, 20)), .IsHitTestVisible = False}
        Overlay.Children.Add(MaskPath)

        ' 目标区域高亮边框
        HoleStroke = New Border With {
            .BorderBrush = GetBrush("ColorBrush3", Color.FromRgb(59, 130, 246)),
            .BorderThickness = New Thickness(2),
            .CornerRadius = New CornerRadius(8),
            .IsHitTestVisible = False,
            .HorizontalAlignment = HorizontalAlignment.Left,
            .VerticalAlignment = VerticalAlignment.Top,
            .Visibility = Visibility.Collapsed}
        Overlay.Children.Add(HoleStroke)

        ' 左上角 Esc 提示
        Dim EscChip As New Border With {
            .Background = GetBrush("ColorBrush3", Color.FromRgb(59, 130, 246)),
            .CornerRadius = New CornerRadius(6),
            .Padding = New Thickness(12, 7, 12, 7),
            .HorizontalAlignment = HorizontalAlignment.Left,
            .VerticalAlignment = VerticalAlignment.Top,
            .Margin = New Thickness(18, 16, 0, 0),
            .IsHitTestVisible = False}
        EscChip.Child = New TextBlock With {
            .Text = "按 ESC 退出向导",
            .FontSize = 13,
            .FontWeight = FontWeights.Bold,
            .Foreground = Brushes.White}
        Overlay.Children.Add(EscChip)

        ' 说明卡片
        Card = New Border With {
            .Width = 380,
            .Background = GetBrush("ColorBrushBg1", Color.FromRgb(255, 255, 255)),
            .BorderBrush = GetBrush("ColorBrushGray6", Color.FromRgb(221, 226, 232)),
            .BorderThickness = New Thickness(1),
            .CornerRadius = New CornerRadius(10),
            .Padding = New Thickness(18, 16, 18, 14),
            .HorizontalAlignment = HorizontalAlignment.Left,
            .VerticalAlignment = VerticalAlignment.Top}
        Dim Panel As New StackPanel
        LabStep = New TextBlock With {
            .FontSize = 12,
            .Foreground = GetBrush("ColorBrushGray3", Color.FromRgb(122, 130, 142)),
            .Margin = New Thickness(0, 0, 0, 6)}
        LabTitle = New TextBlock With {
            .FontSize = 19,
            .FontWeight = FontWeights.Bold,
            .Foreground = GetBrush("ColorBrush1", Color.FromRgb(31, 39, 51)),
            .Margin = New Thickness(0, 0, 0, 8)}
        LabContent = New TextBlock With {
            .FontSize = 13,
            .LineHeight = 21,
            .TextWrapping = TextWrapping.Wrap,
            .Foreground = GetBrush("ColorBrush1", Color.FromRgb(31, 39, 51))}
        Dim BtnBar As New StackPanel With {
            .Orientation = Orientation.Horizontal,
            .HorizontalAlignment = HorizontalAlignment.Right,
            .Margin = New Thickness(0, 16, 0, 0)}
        BtnPrev = New MyButton With {.Text = "上一步", .Height = 30, .Padding = New Thickness(14, 0, 14, 0), .Margin = New Thickness(0, 0, 8, 0)}
        BtnNext = New MyButton With {.Text = "下一步", .Height = 30, .Padding = New Thickness(18, 0, 18, 0), .ColorType = MyButton.ColorState.Highlight}
        Dim BtnClose As New MyButton With {.Text = "退出", .Height = 30, .Padding = New Thickness(14, 0, 14, 0), .Margin = New Thickness(8, 0, 0, 0)}
        AddHandler BtnPrev.Click, Sub(sender, e) MoveStep(-1)
        AddHandler BtnNext.Click, Sub(sender, e) MoveStep(1)
        AddHandler BtnClose.Click, Sub(sender, e) Close()
        BtnBar.Children.Add(BtnPrev)
        BtnBar.Children.Add(BtnNext)
        BtnBar.Children.Add(BtnClose)
        Panel.Children.Add(LabStep)
        Panel.Children.Add(LabTitle)
        Panel.Children.Add(LabContent)
        Panel.Children.Add(BtnBar)
        Card.Child = Panel
        Overlay.Children.Add(Card)

        FrmMain.PanBack.Children.Add(Overlay)
    End Sub

    Private Sub ShowStep()
        Dim CurrentStep As TourStep = Steps(Index)
        LabStep.Text = $"步骤 {Index + 1} / {Steps.Count}"
        LabTitle.Text = CurrentStep.Title
        LabContent.Text = CurrentStep.ContentText
        BtnPrev.IsEnabled = Index > 0
        BtnNext.Text = If(Index = Steps.Count - 1, "完成", "下一步")
        BtnNext.ColorType = MyButton.ColorState.Highlight

        If CurrentStep.Navigate IsNot Nothing Then CurrentStep.Navigate()

        If MoveTimer IsNot Nothing Then MoveTimer.Stop()
        MoveTimer = New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(450)}
        AddHandler MoveTimer.Tick,
            Sub(sender, e)
                MoveTimer.Stop()
                If Not IsActive Then Return
                PositionStep(Steps(Index).GetTarget)
            End Sub
        MoveTimer.Start()
        PositionStep(CurrentStep.GetTarget)
    End Sub

    Private Sub MoveStep(Delta As Integer)
        Dim NewIndex As Integer = Math.Min(Math.Max(Index + Delta, 0), Steps.Count)
        If NewIndex >= Steps.Count Then
            Close()
            Return
        End If
        Index = NewIndex
        ShowStep()
    End Sub

    Private Sub PositionStep(GetTarget As Func(Of FrameworkElement))
        If Overlay Is Nothing Then Return
        Dim Width As Double = FrmMain.PanBack.ActualWidth
        Dim Height As Double = FrmMain.PanBack.ActualHeight
        If Width <= 0 OrElse Height <= 0 Then Return

        Dim Target As FrameworkElement = Nothing
        If GetTarget IsNot Nothing Then
            Try
                Target = GetTarget()
            Catch ex As Exception
            End Try
        End If
        If Target IsNot Nothing AndAlso (Not Target.IsVisible OrElse Target.ActualWidth <= 0) Then Target = Nothing

        Dim Hole As Rect
        If Target Is Nothing Then
            Hole = New Rect(Width / 2 - 200, Height / 2 - 120, 400, 240)
            HoleStroke.Visibility = Visibility.Collapsed
        Else
            Try
                Dim Bounds = Target.TransformToAncestor(FrmMain.PanBack).TransformBounds(New Rect(0, 0, Target.ActualWidth, Target.ActualHeight))
                Hole = New Rect(Math.Max(0, Bounds.X - 6), Math.Max(0, Bounds.Y - 6), Math.Min(Width, Bounds.Width + 12), Math.Min(Height, Bounds.Height + 12))
            Catch ex As Exception
                Hole = New Rect(Width / 2 - 200, Height / 2 - 120, 400, 240)
            End Try
            HoleStroke.Visibility = Visibility.Visible
            HoleStroke.Margin = New Thickness(Hole.X, Hole.Y, 0, 0)
            HoleStroke.Width = Hole.Width
            HoleStroke.Height = Hole.Height
        End If

        Dim Geometry As New CombinedGeometry(GeometryCombineMode.Exclude,
                                             New RectangleGeometry(New Rect(0, 0, Width, Height)),
                                             New RectangleGeometry(Hole, 10, 10))
        MaskPath.Data = Geometry

        Card.Measure(New Size(380, Double.PositiveInfinity))
        Dim CardHeight As Double = Math.Max(140, Card.DesiredSize.Height)
        Dim CardX As Double
        If Hole.X + Hole.Width / 2 > Width * 0.55 Then
            CardX = Math.Max(20, Hole.X - 380 - 28)
        Else
            CardX = Math.Min(Width - 380 - 20, Hole.X + Hole.Width + 28)
        End If
        If CardX < 20 Then CardX = 20
        Dim CardY As Double = Hole.Y + Hole.Height / 2 - CardHeight / 2
        CardY = Math.Min(Math.Max(84, CardY), Height - CardHeight - 16)
        If CardY < 84 Then CardY = 84
        Card.Margin = New Thickness(CardX, CardY, 0, 0)
    End Sub

    Private Sub OnSizeChanged(sender As Object, e As SizeChangedEventArgs)
        If IsActive Then PositionStep(Steps(Index).GetTarget)
    End Sub

    Private Sub OnKeyDown(sender As Object, e As KeyEventArgs)
        If Not IsActive Then Return
        Select Case e.Key
            Case Key.Escape
                e.Handled = True
                Close()
            Case Key.Right, Key.Down, Key.Enter
                e.Handled = True
                MoveStep(1)
            Case Key.Left, Key.Up
                e.Handled = True
                MoveStep(-1)
        End Select
    End Sub

#End Region

End Module
