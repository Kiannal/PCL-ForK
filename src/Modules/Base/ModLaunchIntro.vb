Imports System.Windows.Threading
Imports System.Windows.Interop

Public Module ModLaunchIntro

    Public Const DefaultVideoPath As String = "C:\Users\Public\Pictures\video_2026-11-05_14-36-59.mp4"
    Private IntroForm As FrmGameIntro = Nothing
    Private LauncherWasHidden As Boolean = False

    Public Sub ShowForWindow(Handle As IntPtr)
        Try
            If Handle = IntPtr.Zero Then Return
            Dim VideoPath As String = Settings.Get(Of String)("LaunchIntroVideo")
            Dim LocalVideo As String = PathPure.Value & "LaunchIntro.mp4"
            If Not FileUtils.Exists(LocalVideo) AndAlso FileUtils.Exists(DefaultVideoPath) Then
                Try
                    DirectoryUtils.Create(PathPure.Value)
                    FileUtils.Copy(DefaultVideoPath, LocalVideo)
                Catch ex As Exception
                    Logger.Warn(ex, "复制启动视频失败")
                End Try
            End If
            If String.IsNullOrWhiteSpace(VideoPath) Then
                VideoPath = If(FileUtils.Exists(LocalVideo), LocalVideo, DefaultVideoPath)
            End If
            If Not FileUtils.Exists(VideoPath) Then
                Logger.Warn($"启动视频不存在：{VideoPath}")
                Return
            End If
                If IntroForm IsNot Nothing Then IntroForm.Close()
            Try
                If FrmMain IsNot Nothing Then
                    LauncherWasHidden = FrmMain.Hidden
                    FrmMain.Hidden = True
                End If
            Catch ex As Exception
                Logger.Warn(ex, "隐藏启动器失败")
            End Try
            IntroForm = New FrmGameIntro(Handle, VideoPath)
            AddHandler IntroForm.Closed, Sub()
                                             IntroForm = Nothing
                                             RestoreLauncher()
                                         End Sub
            IntroForm.Show()
            Logger.Info($"已播放启动视频：{VideoPath}")
        Catch ex As Exception
            Logger.Warn(ex, "播放启动视频失败")
            RestoreLauncher()
        End Try
    End Sub

    Private Sub RestoreLauncher()
        Try
            If FrmMain IsNot Nothing AndAlso Not LauncherWasHidden Then FrmMain.Hidden = False
        Catch ex As Exception
            Logger.Warn(ex, "恢复启动器显示失败")
        End Try
    End Sub

End Module

Public Class FrmGameIntro
    Inherits Window

    Private Structure RECT
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    Private Declare Function GetWindowRect Lib "user32" (hWnd As IntPtr, ByRef lpRect As RECT) As Boolean
    Private Declare Function IsWindowVisible Lib "user32" (hWnd As IntPtr) As Boolean
    Private Declare Function IsIconic Lib "user32" (hWnd As IntPtr) As Boolean
    Private Declare Function SetWindowPos Lib "user32" (hWnd As IntPtr, hWndInsertAfter As IntPtr, x As Integer, y As Integer, cx As Integer, cy As Integer, uFlags As UInteger) As Boolean

    Private Const HWND_TOPMOST As Integer = -1
    Private Const SWP_NOSIZE As UInteger = &H1
    Private Const SWP_NOMOVE As UInteger = &H2

    Private ReadOnly TargetHandle As IntPtr
    Private ReadOnly Media As New MediaElement
    Private ReadOnly Timer As New DispatcherTimer
    Private ReadOnly VideoFilePath As String

    Public Sub New(Target As IntPtr, VideoPath As String)
        TargetHandle = Target
        VideoFilePath = VideoPath
        WindowStyle = WindowStyle.None
        AllowsTransparency = False
        Background = Brushes.Black
        ShowInTaskbar = False
        Topmost = True
        ShowActivated = True
        ResizeMode = ResizeMode.NoResize
        WindowStartupLocation = WindowStartupLocation.Manual
        Focusable = True
        Media.LoadedBehavior = MediaState.Manual
        Media.UnloadedBehavior = MediaState.Manual
        Media.Stretch = Stretch.UniformToFill
        Media.IsHitTestVisible = False
        Content = Media
        AddHandler Media.MediaEnded, Sub() Close()
        AddHandler Media.MediaFailed, Sub(sender, e)
                                              Dim Message As String = If(e.ErrorException IsNot Nothing, e.ErrorException.Message, "未知错误")
                                              Logger.Warn("启动视频播放失败：" & Message)
                                              Close()
                                          End Sub
        AddHandler Loaded, Sub()
                               ApplyBounds()
                               Try
                                   Media.Source = New Uri(VideoPath)
                                   Media.Play()
                               Catch ex As Exception
                                   Logger.Warn(ex, "启动视频播放失败")
                                   Close()
                               End Try
                           End Sub
        AddHandler MouseLeftButtonUp, Sub() Close()
        AddHandler KeyDown, Sub(sender, e)
                                If e.Key = Key.Escape Then Close()
                            End Sub
        Timer.Interval = TimeSpan.FromMilliseconds(150)
        AddHandler Timer.Tick, Sub() ApplyBounds()
        Timer.Start()
        AddHandler Closed, Sub()
                               Timer.Stop()
                               Try
                                   Media.Stop()
                                   Media.Close()
                               Catch ex As Exception
                               End Try
                           End Sub
    End Sub

    Private NotReadyCount As Integer = 0

    Private Sub ApplyBounds()
        Try
            If Not IsLoaded Then Return
            '游戏窗口可能刚创建、短暂不可见或最小化，先等一会，不要直接关闭
            If IsIconic(TargetHandle) Then
                If Visibility <> Visibility.Hidden Then Visibility = Visibility.Hidden
                NotReadyCount += 1
                If NotReadyCount > 100 Then Close()
                Return
            End If
            Dim Rect As RECT
            If Not GetWindowRect(TargetHandle, Rect) Then
                NotReadyCount += 1
                If NotReadyCount > 100 Then Close()
                Return
            End If
            Dim NewWidth As Integer = Rect.Right - Rect.Left
            Dim NewHeight As Integer = Rect.Bottom - Rect.Top
            If NewWidth <= 0 OrElse NewHeight <= 0 Then
                NotReadyCount += 1
                If NotReadyCount > 100 Then Close()
                Return
            End If
            NotReadyCount = 0
            If Visibility <> Visibility.Visible Then Visibility = Visibility.Visible
            Left = Rect.Left
            Top = Rect.Top
            Width = NewWidth
            Height = NewHeight
            Dim Handle As IntPtr = New WindowInteropHelper(Me).Handle
            If Handle <> IntPtr.Zero Then SetWindowPos(Handle, New IntPtr(HWND_TOPMOST), 0, 0, 0, 0, SWP_NOMOVE Or SWP_NOSIZE)
        Catch ex As Exception
            Logger.Warn(ex, "更新启动视频窗口位置失败")
        End Try
    End Sub
End Class
