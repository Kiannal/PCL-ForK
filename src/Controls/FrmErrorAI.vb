Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Threading.Tasks

Public Class FrmErrorAI
    Inherits Window

    Private ReadOnly Ex As Exception
    Private ReadOnly LabAnalysis As New TextBlock With {.TextWrapping = TextWrapping.Wrap, .FontSize = 12.5, .Foreground = New SolidColorBrush(Color.FromRgb(200, 205, 212)), .Margin = New Thickness(0, 8, 0, 0)}
    Private ReadOnly BtnFix As New Button With {.Content = "清理缓存并重启", .Height = 30, .Margin = New Thickness(0, 0, 10, 0)}
    Private ReadOnly BtnCopy As New Button With {.Content = "复制报错", .Height = 30, .Margin = New Thickness(0, 0, 10, 0)}
    Private ReadOnly BtnLog As New Button With {.Content = "打开日志", .Height = 30, .Margin = New Thickness(0, 0, 10, 0)}
    Private ReadOnly BtnContinue As New Button With {.Content = "继续运行", .Height = 30, .Margin = New Thickness(0, 0, 10, 0)}
    Private ReadOnly BtnExit As New Button With {.Content = "退出启动器", .Height = 30}
    Public ResultContinue As Boolean = False

    Public Sub New(ex As Exception)
        Me.Ex = ex
        Title = "AI 错误诊断"
        Width = 760
        Height = 620
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
        Header.Children.Add(New TextBlock With {.Text = "启动器发生错误", .FontSize = 22, .FontWeight = FontWeights.SemiBold, .Foreground = Brushes.White})
        Header.Children.Add(New TextBlock With {.Text = "DeepSeek 正在分析错误日志并给出处理建议……", .FontSize = 12, .Margin = New Thickness(0, 4, 0, 0), .Foreground = New SolidColorBrush(Color.FromRgb(150, 162, 176))})
        Root.Children.Add(Header)

        Dim Info As New TextBox With {.Text = Ex.ToString(), .IsReadOnly = True, .TextWrapping = TextWrapping.Wrap, .VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .Margin = New Thickness(0, 12, 0, 8), .FontSize = 11.5, .Background = New SolidColorBrush(Color.FromRgb(30, 32, 37)), .Foreground = New SolidColorBrush(Color.FromRgb(210, 214, 220))}
        Grid.SetRow(Info, 1)
        Dim Stack As New Grid
        Stack.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
        Stack.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Stack.Children.Add(Info)
        Grid.SetRow(LabAnalysis, 1)
        Stack.Children.Add(LabAnalysis)
        Grid.SetRow(Stack, 1)
        Root.Children.Add(Stack)

        Dim Bar As New StackPanel With {.Orientation = Orientation.Horizontal, .HorizontalAlignment = HorizontalAlignment.Right, .Margin = New Thickness(0, 12, 0, 0)}
        AddHandler BtnFix.Click, Sub() AutoFix()
        AddHandler BtnCopy.Click, Sub()
                                       Try
                                           Clipboard.SetText(Ex.ToString())
                                       Catch ex2 As Exception
                                       End Try
                                   End Sub
        AddHandler BtnLog.Click, Sub()
                                      Try
                                          OpenExplorer(PathTemp)
                                      Catch ex2 As Exception
                                      End Try
                                  End Sub
        AddHandler BtnContinue.Click, Sub()
                                          ResultContinue = True
                                          Close()
                                      End Sub
        AddHandler BtnExit.Click, Sub()
                                      ResultContinue = False
                                      Close()
                                  End Sub
        Bar.Children.Add(BtnFix)
        Bar.Children.Add(BtnCopy)
        Bar.Children.Add(BtnLog)
        Bar.Children.Add(BtnContinue)
        Bar.Children.Add(BtnExit)
        Grid.SetRow(Bar, 2)
        Root.Children.Add(Bar)
        Return Root
    End Function

    Private Sub Analyze()
        Dim Detail As String = Ex.ToString()
        Task.Run(Sub()
                     Dim Text As String = ""
                     Try
                         Text = ModAutoTranslate.AskDeepSeek("你是启动器故障诊断助手。根据以下错误日志，用简体中文简短说明原因（不超过 120 字），最后单独一行给出建议操作（如：清理缓存 / 重新登录 / 更新 Java / 删除冲突 Mod）。只输出这两部分。" & vbCrLf & Detail)
                     Catch ex2 As Exception
                     End Try
                     If String.IsNullOrWhiteSpace(Text) Then Text = "AI 分析失败，可查看下方原始日志或清理缓存后重试。"
                     Dispatcher.Invoke(Sub() LabAnalysis.Text = Text)
                 End Sub)
    End Sub

    Private Sub AutoFix()
        Try
            Dim CacheDir As String = PathPure.Value & "Cache"
            If IO.Directory.Exists(CacheDir) Then IO.Directory.Delete(CacheDir, True)
            IO.Directory.CreateDirectory(CacheDir)
            LabAnalysis.Text = "已清理启动器缓存。点击「继续运行」返回，或「退出启动器」后重新打开。"
        Catch ex2 As Exception
            LabAnalysis.Text = "清理缓存失败：" & ex2.Message
        End Try
    End Sub
End Class
