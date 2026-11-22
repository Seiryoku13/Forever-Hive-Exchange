using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ForeverHive {
internal static class Tests {
    static int count;
    static void Check(bool truth,string what){if(!truth)throw new Exception(what);Console.WriteLine("PASS "+what);count++;}
    static void Reject(Action act,string what){try{act();}catch{Check(true,what);return;}throw new Exception("Accepted invalid input: "+what);}
    static string Feed(long ts){return "{\"house\":{\"key\":\"us-2-114\"},\"t\":"+(ts*1000)+",\"f\":[\"listed\",\"vol24\",\"ask\"],\"items\":[{\"id\":2589,\"name\":\"Linen Cloth\",\"o\":[3,null,100]}]}";}
    static byte[] Cache(long ts){return Encoding.ASCII.GetBytes("select(2, ...).LoadData(\"APP_INFO\",\"Forever-US\",[[fixture]])\nselect(2, ...).LoadData(\"AUCTIONDB_NON_COMMODITY_DATA\",\"Normal-Horde\",[[return {downloadTime="+ts+",fields={\"itemString\",\"minBuyout\",\"numAuctions\",\"marketValueRecent\"},data={{2589,34,A,68},{\"i:2589:42\",1,1,1},{123,1,1,1}}}]])");}
    // Every test runs in its own generated temporary directory. Never touch the user's client or live data.
    [STAThread] public static int Main(){
        var dir=Path.Combine(Path.GetTempPath(),"ForexHiveTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string file=Path.Combine(dir,"synthetic.sqlite");
        try{
            Check(Economy.Net(200,500,2)==188,"sale cut and lost deposit in copper");
            Check(Economy.Net(1,500,0)==0,"conservative rounding of fractional proceeds");
            Check(Economy.Undercut(1)==1&&Economy.Undercut(100)==99,"minimum post and one-copper undercut");
            Check(Economy.Median(new long[]{9,1,5,3})==4,"even sample median");
            for(int fee=0;fee<=9900;fee+=99)for(long cost=1;cost<300;cost+=13){long floor=Economy.Floor(cost,fee,5);if(Economy.Net(floor,fee,5)<cost||Economy.Net(floor-1,fee,5)>=cost)throw new Exception("break-even invariant");}
            Check(true,"break-even boundary across 2,323 fee and cost combinations");
            Check(LocalSources.Base32("1000000")==1073741824L,"TSM extended base-32 price");
            int pos=0;Check(LocalSources.Unquote("\"a\\000\\255\\\"b\"",ref pos).SequenceEqual(new byte[]{97,0,255,34,98}),"Lua byte escapes");
            string mk="US / normal / horde";Check(LocalSources.Market("US","PvE Horde")==mk&&Network.Key(mk)=="us-2-114","house identity across sources");
            Check(Network.Key("EU / pvp / alliance")=="eu-2-110"&&LocalSources.Market("US","Unknown")==null,"regional isolation and unknown house handling");
            long now=Db.Now;int day=(int)(now/86400);
            using(var db=new Db(file)){
                db.Init();db.Set("migration-test","keep");db.Exec("DROP TABLE change_note;PRAGMA user_version=1");db.Init();
                Check(db.Get("migration-test")=="keep"&&db.Scalar("PRAGMA user_version")=="2"&&db.Scalar("SELECT count(*) FROM change_note")=="0","v1 migration preserves existing state and creates hotfix storage");
                db.Exec("PRAGMA user_version=99");Reject(()=>db.Init(),"newer database schema rejected");db.Exec("PRAGMA user_version=2");
                LocalSources.Tsm(db,Cache(now),"US");
                Check(db.Scalar("SELECT ask||','||qty||','||ref FROM quote WHERE id=2589")=="100,10,200","TSM field decoding anchored to names");
                Check(db.Scalar("SELECT count(*) FROM quote")=="2","variant rows excluded");
                LocalSources.Tsm(db,Cache(now),"US");Check(db.Scalar("SELECT count(*) FROM quote")=="2","repeat import is idempotent");
                Reject(()=>LocalSources.Tsm(db,Cache(now),"EU"),"mismatched TSM region rejected");
                var cbor=new byte[]{0xA1,0x44,50,53,56,57,0xA4,0x41,104,0xA1,0x41,49,0x18,100,0x41,108,0x80,0x41,97,0xA1,0x41,49,2,0x41,109,0x18,100};
                string auction="AUCTIONATOR_PRICE_DATABASE = {\n[\"__dbversion\"] = 8,\n[\"PvE Horde\"] = \""+LocalSources.Bytes.GetString(cbor)+"\",\n}";
                LocalSources.Auctionator(db,LocalSources.Bytes.GetBytes(auction),"US");
                Check(db.Scalar("SELECT day||','||low||','||high||','||available FROM daily")=="18263,100,100,2","Auctionator v8 CBOR, low fallback, date and peak supply");
                Reject(()=>db.Transaction(()=>LocalSources.Auctionator(db,LocalSources.Bytes.GetBytes(auction.Replace("= 8","= 99")),"US")),"unknown Auctionator schema rolls back");
                for(int i=1;i<=12;i++)db.Exec("INSERT INTO daily VALUES('"+mk+"',2589,"+(day-i)+","+(i<=3?180:150)+",200,15)");
                var a=new Analysis(db,mk,500,0);Check(a.Items[2589].Flip&&a.Items[2589].Invest,"evidence-gated flip and rising-history investment");
                Check(!a.Items[123].Flip,"missing history suppresses recommendations");
                Network.Overview(db,mk,Feed(now+1));var b=new Analysis(db,mk,500,0);
                Check(b.Source=="Public feed"&&b.Items.Count==1&&b.Items[2589].Volume==-1,"newest full snapshot suppresses absent listings and retains unknown demand");
                Check(b.Items[2589].Recent==200,"contemporaneous TSM value constrains the public feed resale target");
                Reject(()=>Network.Overview(db,"EU / normal / horde",Feed(now)),"wrong feed house rejected");
                Reject(()=>db.Transaction(()=>{db.Set("partial","bad");Network.Overview(db,mk,"{}");}),"malformed feed rejected");
                Check(db.Get("partial")==""&&db.Scalar("SELECT count(*) FROM quote")=="3","failed transaction preserves previous snapshot");
                db.Exec("INSERT INTO recipe VALUES(1,'Fixture',2589,2,'2589:1'); INSERT INTO recipe VALUES(2,'Missing reagent',2589,1,'999:1');");
                var crafts=b.Crafts(db);Check(crafts.Rows.Count==1&&(long)crafts.Rows[0][4]==88,"craft yield, full material coverage and gross rounding");
                Analysis.Record(db,mk,2589,true,10,100,10);Analysis.Record(db,mk,2589,false,2,200,20);
                var pt=b.Portfolio(db);Check(pt.Rows.Count==1&&pt.Rows[0][1].ToString()=="8"&&pt.Rows[0][4].ToString()==Economy.Money(178),"weighted-average cost basis and realized P&L after fees");
                Reject(()=>Analysis.Record(db,mk,2589,false,9,100,0),"oversell blocked transactionally");
                Check(db.Scalar("SELECT count(*) FROM trade")=="2","rejected sale preserves positions");
                Network.SeedNews(db);Network.NewsIndex(db,"<a href='/en-us/news/123/wow-forever-preview'>Preview</a>");
                Check(db.Scalar("SELECT kind FROM event WHERE url LIKE '%/123'")=="Announcement","Forever news classification and canonical link");
                Check(Network.Hotfixes(db,"<h3>November 20, 2026</h3><ul><li>Test change</li></ul>")==1,"dated hotfix parser");
                string today=DateTime.UtcNow.ToString("MMMM d, yyyy",System.Globalization.CultureInfo.InvariantCulture);
                Network.Hotfixes(db,"<p>"+today+"</p><ul><li>Linen Cloth requirements changed.</li></ul>");
                Check(new Analysis(db,mk,500,0).Items[2589].Theme.Contains("requirements changed"),"fresh dated patch matched to a screened item");
                Check(!Analysis.MentionsItem("Mooncloth stockpiles","Cloth")&&Analysis.MentionsItem("Reduced costs for Linen Cloth.","linen cloth"),"item match respects word boundaries");
                Check(Network.BlueFeed(db,"<rss><channel><item><link>https://news.blizzard.com/en-us/article/1</link><title>Upcoming test</title></item><item><link>https://example.com</link><title>Other</title></item></channel></rss>")==1,"RSS fallback accepts official destinations only");
                var payload=Encoding.UTF8.GetBytes("fixture");Check(Updates.Verify("sha256:"+Updates.Digest(payload),payload)&&!Updates.Verify("sha256:"+new string('0',64),payload),"updater accepts matching digest and rejects modified asset");
                db.Set("snap/TSM/"+mk,(now-30000).ToString());db.Set("snap/Public feed/"+mk,"0");db.Exec("UPDATE quote SET ts="+(now-30000)+" WHERE src='TSM'");
                var stale=new Analysis(db,mk,500,0);Check(!stale.Items[2589].Fresh&&!stale.Items[2589].Flip&&stale.Crafts(db).Rows.Count==0,"stale feed suppresses actionable recommendations");
                var wow=Path.Combine(dir,"_forever_");Directory.CreateDirectory(Path.Combine(wow,"WTF"));File.WriteAllText(Path.Combine(wow,@"WTF\Config.wtf"),"SET portal \"US\"");
                var addon=Path.Combine(wow,@"Interface\AddOns\TradeSkillMaster_AppHelper");Directory.CreateDirectory(addon);string source=Path.Combine(addon,"AppData.lua");
                File.WriteAllBytes(source,Cache(now+2));LocalSources.Sync(db,wow);int initial=int.Parse(db.Scalar("SELECT count(*) FROM quote"));LocalSources.Sync(db,wow);
                Check(int.Parse(db.Scalar("SELECT count(*) FROM quote"))==initial,"unchanged saved file skipped on polling");
                File.WriteAllText(source,"incomplete");Check(LocalSources.Sync(db,wow).Count==1&&int.Parse(db.Scalar("SELECT count(*) FROM quote"))==initial,"torn source is retried with prior state intact");
                File.WriteAllBytes(source,Cache(now+3));Check(LocalSources.Sync(db,wow).Count==0&&db.Get("snap/TSM/"+mk)==(now+3).ToString(),"source recovers on next changed-file poll");
                string fhc="FHCWire = {\n[\"a\"] = \"FHC1:"+Uri.EscapeDataString("{\"id\":\"a\",\"name\":\"Test\",\"realm\":\"Normal\",\"updated\":100,\"details\":[{\"section\":\"Inventory\",\"item\":2589,\"name\":\"Linen Cloth\",\"count\":12,\"updated\":99}]}")+"\",\n}";
                LocalSources.Hive(db,Encoding.UTF8.GetBytes(fhc));Check(db.Scalar("SELECT qty FROM inventory")=="12","Forever Hive Command FHC1 inventory decoding");
                var sw=Stopwatch.StartNew();db.Exec("WITH RECURSIVE n(x) AS (SELECT 1 UNION ALL SELECT x+1 FROM n WHERE x<10000) INSERT INTO quote SELECT 'PERF',x,'TSM',"+now+",100,1,100,NULL FROM n;");db.Set("snap/TSM/PERF",now.ToString());var perf=new Analysis(db,"PERF",500,0);sw.Stop();
                Check(perf.Items.Count==10000,"10,000-item ingestion and model reload ("+sw.ElapsedMilliseconds+" ms)");
            }
            using(var db=new Db(file)){Check(db.Scalar("PRAGMA integrity_check")=="ok"&&db.Scalar("SELECT count(*) FROM trade")=="2","database integrity and persistence across reopen");}
            Application.EnableVisualStyles();using(var ui=new Desk(file)){ui.Go("Portfolio");Check(ui.ShownRows==1,"desktop portfolio rows bound");foreach(var view in new[]{"Market","Flips","Investments","Crafting","Inventory","Price alerts","Patch watch","Sources"})ui.Go(view);Check(ui.ShownRows>0,"desktop navigation and source status");}
            Console.WriteLine(count+" checks passed.");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{try{Directory.Delete(dir,true);}catch(IOException){}}
    }
}
}
