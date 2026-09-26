using FarPoint.Win.Spread;
using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Windows.Forms;

namespace FrameWebforCS.components.input
{
    public partial class InputNodesComponent : UserControl
    {
        private readonly InputDataService _input = InputDataService.Instance;
        private readonly InputNodesService _nodes = InputNodesService.Instance;
        private readonly List<string> _rowNodeIds = new List<string>();
        private readonly Dictionary<string, int> _nodeRows =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, NodeSnapshot> _displayedNodes =
            new Dictionary<string, NodeSnapshot>(StringComparer.Ordinal);
        private readonly SheetView fpSpread1_Sheet1;

        private bool _eventsAttached;
        private bool _pendingFullSynchronization;
        private bool _isApplyingServiceToUi;
        private bool _isUpdatingServiceFromUi;

        public InputNodesComponent()
        {
            InitializeComponent();

            fpSpread1_Sheet1 = fpSpread1.AddNewSheetView();
            fpSpread1_Sheet1.SheetName = "Node";

            ConfigureColumns();
            AttachEvents();
            SynchronizeAllNodes();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (_pendingFullSynchronization)
            {
                _pendingFullSynchronization = false;
                SynchronizeAllNodes();
            }
        }

        private void ConfigureColumns()
        {
            var header = fpSpread1_Sheet1.ColumnHeader;
            var column = fpSpread1_Sheet1.Columns;

            if (_input.dimension == 3)
            {
                fpSpread1_Sheet1.ColumnCount = 3;
                header.Cells[0, 0].Text = "X";
                header.Cells[0, 1].Text = "Y";
                header.Cells[0, 2].Text = "Z";
                column[0].Width = 80;
                column[1].Width = 80;
                column[2].Width = 80;
            }
            else
            {
                fpSpread1_Sheet1.ColumnCount = 2;
                header.Cells[0, 0].Text = "X";
                header.Cells[0, 1].Text = "Y";
                column[0].Width = 80;
                column[1].Width = 80;
            }

            float w = 0;
            var col = fpSpread1_Sheet1.Columns;
            for (int i = 0; i < col.Count; i++)
            {
                w += col[i].Width;
            }
            w += 100;

            Width = (int)w;
        }

        private void AttachEvents()
        {
            if (_eventsAttached)
            {
                return;
            }

            fpSpread1.Change += FpSpread1_Change;
            _nodes.NodesChanged += Nodes_NodesChanged;
            Disposed += InputNodesComponent_Disposed;
            _eventsAttached = true;
        }

        private void InputNodesComponent_Disposed(object? sender, EventArgs e)
        {
            if (!_eventsAttached)
            {
                return;
            }

            fpSpread1.Change -= FpSpread1_Change;
            _nodes.NodesChanged -= Nodes_NodesChanged;
            Disposed -= InputNodesComponent_Disposed;
            _eventsAttached = false;
        }

        private void Nodes_NodesChanged(object? sender, NodesChangedEventArgs e)
        {
            if (_isUpdatingServiceFromUi &&
                e.Kind == NodesChangeKind.NodeUpdated &&
                e.UpdatedNode.HasValue)
            {
                NodeSnapshot updatedNode = e.UpdatedNode.Value;
                _displayedNodes[updatedNode.Id] = updatedNode;
                return;
            }

            if (IsDisposed)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                _pendingFullSynchronization = true;
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke((System.Action)(() => ApplyServiceChange(e)));
                }
                catch (InvalidOperationException)
                {
                    _pendingFullSynchronization = true;
                }

                return;
            }

