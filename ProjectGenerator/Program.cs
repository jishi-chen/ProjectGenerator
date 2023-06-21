using System.Diagnostics;
using System.Text.Json;
using System.Xml;

class Program
{
    static void Main(string[] args)
    {
        const string dotnetPath = $@"C:\Program Files\dotnet\dotnet.exe"; // dotnet CLI 的路徑
        string settingFileName = "settings.json";
        string originalPath = Directory.GetCurrentDirectory();
        string settingFilePath = Path.Combine(originalPath, settingFileName);
        string jsonString = File.ReadAllText(settingFilePath);
        JsonDocument jsonDocument = JsonDocument.Parse(jsonString);
        JsonElement root = jsonDocument.RootElement;

        Console.WriteLine("請輸入專案名稱：");
        string? projectName = Console.ReadLine(); // 新專案的名稱
        if (string.IsNullOrEmpty(projectName))
        {
            projectName = root.GetProperty("ProjectName").GetString();
        }
        Console.WriteLine("請輸入套件版本：");
        string? packageVersion = Console.ReadLine();
        if (string.IsNullOrEmpty(packageVersion))
        {
            packageVersion = root.GetProperty("PackageVersion").GetString();
        }

        //清除套件快取/重新裝載套件/建置專案取得套件資料
        string? packageName = root.GetProperty("PackageName").GetString();
        string? BuildToolProjectPath = root.GetProperty("BuildToolProjectPath").GetString();
        string removePackageCommand = $"remove {BuildToolProjectPath} package {packageName}";
        RunCommand(dotnetPath, removePackageCommand);
        string clearNugetCacheCommand = $"dotnet nuget locals all --clear";
        RunCommand(dotnetPath, clearNugetCacheCommand);
        string addPackageCommand = $"add {BuildToolProjectPath} package {packageName} -v {packageVersion}";
        RunCommand(dotnetPath, addPackageCommand);
        string buildProjectCommand = $"build {BuildToolProjectPath}";
        RunCommand(dotnetPath, buildProjectCommand);


        //建立專案
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop); //取得桌面路徑
        string projectDirectory = Path.Combine(desktopPath, projectName);

        if (!Directory.Exists(projectDirectory))
        {
            string dotnetVersion = root.GetProperty("DotnetVersion").GetString()!; // 指定的 .NET Core 版本       
            string createProjectCommand = $"new mvc --name {projectName} --output {projectDirectory} --force --no-restore --framework net{dotnetVersion}"; // 建立新專案
            RunCommand(dotnetPath, createProjectCommand);
        }

        //將套件資料複製至新專案
        string productName = root.GetProperty("ProductName").GetString()!;
        Directory.SetCurrentDirectory(projectDirectory);
        string contentPath = Path.Combine(originalPath, $"packages\\{packageName}\\{packageVersion}\\content");
        try
        {
            CheckDirectory(Directory.GetDirectories(contentPath), projectName, productName, projectDirectory);
            CopyFiles(Directory.GetFiles(contentPath), projectName, productName, projectDirectory);

        }
        catch (Exception ex)
        {
            Console.WriteLine("複製檔案發生錯誤：" + ex.Message);
            Console.ReadLine();
        }

        //修改.csproj中要使用的ProjectReference
        string csprojFile = Path.Combine(projectDirectory, projectName + ".csproj");
        XmlDocument doc = new XmlDocument();
        doc.Load(csprojFile);
        XmlNode rootNode = doc.DocumentElement!;
        XmlElement element = doc.CreateElement("ItemGroup");

        JsonElement packageReferce = root.GetProperty("PackageReferce");
        foreach (JsonElement item in packageReferce.EnumerateArray())
        {
            XmlElement referenceElement = doc.CreateElement("PackageReference");
            string name = item.GetProperty("Include").GetString()!;
            string version = item.GetProperty("Version").GetString()!;
            referenceElement.SetAttribute("Include", name);
            referenceElement.SetAttribute("Version", version);
            element.AppendChild(referenceElement);
        }
        
        rootNode.AppendChild(element);
        doc.Save(csprojFile);

        Console.WriteLine("專案建立完成！");
        Console.ReadLine();
    }

    static void RunCommand(string command, string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Console.WriteLine(output);
    }
    static void CheckDirectory(string[] folderPath, string replaceString, string searchString, string destinationPath)
    {
        for (int i = 0; i < folderPath.Length; i++)
        {
            string folderName = Path.GetFileName(folderPath[i])!;
            CheckDirectory(Directory.GetDirectories(folderPath[i]), replaceString, searchString, Path.Combine(destinationPath, folderName));
            string[] files = Directory.GetFiles(folderPath[i]);
            CopyFiles(files, replaceString, searchString, Path.Combine(destinationPath, folderName));
        }
    }
    static void CopyFiles(string[] files, string replaceString, string searchString, string destinationPath)
    {
        if (!Directory.Exists(destinationPath))
        {
            Directory.CreateDirectory(destinationPath);
        }
        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileName(files[i])!;
            string fileContent = File.ReadAllText(files[i]);
            string modifiedContent = fileContent.Replace(searchString, replaceString);
           
            File.WriteAllText(Path.Combine(destinationPath, fileName), modifiedContent);
        }
    }
}