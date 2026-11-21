using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace ForeverHive {
internal static class Economy {
    public static long Net(long gross,int feeBps,long loss){
        if(gross<0||feeBps<0||feeBps>=10000||loss<0)throw new ArgumentOutOfRangeException();
        return checked((long)Math.Floor(gross*(10000m-feeBps)/10000m)-loss);
    }
    public static long Floor(long cost,int bps,long loss){return checked((long)Math.Ceiling(((decimal)cost+loss)*10000m/(10000-bps)));}
    public static long Undercut(long ask){return Math.Max(1,ask-1);}
    public static long Median(IEnumerable<long> values){var v=values.OrderBy(x=>x).ToArray();int n=v.Length;return n==0?0:(long)(((decimal)v[(n-1)/2]+v[n/2])/2);}
    public static string Money(long v){decimal a=Math.Abs((decimal)v);return (v<0?"−":"")+(a>=10000?Math.Floor(a/10000).ToString("N0")+"g ":"")+(a>=100?Math.Floor(a%10000/100)+"s ":"")+(a%100)+"c";}
    public static string Time(long ts){return DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime.ToString("MMM d, HH:mm");}
}
}
