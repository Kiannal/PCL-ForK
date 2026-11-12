Imports System.IO
Imports System.Text
Imports System.Diagnostics

Public Module ModModDev

    Private ReadOnly LoaderDefaults As New Dictionary(Of String, String) From {
        {"forge", "1.20.1"},
        {"neoforge", "1.21.1"},
        {"fabric", "1.20.1"}
    }

    Public Function GetDefaultMcVersion(Loader As String) As String
        Return If(LoaderDefaults.ContainsKey(Loader), LoaderDefaults(Loader), "1.20.1")
    End Function

    Public Function HasInvalidModId(Value As String) As Boolean
        If String.IsNullOrWhiteSpace(Value) Then Return True
        For Each C As Char In Value
            If Not (Char.IsLower(C) OrElse Char.IsDigit(C) OrElse C = "_"c) Then Return True
        Next
        Return False
    End Function

    Public Function GetProjectFolder(Folder As String, ModId As String) As String
        If String.IsNullOrWhiteSpace(Folder) OrElse String.IsNullOrWhiteSpace(ModId) Then Return ""
        Return IO.Path.Combine(Folder, ModId.Trim().ToLower())
    End Function

    Public Function Generate(ModId As String, ModName As String, Package As String, ModVersion As String,
                             Author As String, Loader As String, McVersion As String, Folder As String) As String
        ModId = If(ModId, "").Trim().ToLower()
        ModName = If(ModName, "").Trim()
        Package = If(Package, "").Trim()
        ModVersion = If(ModVersion, "").Trim()
        Author = If(Author, "").Trim()
        Loader = If(Loader, "forge").Trim().ToLower()
        McVersion = If(McVersion, "").Trim()
        Folder = If(Folder, "").Trim()
        If HasInvalidModId(ModId) Then Throw New Exception("ModID 只能包含小写字母、数字和下划线")
        If String.IsNullOrWhiteSpace(Package) OrElse Not Package.Contains(".") Then Throw New Exception("包名格式不正确，例如 com.example.examplemod")
        If String.IsNullOrWhiteSpace(Folder) Then Throw New Exception("请选择项目位置")
        If String.IsNullOrWhiteSpace(ModName) Then ModName = ModId
        If String.IsNullOrWhiteSpace(ModVersion) Then ModVersion = "1.0.0"
        If String.IsNullOrWhiteSpace(Author) Then Author = "开发者"
        If Not LoaderDefaults.ContainsKey(Loader) Then Loader = "forge"
        If String.IsNullOrWhiteSpace(McVersion) Then McVersion = LoaderDefaults(Loader)

        Dim ProjectFolder As String = IO.Path.Combine(Folder, ModId)
        Directory.CreateDirectory(ProjectFolder)
        Dim ClassName As String = BuildClassName(ModId)
        Dim JavaFolder As String = IO.Path.Combine(ProjectFolder, "src\main\java", Package.Replace("."c, "\"c))
        Dim ResourceFolder As String = IO.Path.Combine(ProjectFolder, "src\main\resources")
        Directory.CreateDirectory(JavaFolder)
        Directory.CreateDirectory(ResourceFolder)
        CleanModMetadata(ProjectFolder, ModId)

        WriteFile(ProjectFolder, ".gitignore", TextFile(
            ".gradle/", "build/", "run/", ".idea/", "*.iml", "out/", "*.log"))
        WriteFile(ProjectFolder, "README.md", BuildReadme(ModName, ModId, Loader, McVersion, Package, ClassName))
        WriteFile(ProjectFolder, "gradle.properties", BuildGradleProperties(ModId, ModName, ModVersion, Author, Package, Loader, McVersion))

        Select Case Loader
            Case "fabric"
                WriteFile(ProjectFolder, "build.gradle", BuildGradleFabric(McVersion))
                WriteFile(ProjectFolder, "settings.gradle", BuildSettingsFabric(ModId))
                WriteFile(ProjectFolder, "src\main\java\" & Package.Replace("."c, "\"c) & "\" & ClassName & ".java", BuildJavaFabric(Package, ClassName, ModId))
                WriteFile(ProjectFolder, "src\main\resources\fabric.mod.json", BuildFabricModJson(ModId, ModName, ModVersion, Author, Package, ClassName, McVersion))
                WriteFile(ProjectFolder, "src\main\resources\" & ModId & ".mixins.json", BuildMixinsJson(Package))
                WriteFile(ProjectFolder, "src\main\resources\pack.mcmeta", BuildPackMcmeta(15))
            Case "neoforge"
                WriteFile(ProjectFolder, "build.gradle", BuildGradleNeo(McVersion))
                WriteFile(ProjectFolder, "settings.gradle", BuildSettingsNeo(ModId))
                WriteFile(ProjectFolder, "src\main\java\" & Package.Replace("."c, "\"c) & "\" & ClassName & ".java", BuildJavaNeo(Package, ClassName, ModId))
                WriteFile(ProjectFolder, "src\main\resources\META-INF\neoforge.mods.toml", BuildNeoModsToml(ModId, ModName, ModVersion, McVersion))
                WriteFile(ProjectFolder, "src\main\resources\pack.mcmeta", BuildPackMcmeta(34))
            Case Else
                WriteFile(ProjectFolder, "build.gradle", BuildGradleForge(ModId, McVersion))
                WriteFile(ProjectFolder, "settings.gradle", BuildSettingsForge(ModId))
                WriteFile(ProjectFolder, "src\main\java\" & Package.Replace("."c, "\"c) & "\" & ClassName & ".java", BuildJavaForge(Package, ClassName, ModId))
                WriteFile(ProjectFolder, "src\main\resources\META-INF\mods.toml", BuildForgeModsToml(ModId, ModName, ModVersion, McVersion))
                WriteFile(ProjectFolder, "src\main\resources\pack.mcmeta", BuildPackMcmeta(15))
        End Select
        Return ProjectFolder
    End Function

    Public Function EnvReport() As String
        Dim JavaText As String = RunCommand("java", "-version")
        Dim GradleText As String = RunCommand("gradle", "-v")
        Dim GitText As String = RunCommand("git", "--version")
        Dim JavaHome As String = If(Environment.GetEnvironmentVariable("JAVA_HOME"), "")
        Dim Sb As New StringBuilder()
        Sb.AppendLine("Java：" & If(JavaText.Contains("version"), JavaText.BeforeFirst(vbLf).Trim(), "未找到（请安装 JDK 并配置 PATH）"))
        If JavaText.Contains("version") AndAlso JavaText.Contains("21") Then Sb.AppendLine("  当前是 Java 21，适合 NeoForge 1.21.1")
        If JavaText.Contains("version") AndAlso JavaText.Contains("17") Then Sb.AppendLine("  当前是 Java 17，适合 Forge/Fabric 1.20.1")
        Sb.AppendLine("JAVA_HOME：" & If(String.IsNullOrWhiteSpace(JavaHome), "未设置", JavaHome))
        Sb.AppendLine("Gradle：" & If(GradleText.Contains("Gradle"), GradleText.BeforeFirst(vbLf).Trim(), "未找到（可安装 Gradle 或使用 IDE 内置）"))
        Sb.AppendLine("Git：" & If(GitText.Contains("git version"), GitText.Trim(), "未找到（可选）"))
        Return Sb.ToString()
    End Function

    Private Function RunCommand(FileName As String, Arguments As String) As String
        Try
            Dim Info As New ProcessStartInfo(FileName, Arguments) With {
                .UseShellExecute = False,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .CreateNoWindow = True}
            Using Proc As Process = Process.Start(Info)
                Dim Out As String = Proc.StandardOutput.ReadToEnd()
                Dim Err As String = Proc.StandardError.ReadToEnd()
                Proc.WaitForExit(5000)
                Return If(Out.Length > 0, Out, Err)
            End Using
        Catch ex As Exception
            Return ""
        End Try
    End Function

    Private Function BuildClassName(ModId As String) As String
        Dim Base As String = ModId.BeforeFirst("_").Replace("-", "").Replace(".", "")
        If Base.Length = 0 Then Base = "Examplemod"
        Return Char.ToUpper(Base(0)) & If(Base.Length > 1, Base.Substring(1), "")
    End Function

    Private Sub WriteFile(Folder As String, RelativePath As String, Content As String)
        Dim FullPath As String = IO.Path.Combine(Folder, RelativePath)
        Directory.CreateDirectory(IO.Path.GetDirectoryName(FullPath))
        IO.File.WriteAllText(FullPath, Content, New UTF8Encoding(False))
    End Sub

    Private Sub CleanModMetadata(ProjectFolder As String, ModId As String)
        For Each RelativePath As String In {
            "src\main\resources\META-INF\mods.toml",
            "src\main\resources\META-INF\neoforge.mods.toml",
            "src\main\resources\fabric.mod.json",
            "src\main\resources\" & ModId & ".mixins.json"}
            Try
                Dim FullPath As String = IO.Path.Combine(ProjectFolder, RelativePath)
                If IO.File.Exists(FullPath) Then IO.File.Delete(FullPath)
            Catch ex As Exception
            End Try
        Next
    End Sub

    Private Function TextFile(ParamArray Lines() As String) As String
        Return String.Join(vbCrLf, Lines) & vbCrLf
    End Function

    Private Function BuildReadme(ModName As String, ModId As String, Loader As String, McVersion As String, Package As String, ClassName As String) As String
        Dim JavaVersion As String = If(Loader = "neoforge", "21", "17")
        Dim Sb As New StringBuilder()
        Sb.AppendLine("# " & ModName)
        Sb.AppendLine()
        Sb.AppendLine("Minecraft " & McVersion & " / " & Loader & " 模组项目，由 PCL-ForK 生成。")
        Sb.AppendLine()
        Sb.AppendLine("## 需要")
        Sb.AppendLine()
        Sb.AppendLine("- JDK " & JavaVersion)
        Sb.AppendLine("- IntelliJ IDEA（推荐）")
        Sb.AppendLine("- 可访问 Maven 的网络")
        Sb.AppendLine()
        Sb.AppendLine("## 开发")
        Sb.AppendLine()
        Sb.AppendLine("1. 用 IntelliJ IDEA 打开本文件夹，等待 Gradle 同步。")
        Sb.AppendLine("2. 运行 `runClient` 启动带模组的客户端。")
        Sb.AppendLine("3. 执行 `build` 打包，产物在 `build/libs/`。")
        Sb.AppendLine()
        Sb.AppendLine("如果没有 Gradle Wrapper，可先安装 Gradle，在项目目录执行 `gradle wrapper`，之后用 `gradlew build`。")
        Sb.AppendLine()
        Sb.AppendLine("## 结构")
        Sb.AppendLine()
        Sb.AppendLine("- ModID：`" & ModId & "`")
        Sb.AppendLine("- 主类：`" & ClassName & "`")
        Sb.AppendLine("- 包名：`" & Package & "`")
        Return Sb.ToString()
    End Function

    Private Function BuildGradleProperties(ModId As String, ModName As String, ModVersion As String, Author As String, Package As String, Loader As String, McVersion As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("org.gradle.jvmargs=-Xmx2G")
        Sb.AppendLine("org.gradle.parallel=true")
        Sb.AppendLine()
        Sb.AppendLine("mod_id=" & ModId)
        Sb.AppendLine("mod_name=" & ModName)
        Sb.AppendLine("mod_version=" & ModVersion)
        Sb.AppendLine("mod_group_id=" & Package)
        Sb.AppendLine("mod_authors=" & Author)
        Sb.AppendLine("mod_description=" & ModName)
        Sb.AppendLine()
        Sb.AppendLine("minecraft_version=" & McVersion)
        Select Case Loader
            Case "forge"
                Sb.AppendLine("forge_version=47.4.0")
                Sb.AppendLine("mapping_channel=official")
                Sb.AppendLine("mapping_version=" & McVersion)
            Case "neoforge"
                Sb.AppendLine("neo_version=21.1.80")
            Case "fabric"
                Sb.AppendLine("yarn_mappings=" & McVersion & "+build.10")
                Sb.AppendLine("loader_version=0.15.11")
                Sb.AppendLine("fabric_version=0.92.2+1.20.1")
                Sb.AppendLine("maven_group=" & Package)
                Sb.AppendLine("archives_base_name=" & ModId)
        End Select
        Return Sb.ToString()
    End Function

    Private Function BuildPackMcmeta(Format As Integer) As String
        Return "{""pack"":{""description"":""mod resources"",""pack_format"":" & Format & "}}" & vbCrLf
    End Function

    Private Function BuildGradleForge(ModId As String, McVersion As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("plugins {")
        Sb.AppendLine("    id 'eclipse'")
        Sb.AppendLine("    id 'idea'")
        Sb.AppendLine("    id 'net.minecraftforge.gradle' version '[6.0,6.2)'")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("version = mod_version")
        Sb.AppendLine("group = mod_group_id")
        Sb.AppendLine()
        Sb.AppendLine("base {")
        Sb.AppendLine("    archivesName = mod_id")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("java.toolchain.languageVersion = JavaLanguageVersion.of(17)")
        Sb.AppendLine()
        Sb.AppendLine("minecraft {")
        Sb.AppendLine("    mappings channel: mapping_channel, version: mapping_version")
        Sb.AppendLine("    copyIdeResources = true")
        Sb.AppendLine("    runs {")
        Sb.AppendLine("        client {")
        Sb.AppendLine("            workingDirectory project.file('run')")
        Sb.AppendLine("            property 'forge.logging.console.level', 'debug'")
        Sb.AppendLine("            mods {")
        Sb.AppendLine("                " & ModId & " {")
        Sb.AppendLine("                    source sourceSets.main")
        Sb.AppendLine("                }")
        Sb.AppendLine("            }")
        Sb.AppendLine("        }")
        Sb.AppendLine("        server {")
        Sb.AppendLine("            workingDirectory project.file('run')")
        Sb.AppendLine("            property 'forge.logging.console.level', 'debug'")
        Sb.AppendLine("            mods {")
        Sb.AppendLine("                " & ModId & " {")
        Sb.AppendLine("                    source sourceSets.main")
        Sb.AppendLine("                }")
        Sb.AppendLine("            }")
        Sb.AppendLine("        }")
        Sb.AppendLine("    }")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("repositories {")
        Sb.AppendLine("    maven { url = 'https://maven.minecraftforge.net/' }")
        Sb.AppendLine("    mavenCentral()")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("dependencies {")
        Sb.AppendLine("    minecraft ""net.minecraftforge:forge:${minecraft_version}-${forge_version}""")
        Sb.AppendLine("}")
        Return Sb.ToString()
    End Function

    Private Function BuildSettingsForge(ModId As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("pluginManagement {")
        Sb.AppendLine("    repositories {")
        Sb.AppendLine("        maven { url = 'https://maven.minecraftforge.net/' }")
        Sb.AppendLine("        mavenCentral()")
        Sb.AppendLine("        gradlePluginPortal()")
        Sb.AppendLine("    }")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("rootProject.name = """ & ModId & """")
        Return Sb.ToString()
    End Function

    Private Function BuildJavaForge(Package As String, ClassName As String, ModId As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("package " & Package & ";")
        Sb.AppendLine()
        Sb.AppendLine("import net.minecraft.world.item.Item;")
        Sb.AppendLine("import net.minecraftforge.common.MinecraftForge;")
        Sb.AppendLine("import net.minecraftforge.eventbus.api.IEventBus;")
        Sb.AppendLine("import net.minecraftforge.fml.common.Mod;")
        Sb.AppendLine("import net.minecraftforge.fml.javafmlmod.FMLJavaModLoadingContext;")
        Sb.AppendLine("import net.minecraftforge.registries.DeferredRegister;")
        Sb.AppendLine("import net.minecraftforge.registries.ForgeRegistries;")
        Sb.AppendLine("import net.minecraftforge.registries.RegistryObject;")
        Sb.AppendLine("import org.slf4j.Logger;")
        Sb.AppendLine("import com.mojang.logging.LogUtils;")
        Sb.AppendLine()
        Sb.AppendLine("@Mod(" & ClassName & ".MODID)")
        Sb.AppendLine("public class " & ClassName & " {")
        Sb.AppendLine("    public static final String MODID = """ & ModId & """;")
        Sb.AppendLine("    private static final Logger LOGGER = LogUtils.getLogger();")
        Sb.AppendLine("    public static final DeferredRegister<Item> ITEMS = DeferredRegister.create(ForgeRegistries.ITEMS, MODID);")
        Sb.AppendLine("    public static final RegistryObject<Item> EXAMPLE_ITEM = ITEMS.register(""example_item"", () -> new Item(new Item.Properties()));")
        Sb.AppendLine()
        Sb.AppendLine("    public " & ClassName & "() {")
        Sb.AppendLine("        IEventBus modEventBus = FMLJavaModLoadingContext.get().getModEventBus();")
        Sb.AppendLine("        ITEMS.register(modEventBus);")
        Sb.AppendLine("        MinecraftForge.EVENT_BUS.register(this);")
        Sb.AppendLine("        LOGGER.info(""{} loaded"", MODID);")
        Sb.AppendLine("    }")
        Sb.AppendLine("}")
        Return Sb.ToString()
    End Function

    Private Function BuildForgeModsToml(ModId As String, ModName As String, ModVersion As String, McVersion As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("modLoader=""javafml""")
        Sb.AppendLine("loaderVersion=""[47,)""")
        Sb.AppendLine("license=""All rights reserved""")
        Sb.AppendLine()
        Sb.AppendLine("[[mods]]")
        Sb.AppendLine("modId=""" & ModId & """")
        Sb.AppendLine("version=""" & ModVersion & """")
        Sb.AppendLine("displayName=""" & ModName & """")
        Sb.AppendLine("description='''" & ModName & "'''")
        Sb.AppendLine()
        Sb.AppendLine("[[dependencies." & ModId & "]]")
        Sb.AppendLine("    modId=""forge""")
        Sb.AppendLine("    mandatory=true")
        Sb.AppendLine("    versionRange=""[47,)""")
        Sb.AppendLine("    ordering=""NONE""")
        Sb.AppendLine("    side=""BOTH""")
        Sb.AppendLine()
        Sb.AppendLine("[[dependencies." & ModId & "]]")
        Sb.AppendLine("    modId=""minecraft""")
        Sb.AppendLine("    mandatory=true")
        Sb.AppendLine("    versionRange=""[" & McVersion & "," & NextMinor(McVersion) & ")""")
        Sb.AppendLine("    ordering=""NONE""")
        Sb.AppendLine("    side=""BOTH""")
        Return Sb.ToString()
    End Function

    Private Function BuildGradleNeo(McVersion As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("plugins {")
        Sb.AppendLine("    id 'java'")
        Sb.AppendLine("    id 'net.neoforged.gradle.userdev' version '7.0.145'")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("version = mod_version")
        Sb.AppendLine("group = mod_group_id")
        Sb.AppendLine()
        Sb.AppendLine("base {")
        Sb.AppendLine("    archivesName = mod_id")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("java.toolchain.languageVersion = JavaLanguageVersion.of(21)")
        Sb.AppendLine()
        Sb.AppendLine("runs {")
        Sb.AppendLine("    client {")
        Sb.AppendLine("        modSource sourceSets.main")
        Sb.AppendLine("    }")
        Sb.AppendLine("    server {")
        Sb.AppendLine("        modSource sourceSets.main")
        Sb.AppendLine("    }")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("repositories {")
        Sb.AppendLine("    maven { url = 'https://maven.neoforged.net/releases/' }")
        Sb.AppendLine("    mavenCentral()")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("dependencies {")
        Sb.AppendLine("    implementation ""net.neoforged:neoforge:${neo_version}""")
        Sb.AppendLine("}")
        Return Sb.ToString()
    End Function

    Private Function BuildSettingsNeo(ModId As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("pluginManagement {")
        Sb.AppendLine("    repositories {")
        Sb.AppendLine("        maven { url = 'https://maven.neoforged.net/releases/' }")
        Sb.AppendLine("        mavenCentral()")
        Sb.AppendLine("        gradlePluginPortal()")
        Sb.AppendLine("    }")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("rootProject.name = """ & ModId & """")
        Return Sb.ToString()
    End Function

    Private Function BuildJavaNeo(Package As String, ClassName As String, ModId As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("package " & Package & ";")
        Sb.AppendLine()
        Sb.AppendLine("import net.minecraft.world.item.Item;")
        Sb.AppendLine("import net.neoforged.bus.api.IEventBus;")
        Sb.AppendLine("import net.neoforged.fml.common.Mod;")
        Sb.AppendLine("import net.neoforged.neoforge.registries.DeferredItem;")
        Sb.AppendLine("import net.neoforged.neoforge.registries.DeferredRegister;")
        Sb.AppendLine("import org.slf4j.Logger;")
        Sb.AppendLine("import com.mojang.logging.LogUtils;")
        Sb.AppendLine()
        Sb.AppendLine("@Mod(" & ClassName & ".MODID)")
        Sb.AppendLine("public class " & ClassName & " {")
        Sb.AppendLine("    public static final String MODID = """ & ModId & """;")
        Sb.AppendLine("    private static final Logger LOGGER = LogUtils.getLogger();")
        Sb.AppendLine("    public static final DeferredRegister.Items ITEMS = DeferredRegister.createItems(MODID);")
        Sb.AppendLine("    public static final DeferredItem<Item> EXAMPLE_ITEM = ITEMS.register(""example_item"", () -> new Item(new Item.Properties()));")
        Sb.AppendLine()
        Sb.AppendLine("    public " & ClassName & "(IEventBus modEventBus) {")
        Sb.AppendLine("        ITEMS.register(modEventBus);")
        Sb.AppendLine("        LOGGER.info(""{} loaded"", MODID);")
        Sb.AppendLine("    }")
        Sb.AppendLine("}")
        Return Sb.ToString()
    End Function

    Private Function BuildNeoModsToml(ModId As String, ModName As String, ModVersion As String, McVersion As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("modLoader=""javafml""")
        Sb.AppendLine("loaderVersion=""[4,)""")
        Sb.AppendLine("license=""All rights reserved""")
        Sb.AppendLine()
        Sb.AppendLine("[[mods]]")
        Sb.AppendLine("modId=""" & ModId & """")
        Sb.AppendLine("version=""" & ModVersion & """")
        Sb.AppendLine("displayName=""" & ModName & """")
        Sb.AppendLine("description='''" & ModName & "'''")
        Sb.AppendLine()
        Sb.AppendLine("[[dependencies." & ModId & "]]")
        Sb.AppendLine("modId=""neoforge""")
        Sb.AppendLine("type=""required""")
        Sb.AppendLine("versionRange=""[21.1,)""")
        Sb.AppendLine("ordering=""NONE""")
        Sb.AppendLine("side=""BOTH""")
        Sb.AppendLine()
        Sb.AppendLine("[[dependencies." & ModId & "]]")
        Sb.AppendLine("modId=""minecraft""")
        Sb.AppendLine("type=""required""")
        Sb.AppendLine("versionRange=""[" & McVersion & "," & NextMinor(McVersion) & ")""")
        Sb.AppendLine("ordering=""NONE""")
        Sb.AppendLine("side=""BOTH""")
        Return Sb.ToString()
    End Function

    Private Function BuildGradleFabric(McVersion As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("plugins {")
        Sb.AppendLine("    id 'fabric-loom' version '1.7-SNAPSHOT'")
        Sb.AppendLine("    id 'maven-publish'")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("version = project.mod_version")
        Sb.AppendLine("group = project.maven_group")
        Sb.AppendLine()
        Sb.AppendLine("base {")
        Sb.AppendLine("    archivesName = project.archives_base_name")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("repositories {")
        Sb.AppendLine("    maven { url = 'https://maven.fabricmc.net/' }")
        Sb.AppendLine("    mavenCentral()")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("dependencies {")
        Sb.AppendLine("    minecraft ""com.mojang:minecraft:${project.minecraft_version}""")
        Sb.AppendLine("    mappings ""net.fabricmc:yarn:${project.yarn_mappings}:v2""")
        Sb.AppendLine("    modImplementation ""net.fabricmc:fabric-loader:${project.loader_version}""")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("processResources {")
        Sb.AppendLine("    inputs.property ""version"", project.version")
        Sb.AppendLine("    filesMatching(""fabric.mod.json"") {")
        Sb.AppendLine("        expand ""version"": project.version")
        Sb.AppendLine("    }")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("tasks.withType(JavaCompile).configureEach {")
        Sb.AppendLine("    it.options.release = 17")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("java {")
        Sb.AppendLine("    withSourcesJar()")
        Sb.AppendLine("    sourceCompatibility = JavaVersion.VERSION_17")
        Sb.AppendLine("    targetCompatibility = JavaVersion.VERSION_17")
        Sb.AppendLine("}")
        Return Sb.ToString()
    End Function

    Private Function BuildSettingsFabric(ModId As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("pluginManagement {")
        Sb.AppendLine("    repositories {")
        Sb.AppendLine("        maven { name = 'Fabric'; url = 'https://maven.fabricmc.net/' }")
        Sb.AppendLine("        mavenCentral()")
        Sb.AppendLine("        gradlePluginPortal()")
        Sb.AppendLine("    }")
        Sb.AppendLine("}")
        Sb.AppendLine()
        Sb.AppendLine("rootProject.name = """ & ModId & """")
        Return Sb.ToString()
    End Function

    Private Function BuildJavaFabric(Package As String, ClassName As String, ModId As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("package " & Package & ";")
        Sb.AppendLine()
        Sb.AppendLine("import net.fabricmc.api.ModInitializer;")
        Sb.AppendLine("import net.minecraft.item.Item;")
        Sb.AppendLine("import net.minecraft.registry.Registries;")
        Sb.AppendLine("import net.minecraft.registry.Registry;")
        Sb.AppendLine("import net.minecraft.util.Identifier;")
        Sb.AppendLine("import org.slf4j.Logger;")
        Sb.AppendLine("import org.slf4j.LoggerFactory;")
        Sb.AppendLine()
        Sb.AppendLine("public class " & ClassName & " implements ModInitializer {")
        Sb.AppendLine("    public static final String MODID = """ & ModId & """;")
        Sb.AppendLine("    public static final Logger LOGGER = LoggerFactory.getLogger(MODID);")
        Sb.AppendLine("    public static final Item EXAMPLE_ITEM = new Item(new Item.Settings());")
        Sb.AppendLine()
        Sb.AppendLine("    @Override")
        Sb.AppendLine("    public void onInitialize() {")
        Sb.AppendLine("        Registry.register(Registries.ITEM, new Identifier(MODID, ""example_item""), EXAMPLE_ITEM);")
        Sb.AppendLine("        LOGGER.info(""{} loaded"", MODID);")
        Sb.AppendLine("    }")
        Sb.AppendLine("}")
        Return Sb.ToString()
    End Function

    Private Function BuildFabricModJson(ModId As String, ModName As String, ModVersion As String, Author As String, Package As String, ClassName As String, McVersion As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("{")
        Sb.AppendLine("  ""schemaVersion"": 1,")
        Sb.AppendLine("  ""id"": """ & ModId & """,")
        Sb.AppendLine("  ""version"": """ & ModVersion & """,")
        Sb.AppendLine("  ""name"": """ & ModName & """,")
        Sb.AppendLine("  ""description"": """ & ModName & """,")
        Sb.AppendLine("  ""authors"": [""" & Author & """],")
        Sb.AppendLine("  ""license"": ""MIT"",")
        Sb.AppendLine("  ""environment"": ""*"",")
        Sb.AppendLine("  ""entrypoints"": {")
        Sb.AppendLine("    ""main"": [""" & Package & "." & ClassName & """]")
        Sb.AppendLine("  },")
        Sb.AppendLine("  ""mixins"": [""" & ModId & ".mixins.json""],")
        Sb.AppendLine("  ""depends"": {")
        Sb.AppendLine("    ""fabricloader"": "">=0.15.0"",")
        Sb.AppendLine("    ""minecraft"": ""~" & McVersion & """,")
        Sb.AppendLine("    ""java"": "">=17""")
        Sb.AppendLine("  }")
        Sb.AppendLine("}")
        Return Sb.ToString()
    End Function

    Private Function BuildMixinsJson(Package As String) As String
        Dim Sb As New StringBuilder()
        Sb.AppendLine("{")
        Sb.AppendLine("  ""required"": true,")
        Sb.AppendLine("  ""package"": """ & Package & ".mixin"",")
        Sb.AppendLine("  ""compatibilityLevel"": ""JAVA_17"",")
        Sb.AppendLine("  ""mixins"": [],")
        Sb.AppendLine("  ""injectors"": {")
        Sb.AppendLine("    ""defaultRequire"": 1")
        Sb.AppendLine("  }")
        Sb.AppendLine("}")
        Return Sb.ToString()
    End Function

    Private Function NextMinor(Version As String) As String
        Dim Parts As String() = Version.Split("."c)
        If Parts.Length >= 2 AndAlso Integer.TryParse(Parts(1), Nothing) Then
            Return Parts(0) & "." & (Integer.Parse(Parts(1)) + 1)
        End If
        Return Version
    End Function

End Module
