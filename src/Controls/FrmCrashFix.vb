Imports System.Threading.Tasks
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports Newtonsoft.Json.Linq

Public Class FrmCrashFix
    Inherits Window

    Private ReadOnly Detail As String
    Private ReadOnly LabAnalysis As New TextBlock With {.TextWrapping = TextWrapping.Wrap, .FontSize = 12.5, .Foreground = New SolidColorBrush(Color.FromRgb(200, 205, 212))}
    Private ReadOnly LabStatus As New TextBlock With {.TextWrapping = TextWrapping.Wrap, .FontSize = 12, .Margin = New Thickness(0, 8, 0, 0), .Foreground = New SolidColorBrush(Color.FromRgb(150, 162, 176))}

    Public Sub New(detail As String)
        Me.Detail = detail
        Title = "Minecraft 崩溃诊断"
        Width = 760
        Height = 600
        WindowStartupLocation = WindowStartupLocation.CenterScreen
        Background = New SolidColorBrush(Color.FromRgb(22, 24, 28))
        Content = BuildUi()
        AddHandler Loaded, Sub() Analyze()
    End Sub

    Private Function BuildUi() As UIElement
        Dim Root As New Grid With {.Margin = New Thickness(20)}
        Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
        Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Dim Header As New StackPanel
        Header.Children.Add(New TextBlock With {.Text = "Minecraft 崩溃诊断", .FontSize = 22, .FontWeight = FontWeights.SemiBold, .Foreground = Brushes.White})
        Header.Children.Add(New TextBlock With {.Text = "DeepSeek 正在读取崩溃日志并给出修复建议……", .FontSize = 12, .Margin = New Thickness(0, 4, 0, 0), .Foreground = New SolidColorBrush(Color.FromRgb(150, 162, 176))})
        Root.Children.Add(Header)

        Dim Scroll As New ScrollViewer With {.VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .Margin = New Thickness(0, 12, 0, 8)}
        Dim Stack As New StackPanel
        Stack.Children.Add(New TextBlock With {.Text = Detail, .TextWrapping = TextWrapping.Wrap, .FontSize = 11.5, .Foreground = New SolidColorBrush(Color.FromRgb(200, 205, 212))})
        Stack.Children.Add(New Separator With {.Margin = New Thickness(0, 10, 0, 10)})
        Stack.Children.Add(LabAnalysis)
        Stack.Children.Add(LabStatus)
        Scroll.Content = Stack
        Grid.SetRow(Scroll, 1)
        Root.Children.Add(Scroll)

        Dim Bar As New StackPanel With {.Orientation = Orientation.Horizontal, .HorizontalAlignment = HorizontalAlignment.Right, .Margin = New Thickness(0, 12, 0, 0)}
        Dim BtnInstall As New Button With {.Content = "自动安装缺失前置", .Height = 30, .Margin = New Thickness(0, 0, 10, 0)}
        Dim BtnDisable As New Button With {.Content = "禁用最近添加的 Mod", .Height = 30, .Margin = New Thickness(0, 0, 10, 0)}
        Dim BtnOpen As New Button With {.Content = "打开 Mods 文件夹", .Height = 30, .Margin = New Thickness(0, 0, 10, 0)}
        Dim BtnClose As New Button With {.Content = "关闭", .Height = 30}
        AddHandler BtnInstall.Click, Sub() InstallMissing()
        AddHandler BtnDisable.Click, Sub() DisableNewest()
        AddHandler BtnOpen.Click, Sub()
                                      Try
                                          OpenExplorer(McInstanceSelected.PathIndie & "mods\")
                                      Catch ex As Exception
                                      End Try
                                  End Sub
        AddHandler BtnClose.Click, Sub() Close()
        Bar.Children.Add(BtnInstall)
        Bar.Children.Add(BtnDisable)
        Bar.Children.Add(BtnOpen)
        Bar.Children.Add(BtnClose)
        Grid.SetRow(Bar, 2)
        Root.Children.Add(Bar)
        Return Root
    End Function

    Private Sub Analyze()
        Task.Run(Sub()
                     Dim Text As String = ""
                     Try
                         Text = ModAutoTranslate.AskDeepSeek("你是 Minecraft 崩溃诊断助手。根据以下崩溃日志，用简体中文简短说明原因并给出修复步骤，不超过 150 字。" & vbCrLf & Detail)
                     Catch ex As Exception
                     End Try
                     If String.IsNullOrWhiteSpace(Text) Then Text = "AI 分析失败，可尝试自动安装缺失前置或禁用最近添加的 Mod。"
                     Dispatcher.Invoke(Sub() LabAnalysis.Text = Text)
                 End Sub)
    End Sub

    Private Sub InstallMissing()
        LabStatus.Text = "正在识别缺失的前置 Mod……"
        Task.Run(Sub()
                     Dim Names As New List(Of String)
                     For Each Kw As String In {"libipn", "malilib", "sodium", "fabric-api", "architectury", "cloth-config", "geckolib", "konkrete", "kotlin"}
                         If Detail.ToLower().Contains(Kw) Then Names.Add(Kw)
                     Next
                     If Names.Count = 0 Then
                         Dispatcher.Invoke(Sub() LabStatus.Text = "日志里没有识别到常见前置名称，请手动查看 Mod 页面。")
                         Return
                     End If
                     Dim Ok As New List(Of String)
                     For Each Nm As String In Names
                         Try
                             If InstallMod(Nm) Then Ok.Add(Nm)
                         Catch ex As Exception
                         End Try
                     Next
                     Dispatcher.Invoke(Sub()
                                           LabStatus.Text = If(Ok.Count > 0, "已安装：" & String.Join(", ", Ok), "自动安装失败，可在 Modrinth 手动下载。")
                                       End Sub)
                 End Sub)
    End Sub

    Private Function InstallMod(Name As String) As Boolean
        Dim Ver As String = "1.20.1"
        Dim Loader As String = "fabric"
        Dim Inst As McInstance = Nothing
        Try
            Inst = McInstanceSelected
            If Inst Is Nothing AndAlso FrmMain IsNot Nothing Then Inst = PageInstanceLeft.Instance
            If Inst IsNot Nothing Then
                Ver = Inst.Version.Vanilla.ToString()
                Dim J As String = If(Inst.JsonVersion Is Nothing, "", Inst.JsonVersion.ToString())
                If J.Contains("forge") Then Loader = "forge"
                If J.Contains("fabric") Then Loader = "fabric"
            End If
        Catch ex As Exception
        End Try
        Dim SearchUrl As String = "https://api.modrinth.com/v2/search?limit=1&query=" & Uri.EscapeDataString(Name) & "&facets=" & Uri.EscapeDataString("[[%22project_type:mod%22]]")
        Dim SearchJson As JObject = JObject.Parse(NetRequestByClient(SearchUrl, Timeout:=15000))
        If SearchJson("hits") Is Nothing OrElse CType(SearchJson("hits"), JArray).Count = 0 Then Return False
        Dim Slug As String = SearchJson("hits")(0)("slug").ToString()
        Dim EncLoad As String = Uri.EscapeDataString("[%22" & Loader & "%22]")
        Dim EncVer As String = Uri.EscapeDataString("[%22" & Ver & "%22]")
        Dim Versions As JArray = JArray.Parse(NetRequestByClient($"https://api.modrinth.com/v2/project/{Slug}/version?loaders={EncLoad}&game_versions={EncVer}", Timeout:=15000))
        If Versions.Count = 0 Then Return False
        Dim Files As JArray = TryCast(Versions(0)("files"), JArray)
        If Files Is Nothing OrElse Files.Count = 0 Then Return False
        Dim Url As String = Files(0)("url").ToString()
        Dim FileName As String = Files(0)("filename").ToString()
        If Inst Is Nothing Then Return False
        Dim ModsDir As String = Inst.PathIndie & "mods\"
        DirectoryUtils.Create(ModsDir)
        NetDownloadByLoader(Url, ModsDir & FileName)
        Return True
    End Function

    Private Sub DisableNewest()
        Try
            Dim Inst As McInstance = If(McInstanceSelected, PageInstanceLeft.Instance)
            If Inst Is Nothing Then
                LabStatus.Text = "没有选中的版本。"
                Return
            End If
            Dim ModsDir As String = Inst.PathIndie & "mods\"
            Dim Files As New List(Of String)(IO.Directory.GetFiles(ModsDir, "*.jar"))
            If Files.Count = 0 Then
                LabStatus.Text = "mods 文件夹为空。"
                Return
            End If
            Dim Newest As String = Files.OrderByDescending(Function(f) IO.File.GetLastWriteTime(f)).First()
            IO.File.Move(Newest, Newest & ".disabled")
            LabStatus.Text = "已禁用：" & IO.Path.GetFileName(Newest)
        Catch ex As Exception
            LabStatus.Text = "禁用失败：" & ex.Message
        End Try
    End Sub
End Class
