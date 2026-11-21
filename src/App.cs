using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;

[assembly:AssemblyTitle("Forever Hive Exchange")]
[assembly:AssemblyProduct("Forever Hive Exchange")]
[assembly:AssemblyVersion("0.1.0.0")]
[assembly:AssemblyFileVersion("0.1.0.0")]
namespace ForeverHive {
internal static class App {
    public const string Version="0.1.0";
    public static readonly string DefaultWow=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),@"World of Warcraft\_forever_");
    public static string DataDir {
        get{
            string root=AppDomain.CurrentDomain.BaseDirectory;
            return File.Exists(Path.Combine(root,"portable.flag"))?Path.Combine(root,"Data"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Forever Hive Exchange\Data");
        }
    }
    [STAThread] public static int Main(string[] args){
        try{
            var cli=args.Length>0;
            string path=cli&&args.Length>=2?Path.GetFullPath(args[1]):Path.Combine(DataDir,"market.sqlite");
            using(var db=new Db(path)){
                db.Init();
                if(cli){
                    if(args[0]=="--sync"){var errs=LocalSources.Sync(db,args.Length>2?args[2]:DefaultWow);foreach(var err in errs)Console.Error.WriteLine(err);return errs.Count==0?0:1;}
                    if(args[0]=="--summary"){foreach(var sql in new[]{"SELECT count(*) FROM quote","SELECT count(*) FROM daily","SELECT count(*) FROM inventory","SELECT count(*) FROM recipe","SELECT count(*) FROM item","PRAGMA integrity_check"})Console.WriteLine(db.Scalar(sql));return 0;}
                    return 2;
                }
            }
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Desk(path));return 0;
        }catch(Exception ex){if(args.Length>0)Console.Error.WriteLine(ex.ToString());else MessageBox.Show(ex.Message,"Forever Hive Exchange");return 1;}
    }
}
}
