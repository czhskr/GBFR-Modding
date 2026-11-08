namespace PartIndicator;

public sealed class NativeStateReader(nint moduleBase)
{
    private (WorldIdentity World, long Generation, nint Scene, nint Actor, uint Id, ulong Stamp, uint Type,
        nint Parameter, int[] Initials, int VerifiedMask, int BrokenMask)? _proof;
    private (WorldIdentity World, long Generation, nint Scene, nint Actor, uint Id, ulong Stamp, uint Type,
        nint Parameter, float Initial, bool BrokenSeen)? _headProof;
    private readonly Dictionary<nint, (WorldIdentity World, long Generation, nint Scene, nint Actor, uint Id, ulong Stamp, uint Type,
        nint Parameter, int[] Initials, int VerifiedMask, int BrokenMask)> _proofCache = [];
    private readonly Dictionary<nint, (WorldIdentity World, long Generation, nint Scene, nint Actor, uint Id, ulong Stamp, uint Type,
        nint Parameter, float Initial, bool BrokenSeen)> _headProofCache = [];
    private WorldIdentity _proofWorld;
    private long _proofGeneration;
    private static T R<T>(nint p) where T : unmanaged => ProcessMemory.Read<T>(p);
    private void ClearProof() { _proof = null; _headProof = null; }
    private void ClearAllProofs() { ClearProof(); _proofCache.Clear(); _headProofCache.Clear(); }
    private bool Reject() { ClearProof(); return false; }
    private bool RejectWorld() { ClearAllProofs(); return false; }

    public bool TryReadWorld(out WorldIdentity world)
    {
        world = default;
        try
        {
            nint session = R<nint>(moduleBase + 0x7c53d10);
            if (R<int>(session + 4) != 0) return RejectWorld(); // shortcut: offline only; validate online ownership before enabling it.
            uint id = R<uint>(moduleBase + 0x7035ca0);
            nint scene = R<nint>(moduleBase + 0x7035ca8), actor = R<nint>(moduleBase + 0x7035d00);
            ulong stamp = R<ulong>(moduleBase + 0x7035cb0);
            if (id is 0 or > 1048576 || scene == 0 || actor == 0 || stamp == 0) return RejectWorld();
            nint manager = R<nint>(moduleBase + 0x7020988), scenes = R<nint>(manager + 0x20), stamps = R<nint>(manager + 0x48);
            nint slot = (nint)((id - 1) * 8);
            if (R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || R<nint>(scene + 0x70) != actor ||
                R<uint>(scene + 0x38) != id || R<nint>(scene + 0x40) != scene || R<ulong>(scene + 0x48) != stamp) return RejectWorld();
            nint begin = R<nint>(moduleBase + 0x7362690), end = R<nint>(moduleBase + 0x7362698);
            long bytes = end - begin;
            if (begin == 0 || bytes is < 8 or > 128 || bytes % 8 != 0) return RejectWorld();
            nint player = 0, entry = 0, owner = 0;
            for (int offset = 0; offset < bytes; offset += 8)
            {
                nint candidate = R<nint>(begin + offset);
                if (R<nint>(candidate + 0x230) != actor) continue;
                if (player != 0) return RejectWorld();
                nint a = R<nint>(candidate + 8), b = R<nint>(a), c = R<nint>(b + 0x28);
                if (R<nint>(c + 0x18) != scene) return RejectWorld();
                player = candidate; entry = begin + offset; owner = c;
            }
            if (player == 0 || R<nint>(moduleBase + 0x7362690) != begin || R<nint>(moduleBase + 0x7362698) != end ||
                R<nint>(entry) != player || R<nint>(owner + 0x18) != scene || R<nint>(player + 0x230) != actor ||
                R<nint>(scene + 0x70) != actor || R<uint>(scene + 0x38) != id || R<ulong>(scene + 0x48) != stamp ||
                R<nint>(moduleBase + 0x7020988) != manager || R<nint>(manager + 0x20) != scenes || R<nint>(manager + 0x48) != stamps ||
                R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp ||
                R<uint>(moduleBase + 0x7035ca0) != id || R<nint>(moduleBase + 0x7035ca8) != scene || R<ulong>(moduleBase + 0x7035cb0) != stamp ||
                R<nint>(moduleBase + 0x7035d00) != actor || R<nint>(moduleBase + 0x7c53d10) != session || R<int>(session + 4) != 0) return RejectWorld();
            world = new(player, scene, actor, id, stamp); return world.Valid || RejectWorld();
        }
        catch (IOException) { return RejectWorld(); }
    }

    public bool TryReadSnapshot(WorldIdentity world, long generation, out PartSnapshot? snapshot) =>
        TryReadSnapshots(world, generation, out snapshot, out _);

