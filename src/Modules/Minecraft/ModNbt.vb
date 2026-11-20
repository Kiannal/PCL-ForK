Imports System.IO
Imports System.IO.Compression
Imports System.Text

Public Module ModNbt

    Public Class NbtTag
        Public Property TagType As Byte
        Public Property Name As String = ""
        Public Property Value As Object
        Public Function AsCompound() As NbtCompound
            Return TryCast(Value, NbtCompound)
        End Function
    End Class

    Public Class NbtCompound
        Public ReadOnly Tags As New List(Of NbtTag)
        Default Public Property Item(Key As String) As NbtTag
            Get
                Return Tags.FirstOrDefault(Function(t) t.Name = Key)
            End Get
            Set(value As NbtTag)
                Dim Existing As NbtTag = Tags.FirstOrDefault(Function(t) t.Name = Key)
                If Existing IsNot Nothing Then
                    If value Is Nothing Then
                        Tags.Remove(Existing)
                    Else
                        value.Name = Key
                        Tags(Tags.IndexOf(Existing)) = value
                    End If
                ElseIf value IsNot Nothing Then
                    value.Name = Key
                    Tags.Add(value)
                End If
            End Set
        End Property
    End Class

    Public Class NbtFile
        Public Property Root As NbtTag
    End Class

    Public Function ReadFile(Path As String) As NbtFile
        Using Stream As New FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            Using Gzip As New GZipStream(Stream, CompressionMode.Decompress)
                Using Reader As New BinaryReader(Gzip, Encoding.UTF8)
                    Dim RootType As Byte = Reader.ReadByte()
                    If RootType = 0 Then Throw New Exception("NBT 文件为空")
                    Dim RootName As String = ReadString(Reader)
                    Dim Root As New NbtTag With {.TagType = RootType, .Name = RootName, .Value = ReadPayload(RootType, Reader)}
                    Return New NbtFile With {.Root = Root}
                End Using
            End Using
        End Using
    End Function

    Public Function ReadGzipBytes(Path As String) As Byte()
        Using Stream As New FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            Using Gzip As New GZipStream(Stream, CompressionMode.Decompress)
                Using Memory As New MemoryStream
                    Gzip.CopyTo(Memory)
                    Return Memory.ToArray()
                End Using
            End Using
        End Using
    End Function

    Public Sub WriteFile(Path As String, File As NbtFile)
        Using Stream As New FileStream(Path, FileMode.Create, FileAccess.Write)
            Using Gzip As New GZipStream(Stream, CompressionMode.Compress)
                Using Writer As New BinaryWriter(Gzip, Encoding.UTF8)
                    Writer.Write(File.Root.TagType)
                    WriteString(Writer, File.Root.Name)
                    WritePayload(Writer, File.Root.TagType, File.Root.Value)
                End Using
            End Using
        End Using
    End Sub

    Private Function ReadString(Reader As BinaryReader) As String
        Dim Length As Integer = ReadUShort(Reader)
        If Length = 0 Then Return ""
        Dim Bytes As Byte() = Reader.ReadBytes(Length)
        Return Encoding.UTF8.GetString(Bytes)
    End Function

    Private Sub WriteString(Writer As BinaryWriter, Value As String)
        Dim Bytes As Byte() = Encoding.UTF8.GetBytes(If(Value, ""))
        WriteUShort(Writer, Bytes.Length)
        Writer.Write(Bytes)
    End Sub

    Private Function ReadUShort(Reader As BinaryReader) As Integer
        Dim Bytes As Byte() = Reader.ReadBytes(2)
        If Bytes.Length < 2 Then Return 0
        Return (CInt(Bytes(0)) << 8) Or Bytes(1)
    End Function

    Private Sub WriteUShort(Writer As BinaryWriter, Value As Integer)
        Writer.Write(CByte((Value >> 8) And &HFF))
        Writer.Write(CByte(Value And &HFF))
    End Sub

    Private Function ReadPayload(TagType As Byte, Reader As BinaryReader) As Object
        Select Case TagType
            Case 1
                Return Reader.ReadByte()
            Case 2
                Dim Bytes As Byte() = Reader.ReadBytes(2)
                Array.Reverse(Bytes)
                Return BitConverter.ToInt16(Bytes, 0)
            Case 3
                Dim Bytes As Byte() = Reader.ReadBytes(4)
                Array.Reverse(Bytes)
                Return BitConverter.ToInt32(Bytes, 0)
            Case 4
                Dim Bytes As Byte() = Reader.ReadBytes(8)
                Array.Reverse(Bytes)
                Return BitConverter.ToInt64(Bytes, 0)
            Case 5
                Dim Bytes As Byte() = Reader.ReadBytes(4)
                Array.Reverse(Bytes)
                Return BitConverter.ToSingle(Bytes, 0)
            Case 6
                Dim Bytes As Byte() = Reader.ReadBytes(8)
                Array.Reverse(Bytes)
                Return BitConverter.ToDouble(Bytes, 0)
            Case 7
                Dim Length As Integer = ReadInt(Reader)
                Return Reader.ReadBytes(Length)
            Case 8
                Return ReadString(Reader)
            Case 9
                Dim ItemType As Byte = Reader.ReadByte()
                Dim Length As Integer = ReadInt(Reader)
                Dim Items As New List(Of Object)
                For i = 1 To Length
                    Items.Add(ReadPayload(ItemType, Reader))
                Next
                Return New NbtList With {.ItemType = ItemType, .Items = Items}
            Case 10
                Dim Compound As New NbtCompound
                Do
                    Dim ChildType As Byte = Reader.ReadByte()
                    If ChildType = 0 Then Exit Do
                    Dim ChildName As String = ReadString(Reader)
                    Compound.Tags.Add(New NbtTag With {.TagType = ChildType, .Name = ChildName, .Value = ReadPayload(ChildType, Reader)})
                Loop
                Return Compound
            Case 11
                Dim Length As Integer = ReadInt(Reader)
                Dim Result As New List(Of Integer)
                For i = 1 To Length
                    Result.Add(ReadInt(Reader))
                Next
                Return Result
            Case 12
                Dim Length As Integer = ReadInt(Reader)
                Dim Result As New List(Of Long)
                For i = 1 To Length
                    Dim Bytes As Byte() = Reader.ReadBytes(8)
                    Dim Value As Long = 0
                    For j = 0 To 7
                        Value = (Value << 8) Or Bytes(j)
                    Next
                    Result.Add(Value)
                Next
                Return Result
            Case Else
                Throw New Exception("未知的 NBT 标签类型：" & TagType)
        End Select
    End Function

    Private Function ReadInt(Reader As BinaryReader) As Integer
        Dim Bytes As Byte() = Reader.ReadBytes(4)
        Array.Reverse(Bytes)
        Return BitConverter.ToInt32(Bytes, 0)
    End Function

    Public Class NbtList
        Public Property ItemType As Byte
        Public Property Items As New List(Of Object)
    End Class

    Private Sub WritePayload(Writer As BinaryWriter, TagType As Byte, Value As Object)
        Select Case TagType
            Case 1
                Writer.Write(CByte(Value))
            Case 2
                Dim Number As Integer = CInt(CShort(Value))
                Writer.Write(CByte((Number >> 8) And &HFF))
                Writer.Write(CByte(Number And &HFF))
            Case 3
                WriteInt(Writer, CInt(Value))
            Case 4
                Dim NumberBytes As Byte() = BitConverter.GetBytes(CLng(Value))
                Array.Reverse(NumberBytes)
                Writer.Write(NumberBytes)
            Case 5
                Dim Bytes As Byte() = BitConverter.GetBytes(CSng(Value))
                Array.Reverse(Bytes)
                Writer.Write(Bytes)
            Case 6
                Dim Bytes As Byte() = BitConverter.GetBytes(CDbl(Value))
                Array.Reverse(Bytes)
                Writer.Write(Bytes)
            Case 7
                Dim Bytes As Byte() = CType(Value, Byte())
                WriteInt(Writer, Bytes.Length)
                Writer.Write(Bytes)
            Case 8
                WriteString(Writer, CStr(Value))
            Case 9
                Dim List As NbtList = CType(Value, NbtList)
                Writer.Write(List.ItemType)
                WriteInt(Writer, List.Items.Count)
                For Each Item In List.Items
                    WritePayload(Writer, List.ItemType, Item)
                Next
            Case 10
                Dim Compound As NbtCompound = CType(Value, NbtCompound)
                For Each Tag In Compound.Tags
                    Writer.Write(Tag.TagType)
                    WriteString(Writer, Tag.Name)
                    WritePayload(Writer, Tag.TagType, Tag.Value)
                Next
                Writer.Write(CByte(0))
            Case 11
                Dim Numbers As List(Of Integer) = CType(Value, List(Of Integer))
                WriteInt(Writer, Numbers.Count)
                For Each Number In Numbers
                    WriteInt(Writer, Number)
                Next
            Case 12
                Dim Numbers As List(Of Long) = CType(Value, List(Of Long))
                WriteInt(Writer, Numbers.Count)
                For Each Number In Numbers
                    For i = 7 To 0 Step -1
                        Writer.Write(CByte((Number >> (8 * i)) And &HFF))
                    Next
                Next
        End Select
    End Sub

    Public Sub WriteInt(Writer As BinaryWriter, Value As Integer)
        Writer.Write(CByte((Value >> 24) And &HFF))
        Writer.Write(CByte((Value >> 16) And &HFF))
        Writer.Write(CByte((Value >> 8) And &HFF))
        Writer.Write(CByte(Value And &HFF))
    End Sub

    Public Function GetCompoundChild(Parent As NbtCompound, ChildName As String) As NbtCompound
        Try
            Dim Tag As NbtTag = Parent?.Item(ChildName)
            Return TryCast(Tag?.Value, NbtCompound)
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Public Function GetCompound(Parent As NbtTag, ChildName As String) As NbtCompound
        Dim Compound As NbtCompound = TryCast(Parent?.Value, NbtCompound)
        If Compound Is Nothing Then Return Nothing
        Dim Child As NbtTag = Compound(ChildName)
        If Child Is Nothing Then Return Nothing
        Return TryCast(Child.Value, NbtCompound)
    End Function

    Public Function GetValue(Compound As NbtCompound, Key As String) As Object
        Try
            Dim Tag As NbtTag = Compound?.Item(Key)
            If Tag Is Nothing Then Return Nothing
            Return Tag.Value
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Public Sub SetValue(Compound As NbtCompound, Key As String, TagType As Byte, Value As Object)
        Compound(Key) = New NbtTag With {.TagType = TagType, .Value = Value}
    End Sub

End Module
