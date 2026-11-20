Imports System.Diagnostics
Imports System.Threading
Imports Newtonsoft.Json.Linq

Public Module ModTerracotta

    Public Function IsInstalled() As Boolean
        Try
            Dim Folder As String = PathPure.Value & "Terracotta\"
            If Not IO.Directory.Exists(Folder) Then Return False
            If IO.File.Exists(IO.Path.Combine(Folder, "terracotta.exe")) Then Return True
            Return IO.Directory.GetFiles(Folder, "*.exe", IO.SearchOption.AllDirectories).Any()
        Catch ex As Exception
            Return False
        End Try
    End Function

    Public Function EnsureExecutable() As String
        Dim Folder As String = PathPure.Value & "Terracotta\"
        DirectoryUtils.Create(Folder)
        Dim ExePath As String = IO.Path.Combine(Folder, "terracotta.exe")
        If IO.File.Exists(ExePath) Then Return ExePath
        Dim Found As String = IO.Directory.GetFiles(Folder, "*.exe", IO.SearchOption.AllDirectories).FirstOrDefault()
        If Found IsNot Nothing Then Return Found

        Dim Archive As String = IO.Path.Combine(Folder, "terracotta.tar.gz")
        NetDownloadByLoader("https://github.com/burningtnt/Terracotta/releases/download/v0.4.2/terracotta-0.4.2-windows-x86_64-pkg.tar.gz", Archive)
        Dim TarProcess As Process = Process.Start(New ProcessStartInfo("tar.exe", "-xzf """ & Archive & """ -C """ & Folder.TrimEnd("\"c) & """") With {.UseShellExecute = False, .CreateNoWindow = True})
        TarProcess.WaitForExit()
        Try
            IO.File.Delete(Archive)
        Catch ex As Exception
        End Try
        Found = IO.Directory.GetFiles(Folder, "*.exe", IO.SearchOption.AllDirectories).FirstOrDefault()
        If Found Is Nothing Then Throw New Exception("陶瓦下载完成但未找到可执行文件")
        Return Found
    End Function

    Public Function StartServer() As Integer
        Dim ExePath As String = EnsureExecutable()
        Dim PortFile As String = PathTemp & "terracotta-port.json"
        Try
            IO.File.Delete(PortFile)
        Catch ex As Exception
        End Try
        Process.Start(New ProcessStartInfo(ExePath, "--hmcl """ & PortFile & """") With {
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .WorkingDirectory = IO.Path.GetDirectoryName(ExePath)})
        For i = 1 To 60
            If IO.File.Exists(PortFile) Then Exit For
            Thread.Sleep(250)
        Next
        If Not IO.File.Exists(PortFile) Then Throw New Exception("陶瓦启动超时，请稍后重试")
        Dim Json As JObject = JObject.Parse(IO.File.ReadAllText(PortFile))
        Return Json("port").ToObject(Of Integer)()
    End Function

    Public Function HttpGet(Url As String) As String
        Return NetRequestByClient(Url, Timeout:=8000, MakeLog:=False)
    End Function

    Public Sub ResetState(Port As Integer)
        Try
            HttpGet("http://127.0.0.1:" & Port & "/state/ide")
        Catch ex As Exception
        End Try
    End Sub

    Public Sub Shutdown(Port As Integer)
        Try
            HttpGet("http://127.0.0.1:" & Port & "/panic?peaceful=true")
        Catch ex As Exception
        End Try
    End Sub

End Module
