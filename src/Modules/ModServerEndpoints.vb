' PCL-ForK 服务端地址配置
' 部署时把下面几项改成你自己的服务器与存储桶，其余代码无需改动。
Public Module ModServerEndpoints

    ' 你的后端服务根地址（不带末尾斜杠）
    Public Const BaseUrl As String = "http://YOUR_SERVER_HOST:8899"

    ' 游戏文件存储桶域名（对象存储，用于版本文件下载加速）
    Public Const GameFileHost As String = "your-bucket.example.com"

    ' 存档存储桶根地址（公开读，末尾保留斜杠）
    Public Const SaveHost As String = "https://YOUR_SAVE_BUCKET/"

    ' ---- 以下由 BaseUrl 派生，一般不用改 ----
    Public ReadOnly VersionUrl As String = BaseUrl & "/version.json"
    Public ReadOnly NoticeUrl As String = BaseUrl & "/notice.json"
    Public ReadOnly PagesUrl As String = BaseUrl & "/pages.php"
    Public ReadOnly ChatUrl As String = BaseUrl & "/chat.php"
    Public ReadOnly ChatApiUrl As String = BaseUrl & "/chat.php?api=1"
    Public ReadOnly FeedbackUrl As String = BaseUrl & "/feedback.php"
    Public ReadOnly UploadUrl As String = BaseUrl & "/upload.php"
    Public ReadOnly UploadByTypeUrl As String = BaseUrl & "/upload.php?type="
    Public ReadOnly ModrinthProxy As String = BaseUrl & "/modrinth.php?_p="
    Public ReadOnly GameFileProxy As String = BaseUrl & "/mcdl.php?u="

End Module
