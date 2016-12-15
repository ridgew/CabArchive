using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using CabLib;
using System.Reflection;

namespace CabArchive
{
    class Program
    {
        /*
        Windows Registry Editor Version 5.00

        [HKEY_CLASSES_ROOT\*\shell\压缩成CAB格式]
        [HKEY_CLASSES_ROOT\*\shell\压缩成CAB格式\command]
        @="makecab /v3 /D maxdisksize=1024000000 /D CompressionType=LZX /D CompressionMemory=21 \"%1\""

        [HKEY_CLASSES_ROOT\*\shell\解压CAB到当前文件夹]
        [HKEY_CLASSES_ROOT\*\shell\解压CAB到当前文件夹\command]
        @="expand -r \"%1\"" 



        REGEDIT4
        [-HKEY_CLASSES_ROOT\.cab]

        [HKEY_CLASSES_ROOT\.cab]
        @="ExploreCabinet"

        [HKEY_CLASSES_ROOT\ExploreCabinet]
        "EditFlags"=dword:00000000
        "BrowserFlags"=dword:00000008
        @=""

        [HKEY_CLASSES_ROOT\ExploreCabinet\DefaultIcon]
        @="cabview.dll,0"

        [HKEY_CLASSES_ROOT\ExploreCabinet\shell]
        @="open"

        [HKEY_CLASSES_ROOT\ExploreCabinet\shell\open]

        [HKEY_CLASSES_ROOT\ExploreCabinet\shell\open\command]
        @="explorer.exe /root,{0CD7A5C0-9F37-11CE-AE65-08002B2E1262},%1"

        */

        static ConsoleColor RawConsoleColor = Console.ForegroundColor;
        const string DllResourceName = "CabLib.dll";
        //解压时文件进度计数
        static ushort CurrentExtraFileCount = 0, ExtraFileIndex = 0;

        static Program()
        {
            DumpAssembly();
            //AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(Resolver);
        }

        static Assembly Resolver(object sender, ResolveEventArgs args)
        {
            //Console.WriteLine("find ...");

            // super defensive
            Assembly a1 = Assembly.GetExecutingAssembly();
            if (a1 == null)
                throw new Exception("GetExecutingAssembly returns null.");

            string[] tokens = args.Name.Split(',');

            String[] names = a1.GetManifestResourceNames();

            //Console.WriteLine("Try " + args.Name);
            //Console.WriteLine(string.Join("\n", names));

            if (names == null)
                throw new Exception("GetManifestResourceNames returns null.");

            // workitem 7978
            Stream s = null;
            foreach (string n in names)
            {
                string root = n.Substring(0, n.Length - 4);
                string ext = n.Substring(n.Length - 3);
                if (root.Equals(tokens[0]) && ext.ToLower().Equals("dll"))
                {
                    s = a1.GetManifestResourceStream(n);
                    if (s != null) break;
                }
            }

            if (s == null)
                throw new Exception(String.Format("GetManifestResourceStream returns null. Available resources: [{0}]",
                                                  String.Join("|", names)));

            byte[] block = new byte[s.Length];
            if (block == null)
                throw new Exception(String.Format("Cannot allocated buffer of length({0}).", s.Length));

            s.Read(block, 0, block.Length);

            Assembly a2 = null;
            try
            {
               a2 = Assembly.Load(block);
            }
            catch (BadImageFormatException)
            {
                File.WriteAllBytes(tokens[0] + ".dll", block);
            }
            if (a2 == null)
                throw new Exception("Assembly.Load(block) returns null");

            return a2;
        }

        static void DumpAssembly()
        {
            DumpAssembly(false, null);
        }

        static void DumpAssembly(bool blnForce, string dependenceFile)
        {
            string targetFile = !string.IsNullOrEmpty(dependenceFile) ? dependenceFile : AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + "\\" + DllResourceName;
            FileInfo fInfo = new FileInfo(targetFile);
            if (blnForce || !fInfo.Exists)
            {
                Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(DllResourceName);
                byte[] block = new byte[s.Length];
                s.Read(block, 0, block.Length);
                File.WriteAllBytes(fInfo.FullName, block);

                //Console.WriteLine("Dump to " + fInfo.FullName);
                s.Dispose();
            }
        }

