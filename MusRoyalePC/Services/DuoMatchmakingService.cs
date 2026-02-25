using Google.Cloud.Firestore;
using MusRoyalePC.Models;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MusRoyalePC.Services;

/// <summary>
/// Matchmaking de dúos basado en Firestore (colección PartidaDuo), siguiendo la lógica Android.
/// </summary>
public sealed class DuoMatchmakingService
{
    private FirestoreChangeListener? _listener;
    private FirestoreChangeListener? _inviteListener;

    private readonly System.Collections.Generic.HashSet<string> _seenInvites = new(StringComparer.Ordinal);

    public event Action<DuoMatchState>? OnStateChanged;
    public event Action<DuoInvite>? OnInviteReceived;

    public void StopListening()
    {
        try
        {
            _listener?.StopAsync();
        }
        catch
        {
            // ignore
        }
        finally
        {
            _listener = null;
        }
    }

    public async Task<string> CreateInviteAsync(string emisorId, string receptorId, int apuesta, CancellationToken ct = default)
    {
        var db = FirestoreService.Instance.Db;
        var docRef = db.Collection("PartidaDuo").Document();

        var payload = new
        {
            idemisor = emisorId,
            idreceptor = receptorId,
            onartua = false,
            jokatu = false,
            apuesta = apuesta
        };

        await docRef.SetAsync(payload, cancellationToken: ct);
        return docRef.Id;
    }

    public void Listen(string partidaId)
    {
        StopListening();

        var db = FirestoreService.Instance.Db;
        var docRef = db.Collection("PartidaDuo").Document(partidaId);

        _listener = docRef.Listen(snapshot =>
        {
            try
            {
                if (snapshot == null || !snapshot.Exists)
                {
                    Raise(new DuoMatchState(partidaId, DuoPhase.Deleted, null, null, false, false, 0));
                    return;
                }

                string? idemisor = snapshot.ContainsField("idemisor") ? snapshot.GetValue<string>("idemisor") : null;
                string? idreceptor = snapshot.ContainsField("idreceptor") ? snapshot.GetValue<string>("idreceptor") : null;
                bool onartua = snapshot.ContainsField("onartua") && snapshot.GetValue<bool>("onartua");
                bool jokatu = snapshot.ContainsField("jokatu") && snapshot.GetValue<bool>("jokatu");
                int apuesta = 0;
                if (snapshot.ContainsField("apuesta"))
                {
                    try { apuesta = Convert.ToInt32(snapshot.GetValue<long>("apuesta")); } catch { }
                    try { apuesta = Convert.ToInt32(snapshot.GetValue<int>("apuesta")); } catch { }
                }

                var phase = DuoPhase.WaitingAccept;
                if (onartua && !jokatu) phase = DuoPhase.Accepted;
                if (onartua && jokatu) phase = DuoPhase.Play;

                Raise(new DuoMatchState(partidaId, phase, idemisor, idreceptor, onartua, jokatu, apuesta));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DuoMatchmakingService] Listen error: {ex}");
            }
        });
    }

    public async Task AcceptAsync(string partidaId, CancellationToken ct = default)
    {
        var db = FirestoreService.Instance.Db;
        await db.Collection("PartidaDuo").Document(partidaId).UpdateAsync("onartua", true, cancellationToken: ct);
    }

    public async Task SetPlayAsync(string partidaId, CancellationToken ct = default)
    {
        var db = FirestoreService.Instance.Db;
        await db.Collection("PartidaDuo").Document(partidaId).UpdateAsync("jokatu", true, cancellationToken: ct);
    }

    public async Task DeleteAsync(string partidaId, CancellationToken ct = default)
    {
        var db = FirestoreService.Instance.Db;
        await db.Collection("PartidaDuo").Document(partidaId).DeleteAsync(cancellationToken: ct);
    }

    public void StopInviteListening()
    {
        try
        {
            _inviteListener?.StopAsync();
        }
        catch { }
        finally
        {
            _inviteListener = null;
        }
    }

    public void ListenInvitesForUser(string receptorId)
    {
        StopInviteListening();
        _seenInvites.Clear();

        try
        {
            var db = FirestoreService.Instance.Db;
            var query = db.Collection("PartidaDuo")
                .WhereEqualTo("idreceptor", receptorId)
                .WhereEqualTo("onartua", false);

            Debug.WriteLine($"[DuoMatchmakingService] ListenInvitesForUser receptorId='{receptorId}'");

            _inviteListener = query.Listen(snapshot =>
            {
                try
                {
                    Debug.WriteLine($"[DuoMatchmakingService] Invite snapshot docs={snapshot.Documents.Count} changes={snapshot.Changes.Count}");

                    foreach (var change in snapshot.Changes)
                    {
                        if (change.ChangeType is not (DocumentChange.Type.Added or DocumentChange.Type.Modified))
                            continue;

                        var doc = change.Document;
                        if (!doc.Exists) continue;

                        if (!_seenInvites.Add(doc.Id))
                            continue;

                        string? idemisor = doc.ContainsField("idemisor") ? doc.GetValue<string>("idemisor") : null;
                        int apuesta = 0;
                        if (doc.ContainsField("apuesta"))
                        {
                            try { apuesta = Convert.ToInt32(doc.GetValue<long>("apuesta")); } catch { }
                            try { apuesta = Convert.ToInt32(doc.GetValue<int>("apuesta")); } catch { }
                        }

                        Debug.WriteLine($"[DuoMatchmakingService] Invite detected docId='{doc.Id}', idemisor='{idemisor}', apuesta={apuesta}");
                        OnInviteReceived?.Invoke(new DuoInvite(doc.Id, idemisor ?? string.Empty, receptorId, apuesta));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DuoMatchmakingService] Invite callback error: {ex}");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DuoMatchmakingService] Error starting invite listener: {ex}");
        }
    }

    private void Raise(DuoMatchState state)
    {
        try
        {
            OnStateChanged?.Invoke(state);
        }
        catch
        {
            // ignore
        }
    }
}

public enum DuoPhase
{
    WaitingAccept,
    Accepted,
    Play,
    Deleted
}

public sealed record DuoMatchState(
    string PartidaId,
    DuoPhase Phase,
    string? EmisorId,
    string? ReceptorId,
    bool Onartua,
    bool Jokatu,
    int Apuesta);

public sealed record DuoInvite(string PartidaId, string EmisorId, string ReceptorId, int Apuesta);
