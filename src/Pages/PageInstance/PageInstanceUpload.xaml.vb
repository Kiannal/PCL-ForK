Imports System.IO
Imports System.IO.Compression
Imports System.Net
Imports System.Text
Imports System.Windows.Media

Public Class PageInstanceUpload
    Inherits MyPageRight

    Private SelectedPath As String = ""
    

    Private Sub PageInstanceUpload_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        RefreshList()
    End Sub

    Public Sub Refresh()
        RefreshList()
    End Sub

    Public Sub RefreshList()
        PanList.Children.Clear()
        SelectedPath = ""
        Try
            Dim Dir As String = McInstanceSelected.PathIndie & "saves"
            If Dir Is Nothing OrElse Not Directory.Exists(Dir) Then
                LabTip.Text = "当前版本没有存档文件夹（尚未进入过游戏）"
                Return
            End If
            Dim N As Integer = 0
            For Each Sub1 As String In Directory.GetDirectories(Dir)
                Dim Info As New DirectoryInfo(Sub1)
                Dim Size As Long = 0
                Try
                    For Each F As FileInfo In Info.GetFiles("*", SearchOption.AllDirectories)
                        Size += F.Length
                    Next
                Catch
                End Try
                PanList.Children.Add(BuildRow(Info.Name, Math.Round(Size / 1048576, 1) & " MB    " & Info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), Info.FullName))
                N += 1
            Next
            LabTip.Text = If(N = 0, "没有找到存档", "共 " & N & " 个存档 · 上传后保留 7 天")
        Catch ex As Exception
            LabTip.Text = "读取存档失败：" & ex.Message
        End Try
    End Sub

    Private Function BuildRow(Name As String, Info As String, Path As String) As Border
        Dim Row As New Border With {
            .CornerRadius = New CornerRadius(6),
            .Padding = New Thickness(14, 10, 14, 10),
            .Margin = New Thickness(0, 0, 0, 6),
            .Background = New SolidColorBrush(Color.FromRgb(255, 255, 255)),
            .Tag = Path
        }
        Dim G As New Grid
        G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(0, GridUnitType.Auto)})
        G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        G.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(0, GridUnitType.Auto)})

        Dim IconBox As New Border With {
            .Width = 44, .Height = 44,
            .CornerRadius = New CornerRadius(8),
            .Background = New SolidColorBrush(Color.FromRgb(240, 243, 247)),
            .Margin = New Thickness(0, 0, 12, 0),
            .VerticalAlignment = VerticalAlignment.Center
        }
        Dim Icon As New TextBlock With {
            .Text = "📁", .FontSize = 20,
            .HorizontalAlignment = HorizontalAlignment.Center,
            .VerticalAlignment = VerticalAlignment.Center
        }
        IconBox.Child = Icon
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
            .Width = 30, .Height = 30,
            .ToolTip = "选择并上传",
            .VerticalAlignment = VerticalAlignment.Center,
            .Logo = "M9 16h6v-6h4l-7-7-7 7h4v6zm-4 4h14v-2H5v2z"
        }
        Grid.SetColumn(Btn, 2)
        G.Children.Add(Btn)

        Row.Child = G
        AddHandler Btn.Click, Sub(s, e)
                                  SelectedPath = Path
                                  For Each C In PanList.Children
                                      Dim B = TryCast(C, Border)
                                      If B IsNot Nothing Then B.Background = New SolidColorBrush(Color.FromRgb(255, 255, 255))
                                  Next
                                  Row.Background = New SolidColorBrush(Color.FromRgb(232, 240, 255))
                                  BtnUpload_Click(Nothing, Nothing)
                              End Sub
        AddHandler Row.MouseLeftButtonUp, Sub(s, e)
                                              SelectedPath = Path
                                              For Each C In PanList.Children
                                                  Dim B = TryCast(C, Border)
                                                  If B IsNot Nothing Then B.Background = New SolidColorBrush(Color.FromRgb(255, 255, 255))
                                              Next
                                              Row.Background = New SolidColorBrush(Color.FromRgb(232, 240, 255))
                                          End Sub
        AddHandler Row.MouseEnter, Sub(s, e) If SelectedPath <> Path Then Row.Background = New SolidColorBrush(Color.FromRgb(248, 250, 252))
        AddHandler Row.MouseLeave, Sub(s, e) If SelectedPath <> Path Then Row.Background = New SolidColorBrush(Color.FromRgb(255, 255, 255))
        Return Row
    End Function

    Private Sub BtnRefresh_Click(sender As Object, e As EventArgs) Handles BtnRefresh.Click
        RefreshList()
    End Sub

    Private Sub BtnUpload_Click(sender As Object, e As EventArgs) Handles BtnUpload.Click
        If SelectedPath = "" Then
            Hint("请先在列表中选择一个存档", HintType.Red)
            Return
        End If
        Dim Src As String = SelectedPath
        Dim Name As String = IO.Path.GetFileName(Src)
        BtnUpload.IsEnabled = False
        LabStatus.Text = "正在打包……"
        RunInNewThread(
            Sub()
                Dim ZipPath As String = IO.Path.Combine(PathTemp, "PCLSave_" & Guid.NewGuid().ToString("N") & ".zip")
                Try
                    ZipFile.CreateFromDirectory(Src, ZipPath, CompressionLevel.Optimal, False)
                    RunInUi(Sub() LabStatus.Text = "正在上传（" & Math.Round(New FileInfo(ZipPath).Length / 1048576, 1) & " MB）……")
                    Dim Resp As String = UploadFile(ZipPath, Name)
                    RunInUi(Sub()
                                Try
                                    Dim Url As String = Mid(Resp, InStr(Resp, """url"":") + 7)
                                    Url = Url.Substring(0, Url.IndexOf(""""c))
                                    LabStatus.Text = "已上传 · 7 天后自动删除"
                                    MyMsgBox("存档成功上传至云端进行加密" & vbCrLf & vbCrLf & "为保证服务器流畅与正常运行，此存档将在七天后从服务器删除。", "上传成功", "确定")
                                Catch
                                    LabStatus.Text = "返回异常：" & Left(Resp, 120)
                                End Try
                            End Sub)
                Catch ex As Exception
                    RunInUi(Sub()
                                LabStatus.Text = "上传失败：" & ex.Message
                                Hint("上传失败：" & ex.Message, HintType.Red)
                            End Sub)
                Finally
                    Try : IO.File.Delete(ZipPath) : Catch : End Try
                    RunInUi(Sub() BtnUpload.IsEnabled = True)
                End Try
            End Sub, "上传存档")
    End Sub

    Private Const BaseUrl As String = ModServerEndpoints.BaseUrl

    Private Function UploadFile(ZipPath As String, SaveName As String) As String
        Dim Did As String = ModMain.PclDeviceId()
        Dim ReqJson As String = ModMain.PclGet(ModMain.PclUrl(BaseUrl) & "/saveup.php?did=" & Uri.EscapeDataString(Did) & "&name=" & Uri.EscapeDataString(SaveName))
        Dim PutUrl As String = CutJson(ReqJson, "put")
        If PutUrl.Length = 0 Then Return ReqJson
        Dim Put As HttpWebRequest = CType(WebRequest.Create(PutUrl), HttpWebRequest)
        Put.Method = "PUT"
        Put.Timeout = 600000
        Put.ReadWriteTimeout = 600000
        Put.ContentType = "application/zip"
        Put.ContentLength = New FileInfo(ZipPath).Length
        Using S As Stream = Put.GetRequestStream()
            Using Fs As New FileStream(ZipPath, FileMode.Open, FileAccess.Read)
                Dim Buf(81919) As Byte
                Dim Read As Integer
                Do
                    Read = Fs.Read(Buf, 0, Buf.Length)
                    If Read <= 0 Then Exit Do
                    S.Write(Buf, 0, Read)
                Loop
            End Using
        End Using
        Try
            Using Resp As HttpWebResponse = CType(Put.GetResponse(), HttpWebResponse)
                Return "{""ok"":true,""url"":""https://saves/"" & CutJson(ReqJson, ""key"") & """"}"
            End Using
        Catch ex As WebException
            Return "{""ok"":false,""msg"":""" & ex.Message.Replace("""", "'") & """}"
        End Try
    End Function

    Private Shared Function CutJson(Raw As String, Key As String) As String
        Dim K As String = """" & Key & """:"""
        Dim I As Integer = Raw.IndexOf(K, StringComparison.Ordinal)
        If I < 0 Then Return ""
        Dim S As Integer = I + K.Length
        Dim E As Integer = Raw.IndexOf(""""c, S)
        If E < 0 Then Return ""
        Return Raw.Substring(S, E - S)
    End Function

End Class