        static void Main(string[] args)
        {
            string dependenceFile = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + "\\" + DllResourceName;
            if (!File.Exists(dependenceFile))
            {
                DumpAssembly(true, dependenceFile);
                System.Threading.Thread.Sleep(500);
                //System.Diagnostics.Process.Start(Assembly.GetExecutingAssembly().CodeBase, string.Join(" ", args));
                Main(args);
                return;
            }

            if (args.Length < 1)
            {
                Error("错误：请指定要压缩的文件夹或者是要解压的Cab文件！");
            }
            else
            {
                FileInfo fInfo = null;
                DirectoryInfo dInfo = null;


                if (Directory.Exists(args[0]))
                {
                    dInfo = new DirectoryInfo(args[0]);
                    if (args.Length == 1)
                    {
                        CreateCab(dInfo.FullName);
                    }
                    else
                    {
                        fInfo = new FileInfo(args[1]);
                        CreateCab(dInfo.FullName, fInfo.FullName);
                    }
                }
                else
                {
                    if (!args[0].EndsWith(".cab"))
                    {
                        Error("错误：请指定要解压的Cab文件！");
                    }
                    else
                    {
                        fInfo = new FileInfo(args[0]);
                        if (args.Length == 1)
                        {
                            ExtractCab(fInfo.FullName);
                        }
                        else
                        {
                            dInfo = new DirectoryInfo(args[1]);
                            ExtractCab(fInfo.FullName, dInfo.FullName);
                        }
                    }
                }
            }

            //Console.WriteLine("按任意键继续...");
            //Console.Read();
        }

        static void Error(string format, params object[] errMsgs)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(format, errMsgs);
            Console.ForegroundColor = RawConsoleColor;
        }

