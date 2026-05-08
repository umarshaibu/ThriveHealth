using Microsoft.AspNetCore.SignalR;

namespace ThriveHealth.Web.Hubs;

/// <summary>
/// Real-time message + presence broadcast for tele-chat threads.
/// Clients subscribe to "patient-{N}" and receive any new messages on that patient's thread.
///
/// AuthN is intentionally NOT [Authorize]'d here so both staff (cookie auth)  and portal patients
/// (their own portal scheme) can connect through the single hub. Authorization on which patient a
/// caller may subscribe to happens server-side: the controller checks ownership before broadcasting,
/// so the hub itself only needs to relay payloads handed to it from a verified controller path.
/// </summary>
public class TeleChatHub : Hub
{
    public Task SubscribePatient(int patientId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupForPatient(patientId));

    public Task UnsubscribePatient(int patientId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupForPatient(patientId));

    public Task SubscribeFacilityInbox(int facilityId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupForInbox(facilityId));

    public Task UnsubscribeFacilityInbox(int facilityId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupForInbox(facilityId));

    public static string GroupForPatient(int patientId) => $"telechat-patient-{patientId}";
    public static string GroupForInbox(int facilityId) => $"telechat-inbox-{facilityId}";
}