    public bool TryReadSnapshots(WorldIdentity world, long generation, out PartSnapshot? snapshot, out PartSnapshot? second)
    {
        snapshot = null; second = null;
        nint cacheTarget = 0;
        try
        {
            if (!world.Valid || !TryReadWorld(out var before) || before != world) return RejectWorld();
            if (_proofWorld != world || _proofGeneration != generation)
            { ClearAllProofs(); _proofWorld = world; _proofGeneration = generation; }
            nint scene = R<nint>(world.Player + 0x108);
            if (scene == 0) return false; // A normal lock transition hides this frame without forgetting other targets.
            cacheTarget = scene;
            _proof = _proofCache.TryGetValue(scene, out var cached) ? cached : null;
            _headProof = _headProofCache.TryGetValue(scene, out var headCached) ? headCached : null;
            nint actor = R<nint>(scene + 0x70), vt = R<nint>(actor);
            int selected = R<int>(world.Player + 0x118);
            uint type = (long)(vt - moduleBase) switch {
                0x61528a8 => 0x500u, 0x6150b68 => 0x501u, 0x6151ee8 => 0x502u,
                0x61791e8 => 0x1800u, 0x617b868 => 0x1801u, 0x617aec8 => 0x1802u, 0x617c208 => 0x1803u,
                0x6179b88 => 0x1804u, 0x617cba8 => 0x1805u, 0x617a528 => 0x1806u,
                0x6176de0 => 0x7200u, 0x6178120 => 0x7210u, 0x6175aa0 => 0x7201u, 0x6177780 => 0x7211u,
                0x5b8aed8 => 0x7100u, 0x5be1e40 => 0x7101u, 0x5b09148 => 0x3100u, 0x5b77238 => 0x1900u,
                0x5b02098 => 0x1700u, 0x59e9398 => 0x7500u, 0x5a9d688 => 0x7001u, _ => 0u };
            if (_proof is { } old && (old.World != world || old.Generation != generation || old.Scene != scene || old.Actor != actor)) ClearProof();
            if (type is not (0x500 or 0x501 or 0x502)) _proof = null;
            if (type is not (0x1700 or >= 0x1800 and <= 0x1806)) _headProof = null;
            if (type == 0) return false;
            if (R<uint>(actor + 0x160) == 0) return Reject();
            if (type is 0x7200 or 0x7210 or 0x7201 or 0x7211) return TryReadFurycane(world, generation, scene, actor, vt, type, selected, out snapshot);
            if (type is 0x7100 or 0x7101) return TryReadVulkan(world, generation, scene, actor, vt, type, selected, out snapshot);
            if (type == 0x3100) return TryReadPyet(world, generation, scene, actor, vt, selected, out snapshot);
            if (type == 0x1900) return TryReadTayuitar(world, generation, scene, actor, vt, selected, out snapshot);
            if (type == 0x7001) return TryReadProto(world, generation, scene, actor, vt, selected, out snapshot);
            if (type == 0x7500) return TryReadAngra(world, generation, scene, actor, vt, selected, out snapshot, out second);
            if (type is 0x500 or 0x501 or 0x502) return TryReadCrocodile(world, generation, scene, actor, vt, type, selected, out snapshot);
            return TryReadHead(world, generation, scene, actor, vt, type, selected, out snapshot);
        }
        catch (IOException) { return Reject(); }
        finally
        {
            if (cacheTarget != 0)
            {
                // shortcut: retain at most 64 targets per world/generation; validate larger encounters before raising it.
                if ((_proof.HasValue || _headProof.HasValue) && _proofCache.Count + _headProofCache.Count >= 64 &&
                    !_proofCache.ContainsKey(cacheTarget) && !_headProofCache.ContainsKey(cacheTarget))
                { _proofCache.Clear(); _headProofCache.Clear(); }
                if (_proof is { } p) _proofCache[cacheTarget] = p; else _proofCache.Remove(cacheTarget);
                if (_headProof is { } h) _headProofCache[cacheTarget] = h; else _headProofCache.Remove(cacheTarget);
            }
        }
    }

    private bool TryReadHead(WorldIdentity world, long generation, nint scene, nint actor, nint vt, uint type, int selected, out PartSnapshot? snapshot)
    {
        snapshot = null;
        uint id = R<uint>(scene + 0x38); ulong stamp = R<ulong>(scene + 0x48);
        if (id is 0 or > 1048576 || stamp == 0) return Reject();
        nint manager = R<nint>(moduleBase + 0x7020988), scenes = R<nint>(manager + 0x20), stamps = R<nint>(manager + 0x48), slot = (nint)((id - 1) * 8);
        if (R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || R<nint>(scene + 0x40) != scene) return Reject();
        nint locks = R<nint>(actor + 0x788), locksEnd = R<nint>(actor + 0x790); long lockBytes = locksEnd - locks;
        if (locks == 0 || lockBytes is < 48 or > 24576 || lockBytes % 48 != 0) return Reject();
        int[] ids = new int[lockBytes / 48]; int matches = 0;
        for (int i = 0; i < ids.Length; i++) { ids[i] = R<int>(locks + i * 48); if (ids[i] == selected) matches++; }
        // Behemoth has two non-head body entries with ID 1; its head must still be unique.
        if (ids.Count(value => value == 5) != 1 || matches == 0 || (type != 0x1700 && matches != 1)) return Reject();
        nint hb = R<nint>(actor + 0xcd8), he = R<nint>(actor + 0xce0); long bytes = he - hb;
        if (bytes is not (0 or 4)) return Reject();
        bool broken = bytes == 4;
        if (broken && R<int>(hb) != 0) return Reject();
        int currentOffset = type == 0x1700 ? 0x2660 : 0x27a4;
        float current = R<float>(actor + currentOffset);
        if (!float.IsFinite(current) || current is < 0 or > 1e9f) return Reject();
        nint parameter = R<nint>(actor + 0x1598); byte flags = R<byte>(actor + 0xe);
        int configuredInteger = type == 0x1700 ? R<int>(parameter + 0x8b0) : 0;
        float configured = type == 0x1700 ? configuredInteger : R<float>(parameter + 0xf50);
        var proof = _headProof;
        // shortcut: alternate-mode maxima remain icon-only until their actual initialization is validated.
        if ((flags & 0x40) != 0 || !float.IsFinite(configured) || configured is < 1 or > 1e9f ||
            (type == 0x1700 && configuredInteger is < 1 or > 1000000000) || current > configured) proof = null;
        else
        {
            if (proof is { } old && (old.World != world || old.Generation != generation || old.Scene != scene || old.Actor != actor ||
                old.Id != id || old.Stamp != stamp || old.Type != type || old.Parameter != parameter || old.Initial != configured ||
                (old.BrokenSeen && !broken))) proof = null;
            if (proof is null && !broken && current == configured) proof = (world, generation, scene, actor, id, stamp, type, parameter, configured, false);
            if (proof is { } p && broken) proof = p with { BrokenSeen = true };
        }
        if (R<float>(actor + currentOffset) != current || R<nint>(actor + 0x1598) != parameter ||
            (type == 0x1700 ? R<int>(parameter + 0x8b0) != configuredInteger : !R<float>(parameter + 0xf50).Equals(configured)) || R<byte>(actor + 0xe) != flags ||
            R<nint>(actor + 0xcd8) != hb || R<nint>(actor + 0xce0) != he || (broken && R<int>(hb) != 0) ||
            R<nint>(actor + 0x788) != locks || R<nint>(actor + 0x790) != locksEnd || ids.Where((value, i) => R<int>(locks + i * 48) != value).Any() ||
            R<nint>(world.Player + 0x108) != scene || R<int>(world.Player + 0x118) != selected || R<nint>(scene + 0x70) != actor || R<nint>(actor) != vt ||
            R<uint>(scene + 0x38) != id || R<nint>(scene + 0x40) != scene || R<ulong>(scene + 0x48) != stamp ||
            R<nint>(moduleBase + 0x7020988) != manager || R<nint>(manager + 0x20) != scenes || R<nint>(manager + 0x48) != stamps ||
            R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || !TryReadWorld(out var after) || after != world) return Reject();
        _headProof = proof;
        if (selected != 5) return false;
        double? initial = proof?.Initial;
        snapshot = new(scene, actor, type, selected, true, current, initial, broken ? [0] : [], initial.HasValue, generation);
        return PartStatePolicy.Evaluate(snapshot).Visible;
    }

