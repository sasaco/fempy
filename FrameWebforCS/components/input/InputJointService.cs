using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace FrameWebforCS.components.input
{
    internal class clsJoint : INotifyPropertyChanged
    {
        public int? row;
        public string? m = null;
        public float? xi = null;
        public float? yi = null;
        public float? zi = null;
        public float? xj = null;
        public float? yj = null;
        public float? zj = null;

        public event PropertyChangedEventHandler? PropertyChanged;
        public string? M { get => m; set { m = value; Changed(nameof(M)); } }
        public float? Xi { get => xi; set { xi = value; Changed(nameof(Xi)); } }
        public float? Yi { get => yi; set { yi = value; Changed(nameof(Yi)); } }
        public float? Zi { get => zi; set { zi = value; Changed(nameof(Zi)); } }
        public float? Xj { get => xj; set { xj = value; Changed(nameof(Xj)); } }
        public float? Yj { get => yj; set { yj = value; Changed(nameof(Yj)); } }
        public float? Zj { get => zj; set { zj = value; Changed(nameof(Zj)); } }
        public bool IsEmpty => string.IsNullOrWhiteSpace(m) && xi == null && yi == null && zi == null && xj == null && yj == null && zj == null;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal class InputJointService
    {
        private const int MaxNodeId = 100_000;
        private const int SheetCount = 6;
        private static readonly Lazy<InputJointService> _instance = new(() => new InputJointService());
        public static InputJointService Instance => _instance.Value;

        private Dictionary<string, List<clsJoint>> _joint = new();
        private readonly Dictionary<string, BindingList<clsJoint>> _sheets = new();

        private InputJointService()
        {
            for (int sheet = 1; sheet <= SheetCount; sheet++)
            {
                string id = sheet.ToString(CultureInfo.InvariantCulture);
                var rows = new BindingList<clsJoint> { AllowNew = false, AllowRemove = false, RaiseListChangedEvents = false };
                for (int index = 0; index < MaxNodeId; index++) rows.Add(new clsJoint());
                rows.RaiseListChangedEvents = true;
                rows.ListChanged += (_, e) => RowsChanged(id, rows, e);
                _sheets.Add(id, rows);
            }
        }

        public BindingList<clsJoint> GetRows(string sheetName) => _sheets[sheetName];

        public void clear() => ReplaceRows(new Dictionary<string, List<clsJoint>>());

        public void setJointJson(JsonElement jsonData)
        {
            var loaded = DataHelperModule.JsonToDict(jsonData, "joint",
                static json => DataHelperModule.JsonToList<clsJoint>(json));
            if (loaded == null) return;
            ValidateRows(loaded);
            ReplaceRows(loaded);
        }

        public Dictionary<string, object> getJointJson()
        {
            var result = new Dictionary<string, object>();
            foreach (var (sheet, rows) in _joint)
            {
                var data = new List<Dictionary<string, object?>>();
                foreach (var value in rows.OrderBy(value => value.row))
                    if (!value.IsEmpty) data.Add(DataHelperModule.ClassToDictionary(value));
                if (data.Count > 0) result.Add(sheet, data);
            }
            return result;
        }

        private static void ValidateRows(Dictionary<string, List<clsJoint>> data)
        {
            foreach (var (sheet, values) in data)
            {
                if (!int.TryParse(sheet, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ||
                    number < 1 || number > SheetCount || sheet != number.ToString(CultureInfo.InvariantCulture))
                    throw new JsonException($"Invalid joint sheet: {sheet}");
                var seen = new HashSet<int>();
                foreach (var value in values)
                    if (value.row is not int row || row < 1 || row > MaxNodeId || !seen.Add(row))
                        throw new JsonException($"Invalid joint row: {value.row}");
            }
        }

        private void RowsChanged(string sheet, BindingList<clsJoint> rows, ListChangedEventArgs e)
        {
            if (e.ListChangedType != ListChangedType.ItemChanged || e.NewIndex < 0) return;
            var value = rows[e.NewIndex];
            value.row = e.NewIndex + 1;
            if (!_joint.TryGetValue(sheet, out var active))
                _joint[sheet] = active = new List<clsJoint>();
            active.RemoveAll(item => item.row == value.row);
            if (!value.IsEmpty) active.Add(value);
            if (active.Count == 0) _joint.Remove(sheet);
        }

        private void ReplaceRows(Dictionary<string, List<clsJoint>> next)
        {
            foreach (var (sheet, rows) in _sheets)
            {
                rows.RaiseListChangedEvents = false;
                try
                {
                    if (_joint.TryGetValue(sheet, out var old))
                        foreach (var item in old) rows[item.row!.Value - 1] = new clsJoint();
                    if (next.TryGetValue(sheet, out var current))
                        foreach (var item in current) rows[item.row!.Value - 1] = item;
                }
                finally { rows.RaiseListChangedEvents = true; rows.ResetBindings(); }
            }
            _joint = next;
        }
    }
}
