using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace FrameWebforCS.components.input
{
    internal class clsMember : INotifyPropertyChanged
    {
        public string? ni = null;
        public string? nj = null;
        public string? e = null;
        public float? cg = null;
        private float? _iLength;
        private float? _jLength;
        private int? _rigidMaterial;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? Ni { get => ni; set { ni = value; Changed(nameof(Ni)); } }
        public string? Nj { get => nj; set { nj = value; Changed(nameof(Nj)); } }
        public string? E { get => e; set { e = value; Changed(nameof(E)); } }
        public float? Cg { get => cg; set { cg = value; Changed(nameof(Cg)); } }
        public float? Ilength { get => _iLength; set { _iLength = value; Changed(nameof(Ilength)); } }
        public float? Jlength { get => _jLength; set { _jLength = value; Changed(nameof(Jlength)); } }
        public int? E1 { get => _rigidMaterial; set { _rigidMaterial = value; Changed(nameof(E1)); } }

        public bool IsEmpty => string.IsNullOrWhiteSpace(ni) && string.IsNullOrWhiteSpace(nj)
            && string.IsNullOrWhiteSpace(e) && cg == null;
        public bool IsRigidEmpty => Ilength == null && Jlength == null && E1 == null;

        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal class InputMembersService
    {
        private const int MaxNodeId = 100_000;
        private static readonly Lazy<InputMembersService> _instance = new(() => new InputMembersService());
        public static InputMembersService Instance => _instance.Value;

        private Dictionary<string, clsMember> _member = new();
        private HashSet<int> _rigidRows = new();
        public BindingList<clsMember> Members { get; } = new();

        private InputMembersService()
        {
            Members.AllowNew = false;
            Members.AllowRemove = false;
            Members.RaiseListChangedEvents = false;
            for (int row = 0; row < MaxNodeId; row++)
                Members.Add(new clsMember());
            Members.RaiseListChangedEvents = true;
            Members.ListChanged += Members_ListChanged;
        }

        public void clear()
        {
            ReplaceRows(new Dictionary<string, clsMember>());
            ReplaceRigidRows(new Dictionary<int, (float? ILength, float? JLength, int? Material)>());
        }

        public void setMemberJson(JsonElement jsonData)
        {
            if (!jsonData.TryGetProperty("member", out JsonElement memberJson) ||
                memberJson.ValueKind != JsonValueKind.Object)
                return;

            var next = new Dictionary<string, clsMember>();
            var seenIds = new HashSet<string>();
            foreach (JsonProperty entry in memberJson.EnumerateObject())
            {
                if (!int.TryParse(entry.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int row) ||
                    row < 1 || row > MaxNodeId)
                    throw new JsonException($"Invalid member: {entry.Name}");
                string id = row.ToString(CultureInfo.InvariantCulture);
                if (!seenIds.Add(id))
                    throw new JsonException($"Duplicate member: {entry.Name}");
                clsMember? member = DataHelperModule.JsonToClass<clsMember>(entry.Value);
                if (member != null && !member.IsEmpty)
                    next.Add(id, member);
            }
            ReplaceRows(next);
        }

        public Dictionary<string, object> getMemberJson()
        {
            var members = new Dictionary<string, object>();
            foreach (var (id, member) in _member)
                if (!member.IsEmpty)
                    members.Add(id, DataHelperModule.ClassToDictionary(member));
            return members;
        }

        public void setRigidJson(JsonElement jsonData)
        {
            var next = new Dictionary<int, (float? ILength, float? JLength, int? Material)>();
            if (jsonData.TryGetProperty("rigid", out JsonElement rigidJson))
            {
                if (rigidJson.ValueKind != JsonValueKind.Array)
                    throw new JsonException("Invalid rigid data");
                foreach (JsonElement entry in rigidJson.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object ||
                        !entry.TryGetProperty("m", out JsonElement idJson) ||
                        idJson.ValueKind != JsonValueKind.String ||
                        !int.TryParse(idJson.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out int row) ||
                        row < 1 || row > MaxNodeId || next.ContainsKey(row))
                        throw new JsonException("Invalid rigid member number");
                    next.Add(row, (
                        ReadRigidLength(entry, "Ilength"),
                        ReadRigidLength(entry, "Jlength"),
                        ReadRigidMaterial(entry)));
                }
            }
            ReplaceRigidRows(next);
        }

        public List<object> getRigidJson()
        {
            var result = new List<object>();
            foreach (int row in _rigidRows.OrderBy(row => row))
            {
                clsMember member = Members[row - 1];
                if (member.E1 is int material)
                    result.Add(new {
                        m = row.ToString(CultureInfo.InvariantCulture),
                        Ilength = member.Ilength ?? 0,
                        Jlength = member.Jlength ?? 0,
                        e = material
                    });
            }
            return result;
        }

        private static float? ReadRigidLength(JsonElement entry, string name)
        {
            if (!entry.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
                return null;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float length) && float.IsFinite(length))
                return length;
            throw new JsonException($"Invalid rigid {name}");
        }

        private static int? ReadRigidMaterial(JsonElement entry)
        {
            if (!entry.TryGetProperty("e", out JsonElement value) || value.ValueKind == JsonValueKind.Null)
                return null;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int material))
                return material;
            throw new JsonException("Invalid rigid material");
        }

        private void ReplaceRigidRows(Dictionary<int, (float? ILength, float? JLength, int? Material)> next)
        {
            Members.RaiseListChangedEvents = false;
            try
            {
                foreach (int row in _rigidRows)
                {
                    clsMember member = Members[row - 1];
                    member.Ilength = null;
                    member.Jlength = null;
                    member.E1 = null;
                }
                foreach (var (row, value) in next)
                {
                    clsMember member = Members[row - 1];
                    member.Ilength = value.ILength;
                    member.Jlength = value.JLength;
                    member.E1 = value.Material;
                }
                _rigidRows = next.Where(item => item.Value.ILength != null ||
                    item.Value.JLength != null || item.Value.Material != null)
                    .Select(item => item.Key).ToHashSet();
            }
            finally
            {
                Members.RaiseListChangedEvents = true;
                Members.ResetBindings();
            }
        }

        private void Members_ListChanged(object? sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType != ListChangedType.ItemChanged || e.NewIndex < 0)
                return;
            string id = (e.NewIndex + 1).ToString(CultureInfo.InvariantCulture);
            clsMember member = Members[e.NewIndex];
            if (member.IsEmpty)
                _member.Remove(id);
            else
                _member[id] = member;
            if (member.IsRigidEmpty)
                _rigidRows.Remove(e.NewIndex + 1);
            else
                _rigidRows.Add(e.NewIndex + 1);
        }

        private void ReplaceRows(Dictionary<string, clsMember> next)
        {
            Members.RaiseListChangedEvents = false;
            try
            {
                foreach (string id in _member.Keys)
                    if (!next.ContainsKey(id))
                        Members[int.Parse(id, CultureInfo.InvariantCulture) - 1] = new clsMember();
                foreach (var (id, member) in next)
                    Members[int.Parse(id, CultureInfo.InvariantCulture) - 1] = member;
                _member = next;
            }
            finally
            {
                Members.RaiseListChangedEvents = true;
                Members.ResetBindings();
            }
        }
    }
}