    private bool TryReadCrocodile(WorldIdentity world, long generation, nint scene, nint actor, nint vt, uint type, int selected, out PartSnapshot? snapshot)
    {
        snapshot = null;
        int index = PartStatePolicy.CrocodilePartIndex(type, selected);
        uint id = R<uint>(scene + 0x38); ulong stamp = R<ulong>(scene + 0x48);
        if (id is 0 or > 1048576 || stamp == 0) return Reject();
        nint manager = R<nint>(moduleBase + 0x7020988), scenes = R<nint>(manager + 0x20), stamps = R<nint>(manager + 0x48), slot = (nint)((id - 1) * 8);
        if (R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || R<nint>(scene + 0x40) != scene) return Reject();
        nint locks = R<nint>(actor + 0x788), locksEnd = R<nint>(actor + 0x790); long lockBytes = locksEnd - locks;
        if (locks == 0 || lockBytes is < 48 or > 24576 || lockBytes % 48 != 0) return Reject();
        int[] ids = new int[lockBytes / 48]; int matches = 0;
        for (int i = 0; i < ids.Length; i++) { ids[i] = R<int>(locks + i * 48); if (ids[i] == selected) matches++; }
        if (matches != 1) return Reject();
        nint hb = R<nint>(actor + 0xcd8), he = R<nint>(actor + 0xce0); long historyBytes = he - hb;
        if (historyBytes < 0 || historyBytes > 12 || historyBytes % 4 != 0) return Reject();
        int[] history = new int[historyBytes / 4]; int brokenMask = 0;
        for (int i = 0; i < history.Length; i++)
        {
            history[i] = R<int>(hb + i * 4);
            if (history[i] is < 0 or > 2 || (brokenMask & (1 << history[i])) != 0) return Reject();
            brokenMask |= 1 << history[i];
        }
        int[] current = new int[3]; float[] configured = new float[3];
        nint parameter = R<nint>(actor + 0x1598); byte flags = R<byte>(actor + 0xe);
        for (int i = 0; i < 3; i++)
        {
            current[i] = R<int>(actor + 0x2a98 + i * 4); configured[i] = R<float>(parameter + 0x8b0 + i * 4);
            if (current[i] is < 0 or > 1000000000) return Reject();
        }
        var proof = _proof;
        // shortcut: alternate-mode initial values remain unverified; keep the state icon without a percentage.
        if ((flags & 0x40) != 0 || configured.Any(x => !float.IsFinite(x) || x is < 1 or > 1e9f)) proof = null;
        else
        {
            int[] initial = configured.Select(x => (int)x).ToArray();
            if (proof is { } old && (old.World != world || old.Generation != generation || old.Scene != scene || old.Actor != actor ||
                old.Id != id || old.Stamp != stamp || old.Type != type || old.Parameter != parameter || !old.Initials.SequenceEqual(initial) ||
                (brokenMask & old.BrokenMask) != old.BrokenMask)) proof = null;
            if (current.Where((value, i) => value > initial[i]).Any()) proof = null;
            else
            {
                int verified = proof?.VerifiedMask ?? 0;
                for (int i = 0; i < 3; i++) if (current[i] == initial[i] && (brokenMask & (1 << i)) == 0) verified |= 1 << i;
                proof = (world, generation, scene, actor, id, stamp, type, parameter, initial, verified, brokenMask);
            }
        }
        if (R<nint>(actor + 0x1598) != parameter || R<byte>(actor + 0xe) != flags ||
            current.Where((value, i) => R<int>(actor + 0x2a98 + i * 4) != value).Any() ||
            configured.Where((value, i) => !R<float>(parameter + 0x8b0 + i * 4).Equals(value)).Any() ||
            R<nint>(actor + 0xcd8) != hb || R<nint>(actor + 0xce0) != he || history.Where((value, i) => R<int>(hb + i * 4) != value).Any() ||
            R<nint>(actor + 0x788) != locks || R<nint>(actor + 0x790) != locksEnd || ids.Where((value, i) => R<int>(locks + i * 48) != value).Any() ||
            R<nint>(world.Player + 0x108) != scene || R<int>(world.Player + 0x118) != selected || R<nint>(scene + 0x70) != actor || R<nint>(actor) != vt ||
            R<uint>(scene + 0x38) != id || R<nint>(scene + 0x40) != scene || R<ulong>(scene + 0x48) != stamp ||
            R<nint>(moduleBase + 0x7020988) != manager || R<nint>(manager + 0x20) != scenes || R<nint>(manager + 0x48) != stamps ||
            R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || !TryReadWorld(out var after) || after != world) return Reject();
        _proof = proof;
        if (index < 0) return false;
        double? maximum = proof is { } p && (p.VerifiedMask & (1 << index)) != 0 ? p.Initials[index] : null;
        snapshot = new(scene, actor, type, selected, true, current[index], maximum, history, maximum.HasValue, generation);
        return PartStatePolicy.Evaluate(snapshot).Visible;
    }

    private static nint FindStateComponent(nint actor, uint key)
    {
        nint registry = actor + 0xc0;
        if (R<uint>(registry + 8) != 0) return 0;
        foreach (int offset in new[] { 0x10, 0x20 })
        {
            nint head = R<nint>(registry + offset);
            if (head == 0) continue;
            nint node = R<nint>(head + 8), best = head;
            var seen = new HashSet<nint>();
            while (node != 0 && R<byte>(node + 0x19) == 0)
            {
                if (seen.Count >= 128 || !seen.Add(node)) throw new IOException("State component tree bound");
                if (R<uint>(node + 0x20) >= key) { best = node; node = R<nint>(node); }
                else node = R<nint>(node + 0x10);
            }
            if (node == 0) return 0;
            if (best == head || R<uint>(best + 0x20) != key) continue;
            nint component = R<nint>(best + 0x28);
            if (component == 0 || R<uint>(registry + 8) != 0 || R<nint>(registry + offset) != head ||
                R<uint>(best + 0x20) != key || R<nint>(best + 0x28) != component) return 0;
            return component;
        }
        return 0;
    }

