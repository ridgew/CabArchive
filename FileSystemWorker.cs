using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace CabArchive
{

    /// <summary>
    /// 汇报消息
    /// </summary>
    public delegate void ReportMessage(string msg);

    /// <summary>
    /// 汇报进度
    /// </summary>
    public delegate void ReportProgress(float percent);


    /// <summary>
    /// 具体文件项的操作
    /// </summary>
    public interface IFileItemWorker : IDisposable
    {
        /// <summary>
        /// Packages the file.
        /// </summary>
        /// <param name="pkgFileInfo">The PKG file info.</param>
        void PackageFile(FileInfo pkgFileInfo);

        /// <summary>
        /// 基础根路径
        /// </summary>
        /// <value>The base dir.</value>
        string BaseDir { get; set; }
    }

    /// <summary>
    /// 文件系统的相关工具
    /// </summary>
    public class FileSystemWorker: IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectArchive"/> class.
        /// </summary>
        /// <param name="ProjectDir">The project dir.</param>
        /// <param name="Pkg">The PKG.</param>
        public FileSystemWorker(string ProjectDir, IFileItemWorker Pkg)
        {
            LocalBaseDir = ProjectDir;
            PackageImplement = Pkg;
            PackageImplement.BaseDir = ProjectDir;
        }

        /// <summary>
        /// Gets or sets the package implement.
        /// </summary>
        /// <value>The package implement.</value>
        public IFileItemWorker PackageImplement { get; set; }

        /// <summary>
        /// 获取或设置打包的文件匹配信息
        /// </summary>
        /// <value>The file match.</value>
        public Predicate<FileInfo> FileMatch { get; set; }

        /// <summary>
        /// 获取或设置打包的文件排除信息
        /// </summary>
        public Predicate<FileInfo> FileExclude { get; set; }

        /// <summary>
        /// 消息汇报
        /// </summary>
        /// <value>The MSG report.</value>
        public ReportMessage MsgReport { get; set; }

        /// <summary>
        /// 进度汇报
        /// </summary>
        /// <value>The progress report.</value>
        public ReportProgress ProgressReport { get; set; }

        /// <summary>
        /// 本地基准目录
        /// </summary>
        /// <value></value>
        public string LocalBaseDir { get; set; }

        /// <summary>
        /// 运行相关任务
        /// </summary>
        public void Execute()
        {
            Report("开始分析目录");
            DoAnalyze(this.LocalBaseDir);

            int total = PackageQueue.Count;
            Report("开始处理目录");
            for (int i = 1; i <= total; i++)
            {
                PackageFile(PackageQueue.Dequeue());
                ReportProgress((float)i / (float)total);
            }
            PackageQueue.Clear();
            Report("操作完成");
        }

        private Queue<FileInfo> PackageQueue = new Queue<FileInfo>();

        private void DoAnalyze(string currentDir)
        {
            Report("分析目录：" + currentDir);

            foreach (string fileName in Directory.GetFiles(currentDir))
            {
                FileInfo currentFile = new FileInfo(fileName);

                if (FileExclude != null && FileExclude(currentFile)) continue;

                if (FileMatch != null)
                {
                    if (!FileMatch(currentFile))
                    {
                        Report("文件：" + fileName + "被跳过");
                        continue;
                    }
                }

                PackageQueue.Enqueue(currentFile);
            }

            foreach (string subDir in Directory.GetDirectories(currentDir))
            {
                DoAnalyze(subDir);
            }
        }

        /// <summary>
        /// Packages the file.
        /// </summary>
        /// <param name="pkgFileInfo">The PKG file info.</param>
        /// <remarks>[ST:TODO]</remarks>
        private void PackageFile(FileInfo pkgFileInfo)
        {
            Report("处理文件：" + pkgFileInfo.FullName);
            if (PackageImplement != null)
            {
                PackageImplement.PackageFile(pkgFileInfo);
            }
        }

        private void Report(string msg)
        {
            if (MsgReport != null) MsgReport(msg);
        }

        private void ReportProgress(float percent)
        {
            if (ProgressReport != null) ProgressReport(percent);
        }


        #region IDisposable 成员

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (PackageImplement != null) PackageImplement.Dispose();
        }

        #endregion
    }

}
