using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace FrameWebforCS.components.input
{
    internal class clsNode
    {
        public clsNode(float? x = null, float? y = null, float? z = null)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float? X;
        public float? Y;
        public float? Z;
    }

    internal enum NodeCoordinateAxis
    {
        X,
        Y,
        Z,
    }

    internal enum NodesChangeKind
    {
        Reset,
        NodeUpdated,
    }

    internal readonly struct NodeSnapshot
    {
        public NodeSnapshot(string id, float? x, float? y, float? z)
        {
            Id = id;
            X = x;
            Y = y;
            Z = z;
        }

        public string Id { get; }
        public float? X { get; }
        public float? Y { get; }
        public float? Z { get; }
    }

    internal sealed class NodesChangedEventArgs : EventArgs
    {
        private NodesChangedEventArgs(NodesChangeKind kind, NodeSnapshot? updatedNode)
        {
            Kind = kind;
            UpdatedNode = updatedNode;
        }

        public NodesChangeKind Kind { get; }
        public NodeSnapshot? UpdatedNode { get; }

        public static NodesChangedEventArgs Reset()
        {
            return new NodesChangedEventArgs(NodesChangeKind.Reset, null);
        }

        public static NodesChangedEventArgs NodeUpdated(NodeSnapshot node)
        {
            return new NodesChangedEventArgs(NodesChangeKind.NodeUpdated, node);
        }
    }

    internal class InputNodesService
    {
        private static readonly Lazy<InputNodesService> _instance =
            new Lazy<InputNodesService>(() => new InputNodesService());

        public static InputNodesService Instance => _instance.Value;

        private readonly object _syncRoot = new object();
        private Dictionary<string, clsNode> _node =
            new Dictionary<string, clsNode>(StringComparer.Ordinal);
        private EventHandler<NodesChangedEventArgs>? _nodesChanged;

        public event EventHandler<NodesChangedEventArgs> NodesChanged
        {
            add
            {
                lock (_syncRoot)
                {
                    _nodesChanged += value;
                }
            }
            remove
            {
                lock (_syncRoot)
                {
                    _nodesChanged -= value;
                }
            }
        }

        private InputNodesService()
        {
            clear();
        }

        public void clear()
        {
            EventHandler<NodesChangedEventArgs>? handler;
            lock (_syncRoot)
            {
                _node = new Dictionary<string, clsNode>(StringComparer.Ordinal);
                handler = _nodesChanged;
            }

            handler?.Invoke(this, NodesChangedEventArgs.Reset());
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setNodeJson(JsonElement jsonData)
        {
            var nodes = DataHelperModule.JsonToDict(jsonData, "node", ReadNode);
            if (nodes == null)
            {
                return;
            }

            EventHandler<NodesChangedEventArgs>? handler;
            lock (_syncRoot)
            {
                _node = new Dictionary<string, clsNode>(nodes, StringComparer.Ordinal);
                handler = _nodesChanged;
            }

            handler?.Invoke(this, NodesChangedEventArgs.Reset());
        }

        public IReadOnlyList<NodeSnapshot> GetNodesSnapshot()
        {
            lock (_syncRoot)
            {
                var snapshot = new List<NodeSnapshot>(_node.Count);
                foreach (KeyValuePair<string, clsNode> node in _node)
                {
                    snapshot.Add(ToSnapshot(node.Key, node.Value));
                }

                return snapshot.AsReadOnly();
            }
        }

        public bool TryUpdateCoordinate(
            string nodeId,
            NodeCoordinateAxis axis,
            float value,
            out string? error)
        {
            EventHandler<NodesChangedEventArgs>? handler;
            NodesChangedEventArgs change;
            lock (_syncRoot)
            {
                if (string.IsNullOrEmpty(nodeId) || !_node.TryGetValue(nodeId, out clsNode? current))
                {
                    error = "指定された節点 ID は存在しません。";
                    return false;
                }

                if (!float.IsFinite(value))
                {
                    error = "有限な数値を入力してください。";
                    return false;
                }

                float? currentValue;
                switch (axis)
                {
                    case NodeCoordinateAxis.X:
                        currentValue = current.X;
                        break;
                    case NodeCoordinateAxis.Y:
                        currentValue = current.Y;
                        break;
                    case NodeCoordinateAxis.Z:
                        currentValue = current.Z;
                        break;
                    default:
                        error = "指定された座標軸は存在しません。";
                        return false;
                }

                if (currentValue.HasValue && currentValue.Value.Equals(value))
                {
                    error = null;
                    return true;
                }

                var replacement = new clsNode(current.X, current.Y, current.Z);
                switch (axis)
                {
                    case NodeCoordinateAxis.X:
                        replacement.X = value;
                        break;
                    case NodeCoordinateAxis.Y:
                        replacement.Y = value;
                        break;
                    case NodeCoordinateAxis.Z:
                        replacement.Z = value;
                        break;
                }

                _node[nodeId] = replacement;
                NodeSnapshot updatedNode = ToSnapshot(nodeId, replacement);
                handler = _nodesChanged;
                change = NodesChangedEventArgs.NodeUpdated(updatedNode);
                error = null;
            }

            handler?.Invoke(this, change);
            return true;
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        public Dictionary<string, object> getNodeJson()
        {
            lock (_syncRoot)
            {
                var nodes = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, clsNode> node in _node)
                {
                    nodes.Add(
                        node.Key,
                        new Dictionary<string, object?>
                        {
                            ["x"] = node.Value.X,
                            ["y"] = node.Value.Y,
                            ["z"] = node.Value.Z,
                        });
                }

                return nodes;
            }
        }

        private static clsNode? ReadNode(JsonElement nodeJson)
        {
            if (nodeJson.ValueKind != JsonValueKind.Object ||
                !TryReadCoordinate(nodeJson, "x", out float? x) ||
                !TryReadCoordinate(nodeJson, "y", out float? y) ||
                !TryReadCoordinate(nodeJson, "z", out float? z))
            {
                return null;
            }

            return new clsNode(x, y, z);
        }

        private static bool TryReadCoordinate(JsonElement nodeJson, string propertyName, out float? value)
        {
            value = null;
            if (!nodeJson.TryGetProperty(propertyName, out JsonElement coordinate))
            {
                return false;
            }

            if (coordinate.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (coordinate.ValueKind != JsonValueKind.Number ||
                !coordinate.TryGetSingle(out float parsed) ||
                !float.IsFinite(parsed))
            {
                return false;
            }

            value = parsed;
            return true;
        }

        private static NodeSnapshot ToSnapshot(string nodeId, clsNode node)
        {
            return new NodeSnapshot(nodeId, node.X, node.Y, node.Z);
        }
    }
}
