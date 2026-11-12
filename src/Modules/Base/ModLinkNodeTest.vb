Imports System.Diagnostics
Imports System.Net.Sockets

Public Class LinkNodeResult
    Public Address As String = ""
    Public Host As String = ""
    Public Port As Integer = 11010
    Public Latency As Integer = -1
    Public Ok As Boolean = False
    Public Source As String = ""
    Public Overrides Function ToString() As String
        If Ok Then Return $"{Address}    {Latency} ms    ({Source})"
        Return $"{Address}    不可用    ({Source})"
    End Function
End Class

Public Module ModLinkNodeTest

    Public Function GetCandidateNodes() As List(Of LinkNodeResult)
        Dim Result As New List(Of LinkNodeResult)
        Try
            Dim Added As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim AddNode As Action(Of String, String) =
                Sub(Address As String, Source As String)
                    If String.IsNullOrWhiteSpace(Address) Then Return
                    Address = Address.Trim()
                    If Not Added.Add(Address) Then Return
                    Dim Parsed As String() = ParseNode(Address)
                    If Parsed Is Nothing Then Return
                    Result.Add(New LinkNodeResult With {
                        .Address = Address, .Host = Parsed(0), .Port = Integer.Parse(Parsed(1)), .Source = Source})
                End Sub
            Dim CustomPeers As String = Settings.Get(Of String)("LinkCustomPeer")
            If Not String.IsNullOrWhiteSpace(CustomPeers) Then
                For Each Peer In CustomPeers.Split("，,".ToCharArray)
                    AddNode(Peer, "自定义节点")
                Next
            End If
            Try
                Dim LinkConfig As JObject = TryCast(ServerConfig?("Link"), JObject)
                If LinkConfig IsNot Nothing Then
                    If LinkConfig("Peers") IsNot Nothing Then
                        For Each Peer In LinkConfig("Peers")
                            AddNode(Peer.ToString(), "PCL 节点")
                        Next
                    End If
                    If LinkConfig("DiscoverPeer") IsNot Nothing Then AddNode(LinkConfig("DiscoverPeer").ToString(), "回退节点")
                    If LinkConfig("MandatoryPeers") IsNot Nothing Then
                        For Each Peer In LinkConfig("MandatoryPeers")
                            AddNode(Peer.ToString(), "强制节点")
                        Next
                    End If
                End If
            Catch ex As Exception
                Logger.Warn(ex, "读取 PCL 节点配置失败")
            End Try
            Return Result
        Catch ex As Exception
            Logger.Warn(ex, "获取候选节点失败")
            Return Result
        End Try
    End Function

    Public Function ParseNode(Address As String) As String()
        Try
            Dim Clean As String = Address.Trim()
            For Each Scheme In {"tcp://", "udp://", "ws://", "wss://", "quic://"}
                If Clean.StartsWithF(Scheme, True) Then Clean = Clean.Substring(Scheme.Length)
            Next
            Clean = Clean.TrimEnd("/"c)
            Dim Host As String = Clean
            Dim Port As Integer = 11010
            If Clean.StartsWith("[") Then
                Dim CloseIndex As Integer = Clean.IndexOf("]")
                If CloseIndex > 0 Then
                    Host = Clean.Substring(1, CloseIndex - 1)
                    If Clean.Length > CloseIndex + 1 AndAlso Clean(CloseIndex + 1) = ":"c Then
                        Integer.TryParse(Clean.Substring(CloseIndex + 2), Port)
                    End If
                End If
            Else
                Dim ColonIndex As Integer = Clean.LastIndexOf(":"c)
                If ColonIndex > 0 AndAlso Not Clean.Contains("]") Then
                    Host = Clean.Substring(0, ColonIndex)
                    Integer.TryParse(Clean.Substring(ColonIndex + 1), Port)
                End If
            End If
            If Host = "" OrElse Port <= 0 OrElse Port > 65535 Then Return Nothing
            Return {Host, Port.ToString()}
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Public Sub TestNode(Node As LinkNodeResult)
        Try
            Dim Watch As Stopwatch = Stopwatch.StartNew()
            Dim Client As New TcpClient
            Try
                Dim ConnectTask = Client.ConnectAsync(Node.Host, Node.Port)
                If ConnectTask.Wait(2500) Then
                    Watch.Stop()
                    Node.Latency = CInt(Watch.ElapsedMilliseconds)
                    Node.Ok = True
                Else
                    Node.Ok = False
                    Node.Latency = -1
                End If
            Finally
                Client.Close()
            End Try
        Catch ex As Exception
            Node.Ok = False
            Node.Latency = -1
        End Try
    End Sub

End Module
