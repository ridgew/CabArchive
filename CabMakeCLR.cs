using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace CabArchive
{
 
    public class CabMakeCLR : IDisposable
    {
        public CabMakeCLR(string cabFileName)
        {
            targetFileName = cabFileName;

            Initial();
            CreateCab(cabFileName, false, false, false);
        }

        private string targetFileName = "";
        Type cabType = null;
        Object cabInstance = null;

        void Initial()
        { 
            cabType = Type.GetTypeFromProgID("MakeCab.MakeCab", false);
            if (cabType == null) return;

            cabInstance = Activator.CreateInstance(cabType);
        }

        /// <summary>
        /// 创建本地CAB文件
        /// </summary>
        /// <param name="CabFileName">本地Cab文件路径</param>
        /// <param name="MakeSignable">是否可数字签名</param>
        /// <param name="ExtraSpace">抽取空格?</param>
        /// <param name="Use10Format">使用8+3命名格式?</param>
        private void CreateCab(string CabFileName, bool MakeSignable, bool ExtraSpace, bool Use10Format)
        {
            cabType.InvokeMember("CreateCab", BindingFlags.InvokeMethod, Type.DefaultBinder, cabInstance,
                new object[] { CabFileName, false, false, false });
        }

        /// <summary>
        /// 在当前CAB中添加文件
        /// </summary>
        /// <param name="FileName">本地文件名路径</param>
        /// <param name="FileNameInCab">CAB中的文件路径</param>
        public void AddFile(string FileName, string FileNameInCab)
        {
            cabType.InvokeMember("AddFile", BindingFlags.InvokeMethod, null, cabInstance,
                new object[] { FileName, FileNameInCab });
        }

        //public void CopyFile(string CabName, string FileNameInCab)
        //{ 
        
        //}

        /// <summary>
        /// 关闭当前CAB文件，释放占用内容
        /// </summary>
        public void CloseCab()
        {
            cabType.InvokeMember("CloseCab", BindingFlags.InvokeMethod, null, cabInstance,new object[0]);
        }

        #region IDisposable 成员

        /// <summary>
        /// 执行与释放或重置非托管资源相关的应用程序定义的任务。
        /// </summary>
        public void Dispose()
        {
            CloseCab();

            while (System.Runtime.InteropServices.Marshal.ReleaseComObject(cabInstance) > 0) ;
            cabInstance = null;
        }

        #endregion
    }
}
