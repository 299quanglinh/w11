namespace CampusSafetyHub;

/*
================================================================================
BÀI THỰC HÀNH MẪU THIẾT KẾ: CAMPUS SAFETY HUB
- Adapter Pattern: Tích hợp cảm biến khói cũ (LegacySmokeDetector -> SmokeSensorAdapter)
- Observer Pattern: Giám sát và phát cảnh báo đa kênh (SensorMonitor -> IAlertObserver)
- Mediator Pattern: Điều phối các thiết bị ứng phó sự cố (EmergencyMediator)

TRẢ LỜI CÁC CÂU HỎI THIẾT KẾ:
1. Adapter nên kế thừa hay chứa LegacySmokeDetector?
   -> Chứa (Composition): Ưu tiên composition over inheritance, giữ lớp cũ nguyên vẹn và linh hoạt.
2. SensorMonitor có nên biết EmailAlertObserver cụ thể không?
   -> Không: Chỉ phụ thuộc vào trừu tượng IAlertObserver (DIP & OCP), cho phép thêm/bớt observer động.
3. Alarm có cần tham chiếu đến DoorController không?
   -> Không: Các thiết bị không phụ thuộc trực tiếp nhau, giao tiếp tập trung qua EmergencyMediator.
4. Ai chịu trách nhiệm quyết định giá trị nào là vượt ngưỡng?
   -> SensorMonitor (trong CheckSensor dựa trên thang đo đã chuẩn hóa).
================================================================================
*/

// ==========================================
// 1. GIAO DIỆN CẢM BIẾN & DỮ LIỆU CẢNH BÁO
// ==========================================

// Giao diện chuẩn mà module giám sát sử dụng
public interface ISensor
{
    double ReadValue();
    string GetUnit();
}

// Đối tượng dữ liệu bất biến mô tả cảnh báo
public sealed record AlertEvent(
    string SensorName,
    double Value,
    string Unit,
    string Severity,
    DateTime OccurredAt);

// ==========================================
// 2. ADAPTER PATTERN
// ==========================================

// Lớp có sẵn, giả định từ thư viện bên ngoài không được sửa đổi
public sealed class LegacySmokeDetector
{
    private int smokeLevel;

    public LegacySmokeDetector(int smokeLevel)
    {
        this.smokeLevel = smokeLevel;
    }

    public int GetSmokeLevel() => smokeLevel; // Thang đo 0..1000

    public string GetDescription() => "Legacy smoke detector";

    public void SetSmokeLevel(int value)
    {
        smokeLevel = Math.Clamp(value, 0, 1000);
    }
}

// Adapter chuyển đổi giao diện cũ sang ISensor và chuẩn hóa thang đo 0..1000 thành 0..100%
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

// Hợp đồng nhận cảnh báo
public interface IAlertObserver
{
    void Update(AlertEvent alertEvent);
}

// Subject: Quản lý observers, đọc sensor, so ngưỡng và phát cảnh báo
public sealed class SensorMonitor
{
    private readonly List<IAlertObserver> observers = new();

    public void Attach(IAlertObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        // Yêu cầu 1: Không cho cùng một observer đăng ký hai lần
        if (!observers.Contains(observer))
        {
            observers.Add(observer);
        }
    }

    public void Detach(IAlertObserver observer)
    {
        // Yêu cầu 2: Có thể hủy đăng ký observer
        observers.Remove(observer);
    }

    public void CheckSensor(string sensorName, ISensor sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        double value = sensor.ReadValue();

        // Quy ước mức độ:
        // Dưới 50%: Bình thường, không phát cảnh báo
        // Từ 50% đến dưới 70%: WARNING
        // Từ 70% trở lên: CRITICAL
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
        // Yêu cầu 3: Duyệt trên ToArray() để observer có thể bị gỡ khi đang thông báo mà không hỏng vòng lặp
        foreach (IAlertObserver observer in observers.ToArray())
        {
            try
            {
                // Yêu cầu 4: AlertEvent là record bất biến, observer không thể sửa đổi
                observer.Update(alertEvent);
            }
            catch (Exception ex)
            {
                // Yêu cầu 5: Một observer lỗi không được làm sai dữ liệu của observer khác
                Console.WriteLine($"[MONITOR LOG] Lỗi tại {observer.GetType().Name}: {ex.Message}");
            }
        }
    }
}

