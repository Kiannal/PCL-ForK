Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Windows.Media.Media3D

Public Module ModSkinPreview

    Public YawRotation As AxisAngleRotation3D = Nothing
    Public PitchRotation As AxisAngleRotation3D = Nothing

    Public Sub Clear(Target As Viewport3D)
        Target.Children.Clear()
        YawRotation = Nothing
        PitchRotation = Nothing
    End Sub

    Public Sub RotateBy(DX As Double, DY As Double)
        If YawRotation Is Nothing OrElse PitchRotation Is Nothing Then Return
        YawRotation.Angle += DX * 0.5
        PitchRotation.Angle = Math.Max(-60, Math.Min(60, PitchRotation.Angle + DY * 0.5))
    End Sub

    Public Sub ShowSkin(Image As BitmapImage, Slim As Boolean, Target As Viewport3D)
        Target.Children.Clear()
        Try
            Dim TexW As Integer = If(Image.PixelWidth > 0, Image.PixelWidth, 64)
            Dim TexH As Integer = If(Image.PixelHeight > 0, Image.PixelHeight, 64)
            Dim Texture As BitmapImage = MakeSharpTexture(Image)
            Dim Group As New Model3DGroup
            Group.Children.Add(New AmbientLight(Color.FromRgb(200, 200, 205)))
            Group.Children.Add(New DirectionalLight(Colors.White, New Vector3D(-0.5, -0.7, -0.9)))
            Group.Children.Add(New DirectionalLight(Color.FromRgb(80, 80, 90), New Vector3D(0.6, 0.3, 0.6)))

            Dim IsLegacy As Boolean = Image.PixelHeight <= 32
            Dim LimbW As Double = If(Slim, 3.0, 4.0)

            AddBox(Group, Texture, TexW, TexH, 0, 28, 0, 8, 8, 8,
                   New Rect(8, 8, 8, 8), New Rect(24, 8, 8, 8), New Rect(16, 8, 8, 8), New Rect(0, 8, 8, 8), New Rect(8, 0, 8, 8), New Rect(16, 0, 8, 8))
            AddBox(Group, Texture, TexW, TexH, 0, 18, 0, 8, 12, 4,
                   New Rect(20, 20, 8, 12), New Rect(32, 20, 8, 12), New Rect(16, 20, 4, 12), New Rect(28, 20, 4, 12), New Rect(20, 16, 8, 4), New Rect(28, 16, 8, 4))
            AddBox(Group, Texture, TexW, TexH, 6, 18, 0, LimbW, 12, 4,
                   New Rect(44, 20, 4, 12), New Rect(52, 20, 4, 12), New Rect(40, 20, 4, 12), New Rect(48, 20, 4, 12), New Rect(44, 16, 4, 4), New Rect(48, 16, 4, 4))
            If IsLegacy Then
                AddBox(Group, Texture, TexW, TexH, -6, 18, 0, LimbW, 12, 4,
                       New Rect(44, 20, 4, 12), New Rect(52, 20, 4, 12), New Rect(40, 20, 4, 12), New Rect(48, 20, 4, 12), New Rect(44, 16, 4, 4), New Rect(48, 16, 4, 4))
                AddBox(Group, Texture, TexW, TexH, 2, 6, 0, LimbW, 12, 4,
                       New Rect(4, 20, 4, 12), New Rect(12, 20, 4, 12), New Rect(0, 20, 4, 12), New Rect(8, 20, 4, 12), New Rect(4, 16, 4, 4), New Rect(8, 16, 4, 4))
                AddBox(Group, Texture, TexW, TexH, -2, 6, 0, LimbW, 12, 4,
                       New Rect(4, 20, 4, 12), New Rect(12, 20, 4, 12), New Rect(0, 20, 4, 12), New Rect(8, 20, 4, 12), New Rect(4, 16, 4, 4), New Rect(8, 16, 4, 4))
            Else
                AddBox(Group, Texture, TexW, TexH, -6, 18, 0, LimbW, 12, 4,
                       New Rect(36, 52, 4, 12), New Rect(44, 52, 4, 12), New Rect(32, 52, 4, 12), New Rect(40, 52, 4, 12), New Rect(36, 48, 4, 4), New Rect(40, 48, 4, 4))
                AddBox(Group, Texture, TexW, TexH, 2, 6, 0, LimbW, 12, 4,
                       New Rect(4, 20, 4, 12), New Rect(12, 20, 4, 12), New Rect(0, 20, 4, 12), New Rect(8, 20, 4, 12), New Rect(4, 16, 4, 4), New Rect(8, 16, 4, 4))
                AddBox(Group, Texture, TexW, TexH, -2, 6, 0, LimbW, 12, 4,
                       New Rect(20, 52, 4, 12), New Rect(28, 52, 4, 12), New Rect(16, 52, 4, 12), New Rect(24, 52, 4, 12), New Rect(20, 48, 4, 4), New Rect(24, 48, 4, 4))
            End If

            YawRotation = New AxisAngleRotation3D(New Vector3D(0, 1, 0), -20)
            PitchRotation = New AxisAngleRotation3D(New Vector3D(1, 0, 0), 0)
            Dim Transforms As New Transform3DGroup
            Transforms.Children.Add(New RotateTransform3D(PitchRotation))
            Transforms.Children.Add(New RotateTransform3D(YawRotation))
            Transforms.Children.Add(New TranslateTransform3D(0, -16, 0))
            Group.Transform = Transforms

            Target.Children.Add(New ModelVisual3D With {.Content = Group})
            Target.Camera = New PerspectiveCamera(New Point3D(0, 0, 64), New Vector3D(0, 0, -64), New Vector3D(0, 1, 0), 30)
        Catch ex As Exception
            Logger.Warn(ex, "生成皮肤预览失败")
        End Try
    End Sub

    Private Function MakeSharpTexture(Source As BitmapImage) As BitmapImage
        Try
            If Source.UriSource Is Nothing Then Return Source
            Dim Path As String = Source.UriSource.LocalPath
            If Not IO.File.Exists(Path) Then Return Source
            Using Src As New System.Drawing.Bitmap(Path)
                Dim W As Integer = Src.Width * 16
                Dim H As Integer = Src.Height * 16
                Using Dst As New System.Drawing.Bitmap(W, H)
                    Using G As System.Drawing.Graphics = System.Drawing.Graphics.FromImage(Dst)
                        G.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor
                        G.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half
                        G.DrawImage(Src, 0, 0, W, H)
                    End Using
                    Using Ms As New IO.MemoryStream
                        Dst.Save(Ms, System.Drawing.Imaging.ImageFormat.Png)
                        Ms.Position = 0
                        Dim Result As New BitmapImage
                        Result.BeginInit()
                        Result.CacheOption = BitmapCacheOption.OnLoad
                        Result.StreamSource = Ms
                        Result.EndInit()
                        Result.Freeze()
                        Return Result
                    End Using
                End Using
            End Using
        Catch ex As Exception
            Return Source
        End Try
    End Function

    Private Sub AddBox(Group As Model3DGroup, Image As BitmapImage, TexW As Integer, TexH As Integer, X As Double, Y As Double, Z As Double,
                       W As Double, H As Double, D As Double,
                       Front As Rect, Back As Rect, Right As Rect, Left As Rect, Top As Rect, Bottom As Rect)
        Dim Hx As Double = W / 2, Hy As Double = H / 2, Hz As Double = D / 2
        AddFace(Group, Image, TexW, TexH,
                New Point3D(X - Hx, Y - Hy, Z + Hz), New Point3D(X + Hx, Y - Hy, Z + Hz),
                New Point3D(X + Hx, Y + Hy, Z + Hz), New Point3D(X - Hx, Y + Hy, Z + Hz), Front)
        AddFace(Group, Image, TexW, TexH,
                New Point3D(X + Hx, Y - Hy, Z - Hz), New Point3D(X - Hx, Y - Hy, Z - Hz),
                New Point3D(X - Hx, Y + Hy, Z - Hz), New Point3D(X + Hx, Y + Hy, Z - Hz), Back)
        AddFace(Group, Image, TexW, TexH,
                New Point3D(X + Hx, Y - Hy, Z - Hz), New Point3D(X + Hx, Y - Hy, Z + Hz),
                New Point3D(X + Hx, Y + Hy, Z + Hz), New Point3D(X + Hx, Y + Hy, Z - Hz), Right)
        AddFace(Group, Image, TexW, TexH,
                New Point3D(X - Hx, Y - Hy, Z + Hz), New Point3D(X - Hx, Y - Hy, Z - Hz),
                New Point3D(X - Hx, Y + Hy, Z - Hz), New Point3D(X - Hx, Y + Hy, Z + Hz), Left)
        AddFace(Group, Image, TexW, TexH,
                New Point3D(X - Hx, Y + Hy, Z + Hz), New Point3D(X + Hx, Y + Hy, Z + Hz),
                New Point3D(X + Hx, Y + Hy, Z - Hz), New Point3D(X - Hx, Y + Hy, Z - Hz), Top)
        AddFace(Group, Image, TexW, TexH,
                New Point3D(X - Hx, Y - Hy, Z - Hz), New Point3D(X + Hx, Y - Hy, Z - Hz),
                New Point3D(X + Hx, Y - Hy, Z + Hz), New Point3D(X - Hx, Y - Hy, Z + Hz), Bottom)
    End Sub

    Private Sub AddFace(Group As Model3DGroup, Image As BitmapImage, TexW As Integer, TexH As Integer,
                        P1 As Point3D, P2 As Point3D, P3 As Point3D, P4 As Point3D, Uv As Rect)
        Dim W As Double = If(TexW > 0, TexW, 64)
        Dim H As Double = If(TexH > 0, TexH, 64)
        Dim U As Double = Uv.X / W
        Dim V As Double = Uv.Y / H
        Dim S As Double = Uv.Width / W
        Dim T As Double = Uv.Height / H
        Dim Brush As New ImageBrush(Image) With {
            .Viewbox = New Rect(U, V, S, T),
            .ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
            .Viewport = New Rect(0, 0, 1, 1),
            .ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            .Stretch = Stretch.Fill}
        Dim Mat As New DiffuseMaterial(Brush)
        Dim Mesh As New MeshGeometry3D
        Mesh.Positions.Add(P1)
        Mesh.Positions.Add(P2)
        Mesh.Positions.Add(P3)
        Mesh.Positions.Add(P4)
        Mesh.TextureCoordinates.Add(New Point(0, 1))
        Mesh.TextureCoordinates.Add(New Point(1, 1))
        Mesh.TextureCoordinates.Add(New Point(1, 0))
        Mesh.TextureCoordinates.Add(New Point(0, 0))
        Mesh.TriangleIndices.Add(0)
        Mesh.TriangleIndices.Add(1)
        Mesh.TriangleIndices.Add(2)
        Mesh.TriangleIndices.Add(0)
        Mesh.TriangleIndices.Add(2)
        Mesh.TriangleIndices.Add(3)
        Group.Children.Add(New GeometryModel3D(Mesh, Mat) With {.BackMaterial = Mat})
    End Sub

End Module
