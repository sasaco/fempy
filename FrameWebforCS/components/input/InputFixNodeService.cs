using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace FrameWebforCS.components.input
{
    internal class clsFixNode : INotifyPropertyChanged
    {
        public int row;
        public string? n = null;
        public float? tx = null;
        public float? ty = null;
        public float? tz = null;
        public float? rx = null;
        public float? ry = null;
        public float? rz = null;

        public event PropertyChangedEventHandler? PropertyChanged;
        public string? N { get => n; set { n = value; Changed(nameof(N)); } }
        public float? Tx { get => tx; set { tx = value; Changed(nameof(Tx)); } }
        public float? Ty { get => ty; set { ty = value; Changed(nameof(Ty)); } }
        public float? Tz { get => tz; set { tz = value; Changed(nameof(Tz)); } }
        public float? Rx { get => rx; set { rx = value; Changed(nameof(Rx)); } }
        public float? Ry { get => ry; set { ry = value; Changed(nameof(Ry)); } }
        public float? Rz { get => rz; set { rz = value; Changed(nameof(Rz)); } }
        public bool IsEmpty => string.IsNullOrWhiteSpace(n) && tx == null && ty == null && tz == null && rx == null && ry == null && rz == null;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal class InputFixNodeService
    {
        private const int MaxNodeId = 100_000;
        private const int SheetCount = 6;
        private static readonly Lazy<InputFixNodeService> _instance = new(() => new InputFixNodeService());
        public static InputFixNodeService Instance => _instance.Value;

        private Dictionary<string, List<clsFixNode>> _fix_node = new();
        private readonly Dictionary<string, BindingList<clsFixNode>> _sheets = new();

        private InputFixNodeService()
        {
            for (int sheet = 1; sheet <= SheetCount; sheet++)
            {
                string id = sheet.ToString(CultureInfo.InvariantCulture);
                var rows = new BindingList<clsFixNode> { AllowNew = false, AllowRemove = false, RaiseListChangedEvents = false };
                for (int index = 0; index < MaxNodeId; index++) rows.Add(new clsFixNode());
                rows.RaiseListChangedEvents = true;
                rows.ListChanged += (_, e) => RowsChanged(id, rows, e);
                _sheets.Add(id, rows);
            }
        }

        public BindingList<clsFixNode> GetRows(string sheetName) => _sheets[sheetName];

        public void clear() => ReplaceRows(new Dictionary<string, List<clsFixNode>>());

        public void setFixNodeJson(JsonElement jsonData)
        {
            var loaded = DataHelperModule.JsonToDict(jsonData, "fix_node",
                static json => DataHelperModule.JsonToList<clsFixNode>(json));
            if (loaded == null) return;
            ValidateRows(loaded);
            ReplaceRows(loaded);
        }

        public Dictionary<string, object> getFixNodeJson()
        {
            var result = new Dictionary<string, object>();
            foreach (var (sheet, rows) in _fix_node)
            {
                var data = new List<Dictionary<string, object?>>();
                foreach (var value in rows.OrderBy(value => value.row))
                    if (!value.IsEmpty) data.Add(DataHelperModule.ClassToDictionary(value));
                if (data.Count > 0) result.Add(sheet, data);
            }
            return result;
        }

        private static void ValidateRows(Dictionary<string, List<clsFixNode>> data)
        {
            foreach (var (sheet, values) in data)
            {
                if (!int.TryParse(sheet, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ||
                    number < 1 || number > SheetCount || sheet != number.ToString(CultureInfo.InvariantCulture))
                    throw new JsonException($"Invalid fix_node sheet: {sheet}");
                var seen = new HashSet<int>();
                foreach (var value in values)
                    if (value.row < 1 || value.row > MaxNodeId || !seen.Add(value.row))
                        throw new JsonException($"Invalid fix_node row: {value.row}");
            }
        }

        private void RowsChanged(string sheet, BindingList<clsFixNode> rows, ListChangedEventArgs e)
        {
            if (e.ListChangedType != ListChangedType.ItemChanged || e.NewIndex < 0) return;
            var value = rows[e.NewIndex];
            value.row = e.NewIndex + 1;
            if (!_fix_node.TryGetValue(sheet, out var active))
                _fix_node[sheet] = active = new List<clsFixNode>();
            active.RemoveAll(item => item.row == value.row);
            if (!value.IsEmpty) active.Add(value);
            if (active.Count == 0) _fix_node.Remove(sheet);
        }

        private void ReplaceRows(Dictionary<string, List<clsFixNode>> next)
        {
            foreach (var (sheet, rows) in _sheets)
            {
                rows.RaiseListChangedEvents = false;
                try
                {
                    if (_fix_node.TryGetValue(sheet, out var old))
                        foreach (var item in old) rows[item.row - 1] = new clsFixNode();
                    if (next.TryGetValue(sheet, out var current))
                        foreach (var item in current) rows[item.row - 1] = item;
                }
                finally { rows.RaiseListChangedEvents = true; rows.ResetBindings(); }
            }
            _fix_node = next;
        }
    }
}
