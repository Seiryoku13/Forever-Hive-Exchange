using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ForeverHive {
internal sealed class Listing {
    public int Id;public string Name,Source;public long Ask,Quantity,Recent,Volume,Time,Baseline,Target,Profit;public int Days,Span;
    public bool Fresh,Invest,Flip;public decimal Trend,ROI;
    public string Label{get{return String.IsNullOrEmpty(Name)?"Item #"+Id:Name;}}
    public string Theme{
        get{
            string n=Label.ToLowerInvariant();
            if(n.Contains("stormshroud"))return "Stormshroud hotfix • demand uncertain";
            if(DateTime.UtcNow.Date<=new DateTime(2026,12,9)&&(n.Contains("elixir")||n.Contains("potion")||n.Contains("flask")))return "Dec 9 raid • demand hypothesis";
            return "";
        }
    }
}
internal sealed class Analysis {
    public readonly Dictionary<int,Listing> Items=new Dictionary<int,Listing>();
    public string Source,Market;public long Snapshot;public int Fee;public long Loss;public int DailyCount;
    static long L(DataRow row,int i){return LocalSources.Num(row[i]);}
    public Analysis(Db db,string market,int fee,long loss){
        Market=market;Fee=fee;Loss=loss;string m=Db.Q(market);
        long ts1=LocalSources.Num(db.Get("snap/TSM/"+market)),ts2=LocalSources.Num(db.Get("snap/Public feed/"+market));
        Snapshot=Math.Max(ts1,ts2);Source=ts2>0&&ts2>=ts1?"Public feed":"TSM";
        string refPrice="q.ref";
        if(Source=="Public feed"&&ts1>0&&Math.Abs(ts2-ts1)<=3600){
            refPrice="coalesce(q.ref,(SELECT t.ref FROM quote t WHERE t.market=q.market AND t.id=q.id AND t.src='TSM' AND t.ts="+ts1+"))";
        }
        string sql="SELECT q.id,i.name,q.ask,q.qty,"+refPrice+",q.sold FROM quote q LEFT JOIN item i ON i.id=q.id WHERE q.market="+m+" AND q.src="+Db.Q(Source)+" AND q.ts="+Snapshot;
        if(Snapshot==0){Source="Auctionator daily low";sql="SELECT d.id,i.name,d.low,NULL,NULL,NULL FROM daily d LEFT JOIN item i ON i.id=d.id WHERE market="+m+" AND day=(SELECT max(day) FROM daily WHERE market="+m+")";}
        foreach(DataRow r in db.Query(sql).Rows){
            var it=new Listing{Id=(int)L(r,0),Name=r[1].ToString(),Ask=L(r,2),Quantity=r.IsNull(3)?-1:L(r,3),Recent=L(r,4),Volume=r.IsNull(5)?-1:L(r,5),Time=Snapshot,Source=Source,Fresh=Snapshot>0&&Db.Now-Snapshot<=21600&&Snapshot<=Db.Now+600};
            Items[it.Id]=it;
        }
        int day=(int)((Snapshot==0?Db.Now:Snapshot)/86400);
        var days=new Dictionary<int,SortedDictionary<int,long>>();
        // Aggregate provider observations before using Auctionator's daily minimum for overlapping days.
        string hist="SELECT id,CAST(ts/86400 AS INTEGER) AS day,min(ask) AS low FROM quote WHERE market="+m+" AND ts>="+((long)(day-21)*86400)+" AND ts<"+((long)day*86400)+" GROUP BY id,day UNION ALL SELECT id,day,low FROM daily WHERE market="+m+" AND day BETWEEN "+(day-21)+" AND "+(day-1);
        foreach(DataRow r in db.Query(hist).Rows){int id=(int)L(r,0);if(!Items.ContainsKey(id))continue;SortedDictionary<int,long> h;if(!days.TryGetValue(id,out h))days[id]=h=new SortedDictionary<int,long>();int d=(int)L(r,1);long lo=L(r,2);if(lo>0)h[d]=h.ContainsKey(d)?Math.Min(h[d],lo):lo;}
        DailyCount=(int)LocalSources.Num(db.Scalar("SELECT count(*) FROM daily WHERE market="+m));
        foreach(var it in Items.Values){
            SortedDictionary<int,long> h;if(!days.TryGetValue(it.Id,out h))continue;
            it.Days=h.Count;it.Span=h.Keys.Last()-h.Keys.First()+1;it.Baseline=Economy.Median(h.Values);
            long anchor=it.Recent>0?Math.Min(it.Recent,it.Baseline):it.Baseline;
            it.Target=Economy.Undercut(anchor);it.Profit=Economy.Net(it.Target,fee,loss)-it.Ask;it.ROI=it.Profit*100m/it.Ask;
            var near=h.Where(x=>x.Key>=day-3).Select(x=>x.Value);var prev=h.Where(x=>x.Key>=day-14&&x.Key<day-3).Select(x=>x.Value);
            long recent=Economy.Median(near),prior=Economy.Median(prev);if(prior>0&&recent>0)it.Trend=(recent-prior)*100m/prior;
            bool eligible=it.Fresh&&h.Keys.Last()>=day-3&&it.Profit>0&&it.Volume!=0;
            it.Flip=eligible&&it.Days>=3&&it.ROI>=15;
            it.Invest=eligible&&it.Days>=7&&near.Count()>=2&&prev.Count()>=4&&it.Trend>=5&&it.ROI>=10;
        }
    }
    public DataTable Crafts(Db db){
        var t=new DataTable();t.Columns.Add("Profession / recipe");t.Columns.Add("Reagents");t.Columns.Add("Output net");t.Columns.Add("Margin");t.Columns.Add("Margin (c)",typeof(long));
        if(Snapshot==0||Db.Now-Snapshot>21600)return t;
        foreach(DataRow r in db.Query("SELECT name,output,units,materials FROM recipe").Rows){
            Listing output;if(!Items.TryGetValue((int)L(r,1),out output))continue;
            long cost=0;bool complete=true;
            foreach(var mat in r[3].ToString().Split(',')){var pair=mat.Split(':');Listing input;if(!Items.TryGetValue(int.Parse(pair[0]),out input)){complete=false;break;}cost=checked(cost+input.Ask*int.Parse(pair[1]));}
            if(!complete||cost<=0)continue;
            long net=Economy.Net(checked(Economy.Undercut(output.Ask)*L(r,2)),Fee,checked(Loss*L(r,2)));
            t.Rows.Add(r[0],Economy.Money(cost),Economy.Money(net),Economy.Money(net-cost),net-cost);
        }
        t.DefaultView.Sort="[Margin (c)] DESC";return t;
    }
    public static void Record(Db db,string market,int item,bool buy,int qty,long unit,long totalFees){
        if(item<=0||qty<=0||unit<=0||totalFees<0||qty>1000000||unit>1000000000||totalFees>1000000000000000)throw new ArgumentException("Enter a positive item ID, quantity and price within the form limits.");
        db.Transaction(delegate{
            if(!buy){long owned=LocalSources.Num(db.Scalar("SELECT sum(case side when 'Buy' then qty else -qty end) FROM trade WHERE market="+Db.Q(market)+" AND id="+item));if(owned<qty)throw new InvalidOperationException("This sale exceeds the quantity recorded in your ledger.");}
            db.Exec("INSERT INTO trade(market,id,side,qty,unit,fee,ts) VALUES("+Db.Q(market)+","+item+","+Db.Q(buy?"Buy":"Sell")+","+qty+","+unit+","+totalFees+","+Db.Now+")");
        });
    }
    public DataTable Portfolio(Db db){
        var t=new DataTable();foreach(var col in new[]{"Item","Owned","Open cost basis","Estimated net value","Realized P&L","Unrealized P&L"})t.Columns.Add(col);
        foreach(var group in db.Query("SELECT id,side,qty,unit,fee FROM trade WHERE market="+Db.Q(Market)+" ORDER BY seq").Rows.Cast<DataRow>().GroupBy(r=>(int)L(r,0))){
            long held=0;decimal basis=0,realized=0;
            foreach(var r in group){long n=L(r,2),value=checked(n*L(r,3)),fees=L(r,4);if(r[1].ToString()=="Buy"){basis+=value+fees;held+=n;}else{decimal soldBasis=n==held?basis:basis*n/held;realized+=value-fees-soldBasis;basis-=soldBasis;held-=n;}}
            Listing it;bool known=Items.TryGetValue(group.Key,out it)&&it.Fresh;long mark=known?Economy.Net(it.Ask*held,Fee,Loss*held):0;
            t.Rows.Add(it==null?"Item #"+group.Key:it.Label,held,Economy.Money((long)Math.Round(basis)),known?Economy.Money(mark):"—",Economy.Money((long)Math.Round(realized)),known?Economy.Money((long)Math.Round(mark-basis)):"—");
        }return t;
    }
}
}