    private static nint FindStateIntNode(nint component, uint key)
    {
        nint head = R<nint>(component + 0x98), found = 0;
        if (head == 0) return 0;
        var seen = new HashSet<nint>();
        for (nint node = R<nint>(head); node != head; node = R<nint>(node))
        {
            if (node == 0 || seen.Count >= 512 || !seen.Add(node)) throw new IOException("State integer list bound");
            if (R<uint>(node + 0x10) != key) continue;
            if (found != 0) throw new IOException("Duplicate state integer");
            found = node;
        }
        return found;
    }

    private bool TryReadProto(WorldIdentity world, long generation, nint scene, nint actor, nint vt, int selected, out PartSnapshot? snapshot)
    {
        snapshot = null;
        if (selected != 2048) return false;
        uint id = R<uint>(scene + 0x38); ulong stamp = R<ulong>(scene + 0x48);
        if (id is 0 or > 1048576 || stamp == 0) return Reject();
        nint manager = R<nint>(moduleBase + 0x7020988), scenes = R<nint>(manager + 0x20), stamps = R<nint>(manager + 0x48), slot = (nint)((id - 1) * 8);
        if (R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || R<nint>(scene + 0x40) != scene) return Reject();
        nint locks = R<nint>(actor + 0x788), end = R<nint>(actor + 0x790); long bytes = end - locks;
        if (locks == 0 || bytes is < 48 or > 24576 || bytes % 48 != 0) return Reject();
        int[] ids = new int[bytes / 48]; int chosen = -1;
        for (int i = 0; i < ids.Length; i++)
        { ids[i] = R<int>(locks + i * 48); if (ids[i] == selected) { if (chosen != -1) return Reject(); chosen = i; } }
        if (chosen < 0 || R<byte>(locks + chosen * 48 + 0x29) != 1) return false;
        nint owner = R<nint>(actor + 0x648);
        if (owner == 0) return Reject();
        nint obj = R<nint>(owner);
        if (obj == 0) return Reject();
        nint context = R<nint>(obj + 0x28);
        if (context == 0 || R<nint>(context + 0x18) != scene || R<nint>(scene + 0x70) != actor) return Reject();
        int epoch = R<int>(moduleBase + 0x7c28684); uint key = R<uint>(moduleBase + 0x7c28680);
        if (epoch is 0 or -1 || key > 65535) return false;
        nint component = FindStateComponent(actor, key);
        if (component == 0) return false;
        nint node = FindStateIntNode(component, 0x76033489);
        if (node == 0) return false;
        int current = R<int>(node + 0x14);
        if (current is < 0 or > 1000000000) return Reject();
        nint hb = R<nint>(actor + 0xcd8), he = R<nint>(actor + 0xce0); long historyBytes = he - hb;
        if (historyBytes is not (0 or 4) || historyBytes == 4 && (hb == 0 || R<int>(hb) != 0)) return Reject();
        bool processed = historyBytes == 4;
        if (current == 0 && !processed) return false; // A separate native reset also writes zero.
        nint parameter = R<nint>(actor + 0x1598);
        if (parameter == 0) return Reject();
        byte flags = R<byte>(actor + 0xe); int configured = R<int>(parameter + 0x5978);
        bool verified = (flags & 0x40) == 0 && configured is >= 1 and <= 1000000000 && current <= configured;
        if (R<int>(moduleBase + 0x7c28684) != epoch || R<uint>(moduleBase + 0x7c28680) != key ||
            FindStateComponent(actor, key) != component || FindStateIntNode(component, 0x76033489) != node || R<int>(node + 0x14) != current ||
            R<nint>(actor + 0x648) != owner || R<nint>(owner) != obj || R<nint>(obj + 0x28) != context || R<nint>(context + 0x18) != scene ||
            R<nint>(actor + 0x1598) != parameter || R<int>(parameter + 0x5978) != configured || R<byte>(actor + 0xe) != flags ||
            R<nint>(actor + 0xcd8) != hb || R<nint>(actor + 0xce0) != he || processed && R<int>(hb) != 0 ||
            R<nint>(actor + 0x788) != locks || R<nint>(actor + 0x790) != end || R<byte>(locks + chosen * 48 + 0x29) != 1 ||
            ids.Where((value, i) => R<int>(locks + i * 48) != value).Any() ||
            R<nint>(world.Player + 0x108) != scene || R<int>(world.Player + 0x118) != selected || R<nint>(scene + 0x70) != actor || R<nint>(actor) != vt ||
            R<uint>(scene + 0x38) != id || R<nint>(scene + 0x40) != scene || R<ulong>(scene + 0x48) != stamp ||
            R<nint>(moduleBase + 0x7020988) != manager || R<nint>(manager + 0x20) != scenes || R<nint>(manager + 0x48) != stamps ||
            R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || !TryReadWorld(out var after) || after != world) return Reject();
        bool broken = current == 0 && processed;
        snapshot = new(scene, actor, 0x7001, selected, true, current, verified ? configured : null, processed ? [0] : [], verified, generation, broken);
        return PartStatePolicy.Evaluate(snapshot).Visible;
    }

