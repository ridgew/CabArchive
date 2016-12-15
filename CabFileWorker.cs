using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace CabArchive
{
    public class CabFileWorker : IFileItemWorker
    {
        public CabFileWorker(CabMakeCLR cabAgent, string targetName)
        {
            _cabAgent = cabAgent;
            _cabFileName = targetName;
        }

        private CabMakeCLR _cabAgent = null;
        private string _cabFileName = null;

        #region IFileItemWorker 成员

        /// <summary>
        /// Packages the file.
        /// </summary>
        /// <param name="pkgFileInfo">The PKG file info.</param>
        public void PackageFile(FileInfo pkgFileInfo)
        {
            _cabAgent.AddFile(pkgFileInfo.FullName, pkgFileInfo.FullName.Replace(BaseDir, "").Replace('/', '\\').TrimStart('\\'));
        }

        /// <summary>
        /// 基础根路径
        /// </summary>
        /// <value>The base dir.</value>
        public string BaseDir
        {
            get;
            set;
        }

        #endregion

        #region IDisposable 成员

        /// <summary>
        /// 执行与释放或重置非托管资源相关的应用程序定义的任务。
        /// </summary>
        public void Dispose()
        {
            
        }

        #endregion
    }
}
