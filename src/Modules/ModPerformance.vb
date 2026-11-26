''' <summary>
''' 设备性能评估与低配降级。
''' 低配机器上 WPF 会退化成软件渲染，透明窗口与投影是主要开销来源。
''' </summary>
Public Module ModPerformance

    Private _IsLowEnd As Integer = -1

    ''' <summary>
    ''' 是否为低配设备。结果缓存，只检测一次。
    ''' </summary>
    Public Function IsLowEnd() As Boolean
        If _IsLowEnd >= 0 Then Return _IsLowEnd = 1
        Dim Result As Boolean = False
        Try
            Dim Cores As Integer = Environment.ProcessorCount
            If Cores <= 2 Then Result = True
            Dim TotalMem As Long = 0
            Try
                TotalMem = CLng(New Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory)
            Catch
            End Try
            If TotalMem > 0 AndAlso TotalMem < 4L * 1024 * 1024 * 1024 Then Result = True
            '32 位系统内存天然受限
            If Not Environment.Is64BitOperatingSystem Then Result = True
            Logger.Info($"[性能] 核心数 {Cores}，内存 {TotalMem / 1073741824.0:0.0} GB，低配判定 {Result}")
        Catch ex As Exception
            Logger.Warn(ex, "检测设备性能失败")
        End Try
        _IsLowEnd = If(Result, 1, 0)
        Return Result
    End Function

    ''' <summary>
    ''' 低配设备上的动画倍率，放大后动画帧数更少。
    ''' </summary>
    Public Function AnimationScale() As Double
        Return If(IsLowEnd(), 1.8, 1.0)
    End Function

End Module
