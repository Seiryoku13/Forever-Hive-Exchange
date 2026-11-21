using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ForeverHive {
internal static class Updates {
    const string Api="https://api.github.com/repos/Seiryoku13/Forever-Hive-Exchange/releases/latest";
    public static string Digest(byte[] b){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
    public static bool Verify(string digest,byte[] bytes){return Regex.IsMatch(digest??"",@"^sha256:[a-f0-9]{64}$")&&String.Equals(digest,"sha256:"+Digest(bytes),StringComparison.Ordinal);}
    public static void Check(Db db,string data,bool force){
        if(!force&&Db.Now-LocalSources.Num(db.Get("try/update"))<86400)return;
        db.Set("try/update",Db.Now.ToString());
        try{
            var release=LocalSources.Obj(LocalSources.Json().DeserializeObject(Encoding.UTF8.GetString(Network.Fetch(Api,1024*1024))));
            string tag=LocalSources.S(release,"tag_name");Version next;
            if(!Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$")||!Version.TryParse(tag.Substring(1),out next))throw new InvalidDataException("Unrecognized release version.");
            if(next<=new Version(App.Version)){db.State("App updates","Current version "+App.Version);return;}
            if(db.Get("updates","0")!="1"){db.State("App updates","Version "+tag+" available on GitHub");return;}
            var asset=((object[])release["assets"]).Select(LocalSources.Obj).Single(a=>LocalSources.S(a,"name")=="ForeverHiveExchange-Setup.exe");
            string url=LocalSources.S(asset,"browser_download_url"),digest=LocalSources.S(asset,"digest");
            if(url!=Network.Repo+"/releases/download/"+tag+"/ForeverHiveExchange-Setup.exe")throw new InvalidDataException("Installer URL did not match this release.");
            byte[] payload=Network.Fetch(url,32*1024*1024);if(!Verify(digest,payload))throw new InvalidDataException("Release checksum verification failed.");
            string dir=Path.Combine(data,"Updates");Directory.CreateDirectory(dir);string path=Path.Combine(dir,tag+"-Setup.exe");
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllBytes(temp,payload);
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
            db.Set("staged",tag);db.Set("digest",digest);db.State("App updates",tag+" verified • applies on exit");
        }catch(Exception ex){db.State("App updates","Check unavailable: "+ex.Message);}
    }
    public static void Apply(Db db,string data){
        string tag=db.Get("staged");if(db.Get("updates")!="1"||!Regex.IsMatch(tag,@"^v\d+\.\d+\.\d+$")||new Version(tag.Substring(1))<=new Version(App.Version))return;
        string path=Path.Combine(data,"Updates",tag+"-Setup.exe");if(!File.Exists(path)||!Verify(db.Get("digest"),File.ReadAllBytes(path)))return;
        string dir=AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        string portable=File.Exists(Path.Combine(dir,"portable.flag"))?" --portable":"";
        Process.Start(new ProcessStartInfo(path,"--target \""+dir+"\" --quiet --wait "+Process.GetCurrentProcess().Id+portable){UseShellExecute=false});
    }
}
}