    private bool TryReadTayuitar(WorldIdentity world, long generation, nint scene, nint actor, nint vt, int selected, out PartSnapshot? snapshot)
    {
        snapshot = null;
        int index = PartStatePolicy.TayuitarLegIndex(0x1900, selected); bool head = selected == 7;
        if (!head && index < 0) return false;
        uint id = R<uint>(scene + 0x38); ulong stamp = R<ulong>(scene + 0x48);
        if (id is 0 or > 1048576 || stamp == 0) return Reject();
        nint manager = R<nint>(moduleBase + 0x7020988), scenes = R<nint>(manager + 0x20), stamps = R<nint>(manager + 0x48), slot = (nint)((id - 1) * 8);
        if (R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || R<nint>(scene + 0x40) != scene) return Reject();
        nint locks = R<nint>(actor + 0x788), end = R<nint>(actor + 0x790); long bytes = end - locks;
        if (locks == 0 || bytes is < 48 or > 24576 || bytes % 48 != 0) return Reject();
        int[] ids = new int[bytes / 48]; int chosen = -1;
        for (int i = 0; i < ids.Length; i++)
        { ids[i] = R<int>(locks + i * 48); if (ids[i] == selected) { if (chosen != -1) return Reject(); chosen = i; } }
        if (chosen < 0 || R<byte>(locks + chosen * 48 + 0x29) != 1) return false;
        nint parameter = R<nint>(actor + 0x1598); byte flags = R<byte>(actor + 0xe);
        if (parameter == 0) return Reject();
        nint row = head ? 0 : actor + 0x6a68 + index * 40, currentAddress = head ? actor + 0x9420 : row;
        int current = R<int>(currentAddress), cachedInitial = 0, kind = 0, attachment = 0, configured = 0;
        byte active = 0, marker = 0; bool verified = false;
        if (current is < 0 or > 1000000000) return Reject();
        if (head)
        {
            marker = R<byte>(actor + 0x9996); configured = R<int>(parameter + 0xbf0);
            if (marker > 1) return Reject();
            verified = (flags & 0x40) == 0 && configured is >= 1 and <= 1000000000 && current <= configured;
        }
        else
        {
            cachedInitial = R<int>(row + 4); active = R<byte>(row + 8); attachment = R<int>(row + 12); kind = R<int>(row + 16);
            if (cachedInitial is < 1 or > 1000000000 || kind != index || attachment != 0 || active != (current == 0 ? 0 : 1)) return Reject();
            // shortcut: leg resets change current without refreshing cached max; validate cycle scaling before showing percentages.
        }
        if (R<int>(currentAddress) != current || R<nint>(actor + 0x1598) != parameter || R<byte>(actor + 0xe) != flags ||
            (head ? R<byte>(actor + 0x9996) != marker || R<int>(parameter + 0xbf0) != configured :
                R<int>(row + 4) != cachedInitial || R<byte>(row + 8) != active || R<int>(row + 12) != attachment || R<int>(row + 16) != kind) ||
            R<nint>(actor + 0x788) != locks || R<nint>(actor + 0x790) != end || R<byte>(locks + chosen * 48 + 0x29) != 1 ||
            ids.Where((value, i) => R<int>(locks + i * 48) != value).Any() ||
            R<nint>(world.Player + 0x108) != scene || R<int>(world.Player + 0x118) != selected || R<nint>(scene + 0x70) != actor || R<nint>(actor) != vt ||
            R<uint>(scene + 0x38) != id || R<nint>(scene + 0x40) != scene || R<ulong>(scene + 0x48) != stamp ||
            R<nint>(moduleBase + 0x7020988) != manager || R<nint>(manager + 0x20) != scenes || R<nint>(manager + 0x48) != stamps ||
            R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || !TryReadWorld(out var after) || after != world) return Reject();
        bool broken = head ? current == 0 && marker == 1 : active == 0;
        snapshot = new(scene, actor, 0x1900, selected, true, current, verified ? configured : null, [], verified, generation, broken);
        return PartStatePolicy.Evaluate(snapshot).Visible;
    }

    private bool TryReadPyet(WorldIdentity world, long generation, nint scene, nint actor, nint vt, int selected, out PartSnapshot? snapshot)
    {
        snapshot = null;
        int index = PartStatePolicy.PyetPartIndex(0x3100, selected);
        if (index < 0) return false;
        uint id = R<uint>(scene + 0x38); ulong stamp = R<ulong>(scene + 0x48);
        if (id is 0 or > 1048576 || stamp == 0) return Reject();
        nint manager = R<nint>(moduleBase + 0x7020988), scenes = R<nint>(manager + 0x20), stamps = R<nint>(manager + 0x48), slot = (nint)((id - 1) * 8);
        if (R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || R<nint>(scene + 0x40) != scene) return Reject();
        nint locks = R<nint>(actor + 0x788), locksEnd = R<nint>(actor + 0x790); long lockBytes = locksEnd - locks;
        if (locks == 0 || lockBytes is < 48 or > 24576 || lockBytes % 48 != 0) return Reject();
        int[] ids = new int[lockBytes / 48]; int matches = 0;
        for (int i = 0; i < ids.Length; i++) { ids[i] = R<int>(locks + i * 48); if (ids[i] == selected) matches++; }
        if (matches != 1) return Reject();
        nint hb = R<nint>(actor + 0xcd8), he = R<nint>(actor + 0xce0); long historyBytes = he - hb;
        if (historyBytes is < 0 or > 28 || historyBytes % 4 != 0) return Reject();
        int[] history = new int[historyBytes / 4]; int seen = 0;
        for (int i = 0; i < history.Length; i++)
        {
            history[i] = R<int>(hb + i * 4);
            if (history[i] is < 0 or > 6 || (seen & (1 << history[i])) != 0) return Reject();
            seen |= 1 << history[i];
        }
        nint parameter = R<nint>(actor + 0x1598); byte flags = R<byte>(actor + 0xe);
        int[] offsets = [0x20, 0x10, 0x14, 0x18, 0x1c, 0xc, 8];
        int[] current = new int[7], stored = new int[7]; float[] configured = new float[7];
        for (int i = 0; i < 7; i++)
        {
            current[i] = R<int>(actor + 0x25b4 + i * 4); stored[i] = R<int>(actor + 0x25d0 + i * 4);
            configured[i] = R<float>(parameter + 0xf60 + offsets[i]);
            if (current[i] is < 0 or > 1000000000) return Reject();
        }
        // shortcut: the Hell initializer branch remains icon-only until its runtime values are validated.
        bool verified = (flags & 0x40) == 0 && configured.All(x => float.IsFinite(x) && x is >= 1 and <= 1e9f) &&
            !stored.Where((value, i) => value != (int)configured[i]).Any() && !current.Where((value, i) => value > stored[i]).Any();
        bool broken = current[index] == 0 && (seen & (1 << index)) != 0;
        if (current[index] == 0 && !broken) return false;
        if (R<nint>(actor + 0x1598) != parameter || R<byte>(actor + 0xe) != flags ||
            current.Where((value, i) => R<int>(actor + 0x25b4 + i * 4) != value).Any() ||
            stored.Where((value, i) => R<int>(actor + 0x25d0 + i * 4) != value).Any() ||
            configured.Where((value, i) => !R<float>(parameter + 0xf60 + offsets[i]).Equals(value)).Any() ||
            R<nint>(actor + 0xcd8) != hb || R<nint>(actor + 0xce0) != he || history.Where((value, i) => R<int>(hb + i * 4) != value).Any() ||
            R<nint>(actor + 0x788) != locks || R<nint>(actor + 0x790) != locksEnd || ids.Where((value, i) => R<int>(locks + i * 48) != value).Any() ||
            R<nint>(world.Player + 0x108) != scene || R<int>(world.Player + 0x118) != selected || R<nint>(scene + 0x70) != actor || R<nint>(actor) != vt ||
            R<uint>(scene + 0x38) != id || R<nint>(scene + 0x40) != scene || R<ulong>(scene + 0x48) != stamp ||
            R<nint>(moduleBase + 0x7020988) != manager || R<nint>(manager + 0x20) != scenes || R<nint>(manager + 0x48) != stamps ||
            R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || !TryReadWorld(out var after) || after != world) return Reject();
        snapshot = new(scene, actor, 0x3100, selected, true, current[index], verified ? stored[index] : null, history, verified, generation, broken);
        return PartStatePolicy.Evaluate(snapshot).Visible;
    }