// Các observer nhận cảnh báo
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

public sealed class EmailAlertObserver : IAlertObserver
{
    private readonly string recipientEmail;

    public EmailAlertObserver(string recipientEmail = "safety-team@campus.edu.vn")
    {
        this.recipientEmail = recipientEmail;
    }

    public void Update(AlertEvent alertEvent)
    {
        Console.WriteLine($"[EMAIL] Gửi tới <{recipientEmail}>: Cảnh báo {alertEvent.Severity} tại {alertEvent.SensorName} ({alertEvent.Value:F1}{alertEvent.Unit})");
    }
}

public sealed class AuditLogObserver : IAlertObserver
{
    public void Update(AlertEvent alertEvent)
    {
        Console.WriteLine($"[AUDIT LOG] [{alertEvent.OccurredAt:HH:mm:ss}] [{alertEvent.Severity}] {alertEvent.SensorName} -> {alertEvent.Value:F1}{alertEvent.Unit}");
    }
}

// ==========================================
// 4. MEDIATOR PATTERN & THIẾT BỊ ỨNG PHÓ
// ==========================================

// Hợp đồng điều phối
public interface IEmergencyMediator
{
    void Notify(string eventType);
}

// Thiết bị: Còi báo động
public sealed class Alarm
{
    public void Activate() => Console.WriteLine("[ALARM] Còi báo động đã bật! Phát tín hiệu sơ tán khẩn cấp.");
    public void Deactivate() => Console.WriteLine("[ALARM] Đã tắt còi báo động.");
}

// Thiết bị: Kiểm soát cửa
public sealed class DoorController
{
    public void UnlockAll() => Console.WriteLine("[DOOR] Đã mở khóa toàn bộ cửa thoát hiểm phòng máy.");
    public void LockAll() => Console.WriteLine("[DOOR] Đã khóa các cửa phòng máy.");
}

// Thiết bị: Quạt thông gió
public sealed class VentilationController
{
    public void EnableEmergencyMode() => Console.WriteLine("[VENTILATION] Đã ngắt điều hòa, kích hoạt quạt hút khói công suất cao.");
    public void SetNormalMode() => Console.WriteLine("[VENTILATION] Hệ thống thông gió trở về chế độ tiêu chuẩn.");
}

// Mediator: Phối hợp alarm, door và ventilation khi có sự kiện khẩn cấp
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
            Console.WriteLine("[MEDIATOR] Nhận sự kiện FIRE_DETECTED -> Bắt đầu điều phối các thiết bị ứng phó:");
            alarm.Activate();
            doorController.UnlockAll();
            ventilationController.EnableEmergencyMode();
        }
    }
}

// Cầu nối: Chuyển cảnh báo CRITICAL thành sự kiện FIRE_DETECTED cho Mediator
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
            Console.WriteLine($"[BRIDGE] Phát hiện mức độ CRITICAL tại {alertEvent.SensorName}, chuyển sự kiện cho Mediator...");
            mediator.Notify("FIRE_DETECTED");
        }
    }
}

// Observer giả lập dùng để kiểm thử tính năng bắt lỗi
public sealed class FaultyObserver : IAlertObserver
{
    public void Update(AlertEvent alertEvent)
    {
        throw new InvalidOperationException("Mất kết nối mạng đột ngột!");
    }
}

// ==========================================
// 5. CHƯƠNG TRÌNH CHÍNH & KỊCH BẢN MÔ PHỎNG
// ==========================================

public class Program
{
    public static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("===============================================================");
        Console.WriteLine("      MÔ PHỎNG TRUNG TÂM GIÁM SÁT AN TOÀN - CAMPUS SAFETY HUB   ");
        Console.WriteLine("===============================================================\n");

        // 1. Khởi tạo các thiết bị ứng phó và Mediator
        var alarm = new Alarm();
        var doorController = new DoorController();
        var ventilationController = new VentilationController();
        var mediator = new EmergencyMediator(alarm, doorController, ventilationController);

