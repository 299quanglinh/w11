namespace CampusSafetyHub;

// ==========================================
// 1. GIAO DIEN CHUAN & MODEL
// ==========================================

// Giao dien chuan ma module giam sat su dung
public interface ISensor
{
    double ReadValue();
    string GetUnit();
}

// Doi tuong du lieu bat bien mo ta canh bao
public sealed record AlertEvent(
    string SensorName,
    double Value,
    string Unit,
    string Severity,
    DateTime OccurredAt);

// ==========================================
// 2. ADAPTER PATTERN
// ==========================================

// Lop co san tu he thong cu, khong duoc sua doi
public sealed class LegacySmokeDetector
{
    private int smokeLevel;

    public LegacySmokeDetector(int smokeLevel)
    {
        this.smokeLevel = smokeLevel;
    }

    public int GetSmokeLevel() => smokeLevel; // Thang do 0..1000

    public string GetDescription() => "Legacy smoke detector";

    public void SetSmokeLevel(int value)
    {
        smokeLevel = Math.Clamp(value, 0, 1000);
    }
}

// Adapter chuyen doi giao dien cu sang ISensor va chuan hoa gia tri ve 0..100%
public sealed class SmokeSensorAdapter : ISensor
{
    private readonly LegacySmokeDetector legacy;

    public SmokeSensorAdapter(LegacySmokeDetector legacy)
    {
        this.legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
    }

    public double ReadValue()
    {
        return legacy.GetSmokeLevel() / 10.0;
    }

    public string GetUnit() => "% smoke";
}

public class Program
{
    public static void Main()
    {
        // Test nhanh adapter theo yeu cau de bai
        var legacy = new LegacySmokeDetector(735);
        ISensor sensor = new SmokeSensorAdapter(legacy);

        Console.WriteLine($"Gia tri doc duoc: {sensor.ReadValue()}"); // 73.5
        Console.WriteLine($"Don vi: {sensor.GetUnit()}");             // % smoke
    }
}