    private bool TryReadFurycane(WorldIdentity world, long generation, nint scene, nint actor, nint vt, uint type, int selected, out PartSnapshot? snapshot)
    {
        snapshot = null;
        bool humanoid = type is 0x7201 or 0x7211;
        bool temporaryArm = !humanoid && selected is 48 or 80;
        // Each form uses its own native layout and lock relationship; cosmetic/core rows stay hidden.
        if (humanoid ? PartStatePolicy.FurycaneArmorIndex(type, selected) < 0 : selected is not (48 or 80 or 114 or 130 or 146 or 162)) return false;
        uint id = R<uint>(scene + 0x38); ulong stamp = R<ulong>(scene + 0x48);
        if (id is 0 or > 1048576 || stamp == 0) return Reject();
        nint manager = R<nint>(moduleBase + 0x7020988), scenes = R<nint>(manager + 0x20), stamps = R<nint>(manager + 0x48), slot = (nint)((id - 1) * 8);
        if (R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || R<nint>(scene + 0x40) != scene) return Reject();
        nint begin = R<nint>(actor + 0x788), end = R<nint>(actor + 0x790); long bytes = end - begin;
        if (begin == 0 || bytes is < 48 or > 24576 || bytes % 48 != 0) return Reject();
        int[] ids = new int[bytes / 48], maps = new int[humanoid ? 0 : 9]; int chosen = -1;
        for (int i = 0; i < ids.Length; i++)
        {
            ids[i] = R<int>(begin + i * 48);
            if (ids[i] != selected) continue;
            if (chosen != -1) return Reject();
            chosen = i;
        }
        if (chosen == -1) return false;
        // Down transitions can leave a selected arm behind after its native lock is disabled.
        if (temporaryArm && R<byte>(begin + chosen * 48 + 0x29) != 1) return false;
        int index = humanoid ? PartStatePolicy.FurycaneArmorIndex(type, selected) : -1;
        if (humanoid && chosen != index) return Reject();
        for (int i = 0; i < maps.Length; i++)
        {
            maps[i] = R<int>(actor + 0x25bc + i * 4);
            if (i is >= 4 and <= 7 && (maps[i] < 0 || maps[i] >= ids.Length)) return Reject();
            if (maps[i] != chosen) continue;
            if (index != -1) return Reject();
            index = i;
        }
        if (humanoid ? index is < 1 or > 5 : temporaryArm ? index is < 2 or > 3 : index is < 4 or > 7) return false;
        nint record = actor + (humanoid ? 0x2604 : 0x25e8) + index * (humanoid ? 12 : 20);
        int current = R<int>(record), initial = R<int>(record + 4); byte broken = R<byte>(record + 8);
        if (current < 0 || initial is < 1 or > 1000000000 || current > initial || broken > 1) return Reject();
        if (R<int>(record) != current || R<int>(record + 4) != initial || R<byte>(record + 8) != broken ||
            temporaryArm && R<byte>(begin + chosen * 48 + 0x29) != 1 ||
            R<nint>(actor + 0x788) != begin || R<nint>(actor + 0x790) != end ||
            ids.Where((value, i) => R<int>(begin + i * 48) != value).Any() ||
            maps.Where((value, i) => R<int>(actor + 0x25bc + i * 4) != value).Any() ||
            R<nint>(world.Player + 0x108) != scene || R<int>(world.Player + 0x118) != selected || R<nint>(scene + 0x70) != actor || R<nint>(actor) != vt ||
            R<uint>(scene + 0x38) != id || R<nint>(scene + 0x40) != scene || R<ulong>(scene + 0x48) != stamp ||
            R<nint>(moduleBase + 0x7020988) != manager || R<nint>(manager + 0x20) != scenes || R<nint>(manager + 0x48) != stamps ||
            R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || !TryReadWorld(out var after) || after != world) return Reject();
        snapshot = new(scene, actor, type, selected, true, current, initial, [], true, generation, broken != 0);
        return PartStatePolicy.Evaluate(snapshot).Visible;
    }

