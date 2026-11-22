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

// ==========================================
// 3. OBSERVER PATTERN
// ==========================================

// Hop dong nhan canh bao
public interface IAlertObserver
{
    void Update(AlertEvent alertEvent);
}

// Subject: Quan ly observers, doc sensor va phat canh bao khi vuot nguong
public sealed class SensorMonitor
{
    private readonly List<IAlertObserver> observers = new();

    public void Attach(IAlertObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        // Yeu cau 1: Khong cho phep mot observer dang ky 2 lan
        if (!observers.Contains(observer))
        {
            observers.Add(observer);
        }
    }

    public void Detach(IAlertObserver observer)
    {
        // Yeu cau 2: Co the huy dang ky observer
        observers.Remove(observer);
    }

    public void CheckSensor(string sensorName, ISensor sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        double value = sensor.ReadValue();

        // Quy uoc muc do theo de bai:
        // Dưới 50%: Binh thuong, khong phat canh bao
        // Tu 50% den duoi 70%: WARNING
        // Tu 70% tro len: CRITICAL
        string? severity = value switch
        {
            >= 70 => "CRITICAL",
            >= 50 => "WARNING",
            _ => null
        };

        if (severity is null)
        {
            return;
        }

        var alertEvent = new AlertEvent(
            sensorName,
            value,
            sensor.GetUnit(),
            severity,
            DateTime.Now);

        Notify(alertEvent);
    }

    private void Notify(AlertEvent alertEvent)
    {
        // Yeu cau 3: Duyet tren ToArray() de tranh loi khi danh sach bi sua doi trong luc lap
        foreach (IAlertObserver observer in observers.ToArray())
        {
            try
            {
                // Yeu cau 4: AlertEvent la record bat bien, observer khong the sua doi du lieu
                observer.Update(alertEvent);
            }
            catch (Exception ex)
            {
                // Yeu cau 5: Observer loi khong duoc lam hong cac observer khac, bat loi va ghi log
                Console.WriteLine($"[MONITOR LOG] Loi tai observer {observer.GetType().Name}: {ex.Message}");
            }
        }
    }
}

// Observer hien thi bang tin
public sealed class DashboardObserver : IAlertObserver
{
    public void Update(AlertEvent alertEvent)
    {
        Console.WriteLine(
            $"[DASHBOARD] {alertEvent.SensorName}: " +
            $"{alertEvent.Value:F1}{alertEvent.Unit} " +
            $"[{alertEvent.Severity}]");
    }
}

// Observer gui email thong bao
public sealed class EmailAlertObserver : IAlertObserver
{
    private readonly string recipientEmail;

    public EmailAlertObserver(string recipientEmail = "safety@campus.edu.vn")
    {
        this.recipientEmail = recipientEmail;
    }

    public void Update(AlertEvent alertEvent)
    {
        Console.WriteLine($"[EMAIL] To: <{recipientEmail}> | Canh bao {alertEvent.Severity} tai {alertEvent.SensorName} ({alertEvent.Value:F1}{alertEvent.Unit})");
    }
}

// Observer ghi nhat ky he thong
public sealed class AuditLogObserver : IAlertObserver
{
    public void Update(AlertEvent alertEvent)
    {
        Console.WriteLine($"[AUDIT LOG] [{alertEvent.OccurredAt:HH:mm:ss}] [{alertEvent.Severity}] {alertEvent.SensorName} -> {alertEvent.Value:F1}{alertEvent.Unit}");
    }
}

public class Program
{
    public static void Main()
    {
        var legacy = new LegacySmokeDetector(650);
        ISensor sensor = new SmokeSensorAdapter(legacy);

        var monitor = new SensorMonitor();
        monitor.Attach(new DashboardObserver());
        monitor.Attach(new EmailAlertObserver());
        monitor.Attach(new AuditLogObserver());

        Console.WriteLine("Test SensorMonitor voi muc khoi 650 (65% -> WARNING):");
        monitor.CheckSensor("SmokeSensor_Room1", sensor);
    }
}
