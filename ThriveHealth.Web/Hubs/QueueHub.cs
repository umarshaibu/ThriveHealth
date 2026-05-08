using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ThriveHealth.Web.Hubs;

[Authorize]
public class QueueHub : Hub
{
    public Task SubscribeQueue(int facilityId, int clinicId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"queue-{facilityId}-{clinicId}");

    public Task UnsubscribeQueue(int facilityId, int clinicId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"queue-{facilityId}-{clinicId}");
}
