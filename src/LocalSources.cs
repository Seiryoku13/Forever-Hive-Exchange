using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ForeverHive {
internal static class LocalSources {
    public static readonly Encoding Bytes=Encoding.GetEncoding(28591);
    public static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=24*1024*1024,RecursionLimit=64};}
    public static Dictionary<string,object> Obj(object o){return o as Dictionary<string,object>??new Dictionary<string,object>();}
    public static string S(Dictionary<string,object> o,string key){object v;return o.TryGetValue(key,out v)&&v!=null?Convert.ToString(v,CultureInfo.InvariantCulture):"";}
    public static long Num(object value){long n;return long.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),out n)?n:0;}
    public static string Market(string region,string label) {
        var m=Regex.Match(label,@"^(PvE|Normal|PvP|Hardcore|RP-PvP|RP)[ -](Horde|Alliance|Neutral|Blackwater)$",RegexOptions.IgnoreCase);
        if(!m.Success||!Regex.IsMatch(region,@"^(US|EU|KR|TW)$",RegexOptions.IgnoreCase))return null;
        string mode=m.Groups[1].Value.ToLowerInvariant().Replace("pve","normal");
        return region.ToUpperInvariant()+" / "+mode+" / "+m.Groups[2].Value.ToLowerInvariant().Replace("blackwater","neutral");
    }
    public static long Base32(string s){long x=0;if(s.Length==0)throw new InvalidDataException("Empty TSM value.");foreach(char c in s){int d="0123456789ABCDEFGHIJKLMNOPQRSTUV".IndexOf(c);if(d<0)throw new InvalidDataException("Unknown TSM digit.");x=checked(x*32+d);}return x;}
    public static byte[] Unquote(string text,ref int i){
        if(text[i++]!='"')throw new InvalidDataException("Expected a quoted SavedVariables value.");
        var b=new List<byte>();
        while(i<text.Length){
            int c=text[i++];if(c=='"')return b.ToArray();
            if(c=='\\'){if(i>=text.Length)break;c=text[i++];
                if(c>='0'&&c<='9'){int val=c-'0',n=1;while(n++<3&&i<text.Length&&text[i]>='0'&&text[i]<='9')val=val*10+text[i++]-'0';c=val;}
                else if(c=='n'||c=='\n')c=10;else if(c=='r')c=13;else if(c=='t')c=9;else if(c=='\r'){if(i<text.Length&&text[i]=='\n')i++;c=10;}
            }b.Add(checked((byte)c));
        }throw new InvalidDataException("Incomplete SavedVariables string.");
    }
    // Decode the finite CBOR containers that Auctionator writes. No Lua is evaluated.
    sealed class Cbor {
        readonly byte[] bytes; int pos;
        public Cbor(byte[] b){bytes=b;}
        byte Read(){if(pos>=bytes.Length)throw new InvalidDataException("Truncated CBOR.");return bytes[pos++];}
        public object Node(int depth=0){
            if(depth>12)throw new InvalidDataException("CBOR depth exceeded.");
            int tag=Read(),major=tag>>5,info=tag&31;
            if(major==7 && (info==26||info==27)){
                byte[] fp=new byte[info==26?4:8];for(int j=0;j<fp.Length;j++)fp[fp.Length-j-1]=Read();
                return Convert.ToInt64(Math.Ceiling(fp.Length==4?BitConverter.ToSingle(fp,0):BitConverter.ToDouble(fp,0)));
            }
            long n=info;
            if(info>=24){if(info>27)throw new InvalidDataException("Unsupported CBOR length.");n=0;for(int k=0;k<(1<<(info-24));k++)n=checked(n*256+Read());}
            if(major==0)return n;if(major==1)return -1-n;
            if(n>bytes.Length)throw new InvalidDataException("Invalid CBOR size.");
            if(major==2||major==3){string s=Bytes.GetString(bytes,pos,(int)n);pos+=(int)n;return s;}
            var map=new Dictionary<string,object>();
            if(major==5){for(long j=0;j<n;j++){string key=Convert.ToString(Node(depth+1),CultureInfo.InvariantCulture);map[key]=Node(depth+1);}return map;}
            if(major==4){for(long j=0;j<n;j++)map[(j+1).ToString()]=Node(depth+1);return map;}
            throw new InvalidDataException("Unsupported CBOR value.");
        }
        public object Decode(){var obj=Node();if(pos!=bytes.Length)throw new InvalidDataException("Extra CBOR data.");return obj;}
    }
    public static int Auctionator(Db db,byte[] bytes,string region){
        var s=Bytes.GetString(bytes);var start=Regex.Match(s,@"(?m)^AUCTIONATOR_PRICE_DATABASE\s*=\s*\{");
        if(!start.Success)throw new InvalidDataException("Auctionator price database missing.");
        int i=start.Index+start.Length,count=0;
        var entry=new Regex(@"\G[\s,]*\[""([^""\r\n]+)""\]\s*=\s*");
        bool version=false, ended=false;
        while(i<s.Length){
            var e=entry.Match(s,i);if(!e.Success){ended=Regex.IsMatch(s.Substring(i,Math.Min(80,s.Length-i)),@"^[\s,]*\}");break;}
            string label=e.Groups[1].Value;i+=e.Length;
            if(label=="__dbversion"){var v=Regex.Match(s.Substring(i,Math.Min(8,s.Length-i)),@"^\d+");version=v.Value=="8";i+=v.Length;continue;}
            var raw=Unquote(s,ref i);string market=Market(region,label);if(market==null)continue;
            var items=Obj(new Cbor(raw).Decode());
            var batch=new StringBuilder();
            foreach(var it in items){
                int id;if(!int.TryParse(it.Key,out id)||id<=0)continue;
                var val=Obj(it.Value);object x;
                var high=val.TryGetValue("h",out x)?Obj(x):null;
                if(high==null)continue;
                var low=val.TryGetValue("l",out x)?Obj(x):Obj(null);var qty=val.TryGetValue("a",out x)?Obj(x):Obj(null);
                foreach(var h in high){
                    int di;if(!int.TryParse(h.Key,out di)||di<0||di>50000)continue;
                    long hi=Num(h.Value),lo=low.TryGetValue(h.Key,out x)?Num(x):hi;
                    if(lo<=0||hi<lo)continue;
                    int day=18262+di; // calendar day offset from 2020-01-01, not a scan timestamp
                    string qu=qty.TryGetValue(h.Key,out x)?Num(x).ToString():"NULL";
                    batch.AppendFormat("INSERT INTO daily VALUES({0},{1},{2},{3},{4},{5}) ON CONFLICT(market,id,day) DO UPDATE SET low=min(low,excluded.low),high=max(high,excluded.high),available=coalesce(max(available,excluded.available),available,excluded.available);",Db.Q(market),id,day,lo,hi,qu);count++;
                    Flush(db,batch,false);
                }
            }Flush(db,batch,true);
        }
        if(!version||!ended)throw new InvalidDataException("Unsupported or incomplete Auctionator v8 table.");
        return count;
    }
    public static void Flush(Db db,StringBuilder b,bool force){if(b.Length>0&&(force||b.Length>65536)){db.Exec(b.ToString());b.Clear();}}
    public static int Tsm(Db db,byte[] bytes,string region){
        string text=Encoding.UTF8.GetString(bytes);int count=0,sections=0;
        if(!text.Contains("\"Forever-"+region.ToUpperInvariant()+"\""))throw new InvalidDataException("TSM region does not match this client.");
        foreach(string line in text.Split('\n')){
            var head=Regex.Match(line,@"LoadData\(""AUCTIONDB_NON_COMMODITY_DATA"",""([^""]+)"",\[\[return \{downloadTime=(\d+),fields=\{([^}]+)\},data=\{");
            if(!head.Success)continue;
            string mk=Market(region,head.Groups[1].Value);if(mk==null)continue;
            long ts=long.Parse(head.Groups[2].Value);if(ts<=0||ts>Db.Now+86400)throw new InvalidDataException("Invalid TSM snapshot date.");
            string[] cols=head.Groups[3].Value.Replace("\"","").Split(',');
            int p=Array.IndexOf(cols,"minBuyout"),q=Array.IndexOf(cols,"numAuctions"),r=Array.IndexOf(cols,"marketValueRecent");
            if(p<1||cols[0]!="itemString")throw new InvalidDataException("Unknown TSM fields.");
            int end=line.IndexOf("}}]])",head.Index+head.Length,StringComparison.Ordinal);
            if(end<0)throw new InvalidDataException("Incomplete TSM snapshot.");
            string data=line.Substring(head.Index+head.Length,end-(head.Index+head.Length));
            var batch=new StringBuilder();sections++;
            foreach(Match row in Regex.Matches(data,@"\{([^{}]+)\}")){
                var a=row.Groups[1].Value.Split(',');int id;if(!int.TryParse(a[0],out id)||id<=0)continue;
                if(a.Length!=cols.Length)throw new InvalidDataException("Incomplete TSM fields.");
                long price=Base32(a[p]);if(price<=0)continue;
                batch.AppendFormat("INSERT OR REPLACE INTO quote VALUES({0},{1},'TSM',{2},{3},{4},{5},NULL);",Db.Q(mk),id,ts,price,q<0?"NULL":Base32(a[q]).ToString(),r<0?"NULL":Base32(a[r]).ToString());count++;
                Flush(db,batch,false);
            }
            Flush(db,batch,true);db.Set("snap/TSM/"+mk,Math.Max(ts,Num(db.Get("snap/TSM/"+mk))).ToString());
        }
        if(sections==0)throw new InvalidDataException("No supported Forever TSM house found.");return count;
    }
    public static int Hive(Db db,byte[] bytes){
        string s=Encoding.UTF8.GetString(bytes);int ct=0;var wire=Regex.Match(s,@"(?ms)^FHCWire\s*=\s*\{(.*?)^\}");
        if(!wire.Success)throw new InvalidDataException("Missing FHCWire snapshot.");
        foreach(Match m in Regex.Matches(wire.Groups[1].Value,@"=\s*""FHC1:([^""]+)""")){
            var rec=Obj(Json().DeserializeObject(Uri.UnescapeDataString(m.Groups[1].Value)));
            string who=S(rec,"name")+" ("+S(rec,"realm")+")",key=S(rec,"id");long updated=Num(S(rec,"updated"));
            object details;if(key==""||!rec.TryGetValue("details",out details)||!(details is object[]))continue;
            if(updated<Num(db.Get("fhc/"+key)))continue;
            db.Exec("DELETE FROM inventory WHERE character="+Db.Q(who));db.Set("fhc/"+key,updated.ToString());
            foreach(var val in (object[])details){
                var row=Obj(val);string location=S(row,"section");long id=Num(S(row,"item")),qty=Num(S(row,"count"));
                if((location!="Inventory"&&location!="Bank")||id<=0||qty<=0)continue;
                string name=S(row,"name");db.Exec("INSERT OR REPLACE INTO inventory VALUES("+Db.Q(who)+","+Db.Q(location)+","+id+","+Db.Q(name)+","+qty+","+Num(S(row,"updated"))+");INSERT OR REPLACE INTO item VALUES("+id+","+Db.Q(name)+");");ct++;
            }
        }return ct;
    }
    public static int Catalog(Db db,byte[] b){
        int count=0;var batch=new StringBuilder();
        foreach(Match m in Regex.Matches(Encoding.UTF8.GetString(b),@"(?m)^\s*\[""([^""\r\n]+)""\]\s*=\s*(\d+)\s*,")){
            batch.AppendFormat("INSERT OR IGNORE INTO item VALUES({0},{1});",m.Groups[2],Db.Q(m.Groups[1].Value));count++;Flush(db,batch,false);
        }Flush(db,batch,true);return count;
    }
    public static int Recipes(Db db,byte[] b){
        int count=0;string prof="";var batch=new StringBuilder();
        foreach(var line in Encoding.UTF8.GetString(b).Split('\n')){
            var group=Regex.Match(line,@"^\s*\[""([^""]+)""\] = \{\s*$");if(group.Success){prof=group.Groups[1].Value;continue;}
            var name=Regex.Match(line,@"^\s*\[""([^""]+)""\] = \{");if(!name.Success)continue;
            var output=Regex.Match(line,@", o = (\d+)");var id=Regex.Match(line,@", i = (\d+)");var mats=Regex.Match(line,@", r = ""(\d+:\d+(?:,\d+:\d+)*)""");var n=Regex.Match(line,@", n = (\d+)");
            if(!output.Success||!id.Success||!mats.Success)continue;
            batch.AppendFormat("INSERT OR REPLACE INTO recipe VALUES({0},{1},{2},{3},{4});INSERT OR IGNORE INTO item VALUES({2},{5});",id.Groups[1],Db.Q(prof+" / "+name.Groups[1].Value),output.Groups[1],n.Success?n.Groups[1].Value:"1",Db.Q(mats.Groups[1].Value),Db.Q(name.Groups[1].Value));count++;Flush(db,batch,false);
        }Flush(db,batch,true);return count;
    }
    static void ReadChanged(Db db,string file,string label,Func<byte[],int> import,List<string> errors){
        if(!File.Exists(file))return;
        try{
            var info=new FileInfo(file);string stat=info.Length+"/"+info.LastWriteTimeUtc.Ticks, k="file/"+file;
            if(db.Get(k)==stat)return;
            if(info.Length>128*1024*1024)throw new InvalidDataException("Source exceeds size limit.");
            byte[] data=File.ReadAllBytes(file);
            if(File.GetLastWriteTimeUtc(file)!=info.LastWriteTimeUtc)throw new IOException("Source changed during read.");
            db.Transaction(delegate{int n=import(data);db.Set(k,stat);db.State(label,n.ToString("N0")+" records read");});
        }catch(Exception ex){db.State(label,"Retry pending: "+ex.Message);errors.Add(label+": "+ex.Message);}
    }
    public static List<string> Sync(Db db,string dir){
        var errors=new List<string>();if(!Directory.Exists(dir)||!Path.GetFileName(dir.TrimEnd('\\','/')).Equals("_forever_",StringComparison.OrdinalIgnoreCase)){db.State("WoW client","Choose the _forever_ folder");return errors;}
        var addon=Path.Combine(dir,@"Interface\AddOns");string cfg=Path.Combine(dir,@"WTF\Config.wtf");
        var portal=File.Exists(cfg)?Regex.Match(File.ReadAllText(cfg),@"(?im)^SET portal ""(US|EU|KR|TW)"""):Match.Empty;
        string region=portal.Success?portal.Groups[1].Value.ToUpperInvariant():"";
        ReadChanged(db,Path.Combine(addon,@"Mastercraft\ItemData.lua"),"Mastercraft items",b=>Catalog(db,b),errors);
        ReadChanged(db,Path.Combine(addon,@"Mastercraft\RecipeData.lua"),"Mastercraft recipes",b=>Recipes(db,b),errors);
        if(region!="")ReadChanged(db,Path.Combine(addon,@"TradeSkillMaster_AppHelper\AppData.lua"),"TradeSkillMaster",b=>Tsm(db,b,region),errors);
        var acc=Path.Combine(dir,@"WTF\Account");if(Directory.Exists(acc))foreach(var folder in Directory.GetDirectories(acc)){
            string sv=Path.Combine(folder,"SavedVariables");
            if(region!="")ReadChanged(db,Path.Combine(sv,"Auctionator.lua"),"Auctionator",b=>Auctionator(db,b,region),errors);
            ReadChanged(db,Path.Combine(sv,"ForeverHiveCommand.lua"),"Forever Hive Command",b=>Hive(db,b),errors);
        }
        db.State("WoW client",region==""?"Unknown portal in WTF/Config.wtf":region+" / local files checked");return errors;
    }
}
}
