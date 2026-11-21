using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace ForeverHive {
internal static class Network {
    const string Feed="https://auctionhouse.tk.gg";
    const string News="https://worldofwarcraft.blizzard.com";
    public const string Repo="https://github.com/Seiryoku13/Forever-Hive-Exchange";
    public static readonly string[] Modes={"pvp","normal","hardcore","rp","rp-pvp"};
    public static string Key(string market){
        var p=market.Split(new[]{" / "},StringSplitOptions.None);if(p.Length!=3||(p[0]!="US"&&p[0]!="EU"))return null;
        int i=Array.IndexOf(Modes,p[1]),j=Array.IndexOf(new[]{"alliance","horde","neutral"},p[2]);if(i<0||j<0)return null;
        int[,] houses={{110,113,116},{111,114,117},{112,115,118},{119,120,121},{123,124,125}};
        return p[0].ToLowerInvariant()+"-2-"+houses[i,j];
    }
    public static byte[] Fetch(string url,int limit){
        ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
        var req=(HttpWebRequest)WebRequest.Create(url);req.Timeout=10000;req.ReadWriteTimeout=10000;req.UserAgent="ForeverHiveExchange/"+App.Version;
        req.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
        using(var res=req.GetResponse())using(var s=res.GetResponseStream())using(var mem=new MemoryStream()){
            byte[] buf=new byte[16384];int n;while((n=s.Read(buf,0,buf.Length))>0){if(mem.Length+n>limit)throw new InvalidDataException("Response exceeds the size limit.");mem.Write(buf,0,n);}return mem.ToArray();
        }
    }
    static bool Due(Db db,string key,int seconds){long last=LocalSources.Num(db.Get("try/"+key));if(Db.Now-last<seconds)return false;db.Set("try/"+key,Db.Now.ToString());return true;}
    public static int Overview(Db db,string market,string json){
        var o=LocalSources.Obj(LocalSources.Json().DeserializeObject(json));var h=LocalSources.Obj(o["house"]);
        if(LocalSources.S(h,"key")!=Key(market))throw new InvalidDataException("House identifier mismatch.");
        if(o["t"]==null)throw new InvalidDataException("No published snapshot yet.");
        long ts=LocalSources.Num(o["t"])/1000;if(ts<=0||ts>Db.Now+600)throw new InvalidDataException("Invalid feed time.");
        var fields=((object[])o["f"]).Select(Convert.ToString).ToArray();
        int ask=Array.IndexOf(fields,"ask"),qty=Array.IndexOf(fields,"listed"),vol=Array.IndexOf(fields,"vol24");
        if(ask<0||qty<0)throw new InvalidDataException("Unrecognized feed columns.");
        int count=0;var batch=new StringBuilder();
        foreach(object raw in (object[])o["items"]){
            var it=LocalSources.Obj(raw);long id=LocalSources.Num(it["id"]);var row=(object[])it["o"];
            if(id<=0||row.Length!=fields.Length)throw new InvalidDataException("Malformed market row.");
            long price=LocalSources.Num(row[ask]);if(price<=0)continue;
            batch.AppendFormat("INSERT OR REPLACE INTO quote VALUES({0},{1},'Public feed',{2},{3},{4},NULL,{5});",Db.Q(market),id,ts,price,row[qty]==null?"NULL":LocalSources.Num(row[qty]).ToString(),vol<0||row[vol]==null?"NULL":LocalSources.Num(row[vol]).ToString());
            string name=LocalSources.S(it,"name");if(name!="")batch.AppendFormat("INSERT OR REPLACE INTO item VALUES({0},{1});",id,Db.Q(name));count++;LocalSources.Flush(db,batch,false);
        }
        LocalSources.Flush(db,batch,true);db.Set("snap/Public feed/"+market,Math.Max(ts,LocalSources.Num(db.Get("snap/Public feed/"+market))).ToString());return count;
    }
    public static void SeedNews(Db db){
        db.Exec(@"INSERT OR IGNORE INTO event VALUES('https://worldofwarcraft.blizzard.com/en-us/news/24315281','Applied','November 20 hotfixes','2026-11-20','Stormshroud set energy return reduced. Check demand for these pieces against your own house history.');
        INSERT OR IGNORE INTO event VALUES('https://worldofwarcraft.blizzard.com/en-us/news/24303862/world-of-warcraft-forever-whats-next','Announced','First Forever raid unlocks','2026-12-09','A raid opening is a reason to watch flasks, potions and elixirs. Any demand effect is a hypothesis.');");
    }
    public static int NewsIndex(Db db,string html){
        int n=0;
        foreach(Match a in Regex.Matches(html.Replace("\\/","/"),@"(?<url>/en-us/(?:news|article)/(?<id>\d+)/[a-z0-9-]+)",RegexOptions.IgnoreCase)){
            string u=a.Groups["url"].Value;
            string slug=u.Substring(u.LastIndexOf('/')+1);
            if(!Regex.IsMatch(slug,@"(?:wow|warcraft)-forever",RegexOptions.IgnoreCase))continue;
            string title=WebUtility.HtmlDecode(slug.Replace('-',' '));if(title.Length>260)continue;
            string kind=slug.Contains("hotfix")?"Hotfix article":Regex.IsMatch(slug,@"preview|whats-next|roadmap|announc|arriv|upcoming")?"Announcement":"Official news";
            string url=News+"/en-us/news/"+a.Groups["id"].Value;
            db.Exec("INSERT OR IGNORE INTO event VALUES("+Db.Q(url)+","+Db.Q(kind)+","+Db.Q(title)+",'','Open the official article for timing and details.')");
            n++;
        }if(n==0)throw new InvalidDataException("No Forever articles recognized on the official news index.");return n;
    }
    public static bool Official(string input){
        Uri u;if(!Uri.TryCreate(input,UriKind.Absolute,out u)||u.Scheme!="https"||!u.IsDefaultPort||u.UserInfo!="")return false;
        return new[]{"worldofwarcraft.blizzard.com","news.blizzard.com","us.forums.blizzard.com","eu.forums.blizzard.com"}.Contains(u.Host);
    }
    public static int BlueFeed(Db db,string rss){
        var doc=new XmlDocument{XmlResolver=null};
        using(var reader=XmlReader.Create(new StringReader(rss),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))doc.Load(reader);
        int count=0;
        foreach(XmlNode it in doc.SelectNodes("/rss/channel/item")){
            if(it["title"]==null||it["link"]==null)continue;string title=it["title"].InnerText,url=it["link"].InnerText.Trim();
            if(!Official(url))continue;
            string kind=Regex.IsMatch(title,"hotfix",RegexOptions.IgnoreCase)?"Hotfix article":Regex.IsMatch(title,"preview|roadmap|announc|unlock|arriv",RegexOptions.IgnoreCase)?"Announcement":"Official news";
            db.Exec("INSERT OR IGNORE INTO event VALUES("+Db.Q(url)+","+Db.Q(kind)+","+Db.Q(title)+",'','Official post indexed by WoW Campfire. Open for timing.')");count++;
        }
        if(count==0)throw new InvalidDataException("No official Blizzard links in RSS feed.");return count;
    }
    public static int Hotfixes(Db db,string html){
        var matches=Regex.Matches(html,@"(?is)>\s*((?:January|February|March|April|May|June|July|August|September|October|November|December) \d{1,2}, \d{4})\s*<");
        int count=0;
        for(int i=0;i<matches.Count;i++){
            var m=matches[i];DateTime d;if(!DateTime.TryParseExact(m.Groups[1].Value,"MMMM d, yyyy",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out d))continue;
            int from=m.Index+m.Length,to=i+1<matches.Count?matches[i+1].Index:Math.Min(html.Length,from+6000);
            string body=html.Substring(from,to-from);int footer=body.IndexOf("</article>",StringComparison.OrdinalIgnoreCase);if(footer>=0)body=body.Substring(0,footer);
            var lines=Regex.Matches(body,@"(?is)<li\b[^>]*>(.*?)</li>").Cast<Match>()
                .Select(a=>Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(a.Groups[1].Value,"<[^>]+>"," ")),"\\s+"," ").Trim()).Where(x=>x.Length>0).ToArray();
            if(lines.Length==0)continue;
            string day=d.ToString("yyyy-MM-dd"),url=News+"/en-us/news/24315281#"+day;
            string notes=String.Join(" • ",lines.Take(3));if(notes.Length>350)notes=notes.Substring(0,347)+"…";
            db.Exec("INSERT OR REPLACE INTO event VALUES("+Db.Q(url)+",'Applied',"+Db.Q("Forever hotfixes • "+m.Groups[1].Value)+","+Db.Q(day)+","+Db.Q(notes)+")");count++;
        }
        if(count==0)throw new InvalidDataException("Could not recognize dated hotfix notes.");return count;
    }
    public static void Poll(Db db,string market){
        string key=Key(market);
        if(key!=null && Due(db,"market/"+key,3600)){
            string label="Public feed / "+market;
            try{string json=Encoding.UTF8.GetString(Fetch(Feed+"/api/m/"+key,16*1024*1024));db.Transaction(delegate{int n=Overview(db,market,json);db.State(label,n+" prices received");});}
            catch(Exception ex){db.State(label,"Unavailable: "+ex.Message);}
        }
        if(Due(db,"news-index-v2",21600)){
            try{string html=Encoding.UTF8.GetString(Fetch("https://news.blizzard.com/en-us/feed/world-of-warcraft",6*1024*1024));db.Transaction(delegate{int n=NewsIndex(db,html);db.State("Blizzard news",n+" Forever article links checked");});}
            catch(Exception){try{string rss=Encoding.UTF8.GetString(Fetch("https://wowcampfire.com/blue-tracker/feed.xml",4*1024*1024));db.Transaction(delegate{int n=BlueFeed(db,rss);db.State("Blizzard news",n+" official links via WoW Campfire");});}
                catch(Exception ex){db.State("Blizzard news","Unavailable: "+ex.Message);}}
            try{
                string json=Encoding.UTF8.GetString(Fetch("https://us.forums.blizzard.com/en/wow/t/2403040.json",6*1024*1024));
                var thread=LocalSources.Obj(LocalSources.Json().DeserializeObject(json));var stream=LocalSources.Obj(thread["post_stream"]);
                var first=((object[])stream["posts"]).Select(LocalSources.Obj).First(p=>LocalSources.Num(p["post_number"])==1);
                db.Transaction(delegate{int n=Hotfixes(db,LocalSources.S(first,"cooked"));db.State("Blizzard hotfixes",n+" dated entries checked");});
            }catch(Exception ex){db.State("Blizzard hotfixes","Unavailable: "+ex.Message);}
        }
    }
}
}
