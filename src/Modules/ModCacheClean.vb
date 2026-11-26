Imports System.IO

''' <summary>
''' 启动器运行时缓存清理。
''' 图片缓存只按时间做过期判断，旧文件从不删除，长期使用会占满磁盘并拖慢目录枚举。
''' </summary>
Public Module ModCacheClean

    Private Const ImageCacheMaxBytes As Long = 300L * 1024 * 1024
    Private Const ImageCacheMaxDays As Integer = 7

    ''' <summary>
    ''' 清理临时目录下的图片缓存：先删过期文件，再按总大小上限淘汰最旧的。
    ''' </summary>
    Public Sub CleanImageCache()
        Try
            Dim Dir As String = IO.Path.Combine(PathTemp, "MyImage")
            If Not Directory.Exists(Dir) Then Return
            Dim Files As New List(Of FileInfo)
            Dim Total As Long = 0
            Dim Expired As Date = Date.Now.AddDays(-ImageCacheMaxDays)
            For Each F As String In Directory.GetFiles(Dir)
                Try
                    Dim Fi As New FileInfo(F)
                    If Fi.LastWriteTime < Expired Then
                        Fi.Delete()
                        Continue For
                    End If
                    Total += Fi.Length
                    Files.Add(Fi)
                Catch
                End Try
            Next
            Dim Removed As Integer = 0
            If Total > ImageCacheMaxBytes Then
                Files.Sort(Function(A, B) A.LastWriteTime.CompareTo(B.LastWriteTime))
                For Each Fi In Files
                    If Total <= ImageCacheMaxBytes Then Exit For
                    Try
                        Total -= Fi.Length
                        Fi.Delete()
                        Removed += 1
                    Catch
                    End Try
                Next
            End If
            Logger.Info($"[缓存] 图片缓存清理完成，剩余 {Files.Count - Removed} 个文件，{Total / 1048576.0:0.0} MB")
        Catch ex As Exception
            Logger.Warn(ex, "清理图片缓存失败")
        End Try
    End Sub

    ''' <summary>
    ''' 清理下载临时文件与崩溃残留。
    ''' </summary>
    Public Sub CleanTempFiles()
        Try
            If Not Directory.Exists(PathTemp) Then Return
            Dim Removed As Integer = 0
            Dim Expired As Date = Date.Now.AddDays(-2)
            For Each F As String In Directory.GetFiles(PathTemp, "*.tmp")
                Try
                    Dim Fi As New FileInfo(F)
                    If Fi.LastWriteTime < Expired Then
                        Fi.Delete()
                        Removed += 1
                    End If
                Catch
                End Try
            Next
            If Removed > 0 Then Logger.Info($"[缓存] 清理临时文件 {Removed} 个")
        Catch ex As Exception
            Logger.Warn(ex, "清理临时文件失败")
        End Try
    End Sub

End Module
