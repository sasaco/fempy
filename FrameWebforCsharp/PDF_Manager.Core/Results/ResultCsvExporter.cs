using System.Buffers;
using System.Globalization;
using System.Text;
using FrameWebforCsharp.Core.Analysis;
using FrameWebforCsharp.Core.Documents;

namespace FrameWebforCsharp.Core.Results;

public enum ResultExportLimitKind
{
    Rows,
    Bytes,
    Work,
}

public sealed class ResultCsvExportLimits
{
    public const int DefaultMaxRows = 100_000;
    public const int DefaultMaxBytes = 16 * 1024 * 1024;
    public const long DefaultMaxWork = 2_000_000;
    public const int HardMaxRows = 250_000;
    public const int HardMaxBytes = 64 * 1024 * 1024;
    public const long HardMaxWork = 8_000_000;

    public static ResultCsvExportLimits Default { get; } = new();

    public ResultCsvExportLimits(
        int maxRows = DefaultMaxRows,
        int maxBytes = DefaultMaxBytes,
        long maxWork = DefaultMaxWork)
    {
        if (maxRows is < 1 or > HardMaxRows)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRows));
        }

        if (maxBytes is < 1 or > HardMaxBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes));
        }

        if (maxWork is < 1 or > HardMaxWork)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWork));
        }

        MaxRows = maxRows;
        MaxBytes = maxBytes;
        MaxWork = maxWork;
    }

    public int MaxRows { get; }

    public int MaxBytes { get; }

    public long MaxWork { get; }
}

public sealed class ResultExportLimitException : ResultPresentationException
{
    public const string LimitResourceKey = "ResultExportLimitExceeded";

    public ResultExportLimitException(ResultExportLimitKind limitKind, long limit, long actual)
        : base(
            ResultPresentationErrorCode.ExportLimitExceeded,
            $"Result export {limitKind.ToString().ToLowerInvariant()} limit {limit} was exceeded by {actual}.")
    {
        LimitKind = limitKind;
        Limit = limit;
        Actual = actual;
    }

    public ResultExportLimitKind LimitKind { get; }

    public long Limit { get; }

    public long Actual { get; }

    public string ResourceKey => LimitResourceKey;
}

public sealed class ResultCsvExport
{
    internal ResultCsvExport(string suggestedFileName, int rowCount, byte[] bytes)
    {
        SuggestedFileName = suggestedFileName;
        RowCount = rowCount;
        Utf8Bytes = bytes;
    }

    public string SuggestedFileName { get; }

    public string ContentType => "text/csv; charset=utf-8";

    public int RowCount { get; }

    public int ByteCount => Utf8Bytes.Length;

    public ReadOnlyMemory<byte> Utf8Bytes { get; }

    public string Text => Encoding.UTF8.GetString(Utf8Bytes.Span);

    public void WriteTo(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(Utf8Bytes.Span);
    }

    public ValueTask WriteToAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return destination.WriteAsync(Utf8Bytes, cancellationToken);
    }
}

public sealed class ResultPickupFixedWidthExport
{
    internal ResultPickupFixedWidthExport(string suggestedFileName, int rowCount, byte[] bytes)
    {
        SuggestedFileName = suggestedFileName;
        RowCount = rowCount;
        Utf8Bytes = bytes;
    }

    public string SuggestedFileName { get; }

    public string ContentType => "text/plain; charset=utf-8";

    public int RowCount { get; }

    public int ByteCount => Utf8Bytes.Length;

    public ReadOnlyMemory<byte> Utf8Bytes { get; }

    public string Text => Encoding.UTF8.GetString(Utf8Bytes.Span);

    public void WriteTo(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(Utf8Bytes.Span);
    }

    public ValueTask WriteToAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return destination.WriteAsync(Utf8Bytes, cancellationToken);
    }
}

/// <summary>
/// Writes deterministic, long-form result CSV without using any legacy result adapter.
/// </summary>
public sealed class ResultCsvExporter
{
    public const string Header =
        "view_id,view_kind,case_id,state_kind,state_index,table,entity_id,location_id," +
        "position,component,extreme,value,source_case_id";

