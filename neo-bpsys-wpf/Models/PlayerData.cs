namespace neo_bpsys_wpf.Models;

public class PlayerData
{
    //Sur
    public int MachineDecoded { get; set; } = 0;    //破译进度

    public int PalletStunTimes { get; set; } = 0;   //砸板命中次数

    public int RescueTimes { get; set; } = 0;       //救人次数

    public int HealedTimes { get; set; } = 0;       //治疗次数

    public int KiteTime { get; set; } = 0;          //牵制时间

    //Hun
    public int MachineLeft { get; set; } = 0;       //剩余密码机数量

    public int PalletBroken { get; set; } = 0;      //破坏板子数

    public int HitTimes { get; set; } = 0;          //命中求生者次数

    public int TerrorShockTimes { get; set; } = 0;  //恐惧震慑次数

    public int DownTimes { get; set; } = 0;         //击倒次数
}