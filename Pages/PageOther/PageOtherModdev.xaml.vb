Imports System.IO
Imports System.Diagnostics

Public Class PageOtherModdev

    Private Sub PageOtherModdev_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If String.IsNullOrWhiteSpace(TextFolder.Text) Then
            TextFolder.Text = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PCLModDev")
        End If
        LabFormStatus.Text = "填写信息后点击「生成 Gradle 项目」"
    End Sub

    Private Sub ComboLoader_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboLoader.SelectionChanged
        If TextMcVersion Is Nothing Then Return
        TextMcVersion.Text = ModModDev.GetDefaultMcVersion(GetLoaderTag())
    End Sub

    Private Function GetLoaderTag() As String
        Dim Item As MyComboBoxItem = TryCast(ComboLoader.SelectedItem, MyComboBoxItem)
        If Item Is Nothing OrElse Item.Tag Is Nothing Then Return "forge"
        Return Item.Tag.ToString()
    End Function

    Private Sub BtnBrowse_Click(sender As Object, e As EventArgs) Handles BtnBrowse.Click
        Dim Folder As String = Dialogs.SelectFolder("选择项目位置", False).FirstOrDefault()
        If Not String.IsNullOrWhiteSpace(Folder) Then TextFolder.Text = Folder
    End Sub

    Private Sub BtnGenerate_Click(sender As Object, e As EventArgs) Handles BtnGenerate.Click
        Dim ModId As String = If(TextModId.Text, "").Trim().ToLower()
        Dim ProjectFolder As String = ModModDev.GetProjectFolder(TextFolder.Text, ModId)
        Try
            If Directory.Exists(ProjectFolder) AndAlso Directory.GetFileSystemEntries(ProjectFolder).Length > 0 Then
                If MyMsgBox($"文件夹 {ProjectFolder} 已经存在且不为空，是否继续写入？", "生成项目", "继续", "取消") <> 1 Then Return
            End If
            ProjectFolder = ModModDev.Generate(ModId,
                                               If(TextModName.Text, "").Trim(),
                                               If(TextPackage.Text, "").Trim(),
                                               If(TextModVersion.Text, "").Trim(),
                                               If(TextAuthor.Text, "").Trim(),
                                               GetLoaderTag(),
                                               If(TextMcVersion.Text, "").Trim(),
                                               If(TextFolder.Text, "").Trim())
            LabFormStatus.Foreground = New Media.SolidColorBrush(Media.Color.FromRgb(52, 211, 153))
            LabFormStatus.Text = "已生成：" & ProjectFolder
            LabOutput.Text = "项目已生成到：" & ProjectFolder & vbCrLf &
                "1. 用 IntelliJ IDEA 打开这个文件夹；" & vbCrLf &
                "2. 等待 Gradle 同步（首次会下载依赖，需要网络）；" & vbCrLf &
                "3. 运行 runClient 调试，build 打包，产物在 build/libs。"
            Hint("Mod 项目已生成", HintType.Green)
        Catch ex As Exception
            LabFormStatus.Foreground = New Media.SolidColorBrush(Media.Color.FromRgb(248, 113, 113))
            LabFormStatus.Text = "生成失败：" & ex.Message
            Logger.Warn(ex, "生成 Mod 开发项目失败")
        End Try
    End Sub

    Private Sub BtnCheckEnv_Click(sender As Object, e As EventArgs) Handles BtnCheckEnv.Click
        LabEnv.Text = "正在检测……"
        RunInNewThread(
        Sub()
            Dim Report As String = ModModDev.EnvReport()
            RunInUi(Sub() LabEnv.Text = Report)
        End Sub, "ModDev Env Check")
    End Sub

    Private Sub BtnOpenFolder_Click(sender As Object, e As EventArgs) Handles BtnOpenFolder.Click
        Dim Folder As String = ModModDev.GetProjectFolder(TextFolder.Text, TextModId.Text)
        If String.IsNullOrWhiteSpace(Folder) OrElse Not Directory.Exists(Folder) Then
            Hint("项目文件夹还没有生成", HintType.Red)
            Return
        End If
        OpenExplorer(Folder)
    End Sub

    Private Sub BtnOpenCmd_Click(sender As Object, e As EventArgs) Handles BtnOpenCmd.Click
        Dim Folder As String = ModModDev.GetProjectFolder(TextFolder.Text, TextModId.Text)
        If String.IsNullOrWhiteSpace(Folder) OrElse Not Directory.Exists(Folder) Then
            Hint("请先生成项目，或选择已存在的项目文件夹", HintType.Red)
            Return
        End If
        Process.Start(New ProcessStartInfo("cmd.exe", "/k cd /d """ & Folder & """") With {.UseShellExecute = True})
    End Sub

    Private Sub BtnCopyCmd_Click(sender As Object, e As EventArgs) Handles BtnCopyCmd.Click
        ClipboardSet("gradle build")
        Hint("已复制构建命令：gradle build", HintType.Blue)
    End Sub

    Private Sub BtnOpenJava_Click(sender As Object, e As EventArgs) Handles BtnOpenJava.Click
        OpenWebsite("https://adoptium.net/zh-CN/temurin/releases/")
    End Sub

    Private Sub BtnOpenGradle_Click(sender As Object, e As EventArgs) Handles BtnOpenGradle.Click
        OpenWebsite("https://gradle.org/releases/")
    End Sub

    Private Sub BtnOpenIde_Click(sender As Object, e As EventArgs) Handles BtnOpenIde.Click
        OpenWebsite("https://www.jetbrains.com/idea/download/")
    End Sub

    Private Sub BtnDocForge_Click(sender As Object, e As EventArgs) Handles BtnDocForge.Click
        OpenWebsite("https://files.minecraftforge.net/net/minecraftforge/forge/")
    End Sub

    Private Sub BtnDocNeoForge_Click(sender As Object, e As EventArgs) Handles BtnDocNeoForge.Click
        OpenWebsite("https://neoforged.net/")
    End Sub

    Private Sub BtnDocFabric_Click(sender As Object, e As EventArgs) Handles BtnDocFabric.Click
        OpenWebsite("https://fabricmc.net/develop/template/")
    End Sub

    Private Sub BtnDocParchment_Click(sender As Object, e As EventArgs) Handles BtnDocParchment.Click
        OpenWebsite("https://parchmentmc.org/docs/getting-started")
    End Sub

    Private Sub BtnDocMixin_Click(sender As Object, e As EventArgs) Handles BtnDocMixin.Click
        OpenWebsite("https://github.com/SpongePowered/Mixin/wiki")
    End Sub

    Private Sub BtnDocWiki_Click(sender As Object, e As EventArgs) Handles BtnDocWiki.Click
        OpenWebsite("https://minecraft.wiki/w/Java_Edition_protocol")
    End Sub

    Private Sub BtnDocMappings_Click(sender As Object, e As EventArgs) Handles BtnDocMappings.Click
        OpenWebsite("https://minecraft.wiki/w/Obfuscation_map")
    End Sub

End Class
