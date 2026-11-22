using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
[assembly:AssemblyTitle("Forever Hive Exchange Setup")]
[assembly:AssemblyVersion("0.1.1.0")]
[assembly:AssemblyFileVersion("0.1.1.0")]
class Setup {
    static string Option(string[] args,string key,string fallback){int k=Array.IndexOf(args,key);return k>=0&&k+1<args.Length?args[k+1]:fallback;}
    static void Extract(string resource,string dest){
        string tmp=dest+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            using(var src=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))using(var target=File.Create(tmp))src.CopyTo(target);
            if(File.Exists(dest))File.Replace(tmp,dest,null);else File.Move(tmp,dest);
        }finally{if(File.Exists(tmp))File.Delete(tmp);}
    }
    [STAThread] static int Main(string[] args){
        bool quiet=Array.IndexOf(args,"--quiet")>=0,portable=Array.IndexOf(args,"--portable")>=0;
        try{
            string dest=Path.GetFullPath(Option(args,"--target",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Forever Hive Exchange\App")));
            if(!quiet&&MessageBox.Show("Install Forever Hive Exchange 0.1.1 in\n"+dest+"?","Forever Hive Exchange Setup",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return 2;
            int pid;if(int.TryParse(Option(args,"--wait",""),out pid)){try{using(var p=Process.GetProcessById(pid)){if(!p.WaitForExit(30000))throw new IOException("Close Exchange and run setup again.");}}catch(ArgumentException){}}
            Directory.CreateDirectory(dest);string exe=Path.Combine(dest,"ForeverHiveExchange.exe");
            Extract("guide",Path.Combine(dest,"README.md"));if(portable)File.WriteAllText(Path.Combine(dest,"portable.flag"),"Store the database in the adjacent Data folder.");
            Extract("desktop",exe);
            if(!portable){
                Type t=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(t);
                dynamic lnk=shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Forever Hive Exchange.lnk"));lnk.TargetPath=exe;lnk.WorkingDirectory=dest;lnk.Save();
            }
            if(Array.IndexOf(args,"--no-launch")<0)Process.Start(exe);
            return 0;
        }catch(Exception ex){if(!quiet)MessageBox.Show(ex.Message,"Exchange setup could not finish");else Console.Error.WriteLine(ex.Message);return 1;}
    }
}
