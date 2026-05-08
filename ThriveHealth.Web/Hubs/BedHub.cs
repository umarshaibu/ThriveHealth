using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ThriveHealth.Web.Hubs;

[Authorize]
public class BedHub : Hub
{
    public Task SubscribeBeds(int facilityId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"beds-{facilityId}");

    public Task UnsubscribeBeds(int facilityId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"beds-{facilityId}");
}
