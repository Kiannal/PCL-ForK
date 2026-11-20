Imports System.IO
Imports System.IO.Compression
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Media

Public Class PageInstanceDownload
    Inherits MyPageRight

    Private Const DlBase As String = ModServerEndpoints.BaseUrl
    Private Const ListUrl As String = ModServerEndpoints.BaseUrl

    Private Sub PageInstanceDownload_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        RefreshList()
    End Sub

    Public Sub Refresh()
        RefreshList()
    End Sub

    Private Sub RefreshList()
        PanList.Children.Clear()
        LabTip.Text = "正在读取云端存档……"
        RunInNewThread(
            Sub()
                Dim Raw As String = ""
                Try
                    Raw = GetJson(ModMain.PclUrl(ListUrl) & "?did=" & ModMain.PclDeviceId())
                Catch ex As Exception
                    RunInUi(Sub() LabTip.Text = "连接失败：" & ex.Message)
                    Return
                End Try
                Dim Items As New List(Of String())
                Dim Clean As String = Raw.Replace(Chr(13), "").Replace(Chr(10), Chr(10))
                For Each OneLine As String In Clean.Split(New Char() {Chr(10)})
                    Dim TL As String = OneLine.Trim()
                    If TL.Length = 0 Then Continue For
                    Dim PP() As String = TL.Split(New Char() {CChar("|")})
                    If PP.Length >= 3 Then Items.Add(New String() {PP(0), PP(1), PP(2), If(PP.Length >= 4, PP(3), "")})
                Next
                RunInUi(
                    Sub()
                        PanList.Children.Clear()
                        If Items.Count = 0 Then
                            LabTip.Text = "云端没有存档（长度 " & Raw.Length & "，行 " & Clean.Split(New Char() {Chr(10)}).Length & "）"
                            Return
                        End If
                        For Each It In Items
                            Dim T As DateTime = DateTime.MinValue
                            DateTime.TryParse(It(2), Nothing, Globalization.DateTimeStyles.RoundtripKind, T)
                            Dim Info As String = Math.Round(Val(It(1)) / 1048576, 1) & " MB    " & If(T = DateTime.MinValue, "", T.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
                            PanList.Children.Add(BuildRow(It(0), Info, It(3)))
                        Next
                        LabTip.Text = "共 " & Items.Count & " 个云端存档 · 超过 7 天自动清理"
                    End Sub)
            End Sub, "读取云端存档")
    End Sub

    Private Function BuildRow(Name As String, Info As String, Url As String) As Border
        Dim Row As New Border With {
            .CornerRadius = New CornerRadius(6),
            .Padding = New Thickness(14, 10, 14, 10),
            .Margin = New Thickness(0, 0, 0, 6),
            .Background = New SolidColorBrush(Color.FromRgb(255, 255, 255)),
            .Tag = Url
        }
        Dim G As New Grid
        G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(0, GridUnitType.Auto)})
        G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(0, GridUnitType.Auto)})

        Dim IconBox As New Border With {
            .Width = 44, .Height = 44, .CornerRadius = New CornerRadius(8),
            .Background = New SolidColorBrush(Color.FromRgb(240, 243, 247)),
            .Margin = New Thickness(0, 0, 12, 0),
            .VerticalAlignment = VerticalAlignment.Center
        }
        IconBox.Child = New TextBlock With {
            .Text = "🗃", .FontSize = 20,
            .HorizontalAlignment = HorizontalAlignment.Center,
            .VerticalAlignment = VerticalAlignment.Center
        }
        Grid.SetColumn(IconBox, 0)
        G.Children.Add(IconBox)

        Dim Sp As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        Dim T1 As New TextBlock With {.Text = Name, .FontSize = 14, .FontWeight = FontWeights.SemiBold, .TextTrimming = TextTrimming.CharacterEllipsis}
        T1.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Dim T2 As New TextBlock With {.Text = Info, .FontSize = 12, .Margin = New Thickness(0, 3, 0, 0)}
        T2.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray3")
        Sp.Children.Add(T1)
        Sp.Children.Add(T2)
        Grid.SetColumn(Sp, 1)
        G.Children.Add(Sp)

        Dim Btn As New MyIconButton With {
            .Width = 30, .Height = 30, .ToolTip = "下载到本版本",
            .VerticalAlignment = VerticalAlignment.Center,
            .Logo = "M12 16l-6-6h4V4h4v6h4l-6 6zm-8 4h16v-2H4v2z"
        }
        Grid.SetColumn(Btn, 2)
        G.Children.Add(Btn)

        Row.Child = G
        AddHandler Btn.Click, Sub(s, e) DownloadSave(Url, Name)
        AddHandler Row.MouseEnter, Sub(s, e) Row.Background = New SolidColorBrush(Color.FromRgb(248, 250, 252))
        AddHandler Row.MouseLeave, Sub(s, e) Row.Background = New SolidColorBrush(Color.FromRgb(255, 255, 255))
        Return Row
    End Function

    Private Sub DownloadSave(Url As String, Name As String)
        LabStatus.Text = "正在下载 " & Name & " ……"
        RunInNewThread(
            Sub()
                Dim ZipPath As String = IO.Path.Combine(PathTemp, "PCLCloud_" & Guid.NewGuid().ToString("N") & ".zip")
                Try
                    Using Wc As New WebClient()
                        Dim Key As String = Url.Substring(Url.IndexOf("/") + 20)
                        Dim LinkJson As String = GetJson(ModMain.PclUrl(DlBase) & "/savelink.php?did=" & ModMain.PclDeviceId() & "&key=" & Uri.EscapeDataString(Key))
                        Dim GetUrl As String = CutJson(LinkJson, "get")
                        If GetUrl.Length = 0 Then Throw New Exception("获取下载地址失败")
                        Wc.DownloadFile(GetUrl, ZipPath)
                    End Using
                    Dim SaveRoot As String = McInstanceSelected.PathIndie & "saves"
                    If Not Directory.Exists(SaveRoot) Then Directory.CreateDirectory(SaveRoot)
                    Dim Base As String = IO.Path.GetFileNameWithoutExtension(Name)
                    Dim Target As String = IO.Path.Combine(SaveRoot, Base)
                    Dim N As Integer = 1
                    While Directory.Exists(Target)
                        Target = IO.Path.Combine(SaveRoot, Base & "_" & N)
                        N += 1
                    End While
                    ZipFile.ExtractToDirectory(ZipPath, Target)
                    RunInUi(Sub()
                                LabStatus.Text = "已解压到 saves" & IO.Path.GetFileName(Target)
                                Hint("存档已下载：" & IO.Path.GetFileName(Target), HintType.Green)
                            End Sub)
                Catch ex As Exception
                    RunInUi(Sub() LabStatus.Text = "下载失败：" & ex.Message)
                Finally
                    Try : IO.File.Delete(ZipPath) : Catch : End Try
                End Try
            End Sub, "下载云端存档")
    End Sub

    Private Shared Function CutJson(Raw As String, Key As String) As String
        Dim K As String = """" & Key & """:"""
        Dim I As Integer = Raw.IndexOf(K, StringComparison.Ordinal)
        If I < 0 Then Return ""
        Dim S As Integer = I + K.Length
        Dim E As Integer = Raw.IndexOf(""""c, S)
        If E < 0 Then Return ""
        Return Raw.Substring(S, E - S)
    End Function

    Private Function GetJson(Url As String) As String
        Dim Req As HttpWebRequest = CType(WebRequest.Create(Url), HttpWebRequest)
        Req.Timeout = 15000
        Req.ReadWriteTimeout = 15000
        Req.UserAgent = "PCL-ForK"
        Req.AutomaticDecompression = Net.DecompressionMethods.GZip Or Net.DecompressionMethods.Deflate
        Using Resp As HttpWebResponse = CType(Req.GetResponse(), HttpWebResponse)
            Using Sr As New StreamReader(Resp.GetResponseStream(), Encoding.UTF8)
                Return Sr.ReadToEnd()
            End Using
        End Using
    End Function

    Private Sub BtnRefresh_Click(sender As Object, e As EventArgs) Handles BtnRefresh.Click
        RefreshList()
    End Sub

End Class
