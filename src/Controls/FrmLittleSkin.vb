Imports System.Net.Http
Imports System.Text

Public Class FrmLittleSkin
    Inherits Window

    Private Const AuthUrl As String = "https://littleskin.cn/api/yggdrasil/authserver/authenticate"
    Private Const ServerBase As String = "https://littleskin.cn/api/yggdrasil"
    Private Const RegisterUrl As String = "https://littleskin.cn/auth/register"

    Private ReadOnly TextEmail As New TextBox
    Private ReadOnly TextPass As New PasswordBox
    Private ReadOnly BtnLogin As New Button
    Private ReadOnly BtnUse As New Button
    Private ReadOnly ComboProfile As New ComboBox
    Private ReadOnly LabStatus As New TextBlock
    Private ReadOnly LabProfile As New TextBlock
    Private ReadOnly Profiles As New List(Of (Id As String, Name As String))
    Private IsLoggingIn As Boolean = False

    Public Sub New()
        Title = "LittleSkin 登录"
        Width = 480
        SizeToContent = SizeToContent.Height
        ResizeMode = ResizeMode.NoResize
        WindowStartupLocation = WindowStartupLocation.CenterScreen
        Background = New SolidColorBrush(Color.FromRgb(248, 249, 251))

        Dim Root As New StackPanel With {.Margin = New Thickness(20)}

        Root.Children.Add(New TextBlock With {
            .Text = "使用 LittleSkin 账号登录后，会把当前版本的登录方式设为 LittleSkin（Authlib-Injector），启动游戏时即可使用该账号并显示皮肤。",
            .TextWrapping = TextWrapping.Wrap,
            .Foreground = New SolidColorBrush(Color.FromRgb(90, 96, 104)),
            .FontSize = 12,
            .Margin = New Thickness(0, 0, 0, 14)})

        Root.Children.Add(MakeRow("邮箱", TextEmail))
        Root.Children.Add(MakeRow("密码", TextPass))

        Dim LoginRow As New Grid With {.Margin = New Thickness(0, 12, 0, 0)}
        LoginRow.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(70)})
        LoginRow.ColumnDefinitions.Add(New ColumnDefinition)
        BtnLogin.Content = "登录"
        BtnLogin.Padding = New Thickness(18, 4, 18, 4)
        BtnLogin.HorizontalAlignment = HorizontalAlignment.Left
        BtnLogin.FontSize = 12
        Grid.SetColumn(BtnLogin, 1)
        LoginRow.Children.Add(BtnLogin)
        Root.Children.Add(LoginRow)

        LabProfile.Text = "角色"
        LabProfile.VerticalAlignment = VerticalAlignment.Center
        LabProfile.FontSize = 12
        LabProfile.Visibility = Visibility.Collapsed
        Root.Children.Add(LabProfile)

        ComboProfile.Margin = New Thickness(0, 8, 0, 0)
        ComboProfile.FontSize = 12
        ComboProfile.Visibility = Visibility.Collapsed
        Root.Children.Add(ComboProfile)

        LabStatus.TextWrapping = TextWrapping.Wrap
        LabStatus.FontSize = 12
        LabStatus.Margin = New Thickness(0, 10, 0, 0)
        LabStatus.Foreground = New SolidColorBrush(Color.FromRgb(120, 126, 134))
        Root.Children.Add(LabStatus)

        Dim BtnRow As New WrapPanel With {.Margin = New Thickness(0, 16, 0, 0), .HorizontalAlignment = HorizontalAlignment.Right}
        BtnUse.Content = "使用此账号"
        BtnUse.Padding = New Thickness(16, 4, 16, 4)
        BtnUse.FontSize = 12
        BtnUse.Margin = New Thickness(0, 0, 10, 0)
        BtnUse.IsEnabled = False
        BtnRow.Children.Add(BtnUse)
        Dim BtnRegister As New Button With {.Content = "打开注册页", .Padding = New Thickness(16, 4, 16, 4), .FontSize = 12, .Margin = New Thickness(0, 0, 10, 0)}
        BtnRow.Children.Add(BtnRegister)
        Dim BtnClose As New Button With {.Content = "关闭", .Padding = New Thickness(16, 4, 16, 4), .FontSize = 12}
        BtnRow.Children.Add(BtnClose)
        Root.Children.Add(BtnRow)

        Content = Root

        AddHandler BtnLogin.Click, AddressOf LoginClick
        AddHandler BtnUse.Click, AddressOf UseClick
        AddHandler BtnRegister.Click, Sub() OpenWebsite(RegisterUrl)
        AddHandler BtnClose.Click, Sub() Close()
    End Sub

    Private Function MakeRow(Label As String, Box As Control) As Grid
        Dim Row As New Grid With {.Margin = New Thickness(0, 0, 0, 8)}
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(70)})
        Row.ColumnDefinitions.Add(New ColumnDefinition)
        Dim Text As New TextBlock With {.Text = Label, .VerticalAlignment = VerticalAlignment.Center, .FontSize = 12}
        Row.Children.Add(Text)
        Box.FontSize = 12
        Box.Margin = New Thickness(0, 0, 0, 0)
        Grid.SetColumn(Box, 1)
        Row.Children.Add(Box)
        Return Row
    End Function

    Private Sub LoginClick()
        If IsLoggingIn Then Return
        Dim Email As String = TextEmail.Text.Trim()
        Dim Password As String = TextPass.Password
        If Email = "" Then
            LabStatus.Text = "邮箱不能为空"
            Return
        End If
        If Password = "" Then
            LabStatus.Text = "密码不能为空"
            Return
        End If
        IsLoggingIn = True
        BtnLogin.IsEnabled = False
        BtnUse.IsEnabled = False
        LabStatus.Text = "正在登录……"
        RunInNewThread(
            Sub()
                Try
                    Dim Body As New JObject
                    Body("username") = Email
                    Body("password") = Password
                    Body("clientToken") = Guid.NewGuid().ToString()
                    Body("requestUser") = True
                    Dim Agent As New JObject
                    Agent("name") = "Minecraft"
                    Agent("version") = 1
                    Body("agent") = Agent
                    Dim Result As String = NetRequestByClient(AuthUrl, HttpMethod.Post, Body, "application/json", RequireJson:=True, Timeout:=25000)
                    Dim Json As JObject = JObject.Parse(Result)
                    Dim NewProfiles As New List(Of (Id As String, Name As String))
                    Dim SelectedName As String = ""
                    If Json("selectedProfile") IsNot Nothing Then SelectedName = Json("selectedProfile")("name").ToString()
                    If Json("availableProfiles") IsNot Nothing Then
                        For Each Profile As JToken In DirectCast(Json("availableProfiles"), JArray)
                            NewProfiles.Add((Profile("id").ToString(), Profile("name").ToString()))
                        Next
                    End If
                    If NewProfiles.Count = 0 AndAlso SelectedName <> "" Then NewProfiles.Add(("", SelectedName))
                    RunInUi(
                        Sub()
                            Profiles.Clear()
                            Profiles.AddRange(NewProfiles)
                            ComboProfile.Items.Clear()
                            For Each Profile In Profiles
                                ComboProfile.Items.Add(Profile.Name)
                            Next
                            If Profiles.Count > 0 Then
                                Dim SelectedIndex As Integer = Profiles.FindIndex(Function(P) P.Name = SelectedName)
                                ComboProfile.SelectedIndex = If(SelectedIndex >= 0, SelectedIndex, 0)
                                LabProfile.Visibility = Visibility.Visible
                                ComboProfile.Visibility = Visibility.Visible
                                BtnUse.IsEnabled = True
                                LabStatus.Text = "登录成功，角色：" & Profiles(ComboProfile.SelectedIndex).Name
                            Else
                                LabStatus.Text = "登录成功，但该账号下没有可用角色，请先在 LittleSkin 网站创建角色"
                            End If
                            IsLoggingIn = False
                            BtnLogin.IsEnabled = True
                        End Sub)
                Catch ex As Exception
                    RunInUi(
                        Sub()
                            LabStatus.Text = "登录失败：" & ex.Message
                            If ex.Message.Contains("403") OrElse ex.Message.Contains("401") Then LabStatus.Text = "登录失败：邮箱或密码错误"
                            IsLoggingIn = False
                            BtnLogin.IsEnabled = True
                        End Sub)
                End Try
            End Sub, "LittleSkin Login")
    End Sub

    Private Sub UseClick()
        Try
            Dim Email As String = TextEmail.Text.Trim()
            Dim Password As String = TextPass.Password
            Settings.Set("LoginType", McLoginType.Auth)
            Settings.Set("LoginRemember", True)
            Settings.Set("LoginAuthEmail", Email)
            Settings.Set("LoginAuthPass", Password)
            Settings.Set("CacheAuthServerServer", ServerBase)
            Settings.Set("CacheAuthServerName", "LittleSkin")
            Settings.Set("CacheAuthServerRegister", RegisterUrl)
            If McInstanceSelected IsNot Nothing Then
                Settings.Set("VersionServerLogin", 4, McInstanceSelected)
                Settings.Set("VersionServerAuthServer", ServerBase, McInstanceSelected)
                Settings.Set("VersionServerAuthName", "LittleSkin", McInstanceSelected)
                Settings.Set("VersionServerAuthRegister", RegisterUrl, McInstanceSelected)
            End If
            If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.RefreshPage(False, False)
            Hint("已配置 LittleSkin 登录，回到启动页即可使用该账号启动", HintType.Green)
            Close()
        Catch ex As Exception
            LabStatus.Text = "配置失败：" & ex.Message
            Logger.Warn(ex, "配置 LittleSkin 登录失败")
        End Try
    End Sub

End Class