            ApplyServiceChange(e);
        }

        private void ApplyServiceChange(NodesChangedEventArgs e)
        {
            if (IsDisposed)
            {
                return;
            }

            if (e.Kind == NodesChangeKind.Reset)
            {
                SynchronizeAllNodes();
                return;
            }

            if (e.UpdatedNode.HasValue)
            {
                UpdateSingleNode(e.UpdatedNode.Value);
            }
        }

        private void SynchronizeAllNodes()
        {
            IReadOnlyList<NodeSnapshot> snapshot = _nodes.GetNodesSnapshot();
            var orderedNodes = new List<NodeSnapshot>(snapshot);
            orderedNodes.Sort((left, right) => CompareNodeIds(left.Id, right.Id));

            _isApplyingServiceToUi = true;
            try
            {
                _rowNodeIds.Clear();
                _nodeRows.Clear();
                _displayedNodes.Clear();
                fpSpread1_Sheet1.RowCount = orderedNodes.Count;

                for (int row = 0; row < orderedNodes.Count; row++)
                {
                    NodeSnapshot node = orderedNodes[row];
                    _rowNodeIds.Add(node.Id);
                    _nodeRows.Add(node.Id, row);
                    _displayedNodes.Add(node.Id, node);
                    fpSpread1_Sheet1.RowHeader.Cells[row, 0].Text = node.Id;
                    ApplyNodeToRow(row, node);
                }
            }
            finally
            {
                _isApplyingServiceToUi = false;
            }
        }

        private void UpdateSingleNode(NodeSnapshot node)
        {
            if (!_nodeRows.TryGetValue(node.Id, out int row))
            {
                return;
            }

            _displayedNodes[node.Id] = node;
            _isApplyingServiceToUi = true;
            try
            {
                ApplyNodeToRow(row, node);
            }
            finally
            {
                _isApplyingServiceToUi = false;
            }
        }

        private void ApplyNodeToRow(int row, NodeSnapshot node)
        {
            fpSpread1_Sheet1.Cells[row, 0].Value = node.X;
            fpSpread1_Sheet1.Cells[row, 1].Value = node.Y;

            if (fpSpread1_Sheet1.ColumnCount == 3)
            {
                fpSpread1_Sheet1.Cells[row, 2].Value = node.Z;
            }
        }

        private void FpSpread1_Change(object sender, ChangeEventArgs e)
        {
            if (_isApplyingServiceToUi ||
                _isUpdatingServiceFromUi ||
                !ReferenceEquals(sender, fpSpread1) ||
                !ReferenceEquals(e.View.GetSheetView(), fpSpread1_Sheet1) ||
                e.Row < 0 ||
                e.Row >= _rowNodeIds.Count ||
                e.Column < 0 ||
                e.Column >= fpSpread1_Sheet1.ColumnCount ||
                !TryGetAxis(e.Column, out NodeCoordinateAxis axis))
            {
                return;
            }

            string nodeId = _rowNodeIds[e.Row];
            if (!_displayedNodes.TryGetValue(nodeId, out NodeSnapshot authoritativeNode))
            {
                RevertInvalidEdit(e.Row, e.Column, null, "節点の現在値を取得できませんでした。");
                return;
            }

            string text = fpSpread1_Sheet1.Cells[e.Row, e.Column].Text;
            if (!float.TryParse(
                    text,
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.CurrentCulture,
                    out float value) ||
                !float.IsFinite(value))
            {
                RevertInvalidEdit(
                    e.Row,
                    e.Column,
                    GetCoordinate(authoritativeNode, axis),
                    "有限な数値を入力してください。");
                return;
            }

            bool updated;
            string? error;
            _isUpdatingServiceFromUi = true;
            try
            {
                updated = _nodes.TryUpdateCoordinate(nodeId, axis, value, out error);
            }
            finally
            {
                _isUpdatingServiceFromUi = false;
            }

            if (!updated)
            {
                RevertInvalidEdit(
                    e.Row,
                    e.Column,
                    GetCoordinate(authoritativeNode, axis),
                    error ?? "座標を更新できませんでした。");
            }
        }

        private void RevertInvalidEdit(int row, int column, float? value, string error)
        {
            _isApplyingServiceToUi = true;
            try
            {
                fpSpread1_Sheet1.Cells[row, column].Value = value;
            }
            finally
            {
                _isApplyingServiceToUi = false;
            }

            MessageBox.Show(
                this,
                error + "\n直前の値に戻しました。",
                "入力エラー",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static bool TryGetAxis(int column, out NodeCoordinateAxis axis)
        {
            switch (column)
            {
                case 0:
                    axis = NodeCoordinateAxis.X;
                    return true;
                case 1:
                    axis = NodeCoordinateAxis.Y;
                    return true;
                case 2:
                    axis = NodeCoordinateAxis.Z;
                    return true;
                default:
                    axis = default;
                    return false;
            }
        }

        private static float? GetCoordinate(NodeSnapshot node, NodeCoordinateAxis axis)
        {
            return axis switch
            {
                NodeCoordinateAxis.X => node.X,
                NodeCoordinateAxis.Y => node.Y,
                NodeCoordinateAxis.Z => node.Z,
                _ => null,
            };
        }

        private static int CompareNodeIds(string left, string right)
        {
            bool leftIsNumeric = BigInteger.TryParse(
                left,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out BigInteger leftNumber);
            bool rightIsNumeric = BigInteger.TryParse(
                right,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out BigInteger rightNumber);

            if (leftIsNumeric && rightIsNumeric)
            {
                int numericComparison = leftNumber.CompareTo(rightNumber);
                return numericComparison != 0
                    ? numericComparison
                    : StringComparer.Ordinal.Compare(left, right);
            }

            if (leftIsNumeric != rightIsNumeric)
            {
                return leftIsNumeric ? -1 : 1;
            }

            return StringComparer.Ordinal.Compare(left, right);
        }
    }
}
