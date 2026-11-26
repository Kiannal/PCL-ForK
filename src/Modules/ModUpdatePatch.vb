Imports System.IO

''' <summary>
''' PCL-ForK 增量更新补丁。
''' 补丁由服务端按 PCLDP1 格式生成，只包含旧包到新包的复制指令与新增字节。
''' </summary>
Public Module ModUpdatePatch

    Private Const Magic As String = "PCLDP1"

    ''' <summary>
    ''' 校验补丁是否适用于指定的旧文件（大小必须一致）。
    ''' </summary>
    Public Function PatchMatches(OldPath As String, PatchPath As String) As Boolean
        Try
            If Not File.Exists(OldPath) OrElse Not File.Exists(PatchPath) Then Return False
            Using Fs As New FileStream(PatchPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                If Fs.Length < 6 + 8 + 8 + 4 Then Return False
                Dim Head(5) As Byte
                Fs.Read(Head, 0, 6)
                If Global.System.Text.Encoding.ASCII.GetString(Head) <> Magic Then Return False
                Dim SizeBuf(7) As Byte
                Fs.Read(SizeBuf, 0, 8)
                Return BitConverter.ToInt64(SizeBuf, 0) = New FileInfo(OldPath).Length
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "校验更新补丁失败")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 应用补丁，成功后返回 True 并写出 OutPath。
    ''' </summary>
    Public Function ApplyPatch(OldPath As String, PatchPath As String, OutPath As String) As Boolean
        Dim OldBytes As Byte() = Nothing
        Dim Patch As Byte() = Nothing
        Try
            OldBytes = File.ReadAllBytes(OldPath)
            Patch = File.ReadAllBytes(PatchPath)
        Catch ex As Exception
            Logger.Warn(ex, "读取更新补丁失败")
            Return False
        End Try
        If Patch.Length < 26 Then Return False
        If Global.System.Text.Encoding.ASCII.GetString(Patch, 0, 6) <> Magic Then Return False

        Dim Pos As Integer = 6
        Dim OldSize As Long = BitConverter.ToInt64(Patch, Pos) : Pos += 8
        Dim NewSize As Long = BitConverter.ToInt64(Patch, Pos) : Pos += 8
        Dim OpCount As Integer = BitConverter.ToInt32(Patch, Pos) : Pos += 4
        If OldSize <> OldBytes.Length Then
            Logger.Warn($"更新补丁与当前文件不匹配（期望 {OldSize}，实际 {OldBytes.Length}）")
            Return False
        End If
        If NewSize <= 0 OrElse NewSize > 512L * 1024 * 1024 Then Return False
        If OpCount <= 0 OrElse OpCount > 10000000 Then Return False

        Try
            Using Out As New MemoryStream(CInt(NewSize))
                For Index As Integer = 0 To OpCount - 1
                    If Pos >= Patch.Length Then Return False
                    Dim OpType As Byte = Patch(Pos) : Pos += 1
                    If OpType = 0 Then
                        If Pos + 12 > Patch.Length Then Return False
                        Dim SrcOff As Long = BitConverter.ToInt64(Patch, Pos) : Pos += 8
                        Dim Len As Integer = BitConverter.ToInt32(Patch, Pos) : Pos += 4
                        If SrcOff < 0 OrElse Len < 0 OrElse SrcOff + Len > OldBytes.Length Then Return False
                        Out.Write(OldBytes, CInt(SrcOff), Len)
                    ElseIf OpType = 1 Then
                        If Pos + 4 > Patch.Length Then Return False
                        Dim Len As Integer = BitConverter.ToInt32(Patch, Pos) : Pos += 4
                        If Len < 0 OrElse Pos + Len > Patch.Length Then Return False
                        Out.Write(Patch, Pos, Len)
                        Pos += Len
                    Else
                        Return False
                    End If
                Next
                Dim Result As Byte() = Out.ToArray()
                If Result.Length <> CInt(NewSize) Then
                    Logger.Warn($"补丁还原后大小不符（期望 {NewSize}，实际 {Result.Length}）")
                    Return False
                End If
                File.WriteAllBytes(OutPath, Result)
                Return True
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "应用更新补丁失败")
            Return False
        End Try
    End Function

End Module
