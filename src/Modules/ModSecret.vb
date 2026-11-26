'由于包含加解密等安全信息，本文件中的部分代码已被删除

Friend Module ModSecret

    '标注 PCL 的不同分支，仅用于替换标记
    Public Const VersionBranchMain As String = "OpenSource"
    '在开源版的注册表与常规版的注册表隔离，以防数据冲突
    Public Const RegFolder As String = "PCLDebug"
    '用于微软登录的 ClientId
    Public OAuthClientId As String = If(Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID"), "4a07b708-b86d-4365-a55f-f4f23ecb85ab")
    'CurseForge API Key
    Public CurseForgeAPIKey As String = If(Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY"), "")
    '用于匿名数据收集的腾讯云日志服务上报 URL，形如 https://{region}.cls.tencentcs.com/track?topic_id={topic_id}
    Public Const ClsBaseUrl As String = ""

#Region "网络鉴权"

    Public Const RestrictMcDownload As Boolean = False
    Friend Function SecretCdnSign(UrlWithMark As String) As String
        If Not UrlWithMark.EndsWithF("{CDN}") Then Return UrlWithMark
        Dim Text As String = UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20")
        Dim RandomPart As String = RandomInteger(0, 2147483645).ToString("x").EnsureLength("0"c, 8)
        Dim TimeStamp As String = GetUnixTimestampUtc().ToString()
        Dim PathPart As String = Text.Substring(Text.IndexOfF("://") + 3)
        PathPart = PathPart.Substring(PathPart.IndexOfF("/"))
        Dim Sign As String = CryptographyUtils.ComputeHash({PathPart, TimeStamp, RandomPart, "0", "PCL2Server"}.Join("-"), CryptographyUtils.HashMethod.Md5)
        Return Text & If(Text.Contains("?"), "&", "?") & "sign=" & {TimeStamp, RandomPart, "0", Sign}.Join("-")
    End Function
    ''' <summary>
    ''' 设置 Headers 的 UA、Referer。
    ''' </summary>
    Friend Sub SecretHeadersSign(Url As String, ByRef Req As HttpRequestMessage, Optional SimulateBrowserHeaders As Boolean = False)
        If Not Req.Headers.UserAgent.Any Then
            If Url.Contains("baidupcs.com") OrElse Url.Contains("baidu.com") Then
                Req.Headers.Add("User-Agent", "LogStatistic")  '#4951
            ElseIf SimulateBrowserHeaders Then
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)} Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36")
            Else
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)}")
            End If
        End If
        If Not SimulateBrowserHeaders Then Req.Headers.Add("Referer", $"http://{VersionCode}.pcl2.server/")
        If Url.Contains("api.curseforge.com") OrElse Url.Contains("forgecdn.net") Then Req.Headers.Add("x-api-key", CurseForgeAPIKey)
    End Sub

#End Region

#Region "主题"

    Public Color1 As New MyColor(52, 61, 74)
    Public Color2 As New MyColor(11, 91, 203)
    Public Color3 As New MyColor(19, 112, 243)
    Public Color4 As New MyColor(72, 144, 245)
    Public Color5 As New MyColor(150, 192, 249)
    Public Color6 As New MyColor(213, 230, 253)
    Public Color7 As New MyColor(222, 236, 253)
    Public Color8 As New MyColor(234, 242, 254)
    Public ColorBg0 As New MyColor(150, 192, 249)
    Public ColorBg1 As New MyColor(190, Color7)
    Public ColorGray1 As New MyColor(64, 64, 64)
    Public ColorGray2 As New MyColor(115, 115, 115)
    Public ColorGray3 As New MyColor(140, 140, 140)
    Public ColorGray4 As New MyColor(166, 166, 166)
    Public ColorGray5 As New MyColor(204, 204, 204)
    Public ColorGray6 As New MyColor(235, 235, 235)
    Public ColorGray7 As New MyColor(240, 240, 240)
    Public ColorGray8 As New MyColor(245, 245, 245)
    Public ColorSemiTransparent As New MyColor(1, Color8)

    Public ThemeNow As Integer = -1
    Public ColorHue As Integer = 210, ColorSat As Integer = 85, ColorLightAdjust As Integer = 0, ColorHueTopbarDelta As OneOf(Of Integer, Integer()) = 0
    Public ThemeDontClick As Integer = 0

    Public Sub ApplyDarkThemeResources()
        If Not Settings.Get(Of Boolean)("UiDarkMode") Then Return
        Application.Current.Resources("ColorBrushCard") = New SolidColorBrush(Color.FromArgb(245, 29, 29, 34))
        Application.Current.Resources("ColorBrushGlassBorder") = New SolidColorBrush(Color.FromRgb(42, 44, 49))
        Application.Current.Resources("ColorBrushBackgroundTransparentSidebar") = New SolidColorBrush(Color.FromRgb(22, 24, 28))
        Application.Current.Resources("ColorBrushSemiWhite") = New SolidColorBrush(Color.FromArgb(187, 29, 31, 35))
        Application.Current.Resources("ColorBrushHalfWhite") = New SolidColorBrush(Color.FromRgb(39, 41, 46))
        Application.Current.Resources("ColorBrushGray1") = New SolidColorBrush(New MyColor(237, 238, 242))
        Application.Current.Resources("ColorBrushGray2") = New SolidColorBrush(New MyColor(208, 209, 214))
        Application.Current.Resources("ColorBrushGray3") = New SolidColorBrush(New MyColor(170, 172, 180))
        Application.Current.Resources("ColorBrushGray4") = New SolidColorBrush(New MyColor(148, 150, 160))
        Application.Current.Resources("ColorBrushGray5") = New SolidColorBrush(New MyColor(110, 112, 122))
        Application.Current.Resources("ColorBrushGray6") = New SolidColorBrush(New MyColor(58, 58, 65))
        Application.Current.Resources("ColorBrushGray7") = New SolidColorBrush(New MyColor(30, 30, 35))
        Application.Current.Resources("ColorBrushGray8") = New SolidColorBrush(New MyColor(32, 32, 36))
        Application.Current.Resources("ColorBrushWhite") = New SolidColorBrush(Color.FromRgb(39, 41, 46))
    End Sub

    Public Sub DarkModeChanged(Value As Object)
        ThemeRefresh()
        RunInUi(Sub()
                    Try
                        If FrmMain IsNot Nothing AndAlso FrmMain.PanDarkOverlay IsNot Nothing Then
                            FrmMain.PanDarkOverlay.Visibility = If(Settings.Get(Of Boolean)("UiDarkMode"), Visibility.Visible, Visibility.Collapsed)
                        End If
                    Catch ex As Exception
                    End Try
                End Sub)
    End Sub

    Public Sub ThemeRefresh(Optional NewTheme As Integer = -1)
        Try
            If NewTheme > 0 AndAlso Not ThemeCheckOne(NewTheme) Then NewTheme = 0
            If ThemeNow = NewTheme AndAlso NewTheme >= 0 Then Return
            If NewTheme >= 0 Then
                ThemeNow = NewTheme
                ApplyThemePreset(ThemeNow)
            End If
            If ThemeNow = 14 Then
                ColorHue = Settings.Get(Of Integer)("UiLauncherHue")
                ColorSat = Settings.Get(Of Integer)("UiLauncherSat")
                ColorLightAdjust = Settings.Get(Of Integer)("UiLauncherLight") - 20
                ColorHueTopbarDelta = Settings.Get(Of Integer)("UiLauncherDelta") - 90
            ElseIf ThemeNow = 12 Then
                ColorHue = (ColorHue + 2) Mod 360
            ElseIf ThemeNow = 13 Then
                ColorHue = (ColorHue + 1) Mod 360
            ElseIf ThemeDontClick = 2 Then
                ColorHue = (ColorHue + 3) Mod 360
            End If

            Color1 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.2, 25 + ColorLightAdjust * 0.3)
            Color2 = New MyColor().FromHSL2(ColorHue, ColorSat, 45 + ColorLightAdjust)
            Color3 = New MyColor().FromHSL2(ColorHue, ColorSat, 55 + ColorLightAdjust)
            Color4 = New MyColor().FromHSL2(ColorHue, ColorSat, 65 + ColorLightAdjust)
            Color5 = New MyColor().FromHSL2(ColorHue, ColorSat, 80 + ColorLightAdjust * 0.4)
            Color6 = New MyColor().FromHSL2(ColorHue, ColorSat, 91 + ColorLightAdjust * 0.1)
            Color7 = New MyColor().FromHSL2(ColorHue, ColorSat, 95)
            Color8 = New MyColor().FromHSL2(ColorHue, ColorSat, 97)
            ColorBg0 = Color4 * 0.4 + Color5 * 0.4 + ColorGray4 * 0.2
            ColorBg1 = New MyColor(190, Color7)

            If Settings.Get(Of Boolean)("UiDarkMode") Then
                Color1 = New MyColor(237, 238, 242)
                Color2 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 58)
                Color3 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 52)
                Color4 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.7, 45)
                Color5 = New MyColor(58, 58, 65)
                Color6 = New MyColor(44, 44, 50)
                Color7 = New MyColor(33, 33, 38)
                Color8 = New MyColor(24, 24, 28)
                ColorBg0 = New MyColor(214, 220, 230)
                ColorBg1 = New MyColor(238, 22, 22, 26)
                ColorSemiTransparent = New MyColor(1, Color8)
            End If
            ColorSemiTransparent = New MyColor(1, Color8)
            Application.Current.Resources("ColorBrush1") = New SolidColorBrush(Color1)
            Application.Current.Resources("ColorBrush2") = New SolidColorBrush(Color2)
            Application.Current.Resources("ColorBrush3") = New SolidColorBrush(Color3)
            Application.Current.Resources("ColorBrush4") = New SolidColorBrush(Color4)
            Application.Current.Resources("ColorBrush5") = New SolidColorBrush(Color5)
            Application.Current.Resources("ColorBrush6") = New SolidColorBrush(Color6)
            Application.Current.Resources("ColorBrush7") = New SolidColorBrush(Color7)
            Application.Current.Resources("ColorBrush8") = New SolidColorBrush(Color8)
            Application.Current.Resources("ColorBrushBg0") = New SolidColorBrush(ColorBg0)
            Application.Current.Resources("ColorBrushBg1") = New SolidColorBrush(ColorBg1)
            Application.Current.Resources("ColorObject1") = CType(Color1, Color)
            Application.Current.Resources("ColorObject2") = CType(Color2, Color)
            Application.Current.Resources("ColorObject3") = CType(Color3, Color)
            Application.Current.Resources("ColorObject4") = CType(Color4, Color)
            Application.Current.Resources("ColorObject5") = CType(Color5, Color)
            Application.Current.Resources("ColorObject6") = CType(Color6, Color)
            Application.Current.Resources("ColorObject7") = CType(Color7, Color)
            Application.Current.Resources("ColorObject8") = CType(Color8, Color)
            Application.Current.Resources("ColorObjectBg0") = CType(ColorBg0, Color)
            Application.Current.Resources("ColorObjectBg1") = CType(ColorBg1, Color)
            ThemeRefreshMain()
            ApplyDarkThemeResources()
        Catch ex As Exception
            Logger.Error(ex, "刷新主题颜色失败", LogBehavior.Toast)
        End Try
    End Sub
    Private Sub ApplyThemePreset(Id As Integer)
        Select Case Id
            Case 0
                ColorHue = 210 : ColorSat = 85 : ColorLightAdjust = 0
            Case 1
                ColorHue = 172 : ColorSat = 75 : ColorLightAdjust = 0
            Case 2
                ColorHue = 125 : ColorSat = 62 : ColorLightAdjust = 0
            Case 3
                ColorHue = 45 : ColorSat = 85 : ColorLightAdjust = 0
            Case 4
                ColorHue = 30 : ColorSat = 48 : ColorLightAdjust = -6
            Case 5
                ColorHue = 210 : ColorSat = 0 : ColorLightAdjust = -8
            Case 6
                ColorHue = 335 : ColorSat = 72 : ColorLightAdjust = 0
            Case 7
                ColorHue = 272 : ColorSat = 62 : ColorLightAdjust = 0
            Case 8
                ColorHue = 42 : ColorSat = 85 : ColorLightAdjust = 0
            Case 9
                ColorHue = 24 : ColorSat = 90 : ColorLightAdjust = 0
            Case 10
                ColorHue = 0 : ColorSat = 82 : ColorLightAdjust = 0
            Case 11
                ColorHue = 200 : ColorSat = 90 : ColorLightAdjust = 0
            Case 12
                ColorHue = RandomInteger(0, 359) : ColorSat = 100 : ColorLightAdjust = 0
            Case 13
                ColorHue = RandomInteger(0, 359) : ColorSat = 90 : ColorLightAdjust = 0
        End Select
        ColorHueTopbarDelta = 0
    End Sub


    Public Sub ThemeRefreshMain()
        RunInUi(
        Sub()
            If Not FrmMain.IsLoaded Then Return
            If Settings.Get(Of Boolean)("UiDarkMode") Then
                Dim DarkBrush = New LinearGradientBrush With {.EndPoint = New Point(1, 0), .StartPoint = New Point(0, 0)}
                DarkBrush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.35, 26)})
                DarkBrush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.35, 26)})
                DarkBrush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.35, 26)})
                FrmMain.PanTitle.Background = DarkBrush
                FrmMain.PanTitle.Background.Freeze()
                FrmMain.ImgBack.Visibility = Visibility.Collapsed
                FrmMain.MedBack.Visibility = Visibility.Collapsed
                If Settings.Get(Of Boolean)("UiBackgroundColorful") Then
                    Dim DarkGradient = New LinearGradientBrush With {.EndPoint = New Point(0.1, 1), .StartPoint = New Point(0.9, 0)}
                    DarkGradient.GradientStops.Add(New GradientStop With {.Offset = -0.1, .Color = New MyColor().FromHSL2(ColorHue - 15, ColorSat * 0.5, 17)})
                    DarkGradient.GradientStops.Add(New GradientStop With {.Offset = 0.4, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.5, 22)})
                    DarkGradient.GradientStops.Add(New GradientStop With {.Offset = 1.1, .Color = New MyColor().FromHSL2(ColorHue - 15, ColorSat * 0.5, 17)})
                    FrmMain.PanForm.Background = DarkGradient
                Else
                    FrmMain.PanForm.Background = New MyColor(20, 20, 24)
                End If
                FrmMain.PanForm.Background.Freeze()
                If FrmMain.PanDarkOverlay IsNot Nothing Then
                    If Settings.Get(Of Boolean)("UiBackgroundColorful") Then
                        FrmMain.PanDarkOverlay.Background = New SolidColorBrush(Color.FromArgb(190, 16, 16, 20))
                    Else
                        FrmMain.PanDarkOverlay.Background = New SolidColorBrush(Color.FromArgb(240, 16, 16, 20))
                    End If
                    FrmMain.PanDarkOverlay.Background.Freeze()
                End If
                Return
            End If
            Dim Brush = New LinearGradientBrush With {.EndPoint = New Point(1, 0), .StartPoint = New Point(0, 0)}
            Dim Deltas = ColorHueTopbarDelta.Switch(Function(d) New Integer() {-d, 0, d}, Function(d) d)
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue, ColorSat, 54 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue, ColorSat, 54 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue, ColorSat, 54 + ColorLightAdjust)})
            FrmMain.PanTitle.Background = Brush
            FrmMain.PanTitle.Background.Freeze()
            '主页面背景
            If Settings.Get(Of Boolean)("UiBackgroundColorful") Then
                Brush = New LinearGradientBrush With {.EndPoint = New Point(0.1, 1), .StartPoint = New Point(0.9, 0)}
                Brush.GradientStops.Add(New GradientStop With {.Offset = -0.1, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.4, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1.1, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 91)})
                FrmMain.PanForm.Background = Brush
            Else
                FrmMain.PanForm.Background = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 91)
            End If
            FrmMain.PanForm.Background.Freeze()
        End Sub)
    End Sub
    Friend Sub ThemeCheckAll(EffectSetup As Boolean)
        Try
            If FrmSetupUI Is Nothing Then Return
            '只保留默认主题，其余主题不展示，避免出现与赞助解锁类似的锁定表现
            For Each Control In FrmSetupUI.PanLauncherTheme.Children
                If TypeOf Control Is MyRadioBox Then
                    Dim ThemeRadio As MyRadioBox = CType(Control, MyRadioBox)
                    ThemeRadio.Visibility = If(ThemeRadio Is FrmSetupUI.RadioLauncherTheme0, Visibility.Visible, Visibility.Collapsed)
                End If
            Next
            FrmSetupUI.RadioLauncherTheme5Gray.Visibility = Visibility.Collapsed
            FrmSetupUI.LabLauncherTheme5Unlock.Visibility = Visibility.Collapsed
            FrmSetupUI.LabLauncherTheme9Copy.Visibility = Visibility.Collapsed
            FrmSetupUI.LabLauncherTheme11Click.Visibility = Visibility.Collapsed
        Catch ex As Exception
            Logger.Warn(ex, "应用主题可用性失败")
        End Try
    End Sub
    Friend Function ThemeCheckOne(Id As Integer) As Boolean
        Return Id = 0
    End Function
    Friend Function ThemeUnlock(Id As Integer, Optional ShowDoubleHint As Boolean = True, Optional UnlockHint As String = Nothing) As Boolean
        Return False
    End Function


#End Region

#Region "更新"

    Private ReadOnly UpdUrl As String = ModMain.PclUrl("MkXol2Aes9ZiCLLWagSy0W8frtZjC6TfYwizkT9D7441X7KNKV7y")
    Private ReadOnly NoticeUrl As String = ModMain.PclUrl("MkXol2Aes9ZiCLLWagSy0W8frtZjC6TfYwiziTVF9YQ/H/aUNV8=")

    Friend Sub UpdateCheckByButton()
        RunInNewThread(
            Sub()
                Try
                    Dim Raw As String = ModMain.PclGet(UpdUrl)
                    If Raw.Length = 0 Then
                        RunInUi(Sub() Hint("更新服务器连接失败，请稍后再试", HintType.Red))
                        Return
                    End If
                    Dim NewVer As String = ModMain.JsonStr(Raw, "version")
                    Dim Dl As String = ModMain.JsonStr(Raw, "download")
                    Dim Siz As Long = ModMain.JsonNum(Raw, "size")
                    Dim Mb As String = If(Siz > 0, Math.Round(Siz / 1048576, 1) & " MB", "未知")
                    RunInUi(Sub()
                                Dim R As Integer = MyMsgBox("最新版本：" & NewVer & vbCrLf &
                                                            "安装包大小：" & Mb & vbCrLf & vbCrLf &
                                                            "是否前往下载最新版本？", "检查更新", "下载", "取消")
                                If R = 1 AndAlso Dl.Length > 0 Then ModMain.DownloadAndApplyUpdate(Dl)
                            End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "检查更新失败")
                    RunInUi(Sub() Hint("检查更新失败：" & ex.Message, HintType.Red))
                End Try
            End Sub, "检查更新")
    End Sub



    ''' <summary>
    ''' 判断当前运行版本是否等于指定版本，用于确认补丁是否适用于本机文件。
    ''' </summary>
    Private Function IsCurrentVersion(Ver As String) As Boolean
        Try
            Dim Local As Version = Reflection.Assembly.GetEntryAssembly().GetName().Version
            Dim Remote As New Version(0, 0, 0, 0)
            If Local Is Nothing Then Return False
            If Not Version.TryParse(Ver, Remote) Then Return False
            Return Local = Remote
        Catch
            Return False
        End Try
    End Function

    Public Sub UpdateCheckNow()
        Logger.Info("[PCLForK] 启动检查开始")
        Try
            Dim Raw As String = ModMain.PclGet(UpdUrl)
            Logger.Info("[PCLForK] version.json 长度=" & Raw.Length)
            If Raw.Length > 0 Then
                Dim NewVer As String = ModMain.JsonStr(Raw, "version")
                Dim Dl As String = ModMain.JsonStr(Raw, "download")
                Dim Siz As Long = ModMain.JsonNum(Raw, "size")
                Dim PatchUrl As String = ModMain.JsonStr(Raw, "patch")
                Dim PatchSize As Long = ModMain.JsonNum(Raw, "patchSize")
                Dim PatchFrom As String = ModMain.JsonStr(Raw, "patchFrom")
                Dim RemoteVer As New Version(0, 0, 0, 0)
                Dim LocalVer As Version = Reflection.Assembly.GetEntryAssembly().GetName().Version
                Dim NeedShow As Boolean = NewVer.Length > 0
                If NeedShow AndAlso Version.TryParse(NewVer, RemoteVer) AndAlso LocalVer IsNot Nothing AndAlso LocalVer >= RemoteVer Then NeedShow = False
                If NeedShow Then
                    Dim Mb As String = If(Siz > 0, Math.Round(Siz / 1048576, 1) & " MB", "未知")
                    Dim Cl As String = ModMain.JsonArr(Raw, "changelog")
                    Dim UsePatch As Boolean = PatchUrl.Length > 0 AndAlso PatchSize > 0 AndAlso (PatchFrom.Length = 0 OrElse IsCurrentVersion(PatchFrom))
                    Dim Body As String = "检测到新版本：" & NewVer & vbCrLf & "安装包大小：" & Mb
                    If UsePatch Then Body &= vbCrLf & "增量补丁：" & Math.Round(PatchSize / 1024.0, 0) & " KB（无需重下完整包）"
                    If Cl.Length > 0 Then Body &= vbCrLf & vbCrLf & "更新内容：" & vbCrLf & Cl
                    Body &= vbCrLf & vbCrLf & "是否前往下载？"
                    Dim R As Integer = MyMsgBox(Body, "发现更新", "下载", "稍后")
                    If R = 1 AndAlso Dl.Length > 0 Then ModMain.DownloadAndApplyUpdate(Dl, If(UsePatch, PatchUrl, ""))
                End If
            End If
        Catch ex As Exception
            Logger.Warn(ex, "自动检查更新失败")
        End Try
        Task.Run(
            Sub()
                Try
                    Threading.Thread.Sleep(500)
                    Dim Raw2 As String = ModMain.PclGet(NoticeUrl)
                    Logger.Info("[PCLForK] notice.json 长度=" & Raw2.Length)
                    If Raw2.Length = 0 Then Return
                    If Raw2.Contains("""enabled"": false") OrElse Raw2.Contains("""enabled"":false") Then Return
                    Dim Title As String = ModMain.JsonStr(Raw2, "title")
                    Dim Content As String = ModMain.JsonStr(Raw2, "content").Replace("\r\n", vbCrLf).Replace("\n", vbCrLf).Replace("\r", vbCrLf)
                    If Title.Length = 0 AndAlso Content.Length = 0 Then Return
                    Dim StopIt As Boolean = Raw2.Contains("""stopLauncher"": true") OrElse Raw2.Contains("""stopLauncher"":true")
                    Application.Current.Dispatcher.Invoke(
                        Sub()
                            MyMsgBox(Content, If(Title.Length > 0, Title, "公告"), "确定")
                            If StopIt Then End
                        End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "拉取公告失败")
                End Try
            End Sub)
        Try
            ModCacheClean.CleanImageCache()
            ModCacheClean.CleanTempFiles()
        Catch ex As Exception
            Logger.Warn(ex, "缓存清理失败")
        End Try
        Try
            WelfareStartPlayTimer()
        Catch ex As Exception
            Logger.Warn(ex, "福利计时启动失败")
        End Try
        Try
            WelfareCheck()
        Catch ex As Exception
            Logger.Warn(ex, "福利检查初始化失败")
        End Try
    End Sub

    Public Sub UpdateCheckSilent()
        RunInNewThread(
            Sub()
                Threading.Thread.Sleep(4000)
                Try
                    Dim Raw As String = ModMain.PclGet(UpdUrl)
                    If Raw.Length = 0 Then Return
                    Dim NewVer As String = ModMain.JsonStr(Raw, "version")
                    Dim Dl As String = ModMain.JsonStr(Raw, "download")
                    Dim Siz As Long = ModMain.JsonNum(Raw, "size")
                    If NewVer.Length = 0 Then Return
                    Dim RemoteSize As Long = Siz
                    Dim RemoteVer As New Version(0, 0, 0, 0)
                    Dim LocalVer As Version = Reflection.Assembly.GetEntryAssembly().GetName().Version
                    If Version.TryParse(NewVer, RemoteVer) AndAlso LocalVer IsNot Nothing AndAlso LocalVer >= RemoteVer Then Return
                    If Settings.Get(Of String)("PCLForKSkippedVersion") = NewVer Then Return
                    RunInUi(Sub()
                                Dim Mb As String = If(RemoteSize > 0, Math.Round(RemoteSize / 1048576, 1) & " MB", "未知")
                                Dim R As Integer = MyMsgBox("检测到新版本：" & NewVer & vbCrLf &
                                                            "安装包大小：" & Mb & vbCrLf & vbCrLf &
                                                            "是否前往下载？", "发现更新", "下载", "稍后")
                                If R = 1 AndAlso Dl.Length > 0 Then
                                    ModMain.DownloadAndApplyUpdate(Dl)
                                Else
                                    Try
                                        Settings.Set("PCLForKSkippedVersion", NewVer)
                                    Catch
                                    End Try
                                End If
                            End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "自动检查更新失败")
                End Try
            End Sub, "自动检查更新")
    End Sub

    Private ReadOnly WelfareUrl As String = ModMain.PclUrl("MkXol2Aes9ZiCLLWagSy0W8frtZjC6TfYwizkD9d+oYoVLKXMkE=")

    ''' <summary>
    ''' 领取兑换码所需的累计使用时长（秒）。
    ''' </summary>
    Private Const WelfareRequiredSeconds As Long = 1800

    ''' <summary>
    ''' 读取累计使用时长（秒）。
    ''' </summary>
    Friend Function WelfarePlayedSeconds() As Long
        Try
            Dim F As String = WelfareTimeFile()
            If Not IO.File.Exists(F) Then Return 0
            Dim V As Long
            If Long.TryParse(IO.File.ReadAllText(F).Trim(), V) Then Return V
        Catch ex As Exception
            Logger.Warn(ex, "读取累计使用时长失败")
        End Try
        Return 0
    End Function

    ''' <summary>
    ''' 累加使用时长（秒），由计时线程定期调用。
    ''' </summary>
    Friend Sub WelfareAddPlayedSeconds(Sec As Long)
        If Sec <= 0 Then Return
        Try
            Dim F As String = WelfareTimeFile()
            IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(F))
            Dim Cur As Long = WelfarePlayedSeconds()
            IO.File.WriteAllText(F, (Cur + Sec).ToString(), Global.System.Text.Encoding.UTF8)
        Catch ex As Exception
            Logger.Warn(ex, "累计使用时长写入失败")
        End Try
    End Sub

    ''' <summary>
    ''' 累计使用时长文件路径。
    ''' </summary>
    Private Function WelfareTimeFile() As String
        Dim Dir As String = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCL-ForK")
        Return IO.Path.Combine(Dir, "playtime.dat")
    End Function

    ''' <summary>
    ''' 启动累计计时。每 60 秒累加一次，退出时补最后一次。
    ''' </summary>
    Private _WelfareTimer As Threading.Timer = Nothing

    Friend Sub WelfareStartPlayTimer()
        If _WelfareTimer IsNot Nothing Then Return
        Try
            _WelfareTimer = New Threading.Timer(
                Sub(State)
                    Try
                        WelfareAddPlayedSeconds(60)
                        Logger.Info("[福利] 累计使用时长 " & (WelfarePlayedSeconds() \ 60) & " 分钟")
                    Catch ex As Exception
                        Logger.Warn(ex, "福利计时回调失败")
                    End Try
                End Sub, Nothing, 60000, 60000)
            Logger.Info("[福利] 使用时长计时已启动")
        Catch ex As Exception
            Logger.Warn(ex, "启动福利计时失败")
        End Try
    End Sub

    Friend Sub WelfareCheck(Optional Force As Boolean = False) 
        RunInNewThread(
            Sub()
                Try
                    '登录缓存要等启动流程加载完才可用，轮询等待而不是死等固定时长
                    Dim Uuid As String = ""
                    Dim PName As String = ""
                    For Round As Integer = 1 To If(Force, 3, 10)
                        Threading.Thread.Sleep(If(Force, 120, 600))
                        Try
                            Uuid = Settings.Get(Of String)("CacheMsV2Uuid")
                            PName = Settings.Get(Of String)("CacheMsV2Name")
                        Catch
                        End Try
                        If Uuid IsNot Nothing AndAlso Uuid.Length >= 8 Then Exit For
                    Next
                    Dim LType As Integer = 0
                    Try
                        LType = Settings.Get(Of Integer)("LoginType")
                    Catch
                    End Try
                    Logger.Info("[福利] 检查开始，LoginType=" & LType)
                    If LType <> 5 Then Return
                    '累计使用时长门槛
                    Dim PlayedSec As Long = WelfarePlayedSeconds()
                    Logger.Info("[福利] 累计使用时长 " & (PlayedSec \ 60) & " 分钟，要求 " & (WelfareRequiredSeconds \ 60) & " 分钟")
                    If PlayedSec < WelfareRequiredSeconds Then
                        Dim Remain As Long = (WelfareRequiredSeconds - PlayedSec + 59) \ 60
                        If Force Then RunInUi(Sub() Hint("还需要使用 " & Remain & " 分钟才能领取兑换码", HintType.Blue))
                        Return
                    End If
                    Logger.Info("[福利] Uuid长度=" & If(Uuid Is Nothing, -1, Uuid.Length) & " Name=" & If(PName, ""))
                    If Uuid Is Nothing OrElse Uuid.Length < 8 Then
                        '登录缓存解密失败或缺失时，用机器标识派生一个稳定 UUID，保证同一台机器只领一次
                        Dim Seed As String = ""
                        Try
                            Seed = Settings.Get(Of String)("Identify")
                        Catch
                        End Try
                        If String.IsNullOrEmpty(Seed) Then Seed = Environment.MachineName
                        Try
                            Using Md5 As Security.Cryptography.MD5 = Security.Cryptography.MD5.Create()
                                Dim Hash As Byte() = Md5.ComputeHash(Text.Encoding.UTF8.GetBytes("PCLForK-Welfare-" & Seed & "-" & If(PName, "")))
                                Uuid = New Guid(Hash).ToString()
                            End Using
                            Logger.Info("[福利] 登录缓存不可用，改用派生 UUID：" & Uuid)
                        Catch ex As Exception
                            Logger.Warn(ex, "[福利] 派生 UUID 失败")
                            Return
                        End Try
                    End If
                    If PName Is Nothing Then PName = ""
                    Dim Url As String = WelfareUrl & "?uuid=" & Uri.EscapeDataString(Uuid) & "&name=" & Uri.EscapeDataString(PName)
                    Logger.Info("[福利] 请求 " & Url)
                    Dim Raw As String = ModMain.PclGet(Url)
                    Logger.Info("[福利] 返回长度=" & Raw.Length & " 内容=" & Raw)
                    If Raw.Length = 0 Then Return
                    If Not Raw.Contains("""ok"":true") AndAlso Not Raw.Contains("""ok"": true") Then Return
                    Dim Code As String = ModMain.JsonStr(Raw, "code")
                    If Code.Length = 0 Then Return
                    Dim Ttl As String = ModMain.JsonStr(Raw, "title")
                    Dim Dsc As String = ModMain.JsonStr(Raw, "desc")
                    Dim IsAlready As Boolean = Raw.Contains("""already"":true") OrElse Raw.Contains("""already"": true")
                    Dim Shown As String = ""
                    Try
                        Shown = Settings.Get(Of String)("WelfareShownCode")
                    Catch
                    End Try
                    If Shown IsNot Nothing AndAlso Shown = Code AndAlso Not Force Then Return
                    RunInUi(
                        Sub()
                            Dim Body As String = ""
                            If Dsc.Length > 0 Then Body &= Dsc & vbCrLf & vbCrLf
                            If IsAlready Then Body &= "你之前已经领取过了。" & vbCrLf & vbCrLf
                            Body &= "兑换码：" & vbCrLf & Code
                            MyMsgBox(Body, If(Ttl.Length > 0, Ttl, "福利兑换码"), "复制")
                            Try
                                Clipboard.SetText(Code)
                            Catch
                            End Try
                            Try
                                Settings.Set("WelfareShownCode", Code)
                            Catch
                            End Try
                        End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "拉取福利兑换码失败")
                End Try
            End Sub, "福利检查")
    End Sub

    Friend Sub NoticeCheck()
        RunInNewThread(
            Sub()
                Threading.Thread.Sleep(6000)
                Try
                    Dim Raw As String = ModMain.PclGet(NoticeUrl)
                    If Raw.Length = 0 Then Return
                    If Not Raw.Contains("true") Then Return
                    Dim Title As String = ModMain.JsonStr(Raw, "title")
                    Dim Content As String = ModMain.JsonStr(Raw, "content").Replace("\r\n", vbCrLf).Replace("\n", vbCrLf).Replace("\r", vbCrLf)
                    If Title.Length = 0 AndAlso Content.Length = 0 Then Return
                    Dim StopIt As Boolean = Raw.Contains("""stopLauncher"": true") OrElse Raw.Contains("""stopLauncher"":true")
                    RunInUi(Sub()
                                MyMsgBox(Content, If(Title.Length > 0, Title, "公告"), "确定")
                                If StopIt Then End
                            End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "拉取公告失败")
                End Try
            End Sub, "公告检查")
    End Sub

    Friend IsUpdateWaitingRestart As Boolean = False
    Public Sub UpdateRestart(TriggerRestartAndByEnd As Boolean)
    End Sub
    Public Sub UpdateReplace(ProcessId As Integer, OldFileName As String, NewFileName As String, TriggerRestart As Boolean)
    End Sub

    Friend Sub DownloadLatestPCL(Optional LoaderToSyncProgress As LoaderBase = Nothing)
    End Sub

#End Region

#Region "联网配置"

    ''' <summary>
    ''' 联网获取的配置信息。
    ''' 若获取失败或仍在获取中，可能为 Nothing。
    ''' </summary>
    Public ServerConfig As JObject

    Public ServerLoader As New LoaderTask(Of Integer, Integer)("PCL 配置更新", AddressOf ServerLoaderSub, Priority:=ThreadPriority.BelowNormal) With
        {.ReloadTimeout = 1000 * 60 * 60} '超时 1 小时

    Private Sub ServerLoaderSub(Task As LoaderTask(Of Integer, Integer))
        Dim CacheAddress As String = PathTemp & "Cache\ServerConfig.json"
        Try
            If FileUtils.Exists(CacheAddress) Then ServerConfig = FileUtils.ReadAsString(CacheAddress).DeserializeJson()
        Catch ex As Exception
            Logger.Warn(ex, "读取本地服务器配置缓存失败")
            ServerConfig = Nothing
        End Try
        Try
            Dim RawConfig As String = NetRequestByClient(SecretCdnSign("https://pcl2-server-1253424809.file.myqcloud.com/ServerConfig.json{CDN}"), RequireJson:=True)
            ServerConfig = RawConfig.DeserializeJson()
            DirectoryUtils.Create(PathTemp & "Cache\")
            FileUtils.Write(CacheAddress, RawConfig)
            Logger.Info("已从服务器获取 PCL 配置")
        Catch ex As Exception
            Logger.Warn(ex, "获取 PCL 服务器配置失败，将使用本地缓存")
        End Try
        If ServerConfig Is Nothing Then Throw New Exception("无法获取 PCL 服务器配置")
        Try
            Dim LinkConfig As JObject = TryCast(ServerConfig("Link"), JObject)
            If LinkConfig IsNot Nothing Then
                LinkConfig.Remove("DisableReason")
                LinkConfig.Remove("DisableReason2")
            End If
        Catch ex As Exception
            Logger.Warn(ex, "处理 PCL 服务器配置失败")
        End Try
    End Sub

#End Region

#Region "身份标识"

    Public ReadOnly Property CurrentRank As DonationRank
        Get
            Return DonationRank.None
        End Get
    End Property

    Public Sub InputPotatoCode(IsUpdating As Boolean)
    End Sub
    Friend Sub GeneratePotatoCode()
    End Sub

    ''' <summary>
    ''' 获取设备识别码。
    ''' </summary>
    Friend Function GetIdentify() As String
        Return "0000-0000-0000-0000"
    End Function

#End Region

End Module
