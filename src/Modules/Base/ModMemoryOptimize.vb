Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Threading

Public Module ModMemoryOptimize

    <DllImport("kernel32.dll")>
    Private Function SetProcessWorkingSetSize(hProcess As IntPtr, dwMinimumWorkingSetSize As IntPtr, dwMaximumWorkingSetSize As IntPtr) As Boolean
    End Function

    <DllImport("psapi.dll")>
    Private Function EmptyWorkingSet(hProcess As IntPtr) As Boolean
    End Function

    Private OptimizerStarted As Boolean = False
    Private BoostedPids As New HashSet(Of Integer)

    Public Sub EnsureStarted()
        If OptimizerStarted Then Return
        OptimizerStarted = True
        RunInNewThread(
        Sub()
            Do
                Try
                    Dim Interval As Integer = Math.Max(10, Settings.Get(Of Integer)("SystemMemoryOptimizeInterval"))
                    Thread.Sleep(5000)
                    If Not Settings.Get(Of Boolean)("SystemMemoryOptimize") Then Continue Do
                    Dim LoadingNow As Boolean = False
                    Try
                        LoadingNow = McLaunchLoader.State = LoadState.Loading
                    Catch ex As Exception
                    End Try
                    If LoadingNow Then
                        TrimLauncherOnly()
                        Continue Do
                    End If
                    Dim LastRun As Long = GetTimeMs()
                    If LastRun - LastOptimizeTime < Interval * 1000 Then Continue Do
                    LastOptimizeTime = LastRun
                    OptimizeOnce(False)
                Catch ex As Exception
                    Logger.Warn(ex, "实时内存优化失败")
                End Try
            Loop
        End Sub, "Memory Optimize")
    End Sub

    Private LastOptimizeTime As Long = 0

    Public Sub OptimizeOnce(ShowHint As Boolean)
        Dim BeforeTotal As Long = 0
        Dim AfterTotal As Long = 0
        Try
            Dim Current As Process = Process.GetCurrentProcess()
            BeforeTotal += Current.WorkingSet64
            Try
                GC.Collect(2, GCCollectionMode.Forced, True, True)
                GC.WaitForPendingFinalizers()
                GC.Collect(2, GCCollectionMode.Forced, True, True)
            Catch ex As Exception
            End Try
            TrimProcess(Current)
            AfterTotal += Current.WorkingSet64

            Dim Loading As Boolean
            Try
                Loading = McLaunchLoader.State = LoadState.Loading
            Catch ex As Exception
                Loading = False
            End Try
            For Each JavaProcess In GetMinecraftProcesses()
                Try
                    BeforeTotal += JavaProcess.WorkingSet64
                    If Loading Then BoostProcess(JavaProcess)
                    If Not Loading Then TrimProcess(JavaProcess)
                    AfterTotal += JavaProcess.WorkingSet64
                Catch ex As Exception
                End Try
            Next

            If Settings.Get(Of Boolean)("SystemMemoryOptimizeBackground") Then
                Try
                    For Each Target In GetBackgroundProcesses()
                        Try
                            BeforeTotal += Target.WorkingSet64
                            TrimProcess(Target)
                            AfterTotal += Target.WorkingSet64
                        Catch ex As Exception
                        End Try
                    Next
                Catch ex As Exception
                End Try
            End If

            Dim Freed As Long = Math.Max(0, BeforeTotal - AfterTotal)
            Logger.Info($"实时内存优化：{StringUtils.FormatByteSize(BeforeTotal)} → {StringUtils.FormatByteSize(AfterTotal)}，释放 {StringUtils.FormatByteSize(Freed)}")
            If ShowHint Then Hint($"内存优化完成，释放了 {StringUtils.FormatByteSize(Freed)}", HintType.Green)
        Catch ex As Exception
            Logger.Warn(ex, "内存优化失败")
            If ShowHint Then Hint("内存优化失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private LastIdleTrim As Long = 0
    Public Sub TrimIdle()
        Try
            If Not Settings.Get(Of Boolean)("SystemMemoryOptimize") Then Return
            Dim NowTime As Long = GetTimeMs()
            If NowTime - LastIdleTrim < 5000 Then Return
            LastIdleTrim = NowTime
            TrimLauncherOnly()
        Catch ex As Exception
        End Try
    End Sub

    Private Sub TrimLauncherOnly()
        Try
            GC.Collect(2, GCCollectionMode.Forced, True, True)
            TrimProcess(Process.GetCurrentProcess())
            For Each JavaProcess In GetMinecraftProcesses()
                BoostProcess(JavaProcess)
            Next
        Catch ex As Exception
        End Try
    End Sub

    Private Sub TrimProcess(Process As Process)
        Try
            Process.Refresh()
            EmptyWorkingSet(Process.Handle)
            SetProcessWorkingSetSize(Process.Handle, New IntPtr(-1), New IntPtr(-1))
        Catch ex As Exception
        End Try
    End Sub

    Private Sub BoostProcess(Process As Process)
        Try
            If BoostedPids.Contains(Process.Id) Then Return
            Process.PriorityClass = ProcessPriorityClass.AboveNormal
            BoostedPids.Add(Process.Id)
        Catch ex As Exception
        End Try
    End Sub

    Private Function GetMinecraftProcesses() As List(Of Process)
        Dim Result As New List(Of Process)
        For Each Name In {"javaw", "java", "javaw.exe", "java.exe"}
            Try
                For Each P In Process.GetProcessesByName(Name.Replace(".exe", ""))
                    Try
                        Dim Path As String = P.MainModule.FileName.ToLower()
                        If Path.Contains(".minecraft") OrElse Path.Contains("\runtime\java") OrElse Path.Contains("/runtime/java") Then
                            If Not Result.Any(Function(X) X.Id = P.Id) Then Result.Add(P)
                        End If
                    Catch ex As Exception
                    End Try
                Next
            Catch ex As Exception
            End Try
        Next
        Return Result
    End Function

    Private SkipBackgroundAs As HashSet(Of String) = Nothing

    Private Function GetSkipSet() As HashSet(Of String)
        If SkipBackgroundAs IsNot Nothing Then Return SkipBackgroundAs
        SkipBackgroundAs = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each Name In {"system", "registry", "idle", "csrss", "wininit", "services", "lsass", "smss", "winlogon", "dwm",
                          "explorer", "audiodg", "memory compression", "pcl-fork", "plain craft launcher 2", "searchindexer",
                          "msmpeng", "securityhealthservice", "sihost", "taskhostw", "ctfmon", "fontdrvhost", "conhost", "runtimebroker"}
            SkipBackgroundAs.Add(Name)
        Next
        Return SkipBackgroundAs
    End Function

    Private Function GetBackgroundProcesses() As List(Of Process)
        Dim Result As New List(Of Process)
        Dim CurrentSession As Integer = Process.GetCurrentProcess().SessionId
        For Each P In Process.GetProcesses()
            Try
                If P.SessionId <> CurrentSession Then Continue For
                If P.Id = Process.GetCurrentProcess().Id Then Continue For
                If GetSkipSet().Contains(P.ProcessName) Then Continue For
                If P.MainWindowHandle <> IntPtr.Zero Then Continue For
                If P.WorkingSet64 < 250L * 1024 * 1024 Then Continue For
                Result.Add(P)
            Catch ex As Exception
            End Try
        Next
        Return Result
    End Function

End Module
