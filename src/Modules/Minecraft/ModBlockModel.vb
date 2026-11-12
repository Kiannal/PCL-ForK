Imports System.Windows

Public Class BlockBox
    Public RotX As Double = 0, RotY As Double = 0, RotZ As Double = 0
    Public RotOriginX As Double = 0.5, RotOriginY As Double = 0.5, RotOriginZ As Double = 0.5
    Public X0 As Double, Y0 As Double, Z0 As Double
    Public X1 As Double, Y1 As Double, Z1 As Double
    Public Faces(5) As String
    Public FacePresent(5) As Boolean
End Class

Public Class BlockRenderModel
    Public Boxes As New List(Of BlockBox)
    Public IsFullCube As Boolean
End Class

Public Module ModBlockModel

    Private ReadOnly Cache As New Dictionary(Of String, BlockRenderModel)(StringComparer.OrdinalIgnoreCase)

    Public Function GetModel(BlockName As String, StateText As String) As BlockRenderModel
        Dim Key As String = BlockName & "|" & StateText
        SyncLock Cache
            If Cache.ContainsKey(Key) Then Return Cache(Key)
        End SyncLock
        Dim Model As New BlockRenderModel
        Try
            Dim Name As String = BlockName
            If Name.Contains(":") Then Name = Name.Substring(Name.IndexOf(":"c) + 1)
            Dim StateJson As JObject = ModMinecraftTextures.GetJsonAssetPublic("minecraft/blockstates/" & Name.ToLower() & ".json")
            If StateJson Is Nothing Then
                Model = FallbackCube(BlockName)
            Else
                Dim Props As Dictionary(Of String, String) = ParseProps(StateText)
                If StateJson("variants") IsNot Nothing Then
                    Dim Variants As JObject = CType(StateJson("variants"), JObject)
                    Dim Chosen As JObject = Nothing
                    For Each Pair As JProperty In Variants.Properties()
                        If VariantMatches(Pair.Name, Props) Then
                            Chosen = TryCast(Pair.Value, JObject)
                            Exit For
                        End If
                    Next
                    If Chosen Is Nothing AndAlso Variants("") IsNot Nothing Then Chosen = TryCast(Variants(""), JObject)
                    If Chosen IsNot Nothing Then
                        Dim Ref As String = ModelRefOf(Chosen)
                        If Ref IsNot Nothing Then
                            Dim RotX As Integer = GetInt(Chosen, "x")
                            Dim RotY As Integer = GetInt(Chosen, "y")
                            Model.Boxes = BuildBoxes(Ref, RotX, RotY)
                        End If
                    End If
                ElseIf StateJson("multipart") IsNot Nothing Then
                    For Each Part As JToken In CType(StateJson("multipart"), JArray)
                        Dim PartObject As JObject = TryCast(Part, JObject)
                        If PartObject Is Nothing Then Continue For
                        If PartObject("when") IsNot Nothing AndAlso Not WhenMatches(PartObject("when"), Props) Then Continue For
                        If PartObject("apply") Is Nothing Then Continue For
                        Dim Ref As String = ModelRefOf(TryCast(PartObject("apply"), JObject))
                        If Ref IsNot Nothing Then
                            Dim Apply As JObject = TryCast(PartObject("apply"), JObject)
                            Model.Boxes.AddRange(BuildBoxes(Ref, GetInt(Apply, "x"), GetInt(Apply, "y")))
                        End If
                    Next
                End If
                If Model.Boxes.Count = 0 Then Model = FallbackCube(BlockName)
            End If
            If Model.Boxes.Count = 1 Then
                Dim Box As BlockBox = Model.Boxes(0)
                Model.IsFullCube = Box.RotX = 0 AndAlso Box.RotY = 0 AndAlso Box.RotZ = 0 AndAlso
                    Box.X0 < 0.001 AndAlso Box.Y0 < 0.001 AndAlso Box.Z0 < 0.001 AndAlso
                    Box.X1 > 0.999 AndAlso Box.Y1 > 0.999 AndAlso Box.Z1 > 0.999
            End If
        Catch ex As Exception
            Logger.Warn(ex, "解析方块模型失败：" & BlockName)
            Model = FallbackCube(BlockName)
        End Try
        SyncLock Cache
            Cache(Key) = Model
        End SyncLock
        Return Model
    End Function

    Public Sub ResetModelCache()
        SyncLock Cache
            Cache.Clear()
        End SyncLock
    End Sub

    Private Function FallbackCube(BlockName As String) As BlockRenderModel
        Dim Model As New BlockRenderModel
        Dim Box As New BlockBox With {.X0 = 0, .Y0 = 0, .Z0 = 0, .X1 = 1, .Y1 = 1, .Z1 = 1}
        Dim Faces As Dictionary(Of Integer, String) = ModMinecraftTextures.GetBlockFaceTextures(BlockName)
        If Faces Is Nothing OrElse Faces.Count = 0 Then
            Dim Guessed As String = ModMinecraftTextures.GuessBlockTexturePublic(BlockName)
            If Guessed IsNot Nothing Then
                Faces = New Dictionary(Of Integer, String)
                For Direction As Integer = 0 To 5
                    Faces(Direction) = Guessed
                Next
            End If
        End If
        For Dir As Integer = 0 To 5
            Dim Path As String = Nothing
            If Faces IsNot Nothing Then Faces.TryGetValue(Dir, Path)
            Box.Faces(Dir) = Path
            Box.FacePresent(Dir) = Path IsNot Nothing
        Next
        Model.Boxes.Add(Box)
        Model.IsFullCube = True
        Return Model
    End Function

    Private Function BuildBoxes(ModelRef As String, RotX As Integer, RotY As Integer) As List(Of BlockBox)
        Dim Result As New List(Of BlockBox)
        Dim Merged As JObject = ModMinecraftTextures.LoadMergedModelPublic(ModelRef)
        If Merged Is Nothing OrElse Merged("elements") Is Nothing Then Return Result
        Dim Textures As JObject = TryCast(Merged("textures"), JObject)
        For Each ElementToken As JToken In CType(Merged("elements"), JArray)
            Dim Element As JObject = TryCast(ElementToken, JObject)
            If Element Is Nothing Then Continue For
            Dim From As JArray = TryCast(Element("from"), JArray)
            Dim [To] As JArray = TryCast(Element("to"), JArray)
            If From Is Nothing OrElse [To] Is Nothing Then Continue For
            If From.Count < 3 OrElse [To].Count < 3 Then Continue For
            Dim Box As New BlockBox With {
                .X0 = From(0).ToObject(Of Double)() / 16,
                .Y0 = From(1).ToObject(Of Double)() / 16,
                .Z0 = From(2).ToObject(Of Double)() / 16,
                .X1 = [To](0).ToObject(Of Double)() / 16,
                .Y1 = [To](1).ToObject(Of Double)() / 16,
                .Z1 = [To](2).ToObject(Of Double)() / 16}
            If Element("rotation") IsNot Nothing Then
                Dim RotationObject As JObject = TryCast(Element("rotation"), JObject)
                If RotationObject IsNot Nothing Then
                    Dim Angle As Double = 0
                    If RotationObject("angle") IsNot Nothing Then Angle = RotationObject("angle").ToObject(Of Double)()
                    Dim Axis As String = If(RotationObject("axis") Is Nothing, "", RotationObject("axis").ToString())
                    Dim OriginArray As JArray = TryCast(RotationObject("origin"), JArray)
                    If OriginArray IsNot Nothing AndAlso OriginArray.Count >= 3 Then
                        Box.RotOriginX = OriginArray(0).ToObject(Of Double)() / 16
                        Box.RotOriginY = OriginArray(1).ToObject(Of Double)() / 16
                        Box.RotOriginZ = OriginArray(2).ToObject(Of Double)() / 16
                    End If
                    Select Case Axis.ToLower()
                        Case "x" : Box.RotX = Angle
                        Case "y" : Box.RotY = Angle
                        Case "z" : Box.RotZ = Angle
                    End Select
                End If
            End If
            If Element("faces") IsNot Nothing Then
                Dim FacesObject As JObject = CType(Element("faces"), JObject)
                For Each FaceName As String In {"up", "down", "north", "south", "east", "west"}
                    Dim FaceToken As JToken = FacesObject(FaceName)
                    If FaceToken Is Nothing Then Continue For
                    Dim FaceObject As JObject = TryCast(FaceToken, JObject)
                    If FaceObject Is Nothing OrElse FaceObject("texture") Is Nothing Then Continue For
                    Dim TexturePath As String = ModMinecraftTextures.ResolveTextureRefPublic(FaceObject("texture").ToString(), Textures)
                    Dim Direction As Integer = FaceDirection(FaceName)
                    Direction = RotateDirection(Direction, RotX, RotY)
                    Box.Faces(Direction) = TexturePath
                    Box.FacePresent(Direction) = True
                Next
            End If
            RotateBox(Box, RotX, RotY)
            Result.Add(Box)
        Next
        Return Result
    End Function

    Private Function ModelRefOf(Token As JObject) As String
        If Token Is Nothing Then Return Nothing
        Dim ModelToken As JToken = Token("model")
        If ModelToken Is Nothing Then Return Nothing
        Return ModelToken.ToString()
    End Function

    Private Function GetInt(Token As JObject, Name As String) As Integer
        If Token Is Nothing OrElse Token(Name) Is Nothing Then Return 0
        Try
            Return Token(Name).ToObject(Of Integer)()
        Catch ex As Exception
            Return 0
        End Try
    End Function

    Private Function ParseProps(StateText As String) As Dictionary(Of String, String)
        Dim Result As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        If String.IsNullOrWhiteSpace(StateText) Then Return Result
        For Each Part As String In StateText.Split(","c)
            Dim Pair As String() = Part.Split("="c)
            If Pair.Length = 2 Then Result(Pair(0).Trim()) = Pair(1).Trim()
        Next
        Return Result
    End Function

    Private Function VariantMatches(Key As String, Props As Dictionary(Of String, String)) As Boolean
        If Key Is Nothing Then Return False
        Dim Parts As String() = Key.Split(","c).Where(Function(p) Not String.IsNullOrWhiteSpace(p)).ToArray()
        If Parts.Length = 0 Then Return Props.Count = 0
        For Each Part As String In Parts
            Dim Pair As String() = Part.Split("="c)
            If Pair.Length <> 2 Then Return False
            Dim Value As String = Nothing
            If Not Props.TryGetValue(Pair(0).Trim(), Value) Then Return False
            If Not String.Equals(Value, Pair(1).Trim(), StringComparison.OrdinalIgnoreCase) Then Return False
        Next
        Return True
    End Function

    Private Function WhenMatches(WhenToken As JToken, Props As Dictionary(Of String, String)) As Boolean
        If WhenToken Is Nothing Then Return True
        Dim WhenObject As JObject = TryCast(WhenToken, JObject)
        If WhenObject Is Nothing Then Return True
        If WhenObject("OR") IsNot Nothing Then
            Return CType(WhenObject("OR"), JArray).Any(Function(SubToken) WhenMatches(SubToken, Props))
        End If
        If WhenObject("AND") IsNot Nothing Then
            Return CType(WhenObject("AND"), JArray).All(Function(SubToken) WhenMatches(SubToken, Props))
        End If
        For Each Pair As JProperty In WhenObject.Properties()
            Dim Value As String = Nothing
            If Not Props.TryGetValue(Pair.Name, Value) Then Return False
            Dim Expected As String = Pair.Value.ToString()
            If Expected.Contains("|") Then
                If Not Expected.Split("|"c).Any(Function(OptionText) String.Equals(OptionText.Trim(), Value, StringComparison.OrdinalIgnoreCase)) Then Return False
            ElseIf Not String.Equals(Expected, Value, StringComparison.OrdinalIgnoreCase) Then
                Return False
            End If
        Next
        Return True
    End Function

    Private Function FaceDirection(FaceName As String) As Integer
        Select Case FaceName
            Case "up" : Return 0
            Case "down" : Return 1
            Case "north" : Return 2
            Case "south" : Return 3
            Case "west" : Return 4
            Case Else : Return 5
        End Select
    End Function

    Private Function DirectionVector(Direction As Integer) As Double()
        Select Case Direction
            Case 0 : Return {0, 1, 0}
            Case 1 : Return {0, -1, 0}
            Case 2 : Return {0, 0, -1}
            Case 3 : Return {0, 0, 1}
            Case 4 : Return {-1, 0, 0}
            Case Else : Return {1, 0, 0}
        End Select
    End Function

    Private Function RotateDirection(Direction As Integer, RotX As Integer, RotY As Integer) As Integer
        If RotX = 0 AndAlso RotY = 0 Then Return Direction
        Dim V As Double() = DirectionVector(Direction)
        Dim X As Double = V(0), Y As Double = V(1), Z As Double = V(2)
        If RotX Mod 360 <> 0 Then
            Dim Rad As Double = RotX * Math.PI / 180
            Dim CosV As Double = Math.Cos(Rad), SinV As Double = Math.Sin(Rad)
            Dim NewY As Double = Y * CosV - Z * SinV
            Dim NewZ As Double = Y * SinV + Z * CosV
            Y = NewY : Z = NewZ
        End If
        If RotY Mod 360 <> 0 Then
            Dim Rad As Double = RotY * Math.PI / 180
            Dim CosV As Double = Math.Cos(Rad), SinV As Double = Math.Sin(Rad)
            Dim NewX As Double = X * CosV + Z * SinV
            Dim NewZ As Double = -X * SinV + Z * CosV
            X = NewX : Z = NewZ
        End If
        Dim AbsX As Double = Math.Abs(X), AbsY As Double = Math.Abs(Y), AbsZ As Double = Math.Abs(Z)
        If AbsY >= AbsX AndAlso AbsY >= AbsZ Then Return If(Y > 0, 0, 1)
        If AbsZ >= AbsX Then Return If(Z > 0, 3, 2)
        Return If(X > 0, 5, 4)
    End Function

    Private Sub RotateBox(Box As BlockBox, RotX As Integer, RotY As Integer)
        If RotX Mod 360 = 0 AndAlso RotY Mod 360 = 0 Then Return
        Dim Points As Double(,) = {
            {Box.X0, Box.Y0, Box.Z0}, {Box.X1, Box.Y0, Box.Z0}, {Box.X0, Box.Y1, Box.Z0}, {Box.X1, Box.Y1, Box.Z0},
            {Box.X0, Box.Y0, Box.Z1}, {Box.X1, Box.Y0, Box.Z1}, {Box.X0, Box.Y1, Box.Z1}, {Box.X1, Box.Y1, Box.Z1}}
        Dim MinX As Double = 1, MinY As Double = 1, MinZ As Double = 1, MaxX As Double = 0, MaxY As Double = 0, MaxZ As Double = 0
        For i = 0 To 7
            Dim X As Double = Points(i, 0) - 0.5, Y As Double = Points(i, 1) - 0.5, Z As Double = Points(i, 2) - 0.5
            If RotX Mod 360 <> 0 Then
                Dim Rad As Double = RotX * Math.PI / 180
                Dim CosV As Double = Math.Cos(Rad), SinV As Double = Math.Sin(Rad)
                Dim NewY As Double = Y * CosV - Z * SinV
                Dim NewZ As Double = Y * SinV + Z * CosV
                Y = NewY : Z = NewZ
            End If
            If RotY Mod 360 <> 0 Then
                Dim Rad As Double = RotY * Math.PI / 180
                Dim CosV As Double = Math.Cos(Rad), SinV As Double = Math.Sin(Rad)
                Dim NewX As Double = X * CosV + Z * SinV
                Dim NewZ As Double = -X * SinV + Z * CosV
                X = NewX : Z = NewZ
            End If
            X += 0.5 : Y += 0.5 : Z += 0.5
            MinX = Math.Min(MinX, X) : MinY = Math.Min(MinY, Y) : MinZ = Math.Min(MinZ, Z)
            MaxX = Math.Max(MaxX, X) : MaxY = Math.Max(MaxY, Y) : MaxZ = Math.Max(MaxZ, Z)
        Next
        Box.X0 = Math.Max(0, MinX) : Box.Y0 = Math.Max(0, MinY) : Box.Z0 = Math.Max(0, MinZ)
        Box.X1 = Math.Min(1, MaxX) : Box.Y1 = Math.Min(1, MaxY) : Box.Z1 = Math.Min(1, MaxZ)
    End Sub

End Module
