﻿using FlightEvents.Data;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FlightEvents.Web.Hubs
{
    public class FlightEventHub : Hub<IFlightEventHub>
    {
        public static ConcurrentDictionary<string, string> ConnectionIdToClientIds => connectionIdToClientIds;
        public static ConcurrentDictionary<string, AircraftStatus> ConnectionIdToAircraftStatuses => connectionIdToAircraftStatuses;
        public static ConcurrentDictionary<string, string> ClientIdToAircraftGroup => clientIdToAircraftGroup;

        private static readonly ConcurrentDictionary<string, string> connectionIdToClientIds = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, string> clientIdToConnectionIds = new ConcurrentDictionary<string, string>();

        private static readonly ConcurrentDictionary<string, AircraftStatus> connectionIdToAircraftStatuses = new ConcurrentDictionary<string, AircraftStatus>();
        private static readonly ConcurrentDictionary<string, ATCInfo> connectionIdToToAtcInfos = new ConcurrentDictionary<string, ATCInfo>();
        private static readonly ConcurrentDictionary<string, ATCStatus> connectionIdToAtcStatuses = new ConcurrentDictionary<string, ATCStatus>();

        private static readonly ConcurrentDictionary<string, ChannelWriter<AircraftStatusBrief>> clientIdToChannelWriter = new ConcurrentDictionary<string, ChannelWriter<AircraftStatusBrief>>();

        private static readonly ConcurrentDictionary<string, (string, AircraftPosition)> connectionIdToTeleportRequest = new ConcurrentDictionary<string, (string, AircraftPosition)>();
        private static readonly ConcurrentDictionary<string, string> teleportTokenToConnectionId = new ConcurrentDictionary<string, string>();

        private static readonly ConcurrentDictionary<string, string> clientIdToAircraftGroup = new ConcurrentDictionary<string, string>();

        private readonly IDiscordConnectionStorage discordConnectionStorage;

        public FlightEventHub(IDiscordConnectionStorage discordConnectionStorage)
        {
            this.discordConnectionStorage = discordConnectionStorage;
        }

        public override async Task OnConnectedAsync()
        {
            // Web or Client
            var clientType = (string)Context.GetHttpContext().Request.Query["clientType"];
            var clientId = (string)Context.GetHttpContext().Request.Query["clientId"];
            var clientVersion = (string)Context.GetHttpContext().Request.Query["clientVersion"];
            if (clientId != null)
            {
                connectionIdToClientIds[Context.ConnectionId] = clientId;
                // When a user reconnect, old connection ID is still in the list and need to be removed
                if (clientIdToConnectionIds.TryGetValue(clientId, out var oldConnectionId))
                {
                    connectionIdToClientIds.TryRemove(oldConnectionId, out _);
                }
                clientIdToConnectionIds[clientId] = Context.ConnectionId;
            }
            switch (clientType)
            {
                case "Web":
                    await Groups.AddToGroupAsync(Context.ConnectionId, "Map");
                    break;
                case "Bot":
                    await Groups.AddToGroupAsync(Context.ConnectionId, "Bot");
                    break;
                default:
                    break;
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            // Note: cache based on clientId is removed here, 
            // but there has to be a recovery mechanism in OnConnectedAsync to handle reconnect

            if (connectionIdToClientIds.TryRemove(Context.ConnectionId, out var clientId))
            {
                clientIdToConnectionIds.TryRemove(clientId, out _);
                clientIdToAircraftGroup.TryRemove(clientId, out _);
                await Clients.Groups("Map", "ATC").UpdateATC(clientId, null, null);
            }
            RemoveCacheOnConnectionId(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public void LoginATC(ATCInfo atc)
        {
            connectionIdToToAtcInfos[Context.ConnectionId] = atc;
        }

        public async Task UpdateATC(ATCStatus status)
        {
            if (connectionIdToClientIds.TryGetValue(Context.ConnectionId, out var clientId) && connectionIdToToAtcInfos.TryGetValue(Context.ConnectionId, out var atc))
            {
                int? fromFrequency = null;
                if (connectionIdToAtcStatuses.TryGetValue(Context.ConnectionId, out var lastStatus))
                {
                    fromFrequency = lastStatus.FrequencyCom;
                }
                if (status != null)
                {
                    connectionIdToAtcStatuses[Context.ConnectionId] = status;
                }
                if (fromFrequency != status?.FrequencyCom)
                {
                    await Clients.Groups("Bot").ChangeFrequency(clientId, fromFrequency, status?.FrequencyCom);
                }

                await Clients.Groups("Map").UpdateATC(clientId, status, atc);
            }
        }

        public void LoginAircraft(AircraftLoginInfo aircraft)
        {
            if (connectionIdToClientIds.TryGetValue(Context.ConnectionId, out var clientId))
            {
                if (!string.IsNullOrEmpty(aircraft.AircraftGroup))
                {
                    clientIdToAircraftGroup[clientId] = aircraft.AircraftGroup;
                }
                else
                {
                    clientIdToAircraftGroup.TryRemove(clientId, out _);
                }
            }
        }

        public async Task UpdateAircraft(AircraftStatus status)
        {
            if (connectionIdToClientIds.TryGetValue(Context.ConnectionId, out var clientId))
            {
                // Sanitize status
                if (Math.Abs(status.Latitude) < 0.02 && Math.Abs(status.Longitude) < 0.02)
                {
                    // Aircraft is not loaded
                    status.FrequencyCom1 = 0;
                }

                // Cache latest status
                int fromFrequency = 0;
                if (connectionIdToAircraftStatuses.TryGetValue(Context.ConnectionId, out var lastStatus))
                {
                    fromFrequency = lastStatus.FrequencyCom1;
                }
                connectionIdToAircraftStatuses[Context.ConnectionId] = status;

                if (!connectionIdToAtcStatuses.TryGetValue(Context.ConnectionId, out _))
                {
                    // Switch Discord channel based on COM1 change if ATC Mode is not active
                    var toFrequency = status.FrequencyCom1;
                    if (fromFrequency != toFrequency)
                    {
                        await Clients.Groups("Bot").ChangeFrequency(clientId, fromFrequency == 0 ? null : (int?)fromFrequency, toFrequency == 0 ? null : (int?)toFrequency);
                    }
                }
                await Clients.Groups("ATC").UpdateAircraft(clientId, status);
            }
        }

        public async Task RequestStatusFromDiscord(ulong discordUserId)
        {
            var clientIds = await discordConnectionStorage.GetClientIdsAsync(discordUserId);
            foreach (var clientId in clientIds)
            {
                if (clientIdToConnectionIds.TryGetValue(clientId, out var connectionId) &&
                    connectionIdToAircraftStatuses.TryGetValue(connectionId, out var status))
                {
                    await Clients.Caller.UpdateAircraftToDiscord(discordUserId, clientId, status);
                    return;
                }
            }
            await Clients.Caller.UpdateAircraftToDiscord(discordUserId, clientIds.FirstOrDefault(), null);
        }

        public async Task RequestFlightPlan(string callsign)
        {
            await Clients.All.RequestFlightPlan(Context.ConnectionId, callsign);
        }

        public async Task ReturnFlightPlan(string connectionId, FlightPlanCompact flightPlan, List<string> atcConnectionIds)
        {
            await Clients.Clients(atcConnectionIds).ReturnFlightPlan(connectionId, flightPlan);
        }

        public async Task RequestFlightPlanDetails(string clientId)
        {
            var pairs = connectionIdToClientIds.ToArray();
            if (pairs.Any(p => p.Value == clientId))
            {
                var connectionId = pairs.First(p => p.Value == clientId).Key;
                await Clients.Clients(connectionId).RequestFlightPlanDetails(Context.ConnectionId);
            }
        }

        public async Task ReturnFlightPlanDetails(string connectionId, FlightPlanData flightPlan, string webConnectionId)
        {
            await Clients.Clients(webConnectionId).ReturnFlightPlanDetails(connectionId, flightPlan);
        }

        public async Task<ChannelReader<AircraftStatusBrief>> RequestFlightRoute(string clientId)
        {
            var channel = Channel.CreateUnbounded<AircraftStatusBrief>();
            clientIdToChannelWriter[clientId] = channel.Writer;
            await Clients.Clients(clientIdToConnectionIds[clientId]).RequestFlightRoute(Context.ConnectionId);
            return channel.Reader;
        }

        public async Task StreamFlightRoute(ChannelReader<AircraftStatusBrief> channel)
        {
            if (clientIdToChannelWriter.TryRemove(connectionIdToClientIds[Context.ConnectionId], out var writer))
            {
                Exception localException = null;
                try
                {
                    // Wait asynchronously for data to become available
                    while (await channel.WaitToReadAsync())
                    {
                        // Read all currently available data synchronously, before waiting for more data
                        while (channel.TryRead(out var status))
                        {
                            await writer.WriteAsync(status);
                        }
                    }
                }
                catch (Exception ex)
                {
                    localException = ex;
                }

                writer.Complete(localException);
            }
        }

        public async Task SendMessage(string from, string to, string message)
        {
            await Clients.All.SendMessage(from, to, message);
        }

        public async Task ChangeUpdateRateByCallsign(string callsign, int hz)
        {
            await Clients.All.ChangeUpdateRateByCallsign(callsign, hz);
        }

        public async Task NotifyUpdateRateChanged(int hz)
        {
            await Clients.Group("Map").NotifyUpdateRateChanged(connectionIdToClientIds[Context.ConnectionId], hz);
        }

        public async Task Join(string group)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
            if (connectionIdToClientIds.TryGetValue(Context.ConnectionId, out var clientId))
            {
                if (clientIdToAircraftGroup.TryGetValue(clientId, out var aircraftGroup))
                {
                    switch (group)
                    {
                        case "ClientMap":
                            await Groups.AddToGroupAsync(Context.ConnectionId, "ClientMap:" + aircraftGroup);
                            break;
                    }
                }
            }
        }

        public async Task Leave(string group)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }

        #region ATC

        public async Task SendATC(string to, string message)
        {
            await Clients.GroupExcept("ATC", Context.ConnectionId).SendATC(to, message);
        }

        #endregion

        public void RequestTeleport(string token, AircraftPosition position)
        {
            connectionIdToTeleportRequest[Context.ConnectionId] = (token, position);
            teleportTokenToConnectionId[token] = Context.ConnectionId;
        }

        public async Task AcceptTeleport(string token)
        {
            if (teleportTokenToConnectionId.TryGetValue(token, out string connectionId) && connectionIdToTeleportRequest.TryGetValue(connectionId, out var request))
            {
                await Clients.Caller.Teleport(connectionId, request.Item2);
            }
        }

        public static void RemoveCacheOnConnectionId(string connectionId)
        {
            connectionIdToAircraftStatuses.TryRemove(connectionId, out _);
            connectionIdToAtcStatuses.TryRemove(connectionId, out _);
        }
    }
}
