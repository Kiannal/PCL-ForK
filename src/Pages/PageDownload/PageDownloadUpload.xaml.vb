Imports System.Windows.Media

Public Class PageDownloadUpload

    Private Sub PageDownloadUpload_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If PanTypes.Children.Count > 0 Then Return
        Dim Types As New List(Of KeyValuePair(Of String, String)) From {
            New KeyValuePair(Of String, String)("mod", "上传 Mod"),
            New KeyValuePair(Of String, String)("pack", "上传整合包"),
            New KeyValuePair(Of String, String)("resourcepack", "上传资源包"),
            New KeyValuePair(Of String, String)("datapack", "上传数据包"),
            New KeyValuePair(Of String, String)("shader", "上传光影包")
        }
        For Each KV As KeyValuePair(Of String, String) In Types
            Dim Btn As New MyButton() With {.Text = KV.Value, .Width = 122, .Height = 36, .Margin = New Thickness(0, 0, 10, 10)}
            Dim TypeKey As String = KV.Key
            Dim TypeName As String = KV.Value.Replace("上传 ", "")
            AddHandler Btn.Click,
                Sub(s2, e2)
                    Dim Dlg As New Microsoft.Win32.OpenFileDialog()
                    Dlg.Title = "选择要上传的" & TypeName
                    Dlg.Filter = "资源文件|*.jar;*.zip;*.7z;*.rar;*.mrpack;*.mcpack|所有文件|*.*"
                    If Dlg.ShowDialog() <> True Then Return
                    Dim Path As String = Dlg.FileName
                    Dim Title As String = GetInnerText(TextName).Trim()
                    If Title.Length = 0 Then Title = System.IO.Path.GetFileNameWithoutExtension(Path)
                    Dim Desc As String = GetInnerText(TextDesc).Trim()
                    LabState.Text = "正在上传 " & System.IO.Path.GetFileName(Path) & " …"
                    Dim Th As New Threading.Thread(
                        Sub()
                            Try
                                Dim R As String = UploadFile(TypeKey, Title, Desc, Path)
                                Dispatcher.Invoke(
                                    Sub()
                                        If R.Contains("""ok"":true") Then
                                            LabState.Text = "上传成功：" & Title
                                            MyMsgBox("上传成功，其他人已可在对应下载页看到并下载。" & vbCrLf & Title, "上传" & TypeName)
                                        Else
                                            LabState.Text = "上传失败：" & R
                                        End If
                                    End Sub)
                            Catch Ex As Exception
                                Dispatcher.Invoke(Sub() LabState.Text = "上传失败：" & Ex.Message)
                            End Try
                        End Sub)
                    Th.IsBackground = True
                    Th.Start()
                End Sub
            PanTypes.Children.Add(Btn)
        Next
    End Sub

    Private Function GetInnerText(Root As DependencyObject) As String
        Try
            Dim TB As System.Windows.Controls.TextBox = FindInnerTextbox(Root)
            If TB IsNot Nothing Then Return If(TB.Text, "")
        Catch
        End Try
        Return ""
    End Function

    Private Function FindInnerTextbox(Root As DependencyObject) As System.Windows.Controls.TextBox
        If Root Is Nothing Then Return Nothing
        Dim Self As System.Windows.Controls.TextBox = TryCast(Root, System.Windows.Controls.TextBox)
        If Self IsNot Nothing Then Return Self
        Dim Count As Integer = 0
        Try
            Count = VisualTreeHelper.GetChildrenCount(Root)
        Catch
            Return Nothing
        End Try
        For I As Integer = 0 To Count - 1
            Dim R As System.Windows.Controls.TextBox = FindInnerTextbox(VisualTreeHelper.GetChild(Root, I))
            If R IsNot Nothing Then Return R
        Next
        Return Nothing
    End Function

    Private Function UploadFile(Type As String, Name As String, Desc As String, FilePath As String) As String
        Dim Boundary As String = "----PCLForK" & Guid.NewGuid().ToString("N")
        Dim Enc As Text.Encoding = Text.Encoding.UTF8
        Using MS As New IO.MemoryStream()
            Dim Fields As New List(Of KeyValuePair(Of String, String)) From {
                New KeyValuePair(Of String, String)("type", Type),
                New KeyValuePair(Of String, String)("name", Name),
                New KeyValuePair(Of String, String)("desc", Desc)
            }
            For Each KV As KeyValuePair(Of String, String) In Fields
                Dim Part As String = "--" & Boundary & vbCrLf & "Content-Disposition: form-data; name=""" & KV.Key & """" & vbCrLf & vbCrLf & KV.Value & vbCrLf
                Dim PB As Byte() = Enc.GetBytes(Part)
                MS.Write(PB, 0, PB.Length)
            Next
            Dim FileName As String = IO.Path.GetFileName(FilePath)
            Dim Head As String = "--" & Boundary & vbCrLf & "Content-Disposition: form-data; name=""file""; filename=""" & FileName & """" & vbCrLf & "Content-Type: application/octet-stream" & vbCrLf & vbCrLf
            Dim HB As Byte() = Enc.GetBytes(Head)
            MS.Write(HB, 0, HB.Length)
            Dim FB As Byte() = IO.File.ReadAllBytes(FilePath)
            MS.Write(FB, 0, FB.Length)
            Dim Tail As Byte() = Enc.GetBytes(vbCrLf & "--" & Boundary & "--" & vbCrLf)
            MS.Write(Tail, 0, Tail.Length)

            Dim Req As Net.HttpWebRequest = CType(Net.WebRequest.Create(ModMain.PclUrl("MkXol2Aes9ZiCLLWagSy0W8frtZjC6TfYwizkipd84Y+H+yPKg==")), Net.HttpWebRequest)
            Req.Method = "POST"
            Req.ContentType = "multipart/form-data; boundary=" & Boundary
            Req.ContentLength = MS.Length
            Req.Proxy = Nothing
            Req.UserAgent = "PCL-ForK"
            Req.Timeout = 900000
            Req.ReadWriteTimeout = 900000
            MS.Position = 0
            Using RS As IO.Stream = Req.GetRequestStream()
                MS.CopyTo(RS)
            End Using
            Using Resp As Net.WebResponse = Req.GetResponse()
                Using SR As New IO.StreamReader(Resp.GetResponseStream(), Text.Encoding.UTF8)
                    Return SR.ReadToEnd()
                End Using
            End Using
        End Using
    End Function

End Class
