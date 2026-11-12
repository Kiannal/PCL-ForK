Public Class PageInstanceTranslate

    Private Entries As List(Of ModAutoTranslate.TranslationEntry) = New List(Of ModAutoTranslate.TranslationEntry)
    Private LoadedInstance As McInstance = Nothing
    Private Running As Boolean = False

    Public Sub New()
        InitializeComponent()
        AddHandler BtnScan.Click, Sub(sender, e) Scan()
        AddHandler BtnSelectAll.Click, Sub(sender, e) SetAllChecked(True)
        AddHandler BtnSelectNone.Click, Sub(sender, e) SetAllChecked(False)
        AddHandler BtnSelectInvert.Click, Sub(sender, e) InvertChecked()
        AddHandler BtnStart.Click, Sub(sender, e) StartTranslate()
        AddHandler BtnDeepSeek.Click, Sub(sender, e)
                                          Dim Key As String = MyMsgBoxInput("DeepSeek 翻译", "输入 DeepSeek API Key（会加密保存在本机，留空则使用免费 Google 翻译）。", "")
                                          If Key Is Nothing Then Return
                                          If ModAutoTranslate.SetDeepSeekKey(Key) Then
                                              Hint(If(Key.Trim() = "", "已清空 DeepSeek Key", "DeepSeek Key 已加密保存"), HintType.Green)
                                          Else
                                              Hint("保存 DeepSeek Key 失败", HintType.Red)
                                          End If
                                      End Sub
    End Sub

    Private Sub PageEnterHandler() Handles Me.PageEnter
        Try
            If Running Then Return
            If PageInstanceLeft.Instance Is Nothing Then Return
            LoadedInstance = PageInstanceLeft.Instance
            Scan()
        Catch ex As Exception
        End Try
    End Sub

    Private Sub PageInstanceTranslate_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Try
            Dim Version As McInstance = PageInstanceLeft.Instance
            If Version Is Nothing Then Return
            If Version Is LoadedInstance AndAlso Entries.Any() Then Return
            LoadedInstance = Version
            TextLog.Text = ""
            Scan()
        Catch ex As Exception
            Logger.Warn(ex, "初始化自动汉化页失败")
        End Try
    End Sub

    Private Sub Log(Text As String)
        Try
            TextLog.AppendText($"[{Date.Now:HH:mm:ss}] {Text}{vbCrLf}")
            TextLog.ScrollToEnd()
        Catch ex As Exception
        End Try
    End Sub

    Private Sub Scan()
        If Running Then Return
        Dim Version As McInstance = PageInstanceLeft.Instance
        If Version Is Nothing Then Return
        Running = True
        BtnScan.IsEnabled = False
        BtnStart.IsEnabled = False
        Log("正在扫描模组语言文件……")
        RunInNewThread(
            Sub()
                Dim Result As List(Of ModAutoTranslate.TranslationEntry) = ModAutoTranslate.ScanMods(Version)
                RunInUi(
                    Sub()
                        Entries = Result
                        ListMods.ItemsSource = Nothing
                        ListMods.ItemsSource = Entries
                        Dim TranslateCount As Integer = Entries.Where(Function(en) en.IsChecked).Count
                        Log($"扫描完成：共 {Entries.Count} 个模组，其中 {TranslateCount} 个可汉化")
                        BtnScan.IsEnabled = True
                        BtnStart.IsEnabled = Entries.Any(Function(en) en.IsChecked)
                        Running = False
                    End Sub)
            End Sub, "AutoTranslateScan")
    End Sub

    Private Sub RefreshList()
        ListMods.ItemsSource = Nothing
        ListMods.ItemsSource = Entries
        BtnStart.IsEnabled = Not Running AndAlso Entries.Any(Function(en) en.IsChecked)
    End Sub

    Private Sub SetAllChecked(Checked As Boolean)
        For Each Entry In Entries
            Entry.IsChecked = Checked
        Next
        RefreshList()
    End Sub

    Private Sub InvertChecked()
        For Each Entry In Entries
            Entry.IsChecked = Not Entry.IsChecked
        Next
        RefreshList()
    End Sub

    Private Sub StartTranslate()
        If Running Then Return
        Dim Version As McInstance = PageInstanceLeft.Instance
        If Version Is Nothing Then Return
        Dim Targets As List(Of ModAutoTranslate.TranslationEntry) = Entries.Where(Function(en) en.IsChecked).ToList()
        If Not Targets.Any() Then
            Hint("请先勾选要汉化的模组", HintType.Blue)
            Return
        End If
        If MyMsgBox($"将自动翻译 {Targets.Count} 个模组并生成汉化资源包（需要联网，可能耗时较长），是否继续？", "自动汉化", "开始", "取消") <> 1 Then Return
        Running = True
        BtnStart.IsEnabled = False
        BtnScan.IsEnabled = False
        Log("开始自动汉化……")
        RunInNewThread(
            Sub()
                Dim Result As String = ModAutoTranslate.TranslateMods(Version, Targets,
                    Sub(Text) RunInUi(Sub() Log(Text)))
                RunInUi(
                    Sub()
                        Running = False
                        BtnScan.IsEnabled = True
                        BtnStart.IsEnabled = Entries.Any(Function(en) en.IsChecked)
                        If Result IsNot Nothing Then
                            Log("汉化完成：" & Result)
                            Hint("自动汉化完成，资源包已写入并启用", HintType.Green)
                        Else
                            Hint("自动汉化未完成，请查看日志", HintType.Red)
                        End If
                        Scan()
                    End Sub)
            End Sub, "AutoTranslateRun")
    End Sub

End Class
