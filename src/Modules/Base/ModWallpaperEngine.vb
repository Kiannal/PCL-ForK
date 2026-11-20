Imports System.IO
Imports System.Text
Imports Newtonsoft.Json.Linq

Public Module ModWallpaperEngine

    Public Class WeWallpaperInfo
        Public Property IsVideo As Boolean
        Public Property MediaPath As String
    End Class

    Public Function WeFindFolder() As String
        Try
            For Each ProcName In {"wallpaper64", "wallpaper32"}
                For Each Proc In Diagnostics.Process.GetProcessesByName(ProcName)
                    Try
                        Dim MainPath As String = If(Proc.MainModule Is Nothing, Nothing, Proc.MainModule.FileName)
                        If Not String.IsNullOrEmpty(MainPath) Then
                            Dim Folder As String = IO.Path.GetDirectoryName(MainPath)
                            If IO.File.Exists(IO.Path.Combine(Folder, "config.json")) Then Return Folder
                        End If
                    Catch
                    End Try
                Next
            Next
        Catch
        End Try
        Dim Candidates As New List(Of String)
        For Each Drive In {"C:\", "D:\", "E:\", "F:\", "G:\"}
            Candidates.Add(Drive & "steam\steamapps\common\wallpaper_engine")
            Candidates.Add(Drive & "SteamLibrary\steamapps\common\wallpaper_engine")
            Candidates.Add(Drive & "Program Files (x86)\Steam\steamapps\common\wallpaper_engine")
            Candidates.Add(Drive & "Program Files\Steam\steamapps\common\wallpaper_engine")
        Next
        For Each Candidate In Candidates
            If IO.File.Exists(IO.Path.Combine(Candidate, "config.json")) Then Return Candidate
        Next
        Return Nothing
    End Function

    Public Function WeGetCurrentWallpaper() As WeWallpaperInfo
        Try
            Dim Folder As String = WeFindFolder()
            If String.IsNullOrEmpty(Folder) Then Return Nothing
            Dim ConfigPath As String = IO.Path.Combine(Folder, "config.json")
            If Not IO.File.Exists(ConfigPath) Then Return Nothing
            Dim Json As JObject = JObject.Parse(IO.File.ReadAllText(ConfigPath, Encoding.UTF8))
            Dim Selected As JObject = Nothing
            For Each UserEntry In Json.Properties()
                If UserEntry.Value.Type = JTokenType.Object AndAlso UserEntry.Value("general") IsNot Nothing Then
                    Selected = TryCast(UserEntry.Value.SelectToken("general.wallpaperconfig.selectedwallpapers"), JObject)
                    If Selected IsNot Nothing Then Exit For
                End If
            Next
            If Selected Is Nothing Then Return Nothing
            Dim WallpaperFile As String = ""
            For Each MonitorEntry In Selected.Properties()
                Dim FileToken As JToken = MonitorEntry.Value("file")
                If FileToken IsNot Nothing Then WallpaperFile = FileToken.ToString()
                If WallpaperFile <> "" Then Exit For
            Next
            If WallpaperFile = "" Then Return Nothing
            WallpaperFile = WallpaperFile.Replace("/", "\")
            If Not IO.File.Exists(WallpaperFile) Then Return Nothing
            Dim Extension As String = IO.Path.GetExtension(WallpaperFile).ToLower()
            If {"mp4", "m4v", "wmv", "mov", "avi", "webm"}.Contains(Extension) Then
                Return New WeWallpaperInfo With {.IsVideo = True, .MediaPath = WallpaperFile}
            End If
            Dim WallpaperFolder As String = IO.Path.GetDirectoryName(WallpaperFile)
            If String.Equals(IO.Path.GetFileName(WallpaperFile), "project.json", StringComparison.OrdinalIgnoreCase) Then
                Try
                    Dim ProjectJson As JObject = JObject.Parse(IO.File.ReadAllText(WallpaperFile, Encoding.UTF8))
                    Dim ProjectType As String = If(ProjectJson("type") Is Nothing, "", ProjectJson("type").ToString())
                    Dim ProjectFile As String = If(ProjectJson("file") Is Nothing, "", ProjectJson("file").ToString())
                    If ProjectType = "video" AndAlso ProjectFile <> "" Then
                        Dim VideoPath As String = IO.Path.Combine(WallpaperFolder, ProjectFile)
                        If IO.File.Exists(VideoPath) Then Return New WeWallpaperInfo With {.IsVideo = True, .MediaPath = VideoPath}
                    End If
                Catch
                End Try
            End If
            For Each PreviewName In {"preview.gif", "preview.jpg", "preview.png", "preview.jpeg", "preview.bmp", "preview.webp"}
                Dim PreviewPath As String = IO.Path.Combine(WallpaperFolder, PreviewName)
                If IO.File.Exists(PreviewPath) Then Return New WeWallpaperInfo With {.IsVideo = False, .MediaPath = PreviewPath}
            Next
            Return Nothing
        Catch ex As Exception
            Logger.Warn(ex, "获取 Wallpaper Engine 当前壁纸失败")
            Return Nothing
        End Try
    End Function

    Public Function WeSync(Optional MakeHint As Boolean = False) As Integer
        Try
            Dim Info As WeWallpaperInfo = WeGetCurrentWallpaper()
            If Info Is Nothing Then Return -1
            If Info.IsVideo Then
                Settings.Set("UiBackgroundImage", "")
                Settings.Set("UiBackgroundVideo", Info.MediaPath)
                Settings.Set("UiBackgroundType", 1)
            ElseIf Settings.Get(Of Integer)("UiBackgroundType") = 1 Then
                Return 2
            Else
                Settings.Set("UiBackgroundVideo", "")
                Settings.Set("UiBackgroundImage", Info.MediaPath)
                Settings.Set("UiBackgroundType", 0)
            End If
            If FrmMain IsNot Nothing AndAlso FrmMain.IsLoaded Then
                FrmMain.UpdateBackgroundAndTitleBar()
                PageSetupUI.BackgroundRefresh(False, True)
            End If
            Return If(Info.IsVideo, 1, 0)
        Catch ex As Exception
            Logger.Warn(ex, "同步 Wallpaper Engine 壁纸失败")
            Return -1
        End Try
    End Function

End Module