    private bool TryReadAngra(WorldIdentity world, long generation, nint scene, nint actor, nint vt, int selected,
        out PartSnapshot? snapshot, out PartSnapshot? second)
    {
        snapshot = second = null;
        if (selected != 5) return false;
        int mode = R<int>(actor + 0x2288);
        if (mode is not (0 or 1 or 2)) return false;
        uint id = R<uint>(scene + 0x38); ulong stamp = R<ulong>(scene + 0x48);
        if (id is 0 or > 1048576 || stamp == 0) return Reject();
        nint manager = R<nint>(moduleBase + 0x7020988), scenes = R<nint>(manager + 0x20), stamps = R<nint>(manager + 0x48), slot = (nint)((id - 1) * 8);
        if (R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || R<nint>(scene + 0x40) != scene) return Reject();
        nint controller = actor + 0x21b0, controllerVt = R<nint>(controller), owner = R<nint>(controller + 8);
        nint ownerObject = R<nint>(owner), ownerContext = R<nint>(ownerObject + 0x28);
        if (controllerVt != moduleBase + 0x6166438 || R<nint>(ownerContext + 0x18) != scene) return Reject();
        nint states = R<nint>(controller + 0x10), statesEnd = R<nint>(controller + 0x18),
            definitions = R<nint>(controller + 0x28), definitionsEnd = R<nint>(controller + 0x30),
            damage = R<nint>(controller + 0x40), damageEnd = R<nint>(controller + 0x48);
        if (states == 0 || definitions == 0 || damage == 0 || statesEnd - states != 4 * 12 ||
            definitionsEnd - definitions != 4 * 56 || damageEnd - damage != 4 * 56) return Reject();
        nint parameter = R<nint>(actor + 0x1598);
        int[] current = new int[4], initial = new int[4], configured = new int[4]; byte[] broken = new byte[4];
        uint[] masks = [0x400000, 0x100000, 0x800000, 0x200000];
        bool DefinitionMatches(int i) => R<int>(definitions + i * 56) == i && R<int>(definitions + i * 56 + 4) == -1 &&
            R<byte>(definitions + i * 56 + 8) == 1 && R<int>(damage + i * 56) == i && R<uint>(damage + i * 56 + 8) == masks[i];
        for (int i = 0; i < 4; i++)
        {
            if (!DefinitionMatches(i)) return Reject();
            current[i] = R<int>(states + i * 12); initial[i] = R<int>(states + i * 12 + 4); broken[i] = R<byte>(states + i * 12 + 8);
            configured[i] = R<int>(parameter + 0x2df8 + i * 16);
            if (current[i] is < 0 or > 1000000000 || initial[i] is < 0 or > 1000000000 ||
                (initial[i] > 0 && current[i] > initial[i]) || broken[i] > 1) return Reject();
        }
        nint begin = R<nint>(actor + 0x788), end = R<nint>(actor + 0x790); long bytes = end - begin;
        if (begin == 0 || bytes is < 48 or > 24576 || bytes % 48 != 0) return Reject();
        int[] ids = new int[bytes / 48];
        for (int i = 0; i < ids.Length; i++) ids[i] = R<int>(begin + i * 48);
        // Native Angra has several head5 rows; the selected head groups the two verified horns.
        if (!ids.Contains(5)) return false;
        if (R<int>(actor + 0x2288) != mode || R<nint>(actor + 0x1598) != parameter ||
            current.Where((value, i) => R<int>(states + i * 12) != value).Any() ||
            initial.Where((value, i) => R<int>(states + i * 12 + 4) != value).Any() ||
            broken.Where((value, i) => R<byte>(states + i * 12 + 8) != value).Any() ||
            configured.Where((value, i) => R<int>(parameter + 0x2df8 + i * 16) != value).Any() ||
            Enumerable.Range(0, 4).Any(i => !DefinitionMatches(i)) ||
            R<nint>(controller) != controllerVt || R<nint>(controller + 8) != owner || R<nint>(owner) != ownerObject ||
            R<nint>(ownerObject + 0x28) != ownerContext || R<nint>(ownerContext + 0x18) != scene ||
            R<nint>(controller + 0x10) != states || R<nint>(controller + 0x18) != statesEnd ||
            R<nint>(controller + 0x28) != definitions || R<nint>(controller + 0x30) != definitionsEnd ||
            R<nint>(controller + 0x40) != damage || R<nint>(controller + 0x48) != damageEnd ||
            R<nint>(actor + 0x788) != begin || R<nint>(actor + 0x790) != end || ids.Where((value, i) => R<int>(begin + i * 48) != value).Any() ||
            R<nint>(world.Player + 0x108) != scene || R<int>(world.Player + 0x118) != selected || R<nint>(scene + 0x70) != actor || R<nint>(actor) != vt ||
            R<uint>(scene + 0x38) != id || R<nint>(scene + 0x40) != scene || R<ulong>(scene + 0x48) != stamp ||
            R<nint>(moduleBase + 0x7020988) != manager || R<nint>(manager + 0x20) != scenes || R<nint>(manager + 0x48) != stamps ||
            R<nint>(scenes + slot) != scene || R<ulong>(stamps + slot) != stamp || !TryReadWorld(out var after) || after != world) return Reject();
        PartSnapshot Horn(int i)
        {
            bool verified = initial[i] > 0 && initial[i] == configured[i];
            return new(scene, actor, 0x7500, selected, true, current[i], verified ? initial[i] : null, [], verified, generation, broken[i] != 0);
        }
        int first = mode == 2 ? 2 : 0;
        snapshot = Horn(first); second = Horn(first + 1);
        return PartStatePolicy.Evaluate(snapshot).Visible && PartStatePolicy.Evaluate(second).Visible;
    }