    public const string PickupHeader =
        "pickup_id,focus_component,member_id,maximum_source_id,minimum_source_id," +
        "station_id,end,distance,segment_length,maximum_fx,maximum_fy,maximum_fz," +
        "maximum_mx,maximum_my,maximum_mz,minimum_fx,minimum_fy,minimum_fz," +
        "minimum_mx,minimum_my,minimum_mz";

    private const int ColumnCount = 13;
    private const int PickupColumnCount = 21;
    private const int Pickup2DColumnCount = 13;
    private static readonly IReadOnlyList<(PickupFocusComponent Focus, string Symbol)> Pickup2DFocuses =
    [
        (PickupFocusComponent.Mz, "M"),
        (PickupFocusComponent.Fy, "S"),
        (PickupFocusComponent.Fx, "N"),
    ];

    public ResultCsvExport ExportBaseStatic(
        StaticAnalysisResult result,
        ResultCsvExportLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return ExportSnapshot(
            result.CaseId,
            "base_static",
            result.CaseId,
            result.State.Kind.ToString().ToLowerInvariant(),
            result.State.Index,
            result.NodeDisplacements,
            result.SupportReactions,
            result.MemberSectionForces,
            result.ShellResults,
            result.SolidResults,
            limits ?? ResultCsvExportLimits.Default);
    }

    public ResultCsvExport ExportPickup(
        PresentedStaticResult result,
        ResultCsvExportLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Kind != DerivedResultKind.Pickup)
        {
            throw new ResultPresentationException(
                ResultPresentationErrorCode.InvalidDefinition,
                $"Derived result '{result.Id}' is not a PICKUP result.");
        }

        PickupEngineeringEnvelope envelope = result.PickupEnvelope ??
            throw new ResultPresentationException(
                ResultPresentationErrorCode.InvalidDefinition,
                $"PICKUP result '{result.Id}' has no engineering envelope.");
        ResultCsvExportLimits effectiveLimits = limits ?? ResultCsvExportLimits.Default;
        int rowCount = CheckedRowCount(checked((long)envelope.MemberEnds.Count * 6));
        Preflight(rowCount, PickupColumnCount, effectiveLimits);
        BoundedCsvWriter writer = new(effectiveLimits.MaxBytes, PickupColumnCount);
        writer.WriteHeader(PickupHeader);

        foreach (PickupFocusComponent focus in Enum.GetValues<PickupFocusComponent>())
        {
            foreach (PickupMemberEndEnvelope memberEnd in envelope.MemberEnds)
            {
                PickupForceComponentEnvelope component = memberEnd.Components[(int)focus];
                writer.WriteRow(
                    CsvField.Text(envelope.PickupId),
                    CsvField.Constant(FocusName(focus)),
                    CsvField.Text(memberEnd.MemberId),
                    CsvField.Text(component.Maximum.SourceId),
                    CsvField.Text(component.Minimum.SourceId),
                    CsvField.Text(memberEnd.StationId),
                    CsvField.Constant(memberEnd.End.ToString()),
                    CsvField.Number(memberEnd.Distance),
                    CsvField.Number(memberEnd.SegmentLength),
                    CsvField.Number(component.Maximum.Components.Fx),
                    CsvField.Number(component.Maximum.Components.Fy),
                    CsvField.Number(component.Maximum.Components.Fz),
                    CsvField.Number(component.Maximum.Components.Mx),
                    CsvField.Number(component.Maximum.Components.My),
                    CsvField.Number(component.Maximum.Components.Mz),
                    CsvField.Number(component.Minimum.Components.Fx),
                    CsvField.Number(component.Minimum.Components.Fy),
                    CsvField.Number(component.Minimum.Components.Fz),
                    CsvField.Number(component.Minimum.Components.Mx),
                    CsvField.Number(component.Minimum.Components.My),
                    CsvField.Number(component.Minimum.Components.Mz));
            }
        }

