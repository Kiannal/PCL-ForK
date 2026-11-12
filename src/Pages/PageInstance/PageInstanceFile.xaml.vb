Imports System.Text

Public Class PageInstanceFile

    Private Class FileEntry
        Public Path As String
        Public IsFolder As Boolean
        Public ReadOnly Property Name As String
            Get
                Return If(IsFolder, "📁  " & IO.Path.GetFileName(Path), "📄  " & IO.Path.GetFileName(Path))
            End Get
        End Property
        Public Overrides Function ToString() As String
            Return Name
        End Function
    End Class

    Private ReadOnly Entries As New List(Of FileEntry)
    Private CurrentFolder As String
    Private CurrentFile As String
    Private SaveEncoding As Encoding = New UTF8Encoding(False)
    Private SaveBom As Boolean = False
    Private IsDirty As Boolean = False
    Private IsLoading As Boolean = False
    Private LoadedRoot As String = ""

    Public Sub New()
        InitializeComponent()
        AddHandler BtnUp.Click, Sub(sender, e) NavigateUp()
        AddHandler BtnRefresh.Click, Sub(sender, e) LoadFolder(CurrentFolder)
        AddHandler BtnOpenFolder.Click, Sub(sender, e)
                                           If CurrentFolder IsNot Nothing Then OpenExplorer(CurrentFolder)
                                       End Sub
        AddHandler BtnSave.Click, Sub(sender, e) SaveFile()
        AddHandler ListFiles.MouseDoubleClick, Sub(sender, e) OpenSelected()
        AddHandler TextFilter.TextChanged, Sub() RefreshList()
        AddHandler TextEditor.TextChanged, Sub()
                                               If IsLoading Then Return
                                               IsDirty = True
                                               BtnSave.IsEnabled = True
                                               UpdateEditorCard()
                                           End Sub
        AddHandler PreviewKeyDown, Sub(sender, e)
                                       If e.Key = Key.S AndAlso Keyboard.Modifiers = ModifierKeys.Control Then
                                           SaveFile()
                                           e.Handled = True
                                       End If
                                   End Sub
    End Sub

    Private Sub PageInstanceFile_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Try
            If PageInstanceLeft.Instance Is Nothing Then Return
            Dim Root As String = PageInstanceLeft.Instance.PathIndie
            If Not DirectoryUtils.Exists(Root) Then Root = PageInstanceLeft.Instance.PathVersion
            If Root = LoadedRoot AndAlso CurrentFolder IsNot Nothing Then Return
            LoadedRoot = Root
            CurrentFile = Nothing
            IsDirty = False
            BtnSave.IsEnabled = False
            CardEditor.Title = "未选择文件"
            TextEditor.Text = ""
            LabInfo.Text = "双击左侧文件开始编辑"
            LoadFolder(Root)
        Catch ex As Exception
            Logger.Warn(ex, "初始化版本文件页失败")
        End Try
    End Sub

    Private Sub NavigateUp()
        If CurrentFolder Is Nothing Then Return
        Dim Parent As String = IO.Path.GetDirectoryName(CurrentFolder.TrimEnd("\"c))
        If String.IsNullOrWhiteSpace(Parent) OrElse Not DirectoryUtils.Exists(Parent) Then Return
        LoadFolder(Parent)
    End Sub

    Private Sub LoadFolder(Folder As String)
        Try
            If String.IsNullOrWhiteSpace(Folder) OrElse Not DirectoryUtils.Exists(Folder) Then Return
            CurrentFolder = Folder
            LabPath.Text = Folder
            Entries.Clear()
            Try
                For Each Dir As String In IO.Directory.GetDirectories(Folder).OrderBy(Function(Item) IO.Path.GetFileName(Item))
                    Entries.Add(New FileEntry With {.Path = Dir, .IsFolder = True})
                Next
            Catch ex As Exception
            End Try
            Try
                For Each FilePath As String In IO.Directory.GetFiles(Folder).OrderBy(Function(Item) IO.Path.GetFileName(Item))
                    Entries.Add(New FileEntry With {.Path = FilePath, .IsFolder = False})
                Next
            Catch ex As Exception
            End Try
            RefreshList()
        Catch ex As Exception
            Logger.Warn(ex, "读取版本文件夹失败")
            LabInfo.Text = "读取文件夹失败：" & ex.Message
        End Try
    End Sub

    Private Sub RefreshList()
        Try
            Dim Filter As String = TextFilter.Text.Trim()
            ListFiles.Items.Clear()
            For Each Entry In Entries
                If Filter = "" OrElse Entry.Name.ContainsIgnoreCase(Filter) Then ListFiles.Items.Add(Entry)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "刷新文件列表失败")
        End Try
    End Sub

    Private Sub OpenSelected()
        Dim Entry As FileEntry = TryCast(ListFiles.SelectedItem, FileEntry)
        If Entry Is Nothing Then Return
        If Entry.IsFolder Then
            LoadFolder(Entry.Path)
        Else
            LoadFile(Entry.Path)
        End If
    End Sub

    Private Sub LoadFile(FilePath As String)
        Try
            If IsDirty Then
                Dim Result As Integer = MyMsgBox("当前文件尚未保存，是否保存修改？", "版本文件编辑", "保存", "不保存", "取消")
                If Result = 1 Then
                    SaveFile()
                ElseIf Result = 3 Then
                    Return
                End If
            End If
            Dim Bytes As Byte() = IO.File.ReadAllBytes(FilePath)
            If Bytes.Contains(CByte(0)) Then
                LabInfo.Text = "这是二进制文件，不能用文本编辑器修改：" & IO.Path.GetFileName(FilePath)
                Return
            End If
            Dim FileEncoding As Encoding
            Dim HasBom As Boolean
            DetectEncoding(Bytes, FileEncoding, HasBom)
            Dim Offset As Integer = If(HasBom, FileEncoding.GetPreamble().Length, 0)
            Dim Text As String = FileEncoding.GetString(Bytes, Offset, Bytes.Length - Offset)
            IsLoading = True
            TextEditor.Text = Text
            IsLoading = False
            CurrentFile = FilePath
            SaveEncoding = FileEncoding
            SaveBom = HasBom
            IsDirty = False
            BtnSave.IsEnabled = False
            UpdateEditorCard()
            LabInfo.Text = $"{IO.Path.GetFileName(FilePath)}  ·  {StringUtils.FormatByteSize(Bytes.Length)}  ·  {FileEncoding.WebName}{If(HasBom, "（带 BOM）", "")}  ·  Ctrl+S 保存"
        Catch ex As Exception
            Logger.Warn(ex, "读取文件失败")
            LabInfo.Text = "读取文件失败：" & ex.Message
        End Try
    End Sub

    Private Sub DetectEncoding(Bytes As Byte(), ByRef Result As Encoding, ByRef HasBom As Boolean)
        HasBom = False
        Result = New UTF8Encoding(False)
        Try
            If Bytes.Length >= 3 AndAlso Bytes(0) = &HEF AndAlso Bytes(1) = &HBB AndAlso Bytes(2) = &HBF Then
                Result = New UTF8Encoding(True)
                HasBom = True
                Return
            End If
            If Bytes.Length >= 2 AndAlso Bytes(0) = &HFF AndAlso Bytes(1) = &HFE Then
                Result = New UnicodeEncoding(False, True)
                HasBom = True
                Return
            End If
            If Bytes.Length >= 2 AndAlso Bytes(0) = &HFE AndAlso Bytes(1) = &HFF Then
                Result = New UnicodeEncoding(True, True)
                HasBom = True
                Return
            End If
            Try
                Dim Strict As New UTF8Encoding(False, True)
                Strict.GetString(Bytes)
                Result = New UTF8Encoding(False)
            Catch ex As DecoderFallbackException
                Result = Encoding.GetEncoding(936)
            End Try
        Catch ex As Exception
            Result = New UTF8Encoding(False)
        End Try
    End Sub

    Private Sub SaveFile()
        If CurrentFile Is Nothing Then
            LabInfo.Text = "请先在左侧双击打开要编辑的文件"
            Return
        End If
        Try
            Dim Text As String = TextEditor.Text
            Dim Content As Byte()
            Try
                Dim Preamble As Byte() = If(SaveBom, SaveEncoding.GetPreamble(), New Byte() {})
                Dim Body As Byte() = SaveEncoding.GetBytes(Text)
                Content = Preamble.Concat(Body).ToArray()
            Catch ex As Exception
                SaveEncoding = New UTF8Encoding(True)
                SaveBom = True
                Content = SaveEncoding.GetPreamble().Concat(Encoding.UTF8.GetBytes(Text)).ToArray()
                LabInfo.Text = "原编码无法表示新字符，已改为 UTF-8"
            End Try
            IO.File.WriteAllBytes(CurrentFile, Content)
            IsDirty = False
            BtnSave.IsEnabled = False
            UpdateEditorCard()
            Logger.Info($"已保存版本文件：{CurrentFile}")
            LabInfo.Text = "已保存：" & IO.Path.GetFileName(CurrentFile)
            Hint("已保存：" & IO.Path.GetFileName(CurrentFile), HintType.Green)
        Catch ex As Exception
            Logger.Warn(ex, "保存文件失败")
            LabInfo.Text = "保存失败：" & ex.Message
            Hint("保存失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub UpdateEditorCard()
        Dim Name As String = If(CurrentFile Is Nothing, "未选择文件", IO.Path.GetFileName(CurrentFile))
        CardEditor.Title = If(IsDirty, Name & "  *", Name)
    End Sub
End Class
