using System.Security.AccessControl;

namespace CustomShell;

public class Shell
{
    private string _currentDirectory;
    private string _userName;
    private string _host;
    private bool _isRunning;

    public Shell()
    {
        _currentDirectory = Directory.GetCurrentDirectory();
        _userName = Environment.UserName;
        _host = Environment.MachineName;
        _isRunning = true;
    }

    public void Run()
    {
        Console.WriteLine($"Login: {DateTime.Now}");
        
        while (_isRunning)
        {
            string dirName = _currentDirectory.Split("/").Last();
            if (dirName == _userName)
            {
                dirName = "~";
            } else if (dirName == "")
            {
                dirName = "/";
            }
            Console.Write($"{_userName}@{_host} {dirName} % ");
            string input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            string[] parts = input.Split(' ');
            string command = parts[0].ToLower();
            string args = parts.Length > 1 ? parts[1] : string.Empty;

            switch (command)
            {
                case "whoami":
                    CmdWhoami();
                    break;
                case "pwd":
                    CmdPwd();
                    break;
                case "ls":
                    CmdLs(parts);
                    break;
                case "cd":
                    CmdCd(args);
                    break;
                case "mkdir":
                    CmdMkdir(args);
                    break;
                case "rm":
                    CmdRm(parts);
                    break;
                case "cp":
                    CmdCp(parts);
                    break;
                case "mv":
                    CmdMv(parts);
                    break;
                case "cat":
                    CmdCat(args);
                    break;
                case "touch":
                    CmdTouch(args);
                    break;
                case "clear":
                    CmdClear();
                    break;
                case "help":
                    CmdHelp();
                    break;
                case "exit":
                    CmdExit();
                    break;
                default:
                    Console.WriteLine($"csh: neznamy prikaz: {command}");
                    break;
            }
        }
    }
    
    private void CmdWhoami()
    {
        Console.WriteLine(Environment.UserName);
    }
    
    private void CmdPwd()
    {
        Console.WriteLine(_currentDirectory);
    }
    
    private void CmdLs(string[] args)
    {
        bool showHidden = args.Contains("a");
        bool showDetails = args.Contains("l");
        
        string[] dirs = Directory.GetDirectories(_currentDirectory);
        string[] files = Directory.GetFiles(_currentDirectory);

        if (!showHidden)
        {
            dirs = dirs.Where(d => !Path.GetFileName(d).StartsWith(".")).ToArray();
            files = files.Where(f => !Path.GetFileName(f).StartsWith(".")).ToArray();
        }

        if (showDetails)
        {
            foreach (string dir in dirs)
            {
                DirectoryInfo di = new DirectoryInfo(dir);
                
                string permissions = GetUnixPermissions(di);
                string owner = GetUnixOwner(dir);
                long size = di.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
                string date = di.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                string name = di.Name;
                Console.WriteLine($"{permissions} {owner} {size,10} {date} {name}");
            }

            foreach (string file in files)
            {
                FileInfo fi = new FileInfo(file);
                
                string permissions = GetUnixPermissions(fi);
                string owner = GetUnixOwner(file);
                long size = fi.Length;
                string date = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                string name = fi.Name;
                Console.WriteLine($"{permissions} {owner} {size,10} {date} {name}");
            }
        }
        else
        {
            foreach (string dir in dirs)
            {
                string[] dirF = dir.Split("/");
                Console.WriteLine($"{dirF[dirF.Length - 1]}");
            }

            foreach (string file in files)
            {
                string[] fileF = file.Split("/");
                Console.WriteLine($"{fileF[fileF.Length - 1]}");
            }
        }
    }
    
    // methody na zistovanie info
    private string GetUnixOwner(string path)
    {
        try
        {
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "stat";
            process.StartInfo.Arguments = $"-f %Su \"{path}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return output;
        }
        catch
        {
            return Environment.UserName;
        }
    }
    
