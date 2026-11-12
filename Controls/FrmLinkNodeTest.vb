Public Class FrmLinkNodeTest
    Inherits Window

    Private ReadOnly ListNodes As New ListBox
    Private ReadOnly LabStatus As New TextBlock
    Private ReadOnly BtnTest As New MyButton
    Private ReadOnly BtnAdd As New MyButton
    Private ReadOnly BtnApply As New MyButton
    Private Nodes As New List(Of LinkNodeResult)
    Private Testing As Boolean = False

    Public Sub New()
        Title = "联机节点测速"
        Width = 640
        Height = 480
        MinWidth = 520
        MinHeight = 380
        WindowStartupLocation = WindowStartupLocation.CenterScreen
        Background = New SolidColorBrush(Color.FromRgb(247, 248, 250))
        FontFamily = New FontFamily("Microsoft YaHei UI")

        Dim Root As New Grid With {.Margin = New Thickness(16)}
        Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Root.RowDefinitions.Add(New RowDefinition)
        Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Content = Root

        Dim Hint As New TextBlock With {
            .Text = "测试各个 EasyTier 节点的 TCP 连接延迟，可用于挑选联机中转线路。测速后可以把可用节点一键写入 设置 → 联机 → 自定义节点。",
            .TextWrapping = TextWrapping.Wrap, .FontSize = 12, .Foreground = New SolidColorBrush(Color.FromRgb(90, 96, 104)),
            .Margin = New Thickness(0, 0, 0, 12)}
        Root.Children.Add(Hint)

        ListNodes.FontSize = 12.5
        ListNodes.Margin = New Thickness(0, 0, 0, 10)
        Grid.SetRow(ListNodes, 1)
        Root.Children.Add(ListNodes)

        Dim ButtonPanel As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 0, 0, 8)}
        BtnTest.Text = "开始测速"
        BtnTest.Height = 30
        BtnTest.Padding = New Thickness(18, 0, 18, 0)
        BtnTest.ColorType = MyButton.ColorState.Highlight
        BtnTest.Margin = New Thickness(0, 0, 8, 0)
        ButtonPanel.Children.Add(BtnTest)
        BtnAdd.Text = "添加节点"
        BtnAdd.Height = 30
        BtnAdd.Padding = New Thickness(18, 0, 18, 0)
        BtnAdd.Margin = New Thickness(0, 0, 8, 0)
        ButtonPanel.Children.Add(BtnAdd)
        BtnApply.Text = "设为自定义节点"
        BtnApply.Height = 30
        BtnApply.Padding = New Thickness(18, 0, 18, 0)
        ButtonPanel.Children.Add(BtnApply)
        Grid.SetRow(ButtonPanel, 2)
        Root.Children.Add(ButtonPanel)

        LabStatus.TextWrapping = TextWrapping.Wrap
        LabStatus.FontSize = 12
        LabStatus.Foreground = New SolidColorBrush(Color.FromRgb(90, 96, 104))
        Grid.SetRow(LabStatus, 3)
        Root.Children.Add(LabStatus)

        AddHandler BtnTest.Click, Sub(sender, e) StartTest()
        AddHandler BtnAdd.Click, Sub(sender, e) AddNode()
        AddHandler BtnApply.Click, Sub(sender, e) ApplyFastest()
        AddHandler Loaded, Sub() RefreshNodes()
    End Sub

    Private Sub RefreshNodes()
        Try
            Nodes = ModLinkNodeTest.GetCandidateNodes()
            ListNodes.ItemsSource = Nothing
            ListNodes.ItemsSource = Nodes
            If Nodes.Any() Then
                LabStatus.Text = $"共 {Nodes.Count} 个候选节点，点击「开始测速」"
            Else
                LabStatus.Text = "没有找到候选节点。可以在 设置 → 联机 → 自定义节点 中填写，或点「添加节点」手动添加。"
            End If
        Catch ex As Exception
            Logger.Warn(ex, "刷新联机节点列表失败")
            LabStatus.Text = "刷新节点列表失败：" & ex.Message
        End Try
    End Sub

    Private Sub StartTest()
        If Testing Then Return
        If Not Nodes.Any() Then
            LabStatus.Text = "没有可测速的节点，请先添加"
            Return
        End If
        Testing = True
        BtnTest.IsEnabled = False
        BtnAdd.IsEnabled = False
        BtnApply.IsEnabled = False
        Dim Targets As New List(Of LinkNodeResult)(Nodes)
        ListNodes.ItemsSource = Nothing
        ListNodes.ItemsSource = Targets
        LabStatus.Text = $"正在测速 {Targets.Count} 个节点……"
        RunInNewThread(
            Sub()
                Dim Tasks As New List(Of Task)
                For Each Node In Targets
                    Dim Item As LinkNodeResult = Node
                    Tasks.Add(Task.Run(Sub() ModLinkNodeTest.TestNode(Item)))
                Next
                Try
                    Task.WaitAll(Tasks.ToArray(), 8000)
                Catch ex As Exception
                    Logger.Warn(ex, "等待节点测速超时")
                End Try
                RunInUi(
                    Sub()
                        Testing = False
                        BtnTest.IsEnabled = True
                        BtnAdd.IsEnabled = True
                        BtnApply.IsEnabled = True
                        Dim Sorted As List(Of LinkNodeResult) =
                            Targets.OrderByDescending(Function(n) n.Ok).
                            ThenBy(Function(n) If(n.Ok, n.Latency, Integer.MaxValue)).ToList()
                        ListNodes.ItemsSource = Nothing
                        ListNodes.ItemsSource = Sorted
                        Dim OkCount As Integer = Sorted.Where(Function(n) n.Ok).Count
                        LabStatus.Text = $"测速完成：{OkCount}/{Sorted.Count} 个节点可用"
                    End Sub)
            End Sub, "LinkNodeTest")
    End Sub

    Private Sub AddNode()
        Dim Input As String = MyMsgBoxInput("添加节点", "输入 EasyTier 节点地址，例如 tcp://1.2.3.4:11010。", HintText:="tcp://...")
        If String.IsNullOrWhiteSpace(Input) Then Return
        Dim Address As String = Input.Trim()
        Dim Parsed As String() = ModLinkNodeTest.ParseNode(Address)
        If Parsed Is Nothing Then
            Hint("节点地址格式不正确", HintType.Red)
            Return
        End If
        If Nodes.Any(Function(n) String.Equals(n.Address, Address, StringComparison.OrdinalIgnoreCase)) Then
            Hint("这个节点已经在列表里了", HintType.Blue)
            Return
        End If
        Nodes.Add(New LinkNodeResult With {
            .Address = Address, .Host = Parsed(0), .Port = Integer.Parse(Parsed(1)), .Source = "手动添加"})
        ListNodes.ItemsSource = Nothing
        ListNodes.ItemsSource = Nodes
        LabStatus.Text = "已添加节点，点击「开始测速」测试延迟"
    End Sub

    Private Sub ApplyFastest()
        Dim Available As List(Of LinkNodeResult) =
            Nodes.Where(Function(n) n.Ok).OrderBy(Function(n) n.Latency).ToList()
        If Not Available.Any() Then
            Hint("还没有可用节点，请先测速", HintType.Red)
            Return
        End If
        Settings.Set("LinkCustomPeer", Available.Select(Function(n) n.Address).Join("，，"))
        Hint($"已把 {Available.Count} 个可用节点按延迟写入自定义节点设置", HintType.Green)
        LabStatus.Text = "已保存到 设置 → 联机 → 自定义节点（按延迟从低到高）"
    End Sub
End Class