        // 2. Tích hợp cảm biến khói cũ qua Adapter
        var legacyDetector = new LegacySmokeDetector(350);
        ISensor smokeSensor = new SmokeSensorAdapter(legacyDetector);

        // 3. Khởi tạo trung tâm giám sát và gắn các observer
        var monitor = new SensorMonitor();
        var dashboard = new DashboardObserver();
        var emailAlert = new EmailAlertObserver("admin-datacenter@campus.edu.vn");
        var auditLog = new AuditLogObserver();
        var emergencyBridge = new EmergencyBridgeObserver(mediator);

        monitor.Attach(dashboard);
        monitor.Attach(emailAlert);
        monitor.Attach(auditLog);
        monitor.Attach(emergencyBridge);

        // -------------------------------------------------------------
        // KỊCH BẢN 1: Mức khói 350 (35.0%) -> Bình thường (< 50%)
        // -------------------------------------------------------------
        Console.WriteLine(">>> KỊCH BẢN 1: Mức khói ở mức bình thường (350 / 1000 = 35.0%)");
        legacyDetector.SetSmokeLevel(350);
        monitor.CheckSensor("SmokeDetector_Rack01", smokeSensor);
        Console.WriteLine("=> Kết quả: Dưới 50%, hệ thống an toàn, không phát cảnh báo.\n");

        // -------------------------------------------------------------
        // KỊCH BẢN 2: Mức khói 620 (62.0%) -> Cảnh báo WARNING (50% - 70%)
        // -------------------------------------------------------------
        Console.WriteLine(">>> KỊCH BẢN 2: Mức khói tăng lên ngưỡng WARNING (620 / 1000 = 62.0%)");
        legacyDetector.SetSmokeLevel(620);
        monitor.CheckSensor("SmokeDetector_Rack01", smokeSensor);
        Console.WriteLine("=> Kết quả: Các observer thông báo, Mediator chưa được kích hoạt.\n");

        // -------------------------------------------------------------
        // KỊCH BẢN 3: Mức khói 850 (85.0%) -> Cảnh báo CRITICAL (>= 70%)
        // -------------------------------------------------------------
        Console.WriteLine(">>> KỊCH BẢN 3: Mức khói chạm ngưỡng CRITICAL (850 / 1000 = 85.0%)");
        legacyDetector.SetSmokeLevel(850);
        monitor.CheckSensor("SmokeDetector_Rack01", smokeSensor);
        Console.WriteLine("=> Kết quả: Bridge nhận diện CRITICAL -> Kích hoạt Mediator điều phối các thiết bị.\n");

        // -------------------------------------------------------------
        // KỊCH BẢN 4: Hủy đăng ký EmailAlertObserver và kiểm tra lại
        // -------------------------------------------------------------
        Console.WriteLine(">>> KỊCH BẢN 4: Hủy đăng ký EmailAlertObserver (Detach) và test lại");
        monitor.Detach(emailAlert);
        legacyDetector.SetSmokeLevel(720);
        monitor.CheckSensor("SmokeDetector_Rack01", smokeSensor);
        Console.WriteLine("=> Kết quả: EmailAlertObserver đã bị gỡ bỏ, không nhận cảnh báo nữa.\n");

        // -------------------------------------------------------------
        // KỊCH BẢN 5: Kiểm tra cơ chế cách ly lỗi observer
        // -------------------------------------------------------------
        Console.WriteLine(">>> KỊCH BẢN 5: Observer gặp sự cố ngoại lệ (Fault Isolation)");
        var faultyObserver = new FaultyObserver();
        monitor.Attach(faultyObserver);
        legacyDetector.SetSmokeLevel(580);
        monitor.CheckSensor("SmokeDetector_Rack01", smokeSensor);
        Console.WriteLine("=> Kết quả: FaultyObserver báo lỗi nhưng các observer khác vẫn nhận dữ liệu bình thường.\n");

        Console.WriteLine("===============================================================");
        Console.WriteLine("                  HOÀN THÀNH MÔ PHỎNG AN TOÀN                  ");
        Console.WriteLine("===============================================================");
    }
}