    private string GetUnixPermissions(FileSystemInfo info)
    {
        try
        {
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                return info.UnixFileMode.ToString().Substring(1);
            }
            return "rwxr-xr-x";
        }
        catch
        {
            return "rwxr-xr-x";
        }
    }
    

    private void CmdCd(string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            _currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return;
        }
        
        string targetDir = args;
        string newDir;

        if (targetDir == "..")
        {
            DirectoryInfo parentDir = Directory.GetParent(_currentDirectory);
            if (parentDir != null)
            {
                newDir = parentDir.FullName;
            }
            else
            {
                Console.WriteLine("csh: uz si v root direktorii");
                return;
            }
        } else if (targetDir == "~")
        {
            newDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        } else if (Path.IsPathRooted(targetDir))
        {
            newDir = targetDir;
        }
        else
        {
            newDir = Path.Combine(_currentDirectory, targetDir);
        }
        
        if (Directory.Exists(newDir))
        {
            _currentDirectory = Path.GetFullPath(newDir);
        }
        else
        {
            Console.WriteLine($"cd: No such file or directory: {targetDir}");
        }
    }
    
    private void CmdMkdir(string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            Console.WriteLine("mkdir: missing operand");
            return;
        }
        
        string dirName = args;
        string newDirPath = Path.Combine(_currentDirectory, dirName);

        try
        {
            if (Directory.Exists(newDirPath))
            {
                Console.WriteLine($"mkdir: cannot create directory '{dirName}': File exists");
                return;
            }
            Directory.CreateDirectory(newDirPath);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    private void CmdRm(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("rm: missing operand");
            return;
        }

        string fileName;

        if (args[1] == "-r")
        {
            fileName = args[2];
        }
        else if (args[1] == "-f")
        {
            fileName = args[2];
        } else
        {
            fileName = args[1];
        }
        string filePath = Path.Combine(_currentDirectory, fileName);

        bool isDirectory = Directory.Exists(filePath) ? true : false;
        
        try
        {
            if (isDirectory)
            {
                if (Directory.Exists(filePath))
                {
                    Directory.Delete(filePath, true);
                }
                else
                {
                    Console.WriteLine($"rm: cannot remove '{fileName}': No such directory");
                }
            }
            else
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                else
                {
                    Console.WriteLine($"rm: cannot remove '{fileName}': No such file");
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    private void CmdCp(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("cp: missing operand");
            return;
        }
        
        string source = Path.Combine(_currentDirectory, args[1]);
        string destination = args[2];

        try
        {
            if (File.Exists(source))
            {
                File.Copy(source, destination, true);
            }
            else
            {
                Console.WriteLine($"cp: cannot stat '{source}': No such file");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    private void CmdMv(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("mv: missing operand");
            return;
        }
        
        string source = Path.Combine(_currentDirectory, args[1]);
        string destination = args[2];

        if (!Path.IsPathRooted(destination))
        {
            destination = Path.Combine(_currentDirectory, destination);
        }

        try
        {
            if (File.Exists(source))
            {
                if (Directory.Exists(destination))
                {
                    string fileName = Path.GetFileName(source);
                    destination = Path.Combine(destination, fileName);
                }
                
                if (File.Exists(destination))
                {
                    Console.WriteLine($"The file '{destination}' already exists.");
                    return;
                }
                
                File.Move(source, destination);
            }
            else
            {
                Console.WriteLine($"mv: cannot stat '{source}': No such file");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    private void CmdCat(string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            Console.WriteLine("cat: missing operand");
            return;
        }
        
        string fileName = args;
        string filePath = Path.Combine(_currentDirectory, fileName);

        try
        {
            if (File.Exists(filePath))
            {
                string content = File.ReadAllText(filePath);
                Console.WriteLine(content);
            }
        } catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
    
    private void CmdTouch(string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            Console.WriteLine("touch: missing operand");
            return;
        }
        
        string fileName = args;
        string filePath = Path.Combine(_currentDirectory, fileName);
        try
        {
            if (!File.Exists(filePath))
            {
                using (File.Create(filePath)) { }
            }
            else
            {
                File.SetLastWriteTime(filePath, DateTime.Now);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
    
    private void CmdClear()
    {
        Console.Clear();
    }
    
    private void CmdHelp()
    {
        Console.WriteLine(@"
Zoznam dostupnych príkazov:

whoami                      - Vypise aktualne prihlaseneho uzivatela
pwd                         - Vypise aktualnu direktoriu s datumom vytvorenia
ls [-l]                     - Vypise obsah direktorii (-l pre detaily)
cd <direktoria>             - Zmení direktoriu (.. = nahor, ~ = domov)
mkdir <nazov>               - Vytvori novu direktoriu
find <subor> [direktoria]   - Najde subor v direktorii a poddirektoriach
rm <subor>                  - Vymaze subor
cp <zdroj> <ciel>           - Skopiruje súbor
mv <zdroj> <ciel>           - Presunie/premenuje subor
cat <subor>                 - Zobrazi obsah súboru
stat <subor>                - Zobrazi detailne info o subore
touch <subor>               - Vytvori prazdny subor
clear                       - Vycisti obrazovku
help                        - Zobrazi tuto napovedu
exit                        - Ukonci program
         ");
    }
    
    private void CmdExit()
    {
        _isRunning = false;
    }
}