using Google.Cloud.Firestore;
using MusRoyalePC.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MusRoyalePC.Services;

public sealed class AvatarStoreService
{
    private readonly FirestoreDb _db;

    public AvatarStoreService(FirestoreDb db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<AvatarStoreItem>> GetCatalogAsync()
    {
        // Colección: Avatars (registro de avatares en Firebase)
        // Campos esperados por documento:
        //  - file (string) -> "avaX.png"
        //  - priceOro (number)
        //  - name (string) opcional
        QuerySnapshot snap = await _db.Collection("Avatars").GetSnapshotAsync();

        var list = new List<AvatarStoreItem>();
        foreach (var doc in snap.Documents)
        {
            string file = doc.ContainsField("file") ? doc.GetValue<string>("file") : doc.Id;
            int price = 0;
            if (doc.ContainsField("priceOro")) price = Convert.ToInt32(doc.GetValue<long>("priceOro"));
            string name = doc.ContainsField("name") ? doc.GetValue<string>("name") : System.IO.Path.GetFileNameWithoutExtension(file);

            list.Add(new AvatarStoreItem { Id = doc.Id, File = file, PriceOro = price, Name = name });
        }

        return list.OrderBy(a => a.PriceOro).ThenBy(a => a.Name).ToList();
    }

    public async Task<(int oro, List<string> owned)> GetUserOroAndOwnedAsync(string userId)
    {
        DocumentSnapshot userDoc = await _db.Collection("Users").Document(userId).GetSnapshotAsync();
        if (!userDoc.Exists) return (0, new List<string>());

        int oro = 0;
        if (userDoc.ContainsField("oro")) oro = Convert.ToInt32(userDoc.GetValue<long>("oro"));

        List<string> owned = userDoc.ContainsField("avatarsComprados")
            ? (userDoc.GetValue<List<string>>("avatarsComprados") ?? new List<string>())
            : new List<string>();

        // Asegurar que el avatar por defecto siempre esté
        if (!owned.Contains("avadef.png")) owned.Add("avadef.png");

        return (oro, owned);
    }

    public async Task PurchaseAvatarAsync(string userId, AvatarStoreItem item)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId vacío");
        if (item is null) throw new ArgumentNullException(nameof(item));
        if (string.IsNullOrWhiteSpace(item.File)) throw new ArgumentException("avatar file vacío");

        DocumentReference userRef = _db.Collection("Users").Document(userId);

        await _db.RunTransactionAsync(async tx =>
        {
            DocumentSnapshot userDoc = await tx.GetSnapshotAsync(userRef);
            if (!userDoc.Exists) throw new InvalidOperationException("Usuario no existe");

            int oro = userDoc.ContainsField("oro") ? Convert.ToInt32(userDoc.GetValue<long>("oro")) : 0;
            List<string> owned = userDoc.ContainsField("avatarsComprados")
                ? (userDoc.GetValue<List<string>>("avatarsComprados") ?? new List<string>())
                : new List<string>();

            if (owned.Contains(item.File))
                return;

            if (oro < item.PriceOro)
                throw new InvalidOperationException("Ez daukazu nahikoa urre.");

            tx.Update(userRef, new Dictionary<string, object>
            {
                ["oro"] = oro - item.PriceOro,
                ["avatarsComprados"] = FieldValue.ArrayUnion(item.File)
            });
        });
    }

    public async Task SetCurrentAvatarAsync(string userId, string avatarFile)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId vacío");
        if (string.IsNullOrWhiteSpace(avatarFile)) throw new ArgumentException("avatarFile vacío");

        await _db.Collection("Users").Document(userId).UpdateAsync("avatarActual", avatarFile);
    }
}