        return writer.Complete(FileName(envelope.PickupId, "pickup-3d"), rowCount);
    }

    /// <summary>
    /// Exports the legacy two-dimensional M/S/N engineering projection as headerless fixed-width
    /// records. Fields are right-aligned and each record is terminated by CRLF.
    /// </summary>
    public ResultPickupFixedWidthExport ExportPickup2D(
        PresentedStaticResult result,
        ResultCsvExportLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Kind != DerivedResultKind.Pickup || result.PickupEnvelope is null)
        {
            throw new ResultPresentationException(
                ResultPresentationErrorCode.InvalidDefinition,
                $"Derived result '{result.Id}' has no PICKUP engineering envelope.");
        }

        ResultCsvExportLimits effectiveLimits = limits ?? ResultCsvExportLimits.Default;
        PickupEngineeringEnvelope envelope = result.PickupEnvelope;
        int rowCount = CheckedRowCount(checked((long)envelope.MemberEnds.Count * 3));
        Preflight(rowCount, Pickup2DColumnCount, effectiveLimits);
        BoundedTextWriter writer = new(effectiveLimits.MaxBytes);
        foreach ((PickupFocusComponent Focus, string Symbol) focus in Pickup2DFocuses)
        {
            foreach (PickupMemberEndEnvelope memberEnd in envelope.MemberEnds)
            {
                PickupForceComponentEnvelope component = memberEnd.Components[(int)focus.Focus];
                writer.Write(FixedText(envelope.PickupId, 5));
                writer.Write(FixedText(focus.Symbol, 5));
                writer.Write(FixedText(memberEnd.MemberId, 5));
                writer.Write(FixedText(component.Maximum.SourceId, 5));
                writer.Write(FixedText(component.Minimum.SourceId, 5));
                writer.Write(FixedText(memberEnd.StationId, 5));
                writer.Write(FixedNumber(memberEnd.Distance, 10, "F3"));
                writer.Write(FixedNumber(component.Maximum.Components.Mz, 10, "F2"));
                writer.Write(FixedNumber(component.Maximum.Components.Fy, 10, "F2"));
                writer.Write(FixedNumber(component.Maximum.Components.Fx, 10, "F2"));
                writer.Write(FixedNumber(component.Minimum.Components.Mz, 10, "F2"));
                writer.Write(FixedNumber(component.Minimum.Components.Fy, 10, "F2"));
                writer.Write(FixedNumber(component.Minimum.Components.Fx, 10, "F2"));
                writer.Write("\r\n");
            }
        }

        return writer.Complete(PickupFileName(envelope.PickupId), rowCount);
    }

    public ResultCsvExport ExportMovingLoad(
        MovingLoadEnvelope envelope,
        ResultCsvExportLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ResultCsvExportLimits effectiveLimits = limits ?? ResultCsvExportLimits.Default;
        int rowCount = CountMovingLoadRows(envelope);
        Preflight(rowCount, ColumnCount, effectiveLimits);

        BoundedCsvWriter writer = new(effectiveLimits.MaxBytes, ColumnCount);
        writer.WriteHeader(Header);
        foreach (NodeDisplacementEnvelope node in envelope.NodeDisplacements)
        {
            WriteDisplacementEnvelopes(writer, envelope.DefinitionId, node);
        }

        foreach (SupportReactionEnvelope reaction in envelope.SupportReactions)
        {
            WriteForceEnvelopes(
                writer,
                envelope.DefinitionId,
                "support_reaction",
                reaction.NodeId,
                string.Empty,
                string.Empty,
                reaction.Components);
        }

        foreach (MemberSectionForceEnvelope member in envelope.MemberSectionForces)
        {
            foreach (MemberSegmentEnvelope segment in member.Segments)
            {
                WriteForceEnvelopes(
                    writer,
                    envelope.DefinitionId,
                    "member_section_force",
                    member.MemberId,
                    segment.SegmentId,
                    "I",
                    segment.IEnd);
                WriteForceEnvelopes(
                    writer,
                    envelope.DefinitionId,
                    "member_section_force",
                    member.MemberId,
                    segment.SegmentId,
                    "J",
                    segment.JEnd);
            }
        }

        if (envelope.MemberForceExtrema.HasValues)
        {
            WriteMemberGlobalExtrema(writer, envelope.DefinitionId, envelope.MemberForceExtrema);
        }

        return writer.Complete(FileName(envelope.DefinitionId, "moving-load"), rowCount);
    }

    private static ResultCsvExport ExportSnapshot(
        string viewId,
        string viewKind,
        string caseId,
        string stateKind,
        int stateIndex,
        IReadOnlyList<NodeDisplacement> nodes,
        IReadOnlyList<SupportReaction> reactions,
        IReadOnlyList<MemberSectionForces> members,
        IReadOnlyList<ShellResult> shells,
        IReadOnlyList<SolidResult> solids,
        ResultCsvExportLimits limits)
    {
        int rowCount = CountSnapshotRows(nodes, reactions, members, shells, solids);
        Preflight(rowCount, ColumnCount, limits);
        BoundedCsvWriter writer = new(limits.MaxBytes, ColumnCount);
        writer.WriteHeader(Header);

        foreach (NodeDisplacement node in nodes)
        {
            WriteComponents(writer, viewId, viewKind, caseId, stateKind, stateIndex,
                "node_displacement", node.NodeId, string.Empty, string.Empty,
                [
                    ("dx", node.Components.Dx), ("dy", node.Components.Dy),
                    ("dz", node.Components.Dz), ("rx", node.Components.Rx),
                    ("ry", node.Components.Ry), ("rz", node.Components.Rz),
                ]);
        }

        foreach (SupportReaction reaction in reactions)
        {
            WriteComponents(writer, viewId, viewKind, caseId, stateKind, stateIndex,
                "support_reaction", reaction.NodeId, string.Empty, string.Empty,
                ForceValues(reaction.Components));
        }

        foreach (MemberSectionForces member in members)
        {
            foreach (MemberSegmentResult segment in member.Segments)
            {
                WriteComponents(writer, viewId, viewKind, caseId, stateKind, stateIndex,
                    "member_section_force", member.MemberId, segment.SegmentId, "I",
                    ForceValues(segment.IEnd));
                WriteComponents(writer, viewId, viewKind, caseId, stateKind, stateIndex,
                    "member_section_force", member.MemberId, segment.SegmentId, "J",
                    ForceValues(segment.JEnd));
            }
        }

        foreach (ShellResult shell in shells)
        {
            foreach (ShellResultLocation location in shell.Locations)
            {
                WriteComponents(writer, viewId, viewKind, caseId, stateKind, stateIndex,
                    "shell_result", shell.ElementId, location.LocationId, "membrane",
                    [("nx", location.MembraneForce.Nx), ("ny", location.MembraneForce.Ny),
                        ("nxy", location.MembraneForce.Nxy)]);
                WriteComponents(writer, viewId, viewKind, caseId, stateKind, stateIndex,
                    "shell_result", shell.ElementId, location.LocationId, "bending",
                    [("mx", location.BendingMoment.Mx), ("my", location.BendingMoment.My),
                        ("mxy", location.BendingMoment.Mxy)]);
                WriteComponents(writer, viewId, viewKind, caseId, stateKind, stateIndex,
                    "shell_result", shell.ElementId, location.LocationId, "shear",
                    [("qx", location.TransverseShear.Qx), ("qy", location.TransverseShear.Qy)]);
                WriteComponents(writer, viewId, viewKind, caseId, stateKind, stateIndex,
                    "shell_result", shell.ElementId, location.LocationId, "top_stress",
                    [("sx", location.TopStress.Sx), ("sy", location.TopStress.Sy),
                        ("txy", location.TopStress.Txy)]);
                WriteComponents(writer, viewId, viewKind, caseId, stateKind, stateIndex,
                    "shell_result", shell.ElementId, location.LocationId, "bottom_stress",
                    [("sx", location.BottomStress.Sx), ("sy", location.BottomStress.Sy),
                        ("txy", location.BottomStress.Txy)]);
            }
        }

        foreach (SolidResult solid in solids)
        {
            foreach (SolidResultLocation location in solid.Locations)
            {
                WriteComponents(writer, viewId, viewKind, caseId, stateKind, stateIndex,
                    "solid_result", solid.ElementId, location.LocationId, "stress",
                    [
                        ("sx", location.Stress.Sx), ("sy", location.Stress.Sy),
                        ("sz", location.Stress.Sz), ("txy", location.Stress.Txy),
                        ("tyz", location.Stress.Tyz), ("tzx", location.Stress.Tzx),
                    ]);
                WriteComponents(writer, viewId, viewKind, caseId, stateKind, stateIndex,
                    "solid_result", solid.ElementId, location.LocationId, "strain",
                    [
                        ("ex", location.Strain.Ex), ("ey", location.Strain.Ey),
                        ("ez", location.Strain.Ez), ("gxy", location.Strain.Gxy),
                        ("gyz", location.Strain.Gyz), ("gzx", location.Strain.Gzx),
                    ]);
            }
        }

        return writer.Complete(FileName(viewId, viewKind), rowCount);
    }

    private static void WriteComponents(
        BoundedCsvWriter writer,
        string viewId,
        string viewKind,
        string caseId,
        string stateKind,
        int stateIndex,
        string table,
        string entityId,
        string locationId,
        string position,
        IReadOnlyList<(string Component, double Value)> components)
    {
        foreach ((string component, double value) in components)
        {
            writer.WriteRow(
                CsvField.Text(viewId), CsvField.Constant(viewKind), CsvField.Text(caseId),
                CsvField.Constant(stateKind), CsvField.Constant(stateIndex.ToString(CultureInfo.InvariantCulture)),
                CsvField.Constant(table), CsvField.Text(entityId), CsvField.Text(locationId),
                CsvField.Constant(position), CsvField.Constant(component), CsvField.Constant("value"),
                CsvField.Number(value), CsvField.Text(caseId));
        }
    }

    private static void WriteDisplacementEnvelopes(
        BoundedCsvWriter writer,
        string viewId,
        NodeDisplacementEnvelope node)
    {
        WriteScalarEnvelope(writer, viewId, "node_displacement", node.NodeId, string.Empty, string.Empty,
            "dx", node.Components.Dx);
        WriteScalarEnvelope(writer, viewId, "node_displacement", node.NodeId, string.Empty, string.Empty,
            "dy", node.Components.Dy);
        WriteScalarEnvelope(writer, viewId, "node_displacement", node.NodeId, string.Empty, string.Empty,
            "dz", node.Components.Dz);
        WriteScalarEnvelope(writer, viewId, "node_displacement", node.NodeId, string.Empty, string.Empty,
            "rx", node.Components.Rx);
        WriteScalarEnvelope(writer, viewId, "node_displacement", node.NodeId, string.Empty, string.Empty,
            "ry", node.Components.Ry);
        WriteScalarEnvelope(writer, viewId, "node_displacement", node.NodeId, string.Empty, string.Empty,
            "rz", node.Components.Rz);
    }

    private static void WriteForceEnvelopes(
        BoundedCsvWriter writer,
        string viewId,
        string table,
        string entityId,
        string locationId,
        string position,
        ForceEnvelopeComponents components)
    {
        WriteScalarEnvelope(writer, viewId, table, entityId, locationId, position, "fx", components.Fx);
        WriteScalarEnvelope(writer, viewId, table, entityId, locationId, position, "fy", components.Fy);
        WriteScalarEnvelope(writer, viewId, table, entityId, locationId, position, "fz", components.Fz);
        WriteScalarEnvelope(writer, viewId, table, entityId, locationId, position, "mx", components.Mx);
        WriteScalarEnvelope(writer, viewId, table, entityId, locationId, position, "my", components.My);
        WriteScalarEnvelope(writer, viewId, table, entityId, locationId, position, "mz", components.Mz);
    }

    private static void WriteScalarEnvelope(
        BoundedCsvWriter writer,
        string viewId,
        string table,
        string entityId,
        string locationId,
        string position,
        string component,
        ScalarEnvelope envelope)
    {
        WriteExtreme(writer, viewId, table, entityId, locationId, position, component,
            "maximum", envelope.Maximum);
        WriteExtreme(writer, viewId, table, entityId, locationId, position, component,
            "minimum", envelope.Minimum);
        WriteExtreme(writer, viewId, table, entityId, locationId, position, component,
            "absolute_maximum", envelope.AbsoluteMaximum);
    }

    private static void WriteExtreme(
        BoundedCsvWriter writer,
        string viewId,
        string table,
        string entityId,
        string locationId,
        string position,
        string component,
        string extreme,
        EnvelopeExtreme value)
        => writer.WriteRow(
            CsvField.Text(viewId), CsvField.Constant("moving_load"), CsvField.Text(string.Empty),
            CsvField.Constant("static"), CsvField.Constant("0"), CsvField.Constant(table),
            CsvField.Text(entityId), CsvField.Text(locationId), CsvField.Constant(position),
            CsvField.Constant(component), CsvField.Constant(extreme), CsvField.Number(value.Value),
            CsvField.Text(value.CaseId));

    private static void WriteMemberGlobalExtrema(
        BoundedCsvWriter writer,
        string viewId,
        MemberForceExtremaComponents components)
    {
        WriteMemberScalarExtrema(writer, viewId, "fx", components.Fx);
        WriteMemberScalarExtrema(writer, viewId, "fy", components.Fy);
        WriteMemberScalarExtrema(writer, viewId, "fz", components.Fz);
        WriteMemberScalarExtrema(writer, viewId, "mx", components.Mx);
        WriteMemberScalarExtrema(writer, viewId, "my", components.My);
        WriteMemberScalarExtrema(writer, viewId, "mz", components.Mz);
    }

    private static void WriteMemberScalarExtrema(
        BoundedCsvWriter writer,
        string viewId,
        string component,
        MemberForceScalarExtrema extrema)
    {
        WriteMemberExtreme(writer, viewId, component, "maximum", extrema.Maximum);
        WriteMemberExtreme(writer, viewId, component, "minimum", extrema.Minimum);
        WriteMemberExtreme(writer, viewId, component, "absolute_maximum", extrema.AbsoluteMaximum);
    }

    private static void WriteMemberExtreme(
        BoundedCsvWriter writer,
        string viewId,
        string component,
        string extreme,
        MemberForceExtreme value)
        => writer.WriteRow(
            CsvField.Text(viewId), CsvField.Constant("moving_load"), CsvField.Text(string.Empty),
            CsvField.Constant("static"), CsvField.Constant("0"), CsvField.Constant("member_force_extrema"),
            CsvField.Text(value.MemberId), CsvField.Text(value.SegmentId),
            CsvField.Constant(value.End.ToString()), CsvField.Constant(component),
            CsvField.Constant(extreme), CsvField.Number(value.Value), CsvField.Text(value.CaseId));

    private static IReadOnlyList<(string Component, double Value)> ForceValues(ForceComponents value) =>
    [
        ("fx", value.Fx), ("fy", value.Fy), ("fz", value.Fz),
        ("mx", value.Mx), ("my", value.My), ("mz", value.Mz),
    ];

    private static int CountSnapshotRows(
        IReadOnlyList<NodeDisplacement> nodes,
        IReadOnlyList<SupportReaction> reactions,
        IReadOnlyList<MemberSectionForces> members,
        IReadOnlyList<ShellResult> shells,
        IReadOnlyList<SolidResult> solids)
    {
        long count = checked((long)nodes.Count * 6 + (long)reactions.Count * 6);
        foreach (MemberSectionForces member in members)
        {
            count = checked(count + (long)member.Segments.Count * 12);
        }

        foreach (ShellResult shell in shells)
        {
            count = checked(count + (long)shell.Locations.Count * 14);
        }

        foreach (SolidResult solid in solids)
        {
            count = checked(count + (long)solid.Locations.Count * 12);
        }

        return CheckedRowCount(count);
    }

    private static int CountMovingLoadRows(MovingLoadEnvelope envelope)
    {
        long count = checked((long)envelope.NodeDisplacements.Count * 18 +
            (long)envelope.SupportReactions.Count * 18);
        foreach (MemberSectionForceEnvelope member in envelope.MemberSectionForces)
        {
            count = checked(count + (long)member.Segments.Count * 36);
        }

        if (envelope.MemberForceExtrema.HasValues)
        {
            count = checked(count + 18);
        }

        return CheckedRowCount(count);
    }

    private static int CheckedRowCount(long count)
    {
        if (count > int.MaxValue)
        {
            throw new ResultExportLimitException(ResultExportLimitKind.Rows, int.MaxValue, count);
        }

        return (int)count;
    }

    private static void Preflight(int rowCount, int columnCount, ResultCsvExportLimits limits)
    {
        if (rowCount > limits.MaxRows)
        {
            throw new ResultExportLimitException(ResultExportLimitKind.Rows, limits.MaxRows, rowCount);
        }

        long work = checked((long)rowCount * columnCount);
        if (work > limits.MaxWork)
        {
            throw new ResultExportLimitException(ResultExportLimitKind.Work, limits.MaxWork, work);
        }
    }

    private static string Format(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ResultPresentationException(
                ResultPresentationErrorCode.InvalidDefinition,
                "Result export contains a non-finite value.");
        }

        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string FocusName(PickupFocusComponent focus)
        => focus.ToString().ToLowerInvariant();

    private static string FixedNumber(double value, int width, string format)
    {
        if (!double.IsFinite(value))
        {
            throw new ResultPresentationException(
                ResultPresentationErrorCode.InvalidDefinition,
                "Result export contains a non-finite value.");
        }

        return FixedText(value.ToString(format, CultureInfo.InvariantCulture), width);
    }

    private static string FixedText(string value, int width)
    {
        string singleLine = string.Concat(value.Select(character =>
            char.IsControl(character) ? ' ' : character));
        return singleLine.Length >= width
            ? singleLine[^width..]
            : singleLine.PadLeft(width, ' ');
    }

    private static string FileName(string id, string suffix)
    {
        StringBuilder builder = new(capacity: 80);
        foreach (char value in id)
        {
            if (builder.Length == 64)
            {
                break;
            }

            builder.Append(char.IsLetterOrDigit(value) || value is '.' or '-' or '_'
                ? value
                : '_');
        }

        if (builder.Length == 0)
        {
            builder.Append("result");
        }

        return $"{builder}-{suffix}.csv";
    }

    private static string PickupFileName(string id)
    {
        StringBuilder builder = new(capacity: 80);
        foreach (char value in id)
        {
            if (builder.Length == 64)
            {
                break;
            }

            builder.Append(char.IsLetterOrDigit(value) || value is '.' or '-' or '_'
                ? value
                : '_');
        }

        if (builder.Length == 0)
        {
            builder.Append("result");
        }

        return $"{builder}-pickup-2d.pik";
    }

    private readonly record struct CsvField(string Value, bool IsUntrustedText)
    {
        internal static CsvField Text(string value) => new(value, true);

        internal static CsvField Constant(string value) => new(value, false);

        internal static CsvField Number(double value) => new(Format(value), false);
    }

    private sealed class BoundedCsvWriter
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly ArrayBufferWriter<byte> _buffer = new();
        private readonly int _maxBytes;
        private readonly int _columnCount;

        internal BoundedCsvWriter(int maxBytes, int columnCount)
        {
            _maxBytes = maxBytes;
            _columnCount = columnCount;
        }

        internal void WriteHeader(string header)
        {
            WriteRaw(header);
            WriteRaw("\r\n");
        }

        internal void WriteRow(params CsvField[] fields)
        {
            if (fields.Length != _columnCount)
            {
                throw new InvalidOperationException($"CSV rows must contain {_columnCount} fields.");
            }

            for (int index = 0; index < fields.Length; index++)
            {
                if (index > 0)
                {
                    WriteRaw(",");
                }

                WriteField(fields[index]);
            }

            WriteRaw("\r\n");
        }

        internal ResultCsvExport Complete(string fileName, int rowCount)
            => new(fileName, rowCount, _buffer.WrittenSpan.ToArray());

        private void WriteField(CsvField field)
        {
            string value = field.Value;
            bool neutralize = field.IsUntrustedText && RequiresFormulaNeutralization(value);
            bool quote = false;
            foreach (char character in value)
            {
                if (character is ',' or '"' or '\r' or '\n')
                {
                    quote = true;
                    break;
                }
            }
            int quoteCount = 0;
            if (quote)
            {
                foreach (char character in value)
                {
                    if (character == '"') quoteCount++;
                }
            }

            long required = (long)Utf8.GetByteCount(value) + (neutralize ? 1L : 0L) +
                (quote ? 2L + quoteCount : 0L);
            EnsureCapacity(required);
            if (!quote)
            {
                if (neutralize)
                {
                    WriteByte((byte)'\'');
                }

                WriteUtf8(value.AsSpan());
                return;
            }

            WriteByte((byte)'"');
            if (neutralize)
            {
                WriteByte((byte)'\'');
            }

            int start = 0;
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] != '"') continue;
                WriteUtf8(value.AsSpan(start, index - start));
                WriteByte((byte)'"');
                WriteByte((byte)'"');
                start = index + 1;
            }

            WriteUtf8(value.AsSpan(start));
            WriteByte((byte)'"');
        }

        private static bool RequiresFormulaNeutralization(string value)
        {
            if (value.Length == 0)
            {
                return false;
            }

            if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n')
            {
                return true;
            }

            int index = 0;
            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                if (value[index] is '\t' or '\r' or '\n')
                {
                    return true;
                }

                index++;
            }

            return index < value.Length && value[index] is '=' or '+' or '-' or '@';
        }

        private void WriteRaw(string value)
        {
            int bytes = Utf8.GetByteCount(value);
            EnsureCapacity(bytes);
            WriteUtf8(value.AsSpan());
        }

        private void WriteUtf8(ReadOnlySpan<char> value)
        {
            int byteCount = Utf8.GetByteCount(value);
            Span<byte> destination = _buffer.GetSpan(byteCount)[..byteCount];
            int written = Utf8.GetBytes(value, destination);
            _buffer.Advance(written);
        }

        private void WriteByte(byte value)
        {
            Span<byte> destination = _buffer.GetSpan(1);
            destination[0] = value;
            _buffer.Advance(1);
        }

        private void EnsureCapacity(long additionalBytes)
        {
            long actual = checked((long)_buffer.WrittenCount + additionalBytes);
            if (actual > _maxBytes)
            {
                throw new ResultExportLimitException(ResultExportLimitKind.Bytes, _maxBytes, actual);
            }
        }
    }

    private sealed class BoundedTextWriter
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly ArrayBufferWriter<byte> _buffer = new();
        private readonly int _maxBytes;

        internal BoundedTextWriter(int maxBytes)
        {
            _maxBytes = maxBytes;
        }

        internal void Write(string value)
        {
            int byteCount = Utf8.GetByteCount(value);
            long actual = checked((long)_buffer.WrittenCount + byteCount);
            if (actual > _maxBytes)
            {
                throw new ResultExportLimitException(ResultExportLimitKind.Bytes, _maxBytes, actual);
            }

            Span<byte> destination = _buffer.GetSpan(byteCount)[..byteCount];
            int written = Utf8.GetBytes(value.AsSpan(), destination);
            _buffer.Advance(written);
        }

        internal ResultPickupFixedWidthExport Complete(string fileName, int rowCount)
            => new(fileName, rowCount, _buffer.WrittenSpan.ToArray());
    }
}
