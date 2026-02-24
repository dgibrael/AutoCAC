namespace AutoCAC.Common;

public sealed record CreatinineClearance
{
    public bool Female { get; set; }
    public double SerumCreatinine { get; set; }
    public double HeightCm { get; set; }
    public double WeightKg { get; set; }
    public int Age { get; set; }
    public bool MissingData => HeightCm == 0 || WeightKg == 0 || Age == 0;
    public double HeightIn => HeightCm / 2.54;
    public double InOver60 => HeightIn > 60 ? (HeightIn - 60) : 0;
    public double Ibw => MissingData ? 0 : (Female ? 45.5 : 50) + (InOver60 * 2.3);
    public double AdjBw => MissingData ? 0 : (0.4 * (WeightKg - Ibw)) + Ibw;
    public double WeightUsed
    {
        get
        {
            if (HeightCm <= 0 || WeightKg < Ibw)
            {
                return WeightKg;
            }
            if (WeightKg >= (Ibw * 1.3))
            {
                return AdjBw;
            }
            return Ibw;
        }
    }
    public double Result
    {
        get
        {
            if (MissingData) return 0;
            return ((140 - Age) * WeightUsed / (72 * SerumCreatinine)) * (Female ? 0.85 : 1);
        }
    }

}
