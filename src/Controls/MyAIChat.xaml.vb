Imports System.Net.Http
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Input
Imports System.Windows.Media

Public Class MyAIChat

    Private Const Endpoint As String = "https://text.pollinations.ai/openai"
    Private Shared ReadOnly SharedHistory As New List(Of (Role As String, Content As String))
    Private IsBusy As Boolean = False
    Private Shared ReadOnly Client As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(90)}

    Private Sub MyAIChat_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If PanChat.Children.Count > 0 Then Return
        If SharedHistory.Count = 0 Then
            AddBubble("你好，我是 AI 助手。可以直接聊天，也可以输入「打开皮肤库」「打开种子地图」这类指令来打开功能。", False)
        Else
            For Each Item In SharedHistory
                AddBubble(Item.Content, Item.Role = "user")
            Next
        End If
    End Sub

    Private Function GetBrush(Name As String, Fallback As Color) As Brush
        Try
            Dim Result As Brush = TryCast(System.Windows.Application.Current.TryFindResource(Name), Brush)
            If Result IsNot Nothing Then Return Result
        Catch ex As Exception
        End Try
        Return New SolidColorBrush(Fallback)
    End Function

    Private Function AddBubble(Content As String, IsUser As Boolean) As TextBlock
        Dim Bubble As New Border With {
            .CornerRadius = New CornerRadius(10),
            .Padding = New Thickness(12, 8, 12, 8),
            .Margin = New Thickness(0, 0, 0, 10),
            .MaxWidth = 540,
            .HorizontalAlignment = If(IsUser, HorizontalAlignment.Right, HorizontalAlignment.Left)}
        If IsUser Then
            Bubble.Background = GetBrush("ColorBrush3", Color.FromRgb(59, 130, 246))
        Else
            Bubble.Background = GetBrush("ColorBrushGray8", Color.FromRgb(247, 248, 250))
        End If
        Dim TextBlock As New TextBlock With {
            .Text = Content,
            .TextWrapping = TextWrapping.Wrap,
            .FontSize = 13,
            .LineHeight = 21,
            .Foreground = If(IsUser, Brushes.White, GetBrush("ColorBrush1", Color.FromRgb(31, 39, 51)))}
        Bubble.Child = TextBlock
        PanChat.Children.Add(Bubble)
        PanChatScroll.ScrollToEnd()
        Return TextBlock
    End Function

    Private Sub BtnClear_Click(sender As Object, e As EventArgs) Handles BtnClear.Click
        SharedHistory.Clear()
        PanChat.Children.Clear()
        AddBubble("对话已清空，继续聊吧。", False)
    End Sub

    Private Sub BtnSend_Click(sender As Object, e As EventArgs) Handles BtnSend.Click
        SendMessage()
    End Sub

    Private Sub TextInput_KeyDown(sender As Object, e As KeyEventArgs) Handles TextInput.KeyDown
        If e.Key = Key.Enter Then
            e.Handled = True
            SendMessage()
        End If
    End Sub

    Private Sub SendMessage()
        If IsBusy Then
            Hint("请等待上一条回复完成", HintType.Blue)
            Return
        End If
        Dim Prompt As String = If(TextInput.Text, "").Trim()
        If String.IsNullOrWhiteSpace(Prompt) Then Return
        TextInput.Text = ""
        AddBubble(Prompt, True)
        SharedHistory.Add(("user", Prompt))

        Dim FeatureMessage As String = Nothing
        If TryOpenFeature(Prompt, FeatureMessage) Then
            SharedHistory.Add(("assistant", FeatureMessage))
            AddBubble(FeatureMessage, False)
            Return
        End If

        IsBusy = True
        Dim Placeholder As TextBlock = AddBubble("正在思考……", False)
        RunInNewThread(
            Sub()
                Dim Reply As String
                Try
                    Reply = CallAI()
                Catch ex As Exception
                    Reply = "请求失败：" & ex.Message
                    If Reply.Contains("402") OrElse Reply.Contains("FREE_LIMIT") OrElse Reply.Contains("429") Then
                        Reply = "免费 AI 接口每 45 秒只能回复 1 条，请稍等一会儿再发。"
                    End If
                End Try
                RunInUi(
                    Sub()
                        Placeholder.Text = Reply
                        SharedHistory.Add(("assistant", Reply))
                        If SharedHistory.Count > 200 Then SharedHistory.RemoveRange(0, SharedHistory.Count - 200)
                        IsBusy = False
                        PanChatScroll.ScrollToEnd()
                    End Sub)
            End Sub, "AI Chat")
    End Sub

    Private Function CallAI() As String
        Dim Messages As New JArray
        Dim SystemMessage As New JObject
        SystemMessage("role") = "system"
        SystemMessage("content") = "你是 PCL-ForK 启动器内置的 AI 助手，请用中文回答。不要声称自己是 ChatGPT、OpenAI 或 GPT，也不要提及底层模型或厂商。"
        Messages.Add(SystemMessage)
        For Each Item In SharedHistory.Skip(Math.Max(0, SharedHistory.Count - 20))
            Dim MsgJson As New JObject
            MsgJson("role") = Item.Role
            MsgJson("content") = Item.Content
            Messages.Add(MsgJson)
        Next
        Dim Proxy As String = Settings.Get(Of String)("DeepSeekProxy")
        Dim Key As String = ModAutoTranslate.GetDeepSeekKey()
        If Not String.IsNullOrWhiteSpace(Proxy) OrElse Not String.IsNullOrWhiteSpace(Key) Then
            Try
                Dim Url As String = If(String.IsNullOrWhiteSpace(Proxy), "https://api.deepseek.com/chat/completions", Proxy.Trim())
                Return CallChatApi(Url, "deepseek-chat", If(String.IsNullOrWhiteSpace(Proxy), Key, ""), Messages)
            Catch ex As Exception
                Logger.Warn(ex, "DeepSeek 对话失败，回退免费接口")
            End Try
        End If
        Return CallChatApi(Endpoint, "openai", "", Messages)
    End Function

    Private Function CallChatApi(Url As String, Model As String, Key As String, Messages As JArray) As String
        Dim Body As New JObject
        Body("model") = Model
        Body("messages") = Messages
        Body("stream") = False
        Using Request As New HttpRequestMessage(HttpMethod.Post, Url)
            Request.Content = New StringContent(Body.ToString(), Encoding.UTF8, "application/json")
            Request.Headers.UserAgent.ParseAdd("PCL-ForK")
            If Not String.IsNullOrWhiteSpace(Key) Then Request.Headers.TryAddWithoutValidation("Authorization", "Bearer " & Key.Trim())
            Using Response As HttpResponseMessage = Client.SendAsync(Request).Result
                Dim Text As String = Response.Content.ReadAsStringAsync().Result
                If Not Response.IsSuccessStatusCode Then
                    Throw New Exception($"HTTP {CInt(Response.StatusCode)}：{If(Text.Length > 240, Text.Substring(0, 240), Text)}")
                End If
                Dim Json As JObject = JObject.Parse(Text)
                If Json("choices") IsNot Nothing AndAlso CType(Json("choices"), JArray).Count > 0 Then
                    Dim MessageToken As JToken = Json("choices")(0)("message")
                    If MessageToken IsNot Nothing Then
                        Dim Content As String = If(MessageToken("content") Is Nothing, "", MessageToken("content").ToString())
                        If Not String.IsNullOrWhiteSpace(Content) Then Return Content.Trim()
                    End If
                    If Json("choices")(0)("text") IsNot Nothing Then Return Json("choices")(0)("text").ToString().Trim()
                End If
                If Json("error") IsNot Nothing Then Throw New Exception(Json("error").ToString())
                Return Text
            End Using
        End Using
    End Function

    Private Function TryOpenFeature(Prompt As String, ByRef Message As String) As Boolean
        Dim Text As String = Prompt.Trim().Replace(" ", "").Replace("　", "")
        Dim MatchFeature As Match = Regex.Match(Text, "^(打开|帮我打开|进入|跳转到|去|启动)(.+)$")
        Dim IsCommand As Boolean = MatchFeature.Success
        Dim Keyword As String = If(IsCommand, MatchFeature.Groups(2).Value, Text)
        If Not IsCommand Then
            Select Case Text
                Case "皮肤库", "投影查看", "投影", "种子地图", "自动汉化", "文件编辑器", "Mod开发", "mod开发", "百宝箱", "联机", "下载", "设置", "工具", "启动", "主页"
                    Keyword = Text
                Case Else
                    Return False
            End Select
        End If
        If True Then
            Select Case True
                Case Keyword.Contains("种子地图")
                    If McInstanceSelected Is Nothing Then
                        Message = "请先在启动页选择一个 Minecraft 版本，再打开种子地图。"
                        Return True
                    End If
                    Return Navigate(FormMain.PageType.InstanceSetup, FormMain.PageSubType.InstanceSeed, "已为你打开种子地图", Message)
                Case Keyword.Contains("自动汉化") OrElse Keyword.Contains("汉化")
                    If McInstanceSelected Is Nothing Then
                        Message = "请先在启动页选择一个 Minecraft 版本。"
                        Return True
                    End If
                    Return Navigate(FormMain.PageType.InstanceSetup, FormMain.PageSubType.InstanceTranslate, "已为你打开自动 Mod 汉化", Message)
                Case Keyword.Contains("文件编辑") OrElse Keyword.Contains("版本文件")
                    If McInstanceSelected Is Nothing Then
                        Message = "请先在启动页选择一个 Minecraft 版本。"
                        Return True
                    End If
                    Return Navigate(FormMain.PageType.InstanceSetup, FormMain.PageSubType.InstanceFile, "已为你打开版本文件编辑器", Message)
                Case Keyword.Contains("皮肤库")
                    Return Navigate(FormMain.PageType.Other, FormMain.PageSubType.OtherSkin, "已为你打开皮肤库", Message)
                Case Keyword.Contains("投影")
                    Return Navigate(FormMain.PageType.Other, FormMain.PageSubType.OtherLitematic, "已为你打开投影查看", Message)
                Case Keyword.Contains("Mod开发") OrElse Keyword.Contains("mod开发")
                    Return Navigate(FormMain.PageType.Other, FormMain.PageSubType.OtherModdev, "已为你打开 Mod 开发", Message)
                Case Keyword.Contains("百宝箱")
                    Return Navigate(FormMain.PageType.Other, FormMain.PageSubType.OtherTest, "已为你打开百宝箱", Message)
                Case Keyword.Contains("联机")
                    Return Navigate(FormMain.PageType.Link, FormMain.PageSubType.Default, "已为你打开联机页", Message)
                Case Keyword.Contains("下载")
                    Return Navigate(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall, "已为你打开下载页", Message)
                Case Keyword.Contains("设置")
                    Return Navigate(FormMain.PageType.Setup, FormMain.PageSubType.SetupLaunch, "已为你打开设置页", Message)
                Case Keyword.Contains("工具")
                    Return Navigate(FormMain.PageType.Other, FormMain.PageSubType.OtherAbout, "已为你打开工具页", Message)
                Case Keyword.Contains("启动") OrElse Keyword.Contains("主页")
                    Return Navigate(FormMain.PageType.Launch, FormMain.PageSubType.Default, "已为你返回启动页", Message)
            End Select
        End If
        Return False
    End Function

    Private Function Navigate(Page As FormMain.PageType, SubType As FormMain.PageSubType, SuccessText As String, ByRef Message As String) As Boolean
        Try
            FrmMain.PageChange(New FormMain.PageStackData With {.Page = Page}, SubType)
            Message = SuccessText
            Return True
        Catch ex As Exception
            Message = "打开功能失败：" & ex.Message
            Logger.Warn(ex, "AI 助手打开功能失败")
            Return True
        End Try
    End Function

End Class
