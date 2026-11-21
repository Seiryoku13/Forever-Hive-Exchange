using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace ForeverHive {
internal sealed class Desk : Form {
    readonly string path;string market,screen="Market",wow;int fee;long loss;bool busy,starting=true;
    Analysis model;
    readonly Panel nav=new Panel(),head=new Panel(),summary=new Panel(),tools=new Panel();
    readonly Label heading=new Label(),caption=new Label(),status=new Label(),metrics=new Label();
    readonly DataGridView grid=new DataGridView();
    readonly TextBox filter=new TextBox();
    readonly ComboBox houses=new ComboBox();readonly FlowLayoutPanel actions=new FlowLayoutPanel();
    readonly Dictionary<string,Button> navButtons=new Dictionary<string,Button>();readonly NotifyIcon tray=new NotifyIcon();
    readonly Timer poll=new Timer{Interval=60000},search=new Timer{Interval=220};
    static readonly Color Ink=Color.FromArgb(28,37,45),Muted=Color.FromArgb(81,101,111),Paper=Color.FromArgb(247,248,245),Gold=Color.FromArgb(239,181,70),Dark=Color.FromArgb(24,36,42);
    public Desk(string file){
        path=file;using(var db=new Db(path)){Network.SeedNews(db);wow=db.Get("wow",App.DefaultWow);
            market=db.Get("market",db.Scalar("SELECT market FROM quote ORDER BY ts DESC LIMIT 1"));if(market=="")market="US / normal / horde";
            fee=(int)LocalSources.Num(db.Get("fee","500"));loss=LocalSources.Num(db.Get("loss","0"));
        }
        Text="Forever Hive Exchange";Font=new Font("Segoe UI",9F);BackColor=Paper;ForeColor=Ink;
        Size=new Size(1250,800);MinimumSize=new Size(910,590);StartPosition=FormStartPosition.CenterScreen;
        var main=new Panel{Dock=DockStyle.Fill,Padding=new Padding(20,12,20,0),BackColor=Paper};
        Controls.Add(main);nav.Dock=DockStyle.Left;nav.Width=168;nav.BackColor=Dark;Controls.Add(nav);
        var brand=new Label{Text="FOREVER HIVE",Location=new Point(16,24),Size=new Size(147,24),ForeColor=Gold,Font=new Font("Segoe UI",12,FontStyle.Bold)};nav.Controls.Add(brand);
        nav.Controls.Add(new Label{Text="EXCHANGE",Location=new Point(18,50),Size=new Size(130,18),ForeColor=Color.WhiteSmoke});
        int y=100;foreach(string s in new[]{"Market","Flips","Investments","Crafting","Portfolio","Inventory","Price alerts","Patch watch","Sources"}){
            var b=new Button{Text=s,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,0,0),Location=new Point(8,y),Size=new Size(152,38),BackColor=Dark,ForeColor=Color.White,FlatStyle=FlatStyle.Flat,TabStop=true};
            b.FlatAppearance.BorderSize=0;b.Click+=(o,e)=>Navigate(s);nav.Controls.Add(b);navButtons[s]=b;y+=43;
        }
        var ver=new Label{Text="v"+App.Version,ForeColor=Color.Silver,AutoSize=true,Anchor=AnchorStyles.Left|AnchorStyles.Bottom};nav.Controls.Add(ver);nav.Resize+=(s,e)=>ver.Location=new Point(20,nav.Height-34);
        grid.Dock=DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.MultiSelect=false;grid.RowHeadersVisible=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.EnableHeadersVisualStyles=false;grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.GridColor=Color.FromArgb(226,231,228);
        grid.ColumnHeadersHeight=36;grid.RowTemplate.Height=30;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(226,233,230);grid.ColumnHeadersDefaultCellStyle.ForeColor=Ink;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(248,224,161);grid.DefaultCellStyle.SelectionForeColor=Ink;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(248,250,248);
        grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)Inspect();};main.Controls.Add(grid);
        status.Dock=DockStyle.Bottom;status.Height=31;status.TextAlign=ContentAlignment.MiddleLeft;status.ForeColor=Muted;main.Controls.Add(status);
        tools.Dock=DockStyle.Top;tools.Height=54;main.Controls.Add(tools);
        tools.Controls.Add(new Label{Text="Filter",AutoSize=true,Location=new Point(0,15)});
        filter.Width=165;filter.Location=new Point(35,12);filter.AccessibleName="Filter rows by name or ID";tools.Controls.Add(filter);
        actions.Location=new Point(211,5);actions.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;actions.Height=44;actions.Width=740;actions.WrapContents=false;tools.Controls.Add(actions);
        summary.Dock=DockStyle.Top;summary.Height=52;summary.BackColor=Color.FromArgb(224,233,228);main.Controls.Add(summary);metrics.Dock=DockStyle.Fill;metrics.TextAlign=ContentAlignment.MiddleLeft;metrics.Padding=new Padding(12,0,0,0);metrics.Font=new Font(Font.FontFamily,10,FontStyle.Bold);summary.Controls.Add(metrics);
        head.Dock=DockStyle.Top;head.Height=86;main.Controls.Add(head);heading.Location=new Point(0,0);heading.Size=new Size(400,44);heading.Font=new Font("Segoe UI",24,FontStyle.Bold);head.Controls.Add(heading);
        caption.Location=new Point(1,50);caption.Size=new Size(980,32);caption.ForeColor=Muted;head.Controls.Add(caption);
        houses.DropDownStyle=ComboBoxStyle.DropDownList;houses.Width=200;houses.Anchor=AnchorStyles.Right|AnchorStyles.Top;head.Controls.Add(houses);head.Resize+=(s,e)=>houses.Location=new Point(head.Width-houses.Width,10);
        foreach(string region in new[]{"US","EU"})foreach(string mode in Network.Modes)foreach(string house in new[]{"horde","alliance","neutral"})houses.Items.Add(region+" / "+mode+" / "+house);
        if(!houses.Items.Contains(market))houses.Items.Add(market);houses.SelectedItem=market;
        houses.SelectedIndexChanged+=(s,e)=>{if(starting)return;market=(string)houses.SelectedItem;using(var db=new Db(path))db.Set("market",market);Reload();Sync(false);};
        filter.TextChanged+=(s,e)=>{search.Stop();search.Start();};search.Tick+=(s,e)=>{search.Stop();Display();};
        poll.Tick+=(s,e)=>Sync(false);tray.Icon=SystemIcons.Information;tray.Text=Text;tray.Visible=true;tray.DoubleClick+=(s,e)=>{Show();WindowState=FormWindowState.Normal;Activate();};
        FormClosed+=(s,e)=>{poll.Stop();search.Stop();tray.Visible=false;tray.Dispose();using(var db=new Db(path))Updates.Apply(db,Path.GetDirectoryName(path));};
        starting=false;Reload();Navigate("Market");
        Shown+=(s,e)=>{poll.Start();Sync(false);};
    }
    void Reload(){using(var db=new Db(path))model=new Analysis(db,market,fee,loss);Display();}
    async void Sync(bool checkUpdates){
        if(busy)return;busy=true;status.Text="Checking saved scans and market sources…";string mk=market,folder=wow;
        try{
            await Task.Run(delegate{
                using(var db=new Db(path)){
                    LocalSources.Sync(db,folder);Network.Poll(db,mk);Updates.Check(db,Path.GetDirectoryName(path),checkUpdates);
                }
            });
            if(IsDisposed)return;Reload();Alerts();
            status.Text="Local files checked "+DateTime.Now.ToString("HH:mm:ss")+"   •   Monitoring every minute while open   •   Source details in Sources";
        }catch(Exception ex){if(!IsDisposed)status.Text="Sync needs attention • "+ex.Message;}
        finally{busy=false;if(!IsDisposed&&mk!=market)Sync(false);}
    }
    void Alerts(){
        using(var db=new Db(path)){
            var hits=new List<string>();foreach(DataRow r in db.Query("SELECT id,cap,alert_ts FROM watch WHERE market="+Db.Q(market)).Rows){
                Listing it;if(!model.Items.TryGetValue((int)LocalSources.Num(r[0]),out it)||!it.Fresh||it.Ask>LocalSources.Num(r[1])||it.Time<=LocalSources.Num(r[2]))continue;
                hits.Add(it.Label+"  "+Economy.Money(it.Ask));db.Exec("UPDATE watch SET alert_ts="+it.Time+" WHERE market="+Db.Q(market)+" AND id="+it.Id);
            }
            if(hits.Count>0)tray.ShowBalloonTip(5000,"Exchange • "+hits.Count+" price alert"+(hits.Count==1?"":"s"),String.Join("\n",hits.Take(4)),ToolTipIcon.Info);
        }
    }
    static Button ButtonFor(string title,Action act){
        var b=new Button{Text=title,AutoSize=true,Height=32,FlatStyle=FlatStyle.Flat,BackColor=Color.White,Padding=new Padding(6,1,6,1),Margin=new Padding(2,4,3,2)};
        b.FlatAppearance.BorderColor=Color.FromArgb(188,200,195);b.Click+=(s,e)=>{try{act();}catch(Exception ex){MessageBox.Show(ex.Message,"Forever Hive Exchange");}};return b;
    }
    void Add(string title,Action f){actions.Controls.Add(ButtonFor(title,f));}
    void Navigate(string s){
        screen=s;filter.Clear();foreach(var entry in navButtons){entry.Value.BackColor=entry.Key==s?Color.FromArgb(57,77,76):Dark;entry.Value.ForeColor=entry.Key==s?Gold:Color.WhiteSmoke;}
        heading.Text=s;actions.Controls.Clear();
        switch(s){
            case "Market":case "Flips":case "Investments":
                Add("Price history",Inspect);Add("Set alert",Watch);Add("Pricing settings",Settings);break;
            case "Portfolio":Add("Record buy",()=>Trade(true));Add("Record sale",()=>Trade(false));Add("Transactions",Transactions);break;
            case "Price alerts":Add("New alert",Watch);Add("Remove selected",RemoveWatch);break;
            case "Sources":Add("Sync now",()=>Sync(false));Add("WoW folder",Folder);Add("Update settings",UpdateSettings);break;
            case "Patch watch":Add("Open article",Inspect);break;
            case "Crafting":Add("Pricing settings",Settings);break;
            case "Inventory":Add("Sync now",()=>Sync(false));break;
        }
        Display();
    }
    public void Go(string name){Navigate(name);}
    public int ShownRows{get{return grid.Rows.Count;}}
    void Display(){
        if(model==null)return;
        string freshness=model.Snapshot==0?"No timestamped snapshot":Economy.Time(model.Snapshot)+(Db.Now-model.Snapshot>21600?" • stale":"");
        metrics.Text=model.Items.Count.ToString("N0")+" priced items     |     "+model.DailyCount.ToString("N0")+" Auctionator item-days     |     "+model.Source+"   "+freshness;
        string term=filter.Text.Trim();var table=new DataTable();string note="";
        using(var db=new Db(path)){
            if(screen=="Market"||screen=="Flips"||screen=="Investments"){
                bool all=screen=="Market";
                note=all?"Search item names or IDs. Double-click an item for its observed history and posting floor.":screen=="Flips"?"Resale scenarios with at least 3 prior days and 15% modeled return. Check listings and demand before buying.":"Rising multi-day prices with a discount to history. Patch associations are hypotheses.";
                table.Columns.Add("ID",typeof(int));table.Columns.Add("Item");table.Columns.Add("Ask");table.Columns.Add(all?"Post at":"Resell at");table.Columns.Add("Prior median");table.Columns.Add("Days",typeof(int));
                if(!all){table.Columns.Add("Net / unit");table.Columns.Add("ROI %",typeof(decimal));table.Columns.Add(screen=="Investments"?"Patch context":"Demand evidence");}
                else{table.Columns.Add(model.Source=="TSM"?"Auctions":"Listed units",typeof(long));table.Columns.Add("24h flow");}
                var rows=model.Items.Values.Where(x=>(all||(screen=="Flips"?x.Flip:x.Invest))&&(x.Id.ToString().Contains(term)||x.Label.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0));
                rows=all?rows.OrderBy(x=>x.Name.Length==0).ThenBy(x=>x.Label):rows.OrderByDescending(x=>x.Profit);
                foreach(var x in rows){
                    var vals=new List<object>{x.Id,x.Label,Economy.Money(x.Ask),all?(x.Fresh?Economy.Money(Economy.Undercut(x.Ask)):"—"):Economy.Money(x.Target),x.Baseline>0?Economy.Money(x.Baseline):"—",x.Days};
                    if(all){vals.Add(x.Quantity<0?(object)DBNull.Value:x.Quantity);vals.Add(x.Volume<0?"Unknown":x.Volume+" inferred");}
                    else{vals.Add(Economy.Money(x.Profit));vals.Add(Math.Round(x.ROI,1));vals.Add(screen=="Investments"?x.Theme:(x.Volume<0?"Unverified":x.Volume+" inferred units / 24h"));}
                    table.Rows.Add(vals.ToArray());
                }
            }else if(screen=="Crafting"){note="Reagent costs use current asks. Output uses a one-copper undercut, sale cut and expected lost deposits. Check materials and recipe access.";table=model.Crafts(db);}
            else if(screen=="Portfolio"){note="Record actual fills in copper. Open positions use weighted-average cost basis and fresh market marks.";table=model.Portfolio(db);}
            else if(screen=="Inventory"){note="Last saved bag and bank counts from Forever Hive Command across characters. Times are local.";table=db.Query("SELECT character AS Character,location AS Location,id AS ID,name AS Item,qty AS Quantity,datetime(ts,'unixepoch','localtime') AS Observed FROM inventory ORDER BY character,location,name");}
            else if(screen=="Price alerts"){note="Windows notifications fire once per qualifying fresh snapshot while Exchange is running.";table=db.Query("SELECT w.id AS ID,coalesce(i.name,'Item #'||w.id) AS Item,w.cap AS [Trigger at or below (c)],case when alert_ts>0 then datetime(alert_ts,'unixepoch','localtime') else 'Waiting' end AS [Last trigger] FROM watch w LEFT JOIN item i ON i.id=w.id WHERE market="+Db.Q(market));}
            else if(screen=="Patch watch"){note="Blizzard announcements and applied changes. Open an article for the latest wording and effective date.";table=db.Query("SELECT kind AS Status,title AS Article,day AS [Effective date],notes AS Notes,url AS URL FROM event ORDER BY day DESC,title");}
            else{note="Read-only imports from your Forever client and a public hourly feed. Each source reports its own connection state.";table=db.Query("SELECT name AS Source,state AS Result,datetime(checked,'unixepoch','localtime') AS [Last check] FROM source ORDER BY name");}
        }
        caption.Text=note;
        if(term!=""&&screen!="Market"&&screen!="Flips"&&screen!="Investments"){foreach(DataRow r in table.Rows.Cast<DataRow>().ToArray())if(!r.ItemArray.Any(v=>v.ToString().IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0))table.Rows.Remove(r);}
        grid.DataSource=table.DefaultView;if(grid.Columns.Contains("URL"))grid.Columns["URL"].Visible=false;
        if(grid.Columns.Contains("Margin (c)"))grid.Columns["Margin (c)"].Visible=false;
        if(grid.Columns.Contains("Item"))grid.Columns["Item"].FillWeight=200;
        if(grid.Columns.Contains("ID"))grid.Columns["ID"].FillWeight=55;
        if(grid.Columns.Contains("Days"))grid.Columns["Days"].FillWeight=45;
        if(grid.Columns.Contains("Notes"))grid.Columns["Notes"].FillWeight=230;
        if(!busy)status.Text=table.Rows.Count==0?"No matching records for this view and market.":"Showing "+table.Rows.Count.ToString("N0")+" rows   •   "+market;
    }
    int Selected(){if(grid.CurrentRow==null||!grid.Columns.Contains("ID"))return 0;return (int)LocalSources.Num(grid.CurrentRow.Cells["ID"].Value);}
    void Inspect(){
        if(grid.CurrentRow==null)return;
        if(screen=="Patch watch"){string url=grid.CurrentRow.Cells["URL"].Value.ToString();if(Network.Official(url))Process.Start(url);return;}
        Listing it;if(!model.Items.TryGetValue(Selected(),out it))return;
        using(var f=new Form{Text=it.Label+" • price history",Size=new Size(850,580),StartPosition=FormStartPosition.CenterParent,Font=Font}){
            var chart=new Chart{Dock=DockStyle.Fill};var area=new ChartArea();chart.ChartAreas.Add(area);area.AxisY.Title="Unit price (copper)";area.AxisX.LabelStyle.Format="MMM d";area.AxisX.MajorGrid.Enabled=false;chart.Legends.Add(new Legend());
            using(var db=new Db(path)){
                string cond=" WHERE market="+Db.Q(market)+" AND id="+it.Id;
                foreach(string seriesName in new[]{"Daily low","Daily high","Snapshot ask"}){
                    var series=chart.Series.Add(seriesName);series.ChartType=SeriesChartType.Line;series.BorderWidth=2;series.MarkerStyle=MarkerStyle.Circle;series.MarkerSize=4;series.XValueType=ChartValueType.DateTime;
                    var history=db.Query(seriesName=="Snapshot ask"?"SELECT ts,ask FROM quote"+cond+" ORDER BY ts":"SELECT day*86400,"+(seriesName=="Daily low"?"low":"high")+" FROM daily"+cond+" ORDER BY day");
                    foreach(DataRow r in history.Rows){var utc=DateTimeOffset.FromUnixTimeSeconds(LocalSources.Num(r[0])).UtcDateTime;series.Points.AddXY(utc,LocalSources.Num(r[1]));}
                }
            }
            f.Controls.Add(chart);var panel=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=75,Padding=new Padding(10)};
            panel.Controls.Add(new Label{Text="Unit cost (c)",AutoSize=true});var cost=Number(0,1000000000,it.Ask);panel.Controls.Add(cost);var result=new Label{AutoSize=true,Margin=new Padding(8,4,0,0)};
            Action calc=delegate{long floor=Economy.Floor((long)cost.Value,fee,loss);long price=Economy.Undercut(it.Ask);result.Text="Break-even "+Economy.Money(floor)+"   •   "+(!it.Fresh?"Await fresh price":price<floor?"Hold: undercut is below cost":"Post at "+Economy.Money(price));};
            cost.ValueChanged+=(s,e)=>calc();calc();panel.Controls.Add(result);f.Controls.Add(panel);
            f.Controls.Add(new Label{Dock=DockStyle.Top,Height=50,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,0,0),Text=it.Label+"   •   ID "+it.Id+"   •   "+market+"   •   "+it.Source+"   •   "+(it.Time>0?Economy.Time(it.Time):"daily history")});f.ShowDialog(this);
        }
    }
    static NumericUpDown Number(decimal min,decimal max,decimal value){return new NumericUpDown{Minimum=min,Maximum=max,ThousandsSeparator=true,Width=140,Value=Math.Max(min,Math.Min(max,value))};}
    static decimal[] Inputs(IWin32Window owner,string title,string[] names,decimal[] min,decimal[] max,decimal[] initial){
        using(var f=new Form{Text=title,ClientSize=new Size(405,75+43*names.Length),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false}){
            var fields=new List<NumericUpDown>();int y=18;
            for(int i=0;i<names.Length;i++){f.Controls.Add(new Label{Text=names[i],Location=new Point(12,y+2),Size=new Size(218,28)});var n=Number(min[i],max[i],initial[i]);n.Location=new Point(237,y);f.Controls.Add(n);fields.Add(n);y+=43;}
            var save=new Button{Text="Save",DialogResult=DialogResult.OK,Location=new Point(295,y+8)};f.Controls.Add(save);f.AcceptButton=save;
            if(f.ShowDialog(owner)!=DialogResult.OK)return null;return fields.Select(n=>n.Value).ToArray();
        }
    }
    void Settings(){var v=Inputs(this,"Pricing assumptions",new[]{"Auction cut (basis points, 500 = 5%)","Expected lost deposit / unit (c)"},new decimal[]{0,0},new decimal[]{9900,1000000000},new decimal[]{fee,loss});if(v==null)return;fee=(int)v[0];loss=(long)v[1];using(var db=new Db(path)){db.Set("fee",fee.ToString());db.Set("loss",loss.ToString());}Reload();}
    void Watch(){int id=Selected();Listing it;model.Items.TryGetValue(id,out it);var v=Inputs(this,"Price alert • "+market,new[]{"Item ID","Notify at or below (c per unit)"},new decimal[]{1,1},new decimal[]{10000000,1000000000},new decimal[]{Math.Max(1,id),it==null?100:it.Ask});if(v==null)return;using(var db=new Db(path))db.Exec("INSERT OR REPLACE INTO watch VALUES("+Db.Q(market)+","+(int)v[0]+","+(long)v[1]+",0)");Navigate("Price alerts");Alerts();}
    void RemoveWatch(){int id=Selected();if(id==0)return;using(var db=new Db(path))db.Exec("DELETE FROM watch WHERE market="+Db.Q(market)+" AND id="+id);Display();}
    void Trade(bool buy){var v=Inputs(this,buy?"Record purchase":"Record completed sale",new[]{"Item ID","Units","Unit execution price (c)","Total fees and lost deposits (c)"},new decimal[]{1,1,1,0},new decimal[]{10000000,1000000,1000000000,1000000000000},new decimal[]{1,1,100,0});if(v==null)return;using(var db=new Db(path))Analysis.Record(db,market,(int)v[0],buy,(int)v[1],(long)v[2],(long)v[3]);Display();}
    void Transactions(){using(var db=new Db(path))using(var f=new Form{Text="Transactions • "+market,Size=new Size(800,450),StartPosition=FormStartPosition.CenterParent}){f.Controls.Add(new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,DataSource=db.Query("SELECT datetime(ts,'unixepoch','localtime') AS Time,id AS Item,side AS Side,qty AS Quantity,unit AS [Unit copper],fee AS [Total fees] FROM trade WHERE market="+Db.Q(market)+" ORDER BY seq DESC")});f.ShowDialog(this);}}
    void Folder(){using(var f=new FolderBrowserDialog{Description="Choose your World of Warcraft _forever_ folder",SelectedPath=wow}){if(f.ShowDialog(this)!=DialogResult.OK)return;if(!Path.GetFileName(f.SelectedPath).Equals("_forever_",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Select the _forever_ game folder.");wow=f.SelectedPath;using(var db=new Db(path))db.Set("wow",wow);Sync(false);}}
    void UpdateSettings(){var v=MessageBox.Show(this,"Automatically download checksum-verified releases and apply them when Exchange closes?\n\nYes enables updates. No selects notification only.","App updates",MessageBoxButtons.YesNoCancel);if(v==DialogResult.Cancel)return;using(var db=new Db(path))db.Set("updates",v==DialogResult.Yes?"1":"0");Sync(true);}
}
}
