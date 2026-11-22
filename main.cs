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

// Lop co san tu he thong cu, gia dinh khong duoc sua doi
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
// Su dung thanh phan chua (composition) thay vi ke thua de tranh phu thuoc chat
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
        // Duoi 50%: Binh thuong, khong phat canh bao
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

// ==========================================
// 4. MEDIATOR PATTERN & COLLEAGUES
// ==========================================

// Hop dong dieu phoi
public interface IEmergencyMediator
{
    void Notify(string eventType);
}

// Thiet bi 1: Coi bao dong
public sealed class Alarm
{
    public void Activate() => Console.WriteLine("[ALARM] Coi bao dong da bat! Phat tin hieu so tan toan khu vuc.");
    public void Deactivate() => Console.WriteLine("[ALARM] Da tat coi bao dong.");
}

// Thiet bi 2: He thong kiem soat cua
public sealed class DoorController
{
    public void UnlockAll() => Console.WriteLine("[DOOR] Da mo toan bo cua thoat hiem phong may.");
    public void LockAll() => Console.WriteLine("[DOOR] Da khoa toan bo cua phong may.");
}

// Thiet bi 3: He thong quat thong gio
public sealed class VentilationController
{
    public void EnableEmergencyMode() => Console.WriteLine("[VENTILATION] Da ngat he thong dieu hoa, bat quat hut khoi khan cap.");
    public void SetNormalMode() => Console.WriteLine("[VENTILATION] He thong thong gio tro ve che do binh thuong.");
}

// Mediator dieu phoi cac thiet bi ung pho khan cap
public sealed class EmergencyMediator : IEmergencyMediator
{
    private readonly Alarm alarm;
    private readonly DoorController doorController;
    private readonly VentilationController ventilationController;

    public EmergencyMediator(Alarm alarm, DoorController doorController, VentilationController ventilationController)
    {
        this.alarm = alarm ?? throw new ArgumentNullException(nameof(alarm));
        this.doorController = doorController ?? throw new ArgumentNullException(nameof(doorController));
        this.ventilationController = ventilationController ?? throw new ArgumentNullException(nameof(ventilationController));
    }

    public void Notify(string eventType)
    {
        if (eventType == "FIRE_DETECTED")
        {
            Console.WriteLine("[MEDIATOR] Nhan su kien FIRE_DETECTED -> Phối hợp các thiết bị ứng phó:");
            alarm.Activate();
            doorController.UnlockAll();
            ventilationController.EnableEmergencyMode();
        }
    }
}

// Bridge Observer: Chuyen canh bao CRITICAL thanh su kien cho Mediator
public sealed class EmergencyBridgeObserver : IAlertObserver
{
    private readonly IEmergencyMediator mediator;

    public EmergencyBridgeObserver(IEmergencyMediator mediator)
    {
        this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public void Update(AlertEvent alertEvent)
    {
        if (alertEvent.Severity == "CRITICAL")
        {
            Console.WriteLine($"[BRIDGE] Phat hien nguy cap CRITICAL tai {alertEvent.SensorName}, chuyen su kien cho Mediator...");
            mediator.Notify("FIRE_DETECTED");
        }
    }
}

public class Program
{
    public static void Main()
    {
        var alarm = new Alarm();
        var door = new DoorController();
        var vent = new VentilationController();
        var mediator = new EmergencyMediator(alarm, door, vent);

        var bridge = new EmergencyBridgeObserver(mediator);
        var alert = new AlertEvent("ServerRoom_Smoke", 75.0, "% smoke", "CRITICAL", DateTime.Now);

        Console.WriteLine("Test EmergencyBridgeObserver kich hoat mediator:");
        bridge.Update(alert);
    }
}
