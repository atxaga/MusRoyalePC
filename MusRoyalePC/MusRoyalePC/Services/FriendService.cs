using Google.Cloud.Firestore;
using MusRoyalePC.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MusRoyalePC.Services;

public sealed class FriendService
{
    private readonly FirestoreDb _db;

    public FriendService(FirestoreDb db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<UserStatsDto?> GetUserStatsAsync(string targetUserId, string currentUserId)
    {
        if (string.IsNullOrWhiteSpace(targetUserId)) return null;

        DocumentSnapshot target = await _db.Collection("Users").Document(targetUserId).GetSnapshotAsync();
        if (!target.Exists) return null;

        DocumentSnapshot me = await _db.Collection("Users").Document(currentUserId).GetSnapshotAsync();

        string username = target.ContainsField("username") ? target.GetValue<string>("username") : targetUserId;
        string email = target.ContainsField("email") ? target.GetValue<string>("email") : string.Empty;

        int partidak = 0;
        if (target.ContainsField("partidak")) partidak = Convert.ToInt32(target.GetValue<long>("partidak"));

        int partidaIrabaziak = 0;
        if (target.ContainsField("partidaIrabaziak")) partidaIrabaziak = Convert.ToInt32(target.GetValue<long>("partidaIrabaziak"));

        List<string> amigos = me.Exists && me.ContainsField("amigos") ? me.GetValue<List<string>>("amigos") : new();
        List<string> mandadas = me.Exists && me.ContainsField("solicitudMandada") ? me.GetValue<List<string>>("solicitudMandada") : new();
        List<string> recibidas = me.Exists && me.ContainsField("solicitudRecibida") ? me.GetValue<List<string>>("solicitudRecibida") : new();

        return new UserStatsDto
        {
            Id = targetUserId,
            Username = username,
            Email = email,
            Partidak = partidak,
            PartidaIrabaziak = partidaIrabaziak,
            IsFriend = amigos.Contains(targetUserId),
            RequestAlreadySent = mandadas.Contains(targetUserId),
            RequestAlreadyReceived = recibidas.Contains(targetUserId),
        };
    }

    public async Task SendFriendRequestAsync(string fromUserId, string toUserId)
    {
        if (string.IsNullOrWhiteSpace(fromUserId)) throw new ArgumentException("fromUserId vacío");
        if (string.IsNullOrWhiteSpace(toUserId)) throw new ArgumentException("toUserId vacío");
        if (string.Equals(fromUserId, toUserId, StringComparison.Ordinal)) return;

        // from: se guarda en solicitudesMandadas
        await _db.Collection("Users").Document(fromUserId)
            .UpdateAsync("solicitudMandada", FieldValue.ArrayUnion(toUserId));

        // to: se guarda en solicitudRecivida la id del que manda
        await _db.Collection("Users").Document(toUserId)
            .UpdateAsync("solicitudRecibida", FieldValue.ArrayUnion(fromUserId));
    }
}
