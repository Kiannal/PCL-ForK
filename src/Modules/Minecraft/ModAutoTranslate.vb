Imports System.Security.Cryptography
Imports System.Threading.Tasks
Imports System.IO.Compression
Imports System.Net.Http
Imports System.Threading
Imports System.Text
Imports System.Text.RegularExpressions

Public Module ModAutoTranslate

    Private ReadOnly HttpShared As HttpClient = CreateHttp()

    Private Function CreateHttp() As HttpClient
        Try
            Net.ServicePointManager.DefaultConnectionLimit = 64
            Net.ServicePointManager.Expect100Continue = False
        Catch ex As Exception
        End Try
        Dim C As New HttpClient
        C.Timeout = TimeSpan.FromSeconds(20)
        Return C
    End Function

        Public Const TranslateUserAgent As String = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36"

    Public Class TranslationEntry
        Public Property ModPath As String = ""
        Public Property FileName As String = ""
        Public Property HasChinese As Boolean = False
        Public Property SourcePaths As New List(Of String)
        Public Property Status As String = ""
        Public Property IsChecked As Boolean = False
        Public ReadOnly Property Display As String
            Get
                Return $"{FileName}    {Status}"
            End Get
        End Property
    End Class

    Private ReadOnly TranslateCache As New Dictionary(Of String, String)
    Private LastRequestTime As Date = Date.MinValue
    Private QuotaExhausted As Boolean = False

    Public Function ScanMods(Version As McInstance) As List(Of TranslationEntry)
        Dim Result As New List(Of TranslationEntry)
        Try
            Dim ModsFolder As String = Version.PathIndie & "mods\"
            If Not DirectoryUtils.Exists(ModsFolder) Then Return Result
            For Each Jar As String In DirectoryUtils.EnumerateFiles(ModsFolder).
                Where(Function(f) f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) OrElse f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                Dim Entry As New TranslationEntry With {.ModPath = Jar, .FileName = IO.Path.GetFileName(Jar)}
                Try
                    Using Zip As ZipArchive = FileUtils.OpenZip(Jar)
                        Dim SourceEntries As List(Of ZipArchiveEntry) =
                            Zip.Entries.Where(Function(e) Regex.IsMatch(e.FullName, "^assets/[^/]+/lang/(en_us|en_gb|en_US)\.json$", RegexOptions.IgnoreCase)).ToList()
                        Dim ChineseEntries As List(Of ZipArchiveEntry) =
                            Zip.Entries.Where(Function(e) Regex.IsMatch(e.FullName, "^assets/[^/]+/lang/zh_cn\.json$", RegexOptions.IgnoreCase)).ToList()
                        Entry.HasChinese = ChineseEntries.Any()
                        Entry.SourcePaths = SourceEntries.Select(Function(e) e.FullName).ToList()
                    End Using
                Catch ex As Exception
                    Entry.Status = "无法读取：" & ex.Message
                    Result.Add(Entry)
                    Continue For
                End Try
                If Entry.HasChinese Then
                    Entry.Status = "已有中文语言文件"
                ElseIf Entry.SourcePaths.Any() Then
                    Entry.Status = $"可汉化（{Entry.SourcePaths.Count} 个语言文件）"
                    Entry.IsChecked = True
                Else
                    Entry.Status = "没有英文语言文件"
                End If
                Result.Add(Entry)
            Next
            Return Result.OrderBy(Function(e) e.FileName).ToList()
        Catch ex As Exception
            Logger.Warn(ex, "扫描模组语言文件失败")
            Return Result
        End Try
    End Function

    Private Class TranslationJob
        Public Entry As TranslationEntry
        Public LangPath As String
        Public Source As JObject
    End Class

    Public Function TranslateMods(Version As McInstance, Entries As List(Of TranslationEntry), Report As Action(Of String)) As String
        TranslateCache.Clear()
        LoadTranslateCache()
        QuotaExhausted = False
        Try
            Dim Jobs As New List(Of TranslationJob)
            Dim JobsLock As New Object
            Dim ScanSem As New SemaphoreSlim(8)
            Dim ScanTasks As New List(Of Task)
            For Each Entry In Entries
                If Entry.HasChinese OrElse Not Entry.SourcePaths.Any() Then Continue For
                Dim CurEntry As TranslationEntry = Entry
                ScanTasks.Add(Task.Run(Sub()
                                           ScanSem.Wait()
                                           Try
                                               Using Zip As ZipArchive = FileUtils.OpenZip(CurEntry.ModPath)
                                                   For Each LangPath As String In CurEntry.SourcePaths
                                                       Dim ZipEntry As ZipArchiveEntry = Zip.GetEntry(LangPath)
                                                       If ZipEntry Is Nothing Then Continue For
                                                       Try
                                                           Dim Text As String
                                                           Using Reader As New IO.StreamReader(ZipEntry.Open(), Encoding.UTF8)
                                                               Text = Reader.ReadToEnd()
                                                           End Using
                                                           Dim Obj As JObject = JObject.Parse(Text)
                                                           SyncLock JobsLock
                                                               Jobs.Add(New TranslationJob With {.Entry = CurEntry, .LangPath = LangPath, .Source = Obj})
                                                           End SyncLock
                                                       Catch ex As Exception
                                                       End Try
                                                   Next
                                               End Using
                                           Catch ex As Exception
                                           Finally
                                               ScanSem.Release()
                                           End Try
                                       End Sub))
            Next
            Task.WaitAll(ScanTasks.ToArray())
            If Not Jobs.Any() Then
                Report("没有需要翻译的语言文件")
                Return Nothing
            End If
            Dim AllStrings As New List(Of String)
            For Each Job In Jobs
                CollectStrings(Job.Source, AllStrings)
            Next
            AllStrings = AllStrings.Distinct().ToList()
            Report($"共 {AllStrings.Count} 条文本需要翻译")
            Dim Translated As New Dictionary(Of String, String)
        Dim Need As New List(Of String)
        For Each ItemText In AllStrings
            Dim Trimmed As String = ItemText.Trim()
            If Trimmed = "" OrElse Trimmed.Length > 400 OrElse Not Regex.IsMatch(Trimmed, "[A-Za-z]") OrElse Regex.IsMatch(Trimmed, "[\u4e00-\u9fff]") Then
                Translated(ItemText) = ItemText
                TranslateCache(ItemText) = ItemText
            Else
                Need.Add(ItemText)
            End If
        Next
        Report($"实际需要翻译 {Need.Count} 条")
        Dim Done As Integer = 0
        Dim Lock As New Object
        Dim Sem As New SemaphoreSlim(12)
        Dim Tasks As New List(Of Task)
        For ChunkStart = 0 To Need.Count - 1 Step 200
            Dim StartIndex As Integer = ChunkStart
            Dim Count As Integer = Math.Min(200, Need.Count - StartIndex)
            Tasks.Add(Task.Run(Sub()
                                   Sem.Wait()
                                   Try
                                       Dim Chunk As New List(Of String)
                                       Dim TokenSets As New List(Of List(Of String))
                                       For J = 0 To Count - 1
                                           Dim Tokens As New List(Of String)
                                           Chunk.Add(ProtectTokens(Need(StartIndex + J), Tokens))
                                           TokenSets.Add(Tokens)
                                       Next
                                       Dim Half As Integer = Math.Max(1, Count - 1)
                                       Dim PartA As New List(Of String)
                                       Dim PartB As New List(Of String)
                                       For K = 0 To Count - 1
                                           If K < Half Then PartA.Add(Chunk(K)) Else PartB.Add(Chunk(K))
                                       Next
                                       Dim Ta As Task(Of List(Of String)) = Task.Run(Function() TranslateDeepSeekBatch(PartA))
                                       Dim Tb As Task(Of List(Of String)) = Task.Run(Function() TranslateGoogleBatch(PartB))
                                       Dim ResA As List(Of String) = Nothing
                                       Dim ResB As List(Of String) = Nothing
                                       If Ta.Wait(50000) Then ResA = Ta.Result
                                       If Tb.IsCompleted AndAlso Not Tb.IsFaulted Then ResB = Tb.Result
                                       If ResA Is Nothing OrElse ResA.Count <> PartA.Count Then ResA = TranslateDeepSeekBatch(PartA)
                                       If ResB Is Nothing OrElse ResB.Count <> PartB.Count Then ResB = TranslateDeepSeekBatch(PartB)
                                       Dim Results As New List(Of String)
                                       If ResA IsNot Nothing Then Results.AddRange(ResA)
                                       If ResB IsNot Nothing Then Results.AddRange(ResB)
                                       For J = 0 To Count - 1
                                           Dim Source As String = Need(StartIndex + J)
                                           Dim Value As String = Nothing
                                           If Results IsNot Nothing AndAlso Results.Count = Count AndAlso Not String.IsNullOrWhiteSpace(Results(J)) Then
                                               Value = RestoreTokens(Results(J), TokenSets(J))
                                           Else
                                               Value = Source
                                           End If
                                           SyncLock Lock
                                               Translated(Source) = Value
                                               TranslateCache(Source) = Value
                                           End SyncLock
                                       Next
                                       SyncLock Lock
                                           Done += Count
                                           Report($"翻译中…… {Done}/{Need.Count}")
                                       End SyncLock
                                   Finally
                                       Sem.Release()
                                   End Try
                               End Sub))
        Next
        Task.WaitAll(Tasks.ToArray())
        If QuotaExhausted Then Report("翻译接口额度已用尽，本次只写入已翻译的部分")
            Dim PackPath As String = WriteResourcePack(Version, Jobs, Translated, Report)
            If PackPath IsNot Nothing Then
                EnableResourcePack(Version, IO.Path.GetFileName(PackPath))
                Report($"资源包已生成并写入选项：{PackPath}")
            End If
            SaveTranslateCache()
            Return PackPath
        Catch ex As Exception
            Logger.Error(ex, "自动汉化模组失败")
            Report("自动汉化失败：" & ex.Message)
            Return Nothing
        End Try
    End Function

    Private Function WriteResourcePack(Version As McInstance, Jobs As List(Of TranslationJob),
                                       Translated As Dictionary(Of String, String), Report As Action(Of String)) As String
        Try
            Dim PackFolder As String = Version.PathIndie & "resourcepacks\"
            DirectoryUtils.Create(PackFolder)
            Dim PackPath As String = PackFolder & "PCL自动汉化.zip"
            Dim VanillaName As String = If(Version.Version IsNot Nothing, Version.Version.VanillaName, "")
            Dim PackFormat As Integer = GetPackFormat(VanillaName)
            Dim MajorFormat As Integer = 0
            Try
                If Version.JsonVersion IsNot Nothing AndAlso Version.JsonVersion("pack_version") IsNot Nothing Then
                    MajorFormat = CInt(Version.JsonVersion("pack_version")("resource_major"))
                End If
            Catch ex As Exception
            End Try
            Dim WrittenCount As Integer = 0
            Using Zip As New ZipArchive(File.Open(PackPath, FileMode.Create), ZipArchiveMode.Create)
                Dim MetaEntry As ZipArchiveEntry = Zip.CreateEntry("pack.mcmeta")
                Using Writer As New IO.StreamWriter(MetaEntry.Open(), New UTF8Encoding(False))
                    Dim Description As String = "PCL 自动汉化（" & Date.Now.ToString("yyyy-MM-dd HH:mm") & "）"
                    Dim MetaJson As String
                If MajorFormat > 0 Then
                    MetaJson = "{""pack"":{""description"":""" & Description & """,""min_format"":" & MajorFormat & ",""max_format"":" & MajorFormat & "}}"
                Else
                    MetaJson = "{""pack"":{""pack_format"":" & PackFormat & ",""description"":""" & Description & """}}"
                End If
                    Writer.Write(MetaJson)
                End Using
                Dim Written As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                For Each Job In Jobs
                    Dim ModId As String = Job.LangPath.Split("/"c)(1)
                    Dim Target As String = $"assets/{ModId}/lang/zh_cn.json"
                    If Not Written.Add(Target) Then Continue For
                    WrittenCount += 1
                    Dim Output As JObject = BuildTranslated(Job.Source, Translated)
                    Dim Entry As ZipArchiveEntry = Zip.CreateEntry(Target)
                    Using Writer As New IO.StreamWriter(Entry.Open(), New UTF8Encoding(False))
                        Writer.Write(Output.ToString())
                    End Using
                Next
            End Using
            Report($"已写入资源包，共 {WrittenCount} 个语言文件")
            Return PackPath
        Catch ex As Exception
            Logger.Error(ex, "生成汉化资源包失败")
            Report("生成汉化资源包失败：" & ex.Message)
            Return Nothing
        End Try
    End Function

    Private Sub EnableResourcePack(Version As McInstance, PackName As String)
        Try
            Dim OptionsPath As String = Version.PathIndie & "options.txt"
            Dim OptionsExists As Boolean = FileUtils.Exists(OptionsPath)
            Dim Lines As New List(Of String)
        If OptionsExists Then Lines.AddRange(IO.File.ReadAllLines(OptionsPath, Encoding.UTF8))
            Dim PackId As String = "file/" & PackName
            Dim Found As Boolean = False
            For i = 0 To Lines.Count - 1
                If Not Lines(i).StartsWith("resourcePacks:", StringComparison.OrdinalIgnoreCase) Then Continue For
                Found = True
                Dim Raw As String = Lines(i).Substring("resourcePacks:".Length).Trim()
                If Raw.Contains(PackId) Then Return
                Dim List As JArray
                Try
                    List = JArray.Parse(Raw)
                Catch ex As Exception
                    List = New JArray("vanilla")
                End Try
                List.Add(PackId)
                Lines(i) = "resourcePacks:" & List.ToString(Newtonsoft.Json.Formatting.None)
                Exit For
            Next
            If Not Found Then Lines.Add("resourcePacks:[""" & PackId & """]")
            IO.File.WriteAllLines(OptionsPath, Lines, New UTF8Encoding(False))
        Catch ex As Exception
            Logger.Warn(ex, "写入资源包选项失败")
        End Try
    End Sub

    Private Function GetPackFormat(VanillaName As String) As Integer
        Try
            If String.IsNullOrWhiteSpace(VanillaName) Then Return 15
            Dim Parts As String() = VanillaName.Split("."c)
            If Parts.Length < 2 OrElse Parts(0) <> "1" Then Return 15
            Dim Minor As Integer = Integer.Parse(Parts(1))
            If Minor >= 21 Then Return 34
            Select Case Minor
                Case 20
                    If Parts.Length >= 3 AndAlso Integer.Parse(Parts(2)) >= 2 Then Return 18
                    Return 15
                Case 19
                    Return 13
                Case 18
                    Return 9
                Case 17
                    Return 8
                Case 16
                    Return 6
                Case Else
                    Return 4
            End Select
        Catch ex As Exception
            Return 15
        End Try
    End Function

    Private Function TranslateOne(SourceText As String) As String
        If SourceText Is Nothing Then Return SourceText
        Dim Trimmed As String = SourceText.Trim()
        If Trimmed = "" Then Return SourceText
        If TranslateCache.ContainsKey(SourceText) Then Return TranslateCache(SourceText)
        If Not Regex.IsMatch(Trimmed, "[A-Za-z]") OrElse Regex.IsMatch(Trimmed, "[\u4e00-\u9fff]") Then
            TranslateCache(SourceText) = SourceText
            Return SourceText
        End If
        Dim Tokens As New List(Of String)
        Dim ProtectedText As String = ProtectTokens(SourceText, Tokens)
        Dim Result As String = TranslateRequest(ProtectedText)
        If Result Is Nothing Then Result = ProtectedText
        Result = RestoreTokens(Result, Tokens)
        TranslateCache(SourceText) = Result
        Return Result
    End Function

    Private Function TranslateRequest(SourceText As String) As String
        If QuotaExhausted Then Return Nothing
        Dim Parts As New List(Of String)
        Dim Rest As String = SourceText
        While Rest.Length > 420
            Dim Cut As Integer = Rest.LastIndexOf(" "c, 420)
            If Cut < 50 Then Cut = 420
            Parts.Add(Rest.Substring(0, Cut))
            Rest = Rest.Substring(Cut).TrimStart()
        End While
        If Rest <> "" Then Parts.Add(Rest)
        Dim Builder As New StringBuilder
        For Each Part In Parts
            Dim Piece As String = TranslateChunk(Part)
            If Piece Is Nothing Then Return Nothing
            Builder.Append(Piece)
        Next
        Return Builder.ToString()
    End Function

    Private Function TranslateChunk(SourceText As String) As String
        Try
            Dim Google As String = TranslateGoogle(SourceText)
            If Not String.IsNullOrWhiteSpace(Google) Then Return Google
        Catch ex As Exception
        End Try
        Try
            Dim Wait As Integer = 160 - CInt((Date.Now - LastRequestTime).TotalMilliseconds)
            If Wait > 0 Then Thread.Sleep(Wait)
            LastRequestTime = Date.Now
            Dim Url As String = "https://api.mymemory.translated.net/get?q=" & Uri.EscapeDataString(SourceText) & "&langpair=en|zh-CN"
            Using Client As New HttpClient
                Client.Timeout = TimeSpan.FromSeconds(20)
                Using Req As New HttpRequestMessage(HttpMethod.Get, Url)
                    Req.Headers.TryAddWithoutValidation("User-Agent", TranslateUserAgent)
                    Using Resp As HttpResponseMessage = Client.SendAsync(Req).Result
                        If Not Resp.IsSuccessStatusCode Then Return Nothing
                        Dim Json As String = Resp.Content.ReadAsStringAsync().Result
                        Dim Obj As JObject = JObject.Parse(Json)
                        If Obj("quotaFinished") IsNot Nothing AndAlso Obj("quotaFinished").ToObject(Of Boolean) Then
                            QuotaExhausted = True
                            Return Nothing
                        End If
                        Dim Translated As String = Obj("responseData")?("translatedText")?.ToString()
                        If String.IsNullOrWhiteSpace(Translated) Then Return Nothing
                        If Translated.Contains("MYMEMORY WARNING") Then
                            QuotaExhausted = True
                            Return Nothing
                        End If
                        Return Translated
                    End Using
                End Using
            End Using
        Catch ex As Exception
            Logger.Warn(ex, "在线翻译请求失败")
            Return Nothing
        End Try
    End Function

    Private Function ProtectTokens(SourceText As String, Tokens As List(Of String)) As String
        Dim Result As String = SourceText
        Result = Regex.Replace(Result, "%(\d+\$)?[sdf]", Function(Match)
                                                                   Tokens.Add(Match.Value)
                                                                   Return $"__PCLP{Tokens.Count - 1}__"
                                                               End Function)
        Result = Regex.Replace(Result, "§[0-9A-Fa-fK-Ok-or]", Function(Match)
                                                                Tokens.Add(Match.Value)
                                                                Return $"__PCLP{Tokens.Count - 1}__"
                                                            End Function)
        Result = Regex.Replace(Result, "\{\d+\}", Function(Match)
                                                           Tokens.Add(Match.Value)
                                                           Return $"__PCLP{Tokens.Count - 1}__"
                                                       End Function)
        Return Result
    End Function

    Private Function RestoreTokens(SourceText As String, Tokens As List(Of String)) As String
        Dim Result As String = SourceText
        For i = 0 To Tokens.Count - 1
            Result = Result.Replace($"__PCLP{i}__", Tokens(i))
            Result = Result.Replace($"__ PCLP {i} __", Tokens(i))
        Next
        Return Result
    End Function

    Private Sub CollectStrings(Obj As JToken, Into As List(Of String))
        If Obj Is Nothing Then Return
        Select Case Obj.Type
            Case JTokenType.Object
                For Each Prop As JProperty In CType(Obj, JObject).Properties()
                    CollectStrings(Prop.Value, Into)
                Next
            Case JTokenType.Array
                For Each Item As JToken In CType(Obj, JArray)
                    CollectStrings(Item, Into)
                Next
            Case JTokenType.String
                Dim Value As String = Obj.ToString()
                If Value.Trim() = "" Then Return
                If Not Regex.IsMatch(Value, "[A-Za-z]") Then Return
                If Regex.IsMatch(Value, "[\u4e00-\u9fff]") Then Return
                If Not Into.Contains(Value) Then Into.Add(Value)
        End Select
    End Sub

    Private Function BuildTranslated(Source As JObject, Known As Dictionary(Of String, String)) As JObject
        Return CType(TranslateToken(Source, Known), JObject)
    End Function

    Private Function TranslateToken(Source As JToken, Known As Dictionary(Of String, String)) As JToken
        If Source Is Nothing Then Return Nothing
        Select Case Source.Type
            Case JTokenType.Object
                Dim Result As New JObject
                For Each Prop As JProperty In CType(Source, JObject).Properties()
                    Result(Prop.Name) = TranslateToken(Prop.Value, Known)
                Next
                Return Result
            Case JTokenType.Array
                Dim Result As New JArray
                For Each Item As JToken In CType(Source, JArray)
                    Result.Add(TranslateToken(Item, Known))
                Next
                Return Result
            Case JTokenType.String
                Dim Value As String = Nothing
                If Known.TryGetValue(Source.ToString(), Value) Then Return New JValue(Value)
                Return New JValue(Source.ToString())
            Case Else
                Return Source.DeepClone()
        End Select
    End Function


    Private Function TranslateGoogle(SourceText As String) As String
        Try
            Dim Url As String = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=zh-CN&dt=t&q=" & Uri.EscapeDataString(SourceText)
            Using Client As New HttpClient
                Client.Timeout = TimeSpan.FromSeconds(15)
                Using Req As New HttpRequestMessage(HttpMethod.Get, Url)
                    Req.Headers.TryAddWithoutValidation("User-Agent", TranslateUserAgent)
                    Using Resp As HttpResponseMessage = Client.SendAsync(Req).Result
                        If Not Resp.IsSuccessStatusCode Then Return Nothing
                        Dim Json As String = Resp.Content.ReadAsStringAsync().Result
                        Dim Arr As JArray = JArray.Parse(Json)
                        If Arr Is Nothing OrElse Arr.Count = 0 Then Return Nothing
                        Dim Segments As JArray = TryCast(Arr(0), JArray)
                        If Segments Is Nothing Then Return Nothing
                        Dim Builder As New StringBuilder
                        For Each Seg In Segments
                            Dim S As JArray = TryCast(Seg, JArray)
                            If S IsNot Nothing AndAlso S.Count > 0 AndAlso S(0) IsNot Nothing Then Builder.Append(S(0).ToString())
                        Next
                        Dim Result As String = Builder.ToString()
                        If String.IsNullOrWhiteSpace(Result) Then Return Nothing
                        Return Result
                    End Using
                End Using
            End Using
        Catch ex As Exception
            Return Nothing
        End Try
    End Function


    Private Function TranslateGoogleBatch(Texts As List(Of String)) As List(Of String)
        Try
            Dim Body As New StringBuilder
            For Each S In Texts
                If Body.Length > 0 Then Body.Append("&")
                Body.Append("q=").Append(Uri.EscapeDataString(S))
            Next
            Using Req As New HttpRequestMessage(HttpMethod.Post, "https://translate.googleapis.com/translate_a/t?client=gtx&sl=en&tl=zh-CN")
                    Req.Headers.TryAddWithoutValidation("User-Agent", TranslateUserAgent)
                    Req.Content = New StringContent(Body.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded")
                Dim SendTask As Task(Of HttpResponseMessage) = HttpShared.SendAsync(Req)
                If Not SendTask.Wait(12000) Then Return Nothing
                Using Resp As HttpResponseMessage = SendTask.Result
                    If Not Resp.IsSuccessStatusCode Then Return Nothing
                    Dim Json As String = Resp.Content.ReadAsStringAsync().Result
                    Dim Arr As JArray = JArray.Parse(Json)
                    Dim Result As New List(Of String)
                    For Each Item In Arr
                        If Item.Type = Newtonsoft.Json.Linq.JTokenType.Array Then
                            Dim Inner As JArray = CType(Item, JArray)
                            Result.Add(If(Inner.Count > 0, Inner(0).ToString(), ""))
                        Else
                            Result.Add(Item.ToString())
                        End If
                    Next
                    Return Result
                End Using
            End Using
        Catch ex As Exception
            Return Nothing
        End Try
    End Function


    Private ReadOnly TranslateCachePath As String = PathPure.Value & "Cache\TranslateCache.json"

    Private Sub LoadTranslateCache()
        Try
            If Not FileUtils.Exists(TranslateCachePath) Then Return
            Dim Obj As JObject = JObject.Parse(FileUtils.TryReadAsString(TranslateCachePath))
            For Each Pair In Obj.Properties()
                TranslateCache(Pair.Name) = Pair.Value.ToString()
            Next
        Catch ex As Exception
        End Try
    End Sub

    Private Sub SaveTranslateCache()
        Try
            DirectoryUtils.Create(PathPure.Value & "Cache\")
            Dim Obj As New JObject
            For Each Pair In TranslateCache
                Obj(Pair.Key) = Pair.Value
            Next
            IO.File.WriteAllText(TranslateCachePath, Obj.ToString(Newtonsoft.Json.Formatting.None), New UTF8Encoding(False))
        Catch ex As Exception
        End Try
    End Sub


    Private Function TranslateDeepSeekBatch(Texts As List(Of String)) As List(Of String)
        Try
            Dim Proxy As String = Settings.Get(Of String)("DeepSeekProxy")
            Dim Key As String = GetDeepSeekKey()
            If String.IsNullOrWhiteSpace(Proxy) AndAlso String.IsNullOrWhiteSpace(Key) Then Return Nothing
            Dim InputArr As New JArray
            For Each S In Texts
                InputArr.Add(S)
            Next
            Dim Body As New JObject
            Body("model") = "deepseek-chat"
            Body("temperature") = 0
            Dim Messages As New JArray
            Messages.Add(New JObject From {{"role", "system"}, {"content", "你是翻译引擎。把用户给出的 JSON 字符串数组逐条翻译成简体中文，保持 %s、%d、§ 颜色符、换行等格式不变，只输出 JSON 字符串数组，不要解释。"}})
            Messages.Add(New JObject From {{"role", "user"}, {"content", InputArr.ToString(Newtonsoft.Json.Formatting.None)}})
            Body("messages") = Messages
            Dim Endpoint As String = If(String.IsNullOrWhiteSpace(Proxy), "https://api.deepseek.com/chat/completions", Proxy.Trim())
            Using Req As New HttpRequestMessage(HttpMethod.Post, Endpoint)
                If Not String.IsNullOrWhiteSpace(Key) AndAlso String.IsNullOrWhiteSpace(Proxy) Then Req.Headers.TryAddWithoutValidation("Authorization", "Bearer " & Key.Trim())
                Req.Content = New StringContent(Body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json")
                Dim SendTask As Task(Of HttpResponseMessage) = HttpShared.SendAsync(Req)
                If Not SendTask.Wait(12000) Then Return Nothing
                Using Resp As HttpResponseMessage = SendTask.Result
                    If Not Resp.IsSuccessStatusCode Then Return Nothing
                    Dim Json As JObject = JObject.Parse(Resp.Content.ReadAsStringAsync().Result)
                    Dim Content As String = Json("choices")(0)("message")("content").ToString().Trim()
                    If Content.StartsWithF("```") Then
                        Content = Regex.Replace(Content, "^```[a-zA-Z]*", "").Trim()
                        If Content.EndsWithF("```") Then Content = Content.Substring(0, Content.Length - 3).Trim()
                    End If
                    Dim Arr As JArray = JArray.Parse(Content)
                    If Arr.Count <> Texts.Count Then Return Nothing
                    Dim Result As New List(Of String)
                    For Each Item In Arr
                        Result.Add(If(Item.Type = Newtonsoft.Json.Linq.JTokenType.String, Item.ToString(), ""))
                    Next
                    Return Result
                End Using
            End Using
        Catch ex As Exception
            Return Nothing
        End Try
    End Function


    Private Const DefaultKeyB64 As String = ""

    Private ReadOnly AesKeyBytes As Byte() = System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("PCL-ForK-DeepSeek-2026"))

    Public Function GetDeepSeekKey() As String
        Try
            Dim Stored As String = Settings.Get(Of String)("DeepSeekApiKey")
            If String.IsNullOrWhiteSpace(Stored) Then Stored = DefaultKeyB64
            If String.IsNullOrWhiteSpace(Stored) Then Return ""
            Dim Data As Byte() = Convert.FromBase64String(Stored)
            Try
                Using Aes As System.Security.Cryptography.Aes = System.Security.Cryptography.Aes.Create()
                    Aes.Key = AesKeyBytes
                    Dim Iv(15) As Byte
                    Buffer.BlockCopy(Data, 0, Iv, 0, 16)
                    Aes.IV = Iv
                    Using Dec As System.Security.Cryptography.ICryptoTransform = Aes.CreateDecryptor()
                        Dim Plain As Byte() = Dec.TransformFinalBlock(Data, 16, Data.Length - 16)
                        Return Encoding.UTF8.GetString(Plain)
                    End Using
                End Using
            Catch ex As Exception
            End Try
            Dim Dpapi As Byte() = ProtectedData.Unprotect(Data, Nothing, DataProtectionScope.CurrentUser)
            Return Encoding.UTF8.GetString(Dpapi)
        Catch ex As Exception
            Return ""
        End Try
    End Function

    Public Function SetDeepSeekKey(Key As String) As Boolean
        Try
            If String.IsNullOrWhiteSpace(Key) Then
                Settings.Set("DeepSeekApiKey", "")
                Return True
            End If
            Dim Plain As Byte() = Encoding.UTF8.GetBytes(Key.Trim())
            Using Aes As System.Security.Cryptography.Aes = System.Security.Cryptography.Aes.Create()
                Aes.Key = AesKeyBytes
                Aes.GenerateIV()
                Using Enc As System.Security.Cryptography.ICryptoTransform = Aes.CreateEncryptor()
                    Dim Cipher As Byte() = Enc.TransformFinalBlock(Plain, 0, Plain.Length)
                    Dim Result(Cipher.Length + 15) As Byte
                    Buffer.BlockCopy(Aes.IV, 0, Result, 0, 16)
                    Buffer.BlockCopy(Cipher, 0, Result, 16, Cipher.Length)
                    Settings.Set("DeepSeekApiKey", Convert.ToBase64String(Result))
                End Using
            End Using
            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function



    Public Function AskDeepSeek(Prompt As String) As String
        Try
            Dim Proxy As String = Settings.Get(Of String)("DeepSeekProxy")
            Dim Key As String = GetDeepSeekKey()
            If String.IsNullOrWhiteSpace(Proxy) AndAlso String.IsNullOrWhiteSpace(Key) Then Return ""
            Dim Url As String = If(String.IsNullOrWhiteSpace(Proxy), "https://api.deepseek.com/chat/completions", Proxy.Trim())
            Dim Body As New JObject
            Body("model") = "deepseek-chat"
            Body("temperature") = 0.3
            Dim Messages As New JArray
            Messages.Add(New JObject From {{"role", "user"}, {"content", Prompt}})
            Body("messages") = Messages
            Using Req As New HttpRequestMessage(HttpMethod.Post, Url)
                If String.IsNullOrWhiteSpace(Proxy) Then Req.Headers.TryAddWithoutValidation("Authorization", "Bearer " & Key.Trim())
                Req.Content = New StringContent(Body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json")
                Dim SendTask As Task(Of HttpResponseMessage) = HttpShared.SendAsync(Req)
                If Not SendTask.Wait(12000) Then Return Nothing
                Using Resp As HttpResponseMessage = SendTask.Result
                    If Not Resp.IsSuccessStatusCode Then Return ""
                    Dim Json As JObject = JObject.Parse(Resp.Content.ReadAsStringAsync().Result)
                    Return Json("choices")(0)("message")("content").ToString().Trim()
                End Using
            End Using
        Catch ex As Exception
            Return ""
        End Try
    End Function

End Module
