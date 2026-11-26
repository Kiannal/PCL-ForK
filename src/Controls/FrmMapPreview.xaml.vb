Imports System.Diagnostics
Imports System.Windows.Input

Public Class FrmMapPreview

    Private ReadOnly _url As String
    Private ReadOnly _key As String
    Private ReadOnly _name As String
    Private ReadOnly _external As Boolean
    Private ReadOnly _onDownload As Action
    Private IsClosing As Boolean = False

    Public Sub New(Title As String, CoverUrl As String, InfoText As String, DownloadUrl As String, TypeKey As String, IsExternal As Boolean, Optional ExtraAction As Action = Nothing)
        InitializeComponent()
        _url = DownloadUrl
        _key = TypeKey
        _name = Title
        _external = IsExternal
        _onDownload = ExtraAction
        CardRoot.Title = Title
        LabInfo.Text = InfoText
        If String.IsNullOrWhiteSpace(CoverUrl) Then
            LabEmpty.Visibility = Visibility.Visible
            CoverBorder.Visibility = Visibility.Collapsed
            ImgCover.Visibility = Visibility.Collapsed
        Else
            ImgCover.Source = CoverUrl
        End If
        If IsExternal Then BtnDownload.Text = "打开原站"
        AddHandler BtnDownload.Click, AddressOf DoDownloadClick
        AddHandler BtnClose.Click, AddressOf DoCloseClick
        AddHandler MouseLeftButtonDown, AddressOf DoDragMove
        AddHandler CoverBorder.SizeChanged, AddressOf DoCoverSizeChanged
        AddHandler Me.Loaded, AddressOf DoPlaceWindow
        AddHandler Me.Loaded, AddressOf DoEnterAnimation
    End Sub

    ''' <summary>
    ''' 入场动画：淡入 + 从下方轻微上浮。
    ''' </summary>
    Private Sub DoEnterAnimation(sender As Object, e As RoutedEventArgs)
        Try
            If PreviewRoot Is Nothing Then Return
            AniStart({
                AaOpacity(PreviewRoot, 1, 180, 0, New AniEaseOutFluent),
                AaTranslateY(PreviewRoot, -24, 240, 0, New AniEaseOutFluent(AniEasePower.Weak))
            }, "Preview Enter")
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' 关闭前淡出。
    ''' </summary>
    Private Sub DoCloseAnimated()
        Try
            If PreviewRoot Is Nothing Then
                Close()
                Return
            End If
            IsClosing = True
            AniStart({
                AaOpacity(PreviewRoot, -PreviewRoot.Opacity, 140, 0, New AniEaseInFluent),
                AaTranslateY(PreviewRoot, 12, 140, 0, New AniEaseInFluent)
            }, "Preview Close")
            AniStart(AaCode(AddressOf Close, 150), "Preview Close Delay")
        Catch
            Close()
        End Try
    End Sub

    Private Sub DoPlaceWindow(sender As Object, e As RoutedEventArgs)
        Try
            Dim OwnerWin As Window = Me.Owner
            If OwnerWin Is Nothing Then OwnerWin = Application.Current.MainWindow
            If OwnerWin Is Nothing Then
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2
                Top = (SystemParameters.PrimaryScreenHeight - Height) / 2
                Return
            End If
            '相对主窗口水平居中后向右微调
            Left = OwnerWin.Left + (OwnerWin.ActualWidth - Width) / 2 + 40
            Top = OwnerWin.Top + (OwnerWin.ActualHeight - Height) / 2
            '不越出屏幕
            Dim MaxL As Double = SystemParameters.PrimaryScreenWidth - Width - 8
            Dim MaxT As Double = SystemParameters.PrimaryScreenHeight - Height - 8
            If Left < 8 Then Left = 8
            If Top < 8 Then Top = 8
            If Left > MaxL Then Left = MaxL
            If Top > MaxT Then Top = MaxT
        Catch
        End Try
    End Sub

    Private Sub DoCoverSizeChanged(sender As Object, e As SizeChangedEventArgs)
        Try
            Dim W = CoverBorder.ActualWidth, H = CoverBorder.ActualHeight
            If W <= 0 OrElse H <= 0 Then Return
            CoverClip.Rect = New Rect(0, 0, W, H)
        Catch
        End Try
    End Sub

    Private Sub DoDragMove(sender As Object, e As MouseButtonEventArgs)
        If e.ButtonState = MouseButtonState.Pressed Then DragMove()
    End Sub

    Private Sub DoCloseClick(sender As Object, e As RoutedEventArgs)
        DoCloseAnimated()
    End Sub

    Private Sub DoDownloadClick(sender As Object, e As RoutedEventArgs)
        Try
            If _onDownload IsNot Nothing Then
                _onDownload.Invoke()
                DoCloseAnimated()
                Return
            End If
            Process.Start(New ProcessStartInfo(_url) With {.UseShellExecute = True})
            Hint(If(_external, "已在浏览器打开资源页面", "已在浏览器打开下载地址"), HintType.Blue)
            DoCloseAnimated()
        Catch ex As Exception
            Hint("打开失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

End Class
