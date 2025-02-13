using DryIoc.ImTools;
using IConnectivity = Shiny.Net.IConnectivity;
using IBattery = Shiny.Power.IBattery;

namespace Sample.Dev;


public class SupportServiceViewModel : ViewModel
{ 
    readonly IConnectivity conn;
    readonly IBattery battery;
    
    public SupportServiceViewModel(
        BaseServices services,
        IConnectivity conn,
        IBattery battery
    ) : base(services)
    {
        this.conn = conn;
        this.battery = battery;
    }

    
    [Reactive] public string NetworkAccess { get; private set; }
    [Reactive] public string ConnectionTypes { get; private set; }
    [Reactive] public string BatteryLevel { get; private set; }
    [Reactive] public string BatteryStatus { get; private set; }

    
    public override void OnAppearing()
    {
        base.OnAppearing();

        this.conn
            .WhenChanged()
            .Subscribe(x =>
            {
                this.NetworkAccess = x.Access.ToString();
                this.ConnectionTypes = x.ConnectionTypes.ToString();
            })
            .DisposedBy(this.DeactivateWith);

        this.battery
            .WhenChanged()
            .Subscribe(x =>
            {
                this.BatteryLevel = (x.Level * 100).ToString();
                this.BatteryStatus = x.Status.ToString();
            })
            .DisposedBy(this.DeactivateWith);
    }
}