namespace CampusSafetyHub;

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

public class Program
{
    public static void Main()
    {
        Console.WriteLine("Campus Safety Hub - Initialized");
    }
}
