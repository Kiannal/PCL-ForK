Public Class FrmScreenshotView
    Inherits Window

    Public Sub New(FilePath As String)
        Title = "截图预览 - " & IO.Path.GetFileName(FilePath)
        Width = 960
        Height = 640
        WindowStartupLocation = WindowStartupLocation.CenterScreen
        Background = New SolidColorBrush(Color.FromRgb(20, 20, 24))
        Dim Grid As New Grid
        Dim Img As New Image With {.Stretch = Stretch.Uniform, .Margin = New Thickness(10)}
        Try
            Dim Bit As New BitmapImage
            Bit.BeginInit()
            Bit.CacheOption = BitmapCacheOption.OnLoad
            Bit.UriSource = New Uri(FilePath)
            Bit.EndInit()
            Bit.Freeze()
            Img.Source = Bit
        Catch ex As Exception
        End Try
        Grid.Children.Add(Img)
        Dim Lab As New TextBlock With {
            .Text = IO.Path.GetFileName(FilePath),
            .Foreground = Brushes.White,
            .FontSize = 13,
            .Margin = New Thickness(14, 0, 14, 10),
            .VerticalAlignment = VerticalAlignment.Bottom,
            .HorizontalAlignment = HorizontalAlignment.Left}
        Grid.Children.Add(Lab)
        Content = Grid
        AddHandler MouseLeftButtonDown, Sub() Close()
    End Sub

End Class