        static void Progress(string format, params object[] errMsgs)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(format, errMsgs);
            Console.ForegroundColor = RawConsoleColor;
        }

        static void CreateCab(string baseDir, string targetFileName)
        {
            if (!Directory.Exists(baseDir))
            {
                Error(baseDir + "不是一个目录或目录不存在！");
                return;
            }

            CabMakeCLR imake = new CabMakeCLR(targetFileName);
            using (FileSystemWorker fSysWorker = new FileSystemWorker(baseDir,
                new CabFileWorker(imake, targetFileName)))
            {
                string siteRoot = AppDomain.CurrentDomain.BaseDirectory;
                fSysWorker.MsgReport = str => Console.WriteLine(string.Format("{0}",
                    str.Replace(siteRoot, "").Replace(baseDir, "").Replace("\\", "/")));

                fSysWorker.ProgressReport = p => Progress(string.Format("已完成{0}...",
                    (p * 100).ToString("0.00") + "%"));

                fSysWorker.Execute();
            }

            imake.Dispose();

            Progress(".目录{0}已压缩为{1}", baseDir, targetFileName);
        }

        static void CreateCab(string baseDir)
        {
            DirectoryInfo dInfo = new DirectoryInfo(baseDir);
            string targetName = Directory.GetParent(baseDir) + "\\" + dInfo.Name + ".cab";
            CreateCab(baseDir, targetName);
        }

        static void ExtractCab(string cabFilePath, string targetDir)
        {
            string strResult = "解压完成.";

            //string fileDir = Path.GetDirectoryName(cabFilePath);
            //int exitCode = RunCmd("expand",
            //    fileDir,
            //    "-r -F:* \"" + cabFilePath + "\" \"" + fileDir + "\"",
            //    ref strResult);
            //if (exitCode != 0)
            //{
            //    Error("解压错误：");
            //}

            try
            {
                Extract extra = new Extract();
                extra.evBeforeCopyFile += new Extract.delBeforeCopyFile(extra_evBeforeCopyFile);
                extra.evAfterCopyFile += new Extract.delAfterCopyFile(extra_evAfterCopyFile);
                extra.evCabinetInfo += new Extract.delCabinetInfo(extra_evCabinetInfo);
                extra.evNextCabinet += new Extract.delNextCabinet(extra_evNextCabinet);

                Extract.kHeaderInfo k_Info = null;
                if (extra.IsFileCabinet(cabFilePath, out k_Info))
                {
                    CurrentExtraFileCount = k_Info.u16_Files;

                    Progress("CAB filesize:        " + k_Info.u32_FileSize + " Bytes");
                    Progress("Folder count in CAB: " + k_Info.u16_Folders);
                    Progress("File   count in CAB: " + k_Info.u16_Files);
                    Progress("Set ID:              " + k_Info.u16_SetID);
                    Progress("CAB Index:           " + k_Info.u16_CabIndex);
                    Progress("Has additional header data:          " + k_Info.b_HasReserve);
                    Progress("Has predecessor in splitted Cabinet: " + k_Info.b_HasPrevious);
                    Progress("Has successor   in splitted Cabinet: " + k_Info.b_HasNext + "\n");

                    //解压到内存
                    //前缀：MEMORY\
                    //extra.ExtractFile(cabFilePath, "MEMORY");

                    extra.ExtractFile(cabFilePath, targetDir);
                }
                else
                {
                    Error("{0}不是一个有效的cab文件！", cabFilePath);
                }

            }
            catch (Exception exp)
            {
                Exception targetExp = exp.InnerException;
                while (targetExp != null)
                {
                    exp = targetExp;
                    targetExp = targetExp.InnerException;
                }
                Error("解压错误：" + exp.Message + Environment.NewLine
                    + exp.StackTrace);
            }

            Console.WriteLine(strResult);
            Progress(".文件{0}已解压到{1}", cabFilePath, targetDir);
        }

        static void extra_evNextCabinet(Extract.kCabinetInfo k_Info, int s32_FdiError)
        {
            
        }

        static void extra_evCabinetInfo(Extract.kCabinetInfo k_Info)
        {
            
        }

        // This function will be called when a file has been succesfully extracted.
        // If Extraction to Memory is enabled, no file is written to disk,
        // instead the entire content of the file is passed in u8_ExtractMem
        // If Extraction to Memory is not enabled, u8_ExtractMem is null
        static void extra_evAfterCopyFile(string s_File, byte[] u8_ExtractMem)
        {
            ExtraFileIndex += 1;
            if (u8_ExtractMem != null)
            {
                Progress("文件：{0}, 大小：{1}字节, 进度：{2}...", s_File, u8_ExtractMem.Length,
                    ((float)ExtraFileIndex / (float)CurrentExtraFileCount * 100).ToString("0") + "%");
            }
            else
            {
                Progress("{0}", string.Format("进度：{0}...", ((float)ExtraFileIndex / (float)CurrentExtraFileCount * 100).ToString("0") + "%"));
            }
        }

        /// <summary>
        /// This event handler is called for every extratced file before it is written to disk
        /// return false here if you want a file not to be extracted
        /// </summary>
        static bool extra_evBeforeCopyFile(Extract.kCabinetFileInfo k_Info)
        {
            Progress("解压文件：{0}", k_Info.s_File);
            return true;
        }

        static void ExtractCab(string cabFilePath)
        {
            ExtractCab(cabFilePath, Path.GetDirectoryName(cabFilePath) 
                + "\\" + Path.GetFileNameWithoutExtension(cabFilePath));
        }

        /// <summary>
        /// 运行控制台命令程序并获取运行结果
        /// </summary>
        /// <param name="cmdPath">命令行程序完整路径</param>
        /// <param name="workDir">命令行程序的工作目录</param>
        /// <param name="strArgs">命令行参数</param>
        /// <param name="output">命令行输出</param>
        /// <returns>命令行程序的状态退出码</returns>
        public static int RunCmd(string cmdPath, string workDir, string strArgs, ref string output)
        {
            int exitCode = 0;
            //System.Web.HttpContext.Current.Response.Write(cmdPath);
            //System.Web.HttpContext.Current.Response.Write("<br>");
            //System.Web.HttpContext.Current.Response.Write(strArgs);
            using (Process proc = new Process())
            {
                ProcessStartInfo psInfo = new ProcessStartInfo(cmdPath, strArgs);
                psInfo.UseShellExecute = false;
                psInfo.RedirectStandardError = true;
                psInfo.RedirectStandardOutput = true;
                psInfo.RedirectStandardInput = true;
                psInfo.WindowStyle = ProcessWindowStyle.Hidden;
                psInfo.WorkingDirectory = workDir;
                proc.StartInfo = psInfo;
                proc.Start();
                string strOutput = "";
                while (!proc.HasExited)
                {
                    strOutput += proc.StandardOutput.ReadToEnd().Replace("\r", "");
                    System.Threading.Thread.Sleep(100);
                }
                output = strOutput;
                exitCode = proc.ExitCode;
                proc.Close();
            }

            //System.Web.HttpContext.Current.Response.Write("<br>");
            //System.Web.HttpContext.Current.Response.Write(output);
            return exitCode;
        }
    }
}
