Imports System.Windows
Imports System.Windows.Controls
Imports System.Threading
Imports ModPcGUI

''' <summary>网易中国版（ES Tool 内核）页面：全部使用启动器原生 UI，不启动外部程序。</summary>
Public Class PageOtherNetEase

    Private Inited As Boolean = False
    Private Accounts As New List(Of String)
    Private CommandButtons As New List(Of Tuple(Of Object, MyButton))

    Private Sub PageOtherNetEase_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If Inited Then Return
        Inited = True
        AddHandler Term.OnOutput, AddressOf OnTermOutput
        AddHandler Term.OnClear, AddressOf OnTermClear
        AddHandler Term.OnWaiting, AddressOf OnTermWaiting
        BuildCommandList()
        InitCore()
    End Sub

#Region "初始化"

    Private Sub InitCore()
        LabAccount.Text = "正在释放网易中国版组件……"
        RunInNewThread(
            Sub()
                Try
                    If Not ModNeteaseTool.EnsureTool(Sub(t As String) RunInUi(Sub() LabAccount.Text = t)) Then
                        RunInUi(Sub() LabAccount.Text = "组件释放失败，请查看启动器日志。")
                        Return
                    End If
                    AppHost.BaseDir = ModNeteaseTool.ToolFolder
                    RunInUi(Sub() LabAccount.Text = "正在初始化核心……")
                    AppHost.Init()
                    RunInUi(Sub()
                                ReloadAccounts()
                                LabAccount.Text = "核心已就绪。选择账号后点「登录」，或使用「离线模式」。"
                            End Sub)
                Catch ex As Exception
                    Logger.Error(ex, "初始化网易中国版失败")
                    RunInUi(Sub() LabAccount.Text = "初始化失败：" & ex.Message)
                End Try
            End Sub, "NetEase Init")
    End Sub

#End Region

#Region "账号"

    Private Sub ReloadAccounts()
        Try
            Accounts = AppHost.ListAccounts()
            ComboAccounts.Items.Clear()
            For Each Item In Accounts
                ComboAccounts.Items.Add(IO.Path.GetFileNameWithoutExtension(Item))
            Next
            If ComboAccounts.Items.Count > 0 Then ComboAccounts.SelectedIndex = 0
            LabAccount.Text = If(Accounts.Count > 0,
                                 $"已载入 {Accounts.Count} 个账号。",
                                 "还没有账号，点「添加账号」导入。")
        Catch ex As Exception
            LabAccount.Text = "读取账号失败：" & ex.Message
        End Try
    End Sub

    Private Sub BtnReloadAccounts_Click() Handles BtnReloadAccounts.Click
        ReloadAccounts()
    End Sub

    Private Sub BtnLogin_Click() Handles BtnLogin.Click
        If ComboAccounts.SelectedIndex < 0 OrElse ComboAccounts.SelectedIndex >= Accounts.Count Then
            Hint("请先选择一个账号", HintType.Red)
            Return
        End If
        Dim Target As String = Accounts(ComboAccounts.SelectedIndex)
        LabAccount.Text = "正在登录……"
        RunInNewThread(
            Sub()
                Try
                    AppHost.Login(Target, "auto")
                Catch ex As Exception
                    AppendLog("登录异常：" & ex.Message & vbCrLf)
                End Try
            End Sub, "NetEase Login")
    End Sub

    Private Sub BtnAddAccount_Click() Handles BtnAddAccount.Click
        Dim Text As String = MyMsgBoxInput("添加网易账号",
            "粘贴凭据：" & vbCrLf &
            "· 电脑版：{...} JSON" & vbCrLf &
            "· 手机版：ey... 开头" & vbCrLf &
            "· 4399：SDK4399... 开头", "")
        If String.IsNullOrWhiteSpace(Text) Then Return
        Try
            Dim Name As String = MyMsgBoxInput("账号名称", "给这个账号起个名字，留空则自动命名：", "")
            If Name Is Nothing Then Return
            If String.IsNullOrWhiteSpace(Name) Then Name = "账号" & (Accounts.Count + 1)
            AppHost.AddAccount(Name.Trim(), Text.Trim())
            ReloadAccounts()
            Hint("账号已添加", HintType.Green)
        Catch ex As Exception
            Hint("添加失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub BtnOffline_Click() Handles BtnOffline.Click
        Try
            AppHost.Offline()
            AppendLog("已切换到离线模式。" & vbCrLf)
        Catch ex As Exception
            AppendLog("离线模式失败：" & ex.Message & vbCrLf)
        End Try
    End Sub

#End Region

#Region "命令"

    Private Sub BuildCommandList()
        PanCommands.Children.Clear()
        CommandButtons.Clear()
        Try
            Dim Groups = Commands.All.GroupBy(Function(c) c.Category)
            For Each Group In Groups
                Dim Header As New TextBlock With {
                    .Text = Group.Key,
                    .FontWeight = FontWeights.SemiBold,
                    .Margin = New Thickness(4, 10, 0, 4),
                    .Foreground = New SolidColorBrush(Color.FromRgb(140, 148, 160))
                }
                PanCommands.Children.Add(Header)
                For Each Cmd In Group
                    Dim Btn As New MyButton With {
                        .Text = Cmd.Label & "（" & Cmd.Name & "）",
                        .Height = 30,
                        .MinWidth = 280,
                        .HorizontalAlignment = HorizontalAlignment.Stretch,
                        .Margin = New Thickness(0, 0, 0, 6)
                    }
                    Dim Local As Object = Cmd
                    AddHandler Btn.Click, Sub()
                                              Try
                                                  Dim Params As New List(Of String)
                                                  For Each Prompt In Local.Prompts
                                                      Dim Value As String = MyMsgBoxInput("参数", Prompt, "")
                                                      If Value Is Nothing Then Return
                                                      Params.Add(Value)
                                                  Next
                                                  Dim Line As String = Local.Name
                                                  If Params.Count > 0 Then Line &= " " & String.Join(" ", Params)
                                                  Term.Submit(Line)
                                                  AppendLog("> " & Line & vbCrLf)
                                              Catch ex As Exception
                                                  AppendLog("发送命令失败：" & ex.Message & vbCrLf)
                                              End Try
                                          End Sub
                    PanCommands.Children.Add(Btn)
                    CommandButtons.Add(New Tuple(Of Object, MyButton)(Cmd, Btn))
                Next
            Next
        Catch ex As Exception
            AppendLog("载入命令列表失败：" & ex.Message & vbCrLf)
        End Try
    End Sub

#End Region

#Region "输入输出"

    Private Sub BtnSend_Click() Handles BtnSend.Click
        Dim Line As String = TextInput.Text
        If String.IsNullOrWhiteSpace(Line) Then Return
        Term.Submit(Line.Trim())
        AppendLog("> " & Line.Trim() & vbCrLf)
        TextInput.Text = ""
    End Sub

    Private Sub BtnClear_Click() Handles BtnClear.Click
        TextLog.Text = ""
    End Sub

    Private Sub AppendLog(Text As String)
        RunInUi(Sub()
                    TextLog.AppendText(Text)
                    TextLog.ScrollToEnd()
                End Sub)
    End Sub

    Private Sub OnTermOutput(Text As String, NewLine As Boolean, Color As ConsoleColor)
        AppendLog(If(NewLine, Text & vbCrLf, Text))
    End Sub

    Private Sub OnTermClear()
        RunInUi(Sub() TextLog.Text = "")
    End Sub

    Private Sub OnTermWaiting(Waiting As Boolean)
        RunInUi(Sub() BtnSend.IsEnabled = True)
    End Sub

#End Region

End Class
