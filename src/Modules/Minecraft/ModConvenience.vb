Imports System.IO.Compression

Public Module ModConvenience

    Public ReadOnly Property BackupRoot As String
        Get
            Return PathPure.Value & "Backup\Saves\"
        End Get
    End Property

    Public Sub OpenInstanceFolder(Instance As McInstance, SubFolder As String)
        Try
            If Instance Is Nothing Then Return
            Dim Target As String
            If String.IsNullOrWhiteSpace(SubFolder) Then
                Target = Instance.PathVersion
            Else
                Target = Instance.PathIndie & SubFolder & "\"
            End If
            DirectoryUtils.Create(Target)
            OpenExplorer(Target)
        Catch ex As Exception
            Logger.Warn(ex, "打开版本文件夹失败")
            Hint("打开文件夹失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Public Function BackupSaves(Instance As McInstance, Optional Silent As Boolean = False) As String
        Try
            If Instance Is Nothing Then Return Nothing
            Dim SavesPath As String = Instance.PathIndie & "saves\"
            If Not DirectoryUtils.Exists(SavesPath) Then
                If Not Silent Then Hint("当前版本没有存档文件夹", HintType.Blue)
                Return Nothing
            End If
            Dim Files As List(Of String) = DirectoryUtils.EnumerateFiles(SavesPath, True).ToList()
            If Not Files.Any() Then
                If Not Silent Then Hint("当前版本没有存档", HintType.Blue)
                Return Nothing
            End If
            DirectoryUtils.Create(BackupRoot)
            Dim BackupPath As String = BackupRoot & Instance.Name & "_" & Date.Now.ToString("yyyyMMdd_HHmmss") & ".zip"
            Using Zip As New ZipArchive(File.Open(BackupPath, FileMode.Create), ZipArchiveMode.Create)
                For Each FilePath In Files
                    Dim Relative As String = FilePath.Substring(SavesPath.Length)
                    Dim Entry As ZipArchiveEntry = Zip.CreateEntry("saves/" & Relative.Replace("\"c, "/"c))
                    Using Source As FileStream = File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                        Using Target As Stream = Entry.Open()
                            Source.CopyTo(Target)
                        End Using
                    End Using
                Next
            End Using
            CleanOldBackups()
            Dim Size As Long = New FileInfo(BackupPath).Length
            Logger.Info($"存档备份完成：{BackupPath}（{StringUtils.FormatByteSize(Size)}）")
            If Not Silent Then
                Hint($"存档备份完成：{StringUtils.FormatByteSize(Size)}", HintType.Green)
                OpenExplorer(BackupRoot)
            End If
            Return BackupPath
        Catch ex As Exception
            Logger.Error(ex, "备份存档失败")
            If Not Silent Then Hint("备份存档失败：" & ex.Message, HintType.Red)
            Return Nothing
        End Try
    End Function

    Public Sub RestoreLatestBackup(Instance As McInstance)
        Try
            If Instance Is Nothing Then Return
            If Not DirectoryUtils.Exists(BackupRoot) Then
                Hint("还没有任何存档备份", HintType.Blue)
                Return
            End If
            Dim Latest As String =
                DirectoryUtils.EnumerateFiles(BackupRoot).
                Where(Function(f) PathUtils.GetExtension(f).Lower = "zip").
                OrderByDescending(Function(f) New FileInfo(f).LastWriteTime).FirstOrDefault()
            If Latest Is Nothing Then
                Hint("还没有任何存档备份", HintType.Blue)
                Return
            End If
            Dim Size As String = StringUtils.FormatByteSize(New FileInfo(Latest).Length)
            If MyMsgBox($"将把备份「{PathUtils.GetLastPart(Latest)}」（{Size}）恢复到当前版本的存档文件夹。{vbCrLf}{vbCrLf}同名存档会被覆盖，恢复前会先自动备份当前存档。", "恢复存档备份", "开始恢复", "取消") <> 1 Then Return
            BackupSaves(Instance, Silent:=True)
            Dim SavesPath As String = Instance.PathIndie & "saves\"
            DirectoryUtils.Create(SavesPath)
            Dim Count As Integer = 0
            Using Zip As ZipArchive = FileUtils.OpenZip(Latest)
                For Each Entry As ZipArchiveEntry In Zip.Entries
                    If Entry.FullName.EndsWith("/") Then Continue For
                    Dim Relative As String = Entry.FullName
                    If Relative.StartsWith("saves/", StringComparison.OrdinalIgnoreCase) Then Relative = Relative.Substring(6)
                    Dim TargetPath As String = SavesPath & Relative.Replace("/"c, "\"c)
                    DirectoryUtils.Create(PathUtils.RemoveLastPart(TargetPath))
                    Using Source As Stream = Entry.Open()
                        Using Target As FileStream = File.Open(TargetPath, FileMode.Create, FileAccess.Write, FileShare.Read)
                            Source.CopyTo(Target)
                        End Using
                    End Using
                    Count += 1
                Next
            End Using
            Logger.Info($"已恢复存档备份：{Latest}，共 {Count} 个文件")
            Hint($"已恢复 {Count} 个存档文件", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "恢复存档备份失败")
            Hint("恢复存档失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub CleanOldBackups()
        Try
            Dim Files As List(Of String) =
                DirectoryUtils.EnumerateFiles(BackupRoot).
                Where(Function(f) PathUtils.GetExtension(f).Lower = "zip").
                OrderByDescending(Function(f) New FileInfo(f).LastWriteTime).ToList()
            For i = 10 To Files.Count - 1
                FileUtils.Delete(Files(i))
            Next
        Catch ex As Exception
            Logger.Warn(ex, "清理旧存档备份失败")
        End Try
    End Sub

    Public Sub AutoBackupSaves(Instance As McInstance)
        Try
            If Instance Is Nothing Then Return
            If Not Settings.Get(Of Boolean)("VersionAutoBackupSaves", Instance:=Instance) Then Return
            BackupSaves(Instance, Silent:=True)
        Catch ex As Exception
            Logger.Warn(ex, "启动前自动备份存档失败")
        End Try
    End Sub

End Module
