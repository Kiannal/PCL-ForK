Imports System.IO
Imports System.Reflection

Public Class LitematicCatalogItem
    Public Property DisplayName As String
    Public Property FileName As String
    Public Property Author As String
    Public Property Description As String
    Public Property SizeText As String
    Public Property LogicalName As String
End Class

Public Module ModLitematicCatalog

    Public ReadOnly Items As New List(Of LitematicCatalogItem) From {
        New LitematicCatalogItem With {
            .DisplayName = "小型石屋",
            .FileName = "小型石屋.litematic",
            .Author = "PCL-ForK",
            .Description = "石砖小屋，带门窗与火把，适合示例与快速搭建",
            .SizeText = "11 × 6 × 11",
            .LogicalName = "PCL.Litematic.house.litematic"},
        New LitematicCatalogItem With {
            .DisplayName = "小麦农场",
            .FileName = "小麦农场.litematic",
            .Author = "PCL-ForK",
            .Description = "带中央水渠与围栏的小麦田",
            .SizeText = "9 × 3 × 9",
            .LogicalName = "PCL.Litematic.farm.litematic"},
        New LitematicCatalogItem With {
            .DisplayName = "刷怪塔示例",
            .FileName = "刷怪塔示例.litematic",
            .Author = "PCL-ForK",
            .Description = "含水道与收集层的刷怪塔结构示例",
            .SizeText = "13 × 14 × 13",
            .LogicalName = "PCL.Litematic.mobtower.litematic"},
        New LitematicCatalogItem With {
            .DisplayName = "下界传送门屋",
            .FileName = "下界传送门屋.litematic",
            .Author = "PCL-ForK",
            .Description = "下界风格小屋，含黑石传送门框",
            .SizeText = "7 × 6 × 7",
            .LogicalName = "PCL.Litematic.portal.litematic"}
    }

    Public Function GetBytes(Item As LitematicCatalogItem) As Byte()
        Try
            Dim Assembly As Assembly = Assembly.GetExecutingAssembly()
            Using Stream = Assembly.GetManifestResourceStream(Item.LogicalName)
                If Stream Is Nothing Then Return Nothing
                Using Memory As New MemoryStream
                    Stream.CopyTo(Memory)
                    Return Memory.ToArray()
                End Using
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "读取内置投影失败：" & Item.DisplayName)
            Return Nothing
        End Try
    End Function

    Public Function Export(Item As LitematicCatalogItem, Optional Folder As String = Nothing) As String
        If String.IsNullOrWhiteSpace(Folder) Then Folder = Path.Combine(Paths.Base, "PCL\Litematic")
        DirectoryUtils.Create(Folder)
        Dim Bytes As Byte() = GetBytes(Item)
        If Bytes Is Nothing OrElse Bytes.Length = 0 Then Throw New Exception("内置投影文件缺失")
        Dim Target As String = Path.Combine(Folder, Item.FileName)
        IO.File.WriteAllBytes(Target, Bytes)
        Return Target
    End Function

    Public Function ExportToTemp(Item As LitematicCatalogItem) As String
        Return Export(Item, Path.Combine(PathTemp, "Litematic"))
    End Function

End Module
