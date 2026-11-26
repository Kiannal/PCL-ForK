Public Class MyComboBox
    Inherits ComboBox
    Implements ISettingControl
    Public Event TextChanged(sender As Object, e As TextChangedEventArgs)

    '基础
    Public Uuid As Integer = GetUuid()
    Private TextBox As MyTextBox
    Public Property HintText As String = ""
    Public Overrides Sub OnApplyTemplate()
        MyBase.OnApplyTemplate()
        If Not IsEditable Then Return
        Try
            TextBox = Template.FindName("PART_EditableTextBox", Me)
            TextBox.AddHandler(LostFocusEvent, New RoutedEventHandler(AddressOf RefreshColor))
            TextBox.ChangedEventList.Add(New RoutedEventHandler(Sub(sender, e) RaiseEvent TextChanged(sender, e)))
            TextBox.Tag = Tag '有时需要用文本框的 Tag 来写入设置
            If Text = "" Then
                TextBox.Text = _Text
            Else
                RaiseEvent TextChanged(Me, Nothing)
            End If
            If HintText.Length > 0 Then TextBox.HintText = HintText
        Catch ex As Exception
            Logger.Error(ex, $"初始化可编辑文本框失败（{If(Name, "")}）")
        End Try
    End Sub
    Private _Text As String = SelectedItem
    Public Shadows Property Text As String
        Get
            If IsEditable Then
                If TextBox Is Nothing Then
                    Return If(_Text, "")
                Else
                    Return If(TextBox.Text, "")
                End If
            Else
                Return If(SelectedItem, "").ToString
            End If
        End Get
        Set(value As String)
            If IsEditable Then
                If IsNothing(TextBox) Then
                    _Text = value
                Else
                    TextBox.Text = value
                End If
            Else
                Throw New NotSupportedException("该 ComboBox 不支持修改文本。")
            End If
        End Set
    End Property

    '鼠标按下接口
    Private IsMouseDown As Boolean = False
    Private Sub MyComboBox_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonDown
        IsMouseDown = True
    End Sub
    Private Sub MyComboBox_PreviewMouseLeftButtonUp(sender As Object, e As EventArgs) Handles Me.PreviewMouseLeftButtonUp, Me.MouseLeave
        IsMouseDown = False
    End Sub

    '指向动画
    Public Sub RefreshColor() Handles Me.IsEnabledChanged, Me.MouseEnter, Me.MouseLeave, Me.PreviewMouseLeftButtonDown, Me.PreviewMouseLeftButtonUp, Me.GotKeyboardFocus
        '判断当前颜色
        Dim ForeColorName As String
        Dim BackColorName As String
        Dim Time As Integer
        If IsEnabled Then
            If IsMouseDown OrElse IsDropDownOpen OrElse (IsEditable AndAlso Template.FindName("PART_EditableTextBox", Me).IsFocused) Then
                ForeColorName = "ColorBrush3"
                BackColorName = "ColorBrush7"
                Time = 10
            ElseIf IsMouseOver Then
                ForeColorName = "ColorBrush4"
                BackColorName = "ColorBrush7"
                Time = 100
            Else
                ForeColorName = "ColorBrushBg0"
                BackColorName = "ColorBrushHalfWhite"
                Time = 100
            End If
        Else
            ForeColorName = "ColorBrushGray5"
            BackColorName = "ColorBrushGray6"
            Time = 200
        End If
        '触发颜色动画
        If IsLoaded AndAlso AniControlEnabled = 0 Then '防止默认属性变更触发动画
            '有动画
            AniStart({
                     AaColor(Me, ForegroundProperty, ForeColorName, Time),
                     AaColor(Me, BackgroundProperty, BackColorName, Time)
                 }, "MyComboBox Color " & Uuid)
        Else
            '无动画
            AniStop("MyComboBox Color " & Uuid)
            SetResourceReference(ForegroundProperty, ForeColorName)
            SetResourceReference(BackgroundProperty, BackColorName)
        End If
    End Sub

    Private RealWidth As Double = Double.NaN '未改动过宽度时保持 NaN
    Private Sub MyComboBox_DropDownOpened(sender As Object, e As EventArgs) Handles Me.DropDownOpened
        '自动宽度才需要临时固定，避免首次打开时 dropDownBorder 重排导致抖动
        If Double.IsNaN(Width) Then
            RealWidth = Width
            Width = ActualWidth
        End If
        '初值由 XAML 静态给出（Opacity=0、Y=-8），这里只负责推到终值
        Try
            Dim Host As FrameworkElement = TryCast(Template.FindName("PanPopup", Me), FrameworkElement)
            If Host Is Nothing Then Return
            Dim Target As Double = If(FrmMain Is Nothing, 1.0, FrmMain.Opacity)
            Host.Opacity = 0
            Dim Trans As TranslateTransform = TryCast(Host.RenderTransform, TranslateTransform)
            If Trans Is Nothing Then
                Trans = New TranslateTransform(0, -8)
                Host.RenderTransform = Trans
                Host.RenderTransformOrigin = New Point(0.5, 0)
            ElseIf Trans.IsFrozen Then
                '模板共享的变换会被冻结，克隆一份可写的再改
                Trans = DirectCast(Trans.Clone(), TranslateTransform)
                Host.RenderTransform = Trans
            End If
            Trans.Y = -8
            AniStart({
                AaOpacity(Host, Target, 180, 0, New AniEaseOutFluent),
                AaTranslateY(Host, 8, 240, 0, New AniEaseOutFluent(AniEasePower.Weak))
            }, "MyComboBox Drop " & Uuid, True)
        Catch ex As Exception
            Logger.Error(ex, "下拉入场动画失败")
        End Try
    End Sub
    Private Sub MyComboBox_DropDownClosed(sender As Object, e As EventArgs) Handles Me.DropDownClosed
        If Not Double.IsNaN(RealWidth) Then
            Width = RealWidth
            RealWidth = Double.NaN
        End If
        '关闭后复位初值，下次打开时动画起点已就绪
        Try
            Dim Host As FrameworkElement = TryCast(Template.FindName("PanPopup", Me), FrameworkElement)
            If Host IsNot Nothing Then
                Host.Opacity = 0
                Dim Trans As TranslateTransform = TryCast(Host.RenderTransform, TranslateTransform)
                If Trans IsNot Nothing AndAlso Not Trans.IsFrozen Then
                    Trans.Y = -8
                ElseIf Trans IsNot Nothing AndAlso Trans.IsFrozen Then
                    Dim Copy As TranslateTransform = DirectCast(Trans.Clone(), TranslateTransform)
                    Copy.Y = -8
                    Host.RenderTransform = Copy
                End If
            End If
        Catch
        End Try
    End Sub

    '修复 WPF Bug：下拉框文本修改后，依然误认为还选择着此前的选项，导致再次点击该选项时内容不变
    Private IsTextChanging As Boolean = False
    Private Sub MyComboBox_TextChanged(sender As Object, e As TextChangedEventArgs) Handles Me.TextChanged
        If IsTextChanging OrElse Not IsEditable Then Return
        If SelectedItem Is Nothing OrElse Text = SelectedItem.ToString Then Return
        Dim RawText As String = Text
        Dim RawSelectionStart As Integer = TextBox.SelectionStart
        IsTextChanging = True
        SelectedItem = Nothing
        Text = RawText
        TextBox.SelectionStart = RawSelectionStart
        IsTextChanging = False
    End Sub

    Public ReadOnly Property ContentPresenter As ContentPresenter
        Get
            Return Template.FindName("PART_Content", Me)
        End Get
    End Property

    Private Sub MyComboBox_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles Me.SelectionChanged
        If IsLoaded AndAlso AniControlEnabled = 0 Then RaiseCustomEvent()
    End Sub

#Region "设置"

    Private Sub RefreshSetting(NewValue As String) Implements ISettingControl.RefreshSetting
        Dim TargetItem = Items.OfType(Of DependencyObject)().FirstOrDefault(Function(i) Val(If(SettingService.GetValue(i), -1)) = NewValue)
        If TargetItem IsNot Nothing Then
            SelectedItem = TargetItem
        ElseIf Items.Count > NewValue Then
            SelectedIndex = NewValue
        Else
            Logger.Warn($"尝试显示设置 {SettingService.GetKey(Me)}，但没有找到设置值 {NewValue} 的对应项")
            SelectedIndex = -1
            SelectedItem = Nothing
        End If
    End Sub

    Private Function GetCurrentSetting() As String Implements ISettingControl.GetCurrentSetting
        If SelectedItem Is Nothing Then Return Nothing
        If TypeOf SelectedItem Is DependencyObject Then
            Return If(SettingService.GetValue(SelectedItem), SelectedIndex)
        Else
            Return SelectedIndex
        End If
    End Function

    Private Sub SaveSetting() Handles Me.SelectionChanged
        If SelectedItem Is Nothing Then Return
        SettingService.SaveSetting(Me)
    End Sub

#End Region

End Class