    private bool TryReadVulkan(WorldIdentity world, long generation, nint scene, nint actor, nint vt, uint type, int selected, out PartSnapshot? snapshot)
    {
        snapshot = null;
        bool alternate = type == 0x7101;
        if (alternate ? selected is not (3840 or 282) : selected is not (9 or 13)) return false;
        if (alternate && R<byte>(actor + 0x1b0c) != 1) return false;
        uint rootId = R<uint>(scene + 0x38); ulong rootStamp = R<ulong>(scene + 0x48);
        nint manager = R<nint>(moduleBase + 0x7020988), scenes = R<nint>(manager + 0x20), stamps = R<nint>(manager + 0x48);
        bool SceneMatches(nint s, nint a, uint id, ulong stamp) => id is > 0 and <= 1048576 && stamp != 0 &&
            R<nint>(scenes + (nint)((id - 1) * 8)) == s && R<ulong>(stamps + (nint)((id - 1) * 8)) == stamp &&
            R<uint>(s + 0x38) == id && R<nint>(s + 0x40) == s && R<ulong>(s + 0x48) == stamp && R<nint>(s + 0x70) == a;
        if (!SceneMatches(scene, actor, rootId, rootStamp)) return Reject();
        nint parameter = R<nint>(actor + 0x1598), definitionOwner = alternate ? R<nint>(actor + 0x1ad0) : parameter + 0xc00;
        bool tail = definitionOwner == parameter + 0x6c8;
        if (alternate && (definitionOwner != parameter + 0x6b0 && !tail || selected != (tail ? 282 : 3840))) return Reject();
        int count = alternate ? 1 : 2, handlesOffset = alternate ? 0x1a98 : 0x22c8;
        nint definitions = R<nint>(definitionOwner), definitionsEnd = R<nint>(definitionOwner + 8);
        nint begin = R<nint>(actor + handlesOffset), end = R<nint>(actor + handlesOffset + 8);
        bool ContainerMatches() => !alternate || R<nint>(actor + 0x1ad0) == definitionOwner &&
            R<uint>(actor + 0x1a80) == rootId && R<nint>(actor + 0x1a88) == scene && R<ulong>(actor + 0x1a90) == rootStamp;
        if (definitions == 0 || definitionsEnd - definitions != count * 0xd0 || begin == 0 || end - begin != count * 24 || !ContainerMatches()) return Reject();
        bool ModelMatches(int index)
        {
            nint d = definitions + index * 0xd0;
            string expected = alternate ? (tail ? "b00_tail" : "b00_scale") : (index == 0 ? "c10_arm" : "c00_arm");
            if (alternate && (R<ushort>(d + 0x68) != 0x7100 || R<short>(d + 0x50) != (tail ? 282 : 22))) return false;
            if (R<ulong>(d + 0xa0) != (ulong)expected.Length || R<ulong>(d + 0xa8) < (ulong)expected.Length || R<ulong>(d + 0xa8) >= 16) return false;
            for (int i = 0; i < expected.Length; i++) if (R<byte>(d + 0x90 + i) != expected[i]) return false;
            return R<byte>(d + 0x90 + expected.Length) == 0;
        }
        if (Enumerable.Range(0, count).Any(i => !ModelMatches(i))) return Reject();
        nint locks = R<nint>(actor + 0x788), locksEnd = R<nint>(actor + 0x790); long bytes = locksEnd - locks;
        if (locks == 0 || bytes is < 48 or > 24576 || bytes % 48 != 0) return Reject();
        int[] ids = new int[bytes / 48]; int matches = 0;
        for (int i = 0; i < ids.Length; i++) { ids[i] = R<int>(locks + i * 48); if (ids[i] == selected) matches++; }
        if (matches != 1) return Reject();
        var parts = new (uint Id, nint Scene, ulong Stamp, nint Actor, int Definition, float Current, float Initial)[count];
        int seen = 0, chosen = -1, wanted = alternate || selected == 9 ? 0 : 1;
        for (int i = 0; i < parts.Length; i++)
        {
            nint entry = begin + i * 24; uint id = R<uint>(entry); nint childScene = R<nint>(entry + 8); ulong stamp = R<ulong>(entry + 16);
            if (id is 0 or > 1048576 || stamp == 0 || childScene == 0 ||
                R<nint>(scenes + (nint)((id - 1) * 8)) != childScene || R<ulong>(stamps + (nint)((id - 1) * 8)) != stamp) return Reject();
            nint child = R<nint>(childScene + 0x70);
            if (!SceneMatches(childScene, child, id, stamp) || R<nint>(child) != moduleBase + 0x5d825e0 ||
                R<uint>(child + 0x368) != rootId || R<nint>(child + 0x370) != scene || R<ulong>(child + 0x378) != rootStamp) return Reject();
            int index = R<int>(child + 0x5e0);
            if (index < 0 || index >= count || (seen & (1 << index)) != 0) return Reject();
            seen |= 1 << index;
            float current = R<float>(child + 0x5c4), initial = R<float>(child + 0x5c0);
            if (!float.IsFinite(current) || !float.IsFinite(initial) || initial is < 1 or > 1e9f || current < 0 || current > initial) return Reject();
            parts[i] = (id, childScene, stamp, child, index, current, initial);
            if (index == wanted) chosen = i;
        }
        if (chosen == -1) return Reject();
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i]; nint entry = begin + i * 24;
            if (R<uint>(entry) != p.Id || R<nint>(entry + 8) != p.Scene || R<ulong>(entry + 16) != p.Stamp ||
                !SceneMatches(p.Scene, p.Actor, p.Id, p.Stamp) || R<nint>(p.Actor) != moduleBase + 0x5d825e0 ||
                R<uint>(p.Actor + 0x368) != rootId || R<nint>(p.Actor + 0x370) != scene || R<ulong>(p.Actor + 0x378) != rootStamp ||
                R<int>(p.Actor + 0x5e0) != p.Definition || R<float>(p.Actor + 0x5c4) != p.Current || R<float>(p.Actor + 0x5c0) != p.Initial) return Reject();
        }
        if (alternate && R<byte>(actor + 0x1b0c) != 1 ||
            R<nint>(actor + 0x1598) != parameter || R<nint>(definitionOwner) != definitions || R<nint>(definitionOwner + 8) != definitionsEnd || !ContainerMatches() ||
            R<nint>(actor + handlesOffset) != begin || R<nint>(actor + handlesOffset + 8) != end || Enumerable.Range(0, count).Any(i => !ModelMatches(i)) ||
            R<nint>(actor + 0x788) != locks || R<nint>(actor + 0x790) != locksEnd || ids.Where((id, i) => R<int>(locks + i * 48) != id).Any() ||
            R<nint>(world.Player + 0x108) != scene || R<int>(world.Player + 0x118) != selected || !SceneMatches(scene, actor, rootId, rootStamp) || R<nint>(actor) != vt ||
            R<nint>(moduleBase + 0x7020988) != manager || R<nint>(manager + 0x20) != scenes || R<nint>(manager + 0x48) != stamps ||
            !TryReadWorld(out var after) || after != world) return Reject();
        var arm = parts[chosen];
        snapshot = new(scene, actor, type, selected, true, arm.Current, arm.Initial, [], true, generation, arm.Current == 0);
        return PartStatePolicy.Evaluate(snapshot).Visible;
    }
}
