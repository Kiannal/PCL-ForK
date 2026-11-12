Public Class PageExperimentLeft

    Public PageID As FormMain.PageSubType = FormMain.PageSubType.Default

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub PageCheck(sender As FrameworkElement, e As RouteEventArgs) Handles ItemScreenshot.Check, ItemLitematic.Check, ItemModdev.Check, ItemSkinPaint.Check
        If sender.Tag IsNot Nothing Then
            PageID = CType(Val(sender.Tag), FormMain.PageSubType)
            Dim Target As MyPageRight = PageGet(PageID)
            AniStop("FrmMain PageChangeRight")
            If Target.Parent IsNot Nothing Then Target.SetValue(ContentPresenter.ContentProperty, Nothing)
            FrmMain.PageRight = Target
            CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnExit()
            AniStart({
                AaCode(
                Sub()
                    CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnForceExit()
                    FrmMain.PanMainRight.Child = FrmMain.PageRight
                    FrmMain.PageRight.Opacity = 0
                End Sub, 130),
                AaCode(
                Sub()
                    FrmMain.PageRight.Opacity = 1
                    FrmMain.PageRight.PageOnEnter()
                End Sub, 30, True)
            }, "PageLeft PageChange")
        End If
    End Sub

    Public Function PageGet(Optional ID As FormMain.PageSubType = FormMain.PageSubType.Default) As MyPageRight
        If ID = FormMain.PageSubType.Default Then ID = PageID
        Select Case CInt(ID)
            Case 6
                If FrmOtherLitematic Is Nothing Then FrmOtherLitematic = New PageOtherLitematic
                Return FrmOtherLitematic
            Case 7
                If FrmOtherModdev Is Nothing Then FrmOtherModdev = New PageOtherModdev
                Return FrmOtherModdev
            Case 8
                If FrmSkinPaint Is Nothing Then FrmSkinPaint = New PageSkinPaint
                Return FrmSkinPaint
            Case Else
                If FrmExperiment Is Nothing Then FrmExperiment = New PageExperiment
                Return FrmExperiment
        End Select
    End Function

End Class
