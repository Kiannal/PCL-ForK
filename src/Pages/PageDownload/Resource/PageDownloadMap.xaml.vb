Public Class PageDownloadMap

    Private Sub PageDownloadMap_PageEnter() Handles Me.PageEnter
        ModMain.RequestPclSource("map")
    End Sub

End Class
