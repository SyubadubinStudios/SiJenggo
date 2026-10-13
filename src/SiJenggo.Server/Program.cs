using SiJenggo.Game.Net;
using ThreeNet;
using ThreeNet.Networking;

// Si Jenggo dedicated relay server (optional).
// Players behind NAT can both connect here instead of one hosting. The relay never simulates the game:
// it assigns player slots, tells everyone how many players are present, and forwards every packet to the
// other peers. Lockstep inputs stay authoritative on the clients, so the relay needs almost no CPU.
//
//   dotnet run --project src/SiJenggo.Server -- [port]
//
// Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.

int port = args.Length > 0 && int.TryParse(args[0], out int p) ? p : 27960;
ThreeNetRuntime.Initialize();
using var session = NetworkSession.Host(port, "si-jenggo-relay");
var slots = new Dictionary<int, int>();

Console.WriteLine($"Si Jenggo relay listening on UDP {session.Port}. Ctrl+C to stop.");

void Broadcast(ReadOnlySpan<byte> data, int? exceptPeer = null)
{
    foreach (var peer in session.Peers)
    {
        if (peer.Id != exceptPeer)
        {
            session.Send(data, 0, true, peer.Id);
        }
    }
}

void AnnouncePeers() => Broadcast([RelayProtocol.MsgPeers, (byte)slots.Count]);

session.PeerConnected += peer =>
{
    int slot = Enumerable.Range(0, 4).First(s => !slots.ContainsValue(s));
    slots[peer.Id] = slot;
    session.Send([RelayProtocol.MsgRole, (byte)slot], 0, true, peer.Id);
    Console.WriteLine($"+ {peer.EndPoint} as player {slot + 1}");
    AnnouncePeers();
};

session.PeerDisconnected += peer =>
{
    slots.Remove(peer.Id);
    Console.WriteLine($"- {peer.EndPoint}");
    AnnouncePeers();
};

session.MessageReceived += message => Broadcast(message.Data, message.Sender.Id);

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};

while (!stop.IsCancellationRequested)
{
    session.Update();
    Thread.Sleep(2);
}
